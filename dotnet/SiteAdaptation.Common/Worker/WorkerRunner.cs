using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SiteAdaptation.Common
{
    // R347. One run of the worker on one request: the checks that refuse a request before any read, the free memory against the
    // measured peak of each large input plus the floor before any read and again before each read, the hash of every input before
    // and after, the command, the outputs moved onto their names once every check passed, then the answer as its last act, every
    // file by a temporary name and a rename. CadReader.exe --request <file> runs it, the tests run it in process
    public static class WorkerRunner
    {
        // R345. The steps of the plan builder the C# gives so far with the input roles each reads, the build refuses another step
        public static readonly Dictionary<string, string[]> Steps = new Dictionary<string, string[]>
        {
            [FaceStep.Name] = new[] { "baseplanPlots", "plotsCsv", "labels" },
        };

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
            var made = new List<WorkerContext>();
            int code;
            try
            {
                Clear(folder, request, answer);
                code = Execute(request, commands, freeMiB, answer, made);
                answer.Message = WorkerExit.Meaning(code);
            }
            catch (WorkerStop stop)
            {
                code = stop.Code;
                answer.Message = stop.Message;
            }
            catch (Exception failure)
            {
                code = WorkerExit.Failed;
                answer.Message = failure.GetType().Name + ": " + failure.Message;
            }
            if (code != WorkerExit.Done)
            {
                // Nothing of a run that did not pass is used, an earlier output under the name stays as it was
                answer.Outputs.Clear();
                foreach (KeyValuePair<string, string> output in made.SelectMany(context => context.Written))
                {
                    try
                    {
                        File.Delete(output.Key);
                    }
                    catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                    {
                        answer.Issues.Add("the temporary file " + output.Key + " could not be deleted, " + failure.Message);
                    }
                }
            }
            answer.ExitCode = code;
            answer.Status = WorkerExit.Status(code);
            answer.FreeMiBAfter = freeMiB();
            answer.PeakPrivateMiB = WorkerMemory.PeakPrivateMiB();
            answer.Seconds = watch.Elapsed.TotalSeconds;
            answer.Library = Library();
            WorkFiles.WriteText(WorkerFiles.AnswerPath(folder, request.RunId), request.RunId, answer.ToJson());
            if (code == WorkerExit.Done)
            {
                // The marks of the commit stay until the answer is written, so a kill before it finds the commit point
                foreach (string left in WorkFiles.EndCommit(made.SelectMany(context => context.Written).ToList()))
                {
                    Console.Error.WriteLine("a mark of the commit could not be deleted: " + left);
                }
            }
            return code;
        }

        // An answer of the run id left by an earlier attempt goes first, so no answer but this run's is ever read under its name, then
        // the temporary files of this run id in the work, output and steps folders. Those of other run ids stay, another run may be
        // writing them now. The step files of the steps of the request go too (R345), so a run that fails leaves none
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
            StepFile.DeleteSteps(request);
        }

        // R347. Null when the read of an input may go on, else the refusal with both numbers named: for an input of a large read the
        // free memory now against the measured peak of its file, or the largest known, plus the floor of siteBuild.minFreeGB
        internal static string MemoryRefusal(WorkerFile input, AddinConfig config, Func<double?> freeMiB)
        {
            var file = new FileInfo(input.Path);
            double? peak = config.WorkerPeakMiB(input.Role, file.Name, file.Exists ? file.Length : -1);
            return peak.HasValue ? WorkerMemory.Refusal(file.Name, freeMiB(), peak.Value, config.MinFreeGiB) : null;
        }

        private static int Execute(WorkerRequest request, IDictionary<string, WorkerCommand> commands, Func<double?> freeMiB, WorkerAnswer answer, List<WorkerContext> made)
        {
            if (request.FormatVersion > WorkerRequest.CurrentFormatVersion || request.FormatVersion < 1)
            {
                throw new WorkerStop(WorkerExit.Refused, "the request has formatVersion " + request.FormatVersion + ", this worker reads 1 to " + WorkerRequest.CurrentFormatVersion);
            }
            if (!WorkerRequest.Commands.Contains(request.Command))
            {
                throw new WorkerStop(WorkerExit.Refused, "the command " + request.Command + " is not one of " + string.Join(", ", WorkerRequest.Commands));
            }
            WorkerCommand command;
            if (!commands.TryGetValue(request.Command, out command))
            {
                throw new WorkerStop(WorkerExit.Refused, "the command " + request.Command + " is not built yet, the worker runs " + string.Join(", ", commands.Keys));
            }
            List<WorkerFile> inputs = request.Inputs ?? new List<WorkerFile>();
            foreach (string step in request.Steps ?? new List<string>())
            {
                if (!Steps.ContainsKey(step))
                {
                    throw new WorkerStop(WorkerExit.Refused, "the step " + step + " is not one the C# build gives, it gives " + string.Join(", ", Steps.Keys.OrderBy(name => name)));
                }
                foreach (string role in Steps[step].Where(role => inputs.All(input => input.Role != role)))
                {
                    throw new WorkerStop(WorkerExit.Refused, "the step " + step + " needs the input " + role + ", the request names none");
                }
                if (string.IsNullOrEmpty(request.StepsFolder))
                {
                    throw new WorkerStop(WorkerExit.Refused, "the request names steps and no stepsFolder for their files");
                }
            }
            // Real coordinates and levels never go into git (CLAUDE.md), as python/parity.py and the Python steps refuse the repo
            foreach (string place in new[] { request.OutputFolder, request.StepsFolder })
            {
                string tree = WorkFiles.RepoAbove(place);
                if (tree != null)
                {
                    throw new WorkerStop(WorkerExit.Refused, "the folder " + place + " lies inside the git work tree " + tree + ", the outputs of a run hold real coordinates and never go into git");
                }
            }
            WorkerFile configInput = inputs.FirstOrDefault(input => input.Role == "config");
            if (configInput == null)
            {
                throw new WorkerStop(WorkerExit.Refused, "the request names no config, the settings file of the pane");
            }
            foreach (WorkerFile input in inputs)
            {
                if (string.IsNullOrEmpty(input.Path))
                {
                    throw new WorkerStop(WorkerExit.Refused, "the input " + input.Role + " is not chosen");
                }
                if (WorkFiles.IsTemporary(input.Path))
                {
                    throw new WorkerStop(WorkerExit.Refused, "the input " + input.Role + " has a temporary name and is never read: " + input.Path);
                }
                if (!File.Exists(input.Path))
                {
                    throw new WorkerStop(WorkerExit.Refused, "the input " + input.Role + " is not on disk: " + input.Path);
                }
            }
            // The config is small and read first, it names the measured peak of each large input and the floor. Before any read each
            // large input is checked against the free memory now, so a run that cannot read all of them reads none
            AddinConfig config = AddinConfig.Load(configInput.Path);
            foreach (WorkerFile input in inputs)
            {
                string refusal = MemoryRefusal(input, config, freeMiB);
                if (refusal != null)
                {
                    throw new WorkerStop(WorkerExit.Memory, refusal);
                }
            }
            foreach (WorkerFile input in inputs)
            {
                var opened = new WorkerOpened { Role = input.Role, Path = input.Path, Sha256Before = FileHash.Sha256(input.Path) };
                answer.Opened.Add(opened);
                if (!string.Equals(opened.Sha256Before, input.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new WorkerStop(WorkerExit.InputChanged, "the input " + input.Role + " changed since the request, its SHA-256 is not the one the request gives: " + input.Path);
                }
            }
            var context = new WorkerContext(request, answer, config, freeMiB);
            made.Add(context);
            command(context);
            foreach (WorkerOpened opened in answer.Opened)
            {
                opened.Sha256After = FileHash.Sha256(opened.Path);
            }
            WorkerOpened changed = answer.Opened.FirstOrDefault(opened => opened.Sha256After != opened.Sha256Before);
            if (changed != null)
            {
                throw new WorkerStop(WorkerExit.InputChanged, "the input " + changed.Role + " changed while it was read, nothing of the run is used (R1): " + changed.Path);
            }
            // Every check passed, so the outputs move onto their names, all of them or none, each replacing the one of an earlier run
            // only now. Their hashes are taken first, so nothing that can fail comes after the commit
            var outputs = context.Written.Select(output => new WorkerFile { Role = "output", Path = output.Value, Sha256 = FileHash.Sha256(output.Key) }).ToList();
            WorkFiles.CommitAll(context.Written, request.RunId);
            answer.Outputs.AddRange(outputs);
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
