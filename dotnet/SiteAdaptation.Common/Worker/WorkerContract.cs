using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SiteAdaptation.Common
{
    // R347. What a command of the worker does: it reads each input of the request through WorkerContext.Input, adds its issues and
    // counts and writes each output through WorkerContext.WriteJson, by a temporary name the runner moves onto its name only once
    // every check of the run passed
    public delegate void WorkerCommand(WorkerContext run);

    // R347. The request the add-in writes into the work folder for the worker, read back by the worker
    public sealed class WorkerRequest
    {
        public const int CurrentFormatVersion = 1;

        // The commands of docs/design/02-folders.md
        public static readonly string[] Commands = { "read", "build", "levelExcel", "reportExcel", "exportChecks", "position", "updateFromReport" };

        public int FormatVersion { get; set; }
        public string RunId { get; set; }
        public WorkerDocument Document { get; set; }
        public string Command { get; set; }
        public List<WorkerFile> Inputs { get; set; } = new List<WorkerFile>();
        public string LevelProfile { get; set; }

        // Q214. The Prototypes folder of the pane a build takes, null takes the folder of pastJob in config (R357)
        public string PrototypeFolder { get; set; }

        public WorkerScope Scope { get; set; }
        public string OutputFolder { get; set; }

        // R345. The steps of the plan builder whose output per plot the worker writes into StepsFolder for the parity
        public List<string> Steps { get; set; }
        public string StepsFolder { get; set; }

        public double TimeLimitS { get; set; }

        public string ToJson()
        {
            return JsonOut.ToJson(this);
        }

        public static WorkerRequest Read(string path)
        {
            return JsonConvert.DeserializeObject<WorkerRequest>(WorkFiles.ReadText(path));
        }

        // Written by a temporary name and a rename, request_<run id>.json in the work folder
        public string Write(string workFolder)
        {
            return WorkFiles.WriteText(WorkerFiles.RequestPath(workFolder, RunId), RunId, ToJson());
        }
    }

    // The document a run belongs to: the full path of the .rvt and the session id the add-in gave it when it opened
    public sealed class WorkerDocument
    {
        public string Path { get; set; }
        public string SessionId { get; set; }
    }

    public sealed class WorkerFile
    {
        public string Role { get; set; }
        public string Path { get; set; }
        public string Sha256 { get; set; }
    }

    // The scope of section 3: project, nbh with its neighbourhood, or plots with the official numbers
    public sealed class WorkerScope
    {
        public string Kind { get; set; }
        public string Nbh { get; set; }
        public List<string> Plots { get; set; }
    }

    public sealed class WorkerOpened
    {
        public string Role { get; set; }
        public string Path { get; set; }
        public string Sha256Before { get; set; }
        public string Sha256After { get; set; }
        // D19: the size and the write time of the input as well, before and after the read
        public long? SizeBefore { get; set; }
        public long? SizeAfter { get; set; }
        public string WriteTimeBefore { get; set; }
        public string WriteTimeAfter { get; set; }
    }

    // R347. What the worker writes as its last act, or the add-in itself for a worker it ended at the time limit
    public sealed class WorkerAnswer
    {
        public string RunId { get; set; }
        public WorkerDocument Document { get; set; }
        public string Status { get; set; }
        public int? ExitCode { get; set; }
        public string Message { get; set; }
        public List<WorkerFile> Outputs { get; set; }
        public Dictionary<string, int> Counts { get; set; }
        public List<string> Issues { get; set; }
        public List<WorkerOpened> Opened { get; set; }
        public double? PeakPrivateMiB { get; set; }
        public double? FreeMiBBefore { get; set; }
        public double? FreeMiBAfter { get; set; }
        public double Seconds { get; set; }
        public string Library { get; set; }

        public string ToJson()
        {
            return JsonOut.ToJson(this);
        }

        public static WorkerAnswer Read(string path)
        {
            return JsonConvert.DeserializeObject<WorkerAnswer>(WorkFiles.ReadText(path));
        }
    }

    // R347. The names of the files of a run in the work folder
    public static class WorkerFiles
    {
        // A run id names files, so it holds letters, digits, dash and underscore only
        private static readonly Regex RunIdForm = new Regex("^[A-Za-z0-9_-]{1,64}$");

        public static bool IsRunId(string runId)
        {
            return runId != null && RunIdForm.IsMatch(runId);
        }

        public static string RequestPath(string folder, string runId)
        {
            return Path.Combine(folder, "request_" + runId + ".json");
        }

        public static string AnswerPath(string folder, string runId)
        {
            return Path.Combine(folder, "answer_" + runId + ".json");
        }

        public static string LogPath(string folder, string runId)
        {
            return Path.Combine(folder, "worker_" + runId + ".log");
        }
    }
}
