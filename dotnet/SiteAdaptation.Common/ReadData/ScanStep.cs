using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.ReadData
{
    // R438. What the gate of the scan tells button 2: go on with the read or stop with nothing written, and the line to show
    public sealed class ScanGateAnswer
    {
        public bool GoOn { get; set; }

        // The pane line when the read goes on, the reason when it stops
        public string Line { get; set; }
    }

    // R438, R439, Q246. Button 2 Read data scans its inputs first by the worker command scan of R420 to R427, or reuses a scan of the
    // same files in the work folder, then the gate the add-in gives decides on scan.json whether the window opens before the read
    public static class ScanStep
    {
        public const string Command = "scan";
        public const string FolderPrefix = "scan_";
        public const string ResultFile = "scan.json";

        // The scan has its own run id beside the read of the same run, so its request and answer files never meet those of the read
        public const string RunIdSuffix = "-scan";

        public const string ClearLine = "Read {0} files, {1} plots, every value clear.";
        public const string NotFinishedLine = "The scan did not finish, {0}. The read goes on without it.";
        public const string StoppedLine = "{0}: the read stopped at the scan, nothing was written.";
        public const string GateFailedLine = "{0}: the check of the scan failed, nothing was written. {1}";
        public const string RunningLine = "{0}: a scan of this work folder runs now, wait for its end.";
        public const string NoScanFile = "the scan wrote no " + ResultFile;
        public const string NoSettings = "the scan names no settings file";

        private const string CadRole = "scanCad:";
        private const string ExcelRole = "scanExcel:";
        private const string PdfRole = "scanPdf:";

        private static readonly string[] CadExtensions = { ".dwg", ".dxf" };
        private static readonly string[] ExcelExtensions = { ".xlsx" };
        private static readonly string[] PdfExtensions = { ".pdf" };

        // The inputs of button 2 the scan takes, in this order. Curbs and Road surface are left out: Lane D measured the scan of the Road
        // Layout drawing at over 40 minutes in the pair trace (Q250), and the road surfaces are LandXML no scan reads
        private static readonly string[] Scanned = { InputNames.Baseplan, InputNames.VillaBlocks, InputNames.Dlus, InputNames.Sales, InputNames.PlotNumbers };

        // The work folders whose scan runs now, so a second click says so and starts nothing
        private static readonly HashSet<string> Running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string FolderOf(string workFolder, string runId)
        {
            return Path.Combine(workFolder, FolderPrefix + runId);
        }

        // R439. scan.json of the newest scan folder of the work folder, by its run id, which starts with the button and the time, else null
        public static string Latest(string workFolder)
        {
            return Scans(workFolder).FirstOrDefault();
        }

        // R439. scan.json of the newest scan folder whose inputs are the same files by name, size and SHA-256 as these, else null. The
        // SHA-256 an input carries is taken as its hash, Inputs takes it from the file just before, so a prototype of a gigabyte is not
        // hashed twice in one press
        public static string Reusable(string workFolder, IReadOnlyList<WorkerFile> inputs)
        {
            if (inputs == null || inputs.Count == 0 || inputs.Any(item => string.IsNullOrEmpty(item.Path) || !File.Exists(item.Path)))
            {
                return null;
            }
            List<string> sized = inputs.Select(item => new FileInfo(item.Path)).Select(file => file.Name + "|" + file.Length).OrderBy(key => key, StringComparer.Ordinal).ToList();
            List<string> hashed = null;
            foreach (string scan in Scans(workFolder))
            {
                JArray listed;
                try
                {
                    listed = JObject.Parse(WorkFiles.ReadText(scan))["inputs"] as JArray;
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    continue;
                }
                if (listed == null || !Keys(listed, false).SequenceEqual(sized))
                {
                    continue;
                }
                hashed = hashed ?? inputs.Select(item => new FileInfo(item.Path).Name + "|" + new FileInfo(item.Path).Length + "|" + (item.Sha256 ?? FileHash.Sha256(item.Path)).ToLowerInvariant())
                    .OrderBy(key => key, StringComparer.Ordinal).ToList();
                if (Keys(listed, true).SequenceEqual(hashed))
                {
                    return scan;
                }
            }
            return null;
        }

        // R438. The scan request of the inputs of button 2: scanCad:<n> for each drawing, scanExcel:<n> for each workbook, scanPdf:<n>
        // for each PDF, n from 1 in that order, prototype:<file name> for each .rvt of the Prototypes folder, then config. roles names
        // the role of each input of the pane by its name there. An input with no file on disk is left out, the flow stops on it first
        public static IReadOnlyList<WorkerFile> Inputs(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string prototypesFolder)
        {
            var byRole = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (RunInput input in pane ?? new RunInput[0])
            {
                if (!input.IsFolder && !string.IsNullOrEmpty(input.Path) && File.Exists(input.Path) && roles.TryGetValue(input.Name, out string role) && !byRole.ContainsKey(role))
                {
                    byRole[role] = Path.GetFullPath(input.Path);
                }
            }
            var files = new List<WorkerFile>();
            foreach (KeyValuePair<string, string[]> kind in new[]
            {
                new KeyValuePair<string, string[]>(CadRole, CadExtensions), new KeyValuePair<string, string[]>(ExcelRole, ExcelExtensions), new KeyValuePair<string, string[]>(PdfRole, PdfExtensions),
            })
            {
                var paths = Scanned.Where(byRole.ContainsKey).Select(role => byRole[role]).Where(path => kind.Value.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                files.AddRange(paths.Select((path, index) => Marked(kind.Key + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), path)));
            }
            if (!string.IsNullOrEmpty(prototypesFolder) && Directory.Exists(prototypesFolder))
            {
                files.AddRange(Directory.GetFiles(prototypesFolder, "*.rvt").OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                    .Select(path => Marked(BuildSteps.PrototypeRole + Path.GetFileName(path), Path.GetFullPath(path))));
            }
            if (byRole.TryGetValue(ReadInputs.Config, out string config))
            {
                files.Add(Marked(ReadInputs.Config, config));
            }
            return files;
        }

        // R438. Button 2 with the scan before its read. The start scans, or reuses a scan of the same inputs, then asks the gate on
        // scan.json: go on starts the read as before with the line of the gate first, stop ends the run with its line and writes
        // nothing. CHOSEN by Code in SCAN-UI: a scan that fails or runs past its time limit never blocks the read, the read goes on with
        // one line that says why the scan did not finish
        public static ButtonBody Wrap(ButtonBody read, IWorkerStart worker, WorkerDocument document, Func<IReadOnlyList<WorkerFile>> scanInputs, Func<string, ScanGateAnswer> gate)
        {
            if (read == null || scanInputs == null || gate == null)
            {
                throw new ArgumentNullException(read == null ? nameof(read) : scanInputs == null ? nameof(scanInputs) : nameof(gate));
            }
            if (read.NotBuilt != null || read.Start == null)
            {
                return read;
            }
            return new ButtonBody
            {
                Preview = context =>
                {
                    if (IsRunning(context.WorkFolder))
                    {
                        throw new InvalidOperationException(string.Format(RunningLine, context.Label));
                    }
                    return read.Preview(context);
                },
                Apply = read.Apply, ReadBack = read.ReadBack, Done = read.Done, Confirm = read.Confirm, Outside = read.Outside,
                Start = (context, done) => Start(read, worker, document, scanInputs, gate, context, done),
            };
        }

        public static bool IsRunning(string workFolder)
        {
            lock (Running)
            {
                return Running.Contains(Path.GetFullPath(workFolder));
            }
        }

        private static void Start(ButtonBody read, IWorkerStart worker, WorkerDocument document, Func<IReadOnlyList<WorkerFile>> scanInputs, Func<string, ScanGateAnswer> gate, RunContext context, Action<RunLater> done)
        {
            string work = Path.GetFullPath(context.WorkFolder);
            lock (Running)
            {
                if (!Running.Add(work))
                {
                    throw new InvalidOperationException(string.Format(RunningLine, context.Label));
                }
            }
            int taken = 0;
            Action<string, string> scanned = (scan, notFinished) =>
            {
                if (Interlocked.Exchange(ref taken, 1) == 1)
                {
                    return;
                }
                lock (Running)
                {
                    Running.Remove(work);
                }
                GoOn(read, gate, context, done, scan, notFinished);
            };
            try
            {
                IReadOnlyList<WorkerFile> inputs = scanInputs();
                string reused = Reusable(work, inputs);
                if (reused != null)
                {
                    scanned(reused, null);
                    return;
                }
                string configPath = inputs.FirstOrDefault(item => item.Role == ReadInputs.Config)?.Path ?? throw new InvalidDataException(NoSettings);
                var request = new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId + RunIdSuffix, Document = document, Command = Command,
                    Inputs = inputs.Select(item => new WorkerFile { Role = item.Role, Path = item.Path, Sha256 = item.Sha256 }).ToList(),
                    Scope = new WorkerScope { Kind = "project" }, OutputFolder = FolderOf(work, context.RunId),
                    TimeLimitS = AddinConfig.Load(configPath).WorkerTimeLimitS(Command),
                };
                if (worker == null)
                {
                    throw new InvalidOperationException("no worker is set to run the scan");
                }
                Directory.CreateDirectory(work);
                string requestPath = request.Write(work);
                worker.Start(requestPath, work, request, answer =>
                {
                    string scan = Path.Combine(request.OutputFolder, ResultFile);
                    if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
                    {
                        scanned(null, (answer?.Status ?? "with no answer") + ", " + (answer?.Message ?? ""));
                    }
                    else
                    {
                        scanned(File.Exists(scan) ? scan : null, NoScanFile);
                    }
                });
            }
            catch (Exception failure)
            {
                scanned(null, failure.Message);
            }
        }

        private static void GoOn(ButtonBody read, Func<string, ScanGateAnswer> gate, RunContext context, Action<RunLater> done, string scan, string notFinished)
        {
            string line;
            if (scan == null)
            {
                line = string.Format(NotFinishedLine, notFinished);
            }
            else
            {
                ScanGateAnswer answer;
                try
                {
                    answer = gate(scan);
                }
                catch (Exception failure)
                {
                    done(RunLater.Failed(string.Format(GateFailedLine, context.Label, failure.Message)));
                    return;
                }
                if (answer == null || !answer.GoOn)
                {
                    done(RunLater.Failed(string.IsNullOrEmpty(answer?.Line) ? string.Format(StoppedLine, context.Label) : answer.Line));
                    return;
                }
                line = answer.Line;
            }
            // The read may start after the worker of the scan answered, outside the flow, so its throw becomes the stop the flow gives
            try
            {
                read.Start(context, later => done(WithLine(later, line)));
            }
            catch (Exception failure)
            {
                done(RunLater.Failed(Joined(line, RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message)))));
            }
        }

        private static RunLater WithLine(RunLater later, string line)
        {
            if (later == null || string.IsNullOrEmpty(line))
            {
                return later;
            }
            return later.Failure != null ? RunLater.Failed(Joined(line, later.Failure)) : RunLater.Done(Joined(line, later.Message), later.Issues, later.Outputs);
        }

        private static string Joined(string line, string message)
        {
            return string.IsNullOrEmpty(line) ? message : string.IsNullOrEmpty(message) ? line : line + Environment.NewLine + message;
        }

        // The prototypes are over a gigabyte together, so a hash is kept per path, size and write time and a second press of button 2
        // on files that did not change hashes nothing on the Revit thread
        private static readonly Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static WorkerFile Marked(string role, string path)
        {
            var file = new FileInfo(path);
            string key = file.FullName + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;
            string hash;
            lock (Hashes)
            {
                Hashes.TryGetValue(key, out hash);
            }
            if (hash == null)
            {
                hash = FileHash.Sha256(path);
                lock (Hashes)
                {
                    Hashes[key] = hash;
                }
            }
            return new WorkerFile { Role = role, Path = path, Sha256 = hash };
        }

        private static IEnumerable<string> Scans(string workFolder)
        {
            if (string.IsNullOrEmpty(workFolder) || !Directory.Exists(workFolder))
            {
                return Enumerable.Empty<string>();
            }
            return Directory.GetDirectories(workFolder, FolderPrefix + "*").OrderByDescending(folder => Path.GetFileName(folder), StringComparer.Ordinal)
                .Select(folder => Path.Combine(folder, ResultFile)).Where(File.Exists).ToList();
        }

        private static IEnumerable<string> Keys(JArray listed, bool withHash)
        {
            return listed.Select(row => (string)row["name"] + "|" + (long?)row["size"] + (withHash ? "|" + ((string)row["sha256"] ?? "").ToLowerInvariant() : ""))
                .OrderBy(key => key, StringComparer.Ordinal).ToList();
        }
    }
}
