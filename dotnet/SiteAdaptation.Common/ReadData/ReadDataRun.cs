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
        public const string DataCheckCommand = "dataCheck";
        public const string FolderPrefix = "read_";
        public const string RecordFile = "read.json";
        public const string RecordFormat = "site-adaptation-read";
        public const string PlotTableFile = "read_plotTable.json";
        public const string DataCheckFile = "data_check.json";

        // The work folders whose worker runs now, so a second click says so and starts nothing
        private static readonly HashSet<string> Running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly IReadOnlyDictionary<string, string> roles;
        private readonly IWorkerStart worker;
        private readonly WorkerDocument document;
        private readonly Func<SettingsScope> scope;
        private readonly Func<ProjectSettings> settings;
        private ReadPlan previewed;

        // roles names the role of each input of the pane by its name there, scope gives the scope of the settings when the read ends.
        // settings gives the settings of the project with their picks when the read starts, after the window of the scan saved them (R473)
        public ReadDataRun(IReadOnlyDictionary<string, string> roles, IWorkerStart worker, WorkerDocument document, Func<SettingsScope> scope, Func<ProjectSettings> settings = null)
        {
            this.roles = roles;
            this.worker = worker;
            this.document = document;
            this.scope = scope;
            this.settings = settings;
        }

        // R492. The road surfaces picked in Set up, read under rev6 alone. Null where no pane gives one, or a pick not made, takes those of config
        public Func<IReadOnlyList<string>> RoadPick { get; set; }

        // R484. The project mode of the settings, null where no pane gives one or the settings hold none, which reads the mode of config
        public Func<string> ModePick { get; set; }

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
            ReadPlan files = Plan(context);
            previewed = files;
            // The dry run lists the files the user gave, picks.json is made at the start once the scan of the start has run (R473)
            ReadPlan plan = Picked(context, files, out _);
            string folder = FolderOf(context.WorkFolder, plan);
            JObject reusable = Reusable(folder, plan);
            if (reusable != null)
            {
                string runId = (string)reusable["runId"];
                return new RunSummary(files.FromDataCheck ? ReusesLine(runId, HasConfirmed(files)) + Instead(files) : RunTexts.ReadReuses(files.Inputs.Count, runId), Lines(files, files.Inputs.Select(input => input.Role)));
            }
            JObject last = Last(context.WorkFolder);
            var held = new HashSet<string>(last == null ? Enumerable.Empty<string>() : ((JArray)last["inputs"]).Select(input => (string)input["role"] + "|" + (string)input["sha256"]), StringComparer.Ordinal);
            IEnumerable<string> unchanged = files.Inputs.Where(input => held.Contains(input.Role + "|" + input.Sha256)).Select(input => input.Role);
            return new RunSummary(files.FromDataCheck ? FolderCounts(files, folder) : RunTexts.ReadCounts(files.Inputs.Count, files.Inputs.Sum(input => input.Bytes) / 1048576.0), Lines(files, unchanged));
        }

        // R821. The dry run of a raw read reused, by the time of the read, so a person knows which read comes back in seconds. In place of
        // RunTexts.ReadFolderReuses, whose words docs/design/14-ui-labels.md lists
        public static string ReusesLine(string runId, bool confirmed)
        {
            return "Reuses the read of " + RunTime(runId) + ", nothing changed: " + (confirmed ? "the folder, the settings file and the values a person confirmed are unchanged." : "the folder and the settings file are unchanged.");
        }

        // R821. A run id is the button, then the time as yyyyMMdd-HHmmss and more. Its time as "08:13 on 8 Nov", the run id itself when it holds none
        public static string RunTime(string runId)
        {
            string[] parts = (runId ?? "").Split('_');
            if (parts.Length > 1 && parts[1].Length >= 15 && DateTime.TryParseExact(parts[1].Substring(0, 15), "yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime at))
            {
                return at.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " on " + at.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);
            }
            return "run " + runId;
        }

        // R639. The confirm line of a raw read: what it reads, why it takes the folder, and why it reads again when the same files were read before
        private static string FolderCounts(ReadPlan files, string folder)
        {
            ReadInput walked = files.Inputs.First(input => input.Role == ReadInputs.ScanFolder);
            string again = NotWhole(folder);
            return RunTexts.ReadFolderCounts(walked.FilesHeld, walked.Bytes / 1048576.0, HasConfirmed(files)) + Instead(files) + (again == null ? "" : " " + RunTexts.ReadAgain(again));
        }

        private static bool HasConfirmed(ReadPlan files)
        {
            return files.Inputs.Any(input => input.Role == ConfirmedProfile.Role);
        }

        // R639. The word that a raw read took the folder since the old read cannot run, empty on the old read
        private static string Instead(ReadPlan plan)
        {
            return plan.FromDataCheck ? " " + RunTexts.ReadFolderInstead(plan.PastLacks) : "";
        }

        private void Start(RunContext context, Action<RunLater> done)
        {
            ReadPlan files = Plan(context);
            // R473. The dry run is held to the files only: the scan runs between the dry run and the start and its window may save a pick,
            // which changes picks.json and so the hash, and is the read the user asked for
            if (previewed != null && previewed.Hash != files.Hash)
            {
                List<string> changed = files.Inputs.Where(input => !previewed.Inputs.Any(before => before.Role == input.Role && before.Sha256 == input.Sha256)).Select(input => Path.GetFileName(input.Path)).ToList();
                done(RunLater.Failed(RunTexts.ChangedSinceDryRun(context.Label, changed.Count > 0 ? changed : new List<string> { "an input" })));
                return;
            }
            ReadPlan plan = Picked(context, files, out JObject picks);
            string folder = FolderOf(context.WorkFolder, plan);
            JObject reusable = Reusable(folder, plan);
            if (reusable != null)
            {
                // R375, R366. The read reused is the read of this run, so its record takes the run id and the inputs of the run: the latest
                // read every later button takes is this folder, and a file moved with the same content is named at its new path
                string before = (string)reusable["runId"];
                JObject record = Recorded(context.RunId, plan, Outputs(reusable), reusable["counts"] as JObject ?? new JObject(), Strings(reusable["issues"]), picks);
                record["reusedFrom"] = (string)reusable["reusedFrom"] ?? before;
                WorkFiles.WriteText(Path.Combine(folder, RecordFile), context.RunId, record.ToString(Newtonsoft.Json.Formatting.Indented));
                done(Ended(context, folder, RunTexts.ReadReused(context.Label, before) + Instead(plan), Strings(reusable["issues"]), Outputs(reusable)));
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
                if (!plan.FromDataCheck)
                {
                    ReadPicks.Write(plan.Inputs.First(input => input.Role == ReadPicks.Role).Path, context.RunId, picks);
                }
                var request = plan.FromDataCheck ? DataCheckRequest(context, plan, folder) : new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId, Document = document, Command = Command,
                    Inputs = plan.Inputs.Select(input => new WorkerFile { Role = input.Role, Path = input.Path, Sha256 = input.Sha256 }).ToList(),
                    // The read steps are of the whole project (R348), the scope is held against the whole plot table after the read
                    Scope = new WorkerScope { Kind = "project" },
                    OutputFolder = folder, Steps = plan.Steps.ToList(), StepsFolder = Path.Combine(folder, "steps"),
                    TimeLimitS = AddinConfig.Load(plan.ConfigPath).WorkerTimeLimitS(Command),
                    RoadSurfaces = RoadSurfaceChoices.ForRequest(ProjectModes.Apply(JObject.Parse(WorkFiles.ReadText(plan.ConfigPath)), ModePick?.Invoke()), RoadPick?.Invoke()),
                    ProjectMode = ModePick?.Invoke(),
                };
                Directory.CreateDirectory(work);
                string requestPath = request.Write(work);
                worker.Start(requestPath, work, request, answer =>
                {
                    context.Timing?.NextStep(RunTiming.WorkerEndStep, RunTiming.Reading);
                    lock (Running)
                    {
                        Running.Remove(work);
                    }
                    done(Answered(context, folder, plan, picks, answer));
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

        // R635. The folder has no one SHA-256 for the worker to hold, it marks each file it opens instead (R600)
        private WorkerRequest DataCheckRequest(RunContext context, ReadPlan plan, string folder)
        {
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId, Document = document, Command = DataCheckCommand,
                // F10, R750. The parameter sheet of the pane goes to the check of card 1 too, in the request only, so the hash and reuse of the read stay
                Inputs = plan.Inputs.Select(input => new WorkerFile { Role = input.Role, Path = input.Path, Sha256 = input.Role == ReadInputs.ScanFolder ? "" : input.Sha256 })
                    .Concat(new[] { DataCheckSheet.Input(settings?.Invoke(), plan.ConfigPath) }).Where(input => input != null).ToList(),
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder,
                TimeLimitS = AddinConfig.Load(plan.ConfigPath).WorkerTimeLimitS(DataCheckCommand),
                ProjectMode = ModePick?.Invoke(),
            };
        }

        private RunLater Answered(RunContext context, string folder, ReadPlan plan, JObject picks, WorkerAnswer answer)
        {
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                return RunLater.Failed(RunTexts.LaterFailed(context.Label, RunTexts.ReadWorker(answer?.Status ?? "with no answer", answer?.Message ?? "")));
            }
            var outputs = (answer.Outputs ?? new List<WorkerFile>()).ToDictionary(output => output.Path, output => output.Sha256, StringComparer.OrdinalIgnoreCase);
            var issues = answer.Issues ?? new List<string>();
            JObject record = Recorded(context.RunId, plan, outputs, JObject.FromObject(answer.Counts ?? new Dictionary<string, int>()), issues, picks);
            // The record goes last, by a temporary name and a rename, so a folder is reused only once every output of it is there
            WorkFiles.WriteText(Path.Combine(folder, RecordFile), context.RunId, record.ToString(Newtonsoft.Json.Formatting.Indented));
            string again = plan.FromDataCheck ? NotWhole(folder) : null;
            string message = RunTexts.ReadDone(context.Label, outputs.Count, issues.Count) + Instead(plan) + (again == null ? "" : " " + RunTexts.ReadNotWhole(again));
            return Ended(context, folder, message, issues, outputs, again);
        }

        // R791, F22 of FAST-9. After a Confirm that saved, the check runs again with the confirmed profile on the files the read just made gathered,
        // in Revit and with no worker, so the plot table, plots.json and the Scope list take the confirmed columns at once. The read folder then
        // moves to the folder its inputs now name, with the confirmed profile among them, and its record lists them: the next press of 2, and the
        // step 2 the Build puts back, reuse it and read nothing. The inputs are those the record of the read holds from its start, with only the
        // profile new, so a folder that changed during the read names another read. Failed with the plain reason when it cannot: before the move
        // nothing of the read changed, after it the folder holds no record and the next press reads again (R792). It writes in the work folder only
        public RunLater ApplyConfirmed(RunContext context, RunLater read)
        {
            Applying apply = Prepare(context, read, out string refused);
            if (apply == null)
            {
                return RunLater.Failed(refused);
            }
            string from = apply.From, saved = apply.Saved;
            ReadPlan plan = apply.Plan;
            JObject before = apply.Before;
            ReadInput config = apply.Config, profile = apply.Profile;
            DataCheckSettings settings = DataCheckSettings.From(JObject.Parse(WorkFiles.ReadText(config.Path)));
            DataCheckInput input = DataCheckApply.Load(WorkFiles.ReadText(saved));
            input.Confirmed = ConfirmedProfile.From(JObject.Parse(WorkFiles.ReadText(profile.Path)), settings.GroupGapsM.Count);
            DataCheckOutput output = DataCheckApply.Check(settings, input, new List<string>());
            if (input.Stopped != null)
            {
                return RunLater.Failed(ApplyStopped.Replace("<why>", input.Stopped));
            }
            var stamps = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            var written = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase)
            {
                [DataCheckFile] = output.Check, ["project_profile.json"] = output.Profile, ["plots.json"] = output.Plots, [PlotTableFile] = DataCheckLogic.PlotTable(output.Plots),
            };
            foreach (string name in written.Keys)
            {
                string path = Path.Combine(from, name);
                stamps[name] = File.Exists(path) ? JObject.Parse(WorkFiles.ReadText(path))[RunStamp.Key] : null;
            }
            // The record goes first and comes back last, so a write that fails leaves a folder no press reuses, which reads again
            string to = Moved(from, FolderOf(context.WorkFolder, plan));
            foreach (KeyValuePair<string, JObject> item in written)
            {
                if (stamps[item.Key] != null)
                {
                    item.Value[RunStamp.Key] = stamps[item.Key].DeepClone();
                }
                WorkFiles.WriteText(Path.Combine(to, item.Key), context.RunId, item.Value.ToString(Newtonsoft.Json.Formatting.Indented, new ExactDouble.Converter()));
            }
            var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in read.Outputs.Keys)
            {
                string moved = Path.Combine(to, Path.GetFileName(path));
                outputs[moved] = FileHash.Sha256(moved);
            }
            JObject counts = before["counts"] as JObject ?? new JObject();
            counts["data check plots"] = ((JArray)output.Plots["plots"]).Count;
            JObject record = Recorded(context.RunId, plan, outputs, counts, read.Issues ?? new List<string>(), new JObject());
            WorkFiles.WriteText(Path.Combine(to, RecordFile), context.RunId, record.ToString(Newtonsoft.Json.Formatting.Indented));
            return RunLater.Done(read.Message, read.Issues, outputs);
        }

        // What an apply of a Confirm works on, made by Prepare once every refusal is passed
        private sealed class Applying
        {
            public string From;
            public string Saved;
            public JObject Before;
            public ReadInput Config;
            public ReadInput Profile;
            public ReadPlan Plan;
        }

        // R791, R820. The refusals both applies share, in this order: no check, no gathered file, too large, no record of a folder read, the
        // settings file changed since the read started, no confirmed profile. Null with the reason when one holds
        private static Applying Prepare(RunContext context, RunLater read, out string refused)
        {
            refused = null;
            string check = read?.Outputs?.Keys.FirstOrDefault(path => string.Equals(Path.GetFileName(path), DataCheckFile, StringComparison.OrdinalIgnoreCase));
            if (check == null)
            {
                refused = ApplyNoCheck;
                return null;
            }
            string from = Path.GetDirectoryName(Path.GetFullPath(check));
            string saved = Path.Combine(from, DataCheckApply.InputFile);
            if (!File.Exists(saved))
            {
                refused = ApplyNoInput;
                return null;
            }
            if (new FileInfo(saved).Length > DataCheckApply.MaxBytes)
            {
                refused = ApplyTooLarge;
                return null;
            }
            JObject before = Record(Path.Combine(from, RecordFile));
            List<ReadInput> inputs = (before?["inputs"] as JArray ?? new JArray()).OfType<JObject>().Where(item => (string)item["role"] != ConfirmedProfile.Role)
                .Select(item => new ReadInput((string)item["role"], (string)item["path"], (long?)item["bytes"] ?? 0, (string)item["sha256"])).ToList();
            ReadInput config = inputs.FirstOrDefault(item => item.Role == ReadInputs.Config);
            if (config == null || !inputs.Any(item => item.Role == ReadInputs.ScanFolder))
            {
                refused = ApplyNoRecord;
                return null;
            }
            if (!File.Exists(config.Path) || !string.Equals(FileHash.Sha256(config.Path), config.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                refused = ApplyConfigChanged;
                return null;
            }
            string profilePath = Path.Combine(Path.GetFullPath(context.WorkFolder), ConfirmedProfile.FileName);
            if (!File.Exists(profilePath))
            {
                refused = ApplyNoProfile;
                return null;
            }
            var profile = new ReadInput(ConfirmedProfile.Role, profilePath, new FileInfo(profilePath).Length, FileHash.Sha256(profilePath));
            inputs.Add(profile);
            return new Applying
            {
                From = from, Saved = saved, Before = before, Config = config, Profile = profile,
                Plan = new ReadPlan(inputs, Strings(before["steps"]), new string[0], config.Path, null, true),
            };
        }

        // R820, F22 of FAST-15. The apply of R791 with the check outside Revit: the worker command dataCheck takes the gathered files of the read
        // just made in place of the folder, started as the read is, so Revit loads none of them and stays free while it runs. The read folder
        // moves first to the folder its inputs now name, with no record, so a worker that fails leaves a folder the next press reads again (R792).
        // The worker writes the four outputs there, then read.json comes last with the inputs of the start and the confirmed profile
        public void ApplyConfirmedLater(RunContext context, RunLater read, Action<RunLater> done)
        {
            // A reused read that was made with this same confirmed file holds its values already, a Confirm of the same values starts no worker
            if (HoldsProfile(context, read))
            {
                done(RunLater.Done(read.Message, read.Issues, read.Outputs));
                return;
            }
            Applying apply = Prepare(context, read, out string refused);
            if (apply == null)
            {
                done(RunLater.Failed(refused));
                return;
            }
            string work = Path.GetFullPath(context.WorkFolder);
            lock (Running)
            {
                if (!Running.Add(work))
                {
                    done(RunLater.Failed(RunTexts.ReadRunning(context.Label)));
                    return;
                }
            }
            try
            {
                string to = Moved(apply.From, FolderOf(context.WorkFolder, apply.Plan));
                string gathered = Path.Combine(to, DataCheckApply.InputFile);
                string known = read.Outputs.Where(output => string.Equals(Path.GetFileName(output.Key), DataCheckApply.InputFile, StringComparison.OrdinalIgnoreCase)).Select(output => output.Value).FirstOrDefault();
                var request = new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId + ConfirmRunSuffix, Document = document, Command = DataCheckCommand,
                    Inputs = new List<WorkerFile>
                    {
                        new WorkerFile { Role = ReadInputs.Config, Path = apply.Config.Path, Sha256 = apply.Config.Sha256 },
                        new WorkerFile { Role = DataCheckApply.GatheredRole, Path = gathered, Sha256 = string.IsNullOrEmpty(known) ? FileHash.Sha256(gathered) : known },
                        new WorkerFile { Role = ConfirmedProfile.Role, Path = apply.Profile.Path, Sha256 = apply.Profile.Sha256 },
                    },
                    Scope = new WorkerScope { Kind = "project" }, OutputFolder = to,
                    TimeLimitS = AddinConfig.Load(apply.Config.Path).WorkerTimeLimitS(DataCheckCommand),
                    ProjectMode = ModePick?.Invoke(),
                };
                string requestPath = request.Write(work);
                worker.Start(requestPath, work, request, answer =>
                {
                    lock (Running)
                    {
                        Running.Remove(work);
                    }
                    done(AppliedLater(context, read, apply, to, answer));
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

        // R820. True when read.json of the read lists confirmed_profile.json of the work folder with its SHA-256 as it is now
        private static bool HoldsProfile(RunContext context, RunLater read)
        {
            string check = read?.Outputs?.Keys.FirstOrDefault(path => string.Equals(Path.GetFileName(path), DataCheckFile, StringComparison.OrdinalIgnoreCase));
            string profile = Path.Combine(Path.GetFullPath(context.WorkFolder), ConfirmedProfile.FileName);
            if (check == null || !File.Exists(profile))
            {
                return false;
            }
            JObject record = Record(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(check)), RecordFile));
            string sha = FileHash.Sha256(profile);
            return (record?["inputs"] as JArray ?? new JArray()).OfType<JObject>()
                .Any(item => (string)item["role"] == ConfirmedProfile.Role && string.Equals((string)item["sha256"], sha, StringComparison.OrdinalIgnoreCase));
        }

        // The run id of the worker run of a Confirm, the run id of the press with this end, so its request and answer files stand beside the read's
        public const string ConfirmRunSuffix = "-confirm";

        // The read folder at the folder of its new inputs, its record taken away until the apply ended. A folder already there for these inputs is
        // a read of the same files and the same profile, this read is the newer of the two
        private static string Moved(string from, string to)
        {
            if (!string.Equals(Path.GetFullPath(to).TrimEnd(Path.DirectorySeparatorChar), from.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(to))
                {
                    Directory.Delete(to, true);
                }
                Directory.Move(from, to);
            }
            File.Delete(Path.Combine(to, RecordFile));
            return to;
        }

        private RunLater AppliedLater(RunContext context, RunLater read, Applying apply, string to, WorkerAnswer answer)
        {
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                return RunLater.Failed(RunTexts.ReadWorker(answer?.Status ?? "with no answer", answer?.Message ?? ""));
            }
            var hashes = (answer.Outputs ?? new List<WorkerFile>()).ToDictionary(output => Path.GetFullPath(output.Path), output => output.Sha256, StringComparer.OrdinalIgnoreCase);
            var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in read.Outputs.Keys)
            {
                string moved = Path.GetFullPath(Path.Combine(to, Path.GetFileName(path)));
                outputs[moved] = hashes.TryGetValue(moved, out string sha) ? sha : FileHash.Sha256(moved);
            }
            JObject counts = apply.Before["counts"] as JObject ?? new JObject();
            if (answer.Counts != null && answer.Counts.TryGetValue("data check plots", out int plots))
            {
                counts["data check plots"] = plots;
            }
            JObject record = Recorded(context.RunId, apply.Plan, outputs, counts, read.Issues ?? new List<string>(), new JObject());
            WorkFiles.WriteText(Path.Combine(to, RecordFile), context.RunId, record.ToString(Newtonsoft.Json.Formatting.Indented));
            return RunLater.Done(read.Message, read.Issues, outputs);
        }

        public const string ApplyNoCheck = "the read named no data check";
        public const string ApplyNoInput = "the read holds no gathered files, it was made before R790";
        public const string ApplyNoProfile = "no confirmed profile is in the work folder";
        public const string ApplyNoRecord = "the read holds no record of a folder read";
        public const string ApplyConfigChanged = "the settings file changed since the read started";
        public const string ApplyNoResult = "the check gave no result";
        public const string ApplyTooLarge = "the gathered files of the read are too large for one check, over 1,000,000,000 bytes";
        public const string ApplyStopped = "the check on the confirmed values stopped: <why>";

        // R474. sources names per value kind the source read and from where, and each conflict pick applied
        public static JObject Recorded(string runId, ReadPlan plan, IReadOnlyDictionary<string, string> outputs, JObject counts, IReadOnlyList<string> issues, JObject picks)
        {
            var record = new JObject
            {
                ["format"] = RecordFormat, ["runId"] = runId,
                ["inputs"] = new JArray(plan.Inputs.Select(input => new JObject { ["role"] = input.Role, ["path"] = input.Path, ["bytes"] = input.Bytes, ["sha256"] = input.Sha256 })),
                ["steps"] = new JArray(plan.Steps),
                ["outputs"] = new JArray(outputs.Select(output => new JObject { ["path"] = output.Key, ["sha256"] = output.Value })),
                ["counts"] = counts.DeepClone(),
                ["issues"] = new JArray(issues),
                ["sources"] = ReadPicks.Sources(picks),
            };
            // R492. The road order of a read under rev6, none in the record of the past job
            if (plan.RoadOrder != null)
            {
                record["roadOrder"] = plan.RoadOrder;
            }
            return record;
        }

        // The scope of R366 held against the whole plot table of the read. A stop keeps the read for the next run and the step as it was
        // again is why the read is not whole, null when it is, as the next run then reads the files and does not reuse them (R639)
        private RunLater Ended(RunContext context, string folder, string message, IReadOnlyList<string> issues, IReadOnlyDictionary<string, string> outputs, string again = null)
        {
            string table = Path.Combine(folder, PlotTableFile);
            if (!File.Exists(table))
            {
                return RunLater.Failed(RunTexts.ReadScope(context.Label, new[] { "the read wrote no plot table, " + PlotTableFile }));
            }
            ScopeResult result = ButtonScope.Resolve(context.Button, scope() ?? new SettingsScope(), ScopePlots(JObject.Parse(WorkFiles.ReadText(table))));
            if (!result.Ok)
            {
                return RunLater.Failed(again == null ? RunTexts.ReadScope(context.Label, result.Stops) : RunTexts.ReadScopeAgain(context.Label, result.Stops, again));
            }
            return RunLater.Done(message, issues, outputs);
        }

        // CHOSEN by Code in Lane B 5c: a row of the plot table is a unit plot when UnitNumber gives it a number, R557, at first the schedule
        // unit of python/cad_rules.unit_plots, and its neighbourhood is its area key nbhId of R256, never an IM letter
        public static IEnumerable<ScopePlot> ScopePlots(JObject table)
        {
            foreach (JToken row in table["plots"] as JArray ?? new JArray())
            {
                string number = UnitNumber(row);
                string nbh = row["nbhId"] == null || row["nbhId"].Type == JTokenType.Null ? null : row["nbhId"].ToString();
                yield return new ScopePlot(number, nbh, number != null);
            }
        }

        // R557. The official number needs a shift per neighbourhood, config holds two of seven, so 5 NBHs had no unit plot and 6b stopped at
        // button 2. A new code of the location plan marks a unit on exactly the same 747 plots of 012 and 013, and its one number finds every
        // plot of the 7 keys. Two numbers name no plot (Q355), so such a plot still needs its official number
        public static string UnitNumber(JToken row)
        {
            string official = Text(row, "officialNumber");
            if (official != null)
            {
                return official;
            }
            string located = Text(row, "locationPlanNumber");
            return Text(row, "locationPlanNewCode") == null || located == null || located.Contains("|") ? null : located;
        }

        private static string Text(JToken row, string field)
        {
            JToken value = row[field];
            return value?.Type == JTokenType.String && ((string)value).Trim().Length > 0 ? (string)value : null;
        }

        private ReadPlan Plan(RunContext context)
        {
            ReadPlan raw = ReadInputs.Raw(context.Inputs, roles, context.WorkFolder, ModePick?.Invoke());
            if (raw != null && raw.Stops.Count == 0)
            {
                return raw;
            }
            if (raw != null)
            {
                throw new InvalidDataException(string.Join(". ", raw.Stops));
            }
            ReadPlan plan = ReadInputs.Make(context.Inputs, roles, context.WorkFolder, RoadPick?.Invoke(), ModePick?.Invoke());
            if (plan.Stops.Count > 0)
            {
                throw new InvalidDataException(string.Join(". ", plan.Stops));
            }
            return plan;
        }

        // R473. The plan of the files with picks.json, the picks of the settings resolved on the newest scan of the work folder
        private ReadPlan Picked(RunContext context, ReadPlan files, out JObject picks)
        {
            // R635. The data check has no picks of the scan, what a person confirmed is a file of its own
            if (files.FromDataCheck)
            {
                picks = new JObject();
                return files;
            }
            JObject config = JObject.Parse(WorkFiles.ReadText(files.ConfigPath));
            picks = ReadPicks.Resolve(settings?.Invoke(), ReadPicks.LatestScan(context.WorkFolder),
                ReadPicks.FilesOf(config, files.Inputs.Select(input => new KeyValuePair<string, string>(input.Role, input.Path))));
            return ReadPicks.WithPicks(files, context.WorkFolder, picks);
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
            // R492. The same files in another road order are another read
            if ((string)record["roadOrder"] != plan.RoadOrder)
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
            // R639. Same files, but a cause outside them may be gone since: a file another program held open, a run refused for memory
            return plan.FromDataCheck && NotWhole(folder) != null ? null : record;
        }

        // R639. Why the raw read in this folder is not whole: its check stopped or left a file or folder not read. Null when it is whole, and when the
        // folder holds no read yet, which is a first read and no read again
        private static string NotWhole(string folder)
        {
            if (Record(Path.Combine(folder, RecordFile)) == null)
            {
                return null;
            }
            string path = Path.Combine(folder, DataCheckFile);
            JObject check;
            try
            {
                check = File.Exists(path) ? JObject.Parse(WorkFiles.ReadText(path)) : null;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                check = null;
            }
            if (check == null)
            {
                return RunTexts.ReadNoResult;
            }
            string stopped = (string)check["stopped"];
            if (!string.IsNullOrEmpty(stopped))
            {
                return RunTexts.ReadStopped(stopped);
            }
            List<JToken> left = (check["files"] as JArray ?? new JArray()).Where(file => (string)file["status"] != "read").ToList();
            return left.Count == 0 ? null : RunTexts.ReadLeftOut(left.Count, (string)left[0]["name"], (string)left[0]["reason"]);
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
            // R635. The folder of a raw read by its whole path, so the person sees which folder was read. R639: first every file the old read lacks, the line above names one
            return plan.PastLacks.Select(RunTexts.ReadFolderLack).Concat(plan.Inputs.Select(input => RunTexts.ReadFile(input.Role == ReadInputs.ScanFolder ? input.Path : Path.GetFileName(input.Path), input.Bytes, input.Sha256, same.Contains(input.Role)))).ToList();
        }
    }
}
