using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.TestHost
{
    // Lane B 5a, docs/design/09-risks.md open items 1 and 2 (R372). The Revit side of the addinRun job, thin: it makes and opens the
    // documents, posts the answers and takes the pictures, and AddinRunRecord and AddinRunJudge of Common hold what it saw and decide.
    // OpenAndActivateDocument may not be called from inside an event handler (RevitAPIUI.xml of Revit 2024), Idling being one, so
    // each phase runs in the Execute of an ExternalEvent of the test host. Between phases the Idling handler only keeps the clock of
    // AddinRunSchedule: a worker answer waits for the ExternalEvent of the add-in, the pictures for Revit to show and lay out the pane.
    // QL20: Revit refuses a handler added inside its own event (run 2), so Subscribe adds every handler once from OnStartup, none is
    // ever removed, and the gate starts and ends the job instead
    internal sealed class AddinRunSession
    {
        // How long an idle wait lasts: at least the first, at most the second, in seconds. Tool timings, not project values
        private const double AnswerMinSeconds = 2;
        private const double AnswerMaxSeconds = 60;
        private const double ShowMinSeconds = 3;
        private const double ShowMaxSeconds = 60;
        // The documents, the routing, the documents of item 2, then the phases that always run: show the pane, the pictures and the end
        private const int PhaseCount = 5;
        private const int ShowPhase = 3;
        // The two worksets Revit names when it makes a model workshared
        private const string LevelsWorkset = "Shared Levels and Grids";
        private const string Workset = "Workset1";

        private readonly UIControlledApplication application;
        private readonly TestHostJob job;
        private readonly TestHostRunner runner;
        private readonly Action<UIApplication> exit;
        private readonly AddinRunRecord record;
        private readonly string resultPath;
        private readonly string folder;
        private readonly AddinRunPhases phases = new AddinRunPhases(PhaseCount, ShowPhase);
        private readonly AddinRunSchedule schedule = new AddinRunSchedule();
        private readonly List<KeyValuePair<string, Document>> labels = new List<KeyValuePair<string, Document>>();
        private readonly Dictionary<int, string> closing = new Dictionary<int, string>();
        private readonly RunGate gate = new RunGate();
        // Lane B 5b. Button 1 on the four copies of 3.1 in place of the phases of 5a, null for the job of 5a
        private readonly SetUpSiteRun setUp;
        // Lane B 5c. The buttons of the job on one copy in place of the phases of 5a, null for a job with none
        private readonly ButtonsRunHost buttons;
        private ExternalEvent phaseEvent;
        private AddinProbe probe;
        // Read once in Start, the read loads Theme.xaml by reflection and the wait asks on every Idling tick
        private int? paneMinWidth;
        private IRunState state;
        private UIApplication ui;
        private RunStepState current;
        private Document first;
        private Document second;
        private Document detached;
        private RunAnswer posted;
        private int routedBefore;
        private bool lastOk = true;
        private bool showWait;

        public AddinRunSession(UIControlledApplication application, TestHostJob job, string documents, TestHostRunner runner, Action<UIApplication> exit)
        {
            this.application = application;
            this.job = job;
            this.runner = runner;
            this.exit = exit;
            folder = Path.GetDirectoryName(job.Model);
            resultPath = TestHostGuard.OutputPath(documents, job.Code, AddinRunFiles.ResultPart);
            record = new AddinRunRecord { Code = job.Code, StartedUtc = DateTime.UtcNow };
            setUp = job.SetUp == true ? new SetUpSiteRun(job, runner) : null;
            buttons = job.Buttons != null ? new ButtonsRunHost(job, runner) : null;
        }

        // From OnStartup, outside any Revit event, the one place a handler is added. The handlers wait on the gate until the first tick
        public void Subscribe()
        {
            phaseEvent = ExternalEvent.Create(new PhaseHandler(this));
            application.ControlledApplication.DocumentCreated += OnCreated;
            application.ControlledApplication.DocumentOpened += OnOpened;
            application.ControlledApplication.DocumentClosing += OnClosing;
            application.ControlledApplication.DocumentClosed += OnClosed;
            application.ControlledApplication.DocumentSavedAs += OnSavedAs;
            application.ViewActivated += OnViewActivated;
            application.Idling += OnTick;
        }

        // From the first tick, on the Home screen: the reads of the add-in, then the first phase is raised before any document is
        // made, and the raise is written at once, so a Revit that never runs it still leaves the record
        public void Start(UIApplication start)
        {
            ui = start;
            probe = AddinProbe.Find(record.Notes);
            record.WorkRoot = probe.Root();
            record.SaveFirst = RunTexts.SaveFirst;
            record.RibbonTab = probe.Tab();
            state = probe.State();
            paneMinWidth = probe.PaneMinWidth();
            runner.Record(log => log.Notes.Add("addinRun: the result goes to " + resultPath + ", the pictures to " + folder));
            int open = start.Application.Documents.Size;
            schedule.Raise(DateTime.UtcNow);
            record.HomeEvent.Raise(DateTime.UtcNow, open, Raise());
            Write();
        }

        private bool Raise()
        {
            ExternalEventRequest request = phaseEvent.Raise();
            if (request != ExternalEventRequest.Accepted)
            {
                record.Notes.Add("the phase event of the test host was not taken: " + request);
            }
            return request == ExternalEventRequest.Accepted;
        }

        // In the API context of the ExternalEvent: the phases run one after the other until one ends in a wait
        private void Execute(UIApplication app)
        {
            if (!gate.Live)
            {
                return;
            }
            schedule.Ran();
            ui = app;
            // QL20 point 4. The first run of the event, raised on the Home screen, is written before phase 0 makes anything
            if (record.HomeEvent.Run(DateTime.UtcNow, app.ActiveUIDocument == null, app.Application.Documents.Size))
            {
                Write();
            }
            if (setUp != null)
            {
                // All its steps run in this one event, each copy opened, run and closed in turn, so no wait sits between them
                lastOk = runner.Run(setUp.Steps(app));
                gate.End();
                if (!lastOk)
                {
                    runner.Record(log => log.Result = "failed");
                }
                exit(ui);
                return;
            }
            if (buttons != null)
            {
                // The buttons run in this event until one starts a worker, the wait for its end runs on the Idling clock
                lastOk = runner.Run(buttons.Phase(app, probe)) && lastOk;
                if (buttons.WaitSeconds.HasValue)
                {
                    showWait = false;
                    schedule.Wait(DateTime.UtcNow, AnswerMinSeconds, buttons.WaitSeconds.Value);
                    return;
                }
                gate.End();
                if (!lastOk)
                {
                    runner.Record(log => log.Result = "failed");
                }
                exit(ui);
                return;
            }
            while (gate.Live)
            {
                int phase = phases.Next(lastOk);
                if (phase < 0)
                {
                    Finish();
                    return;
                }
                lastOk = runner.Run(Phase(phase));
                if (lastOk && phase < 2)
                {
                    showWait = false;
                    schedule.Wait(DateTime.UtcNow, AnswerMinSeconds, AnswerMaxSeconds);
                    return;
                }
                if (phase == ShowPhase)
                {
                    showWait = true;
                    schedule.Wait(DateTime.UtcNow, ShowMinSeconds, ShowMaxSeconds);
                    return;
                }
            }
        }

        private IEnumerable<JobStep> Phase(int phase)
        {
            switch (phase)
            {
                case 0:
                    yield return Step("firstNew", 5, () => Make(job.Model, "first.new"));
                    yield return Step("secondNew", 5, () => Make(AddinRunFiles.Second(job.Model), "second.new"));
                    yield return Step("openFirst", 5, () => Open(job.Model, "first", out first));
                    yield return Step("openSecond", 5, () => Open(AddinRunFiles.Second(job.Model), "second", out second));
                    yield return Step("switchToFirst", 5, SwitchToFirst);
                    yield return Step("postForSecond", 1, () => Post("sa5a-1", "second", second));
                    break;
                case 1:
                    yield return Step("routedForSecond", 1, Routed);
                    yield return Step("closeSecond", 5, () =>
                    {
                        second.Close(false);
                        return "the second document closed while the first stays active";
                    });
                    yield return Step("postForClosed", 1, () => Post("sa5a-2", "second", null));
                    break;
                case 2:
                    yield return Step("routedForClosed", 1, Routed);
                    yield return Step("centralNew", 5, MakeCentral);
                    yield return Step("openDetached", 5, OpenDetached);
                    yield return Step("unsaved", 5, Unsaved);
                    break;
                case ShowPhase:
                    yield return Step("showPane", 2, ShowPane, true);
                    break;
                default:
                    yield return Step("panePng", 2, () => Shot(RevitShots.Pane(probe.PageType(), Path.Combine(folder, AddinRunFiles.PanePng), PaneShown(), paneMinWidth)), true);
                    yield return Step("ribbonPng", 2, RibbonShots, true);
                    yield return Step("closeDetached", 5, CloseDetached, true);
                    yield return Step("judge", 1, () =>
                    {
                        AddinRunJudge.Judge(record);
                        return "held " + record.Held + ", not held " + record.NotHeld + ", not measured " + record.NotMeasured;
                    }, true);
                    break;
            }
        }

        // The window by PrintWindow and the ribbon cut from it, with the tab of the add-in as AdWindows holds it and the panels and
        // buttons RibbonLayout of the add-in builds, read now, so the check follows the ribbon of the build that runs
        private string RibbonShots()
        {
            List<RunShot> shots = RevitShots.Window(ui.MainWindowHandle, RevitShots.RibbonBounds(record.Notes),
                Path.Combine(folder, AddinRunFiles.WindowPng), Path.Combine(folder, AddinRunFiles.RibbonPng));
            RunShot ribbon = shots.FirstOrDefault(shot => shot.Name == "ribbon");
            if (ribbon == null)
            {
                ribbon = new RunShot { Name = "ribbon", Blank = true, Detail = "the ribbon was not read or lies outside the window, no picture cut" };
                shots.Add(ribbon);
            }
            if (record.RibbonTab != null)
            {
                RevitShots.ReadTab(ribbon, record.RibbonTab, record.Notes);
            }
            ribbon.ExpectedPanels = probe.RibbonPanels();
            ribbon.ExpectedButtons = probe.RibbonButtons();
            return string.Join(", ", shots.Select(Shot)) + ", active tab '" + ribbon.ActiveTab + "'";
        }

        private bool? PaneShown()
        {
            try
            {
                return probe.PaneId() is DockablePaneId id ? ui.GetDockablePane(id).IsShown() : (bool?)null;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException failure)
            {
                record.Notes.Add("the pane state was not read: " + failure.Message);
                return null;
            }
        }

        // One step with the pane around it: how often it loaded, the active document and the one the pane shows after it
        private JobStep Step(string name, double minutes, Func<string> action, bool always = false)
        {
            return new JobStep
            {
                Name = name,
                LimitMinutes = minutes,
                Always = always,
                Action = () =>
                {
                    current = new RunStepState { Step = name, LoadsBefore = probe.Loads() };
                    try
                    {
                        return action();
                    }
                    finally
                    {
                        Document active = ui.ActiveUIDocument == null ? null : ui.ActiveUIDocument.Document;
                        current.LoadsAfter = probe.Loads();
                        current.Active = LabelOf(active);
                        current.ActiveSession = probe.SessionOf(active);
                        current.PaneSession = probe.PaneSession();
                        current.PaneProject = probe.PaneProject();
                        record.Steps.Add(current);
                        current = null;
                        Write();
                    }
                },
            };
        }

        private string Make(string path, string label)
        {
            Document document = ui.Application.NewProjectDocument(job.Template);
            RunDocument entry = Describe(label, RunDocument.Created, document);
            document.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = false });
            entry.TitleAfterSave = document.Title;
            entry.PathAfterSave = document.PathName;
            document.Close(false);
            return "made from the template, saved as " + Path.GetFileName(path) + " and closed";
        }

        private string Open(string path, string label, out Document document)
        {
            document = ui.OpenAndActivateDocument(path).Document;
            labels.Add(new KeyValuePair<string, Document>(label, document));
            Describe(label, RunDocument.Opened, document);
            return "opened and made active";
        }

        private string SwitchToFirst()
        {
            Document now = ui.OpenAndActivateDocument(job.Model).Document;
            return "OpenAndActivateDocument gave " + (Same(now, first) ? "the first document again" : "another document, " + now.Title);
        }

        private string Post(string runId, string label, Document document)
        {
            // The second answer is for the same document after it closed, so it carries the path and session of the first
            RunAnswer earlier = record.Answers.LastOrDefault(item => item.PostedFor == label);
            var answer = new RunAnswer
            {
                RunId = runId,
                PostedFor = label,
                PostedSession = document != null ? probe.SessionOf(document) : earlier == null ? null : earlier.PostedSession,
                PostedPath = document != null ? document.PathName : earlier == null ? null : earlier.PostedPath,
                ActiveWhenPosted = LabelOf(ui.ActiveUIDocument == null ? null : ui.ActiveUIDocument.Document),
            };
            record.Answers.Add(answer);
            routedBefore = probe.RoutedCount() ?? 0;
            posted = answer;
            if (!probe.Post(runId, answer.PostedPath, answer.PostedSession))
            {
                throw new InvalidOperationException("the relay of the add-in took no answer, see the notes of the result");
            }
            return "posted for " + label + " while " + answer.ActiveWhenPosted + " is active";
        }

        private string Routed()
        {
            int? count = probe.RoutedCount();
            if (posted == null || count == null || count.Value <= routedBefore)
            {
                return "the relay handed no answer on in the wait (" + (posted == null ? "none posted" : posted.Wait) + ")";
            }
            probe.ReadRouted(routedBefore, posted);
            return posted.RoutedSession != null ? "routed to the session " + posted.RoutedSession : "dropped: " + posted.Issue;
        }

        private string MakeCentral()
        {
            string central = AddinRunFiles.Central(job.Model);
            Document document = ui.Application.NewProjectDocument(job.Template);
            RunDocument entry = Describe("central.new", RunDocument.Created, document);
            document.EnableWorksharing(LevelsWorkset, Workset);
            var options = new SaveAsOptions { OverwriteExistingFile = false };
            options.SetWorksharingOptions(new WorksharingSaveAsOptions { SaveAsCentral = true });
            document.SaveAs(central, options);
            entry.TitleAfterSave = document.Title;
            entry.PathAfterSave = document.PathName;
            entry.IsWorkshared = document.IsWorkshared;
            document.Close(false);
            return "made, workshared, saved as the central " + Path.GetFileName(central) + " and closed";
        }

        private string OpenDetached()
        {
            ModelPath central = ModelPathUtils.ConvertUserVisiblePathToModelPath(AddinRunFiles.Central(job.Model));
            var options = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets };
            detached = ui.OpenAndActivateDocument(central, options, false).Document;
            labels.Add(new KeyValuePair<string, Document>("detached", detached));
            Describe("detached", RunDocument.Detached, detached);
            return "opened detached, Title '" + detached.Title + "', PathName '" + detached.PathName + "'";
        }

        // A detached document asks to be saved when Revit exits, so it is closed unsaved here from the first document, and Revit exits
        // with no question
        private string CloseDetached()
        {
            if (detached == null || !detached.IsValidObject)
            {
                return "no detached document open";
            }
            if (Same(ui.ActiveUIDocument == null ? null : ui.ActiveUIDocument.Document, detached))
            {
                ui.OpenAndActivateDocument(job.Model);
            }
            detached.Close(false);
            return "the detached document closed unsaved";
        }

        private string Unsaved()
        {
            Document document = ui.Application.NewProjectDocument(job.Template);
            RunDocument entry = Describe("unsaved", RunDocument.Created, document);
            document.Close(false);
            return "made, never saved, Title '" + entry.Title + "', PathName '" + entry.PathName + "', then closed";
        }

        // The add-in shows its pane at the first project view of the session when Revit has it hidden (PaneFirstShow). Revit also shows
        // a pane it restores from the last session (QL18), so a pane shown here does not tell the add-in showed it, and the session
        // cannot read the state before PaneFirstShow ran. The judge takes a shown pane as not measured and a hidden one as not held. The
        // job shows it too when it is not, and makes the tab of the add-in the active one for the pictures
        private string ShowPane()
        {
            string shown = "the pane id was not read";
            if (probe.PaneId() is DockablePaneId id)
            {
                DockablePane pane = ui.GetDockablePane(id);
                record.PaneShownBefore = pane.IsShown();
                if (record.PaneShownBefore != true)
                {
                    pane.Show();
                }
                shown = "the pane shown before " + record.PaneShownBefore + ", after " + pane.IsShown();
            }
            bool tab = record.RibbonTab != null && RevitShots.ActivateTab(record.RibbonTab, record.Notes);
            return shown + ", the tab " + record.RibbonTab + " made active " + tab + ", active now '" + RevitShots.ActiveTab() + "'";
        }

        private string Shot(RunShot shot)
        {
            record.Shots.Add(shot);
            return shot.Name + " " + shot.Width + " by " + shot.Height + " px, blank " + shot.Blank;
        }

        private RunDocument Describe(string label, string made, Document document)
        {
            RunEvent seen = record.Events.LastOrDefault(item => (item.Event == RunEvent.Created || item.Event == RunEvent.Opened)
                && item.Title == document.Title && item.PathName == document.PathName);
            var entry = new RunDocument
            {
                Label = label,
                Made = made,
                Title = document.Title,
                PathName = document.PathName,
                IsDetached = document.IsDetached,
                IsWorkshared = document.IsWorkshared,
                SessionId = probe.SessionOf(document),
                SessionAtEvent = seen == null ? null : seen.SessionId,
            };
            WorkFolderOf(entry);
            record.Documents.Add(entry);
            return entry;
        }

        // The work folder as the pane side of the add-in names it for a button, or the refusal it throws, and for a document with no
        // saved path by SavedPath, as the bare name of run 3's detached document, the stop of the R382 flow, which Buttons.Run hands
        // document.PathName. In 5a every body says it is not built, which
        // the flow says first, so a body with nothing in it lets the flow reach the path check, where it stops before it reads the
        // pane, the model or a file
        private void WorkFolderOf(RunDocument entry)
        {
            if (state != null)
            {
                try
                {
                    entry.WorkFolder = state.WorkFolder(entry.PathName);
                }
                catch (InvalidOperationException refused)
                {
                    entry.Stop = refused.Message;
                }
                entry.WorkFolderRead = true;
            }
            if (!SavedPath.Is(entry.PathName))
            {
                entry.ButtonStop = new ButtonFlow(state, null, null, null, null).Run(1, "1", new ButtonBody(), entry.PathName).Message;
                entry.ButtonStopRead = true;
            }
        }

        // Idling starts the job on its first tick, then keeps the clock only: it ends a wait and raises the next phase, and stops the
        // job when Revit runs no raised phase
        private void OnTick(object sender, IdlingEventArgs e)
        {
            if (gate.Finished)
            {
                return;
            }
            e.SetRaiseWithoutDelay();
            if (gate.Begin())
            {
                Start((UIApplication)sender);
                return;
            }
            TickAction action = schedule.Tick(DateTime.UtcNow, Pending(), out WaitState? ended, out double seconds);
            if (ended.HasValue && buttons != null)
            {
                buttons.WaitEnded(ended.Value, seconds);
            }
            else if (ended.HasValue && showWait)
            {
                record.Notes.Add("the wait for the pane and the tab ended " + ended.Value + " after " + seconds + " s");
            }
            else if (ended.HasValue && posted != null && posted.Wait == null)
            {
                posted.Wait = ended.Value.ToString();
                posted.WaitedSeconds = seconds;
            }
            if (action == TickAction.Raise)
            {
                Raise();
            }
            else if (action == TickAction.GiveUp)
            {
                record.Notes.Add("Revit ran no raised phase of the test host in " + AddinRunSchedule.RaiseMaxSeconds + " s, the job stops");
                runner.Record(log => log.Notes.Add("addinRun: the phase event did not run, the job stops"));
                record.HomeEvent.NotRun();
                lastOk = false;
                ui = (UIApplication)sender;
                Finish();
            }
        }

        // What the wait waits for: answers the relay of the add-in still holds, or the pane not yet shown at its least width and the
        // tab of the add-in not yet active
        private int Pending()
        {
            if (!schedule.Waiting)
            {
                return 0;
            }
            if (buttons != null)
            {
                return buttons.Pending(probe);
            }
            if (!showWait)
            {
                return probe.Waiting() ?? 0;
            }
            bool pane = PaneShown() == true && paneMinWidth.HasValue && RevitShots.PaneWidth(probe.PageType()) >= paneMinWidth.Value;
            bool tab = record.RibbonTab != null && RevitShots.ActiveTab() == record.RibbonTab;
            return (pane ? 0 : 1) + (tab ? 0 : 1);
        }

        // Called inside Idling or the ExternalEvent, so it removes no handler (QL20). The gate ends the job, and the judge runs here
        // too, so a job that gave up on its phase event still writes its checks
        private void Finish()
        {
            if (gate.Finished)
            {
                return;
            }
            gate.End();
            record.FinishedUtc = DateTime.UtcNow;
            AddinRunJudge.Judge(record);
            Write();
            if (phases.Failed || !lastOk)
            {
                runner.Record(log => log.Result = "failed");
            }
            exit(ui);
        }

        private sealed class PhaseHandler : IExternalEventHandler
        {
            private readonly AddinRunSession session;

            public PhaseHandler(AddinRunSession session)
            {
                this.session = session;
            }

            public void Execute(UIApplication app)
            {
                session.Execute(app);
            }

            public string GetName()
            {
                return "SiteAdaptation.TestHost.AddinRunPhase";
            }
        }

        // The events PaneHost loads the pane on, each with the pane right after it as the test host sees it
        private void OnCreated(object sender, DocumentCreatedEventArgs e)
        {
            if (gate.Live && e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
                Seen(RunEvent.Created, e.Document, null, true);
            }
        }

        private void OnOpened(object sender, DocumentOpenedEventArgs e)
        {
            if (gate.Live && e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
                Seen(RunEvent.Opened, e.Document, null, true);
            }
        }

        // PaneHost shows the pane again on a save as of a document it holds a session for, and the session stays
        private void OnSavedAs(object sender, DocumentSavedAsEventArgs e)
        {
            if (gate.Live && e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
                Seen(RunEvent.SavedAs, e.Document, null, probe.SessionOf(e.Document) != null);
            }
        }

        private void OnClosing(object sender, DocumentClosingEventArgs e)
        {
            if (!gate.Live)
            {
                return;
            }
            closing[e.DocumentId] = LabelOf(e.Document);
        }

        private void OnClosed(object sender, DocumentClosedEventArgs e)
        {
            if (!gate.Live)
            {
                return;
            }
            closing.TryGetValue(e.DocumentId, out string label);
            closing.Remove(e.DocumentId);
            Seen(RunEvent.Closed, null, null, true, label);
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            if (!gate.Live)
            {
                return;
            }
            Document now = e.CurrentActiveView == null ? null : e.CurrentActiveView.Document;
            Document before = e.PreviousActiveView == null ? null : e.PreviousActiveView.Document;
            bool changed = now == null || before == null || !now.Equals(before);
            Seen(RunEvent.ViewActivated, now, changed, changed);
        }

        // probe is null before Start, so nothing is recorded outside the live job
        private void Seen(string kind, Document document, bool? changed, bool loads, string title = null)
        {
            if (!gate.Live)
            {
                return;
            }
            try
            {
                record.Events.Add(new RunEvent
                {
                    Utc = DateTime.UtcNow,
                    Step = current == null ? null : current.Step,
                    Event = kind,
                    Title = document == null ? title : document.Title,
                    PathName = document == null ? null : document.PathName,
                    SessionId = probe.SessionOf(document),
                    DocumentChanged = changed,
                    PaneLoads = probe.Loads(),
                    PaneSession = probe.PaneSession(),
                });
                if (loads && current != null)
                {
                    current.Events.Add(kind);
                }
            }
            catch (Exception failure)
            {
                record.Notes.Add(kind + " not recorded: " + failure.Message);
            }
        }

        private string LabelOf(Document document)
        {
            if (document == null)
            {
                return null;
            }
            foreach (KeyValuePair<string, Document> pair in labels)
            {
                if (Same(pair.Value, document))
                {
                    return pair.Key;
                }
            }
            return document.IsValidObject ? document.Title : null;
        }

        // A closed document no longer compares, so a failed compare is no match
        private static bool Same(Document one, Document other)
        {
            try
            {
                return one != null && other != null && one.IsValidObject && other.IsValidObject && one.Equals(other);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Write()
        {
            try
            {
                JsonOut.Write(resultPath, record);
            }
            catch (Exception failure)
            {
                runner.Record(log => log.Notes.Add("addinRun: the result was not written: " + failure.Message));
            }
        }
    }
}
