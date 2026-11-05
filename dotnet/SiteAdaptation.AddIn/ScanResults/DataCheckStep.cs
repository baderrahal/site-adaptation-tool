using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.ScanResults
{
    // R620. Button 2 Read data runs the worker command dataCheck (R600) on the input folder of the settings first and shows its rows in
    // the section Data check of the window What the tool read. Use these picks goes on with the read, Cancel stops it with nothing
    // written. CHOSEN by Lead: the smallest layout that shows the rows, Bader may change it after the screenshot
    public static class DataCheckStep
    {
        public const string FolderPrefix = "datacheck_";
        public const string StoppedLine = "{0}: the read stopped at the data check, nothing was written.";

        // The window of the section, a stand-in in the tests: true for Use these picks, false for Cancel
        public static Func<IntPtr, IReadOnlyList<DataCheckRow>, bool> Window { get; set; } = (owner, rows) => ScanResultsDialog.ShowCheck(owner, rows);

        // The command runs as a process the worker folder holds and the add-in waits for, a call a test replaces. It gives the exit code
        public static Func<string, TimeSpan, int> RunWorker { get; set; } = StartWorker;

        public static ButtonBody Wrap(ButtonBody read, PaneSettings settings, Func<string> workerExe, IntPtr owner, WorkerDocument document)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }
            if (read.NotBuilt != null || read.Start == null)
            {
                return read;
            }
            var shown = new HashSet<string>(StringComparer.Ordinal);
            // The check runs in the preview, which comes before the checks of the read on its own inputs, so the rows show even where the read
            // stops on a file this folder does not hold. Once for each run, the preview may be asked again by the same run
            return new ButtonBody
            {
                Preview = context =>
                {
                    bool first;
                    lock (shown)
                    {
                        first = shown.Add(context.RunId);
                    }
                    if (first)
                    {
                        IReadOnlyList<DataCheckRow> rows = Check(context, settings, document, out string why);
                        if (rows != null && !Window(owner, rows))
                        {
                            throw new InvalidOperationException(string.Format(StoppedLine, context.Label));
                        }
                    }
                    return read.Preview(context);
                },
                Apply = read.Apply, ReadBack = read.ReadBack, Done = read.Done, Confirm = read.Confirm, Outside = read.Outside, Start = read.Start,
            };
        }

        // The rows of the check, null with why null when the settings name no folder and null with the reason when the command did not finish.
        // The folder lies under the input root of the config and is read in place, the output goes to the work folder only
        public static IReadOnlyList<DataCheckRow> Check(RunContext context, PaneSettings settings, WorkerDocument document, out string why)
        {
            why = null;
            string folder = WorkFolder.InputRoot(settings.ConfigFile, WorkFolder.ReadIfThere);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder) || string.IsNullOrEmpty(settings.ConfigFile))
            {
                return null;
            }
            string work = Path.GetFullPath(context.WorkFolder);
            string output = Path.Combine(work, FolderPrefix + context.RunId);
            try
            {
                Directory.CreateDirectory(output);
                var request = new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId + "-datacheck", Document = document, Command = "dataCheck",
                    Inputs = new List<WorkerFile> { new WorkerFile { Role = "scanFolder", Path = folder }, new WorkerFile { Role = "config", Path = settings.ConfigFile } },
                    Scope = new WorkerScope { Kind = "project" }, OutputFolder = output, TimeLimitS = 300,
                };
                string requestPath = request.Write(work);
                int code = RunWorker(requestPath, TimeSpan.FromSeconds(request.TimeLimitS + 20));
                // The result file is read, never only the exit code
                string result = Path.Combine(output, DataCheckRows.FileName);
                if (!File.Exists(result))
                {
                    why = "the worker ended with code " + code + " and wrote no " + DataCheckRows.FileName;
                    return null;
                }
                return DataCheckRows.Read(WorkFiles.ReadText(result));
            }
            catch (Exception failure)
            {
                why = failure.Message;
                return null;
            }
        }

        private static int StartWorker(string requestPath, TimeSpan limit)
        {
            string exe = LaunchedWorker.BesideAddIn();
            var info = new ProcessStartInfo(exe, "--request \"" + requestPath + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (Process process = Process.Start(info))
            {
                // Read both pipes to their end so a full pipe never holds the worker
                process.OutputDataReceived += (sender, args) => { };
                process.ErrorDataReceived += (sender, args) => { };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit((int)limit.TotalMilliseconds))
                {
                    process.Kill();
                    return -1;
                }
                return process.ExitCode;
            }
        }
    }
}
