using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace SiteAdaptation.TestHost
{
    // R448, developers only. A Revit started by Start-RevitListener.ps1 stays open and runs one job file after the other through a new
    // ButtonsRunHost each, in the ExternalEvent that AddinRunSession uses. RevitListenerLoop of Common holds the queue and all file work
    // on its own thread, this class only carries a job out. QL20: every handler is added once from OnStartup and none is removed, a
    // field says which job they answer for. It never tells Revit to exit
    internal sealed class RevitListener : IExternalEventHandler
    {
        // As AddinRunSession waits on a worker. Tool timings, not project values
        private const double AnswerMinSeconds = 2;
        private const double LookMilliseconds = 1000;
        // Idling raised with no delay spins a core, and between jobs nothing is in a hurry
        private const int IdleSleepMilliseconds = 100;
        // A worker of an earlier job that ran past its wait ends by its own time limit. CHOSEN, past this the job waiting gets an error
        private const double WorkerEndsHoldMinutes = 10;
        private const double OpenCloseMinutes = 5;

        private readonly UIControlledApplication application;
        private readonly RevitListenerLoop loop;
        private readonly RevitListenerSetting setting;
        private readonly JobEvents idleEvents;
        private ExternalEvent phaseEvent;
        private JobEvents events;
        private Job active;
        // The document left open so that the last one could close, closed as soon as the next model is the active one
        private Document leftOpen;
        private DateTime? heldSinceUtc;

        private RevitListener(UIControlledApplication application, RevitListenerLoop loop, RevitListenerSetting setting)
        {
            this.application = application;
            this.loop = loop;
            this.setting = setting;
            string idleLog = Path.Combine(setting.OutFolder, RevitListenerQueue.ListenerLogFile);
            // Plain JSON, between jobs no config has given the design values JsonOut rounds by
            idleEvents = new JobEvents(new TestHostRunner(new TestHostLog { Kind = "listener", StartedUtc = DateTime.UtcNow },
                value => File.WriteAllText(idleLog, JsonConvert.SerializeObject(value, Formatting.Indented)), () => DateTime.UtcNow));
            events = idleEvents;
        }

        public static void Start(UIControlledApplication application, string token)
        {
            RevitListenerSetting setting = RevitListenerSetting.Read(Environment.GetEnvironmentVariable);
            string refused = setting == null ? "the slot of the listener is not set" : setting.Refusal() ?? FileIdentity.CheckTargetFolder(setting.Root);
            if (refused != null)
            {
                throw new InvalidOperationException(refused);
            }
            string documents = WorkRoot.TestHostDocuments(WorkRoot.Of(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), WorkRoot.ReadIfThere));
            var queue = new RevitListenerQueue(setting);
            queue.Prepare();
            var loop = new RevitListenerLoop(queue,
                claim => RevitListenerCheck.Of(claim, token, documents, setting.InputRoots, File.Exists, Directory.Exists), () => DateTime.UtcNow, FreeGb);
            var listener = new RevitListener(application, loop, setting);
            listener.Subscribe();
            loop.Step = listener.CurrentStep;
            // The start script removed the file of an earlier listener before it started this Revit, and waits for this one
            RevitListenerFiles.WriteWhole(Path.Combine(setting.OutFolder, RevitListenerQueue.ReadyFile), new JObject
            {
                { "pid", Process.GetCurrentProcess().Id },
                // S4. Two Revits write one journal folder, so the freeze guard of a send gets the tail of this one by its name
                { "journal", application.ControlledApplication.RecordingJournalFilename },
            }.ToString());
            new Thread(listener.Looks) { IsBackground = true, Name = "SiteAdaptation listener" }.Start();
            new Thread(listener.Beats) { IsBackground = true, Name = "SiteAdaptation listener beat" }.Start();
        }

        // From OnStartup, outside any Revit event, the one place a handler is added
        private void Subscribe()
        {
            phaseEvent = ExternalEvent.Create(this);
            application.DialogBoxShowing += OnDialog;
            application.ControlledApplication.FailuresProcessing += OnFailures;
            application.ControlledApplication.LinkedResourceOpening += OnLinkOpening;
            application.ControlledApplication.LinkedResourceOpened += OnLinkOpened;
            application.Idling += OnIdling;
        }

        private void Looks()
        {
            while (true)
            {
                try
                {
                    loop.Look();
                }
                catch (Exception failure)
                {
                    // A folder that went away or a file held for a moment, the next look tries again. The listener never stops by itself
                    Trace.WriteLine("listener look: " + failure.Message);
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(LookMilliseconds));
            }
        }

        // L6. Apart from Looks, whose one look may hash inputs for many minutes. Beat keeps its own pace
        private void Beats()
        {
            while (true)
            {
                try
                {
                    loop.Beat();
                }
                catch (Exception failure)
                {
                    Trace.WriteLine("listener beat: " + failure.Message);
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(LookMilliseconds));
            }
        }

        // L5. Free physical memory as python/window_lock.py reads it. Null when Windows does not say, the job then goes on with no look
        private static double? FreeGb()
        {
            var status = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
            return GlobalMemoryStatusEx(ref status) ? status.AvailPhys / (1024.0 * 1024.0 * 1024.0) : (double?)null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint Load;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPage;
            public ulong AvailPage;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtended;
        }

        private string CurrentStep()
        {
            return active?.Runner.CurrentStep;
        }

        private void OnDialog(object sender, DialogBoxShowingEventArgs e)
        {
            events.OnDialog(sender, e);
        }

        private void OnFailures(object sender, FailuresProcessingEventArgs e)
        {
            events.OnFailures(sender, e);
        }

        private void OnLinkOpening(object sender, LinkedResourceOpeningEventArgs e)
        {
            events.OnLinkOpening(sender, e);
        }

        private void OnLinkOpened(object sender, LinkedResourceOpenedEventArgs e)
        {
            events.OnLinkOpened(sender, e);
        }

        private void OnIdling(object sender, IdlingEventArgs e)
        {
            e.SetRaiseWithoutDelay();
            if (active != null)
            {
                Tick((UIApplication)sender);
                return;
            }
            RevitListenerRun waiting = loop.Waiting;
            if (waiting == null)
            {
                Thread.Sleep(IdleSleepMilliseconds);
                return;
            }
            if (HeldByWorkerEnds(waiting))
            {
                Thread.Sleep(IdleSleepMilliseconds);
                return;
            }
            Begin(loop.Take());
        }

        // A worker of an earlier job would answer into this job and count as its own, so the job waits until that worker ended
        private bool HeldByWorkerEnds(RevitListenerRun waiting)
        {
            int ends = AddinProbe.Find(new List<string>()).WorkerEnds() ?? 0;
            if (ends == 0)
            {
                heldSinceUtc = null;
                return false;
            }
            DateTime now = DateTime.UtcNow;
            heldSinceUtc = heldSinceUtc ?? now;
            if ((now - heldSinceUtc.Value).TotalMinutes <= WorkerEndsHoldMinutes)
            {
                return true;
            }
            heldSinceUtc = null;
            loop.End(loop.Take(), RevitListenerResult.Failed, ends + " worker of an earlier job did not end in " + WorkerEndsHoldMinutes + " minutes, this job did not start");
            return true;
        }

        private void Begin(RevitListenerRun run)
        {
            try
            {
                active = new Job(this, run);
            }
            catch (Exception failure)
            {
                // Before any step of the job, in the Idling of Revit, which must never see it
                loop.End(run, RevitListenerResult.Failed, "the job could not start: " + failure.GetType().Name + ": " + failure.Message);
                return;
            }
            events = active.Events;
            active.Schedule.Raise(DateTime.UtcNow);
            RaisePhase();
        }

        private void RaisePhase()
        {
            ExternalEventRequest request = phaseEvent.Raise();
            if (request != ExternalEventRequest.Accepted)
            {
                active.Runner.Record(log => log.Notes.Add("the phase event of the listener was not taken: " + request));
            }
        }

        // The clock of the wait for a worker, as OnTick of AddinRunSession keeps it
        private void Tick(UIApplication ui)
        {
            Job job = active;
            if (job.PastLimit && job.Schedule.Waiting)
            {
                job.Host.Stop();
                job.Schedule.Raise(DateTime.UtcNow);
                RaisePhase();
                return;
            }
            TickAction action = job.Schedule.Tick(DateTime.UtcNow, job.Schedule.Waiting ? job.Host.Pending(job.Probe) : 0, out WaitState? ended, out double seconds);
            if (ended.HasValue)
            {
                job.Host.WaitEnded(ended.Value, seconds);
            }
            if (action == TickAction.Raise)
            {
                RaisePhase();
            }
            else if (action == TickAction.GiveUp)
            {
                GiveUp(job, ui);
            }
        }

        // Revit ran no raised phase, so the write runs here in Idling to put the add-in back. A document cannot be opened in an event
        // handler, so the model stays open until the next job has opened its own
        private void GiveUp(Job job, UIApplication ui)
        {
            job.Host.Stop();
            job.Runner.Record(log => log.Notes.Add("Revit ran no raised phase of the listener in " + AddinRunSchedule.RaiseMaxSeconds + " s, the job stops"));
            job.Runner.Run(job.Host.Phase(ui, job.Probe));
            job.Ok = false;
            leftOpen = job.Host.Model ?? leftOpen;
            Finish(job, "Revit ran no raised phase of the listener");
        }

        public void Execute(UIApplication app)
        {
            Job job = active;
            if (job == null)
            {
                return;
            }
            job.Schedule.Ran();
            string thrown = RunSteps(job, app);
            // A step may start a worker and the limit pass while it does, or the steps stop on a throw. The write still has to run
            if ((thrown != null || job.PastLimit) && !job.Host.Done)
            {
                job.Host.Stop();
                thrown = RunSteps(job, app) ?? thrown;
            }
            if (thrown == null && job.Host.WaitSeconds.HasValue)
            {
                // The next look of the watchdog must see a job still running and not one that finished, for its limit to count
                job.Runner.Record(log => log.FinishedUtc = null);
                job.Schedule.Wait(DateTime.UtcNow, AnswerMinSeconds, job.Host.WaitSeconds.Value);
                return;
            }
            Finish(job, thrown);
        }

        private static string NoThrow(Func<bool> run, Job job)
        {
            try
            {
                job.Ok = run() && job.Ok;
                return null;
            }
            catch (Exception failure)
            {
                job.Ok = false;
                return failure.GetType().Name + ": " + failure.Message;
            }
        }

        private string RunSteps(Job job, UIApplication app)
        {
            return NoThrow(() => job.Runner.Run(Steps(job, app)), job);
        }

        private IEnumerable<JobStep> Steps(Job job, UIApplication app)
        {
            foreach (JobStep step in RevitListenerSteps.Until(job.Host.Phase(app, job.Probe), () => job.PastLimit))
            {
                yield return step;
                if (step.Name == ButtonsRunHost.OpenStep && job.Host.Model != null)
                {
                    yield return new JobStep { Name = "closeLeftOpen", LimitMinutes = OpenCloseMinutes, Always = true, Action = CloseLeftOpen };
                }
            }
            if (job.Host.Done)
            {
                yield return new JobStep { Name = "closeModel", LimitMinutes = OpenCloseMinutes, Always = true, Action = () => CloseModel(job, app) };
            }
        }

        private string CloseLeftOpen()
        {
            if (leftOpen == null || !leftOpen.IsValidObject)
            {
                leftOpen = null;
                return "nothing left open";
            }
            bool closed = leftOpen.Close(false);
            leftOpen = closed ? null : leftOpen;
            return closed ? "the document of the last job closed" : "Revit did not close the document of the last job";
        }

        // RevitAPI.xml: the active document may not be closed by Document.Close, only from the UI. So a small project of the listener is
        // made active first and the model closed behind it, as SetUpSiteRun closes the copy before. That project goes the same way when
        // the next model opens, so Revit holds nothing of the last job
        private string CloseModel(Job job, UIApplication app)
        {
            Document model = job.Host.Model;
            if (model == null || !model.IsValidObject)
            {
                return "no model to close";
            }
            string home = Path.Combine(setting.OutFolder, "listener_home.rvt");
            if (!File.Exists(home))
            {
                Document fresh = app.Application.NewProjectDocument(job.Run.Job.Template);
                fresh.SaveAs(home, new SaveAsOptions { OverwriteExistingFile = false });
                fresh.Close(false);
            }
            leftOpen = app.OpenAndActivateDocument(home).Document;
            bool closed = model.Close(false);
            return closed ? "the model closed, Revit stays open" : "Revit did not close the model";
        }

        private void Finish(Job job, string thrown)
        {
            job.Dispose();
            string outcome = job.PastLimit ? RevitListenerResult.Timeout : job.Ok ? RevitListenerResult.Ok : RevitListenerResult.Failed;
            job.Runner.Record(log => log.Result = outcome);
            string error = null;
            job.Runner.Record(log => error = RevitListenerResult.ErrorOf(log));
            loop.End(job.Run, outcome, thrown ?? error);
            events = idleEvents;
            active = null;
        }

        public string GetName()
        {
            return "SiteAdaptation.TestHost.RevitListener";
        }

        // What belongs to the one job now running. All of it is new per job, the listener keeps nothing of a job but the document left open
        private sealed class Job : IDisposable
        {
            private readonly TestHostWatchdog watchdog;
            private readonly RevitListener listener;
            private int pastLimit;

            public Job(RevitListener listener, RevitListenerRun run)
            {
                this.listener = listener;
                Run = run;
                RevitListenerJob job = run.Job;
                string logPath = listener.LoopLog(run);
                var log = new TestHostLog { Code = job.Code, Kind = job.Kind, Model = job.Model, StartedUtc = DateTime.UtcNow, Guard = "run " + job.RunId + " of the listener" };
                Runner = new TestHostRunner(log, value => JsonOut.Write(logPath, value), () => DateTime.UtcNow);
                Events = new JobEvents(Runner);
                Schedule = new AddinRunSchedule();
                string designRefused = DesignValues.UseConfigFile(job.Config);
                Runner.Record(value => value.Notes.Add(designRefused ?? "the design values of R405 from " + job.Config));
                Probe = AddinProbe.Find(log.Notes);
                Host = new ButtonsRunHost(job, Runner);
                watchdog = new TestHostWatchdog(Runner, log.StartedUtc.AddMinutes(TestHostGuard.TotalMinutesOf(job)), () => DateTime.UtcNow, PastTheLimit, TimeSpan.FromSeconds(10));
            }

            public RevitListenerRun Run { get; }
            public TestHostRunner Runner { get; }
            public JobEvents Events { get; }
            public AddinRunSchedule Schedule { get; }
            public AddinProbe Probe { get; }
            public ButtonsRunHost Host { get; }
            public bool Ok { get; set; } = true;

            public bool PastLimit
            {
                get { return Volatile.Read(ref pastLimit) == 1; }
            }

            // On the thread of the watchdog while Revit may be inside a call that cannot be stopped. The result goes out now, what is left
            // of the job only puts the add-in back when Revit is back. A Revit that never comes back is the freeze guard of the send script
            private void PastTheLimit(string why)
            {
                Volatile.Write(ref pastLimit, 1);
                listener.loop.PastLimit(Run, why);
            }

            public void Dispose()
            {
                watchdog.Dispose();
            }
        }

        private string LoopLog(RevitListenerRun run)
        {
            return Path.Combine(setting.OutFolder, run.Claim.Stem + RevitListenerQueue.LogTail);
        }
    }
}
