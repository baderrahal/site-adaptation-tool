using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SiteAdaptation.Common
{
    // Lane B 5a, docs/design/09-risks.md open items 1 and 2 (R372). One addinRun job in one Revit window makes new projects from the
    // template, switches between them, makes one central and opens it detached, and records what the add-in did with each. What
    // the job decides and records lives here, AddinRunSession of the test host carries it out in Revit
    public static class AddinRunFiles
    {
        public const string SecondTag = "-SECOND";
        public const string CentralTag = "-CENTRAL";
        public const string RunFolder = "addin_run";
        public const string ResultPart = "addinrun";
        public const string PanePng = "pane.png";
        public const string WindowPng = "window.png";
        public const string RibbonPng = "ribbon.png";

        private static string Tail
        {
            get { return TestHostGuard.NewSiteTag + ".rvt"; }
        }

        // R213. <code>-TEST-NEW.rvt is the first, the second and the central take a tag before -TEST-NEW, so each is a new site name
        public static IReadOnlyList<string> All(string model)
        {
            return new[] { model, Second(model), Central(model) };
        }

        public static string Second(string model)
        {
            return Tagged(model, SecondTag);
        }

        public static string Central(string model)
        {
            return Tagged(model, CentralTag);
        }

        private static string Tagged(string model, string tag)
        {
            string name = Path.GetFileName(model);
            return Path.Combine(Path.GetDirectoryName(model), name.Substring(0, name.Length - Tail.Length) + tag + Tail);
        }

        // R103. Null when the job may run: no plan, the first file named by the job code, a template that is there, none of the three
        // files there yet and the run folder made
        public static string CheckJob(TestHostJob job, Func<string, bool> fileExists, Func<string, bool> folderExists)
        {
            if (job.Plan != null)
            {
                return "an addinRun job names no plan";
            }
            if (!string.Equals(Path.GetFileName(job.Model), job.Code + Tail, StringComparison.Ordinal))
            {
                return "an addinRun job makes " + job.Code + Tail + " from its code";
            }
            if (!TestHostGuard.IsPlainFullPath(job.Template) || !job.Template.EndsWith(".rte", StringComparison.OrdinalIgnoreCase))
            {
                return "the template is not a plain full path to a .rte file";
            }
            if (!fileExists(job.Template))
            {
                return "the template does not exist: " + job.Template;
            }
            foreach (string file in All(job.Model))
            {
                if (fileExists(file))
                {
                    return "a file of the run is already there, it is never overwritten: " + file;
                }
            }
            if (!folderExists(Path.GetDirectoryName(job.Model)))
            {
                return "the run folder does not exist";
            }
            return null;
        }

        // R110, QL2. The run folder lies below the work root of the test host, so no file of the run lands in the root itself or
        // anywhere near the sources
        public static string CheckPlace(string model, string documents)
        {
            string root = TestHostGuard.SiteAdaptationFolder(documents);
            string folder = Path.GetDirectoryName(model);
            if (folder == null || !folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return "the run folder of an addinRun job lies under the work root " + root;
            }
            return null;
        }
    }

    // R372. The types and members of the add-in the test host reads by reflection, in one list, so a test on net48 holds each against
    // the add-in it builds with, and a rename there fails the gate before a Revit run
    public static class AddinRunNames
    {
        public const string Assembly = "SiteAdaptation.AddIn";
        public const string PaneHost = "SiteAdaptation.AddIn.PaneHost";
        public const string Current = "Current";
        public const string PaneId = "PaneId";
        public const string Model = "model";
        public const string Sessions = "Sessions";
        public const string Answers = "Answers";
        public const string Routed = "Routed";
        public const string Find = "Find";
        public const string SessionId = "SessionId";
        public const string Loads = "Loads";
        public const string Document = "document";
        public const string ProjectName = "ProjectName";
        public const string Waiting = "Waiting";
        public const string Post = "Post";
        public const string RoutedDocument = "Document";
        public const string RoutedPath = "Path";
        public const string Issue = "Issue";
        public const string WorkFolder = "SiteAdaptation.AddIn.Pane.WorkFolder";
        public const string Root = "Root";
        public const string ReadIfThere = "ReadIfThere";
        public const string Buttons = "SiteAdaptation.AddIn.Buttons";
        public const string State = "State";
        public const string RibbonLayout = "SiteAdaptation.AddIn.RibbonLayout";
        public const string Tab = "Tab";
        public const string Page = "SiteAdaptation.AddIn.Pane.SitePanePage";
        public const string Panels = "Panels";
        public const string PanelName = "Name";
        public const string RibbonButtons = "Buttons";
        public const string ButtonLabel = "Label";
        public const string Theme = "SiteAdaptation.AddIn.Theme.ThemeDictionary";
        public const string ThemeLoad = "Load";
        public const string PaneSize = "SA.Size.Pane";
    }

    // R104. The phases of the job. After a failed phase the job goes on at the first of the phases that always run, which show the
    // pane, take the pictures, close what is open and judge
    public sealed class AddinRunPhases
    {
        private readonly int count;
        private readonly int alwaysFrom;
        private int at = -1;

        public AddinRunPhases(int count, int alwaysFrom)
        {
            this.count = count;
            this.alwaysFrom = alwaysFrom;
        }

        public bool Failed { get; private set; }

        // The phase to run next, -1 when every phase ran
        public int Next(bool lastOk)
        {
            if (at == count - 1)
            {
                return -1;
            }
            if (!lastOk)
            {
                Failed = true;
                at = Math.Max(at + 1, alwaysFrom);
                return at;
            }
            at++;
            return at;
        }
    }

    public enum TickAction
    {
        None,
        Raise,
        GiveUp,
    }

    // R372, RevitAPIUI.xml of Revit 2024: OpenAndActivateDocument "may not be called from inside an event handler", and Idling is one.
    // So each phase runs in the Execute of an ExternalEvent of the test host, and the Idling handler only looks at the clock: it ends
    // a wait and raises the event, and it gives up when Revit has not run a raised event in RaiseMaxSeconds
    public sealed class AddinRunSchedule
    {
        public const double RaiseMaxSeconds = 120;

        private DateTime waitStart;
        private double waitMin;
        private double waitMax;
        private bool waiting;
        private DateTime? raisedAt;

        public bool Waiting
        {
            get { return waiting; }
        }

        public bool Raised
        {
            get { return raisedAt.HasValue; }
        }

        public void Wait(DateTime nowUtc, double minSeconds, double maxSeconds)
        {
            waitStart = nowUtc;
            waitMin = minSeconds;
            waitMax = maxSeconds;
            waiting = true;
            raisedAt = null;
        }

        public void Raise(DateTime nowUtc)
        {
            waiting = false;
            raisedAt = nowUtc;
        }

        public void Ran()
        {
            raisedAt = null;
        }

        // pending is what the wait waits for, answers in the relay or a pane not yet shown, 0 when nothing. ended gives how the
        // wait ended and its seconds when this tick ends it
        public TickAction Tick(DateTime nowUtc, int pending, out WaitState? ended, out double seconds)
        {
            ended = null;
            seconds = 0;
            if (waiting)
            {
                WaitState state = AddinRunWait.Look(waitStart, nowUtc, pending, waitMin, waitMax);
                if (state == WaitState.Wait)
                {
                    return TickAction.None;
                }
                ended = state;
                seconds = Math.Round((nowUtc - waitStart).TotalSeconds, 1);
                Raise(nowUtc);
                return TickAction.Raise;
            }
            if (raisedAt.HasValue && (nowUtc - raisedAt.Value).TotalSeconds >= RaiseMaxSeconds)
            {
                raisedAt = null;
                return TickAction.GiveUp;
            }
            return TickAction.None;
        }
    }

    public enum WaitState
    {
        Wait,
        Ready,
        TimedOut,
    }

    // R372. An answer the relay holds waits for the ExternalEvent of the add-in, which runs only in Revit's idle time, and the pane for
    // Revit to show and lay it out
    public static class AddinRunWait
    {
        public static WaitState Look(DateTime startedUtc, DateTime nowUtc, int waiting, double minSeconds, double maxSeconds)
        {
            double seconds = (nowUtc - startedUtc).TotalSeconds;
            if (waiting == 0 && seconds >= minSeconds)
            {
                return WaitState.Ready;
            }
            return seconds >= maxSeconds ? WaitState.TimedOut : WaitState.Wait;
        }
    }

    // The one result file of the job, testhost_<code>_addinrun.json in the work root
    public sealed class AddinRunRecord
    {
        public string Tool { get; set; } = "SiteAdaptation.TestHost addinRun";
        public string Code { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? FinishedUtc { get; set; }
        // As the add-in names them, read from it by reflection
        public string WorkRoot { get; set; }
        public string SaveFirst { get; set; }
        public string RibbonTab { get; set; }
        // Whether the pane was shown when the job came to show it, so after the first project view of the session
        public bool? PaneShownBefore { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
        public List<RunDocument> Documents { get; set; } = new List<RunDocument>();
        public List<RunEvent> Events { get; set; } = new List<RunEvent>();
        public List<RunStepState> Steps { get; set; } = new List<RunStepState>();
        public List<RunAnswer> Answers { get; set; } = new List<RunAnswer>();
        public List<RunShot> Shots { get; set; } = new List<RunShot>();
        public List<RunCheck> Checks { get; set; } = new List<RunCheck>();
        public int Held { get; set; }
        public int NotHeld { get; set; }
        public int NotMeasured { get; set; }
    }

    public sealed class RunDocument
    {
        public const string Created = "created";
        public const string Opened = "opened";
        public const string Detached = "detached";

        public string Label { get; set; }
        public string Made { get; set; }
        public string Title { get; set; }
        public string PathName { get; set; }
        public string TitleAfterSave { get; set; }
        public string PathAfterSave { get; set; }
        public bool? IsDetached { get; set; }
        public bool? IsWorkshared { get; set; }
        // The session id the add-in holds for the document right after the step that made or opened it
        public string SessionId { get; set; }
        // The id the add-in held when the event reached the test host, null when the add-in had not seen it yet
        public string SessionAtEvent { get; set; }
        // The work folder the pane side of the add-in names for a button on this document, or the refusal it gives
        public string WorkFolder { get; set; }
        public string Stop { get; set; }
        public bool WorkFolderRead { get; set; }
        // For a document with no path, what the flow of a button stops with on it
        public string ButtonStop { get; set; }
        public bool ButtonStopRead { get; set; }
    }

    public sealed class RunEvent
    {
        public const string Created = "DocumentCreated";
        public const string Opened = "DocumentOpened";
        public const string Closed = "DocumentClosed";
        public const string SavedAs = "DocumentSavedAs";
        public const string ViewActivated = "ViewActivated";

        public DateTime Utc { get; set; }
        public string Step { get; set; }
        public string Event { get; set; }
        public string Title { get; set; }
        public string PathName { get; set; }
        public string SessionId { get; set; }
        // ViewActivated only: whether the active document changed, the one case the pane loads again on it
        public bool? DocumentChanged { get; set; }
        public int? PaneLoads { get; set; }
        public string PaneSession { get; set; }
    }

    // The pane around one step: the active document and the document the pane shows after it, and how often it loaded during it
    public sealed class RunStepState
    {
        public string Step { get; set; }
        public string Active { get; set; }
        public string ActiveSession { get; set; }
        public string PaneSession { get; set; }
        public string PaneProject { get; set; }
        public int? LoadsBefore { get; set; }
        public int? LoadsAfter { get; set; }
        // The events of the step the pane loads again on, by the code of PaneHost: created, opened, saved as for a document it knows,
        // closed, and a view of another document made active
        public List<string> Events { get; set; } = new List<string>();
    }

    public sealed class RunAnswer
    {
        public string RunId { get; set; }
        public string PostedFor { get; set; }
        public string PostedSession { get; set; }
        public string PostedPath { get; set; }
        public string ActiveWhenPosted { get; set; }
        public string Wait { get; set; }
        public double? WaitedSeconds { get; set; }
        // True when the relay handed the answer on, to a document or to none
        public bool Routed { get; set; }
        public string RoutedSession { get; set; }
        public string RoutedPath { get; set; }
        public string Issue { get; set; }
    }

    public sealed class RunShot
    {
        public string Name { get; set; }
        public string File { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Colours { get; set; }
        public double BlackShare { get; set; }
        public bool Blank { get; set; }
        public int[] WindowRect { get; set; }
        public int[] PartRect { get; set; }
        public int[] Crop { get; set; }
        public bool? SameHandle { get; set; }
        public string Detail { get; set; }
        // The pane: whether Revit shows it, and the least width of docs/design/12-ui-spec.md Sizes, SA.Size.Pane of Theme.xaml
        public bool? Shown { get; set; }
        public int? MinWidth { get; set; }
        // The ribbon: the tab of the add-in found by its title, visible, the tab active when the picture was taken, the panels the
        // add-in builds and where each panel of the active tab lies on the screen
        public bool? TabFound { get; set; }
        public bool? TabVisible { get; set; }
        public string ActiveTab { get; set; }
        public List<string> ExpectedPanels { get; set; }
        // The button labels of RibbonLayout, and the texts of the buttons the active tab holds as AdWindows gives them
        public List<string> ExpectedButtons { get; set; }
        public List<string> Buttons { get; set; } = new List<string>();
        public List<RunPanel> Panels { get; set; } = new List<RunPanel>();
    }

    public sealed class RunPanel
    {
        public string Title { get; set; }
        // left, top, right, bottom in screen pixels, null when the panel is drawn nowhere
        public int[] Bounds { get; set; }
    }

    public sealed class RunCheck
    {
        public string Item { get; set; }
        public string Name { get; set; }
        // Null when the run did not read what the check needs
        public bool? Held { get; set; }
        public string Detail { get; set; }
    }

    // R372. Each claim of open items 1 and 2 and of the shots, held, not held or not measured, with the counts
    public static class AddinRunJudge
    {
        public const string SessionOnCreated = "a session id on DocumentCreated";
        public const string SessionOnOpened = "a session id on DocumentOpened";
        public const string SessionsDistinct = "every session id its own, a new one after a close";
        public const string PaneShowsActive = "the pane shows the active document";
        public const string LoadOn = "the pane loads again on ";
        public const string AnswerToOwn = "an answer goes to its own document while another is active";
        public const string AnswerAfterClose = "an answer for a closed document is dropped with its issue";
        public const string DetachedNoPath = "a detached document has no PathName";
        public const string DetachedTitleEmpty = "a detached document has an empty Title";
        public const string DetachedStop = "a button on a detached document asks for a save first";
        public const string UnsavedNoPath = "a document never saved has no PathName";
        public const string UnsavedStop = "a document never saved has no work folder and asks for a save";
        public const string SavedFolder = "a saved document gets the work folder of its path";
        public const string PaneShot = "the pane PNG holds a drawn pane";
        public const string RibbonShot = "the ribbon PNG of our own window holds a drawn ribbon";

        private static readonly string[] LoadEvents = { RunEvent.Created, RunEvent.Opened, RunEvent.SavedAs, RunEvent.Closed, RunEvent.ViewActivated };

        public static void Judge(AddinRunRecord record)
        {
            var checks = new List<RunCheck>();
            List<RunDocument> created = record.Documents.Where(document => document.Made == RunDocument.Created).ToList();
            List<RunDocument> opened = record.Documents.Where(document => document.Made != RunDocument.Created).ToList();
            checks.Add(Every("1", SessionOnCreated, created, document => document.SessionId != null, document => document.Label));
            checks.Add(Every("1", SessionOnOpened, opened, document => document.SessionId != null, document => document.Label));
            checks.Add(Distinct(record.Documents));
            List<RunStepState> active = record.Steps.Where(step => step.Active != null).ToList();
            checks.Add(Every("1", PaneShowsActive, active, step => step.PaneSession != null && step.PaneSession == step.ActiveSession, step => step.Step));
            foreach (string kind in LoadEvents)
            {
                List<RunStepState> steps = record.Steps.Where(step => step.Events.Contains(kind)).ToList();
                checks.Add(Every("1", LoadOn + kind, steps.Where(step => step.LoadsBefore.HasValue && step.LoadsAfter.HasValue).ToList(),
                    step => step.LoadsAfter.Value - step.LoadsBefore.Value == step.Events.Count, step => step.Step));
            }
            checks.Add(AnswerCheck(record.Answers.FirstOrDefault(), AnswerToOwn,
                answer => answer.Routed && answer.PostedFor != answer.ActiveWhenPosted && answer.RoutedSession == answer.PostedSession && answer.Issue == null));
            checks.Add(AnswerCheck(record.Answers.Skip(1).FirstOrDefault(), AnswerAfterClose,
                answer => answer.Routed && answer.RoutedSession == null && !string.IsNullOrEmpty(answer.Issue)));

            RunDocument detached = record.Documents.FirstOrDefault(document => document.Made == RunDocument.Detached);
            RunDocument unsaved = record.Documents.FirstOrDefault(document => document.Label == "unsaved");
            RunDocument saved = record.Documents.FirstOrDefault(document => document.Label == "first");
            checks.Add(One("2", DetachedNoPath, detached, document => string.IsNullOrEmpty(document.PathName), document => "PathName '" + document.PathName + "'"));
            checks.Add(One("2", DetachedTitleEmpty, detached, document => string.IsNullOrEmpty(document.Title), document => "Title '" + document.Title + "'"));
            checks.Add(One("2", DetachedStop, detached != null && detached.ButtonStopRead && record.SaveFirst != null ? detached : null,
                document => document.ButtonStop == record.SaveFirst, document => "the button stop '" + document.ButtonStop + "'"));
            checks.Add(One("2", UnsavedNoPath, unsaved, document => string.IsNullOrEmpty(document.PathName), document => "PathName '" + document.PathName + "', Title '" + document.Title + "'"));
            checks.Add(One("2", UnsavedStop, unsaved != null && unsaved.WorkFolderRead && unsaved.ButtonStopRead && record.SaveFirst != null ? unsaved : null,
                document => document.WorkFolder == null && document.ButtonStop == record.SaveFirst,
                document => "work folder '" + document.WorkFolder + "', the button stop '" + document.ButtonStop + "'"));
            checks.Add(One("2", SavedFolder, saved != null && saved.WorkFolderRead && record.WorkRoot != null ? saved : null,
                document => document.WorkFolder != null && document.Stop == null
                    && string.Equals(document.WorkFolder, ExpectedFolder(record.WorkRoot, document.PathName), StringComparison.OrdinalIgnoreCase),
                document => "the add-in names '" + document.WorkFolder + "', the hash of the path gives '" + ExpectedFolder(record.WorkRoot, document.PathName)
                    + "', refusal '" + document.Stop + "'"));

            RunShot pane = record.Shots.FirstOrDefault(shot => shot.Name == "pane");
            RunShot ribbon = record.Shots.FirstOrDefault(shot => shot.Name == "ribbon");
            checks.Add(One("shots", PaneShot, pane != null && pane.Shown.HasValue && pane.MinWidth.HasValue ? pane : null,
                shot => !shot.Blank && shot.Shown == true && shot.Width >= shot.MinWidth.Value,
                shot => "shown " + shot.Shown + ", " + shot.Width + " by " + shot.Height + " px against the least width " + shot.MinWidth + ", " + shot.Colours + " colours"));
            checks.Add(One("shots", RibbonShot, ribbon != null && ribbon.TabFound.HasValue && ribbon.ExpectedPanels != null && ribbon.ExpectedPanels.Count > 0 ? ribbon : null,
                shot => !shot.Blank && shot.SameHandle == true && shot.ActiveTab != null && shot.ActiveTab == record.RibbonTab && MissingPanels(shot).Count == 0
                    && shot.ExpectedButtons != null && shot.ExpectedButtons.Count > 0 && MissingButtons(shot).Count == 0,
                shot => "tab found " + shot.TabFound + ", visible " + shot.TabVisible + ", active tab '" + shot.ActiveTab + "', panels not in the picture: "
                    + (MissingPanels(shot).Count == 0 ? "none" : string.Join(", ", MissingPanels(shot))) + ", buttons not on the tab: "
                    + (MissingButtons(shot).Count == 0 ? "none" : string.Join(", ", MissingButtons(shot))) + ", " + shot.Colours + " colours, same handle " + shot.SameHandle));

            record.Checks = checks;
            record.Held = checks.Count(check => check.Held == true);
            record.NotHeld = checks.Count(check => check.Held == false);
            record.NotMeasured = checks.Count(check => check.Held == null);
        }

        // Each panel the add-in builds must be drawn inside the part of the window the ribbon PNG was cut from
        public static List<string> MissingPanels(RunShot shot)
        {
            return (shot.ExpectedPanels ?? new List<string>()).Where(title =>
            {
                RunPanel panel = shot.Panels.FirstOrDefault(each => each.Title == title);
                return panel == null || shot.Crop == null || !ShotCrop.Inside(shot.PartRect, panel.Bounds);
            }).ToList();
        }

        // Each button label of RibbonLayout must be a button of the active tab, spaces and line breaks taken as one space
        public static List<string> MissingButtons(RunShot shot)
        {
            var found = new HashSet<string>((shot.Buttons ?? new List<string>()).Select(Words));
            return (shot.ExpectedButtons ?? new List<string>()).Where(label => !found.Contains(Words(label))).ToList();
        }

        private static string Words(string text)
        {
            return string.Join(" ", (text ?? "").Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
        // docs/design/06-settings.md: <root>\<file name without extension>_<first 8 hex of the SHA-256 of the full path in lower case>,
        // worked out here a second time, apart from WorkFolder of the add-in, so the run checks the add-in against it
        public static string ExpectedFolder(string root, string pathName)
        {
            if (string.IsNullOrEmpty(pathName) || root == null)
            {
                return null;
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(pathName).ToLowerInvariant()));
                string hex = string.Concat(hash.Take(4).Select(part => part.ToString("x2", CultureInfo.InvariantCulture)));
                return Path.Combine(root, Path.GetFileNameWithoutExtension(pathName) + "_" + hex);
            }
        }

        private static RunCheck Every<T>(string item, string name, List<T> items, Func<T, bool> holds, Func<T, string> label)
        {
            if (items.Count == 0)
            {
                return new RunCheck { Item = item, Name = name, Detail = "nothing of it was read" };
            }
            List<string> off = items.Where(entry => !holds(entry)).Select(label).ToList();
            return new RunCheck
            {
                Item = item,
                Name = name,
                Held = off.Count == 0,
                Detail = (items.Count - off.Count) + " match, " + off.Count + " mismatch" + (off.Count == 0 ? "" : ": " + string.Join(", ", off)),
            };
        }

        private static RunCheck Distinct(List<RunDocument> documents)
        {
            List<RunDocument> read = documents.Where(document => document.SessionId != null).ToList();
            if (read.Count == 0)
            {
                return new RunCheck { Item = "1", Name = SessionsDistinct, Detail = "no session id was read" };
            }
            List<string> shared = read.GroupBy(document => document.SessionId).Where(group => group.Count() > 1)
                .Select(group => string.Join(" and ", group.Select(document => document.Label))).ToList();
            return new RunCheck
            {
                Item = "1",
                Name = SessionsDistinct,
                Held = shared.Count == 0,
                Detail = read.Count + " ids read" + (shared.Count == 0 ? ", all different" : ", shared by " + string.Join(", ", shared)),
            };
        }

        private static RunCheck AnswerCheck(RunAnswer answer, string name, Func<RunAnswer, bool> holds)
        {
            return One("1", name, answer, holds, entry => "posted for " + entry.PostedFor + " while " + entry.ActiveWhenPosted + " was active, wait " + entry.Wait
                + ", routed " + entry.Routed + " to the session " + (entry.RoutedSession ?? "none") + ", issue '" + entry.Issue + "'");
        }

        private static RunCheck One<T>(string item, string name, T value, Func<T, bool> holds, Func<T, string> detail) where T : class
        {
            if (value == null)
            {
                return new RunCheck { Item = item, Name = name, Detail = "nothing of it was read" };
            }
            return new RunCheck { Item = item, Name = name, Held = holds(value), Detail = detail(value) };
        }
    }

    // R384, docs/design/08-install.md line 15. The ribbon is cut from the PrintWindow picture of our own main window. Rectangles are
    // left, top, right, bottom in screen pixels, the crop x, y, width and height inside the window, null when they do not overlap
    public static class ShotCrop
    {
        // True when inner lies whole inside outer and has a width and a height
        public static bool Inside(int[] outer, int[] inner)
        {
            if (outer == null || inner == null || outer.Length != 4 || inner.Length != 4 || inner[2] <= inner[0] || inner[3] <= inner[1])
            {
                return false;
            }
            return inner[0] >= outer[0] && inner[1] >= outer[1] && inner[2] <= outer[2] && inner[3] <= outer[3];
        }

        public static int[] Within(int[] window, int[] part)
        {
            if (window == null || part == null || window.Length != 4 || part.Length != 4)
            {
                return null;
            }
            int left = Math.Max(window[0], part[0]);
            int top = Math.Max(window[1], part[1]);
            int right = Math.Min(window[2], part[2]);
            int bottom = Math.Min(window[3], part[3]);
            if (right <= left || bottom <= top)
            {
                return null;
            }
            return new[] { left - window[0], top - window[1], right - left, bottom - top };
        }
    }

    // R384. A PrintWindow of a window it cannot draw comes back black or one colour, so a picture of one colour or nearly all black
    // counts as blank
    public sealed class ShotPixels
    {
        public const int MaxColours = 4096;
        public const double BlackLimit = 0.98;

        public int Colours { get; private set; }
        public double BlackShare { get; private set; }
        public bool Blank { get; private set; }

        // bgra holds 4 bytes a pixel, blue, green, red and alpha, as WPF copies a Bgra32 picture. The colours are counted up to MaxColours
        public static ShotPixels Of(byte[] bgra)
        {
            var colours = new HashSet<int>();
            int pixels = bgra.Length / 4;
            int black = 0;
            for (int at = 0; at + 3 < bgra.Length; at += 4)
            {
                int colour = bgra[at] | bgra[at + 1] << 8 | bgra[at + 2] << 16;
                if (colour == 0)
                {
                    black++;
                }
                if (colours.Count < MaxColours)
                {
                    colours.Add(colour);
                }
            }
            double share = pixels == 0 ? 1.0 : black / (double)pixels;
            return new ShotPixels { Colours = colours.Count, BlackShare = Rounding.Fixed(share, 3), Blank = colours.Count < 2 || share >= BlackLimit };
        }
    }
}
