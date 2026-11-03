using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;

namespace SiteAdaptation.Common.SetUp
{
    // R373, R374, R382, R383. Button 1 Set up site as a body of the flow. The preview reads the document and fills the dialog, the
    // dialog is its confirm, the shared parameter file is set outside the transaction, Apply sets only what differs in the order of 03a
    // steps 1 to 6, the read back holds every value within half a mm (R331) and site.json goes to its temporary name. All judgement is
    // here, the Revit side only reads and carries it out
    public sealed class SetUpRun
    {
        public const string SiteFile = "site.json";
        public const string SiteFormat = "site-adaptation-site-facts";
        public const int SiteFormatVersion = 1;

        // docs/design/06-settings.md, the schema of the Settings entity on ProjectInformation, a sign of an existing site file
        public const string SettingsSchema = "SiteAdaptation_Settings";

        private readonly ISiteDocument document;
        private readonly ISetUpSiteDialog dialog;
        private readonly ITagStep tags;
        private readonly Func<string, string> readText;
        private readonly string settingsInput;
        private readonly string sheetInput;

        private Func<ProjectSettings> settingsNow;
        private SettingsStore store;
        private Func<DateTime> clock;

        private JObject config;
        private JObject rules;
        private JObject position;
        private ParameterSheet sheet;
        private string configSheet;
        private string otherSheet;
        private double slope;
        private SetUpTarget target;
        private string readBack;
        private Func<RunContext, CoordinateRead> readCad;
        private Func<StepRun> readLastRun;
        private CadSite cad;

        // settingsInput is the name of the input of the Settings file in the pane, sheetInput that of the parameter sheet, null when the
        // pane gives none
        public SetUpRun(ISiteDocument document, ISetUpSiteDialog dialog, ITagStep tags, Func<string, string> readText, string settingsInput, string sheetInput)
        {
            this.document = document;
            this.dialog = dialog;
            this.tags = tags;
            this.readText = readText;
            this.settingsInput = settingsInput;
            this.sheetInput = sheetInput;
        }

        // J1, J19, R367. The real run writes the Settings entity with its record, the settings of the pane at the time of Apply and
        // lastRuns 1. With no store set, as the test host gives none, each run lists SettingsNotWritten as before
        public SetUpRun Recording(Func<ProjectSettings> settings, ISettingsEntity entity, Func<DateTime> utcNow)
        {
            settingsNow = settings ?? throw new ArgumentNullException(nameof(settings));
            store = new SettingsStore(entity);
            clock = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            return this;
        }

        // Q158. The last run of step 1 as the pane reads it for the session, the one a press holds to, so a press never cancels while the pane
        // shows the step not holding. Unset takes the last run of the entity
        public SetUpRun ReadingLastRun(Func<StepRun> lastRun)
        {
            readLastRun = lastRun;
            return this;
        }

        // R454 to R456. The CAD read of coordinates.source cad, the worker command crs in the add-in
        public SetUpRun ReadingCad(Func<RunContext, CoordinateRead> read)
        {
            readCad = read;
            return this;
        }

        // The last view the run made, the one the dialog shows or the one after Apply
        public SetUpSiteView View { get; private set; }

        public SetUpTarget Target
        {
            get { return target; }
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Confirm = Confirm, Outside = Outside, Apply = Apply, ReadBack = context => readBack };
        }

        private RunSummary Preview(RunContext context)
        {
            RunInput settings = context.Inputs.FirstOrDefault(input => input.Name == settingsInput);
            if (settings == null || string.IsNullOrEmpty(settings.Path))
            {
                throw new InvalidOperationException("the run names no " + settingsInput + " file");
            }
            config = Json(settings.Path, "the settings file");
            DesignValues.UseConfigOf(config);
            string profile = config["profile"]?.Type == JTokenType.String ? (string)config["profile"] : throw new InvalidDataException("config names no rules profile, profile (R277)");
            // The paths of config are relative to the repo root, the folder above the one config is in (its note)
            string rulesPath = Path.IsPathRooted(profile) ? profile : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(settings.Path))), profile);
            rules = Json(rulesPath, "the rules profile");
            position = Json(PositionSettings.From(config).File, "the position file");
            cad = SiteFromCad.Load(config, context, readCad);
            slope = RampSlopes.Default(rules, null);
            // R461. The parameter sheet addin.parameterSheet of config names, the one every button reads. A sheet the pane names that is
            // another file stops the run, so button 1 never binds GUIDs button 7 does not write by
            configSheet = Path.GetFullPath(ParameterSheet.PathOf(settings.Path, config));
            sheet = ParameterSheet.FromFile(configSheet);
            RunInput pane = sheetInput == null ? null : context.Inputs.FirstOrDefault(input => input.Name == sheetInput && !string.IsNullOrEmpty(input.Path));
            otherSheet = pane == null || SameSheet(pane.Path) ? null : pane.Path;
            target = Make();
            View = Viewed(context.DryRun ? (Func<double[], double[]>)DryShared : null);
            return Summary(View);
        }

        private bool Confirm(RunContext context, RunSummary summary)
        {
            if (dialog == null)
            {
                throw new InvalidOperationException("the Set up site window is not set");
            }
            var callbacks = new SetUpSiteCallbacks
            {
                DryRun = chosen =>
                {
                    slope = chosen;
                    target = Make();
                    return View = Viewed(DryShared);
                },
                SheetChosen = path =>
                {
                    otherSheet = SameSheet(path) ? null : path;
                    return View = Viewed(View.DryRunDone ? (Func<double[], double[]>)DryShared : null);
                },
            };
            SetUpSiteAnswer answer = dialog.Show(View, callbacks);
            if (answer == null || !answer.Apply)
            {
                return false;
            }
            if (answer.ParameterSheet != null)
            {
                otherSheet = SameSheet(answer.ParameterSheet) ? null : answer.ParameterSheet;
            }
            if (target.RampSlopeChoices.Contains(answer.RampSlope))
            {
                slope = answer.RampSlope;
            }
            target = Make();
            List<string> stops = SheetStops(SetUpPlanner.Stops(document.Read(), target));
            if (stops.Count > 0)
            {
                throw new InvalidOperationException(string.Join(". ", stops));
            }
            // Q158. With nothing to set the run still marks step 1 done, so the steps after it go on after a config edit. A step that holds
            // already records nothing, since a later record would make the steps after it run again (R366). The window said the line
            return SetUpPlanner.Changes(document.Read(), target).Any || !StepHolds(context);
        }

        // Q158. The step holds by StepStates on the run the pane reads and the hash now, the entity holds the ticks and rules file of the window, so a
        // press loses nothing on a reopen, and site.json holds the facts of the model. With no store or no entity there is nothing to hold to
        private bool StepHolds(RunContext context)
        {
            if (store == null)
            {
                return false;
            }
            ProjectSettings held;
            try
            {
                held = store.Load();
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is Newtonsoft.Json.JsonException)
            {
                return false;
            }
            StepRun run = held == null ? null : readLastRun != null ? readLastRun() : StepRun.FromSettings(held, context.Button);
            if (run == null)
            {
                return false;
            }
            var steps = new Dictionary<int, StepNow> { [context.Button] = new StepNow(run, StepHash.Combine(context.InputHashes), StepHash.OnDisk(run.Outputs.Keys)) };
            StepStateKind line = StepStates.Of(true, new SettingsScope(), steps)[context.Button - 1].Kind;
            return (line == StepStateKind.Done || line == StepStateKind.DoneWithIssues) && SameSetup(held, settingsNow())
                && SiteFileAgrees(Path.Combine(context.WorkFolder, SiteFile), document.Read());
        }

        private static bool SameSetup(ProjectSettings held, ProjectSettings now)
        {
            return string.Equals(held.RulesFile ?? "", now.RulesFile ?? "", StringComparison.Ordinal)
                && (held.CurrentFiles ?? new List<string>()).SequenceEqual(now.CurrentFiles ?? new List<string>(), StringComparer.Ordinal);
        }

        // Q158, CHOSEN by the main session: a file left from another state of the model would give the build a wrong survey point, angle and levels,
        // so a press holds site.json to the facts of the model, in the tolerance of the diff, whatever its run id. A file that does not read disagrees
        public static bool SiteFileAgrees(string path, SiteState now)
        {
            if (!File.Exists(path))
            {
                return false;
            }
            JObject file;
            try
            {
                file = JObject.Parse(WorkFiles.ReadText(path));
            }
            catch (Exception failure) when (failure is Newtonsoft.Json.JsonException || failure is IOException || failure is UnauthorizedAccessException)
            {
                return false;
            }
            JObject facts = SiteJson(now, null);
            facts["runId"] = file["runId"]?.DeepClone();
            return SameFacts(file, facts, null);
        }

        private static bool SameFacts(JToken have, JToken want, string name)
        {
            if (want is JObject wanted)
            {
                return have is JObject held && held.Count == wanted.Count && wanted.Properties().All(item => held[item.Name] != null && SameFacts(held[item.Name], item.Value, item.Name));
            }
            if (want is JArray list)
            {
                return have is JArray items && items.Count == list.Count && list.Select((item, at) => SameFacts(items[at], item, name)).All(same => same);
            }
            if (want.Type == JTokenType.Float || want.Type == JTokenType.Integer)
            {
                if (have == null || (have.Type != JTokenType.Float && have.Type != JTokenType.Integer))
                {
                    return false;
                }
                double gap = Math.Abs((double)have - (double)want);
                // Written so a value that is no number never counts as the same
                return name == "angleDeg" || name == "angleToTrueNorthDeg" ? gap <= SetUpPlanner.SameDeg : gap * 1000.0 <= SetUpPlanner.SameMm;
            }
            return JToken.DeepEquals(have, want);
        }

        // 03a The shared parameter file: shared_<run id>.txt in the work folder with the group and the definitions of the sheet, the
        // application set to it before the transaction and put back after it, the file deleted. Nothing is set when no parameter is bound
        private IDisposable Outside(RunContext context)
        {
            SetUpChanges changes = SetUpPlanner.Changes(document.Read(), target);
            if (changes.Parameters.Count == 0)
            {
                return null;
            }
            string path = Path.Combine(context.WorkFolder, SharedParameterText.FileName(context.RunId));
            if (File.Exists(path))
            {
                throw new IOException("the shared parameter file of this run is there already: " + path);
            }
            Directory.CreateDirectory(context.WorkFolder);
            File.WriteAllText(path, SharedParameterText.Of(target.ParameterGroup, target.Parameters.Parameters), SharedParameterText.FileEncoding);
            IDisposable setting;
            try
            {
                setting = document.UseParameterFile(path);
            }
            catch
            {
                File.Delete(path);
                throw;
            }
            return new Scope(() =>
            {
                try
                {
                    setting.Dispose();
                }
                finally
                {
                    File.Delete(path);
                }
            });
        }

        private IReadOnlyList<string> Apply(RunContext context)
        {
            readBack = null;
            SiteState now = document.Read();
            List<string> stops = SheetStops(SetUpPlanner.Stops(now, target));
            if (stops.Count > 0)
            {
                throw new InvalidOperationException(string.Join(". ", stops));
            }
            SetUpChanges changes = SetUpPlanner.Changes(now, target);
            int changing = View == null ? 0 : View.WillSet.Count(row => row.Differs);
            if (changes.Units)
            {
                document.SetUnits(target.Units);
            }
            if (changes.Location)
            {
                document.SetLocation(target);
            }
            if (changes.Position || changes.SurveyPoint || changes.BasePoint)
            {
                document.SetPosition(target, changes);
            }
            if (changes.LevelBase || changes.Levels.Count > 0)
            {
                document.SetLevels(target, changes);
            }
            if (changes.FloorTypes.Count > 0 || changes.WallTypes.Count > 0)
            {
                document.MakeTypes(target, changes);
            }
            if (changes.Parameters.Count > 0)
            {
                document.BindParameters(target, changes.Parameters);
            }
            // Q158. Nothing to set changes no element, so nothing is tagged
            var issues = changes.Any ? new List<string>(tags.Tag(context.RunId, context.Button) ?? new string[0]) : new List<string>();

            SiteState after = document.Read();
            View = SetUpPlanner.View(after, target, document.SharedNow);
            var differs = new List<string>();
            string values = SetUpPlanner.ReadBack(after, target);
            if (values != null)
            {
                differs.Add(values);
            }
            differs.AddRange(View.CheckPoints.Where(row => !row.Pass).Select(row => row.Point + " " + SetUpTexts.Number(row.DiffMm ?? double.NaN, target.GapDecimals) + " mm"));
            if (differs.Count > 0)
            {
                readBack = string.Join(", ", differs);
                return issues;
            }
            // Q158. With nothing set a file that holds the facts of the model stays as it is, one missing or off them is written again
            if (changes.Any || !SiteFileAgrees(Path.Combine(context.WorkFolder, SiteFile), after))
            {
                context.WriteText(SiteFile, SiteJson(after, context.RunId).ToString(Newtonsoft.Json.Formatting.Indented));
            }
            if (store == null)
            {
                issues.Add(SetUpTexts.SettingsNotWritten);
                return issues;
            }
            // Written last, so a read back that differs above writes no entity, and the roll back of the flow takes this write back
            store.Record(settingsNow(), context.Button, LastRun(context, changing, issues), RunKind.Real, context.Transaction);
            return issues;
        }

        // R367. Button 1 always works on the whole project (R366). The mode follows the tick of the settings, since the flow runs the
        // dry run first exactly when it is on (CHOSEN). Counts names the rows of the window that differed and were set
        private ButtonRun LastRun(RunContext context, int changing, IReadOnlyList<string> issues)
        {
            ButtonRun run = ButtonRun.Of(clock(), settingsNow(), new SettingsScope(), context.InputHashes, issues);
            run.Counts["set"] = changing;
            return run;
        }

        // R311. The site facts read back from the document, in the shape plan_from_book.py takes by --site
        public static JObject SiteJson(SiteState after, string runId)
        {
            return new JObject
            {
                ["format"] = SiteFormat, ["formatVersion"] = SiteFormatVersion, ["runId"] = runId,
                ["projectPosition"] = new JObject { ["eastM"] = after.Position.EastM, ["northM"] = after.Position.NorthM, ["elevationM"] = after.Position.ElevationM, ["angleDeg"] = after.Position.AngleDeg },
                ["angleToTrueNorthDeg"] = after.Position.AngleDeg,
                ["surveyPoint"] = new JObject { ["position"] = new JArray(after.Survey.InternalM), ["sharedPosition"] = new JArray(after.Survey.SharedM) },
                ["projectBasePoint"] = new JObject { ["position"] = new JArray(after.ProjectBase.InternalM), ["sharedPosition"] = new JArray(after.ProjectBase.SharedM) },
                ["levels"] = new JArray(after.Levels.Select(level => new JObject { ["name"] = level.Name, ["elevationM"] = level.ElevationM, ["projectElevationM"] = level.ProjectElevationM, ["elevationBase"] = level.Base })),
                ["units"] = new JObject { ["system"] = after.Units.System, ["length"] = after.Units.Length, ["lengthAccuracy"] = after.Units.LengthAccuracy, ["area"] = after.Units.Area, ["angle"] = after.Units.Angle, ["slope"] = after.Units.Slope },
            };
        }

        private double[] DryShared(double[] internalM)
        {
            return document.SharedOf(target.Position, internalM);
        }

        private SetUpTarget Make()
        {
            return SetUpTarget.From(config, rules, null, position, slope, sheet, cad);
        }

        private bool SameSheet(string path)
        {
            return string.Equals(Path.GetFullPath(path), configSheet, StringComparison.OrdinalIgnoreCase);
        }

        // R461. The planner view with the stop of a sheet other than that of config
        private SetUpSiteView Viewed(Func<double[], double[]> dry)
        {
            SetUpSiteView view = SetUpPlanner.View(document.Read(), target, dry);
            view.Stops = SheetStops(view.Stops);
            return view;
        }

        private List<string> SheetStops(List<string> stops)
        {
            return otherSheet == null ? stops : stops.Concat(new[] { SetUpTexts.OtherSheet(Path.GetFileName(otherSheet), Path.GetFileName(configSheet)) }).ToList();
        }

        private JObject Json(string path, string what)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException(SetUpTexts.NotOnDisk(what, path));
            }
            return PyJson.Parse(readText(path)) as JObject ?? throw new InvalidDataException(what + " is no JSON object: " + path);
        }

        private static RunSummary Summary(SetUpSiteView view)
        {
            List<SetUpRow> changing = view.WillSet.Where(row => row.Differs).ToList();
            string counts = view.NothingDiffers ? SetUpTexts.NothingDiffers : SetUpTexts.WillChange(changing.Count, view.WillSet.Count);
            return new RunSummary(counts, changing.Select(row => row.Label + ": " + row.Value).Concat(view.Stops).Concat(view.Waiting).ToList());
        }

        private sealed class Scope : IDisposable
        {
            private Action end;

            public Scope(Action end)
            {
                this.end = end;
            }

            public void Dispose()
            {
                Action once = end;
                end = null;
                once?.Invoke();
            }
        }
    }
}
