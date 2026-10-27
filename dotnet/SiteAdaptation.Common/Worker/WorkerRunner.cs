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
        // R345. The steps the C# gives so far with the input roles each reads, a role ending in a colon and a star any role of that
        // name and a colon, at least one, roles parted by a bar any one of them, and the command that writes each: the plan builder steps of build and the read steps of read
        // (R348 to R350). A request of another step, or of a step of another command, is refused
        public static readonly Dictionary<string, string[]> Steps = new Dictionary<string, string[]>
        {
            [FaceStep.Name] = new[] { FaceStep.BaseplanRoles, "plotsCsv", "labels" },
            // R354. The clusters of a build by keyRules, on the reads of the worker, plots.csv, labels.csv and the rules file, no analysis since Q089
            [SiteClusters.StepName] = BuildSteps.ClusterRoles,
            // R355. The fronts of a build, on the curbs and sales reads of the worker and the road surfaces of the level profile
            [Fronts.StepName] = BuildSteps.FrontRoles,
            // R357. The hands of a build, on the reads of the clusters and the fronts and the prototype files of the folder
            [Hands.StepName] = BuildSteps.HandRoles,
            // R356. The link frame of a build, on the inputs of the hands
            [LinkFrames.StepName] = BuildSteps.HandRoles,
            // R359. The levels of a build, on the reads of the clusters and the fronts with the road surfaces of the level profile
            [KeyLevels.StepName] = BuildSteps.LevelRoles,
            // R358. The floors of a build, on the inputs of the levels and the prototype files the link of the hand takes
            [FloorOutlines.StepName] = BuildSteps.FloorRoles,
            // R360. The overrides of a build, on the inputs of the floors and the override list
            [PlanOverrides.StepName] = BuildSteps.OverrideRoles,
            // R361. The link parameters of a build, on the inputs of the overrides
            [LinkParameters.StepName] = BuildSteps.OverrideRoles,
            // R401. The status of each sourced value of a build, on the inputs of the overrides
            [ValueStatus.StepName] = BuildSteps.OverrideRoles,
            [ReadSteps.Labels] = new[] { "baseplan" },
            [ReadSteps.Blocks] = new[] { "villaBlocks" },
            [ReadSteps.Setbacks] = new[] { "villaBlocks" },
            [ReadSteps.Curbs] = new[] { "curbs" },
            [ReadSteps.Rows] = new[] { "curbs" },
            [ReadSteps.Surfaces] = new[] { RoadSurfaceRole + "*" },
            [ReadSteps.RoadLevels] = new[] { "baseplan", "plotsCsv", "labels", RoadSurfaceRole + "*" },
            [ReadSteps.AreaKey] = new[] { "dlus", "sales" },
            [ReadSteps.DlusSetbacks] = new[] { "sales" },
            [ReadSteps.Sales] = new[] { "sales" },
            // R351. The location plan, numbered by the schedule fit on the faces of the baseplan read, with its codes from the sales workbook
            [LocationPlan.LabelStepName] = LocationPlan.Roles,
            [LocationPlan.FitStepName] = LocationPlan.Roles,
            [LocationPlan.CheckStepName] = LocationPlan.Roles,
            [LocationPlan.FacesStepName] = LocationPlan.Roles,
            // R352. The plot table, on the location plan, the faces, the footprint blocks and the area key of the read
            [PlotTable.StepName] = PlotTable.Roles,
            // R353. The project position file of a key read and the check points of button 1, steps of the command position
            [ProjectPositionFile.PositionStep] = ProjectPositionFile.PositionRoles,
            [ProjectPositionFile.CheckPointsStep] = ProjectPositionFile.CheckPointRoles,
        };

        public static readonly Dictionary<string, string> StepCommand = Steps.Keys.ToDictionary(step => step,
            step => step == FaceStep.Name || BuildSteps.Names.Contains(step) ? "build" : step == ProjectPositionFile.PositionStep || step == ProjectPositionFile.CheckPointsStep ? "position" : "read");

        // R349. The role of a road surface file in a request, roadSurface:<file name with no extension>, one per LandXML file
        public const string RoadSurfaceRole = "roadSurface:";

        private static bool Holds(IEnumerable<WorkerFile> inputs, string role)
        {
            if (role.Contains("|"))
            {
                return role.Split('|').Any(one => Holds(inputs, one));
            }
            return role.EndsWith(":*", StringComparison.Ordinal) ? inputs.Any(input => input.Role != null && input.Role.StartsWith(role.Substring(0, role.Length - 1), StringComparison.Ordinal))
                : inputs.Any(input => input.Role == role);
        }

        public static int Run(string requestPath, IDictionary<string, WorkerCommand> commands, Func<double?> freeMiB)
        {
            return Run(requestPath, commands, freeMiB, WorkerMemory.PrivateMiB);
        }

        // privateMiB gives the private memory the worker holds now, which the rest of a whole peak is held against (Q083)
        public static int Run(string requestPath, IDictionary<string, WorkerCommand> commands, Func<double?> freeMiB, Func<double?> privateMiB)
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
                code = Execute(request, commands, freeMiB, privateMiB, answer, made);
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

        private static int Execute(WorkerRequest request, IDictionary<string, WorkerCommand> commands, Func<double?> freeMiB, Func<double?> privateMiB, WorkerAnswer answer, List<WorkerContext> made)
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
                if (StepCommand[step] != request.Command)
                {
                    throw new WorkerStop(WorkerExit.Refused, "the step " + step + " is a step of the command " + StepCommand[step] + ", the request is " + request.Command);
                }
                // R345. A plot list needs no labels.csv, as a cover file of plan_from_book.py (H1 of the fifth review of PR 55)
                foreach (string role in Steps[step].Where(role => !Holds(inputs, role) && (role != PlotScope.LabelsRole || PlotScope.NeedsLabels(request.Scope))))
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
            // Q083. A whole read of button 2 peaked at 3,891 MiB where the Road Layout alone took 2,612, so a command whose whole peak
            // config names is held against all of it and the floor before its first read
            double? whole = config.WorkerCommandPeakMiB(request.Command);
            if (whole.HasValue)
            {
                string refusal = WorkerMemory.CommandRefusal(request.Command, freeMiB(), whole.Value, null, config.MinFreeGiB);
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
            var context = new WorkerContext(request, answer, config, freeMiB, privateMiB);
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
