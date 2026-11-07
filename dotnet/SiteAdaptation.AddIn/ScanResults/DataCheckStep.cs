using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Scan;

using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.ScanResults
{
    // R620. Button 2 Read data runs the worker command dataCheck (R600) on the input folder of the settings before the read, by the launch,
    // the time limit of config (addin.workerTimeLimitS.dataCheck) and the answer of the worker the scan of R438 uses, and Revit does not
    // wait (R347, R375). Its rows go in the section Data check of the window What the tool read: with the values of the scan when the
    // read goes on, alone when the read cannot go on. A check that gives no result stops the press and says why, the read never goes on
    // as if it had read. CHOSEN by Lead: the layout is the smallest that shows the rows, Bader may change it after the screenshot
    public static class DataCheckStep
    {
        public const string Command = "dataCheck";
        public const string FolderPrefix = "datacheck_";
        public const string RunIdSuffix = "-datacheck";

        // The window with the check alone, a stand-in in the tests. The user can only close it
        public static Action<IntPtr, DataCheckResult> ShowAlone { get; set; } = (owner, result) => ScanResultsDialog.ShowCheck(owner, result);

        // The work folders whose check runs now, so a second press says so and starts nothing. A scan of the same folder counts too
        private static readonly HashSet<string> Running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The stop the read's own preview gave for a run, kept while the check runs, since the preview comes before the check
        private static readonly ConcurrentDictionary<string, string> Held = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        public static bool IsRunning(string workFolder)
        {
            lock (Running)
            {
                return Running.Contains(Path.GetFullPath(workFolder));
            }
        }

        public static ButtonBody Wrap(ButtonBody read, IWorkerStart worker, WorkerDocument document, PaneSettings settings, IntPtr owner)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }
            if (read.NotBuilt != null || read.Start == null || worker == null)
            {
                return read;
            }
            return new ButtonBody
            {
                // The preview of the read stops a folder with no past schedules before any check could run, so its stop is kept and told after
                // the check, in the words the flow gives it. A preview cannot wait for the worker
                Preview = context =>
                {
                    try
                    {
                        return read.Preview(context);
                    }
                    catch (Exception stop)
                    {
                        Held[context.RunId] = stop.Message;
                        return new RunSummary(null, null);
                    }
                },
                Apply = read.Apply, ReadBack = read.ReadBack, Done = read.Done, Confirm = read.Confirm, Outside = read.Outside,
                Start = (context, done) => Start(read, worker, document, settings, owner, context, done),
            };
        }

        // R700, R750. The parameter sheet of the project settings, else the one config names, so the need sharedParameters reads it. Null when
        // neither is on disk, and the need then says missing. The raw read of card 1 takes the same one from Common
        public static WorkerFile SheetInput(PaneSettings settings)
        {
            return DataCheckSheet.Input(settings.Project, settings.ConfigFile);
        }

        private static void Start(ButtonBody read, IWorkerStart worker, WorkerDocument document, PaneSettings settings, IntPtr owner, RunContext context, Action<RunLater> done)
        {
            Held.TryRemove(context.RunId, out string held);
            string work = Path.GetFullPath(context.WorkFolder);
            string folder = WorkFolder.InputRoot(settings.ConfigFile, WorkFolder.ReadIfThere);
            if (string.IsNullOrEmpty(settings.ConfigFile) || string.IsNullOrEmpty(folder))
            {
                done(RunLater.Failed(ScanView.Fill(DataCheckText.NoFolder, context.Label)));
                return;
            }
            if (!Directory.Exists(folder))
            {
                done(RunLater.Failed(ScanView.Fill(DataCheckText.FolderGone, context.Label)));
                return;
            }
            if (ScanStep.IsRunning(work) || IsRunning(work))
            {
                throw new InvalidOperationException(ScanView.Fill(DataCheckText.RunningLine, context.Label));
            }
            lock (Running)
            {
                if (!Running.Add(work))
                {
                    throw new InvalidOperationException(ScanView.Fill(DataCheckText.RunningLine, context.Label));
                }
            }
            int taken = 0;
            Action<RunLater> end = later =>
            {
                // Once, whichever of the answer and a failure of the start comes first
                if (System.Threading.Interlocked.Exchange(ref taken, 1) == 1)
                {
                    return;
                }
                lock (Running)
                {
                    Running.Remove(work);
                }
                done(later);
            };
            string output = Path.Combine(work, FolderPrefix + context.RunId);
            WorkerRequest request;
            string requestPath;
            try
            {
                request = new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId + RunIdSuffix, Document = document, Command = Command,
                    // The empty SHA-256 of these two roles is the one R600 allows this command alone
                    Inputs = new List<WorkerFile> { new WorkerFile { Role = DataCheckStepRoles.Folder, Path = folder }, new WorkerFile { Role = ReadInputs.Config, Path = settings.ConfigFile } }
                        .Concat(new[] { SheetInput(settings) }).Where(input => input != null).ToList(),
                    Scope = new WorkerScope { Kind = "project" }, OutputFolder = output,
                    TimeLimitS = AddinConfig.Load(settings.ConfigFile).WorkerTimeLimitS(Command),
                };
                Directory.CreateDirectory(work);
                requestPath = request.Write(work);
            }
            catch (Exception failure)
            {
                end(RunLater.Failed(ScanView.Fill(DataCheckText.NotStarted, context.Label, failure.Message)));
                return;
            }
            try
            {
                worker.Start(requestPath, work, request, answer => Answered(read, owner, context, end, output, held, answer));
            }
            catch (Exception failure)
            {
                end(RunLater.Failed(ScanView.Fill(DataCheckText.NotStarted, context.Label, failure.Message)));
            }
        }

        // In the API context of Revit, by the hand of the pane (LaunchedWorker.Hand)
        private static void Answered(ButtonBody read, IntPtr owner, RunContext context, Action<RunLater> end, string output, string held, WorkerAnswer answer)
        {
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                end(RunLater.Failed(ScanView.Fill(DataCheckText.NotFinished, context.Label, (answer?.Status ?? "with no answer") + (string.IsNullOrEmpty(answer?.Message) ? "" : ", " + answer.Message))));
                return;
            }
            string file = Path.Combine(output, DataCheckRows.FileName);
            if (!File.Exists(file))
            {
                end(RunLater.Failed(ScanView.Fill(DataCheckText.NoResult, context.Label, "the worker ended with " + answer.Status)));
                return;
            }
            DataCheckResult result;
            try
            {
                string plots = Path.Combine(output, DataCheckRows.PlotsFileName);
                result = DataCheckRows.ReadResult(WorkFiles.ReadText(file), File.Exists(plots) ? WorkFiles.ReadText(plots) : null);
            }
            catch (Exception failure)
            {
                end(RunLater.Failed(ScanView.Fill(DataCheckText.BadResult, context.Label, failure.Message)));
                return;
            }
            try
            {
                if (held != null)
                {
                    ShowAlone(owner, result);
                    end(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(held))));
                    return;
                }
                // The gate of the scan shows these rows with the values of the scan, one window. If the scan never reaches its gate the
                // rows are shown alone when the read ends
                ScanGate.PendingCheck = result;
                read.Start(context, later =>
                {
                    DataCheckResult left = ScanGate.TakeCheck();
                    if (left != null)
                    {
                        try
                        {
                            ShowAlone(owner, left);
                        }
                        catch (Exception failure)
                        {
                            end(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message))));
                            return;
                        }
                    }
                    end(later);
                });
            }
            catch (Exception failure)
            {
                ScanGate.TakeCheck();
                end(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message))));
            }
        }
    }

    // R600. The role the command dataCheck takes its folder by
    internal static class DataCheckStepRoles
    {
        public const string Folder = "scanFolder";
    }
}
