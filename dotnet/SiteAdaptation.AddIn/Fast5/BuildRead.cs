using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.Fast5
{
    // R740, R741, F16 of run 1 of FAST-3. The read of card 1 reads the folder with the data check only, and 4 Check needs the drawings, road surfaces and
    // workbooks of the old read. So the Build fills the empty input rows from the folder and, before Check, runs the old read itself with no box and no
    // Confirm. Its folder holds the plot table of the data check beside the old outputs, so it is the latest read every later step takes, and step 2
    // keeps its own run and stays done. No Revit type, so a test runs it with a worker of its own
    public static class BuildRead
    {
        // It says its lines in the words of a read of step 2 under the name of that step, words the pane has already
        public const string RunSuffix = "-build";
        public const string PlotTableRole = "plotTable";

        // True when the latest read lacks a file 4 Check takes from the read, false with no read at all, where Check names its own stop
        public static bool Needed(string workFolder)
        {
            string read = string.IsNullOrEmpty(workFolder) ? null : ReviewLoad.LatestReadFolder(workFolder);
            return read != null && CheckRun.ReadFiles.Values.Any(file => !File.Exists(Path.Combine(read, file)));
        }

        // R740. Each empty row, or one whose file is gone, takes the file the folder holds for it. A row a person set stays. Returns the keys filled
        public static IReadOnlyList<string> FillRows(PaneSettings settings, Action<string, string> pick)
        {
            var filled = new List<string>();
            FolderRoles.Found found = Found(settings);
            foreach (KeyValuePair<string, string> role in found.Paths.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                string now = settings.InputPath(role.Key);
                if (string.IsNullOrEmpty(now) || (!File.Exists(now) && !Directory.Exists(now)))
                {
                    pick(role.Key, role.Value);
                    filled.Add(role.Key);
                }
            }
            return filled;
        }

        // R741. The read for the build. ended gets true and the line of its end, or false and why, once, maybe before Start returns
        public static void Start(PaneSettings settings, string workFolder, WorkerDocument document, IWorkerStart worker, string label, Action<bool, string> ended)
        {
            try
            {
                Run(settings, workFolder, document, worker, label, ended);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidDataException
                || failure is InvalidOperationException || failure is ArgumentException)
            {
                ended(false, failure.Message);
            }
        }

        private static void Run(PaneSettings settings, string workFolder, WorkerDocument document, IWorkerStart worker, string label, Action<bool, string> ended)
        {
            if (worker == null)
            {
                ended(false, RunTexts.NotBuilt(label));
                return;
            }
            var pane = new List<RunInput>(ReadDataBody.Inputs(settings)) { new RunInput(PaneText.Settings, settings.ConfigFile, false) };
            ReadPlan files = ReadInputs.ForBuild(pane, ReadDataBody.Roles, workFolder, settings.Project.RoadSurfaces, settings.Project.ProjectMode);
            if (files.Stops.Count > 0)
            {
                // R740. Why the folder did not give a row, beside the row the read stops on
                ended(false, string.Join(". ", files.Stops.Concat(Found(settings).Left)));
                return;
            }
            string source = ReviewLoad.LatestReadFolder(workFolder);
            string table = source == null ? null : Path.Combine(source, ReadDataRun.PlotTableFile);
            if (table == null || !File.Exists(table))
            {
                ended(false, SiteBuildText.NoRead);
                return;
            }
            // The plot table is an input of the record and of the hash, not of the worker: a new read of stage 1 is a new read for the build
            var held = new List<ReadInput>(files.Inputs) { new ReadInput(PlotTableRole, Path.GetFullPath(table), new FileInfo(table).Length, FileHash.Sha256(table)) };
            var plan = new ReadPlan(held, files.Steps, new string[0], files.ConfigPath, files.RoadOrder);
            string folder = ReadDataRun.FolderOf(workFolder, plan);
            string runId = RunIds.New(StagesViewModel.ReadDataStep, DateTime.Now) + RunSuffix;
            JObject reusable = ReadDataRun.Reusable(folder, plan);
            if (reusable != null)
            {
                // The record takes the new run id, so this folder is the latest read again after a read of stage 1 made another one latest
                Write(folder, runId, plan, Outputs(reusable), reusable["counts"] as JObject ?? new JObject(), Strings(reusable["issues"]));
                ended(true, RunTexts.ReadReused(label, (string)reusable["runId"]));
                return;
            }
            string work = Path.GetFullPath(workFolder);
            var request = new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Document = document, Command = ReadDataRun.Command,
                Inputs = files.Inputs.Select(input => new WorkerFile { Role = input.Role, Path = input.Path, Sha256 = input.Sha256 }).ToList(),
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, Steps = files.Steps.ToList(), StepsFolder = Path.Combine(folder, "steps"),
                TimeLimitS = AddinConfig.Load(files.ConfigPath).WorkerTimeLimitS(ReadDataRun.Command),
                RoadSurfaces = RoadSurfaceChoices.ForRequest(ProjectModes.Apply(JObject.Parse(WorkFiles.ReadText(files.ConfigPath)), settings.Project.ProjectMode), settings.Project.RoadSurfaces),
                ProjectMode = settings.Project.ProjectMode,
            };
            Directory.CreateDirectory(work);
            worker.Start(request.Write(work), work, request, answer => ended.Invoke(Answered(folder, runId, plan, table, label, answer, out string line), line));
        }

        // The answer of the worker, the plot table copied beside its outputs and the record written last, so the folder is a read only when whole
        public static bool Answered(string folder, string runId, ReadPlan plan, string table, string label, WorkerAnswer answer, out string line)
        {
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                line = RunTexts.ReadWorker(answer?.Status ?? "with no answer", answer?.Message ?? "");
                return false;
            }
            try
            {
                var outputs = (answer.Outputs ?? new List<WorkerFile>()).ToDictionary(output => output.Path, output => output.Sha256, StringComparer.OrdinalIgnoreCase);
                string copy = Path.Combine(folder, ReadDataRun.PlotTableFile);
                File.Copy(table, copy, true);
                outputs[copy] = FileHash.Sha256(copy);
                var issues = answer.Issues ?? new List<string>();
                Write(folder, runId, plan, outputs, JObject.FromObject(answer.Counts ?? new Dictionary<string, int>()), issues);
                line = RunTexts.ReadDone(label, outputs.Count, issues.Count);
                return true;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                line = failure.Message;
                return false;
            }
        }

        // The record of a read of step 2, by the code of ReadDataRun, so 4 Check and the later steps take this folder as any read. It has no picks
        private static void Write(string folder, string runId, ReadPlan plan, IReadOnlyDictionary<string, string> outputs, JObject counts, IReadOnlyList<string> issues)
        {
            JObject record = ReadDataRun.Recorded(runId, plan, outputs, counts, issues, new JObject());
            WorkFiles.WriteText(Path.Combine(folder, ReadDataRun.RecordFile), runId, record.ToString(Formatting.Indented));
        }

        private static IReadOnlyList<string> Strings(JToken token)
        {
            return token is JArray list ? list.Select(item => (string)item).ToList() : new List<string>();
        }

        // The rows the folder of the project gives, none when the config names no folder or the folder does not read, with why
        private static FolderRoles.Found Found(PaneSettings settings)
        {
            JObject config = ConfigOf(settings);
            string root = config?["inputRoot"]?.Type == JTokenType.String ? (string)config["inputRoot"] : null;
            try
            {
                return FolderRoles.Of(config, root, settings.Project.RoadSurfaces);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                var found = new FolderRoles.Found();
                found.Left.Add(failure.Message);
                return found;
            }
        }

        private static IReadOnlyDictionary<string, string> Outputs(JObject record)
        {
            return ((JArray)record["outputs"] ?? new JArray()).ToDictionary(output => (string)output["path"], output => (string)output["sha256"], StringComparer.OrdinalIgnoreCase);
        }

        private static JObject ConfigOf(PaneSettings settings)
        {
            try
            {
                return string.IsNullOrEmpty(settings?.ConfigFile) || !File.Exists(settings.ConfigFile) ? null
                    : ProjectModes.Apply(JObject.Parse(WorkFiles.ReadText(settings.ConfigFile)), settings.Project.ProjectMode);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidDataException)
            {
                return null;
            }
        }
    }
}
