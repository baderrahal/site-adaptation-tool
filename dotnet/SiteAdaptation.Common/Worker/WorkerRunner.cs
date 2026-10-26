using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SiteAdaptation.Common
{
    // R347. One run of the worker on one request: the checks that refuse a request before any read, the free memory against the
    // measured peak of each input plus the floor, the hash of every input before and after, the command, then the answer as its
    // last act, every file by a temporary name and a rename. CadReader.exe --request <file> runs it, the tests run it in process
    public static class WorkerRunner
    {
        // R345. The steps of the plan builder the C# gives so far with the input roles each reads, the build refuses another step
        public static readonly Dictionary<string, string[]> Steps = new Dictionary<string, string[]>
        {
            [FaceStep.Name] = new[] { "baseplanPlots", "plotsCsv", "labels" },
        };

        private sealed class Stop : Exception
        {
            public readonly int Code;

            public Stop(int code, string message) : base(message)
            {
                Code = code;
            }
        }

        public static int Run(string requestPath, IDictionary<string, WorkerCommand> commands, Func<double?> freeMiB)
        {
            var watch = Stopwatch.StartNew();
            string folder = Path.GetDirectoryName(Path.GetFullPath(requestPath));
            var answer = new WorkerAnswer { Issues = new List<string>(), Counts = new Dictionary<string, int>(), Outputs = new List<WorkerFile>(), Opened = new List<WorkerOpened>() };
            answer.FreeMiBBefore = freeMiB();
            WorkerRequest request;
            try
            {
                request = WorkerRequest.Read(requestPath);
            }
            catch (Exception failure)
            {
                // With no request there is no run id to name an answer by, the add-in names the code
                Console.Error.WriteLine("the request could not be read: " + failure.Message);
                return WorkerExit.Refused;
            }
            if (request == null || !WorkerFiles.IsRunId(request.RunId))
            {
                Console.Error.WriteLine("the request names no run id of letters, digits, dash and underscore: " + requestPath);
                return WorkerExit.Refused;
            }
            answer.RunId = request.RunId;
            answer.Document = request.Document;
            int code;
            try
            {
                Clear(folder, request, answer);
                code = Execute(request, commands, freeMiB, answer);
                answer.Message = WorkerExit.Meaning(code);
            }
            catch (Stop stop)
            {
                code = stop.Code;
                answer.Message = stop.Message;
            }
            catch (Exception failure)
            {
                code = WorkerExit.Failed;
                answer.Message = failure.GetType().Name + ": " + failure.Message;
            }
            answer.ExitCode = code;
            answer.Status = WorkerExit.Status(code);
            answer.FreeMiBAfter = freeMiB();
            answer.PeakPrivateMiB = WorkerMemory.PeakPrivateMiB();
            answer.Seconds = watch.Elapsed.TotalSeconds;
            answer.Library = Library();
            WorkFiles.WriteText(WorkerFiles.AnswerPath(folder, request.RunId), request.RunId, answer.ToJson());
            return code;
        }

        // An answer of the run id left by an earlier attempt goes first, so no answer but this run's is ever read under its name, then
        // the temporary files of this run id in the work, output and steps folders. Those of other run ids stay, another run may be
        // writing them now
        private static void Clear(string folder, WorkerRequest request, WorkerAnswer answer)
        {
            string earlier = WorkerFiles.AnswerPath(folder, request.RunId);
            if (File.Exists(earlier))
            {
                File.Delete(earlier);
            }
            foreach (string place in new[] { folder, request.OutputFolder, request.StepsFolder })
            {
                try
                {
                    WorkFiles.DeleteRun(place, request.RunId);
                }
                catch (IOException failure)
                {
                    answer.Issues.Add(failure.Message);
                }
            }
        }

        private static int Execute(WorkerRequest request, IDictionary<string, WorkerCommand> commands, Func<double?> freeMiB, WorkerAnswer answer)
        {
            if (request.FormatVersion > WorkerRequest.CurrentFormatVersion || request.FormatVersion < 1)
            {
                throw new Stop(WorkerExit.Refused, "the request has formatVersion " + request.FormatVersion + ", this worker reads 1 to " + WorkerRequest.CurrentFormatVersion);
            }
            if (!WorkerRequest.Commands.Contains(request.Command))
            {
                throw new Stop(WorkerExit.Refused, "the command " + request.Command + " is not one of " + string.Join(", ", WorkerRequest.Commands));
            }
            WorkerCommand command;
            if (!commands.TryGetValue(request.Command, out command))
            {
                throw new Stop(WorkerExit.Refused, "the command " + request.Command + " is not built yet, the worker runs " + string.Join(", ", commands.Keys));
            }
            List<WorkerFile> inputs = request.Inputs ?? new List<WorkerFile>();
            foreach (string step in request.Steps ?? new List<string>())
            {
                if (!Steps.ContainsKey(step))
                {
                    throw new Stop(WorkerExit.Refused, "the step " + step + " is not one the C# build gives, it gives " + string.Join(", ", Steps.Keys.OrderBy(name => name)));
                }
                foreach (string role in Steps[step].Where(role => inputs.All(input => input.Role != role)))
                {
                    throw new Stop(WorkerExit.Refused, "the step " + step + " needs the input " + role + ", the request names none");
                }
                if (string.IsNullOrEmpty(request.StepsFolder))
                {
                    throw new Stop(WorkerExit.Refused, "the request names steps and no stepsFolder for their files");
                }
            }
            WorkerFile configInput = inputs.FirstOrDefault(input => input.Role == "config");
            if (configInput == null)
            {
                throw new Stop(WorkerExit.Refused, "the request names no config, the settings file of the pane");
            }
            foreach (WorkerFile input in inputs)
            {
                if (string.IsNullOrEmpty(input.Path))
                {
                    throw new Stop(WorkerExit.Refused, "the input " + input.Role + " is not chosen");
                }
                if (WorkFiles.IsTemporary(input.Path))
                {
                    throw new Stop(WorkerExit.Refused, "the input " + input.Role + " has a temporary name and is never read: " + input.Path);
                }
                if (!File.Exists(input.Path))
                {
                    throw new Stop(WorkerExit.Refused, "the input " + input.Role + " is not on disk: " + input.Path);
                }
            }
            // The config is small and read first, it names the measured peak of each large input and the floor
            AddinConfig config = AddinConfig.Load(configInput.Path);
            foreach (WorkerFile input in inputs)
            {
                double? peak = config.WorkerPeakMiB(input.Role);
                if (peak.HasValue)
                {
                    string refusal = WorkerMemory.Refusal(Path.GetFileName(input.Path), freeMiB(), peak.Value, config.MinFreeGiB);
                    if (refusal != null)
                    {
                        throw new Stop(WorkerExit.Memory, refusal);
                    }
                }
            }
            foreach (WorkerFile input in inputs)
            {
                var opened = new WorkerOpened { Role = input.Role, Path = input.Path, Sha256Before = FileHash.Sha256(input.Path) };
                answer.Opened.Add(opened);
                if (!string.Equals(opened.Sha256Before, input.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Stop(WorkerExit.InputChanged, "the input " + input.Role + " changed since the request, its SHA-256 is not the one the request gives: " + input.Path);
                }
            }
            List<string> written = command(request, answer.Issues, answer.Counts) ?? new List<string>();
            foreach (WorkerOpened opened in answer.Opened)
            {
                opened.Sha256After = FileHash.Sha256(opened.Path);
            }
            WorkerOpened changed = answer.Opened.FirstOrDefault(opened => opened.Sha256After != opened.Sha256Before);
            foreach (string path in written)
            {
                answer.Outputs.Add(new WorkerFile { Role = "output", Path = path, Sha256 = FileHash.Sha256(path) });
            }
            if (changed != null)
            {
                throw new Stop(WorkerExit.InputChanged, "the input " + changed.Role + " changed while it was read, nothing of the run is used (R1): " + changed.Path);
            }
            return WorkerExit.Done;
        }

        // The version and the file of Common and of Newtonsoft.Json this process loaded
        public static string Library()
        {
            Assembly common = typeof(WorkerRunner).Assembly;
            Assembly json = typeof(Newtonsoft.Json.JsonConvert).Assembly;
            string jsonFile = json.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? json.GetName().Version.ToString();
            return "SiteAdaptation.Common " + common.GetName().Version + " " + common.Location + ", Newtonsoft.Json " + jsonFile + " " + json.Location;
        }
    }
}
