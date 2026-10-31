using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Scope;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.ReadData
{
    // R347, R375. Starts the worker on a request written in the work folder and hands its answer on once. The add-in starts
    // CadReader.exe by WorkerLaunch and hands the answer on in the API context of Revit, the tests run WorkerRunner in process
    public interface IWorkerStart
    {
        void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended);
    }

    // R375, docs/design/03b-buttons-2-to-8.md 2 Read data. Button 2 as a body of the flow with no model element. The preview lists every
    // input with its size and SHA-256 and whether the last read holds it unchanged. The real run writes the request and starts the
    // worker command read with every read step, into a folder of the work folder named by the one hash over the inputs and the
    // steps, and Revit does not wait (R347). A folder whose record lists every output on disk with its SHA-256 is reused and no worker
    // starts. The plot table is read whole and the scope of R366 held against it after the read.
    // CHOSEN by Code in Lane B 5c: the end comes back through ButtonBody.Start of the flow, which opens no transaction, and the flow
    // gives it to the caller of the run, the add-in, which records it on the document of the request. 6 Levels waits for its worker
    // inside its transaction, the one wait of the design, so button 2 takes the way of docs/design/02-folders.md instead
    public sealed class ReadDataRun
    {
        public const string Command = "read";
        public const string FolderPrefix = "read_";
        public const string RecordFile = "read.json";
        public const string RecordFormat = "site-adaptation-read";
        public const string PlotTableFile = "read_plotTable.json";

        // The work folders whose worker runs now, so a second click says so and starts nothing
        private static readonly HashSet<string> Running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly IReadOnlyDictionary<string, string> roles;
        private readonly IWorkerStart worker;
        private readonly WorkerDocument document;
        private readonly Func<SettingsScope> scope;
        private ReadPlan previewed;

        // roles names the role of each input of the pane by its name there, scope gives the scope of the settings when the read ends
        public ReadDataRun(IReadOnlyDictionary<string, string> roles, IWorkerStart worker, WorkerDocument document, Func<SettingsScope> scope)
        {
            this.roles = roles;
            this.worker = worker;
            this.document = document;
            this.scope = scope;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Start = Start };
        }

        public static bool IsRunning(string workFolder)
        {
            lock (Running)
            {
                return Running.Contains(Path.GetFullPath(workFolder));
            }
        }

        public static string FolderOf(string workFolder, ReadPlan plan)
        {
            return Path.Combine(workFolder, FolderPrefix + plan.Hash.Substring(0, 16));
        }

        private RunSummary Preview(RunContext context)
        {
            if (IsRunning(context.WorkFolder))
            {
                throw new InvalidOperationException(RunTexts.ReadRunning(context.Label));
            }
            ReadPlan plan = Plan(context);
            previewed = plan;
            JObject reusable = Reusable(FolderOf(context.WorkFolder, plan), plan);
            if (reusable != null)
            {
                return new RunSummary(RunTexts.ReadReuses(plan.Inputs.Count, (string)reusable["runId"]), Lines(plan, plan.Inputs.Select(input => input.Role)));
            }
            JObject last = Last(context.WorkFolder);
            var held = new HashSet<string>(last == null ? Enumerable.Empty<string>() : ((JArray)last["inputs"]).Select(input => (string)input["role"] + "|" + (string)input["sha256"]), StringComparer.Ordinal);
            IEnumerable<string> unchanged = plan.Inputs.Where(input => held.Contains(input.Role + "|" + input.Sha256)).Select(input => input.Role);
            return new RunSummary(RunTexts.ReadCounts(plan.Inputs.Count, plan.Inputs.Sum(input => input.Bytes) / 1048576.0), Lines(plan, unchanged));
        }

        private void Start(RunContext context, Action<RunLater> done)
        {
            ReadPlan plan = Plan(context);
            if (previewed != null && previewed.Hash != plan.Hash)
            {
                List<string> changed = plan.Inputs.Where(input => !previewed.Inputs.Any(before => before.Role == input.Role && before.Sha256 == input.Sha256)).Select(input => Path.GetFileName(input.Path)).ToList();
                done(RunLater.Failed(RunTexts.ChangedSinceDryRun(context.Label, changed.Count > 0 ? changed : new List<string> { "an input" })));
                return;
            }
            string folder = FolderOf(context.WorkFolder, plan);
            JObject reusable = Reusable(folder, plan);
            if (reusable != null)
            {
                // R375, R366. The read reused is the read of this run, so its record takes the run id and the inputs of the run: the latest
                // read every later button takes is this folder, and a file moved with the same content is named at its new path
                string before = (string)reusable["runId"];
                JObject record = Recorded(context.RunId, plan, Outputs(reusable), reusable["counts"] as JObject ?? new JObject(), Strings(reusable["issues"]));
                record["reusedFrom"] = (string)reusable["reusedFrom"] ?? before;
                WorkFiles.WriteText(Path.Combine(folder, RecordFile), context.RunId, record.ToString(Newtonsoft.Json.Formatting.Indented));
                done(Ended(context, folder, RunTexts.ReadReused(context.Label, before), Strings(reusable["issues"]), Outputs(reusable)));
                return;
            }
            string work = Path.GetFullPath(context.WorkFolder);
            lock (Running)
            {
                if (!Running.Add(work))
                {
                    throw new InvalidOperationException(RunTexts.ReadRunning(context.Label));
                }
            }
            try
            {
                var request = new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId, Document = document, Command = Command,
                    Inputs = plan.Inputs.Select(input => new WorkerFile { Role = input.Role, Path = input.Path, Sha256 = input.Sha256 }).ToList(),
                    // The read steps are of the whole project (R348), the scope is held against the whole plot table after the read
                    Scope = new WorkerScope { Kind = "project" },
                    OutputFolder = folder, Steps = plan.Steps.ToList(), StepsFolder = Path.Combine(folder, "steps"),
                    TimeLimitS = AddinConfig.Load(plan.ConfigPath).WorkerTimeLimitS(Command),
                };
                Directory.CreateDirectory(work);
                string requestPath = request.Write(work);
                worker.Start(requestPath, work, request, answer =>
                {
                    lock (Running)
                    {
                        Running.Remove(work);
                    }
                    done(Answered(context, folder, plan, answer));
                });
            }
            catch
            {
                lock (Running)
                {
                    Running.Remove(work);
                }
                throw;
            }
        }

        private RunLater Answered(RunContext context, string folder, ReadPlan plan, WorkerAnswer answer)
        {
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                return RunLater.Failed(RunTexts.LaterFailed(context.Label, RunTexts.ReadWorker(answer?.Status ?? "with no answer", answer?.Message ?? "")));
            }
            var outputs = (answer.Outputs ?? new List<WorkerFile>()).ToDictionary(output => output.Path, output => output.Sha256, StringComparer.OrdinalIgnoreCase);
            var issues = answer.Issues ?? new List<string>();
            JObject record = Recorded(context.RunId, plan, outputs, JObject.FromObject(answer.Counts ?? new Dictionary<string, int>()), issues);
            // The record goes last, by a temporary name and a rename, so a folder is reused only once every output of it is there
            WorkFiles.WriteText(Path.Combine(folder, RecordFile), context.RunId, record.ToString(Newtonsoft.Json.Formatting.Indented));
            return Ended(context, folder, RunTexts.ReadDone(context.Label, outputs.Count, issues.Count), issues, outputs);
        }

        private static JObject Recorded(string runId, ReadPlan plan, IReadOnlyDictionary<string, string> outputs, JObject counts, IReadOnlyList<string> issues)
        {
            return new JObject
            {
                ["format"] = RecordFormat, ["runId"] = runId,
                ["inputs"] = new JArray(plan.Inputs.Select(input => new JObject { ["role"] = input.Role, ["path"] = input.Path, ["bytes"] = input.Bytes, ["sha256"] = input.Sha256 })),
                ["steps"] = new JArray(plan.Steps),
                ["outputs"] = new JArray(outputs.Select(output => new JObject { ["path"] = output.Key, ["sha256"] = output.Value })),
                ["counts"] = counts.DeepClone(),
                ["issues"] = new JArray(issues),
            };
        }

        // The scope of R366 held against the whole plot table of the read. A stop keeps the read for the next run and the step as it was
        private RunLater Ended(RunContext context, string folder, string message, IReadOnlyList<string> issues, IReadOnlyDictionary<string, string> outputs)
        {
            string table = Path.Combine(folder, PlotTableFile);
            if (!File.Exists(table))
            {
                return RunLater.Failed(RunTexts.ReadScope(context.Label, new[] { "the read wrote no plot table, " + PlotTableFile }));
            }
            ScopeResult result = ButtonScope.Resolve(context.Button, scope() ?? new SettingsScope(), ScopePlots(JObject.Parse(WorkFiles.ReadText(table))));
            if (!result.Ok)
            {
                return RunLater.Failed(RunTexts.ReadScope(context.Label, result.Stops));
            }
            return RunLater.Done(message, issues, outputs);
        }

        // CHOSEN by Code in Lane B 5c: a row of the plot table is a unit plot when the schedules give it an official number, the schedule
        // unit of python/cad_rules.unit_plots, and its neighbourhood is its area key nbhId of R256, never an IM letter
        public static IEnumerable<ScopePlot> ScopePlots(JObject table)
        {
            foreach (JToken row in table["plots"] as JArray ?? new JArray())
            {
                string number = row["officialNumber"]?.Type == JTokenType.String ? (string)row["officialNumber"] : null;
                string nbh = row["nbhId"] == null || row["nbhId"].Type == JTokenType.Null ? null : row["nbhId"].ToString();
                yield return new ScopePlot(number, nbh, number != null);
            }
        }

        private ReadPlan Plan(RunContext context)
        {
            ReadPlan plan = ReadInputs.Make(context.Inputs, roles, context.WorkFolder);
            if (plan.Stops.Count > 0)
            {
                throw new InvalidDataException(string.Join(". ", plan.Stops));
            }
            return plan;
        }

        // The record of a read in its folder when it lists the same inputs and every output is on disk with its SHA-256, else null
        public static JObject Reusable(string folder, ReadPlan plan)
        {
            JObject record = Record(Path.Combine(folder, RecordFile));
            if (record == null)
            {
                return null;
            }
            var listed = ((JArray)record["inputs"] ?? new JArray()).Select(input => (string)input["role"] + "|" + (string)input["sha256"]).OrderBy(item => item, StringComparer.Ordinal);
            if (!listed.SequenceEqual(plan.Inputs.Select(input => input.Role + "|" + input.Sha256).OrderBy(item => item, StringComparer.Ordinal)))
            {
                return null;
            }
            foreach (KeyValuePair<string, string> output in Outputs(record))
            {
                if (WorkFiles.IsTemporary(output.Key) || !File.Exists(output.Key) || !string.Equals(FileHash.Sha256(output.Key), output.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
            return record;
        }

        // The record of the latest read of the work folder by its run id, which starts with the button and the time
        private static JObject Last(string workFolder)
        {
            if (!Directory.Exists(workFolder))
            {
                return null;
            }
            return Directory.GetDirectories(workFolder, FolderPrefix + "*").Select(folder => Record(Path.Combine(folder, RecordFile))).Where(record => record != null)
                .OrderByDescending(record => (string)record["runId"], StringComparer.Ordinal).FirstOrDefault();
        }

        private static JObject Record(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                JObject record = JObject.Parse(WorkFiles.ReadText(path));
                return (string)record["format"] == RecordFormat ? record : null;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        private static IReadOnlyDictionary<string, string> Outputs(JObject record)
        {
            return ((JArray)record["outputs"] ?? new JArray()).ToDictionary(output => (string)output["path"], output => (string)output["sha256"], StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> Strings(JToken token)
        {
            return token is JArray list ? list.Select(item => (string)item).ToList() : new List<string>();
        }

        private static IReadOnlyList<string> Lines(ReadPlan plan, IEnumerable<string> unchanged)
        {
            var same = new HashSet<string>(unchanged, StringComparer.Ordinal);
            return plan.Inputs.Select(input => RunTexts.ReadFile(Path.GetFileName(input.Path), input.Bytes, input.Sha256, same.Contains(input.Role))).ToList();
        }
    }
}
