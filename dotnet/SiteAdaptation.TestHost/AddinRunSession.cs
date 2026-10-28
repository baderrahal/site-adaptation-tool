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
    // A worker answer waits for the ExternalEvent of the add-in, which runs only once Revit is idle, so the job runs in phases with an
    // idle wait between them
    internal sealed class AddinRunSession
    {
        // How long an idle wait lasts: at least the first, at most the second, in seconds. Tool timings, not project values
        private const double AnswerMinSeconds = 2;
        private const double AnswerMaxSeconds = 60;
        private const double ShowMinSeconds = 3;
        private const double ShowMaxSeconds = 30;
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
        private readonly AddinRunPhases phases = new AddinRunPhases(4);
        private readonly List<KeyValuePair<string, Document>> labels = new List<KeyValuePair<string, Document>>();
        private readonly Dictionary<int, string> closing = new Dictionary<int, string>();
        private AddinProbe probe;
        private IRunState state;
        private UIApplication ui;
        private RunStepState current;
        private Document first;
        private Document second;
        private Document detached;
        private RunAnswer posted;
        private int routedBefore;
        private DateTime waitStart;
        private double waitMin;
        private double waitMax;
        private bool lastOk;

        public AddinRunSession(UIControlledApplication application, TestHostJob job, string documents, TestHostRunner runner, Action<UIApplication> exit)
        {
            this.application = application;
            this.job = job;
            this.runner = runner;
            this.exit = exit;
            folder = Path.GetDirectoryName(job.Model);
            resultPath = TestHostGuard.OutputPath(documents, job.Code, AddinRunFiles.ResultPart);
            record = new AddinRunRecord { Code = job.Code, StartedUtc = DateTime.UtcNow };
        }

        public void Start(UIApplication start)
        {
            ui = start;
            probe = AddinProbe.Find(record.Notes);
            record.WorkRoot = probe.Root();
            record.SaveFirst = RunTexts.SaveFirst;
            record.RibbonTab = probe.Tab();
            state = probe.State();
            application.ControlledApplication.DocumentCreated += OnCreated;
            application.ControlledApplication.DocumentOpened += OnOpened;
            application.ControlledApplication.DocumentClosing += OnClosing;
            application.ControlledApplication.DocumentClosed += OnClosed;
            application.ViewActivated += OnViewActivated;
            runner.Record(log => log.Notes.Add("addinRun: the result goes to " + resultPath + ", the pictures to " + folder));
            Write();
            RunNext(true);
        }

        private void RunNext(bool ok)
        {
            int phase = phases.Next(ok);
            if (phase < 0)
            {
                Finish();
                return;
            }
            lastOk = runner.Run(Phase(phase));
            // After a failed phase the wait has nothing to wait for
            if (!lastOk || phase == 3)
            {
                RunNext(lastOk);
                return;
            }
            waitStart = DateTime.UtcNow;
            waitMin = phase == 2 ? ShowMinSeconds : AnswerMinSeconds;
            waitMax = phase == 2 ? ShowMaxSeconds : AnswerMaxSeconds;
            application.Idling += OnWait;
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
                    yield return Step("showPane", 2, ShowPane);
                    break;
                default:
                    yield return Step("panePng", 2, () => Shot(RevitShots.Pane(probe.PageType(), Path.Combine(folder, AddinRunFiles.PanePng))));
                    yield return Step("ribbonPng", 2, () =>
                    {
                        List<RunShot> shots = RevitShots.Window(ui.MainWindowHandle, RevitShots.RibbonBounds(record.Notes),
                            Path.Combine(folder, AddinRunFiles.WindowPng), Path.Combine(folder, AddinRunFiles.RibbonPng));
                        return string.Join(", ", shots.Select(Shot));
                    });
                    yield return Step("closeDetached", 5, CloseDetached, true);
                    yield return Step("judge", 1, () =>
                    {
                        AddinRunJudge.Judge(record);
                        return "held " + record.Held + ", not held " + record.NotHeld + ", not measured " + record.NotMeasured;
                    }, true);
                    break;
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
                    current = new RunStepState { Step = name, GenerationBefore = probe.Generation() };
                    try
                    {
                        return action();
                    }
                    finally
                    {
                        Document active = ui.ActiveUIDocument == null ? null : ui.ActiveUIDocument.Document;
                        current.GenerationAfter = probe.Generation();
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

        private string ShowPane()
        {
            string shown = "the pane id was not read";
            if (probe.PaneId() is DockablePaneId id)
            {
                DockablePane pane = ui.GetDockablePane(id);
                pane.Show();
                shown = "the pane shown " + pane.IsShown();
            }
            bool tab = record.RibbonTab != null && RevitShots.ActivateTab(record.RibbonTab, record.Notes);
            return shown + ", the tab " + record.RibbonTab + " made active " + tab;
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
        // path the stop of the R382 flow, which Buttons.Run hands document.PathName. In 5a every body says it is not built, which
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
            if (string.IsNullOrEmpty(entry.PathName))
            {
                entry.ButtonStop = new ButtonFlow(state, null, null, null, null).Run(1, "1", new ButtonBody(), entry.PathName).Message;
                entry.ButtonStopRead = true;
            }
        }

        private void OnWait(object sender, IdlingEventArgs e)
        {
            e.SetRaiseWithoutDelay();
            WaitState state = AddinRunWait.Look(waitStart, DateTime.UtcNow, probe.Waiting() ?? 0, waitMin, waitMax);
            if (state == WaitState.Wait)
            {
                return;
            }
            application.Idling -= OnWait;
            if (posted != null && posted.Wait == null)
            {
                posted.Wait = state.ToString();
                posted.WaitedSeconds = Math.Round((DateTime.UtcNow - waitStart).TotalSeconds, 1);
            }
            ui = (UIApplication)sender;
            RunNext(lastOk);
        }

        private void Finish()
        {
            application.ControlledApplication.DocumentCreated -= OnCreated;
            application.ControlledApplication.DocumentOpened -= OnOpened;
            application.ControlledApplication.DocumentClosing -= OnClosing;
            application.ControlledApplication.DocumentClosed -= OnClosed;
            application.ViewActivated -= OnViewActivated;
            record.FinishedUtc = DateTime.UtcNow;
            Write();
            if (phases.Failed)
            {
                runner.Record(log => log.Result = "failed");
            }
            exit(ui);
        }

        // The events PaneHost loads the pane on, each with the pane right after it as the test host sees it
        private void OnCreated(object sender, DocumentCreatedEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
                Seen(RunEvent.Created, e.Document, null, true);
            }
        }

        private void OnOpened(object sender, DocumentOpenedEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
                Seen(RunEvent.Opened, e.Document, null, true);
            }
        }

        private void OnClosing(object sender, DocumentClosingEventArgs e)
        {
            closing[e.DocumentId] = LabelOf(e.Document);
        }

        private void OnClosed(object sender, DocumentClosedEventArgs e)
        {
            closing.TryGetValue(e.DocumentId, out string label);
            closing.Remove(e.DocumentId);
            Seen(RunEvent.Closed, null, null, true, label);
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            Document now = e.CurrentActiveView == null ? null : e.CurrentActiveView.Document;
            Document before = e.PreviousActiveView == null ? null : e.PreviousActiveView.Document;
            bool changed = now == null || before == null || !now.Equals(before);
            Seen(RunEvent.ViewActivated, now, changed, changed);
        }

        private void Seen(string kind, Document document, bool? changed, bool loads, string title = null)
        {
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
                    PaneGeneration = probe.Generation(),
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
