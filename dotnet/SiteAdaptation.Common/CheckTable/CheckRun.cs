using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.CheckTable
{
    // R376, docs/design/03b-buttons-2-to-8.md 4 Check. Button 4 as a body of the flow with no model element. The preview holds the stops,
    // the real run writes one request of the worker command build with every step to checkRows and summary on the latest read of button
    // 2, site.json of button 1, the rules profile, the override list and the prototype files, and Revit does not wait (R347). Its end
    // writes checks_<NBH>.json and opens the check table, and Save overrides writes the list and builds again. A dry run builds by
    // runNow into the dry folder with Save overrides greyed. The flow stops on step 3 not done before the body runs (R366)
    public sealed class CheckRun
    {
        public const string Command = "build";
        public const string Config = "config";
        public const string Rules = "rules";
        public const string Prototypes = "prototypes";
        public const string KeyJoin = "keyJoin";
        public const string KeyReads = "keyReads";
        public const string OverridesRole = "overrides";
        public const string FolderPrefix = "check_";
        public const string RecordFile = "check.json";
        public const string RecordFormat = "site-adaptation-check-run";

        // The read outputs of button 2 the build takes by role, R354
        public static readonly IReadOnlyDictionary<string, string> ReadFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["baseplanRead"] = "read_baseplan.json", ["villaBlocksRead"] = "read_villaBlocks.json", ["curbsRead"] = "read_curbs.json", ["workbooksRead"] = "read_workbooks.json",
        };

        // The inputs of the read the build takes again, by the role the read named them
        private static readonly string[] ReadRoles = { "config", "plotsCsv", "labels" };

        // The files beside config the build reads by name (docs/design/06-settings.md section 6)
        private static readonly string[] BesideConfig = { "floor_outlines.json", "prototype_offsets.json" };

        private readonly IReadOnlyDictionary<string, string> roles;
        private readonly IWorkerStart worker;
        private readonly Func<string, string, WorkerRequest, WorkerAnswer> runNow;
        private readonly WorkerDocument document;
        private readonly Func<SettingsScope> scope;
        private readonly ICheckTableDialog dialog;
        private readonly Func<string, string, string> manualRule;
        private readonly string levelProfile;

        // roles names the role of each input of the pane by its name there. runNow runs the worker and waits, for the dry run, null where
        // the add-in gives none. levelProfile null takes levelProfiles.default of the rules profile (Q050)
        public CheckRun(IReadOnlyDictionary<string, string> roles, IWorkerStart worker, Func<string, string, WorkerRequest, WorkerAnswer> runNow, WorkerDocument document,
            Func<SettingsScope> scope, ICheckTableDialog dialog, Func<string, string, string> manualRule, string levelProfile)
        {
            this.roles = roles;
            this.worker = worker;
            this.runNow = runNow;
            this.document = document;
            this.scope = scope;
            this.dialog = dialog;
            this.manualRule = manualRule;
            this.levelProfile = levelProfile;
        }

        // R443. DoorLevelSource of the pane settings, read for the row Door level from of the window. Null where no pane gives one
        public Func<string> DoorPick { get; set; }

        // R443. Keeps a pick of Door level from in the pane settings. Null keeps it in the window only
        public Action<string> DoorPicked { get; set; }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Start = Start };
        }

        // What a build of the run takes, named
        public sealed class CheckPlan
        {
            public List<WorkerFile> Inputs { get; } = new List<WorkerFile>();
            public List<string> Stops { get; } = new List<string>();
            public string ConfigPath { get; set; }
            public string OverridesPath { get; set; }
            public string ReadFolder { get; set; }
            public string Nbh { get; set; }
            public PastJob Past { get; set; }

            // Q214. The Prototypes folder of the pane the build takes its prototype files from
            public string PrototypeFolder { get; set; }
        }

        public CheckPlan Plan(RunContext context)
        {
            var plan = new CheckPlan();
            string Path0(string role) => context.Inputs.Where(input => roles.TryGetValue(input.Name, out string named) && named == role).Select(input => input.Path).FirstOrDefault();
            string configPath = Path0(Config);
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                plan.Stops.Add("the run names no settings file on disk");
                return plan;
            }
            plan.ConfigPath = Path.GetFullPath(configPath);
            JObject config = JObject.Parse(WorkFiles.ReadText(plan.ConfigPath));
            SettingsScope chosen = scope?.Invoke() ?? new SettingsScope();
            plan.Nbh = chosen.Kind == ScopeKinds.Nbh && !string.IsNullOrWhiteSpace(chosen.Nbh) ? chosen.Nbh.Trim() : null;
            plan.ReadFolder = ReviewLoad.LatestReadFolder(context.WorkFolder);
            JObject record = plan.ReadFolder == null ? null : JObject.Parse(WorkFiles.ReadText(Path.Combine(plan.ReadFolder, ReadDataRun.RecordFile)));
            if (record == null)
            {
                plan.Stops.Add("2 Read data has no read in the work folder, run it first");
                return plan;
            }
            var changed = new List<string>();
            foreach (JObject input in ((JArray)record["inputs"] ?? new JArray()).OfType<JObject>())
            {
                string path = (string)input["path"], role = (string)input["role"];
                if (path == null || !File.Exists(path) || !string.Equals(FileHash.Sha256(path), (string)input["sha256"], StringComparison.OrdinalIgnoreCase))
                {
                    changed.Add(Path.GetFileName(path ?? role));
                    continue;
                }
                if (ReadRoles.Contains(role) || (role ?? "").StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal))
                {
                    plan.Inputs.Add(new WorkerFile { Role = role, Path = path, Sha256 = (string)input["sha256"] });
                }
            }
            if (changed.Count > 0)
            {
                plan.Stops.Add("2 Read data was done on other inputs, changed since: " + string.Join(", ", changed) + ". Run 2 Read data again");
            }
            if (!string.Equals(plan.Inputs.Where(input => input.Role == Config).Select(input => input.Path).FirstOrDefault(), plan.ConfigPath, StringComparison.OrdinalIgnoreCase) && changed.Count == 0)
            {
                plan.Stops.Add("2 Read data was done on other inputs, the settings file of the read is not " + Path.GetFileName(plan.ConfigPath) + ". Run 2 Read data again");
            }
            foreach (KeyValuePair<string, string> file in ReadFiles)
            {
                Add(plan, file.Key, Path.Combine(plan.ReadFolder, file.Value), "the read " + Path.GetFileName(plan.ReadFolder) + " holds no " + file.Value + ", run 2 Read data again");
            }
            string root = Path.GetDirectoryName(Path.GetDirectoryName(plan.ConfigPath));
            string rulesPath = Path0(Rules);
            if (string.IsNullOrEmpty(rulesPath) && config["profile"]?.Type == JTokenType.String)
            {
                rulesPath = Path.Combine(root, (string)config["profile"]);
            }
            Add(plan, Rules, rulesPath, "the rules file is missing: " + (rulesPath ?? "the run and config name none"));
            plan.OverridesPath = OverridesPath(config, plan.ConfigPath, rulesPath);
            Add(plan, OverridesRole, plan.OverridesPath, "the override list is missing: " + (plan.OverridesPath ?? "config names none"));
            string prototypes = Path0(Prototypes);
            List<string> files = !string.IsNullOrEmpty(prototypes) && Directory.Exists(prototypes) ? Directory.GetFiles(prototypes, "*.rvt").OrderBy(name => name, StringComparer.Ordinal).ToList() : new List<string>();
            if (files.Count == 0)
            {
                plan.Stops.Add("the Prototypes folder holds no prototype file: " + (prototypes ?? "none chosen"));
            }
            else
            {
                plan.PrototypeFolder = Path.GetFullPath(prototypes);
            }
            files.ForEach(file => Add(plan, BuildSteps.PrototypeRole + Path.GetFileName(file), file, null));
            foreach (string name in BesideConfig)
            {
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(plan.ConfigPath), name)))
                {
                    plan.Stops.Add(name + " is not beside the settings file");
                }
            }
            if (!File.Exists(Path.Combine(context.WorkFolder, SetUpRun.SiteFile)))
            {
                plan.Stops.Add("1 Set up site wrote no " + SetUpRun.SiteFile + " in the work folder");
            }
            else
            {
                // R80. The survey point, the angle and the level elevations of the plan the build writes beside its steps
                Add(plan, BuildPlan.SiteRole, Path.Combine(context.WorkFolder, SetUpRun.SiteFile), null);
            }
            if (plan.Stops.Count == 0)
            {
                Held(plan, config, rulesPath);
            }
            plan.Past = Past(Path0(KeyJoin), Path0(KeyReads), config);
            return plan;
        }

        // R277, R280. The level profile the rules file holds and the override list as a build loads it, each stop named
        private void Held(CheckPlan plan, JObject config, string rulesPath)
        {
            JObject rules = JObject.Parse(WorkFiles.ReadText(rulesPath));
            JObject profile;
            string name;
            try
            {
                name = BuildRules.ProfileName(rules, levelProfile);
                profile = BuildRules.LevelProfile(rules, name);
            }
            catch (InvalidDataException refused)
            {
                plan.Stops.Add("the rules file holds no such level profile: " + refused.Message + " (R277)");
                return;
            }
            try
            {
                PlanOverrides.Load(plan.OverridesPath, config, profile, name, Path.GetFileName(plan.ConfigPath));
            }
            catch (InvalidDataException refused)
            {
                plan.Stops.Add("a build refuses the override list: " + refused.Message + " (R280)");
            }
        }

        // CHOSEN by Code in Lane B 5c: addin.overrideFile of config when it names one, as section 6 of 06-settings.md plans, else the list of
        // the rules profile by its name in overrides beside config, config/overrides/roshn-makkah.json for rules/roshn-makkah.json
        public static string OverridesPath(JObject config, string configPath, string rulesPath)
        {
            string named = config["addin"]?["overrideFile"]?.Type == JTokenType.String ? (string)config["addin"]["overrideFile"] : null;
            if (named != null)
            {
                return Path.IsPathRooted(named) ? named : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(configPath)), named);
            }
            return rulesPath == null ? null : Path.Combine(Path.GetDirectoryName(configPath), "overrides", Path.GetFileName(rulesPath));
        }

        // Q117. key_join.json and the folder of the key reads testhost_<job>_prototypes.json, both optional
        public static PastJob Past(string keyJoin, string keyReads, JObject config)
        {
            JObject join = !string.IsNullOrEmpty(keyJoin) && File.Exists(keyJoin) ? JObject.Parse(WorkFiles.ReadText(keyJoin)) : null;
            Dictionary<string, KeyLink> links = null;
            string parameter = (string)config["majority"]?["linkPlotParameter"];
            if (!string.IsNullOrEmpty(keyReads) && Directory.Exists(keyReads) && parameter != null)
            {
                links = new Dictionary<string, KeyLink>(StringComparer.Ordinal);
                foreach (string file in Directory.GetFiles(keyReads, "testhost_*_prototypes.json", SearchOption.AllDirectories).OrderBy(name => name, StringComparer.Ordinal))
                {
                    string name = Path.GetFileName(file);
                    string job = name.Substring("testhost_".Length, name.Length - "testhost_".Length - "_prototypes.json".Length);
                    foreach (KeyValuePair<string, KeyLink> link in KeyLinks(JObject.Parse(WorkFiles.ReadText(file)), job, parameter))
                    {
                        // A plot two keys hold keeps the first by file name (CHOSEN), the keys of one zone hold other plots
                        if (!links.ContainsKey(link.Key))
                        {
                            links[link.Key] = link.Value;
                        }
                    }
                }
            }
            return join == null && links == null ? null : new PastJob { KeyJoin = join, Links = links };
        }

        // The links of the project document of a key read by the plot their plot parameter names, a plot two links name is left out
        public static Dictionary<string, KeyLink> KeyLinks(JObject read, string job, string plotParameter)
        {
            JObject project = (read["documents"] as JArray ?? new JArray()).OfType<JObject>().SingleOrDefault(item => (string)item["kind"] == "project");
            var found = new Dictionary<string, KeyLink>(StringComparer.Ordinal);
            var twice = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject link in (project?["links"] as JArray ?? new JArray()).OfType<JObject>())
            {
                JToken value = link["parameters"]?[plotParameter];
                string plot = value == null || value.Type == JTokenType.Null ? null : Scope.ButtonScope.WholeNumber(value.ToString().Trim());
                if (string.IsNullOrEmpty(plot))
                {
                    continue;
                }
                if (found.ContainsKey(plot))
                {
                    twice.Add(plot);
                }
                JToken mirrored = link["transform"]?["mirrored"];
                found[plot] = new KeyLink
                {
                    Code = job, Model = Path.GetFileName((string)project["file"] ?? ""), File = (string)link["file"],
                    Mirrored = mirrored != null && mirrored.Type == JTokenType.Boolean ? (bool?)(bool)mirrored : null, Parameters = link["parameters"] as JObject ?? new JObject(),
                };
            }
            twice.ToList().ForEach(plot => found.Remove(plot));
            return found;
        }

        private static void Add(CheckPlan plan, string role, string path, string missing)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                plan.Stops.Add(missing ?? role + " is not on disk");
                return;
            }
            plan.Inputs.Add(new WorkerFile { Role = role, Path = Path.GetFullPath(path), Sha256 = FileHash.Sha256(path) });
        }

        private CheckPlan Checked(RunContext context)
        {
            CheckPlan plan = Plan(context);
            if (plan.Stops.Count > 0)
            {
                throw new InvalidDataException(string.Join(". ", plan.Stops));
            }
            return plan;
        }

        public WorkerRequest Request(RunContext context, CheckPlan plan, string runId, string folder)
        {
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Document = document, Command = Command, Inputs = plan.Inputs.ToList(),
                Scope = plan.Nbh == null ? new WorkerScope { Kind = "project" } : new WorkerScope { Kind = ScopeKinds.Nbh, Nbh = plan.Nbh },
                LevelProfile = levelProfile, PrototypeFolder = plan.PrototypeFolder, OutputFolder = folder, StepsFolder = Path.Combine(folder, "steps"),
                Steps = new[] { FaceStep.Name }.Concat(BuildSteps.Names).ToList(), TimeLimitS = AddinConfig.Load(plan.ConfigPath).WorkerTimeLimitS(Command),
            };
        }

        private RunSummary Preview(RunContext context)
        {
            CheckPlan plan = Checked(context);
            string line = "builds NBH " + (plan.Nbh ?? "all") + " on the read " + Path.GetFileName(plan.ReadFolder) + " from " + plan.Inputs.Count + " inputs";
            if (!context.DryRun)
            {
                return new RunSummary(line, plan.Past == null ? new[] { CheckTableBuild.NoPastJob } : null);
            }
            if (runNow == null)
            {
                throw new InvalidOperationException("the dry run of " + context.Label + " waits for the worker, which the add-in gives no way to yet (R376)");
            }
            WorkerRequest request = Request(context, plan, context.RunId, context.DryFolder);
            Directory.CreateDirectory(context.DryFolder);
            WorkerAnswer answer = runNow(request.Write(context.DryFolder), context.DryFolder, request);
            CheckTableView view = Answered(context, plan, request, answer, false);
            context.WriteText(ChecksName(plan.Nbh), ChecksText(view, context.RunId, plan));
            dialog?.Show(view, new CheckTableCallbacks { DoorLevel = DoorPicked });
            return new RunSummary(view.Title + ", " + view.Rows.Count + " rows, " + view.FollowLine, view.NoPastJobLine == null ? null : new[] { view.NoPastJobLine });
        }

        private void Start(RunContext context, Action<RunLater> done)
        {
            CheckPlan plan = Checked(context);
            string folder = Path.Combine(context.WorkFolder, FolderPrefix + context.RunId);
            Build(context, plan, context.RunId, folder, done, true);
        }

        private void Build(RunContext context, CheckPlan plan, string runId, string folder, Action<RunLater> done, bool first)
        {
            WorkerRequest request = Request(context, plan, runId, folder);
            Directory.CreateDirectory(folder);
            worker.Start(request.Write(folder), folder, request, answer =>
            {
                try
                {
                    CheckTableView view = Answered(context, plan, request, answer, true);
                    string checks = WorkFiles.WriteText(Path.Combine(folder, ChecksName(plan.Nbh)), runId, ChecksText(view, runId, plan));
                    var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [checks] = FileHash.Sha256(checks) };
                    foreach (WorkerFile output in answer.Outputs ?? new List<WorkerFile>())
                    {
                        outputs[output.Path] = output.Sha256;
                    }
                    var session = new OverrideSession(plan.OverridesPath, plan.Inputs.Where(input => input.Role == OverridesRole).Select(input => input.Sha256).FirstOrDefault());
                    if (first && dialog != null)
                    {
                        var callbacks = new CheckTableCallbacks
                        {
                            Save = choices =>
                            {
                                session.Save(view.Rows, choices, JObject.Parse(WorkFiles.ReadText(plan.ConfigPath)), runId);
                                return plan.OverridesPath;
                            },
                            DoorLevel = DoorPicked,
                        };
                        dialog.Show(view, callbacks);
                    }
                    // R376. No check.json names a plan built on another list than the one on disk, and no build again takes in an edit made
                    // on disk while the window was open
                    if (!session.OnDisk())
                    {
                        throw new InvalidDataException(OverrideSession.NotAsBuilt(plan.OverridesPath));
                    }
                    if (session.Changed)
                    {
                        // R376. The list changed, so the plan is built again on it before the step is done, the hash of the list is new
                        CheckPlan again = Checked(context);
                        Build(context, again, runId + "-again", folder + "-again", done, false);
                        return;
                    }
                    // R377. The record goes last and only on a build that ends done, so 7 takes the plan of the last real run that ended done
                    WorkFiles.WriteText(Path.Combine(folder, RecordFile), runId, Record(runId, outputs).ToString(Newtonsoft.Json.Formatting.Indented));
                    string log = Path.Combine(context.WorkFolder, Floors.LevelsRun.RunLogName(context.RunId));
                    WorkFiles.WriteText(log, context.RunId, RunLog(context.RunId, context.WorkFolder, plan, request).ToString(Newtonsoft.Json.Formatting.Indented));
                    done(RunLater.Done(view.Title + ", " + view.Rows.Count + " rows, " + view.FollowLine, answer.Issues ?? new List<string>(), outputs));
                }
                catch (Exception failure) when (failure is InvalidDataException || failure is IOException || failure is InvalidOperationException)
                {
                    done(RunLater.Failed(RunTexts.LaterFailed(context.Label, failure.Message)));
                }
            });
        }

        private CheckTableView Answered(RunContext context, CheckPlan plan, WorkerRequest request, WorkerAnswer answer, bool real)
        {
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                string why = answer?.Message ?? "with no answer";
                throw new InvalidDataException("the build ended " + (answer?.Status ?? "") + ": " + why + (answer?.Status == WorkerExit.Status(WorkerExit.Refused) ? " (R280 when it names the override list)" : ""));
            }
            string stepPath = Path.Combine(request.StepsFolder, StepFile.FileName(CheckRows.StepName, "csharp"));
            if (!File.Exists(stepPath))
            {
                throw new InvalidDataException("the build wrote no " + Path.GetFileName(stepPath));
            }
            JObject step = JObject.Parse(WorkFiles.ReadText(stepPath));
            JObject config = JObject.Parse(WorkFiles.ReadText(plan.ConfigPath));
            JObject block = config["checkRows"] as JObject ?? throw new InvalidDataException(CheckRows.NoBlock);
            string tableHash = FileHash.Sha256(Path.Combine(plan.ReadFolder, ReadDataRun.PlotTableFile));
            List<ReviewCheck> wrong = plan.Nbh == null ? new List<ReviewCheck>() : ReviewFile.WrongPlots(context.WorkFolder, plan.Nbh, tableHash);
            CheckTableView view = CheckTableBuild.Compose(plan.Nbh, step, block, wrong, plan.Past, manualRule, real);
            JObject list = JObject.Parse(WorkFiles.ReadText(plan.OverridesPath));
            CheckTableBuild.UnderSkip(view.Rows, list, (config["majority"]?["overrideDefaultSources"] as JArray ?? new JArray()).Select(item => (string)item).ToList());
            view.Info = Info(plan, step, list, levelProfile);
            view.DoorLevel = DoorLevelPick.Of(config, plan.ConfigPath, LevelsStep(request.StepsFolder), DoorPick?.Invoke());
            return view;
        }

        // R443. The step levels the build of the run wrote, with doorSource per plot (R408), null when it wrote none
        public static JObject LevelsStep(string stepsFolder)
        {
            string path = Path.Combine(stepsFolder, StepFile.FileName(KeyLevels.StepName, "csharp"));
            return File.Exists(path) ? JObject.Parse(WorkFiles.ReadText(path)) : null;
        }

        // 7.4 of the mock, the line under the title: the level profile the build took, the override list and its entries
        private static CheckTableInfo Info(CheckPlan plan, JObject step, JObject list, string levelProfile)
        {
            string rulesPath = plan.Inputs.Where(input => input.Role == Rules).Select(input => input.Path).FirstOrDefault();
            JObject rules = JObject.Parse(WorkFiles.ReadText(rulesPath));
            return CheckTableInfo.Of(BuildRules.ProfileName(rules, levelProfile), Path.GetFileName(plan.OverridesPath), list, step, plan.Nbh);
        }

        public static JObject Record(string runId, IReadOnlyDictionary<string, string> outputs)
        {
            return new JObject
            {
                ["format"] = RecordFormat, ["runId"] = runId,
                ["outputs"] = new JArray(outputs.OrderBy(output => output.Key, StringComparer.Ordinal).Select(output => new JObject { ["path"] = output.Key, ["sha256"] = output.Value })),
            };
        }

        // R381. The run log of a real run of 4 Check that ended done, in the shape ReportRun.RunLog reads: the plan and the checkRows and
        // summary steps of the last build by its NBH, and the plot table and override list that build read with the SHA-256 it took them
        // at. A project scope names no NBH, so 9 Report stops on it by name
        public static JObject RunLog(string runId, string workFolder, CheckPlan plan, WorkerRequest request)
        {
            var made = new[]
            {
                Tuple.Create(Report.ReportRun.PlanRole, Path.Combine(request.OutputFolder, PlanName(plan.Nbh))),
                Tuple.Create(Report.ReportRun.CheckRowsRole, Path.Combine(request.StepsFolder, StepFile.FileName(CheckRows.StepName, "csharp"))),
                Tuple.Create(Report.ReportRun.SummaryRole, Path.Combine(request.StepsFolder, StepFile.FileName(PlanSummary.StepName, "csharp"))),
            };
            var outputs = new JArray();
            foreach (Tuple<string, string> file in made.Where(file => File.Exists(file.Item2)))
            {
                JObject item = LogItem(workFolder, file.Item1, file.Item2, FileHash.Sha256(file.Item2));
                if (plan.Nbh != null)
                {
                    item["nbh"] = plan.Nbh;
                }
                outputs.Add(item);
            }
            var inputs = new JArray(plan.Inputs.Where(input => input.Role == Report.ReportRun.PlotsCsvRole || input.Role == Report.ReportRun.OverridesRole)
                .Select(input => LogItem(workFolder, input.Role, input.Path, input.Sha256)));
            return new JObject { ["runId"] = runId, ["button"] = 4, ["committed"] = true, ["inputs"] = inputs, ["outputs"] = outputs };
        }

        // A file of the work folder by its path there, so the log holds no path of the machine for it
        private static JObject LogItem(string workFolder, string role, string path, string sha256)
        {
            string full = Path.GetFullPath(path);
            string under = Path.GetFullPath(workFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string given = full.StartsWith(under, StringComparison.OrdinalIgnoreCase) ? full.Substring(under.Length) : full;
            return new JObject { ["role"] = role, ["path"] = given, ["sha256"] = sha256 };
        }

        // R377. The record of a real run of 4 Check that ended done in its folder, null where the run wrote none
        public static JObject RecordOf(string folder)
        {
            string path = Path.Combine(folder, RecordFile);
            if (!File.Exists(path))
            {
                return null;
            }
            JObject record = JObject.Parse(WorkFiles.ReadText(path));
            return (string)record["format"] == RecordFormat ? record : null;
        }

        // R80. The plan of the build in its folder, the one file buttons 5 to 8 read
        public static string PlanName(string nbh)
        {
            return BuildPlan.FileName(nbh);
        }

        public static string ChecksName(string nbh)
        {
            return "checks_" + (nbh ?? "all") + ".json";
        }

        private static string ChecksText(CheckTableView view, string runId, CheckPlan plan)
        {
            var root = new JObject
            {
                ["format"] = "site-adaptation-checks", ["formatVersion"] = 1, ["neighbourhood"] = view.Nbh, ["runId"] = runId, ["read"] = Path.GetFileName(plan.ReadFolder),
                ["title"] = view.Title, ["followEveryRule"] = view.FollowEveryRule, ["noPastJob"] = view.NoPastJobLine,
                ["counts"] = new JObject(view.Counts.Select(item => new JProperty(item.Key, item.Value))),
                ["rows"] = new JArray(view.Rows.Select(row => new JObject
                {
                    ["plot"] = row.Plot, ["unit"] = row.Unit, ["check"] = row.Check, ["field"] = row.Field, ["from"] = row.From, ["use"] = row.OpenUse, ["underSkip"] = row.UnderSkip,
                    ["rule"] = row.Rule?.DeepClone(), ["toolValue"] = row.ToolValue?.DeepClone(), ["pastForm"] = row.PastForm, ["pastValue"] = row.PastValue?.DeepClone(),
                    ["pastOffered"] = row.PastOffered, ["pastWhy"] = row.PastWhy, ["why"] = row.Why?.DeepClone(), ["key"] = row.Key, ["model"] = row.Model, ["manualRule"] = row.ManualRuleId,
                })),
            };
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }
    }
}
