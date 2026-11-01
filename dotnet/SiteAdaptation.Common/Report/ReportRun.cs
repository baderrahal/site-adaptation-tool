using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.Report
{
    // R381, R365, docs/design/03b-buttons-2-to-8.md 9 Report. The body of button 9 as a body of the flow, with no Revit type. It reads the plan of
    // the last real run of 4 Check by the run log of that run in the work folder, the counts of the model and the run logs of buttons 1 to 8, asks
    // the worker for reportExcel per NBH of that run and writes <NBH>_Report.xlsx of each to its temporary name in the work folder, which the flow
    // moves onto its name after the record of the run. The earlier report of a name goes to earlier\<its time>\ first. No model element changes
    public sealed class ReportRun
    {
        public const string Command = "reportExcel";
        public const string Label = "9 Report";
        public const string EarlierFolder = "earlier";

        // The roles of ReportWorkbook of CadReader, which the add-in loads no type of
        public const string PlanRole = "plan";
        public const string CheckRowsRole = "checkRows";
        public const string SummaryRole = "summary";
        public const string OverridesRole = "overrides";
        public const string PlotsCsvRole = "plotsCsv";
        public const string RevitCountsRole = "revitCounts";
        public const string RunLogRolePrefix = "runLog";

        private const int Check = 4;
        private const int Levels = 6;
        private const int LastBuildStep = 8;

        private readonly string settingsInput;
        private readonly Func<string, string> readText;
        private readonly Func<ICollection<string>, string, JObject> readCounts;
        private readonly Func<WorkerRequest, string, WorkerAnswer> worker;
        private readonly WorkerDocument document;

        private string configPath;
        private string rulesPath;
        private string profile;
        private CheckRun check;
        private readonly List<string> issues = new List<string>();
        private readonly List<string> written = new List<string>();

        // settingsInput is the name the pane gives the config among the inputs of the run. readCounts takes the plots whose levels the last
        // committed run of 6 read back and its run id and gives RevitCounts of the model. worker takes a request and the work folder and gives
        // the answer of a run that ended Done, else throws
        public ReportRun(string settingsInput, Func<string, string> readText, Func<ICollection<string>, string, JObject> readCounts,
            Func<WorkerRequest, string, WorkerAnswer> worker, WorkerDocument document)
        {
            this.settingsInput = settingsInput;
            this.readText = readText;
            this.readCounts = readCounts;
            this.worker = worker;
            this.document = document;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Outside = Outside, Apply = Apply };
        }

        public static string NeedsCheck(string why)
        {
            return RunTexts.NeedsStep(Label, Check) + ": " + why;
        }

        public static string ReportName(string nbh)
        {
            return nbh + "_Report.xlsx";
        }

        // Where the earlier file of a name goes: earlier\<its last write time>\<name> in the folder of the name
        public static string EarlierPath(string path, DateTime writeTime)
        {
            string time = writeTime.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), EarlierFolder, time, Path.GetFileName(path));
        }

        private RunSummary Preview(RunContext context)
        {
            RunInput settings = context.Inputs.FirstOrDefault(input => input.Name == settingsInput);
            if (settings == null || string.IsNullOrEmpty(settings.Path))
            {
                throw new InvalidOperationException("the run names no " + settingsInput + " file");
            }
            configPath = settings.Path;
            JObject config = Json(configPath, "the settings file");
            string name = config["profile"]?.Type == JTokenType.String ? (string)config["profile"] : throw new InvalidDataException("config names no rules profile, profile (R277)");
            rulesPath = Path.IsPathRooted(name) ? name : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configPath))), name);
            JObject rules = Json(rulesPath, "the rules profile");
            profile = rules["levelProfiles"]?["default"]?.Type == JTokenType.String ? (string)rules["levelProfiles"]["default"] : throw new InvalidDataException("the rules profile names no levelProfiles.default (R277)");
            check = CheckRun.Find(context.WorkFolder, readText);

            var lines = new List<string>();
            foreach (string nbh in check.Nbhs)
            {
                string target = Path.Combine(context.WorkFolder, ReportName(nbh));
                lines.Add("Writes " + target);
                if (File.Exists(target))
                {
                    lines.Add("Moves the earlier " + ReportName(nbh) + " to " + EarlierPath(target, File.GetLastWriteTime(target)));
                }
            }
            return new RunSummary("Report Excel files: " + check.Nbhs.Count.ToString(CultureInfo.InvariantCulture), lines);
        }

        // The read of the model and the worker run before the transaction of the flow opens, so neither runs inside a Revit transaction and the
        // worker, which can take minutes, holds no transaction open. A failure here changes nothing and the flow deletes every output made
        private IDisposable Outside(RunContext context)
        {
            issues.Clear();
            written.Clear();
            RunLog levels = RunLog.Last(context.WorkFolder, Levels, readText);
            ICollection<string> levelPlots = levels == null ? new List<string>() : levels.LevelPlots.ToList();
            string countsPath = Path.Combine(context.WorkFolder, "revit_counts_" + context.RunId + ".json");
            WorkFiles.WriteText(countsPath, context.RunId, readCounts(levelPlots, levels?.RunId).ToString(Formatting.Indented));

            var logs = new List<WorkerFile>();
            for (int button = 1; button <= LastBuildStep; button++)
            {
                RunLog log = RunLog.Last(context.WorkFolder, button, readText);
                if (log != null)
                {
                    logs.Add(Input(RunLogRolePrefix + button.ToString(CultureInfo.InvariantCulture), log.Path));
                }
            }

            string stage = Path.Combine(context.WorkFolder, "report_" + context.RunId);
            try
            {
                foreach (string nbh in check.Nbhs)
                {
                    string name = ReportName(nbh);
                    var inputs = new List<WorkerFile> { Input("config", configPath), Input("rules", rulesPath) };
                    inputs.AddRange(check.Files(nbh));
                    inputs.Add(Input(RevitCountsRole, countsPath));
                    inputs.AddRange(logs);
                    var request = new WorkerRequest
                    {
                        FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId + "-" + nbh, Command = Command, Document = document, Inputs = inputs,
                        LevelProfile = profile, Scope = new WorkerScope { Kind = "nbh", Nbh = nbh }, OutputFolder = stage, TimeLimitS = AddinConfig.Load(configPath).WorkerTimeLimitS(Command),
                    };
                    WorkerAnswer answer;
                    try
                    {
                        answer = worker(request, context.WorkFolder);
                    }
                    catch (Exception failure)
                    {
                        throw new InvalidOperationException(name + " was not written: " + failure.Message, failure);
                    }
                    WorkerFile book = (answer.Outputs ?? new List<WorkerFile>()).FirstOrDefault(output => string.Equals(Path.GetFileName(output.Path), name, StringComparison.OrdinalIgnoreCase));
                    if (book == null || !File.Exists(book.Path))
                    {
                        throw new InvalidOperationException(name + " was not written: the worker gives no such output");
                    }
                    string temporary = context.Output(Path.Combine(context.WorkFolder, name));
                    File.Move(book.Path, temporary);
                    written.Add(Path.Combine(context.WorkFolder, name));
                    issues.AddRange(answer.Issues ?? new List<string>());
                }
            }
            finally
            {
                if (Directory.Exists(stage))
                {
                    Directory.Delete(stage, true);
                }
            }
            return null;
        }

        // The earlier report of each name is copied to its earlier folder, by a temporary name, before the flow replaces it with the new one, so
        // it is never overwritten silently. A copy left by a run that then rolled back is a spare one and the name still holds the earlier file
        private IReadOnlyList<string> Apply(RunContext context)
        {
            foreach (string target in written.Where(File.Exists))
            {
                string earlier = EarlierPath(target, File.GetLastWriteTime(target));
                Directory.CreateDirectory(Path.GetDirectoryName(earlier));
                string temporary = WorkFiles.TemporaryName(earlier, context.RunId);
                File.Copy(target, temporary, false);
                WorkFiles.Commit(temporary, earlier);
            }
            return issues;
        }

        private JObject Json(string path, string what)
        {
            return PyJson.Parse(readText(path)) as JObject ?? throw new InvalidDataException(what + " is no JSON object: " + path);
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        // R381. The run log of a button in the work folder, run_<run id>.json with the run id of RunIds,
        // so the name sorts by time. The parts 9 Report reads: runId, committed, inputs and outputs as lists of role, path and sha256, an output
        // of 4 Check with the nbh it is of, and levelPlots of 6 Levels, the plots whose levels that run set and read back
        public sealed class RunLog
        {
            public string Path { get; private set; }
            public string RunId { get; private set; }
            public JObject Json { get; private set; }

            public IReadOnlyList<string> LevelPlots
            {
                get { return (Json["levelPlots"] as JArray)?.Select(item => (string)item).ToList() ?? new List<string>(); }
            }

            // The last run log of the button whose run committed, null when the work folder holds none
            public static RunLog Last(string workFolder, int button, Func<string, string> readText)
            {
                if (!Directory.Exists(workFolder))
                {
                    return null;
                }
                string prefix = "run_" + button.ToString(CultureInfo.InvariantCulture) + "_";
                IEnumerable<string> names = Directory.GetFiles(workFolder, prefix + "*.json")
                    .Where(path => !WorkFiles.IsTemporary(path))
                    .OrderByDescending(path => System.IO.Path.GetFileName(path), StringComparer.Ordinal);
                foreach (string path in names)
                {
                    JObject json;
                    try
                    {
                        json = PyJson.Parse(readText(path)) as JObject;
                    }
                    catch (Exception failure) when (failure is IOException || failure is JsonException || failure is FormatException)
                    {
                        continue;
                    }
                    if (json != null && json["committed"]?.Type == JTokenType.Boolean && (bool)json["committed"])
                    {
                        return new RunLog { Path = path, RunId = (string)json["runId"], Json = json };
                    }
                }
                return null;
            }
        }

        // The plan and the step files of the last real run of 4 Check by NBH, and the plot table and override list it read, each held against the
        // SHA-256 its run log gives, never a file of a dry folder. Anything missing or changed stops the button as step 4 not done
        public sealed class CheckRun
        {
            private static readonly string[] PerNbh = { PlanRole, CheckRowsRole, SummaryRole };
            private static readonly string[] Read = { PlotsCsvRole, OverridesRole };

            private readonly Dictionary<string, List<WorkerFile>> files = new Dictionary<string, List<WorkerFile>>(StringComparer.Ordinal);
            private readonly List<WorkerFile> shared = new List<WorkerFile>();

            public IReadOnlyList<string> Nbhs
            {
                get { return files.Keys.OrderBy(nbh => nbh, StringComparer.Ordinal).ToList(); }
            }

            public IEnumerable<WorkerFile> Files(string nbh)
            {
                return files[nbh].Concat(shared);
            }

            public static CheckRun Find(string workFolder, Func<string, string> readText)
            {
                RunLog log = RunLog.Last(workFolder, Check, readText);
                if (log == null)
                {
                    throw new InvalidOperationException(NeedsCheck("the work folder holds no run log of a real run of 4 Check that committed, run_4_<time>.json, run 4 Check first"));
                }
                var run = new CheckRun();
                foreach (JObject output in (log.Json["outputs"] as JArray ?? new JArray()).OfType<JObject>().Where(item => PerNbh.Contains((string)item["role"])))
                {
                    string nbh = (string)output["nbh"];
                    if (string.IsNullOrEmpty(nbh))
                    {
                        throw new InvalidOperationException(NeedsCheck("the output " + (string)output["role"] + " of " + System.IO.Path.GetFileName(log.Path) + " names no nbh"));
                    }
                    if (!run.files.TryGetValue(nbh, out List<WorkerFile> list))
                    {
                        run.files[nbh] = list = new List<WorkerFile>();
                    }
                    list.Add(Held(workFolder, log, output));
                }
                if (run.files.Count == 0)
                {
                    throw new InvalidOperationException(NeedsCheck(System.IO.Path.GetFileName(log.Path) + " lists no plan"));
                }
                foreach (KeyValuePair<string, List<WorkerFile>> nbh in run.files)
                {
                    foreach (string role in PerNbh.Where(role => nbh.Value.Count(file => file.Role == role) != 1))
                    {
                        throw new InvalidOperationException(NeedsCheck(System.IO.Path.GetFileName(log.Path) + " lists no single " + role + " of NBH " + nbh.Key));
                    }
                }
                foreach (string role in Read)
                {
                    JObject input = (log.Json["inputs"] as JArray ?? new JArray()).OfType<JObject>().SingleOrDefault(item => (string)item["role"] == role);
                    if (input == null)
                    {
                        throw new InvalidOperationException(NeedsCheck(System.IO.Path.GetFileName(log.Path) + " lists no input " + role));
                    }
                    run.shared.Add(Held(workFolder, log, input));
                }
                return run;
            }

            // R472. The plan of the NBH the last real run of 4 Check lists, held to the SHA-256 its run log gives, for Update from report (Q278).
            // The plan alone is held, since Apply of Update writes the override list that run read and a second read of the Excel must not stop
            public static WorkerFile PlanOf(string workFolder, string nbh, Func<string, string> readText)
            {
                RunLog log = RunLog.Last(workFolder, Check, readText);
                if (log == null)
                {
                    throw new InvalidOperationException(NeedsCheck("the work folder holds no run log of a real run of 4 Check that committed, run_4_<time>.json, run 4 Check first"));
                }
                List<JObject> plans = (log.Json["outputs"] as JArray ?? new JArray()).OfType<JObject>()
                    .Where(item => (string)item["role"] == PlanRole && (string)item["nbh"] == nbh).ToList();
                if (plans.Count != 1)
                {
                    throw new InvalidOperationException(NeedsCheck(System.IO.Path.GetFileName(log.Path) + " lists no single plan of NBH " + nbh + ", run 4 Check for it"));
                }
                return Held(workFolder, log, plans[0]);
            }

            private static WorkerFile Held(string workFolder, RunLog log, JObject item)
            {
                string role = (string)item["role"];
                string given = (string)item["path"];
                string path = string.IsNullOrEmpty(given) ? null : System.IO.Path.Combine(workFolder, given);
                string name = given == null ? role : System.IO.Path.GetFileName(given);
                if (path == null || WorkFiles.IsTemporary(path) || IsDry(workFolder, path) || !File.Exists(path))
                {
                    throw new InvalidOperationException(NeedsCheck(name + " of the run " + log.RunId + " is not on disk"));
                }
                string sha = FileHash.Sha256(path);
                if (!string.Equals(sha, (string)item["sha256"], StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(NeedsCheck(name + " on disk is not the one the run " + log.RunId + " wrote or read, its SHA-256 differs"));
                }
                return new WorkerFile { Role = role, Path = path, Sha256 = sha };
            }

            private static bool IsDry(string workFolder, string path)
            {
                string dry = System.IO.Path.GetFullPath(System.IO.Path.Combine(workFolder, "dry")) + System.IO.Path.DirectorySeparatorChar;
                return System.IO.Path.GetFullPath(path).StartsWith(dry, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
