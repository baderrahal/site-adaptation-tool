using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;

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
        private readonly Func<string, IReadOnlyList<object[]>> sheetRows;

        private JObject config;
        private JObject rules;
        private JObject position;
        private ParameterSheet sheet;
        private double slope;
        private SetUpTarget target;
        private string readBack;

        // settingsInput is the name of the input of the Settings file in the pane. sheetRows reads the rows of a parameter sheet chosen
        // by Browse, null until the worker reads xlsx for the add-in (R350, an xlsx is never read inside Revit), then Browse stops
        public SetUpRun(ISiteDocument document, ISetUpSiteDialog dialog, ITagStep tags, Func<string, string> readText, string settingsInput, Func<string, IReadOnlyList<object[]>> sheetRows)
        {
            this.document = document;
            this.dialog = dialog;
            this.tags = tags;
            this.readText = readText;
            this.settingsInput = settingsInput;
            this.sheetRows = sheetRows;
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
            string profile = config["profile"]?.Type == JTokenType.String ? (string)config["profile"] : throw new InvalidDataException("config names no rules profile, profile (R277)");
            // The paths of config are relative to the repo root, the folder above the one config is in (its note)
            string rulesPath = Path.IsPathRooted(profile) ? profile : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(settings.Path))), profile);
            rules = Json(rulesPath, "the rules profile");
            position = Json(PositionSettings.From(config).File, "the position file");
            slope = RampSlopes.Default(rules, null);
            sheet = ParameterSheet.FromConfig(config);
            target = Make();
            View = SetUpPlanner.View(document.Read(), target, context.DryRun ? (Func<double[], double[]>)DryShared : null);
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
                    return View = SetUpPlanner.View(document.Read(), target, DryShared);
                },
                SheetChosen = path =>
                {
                    sheet = ReadSheet(path);
                    target = Make();
                    return View = SetUpPlanner.View(document.Read(), target, View.DryRunDone ? (Func<double[], double[]>)DryShared : null);
                },
            };
            SetUpSiteAnswer answer = dialog.Show(View, callbacks);
            if (answer == null || !answer.Apply)
            {
                return false;
            }
            if (answer.ParameterSheet != null && !string.Equals(sheet.Sheet, Path.GetFileName(answer.ParameterSheet), StringComparison.OrdinalIgnoreCase))
            {
                sheet = ReadSheet(answer.ParameterSheet);
            }
            if (RampSlopes.Choices.Contains(answer.RampSlope))
            {
                slope = answer.RampSlope;
            }
            target = Make();
            List<string> stops = SetUpPlanner.Stops(document.Read(), target);
            if (stops.Count > 0)
            {
                throw new InvalidOperationException(string.Join(". ", stops));
            }
            return true;
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
            List<string> stops = SetUpPlanner.Stops(now, target);
            if (stops.Count > 0)
            {
                throw new InvalidOperationException(string.Join(". ", stops));
            }
            SetUpChanges changes = SetUpPlanner.Changes(now, target);
            if (changes.Units)
            {
                document.SetUnits(target.Units);
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
            var issues = new List<string>(tags.Tag(context.RunId, context.Button) ?? new string[0]);

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
            context.WriteText(SiteFile, SiteJson(after, context.RunId).ToString(Newtonsoft.Json.Formatting.Indented));
            // R367 of 4h, the Settings entity with the record of the run, joins at 5c (J1 of steps/lane-b.md)
            issues.Add(SetUpTexts.SettingsNotWritten);
            return issues;
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
            return SetUpTarget.From(config, rules, null, position, slope, sheet);
        }

        private ParameterSheet ReadSheet(string path)
        {
            if (sheetRows == null)
            {
                throw new InvalidOperationException(SetUpTexts.SheetNotRead);
            }
            return ParameterSheet.FromRows(Path.GetFileName(path), sheetRows(path));
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
