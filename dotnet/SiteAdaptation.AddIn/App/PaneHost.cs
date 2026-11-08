using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System.Windows.Threading;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Theme;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn
{
    // R372, docs/design/07-mock.md 7.2. The Revit side of the pane, thin: it hands each document event to PaneSessions and the
    // view model, which decide, and raises the external events that act in the API context of Revit
    public sealed class PaneHost : IDockablePaneProvider
    {
        // Fixed, so Revit keeps the place the user gave the pane from one start to the next
        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("6b1c5a2e-3f47-4d8a-9c0e-5a0f2b7d4e91"));

        private readonly PaneSessions sessions = new PaneSessions();
        private readonly PaneFirstShow firstShow = new PaneFirstShow();
        private readonly DiskProbe probe;
        private readonly PaneViewModel model;
        private ExternalEvent runEvent;
        private RunButtonHandler runner;
        private ExternalEvent answerEvent;
        private ExternalEvent tellEvent;
        private ExternalEvent newProjectEvent;
        private TellHandler teller;
        private IntPtr owner;

        public static PaneHost Current { get; private set; }

        public PaneSessions Sessions
        {
            get { return sessions; }
        }

        public AnswerRelay Answers { get; private set; }

        // The routes of the answers handed on so far, so the run of PR 6 in Revit can read where each one went
        public List<RoutedAnswer> Routed { get; } = new List<RoutedAnswer>();

        // Register runs on the UI thread of Revit, the thread of the page, so its Dispatcher takes the hashes until the page exists
        private PaneHost()
        {
            Dispatcher ui = Dispatcher.CurrentDispatcher;
            probe = new DiskProbe(action => ui.BeginInvoke(action));
            model = new PaneViewModel(probe, new DialogPicker());
        }

        public static void Register(UIControlledApplication application)
        {
            var host = new PaneHost();
            var runner = new RunButtonHandler();
            host.runner = runner;
            // R496. The row Prototypes warns for files older than this Revit, the look runs off the UI thread. A version text that is no
            // number gives no warning
            host.model.FormatLook = host.probe.LookFormat;
            // R812. The copies of this Revit beside the folder, looked for on a thread of their own as the format is
            host.model.CopiesLook = (folder, year, done) => System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                Common.PlaceVillas.PrototypeCopies copies = Common.PlaceVillas.PrototypeCopies.Find(folder, year);
                host.probe.Post(() => done(copies));
            }, System.Threading.CancellationToken.None, System.Threading.Tasks.TaskCreationOptions.LongRunning, System.Threading.Tasks.TaskScheduler.Default);
            host.model.RunningRevit = int.TryParse(application.ControlledApplication.VersionNumber, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int running) ? running : (int?)null;
            host.runEvent = ExternalEvent.Create(runner);
            host.answerEvent = ExternalEvent.Create(new AnswerHandler(host));
            host.Answers = new AnswerRelay(() => host.answerEvent.Raise());
            // R375. Button 2 starts CadReader.exe beside the add-in, its answer comes back through the relay
            ReadDataBody.Worker = new LaunchedWorker(LaunchedWorker.BesideAddIn(), host.Answers.Post);
            // R406. Button 3 reads the latest read of button 2 in the work folder of the run for the NBH of the scope
            ReviewData.ReviewDataBody.Load = context => Common.Review.ReviewLoad.Load(context.WorkFolder, ReviewData.ReviewDataBody.Nbh?.Invoke(), null);
            // R376. Button 4 names the manual rule of a row by check type and field from the rules file of the pane, none where the profile
            // lists none or has no block
            CheckTable.CheckTableBody.ManualRule = rulesFile =>
            {
                Common.ManualRules.ManualRuleBlock block = null;
                try
                {
                    block = string.IsNullOrEmpty(rulesFile) ? null : Common.ManualRules.ManualRuleBlock.FromFile(rulesFile);
                }
                catch (Exception error) when (error is System.IO.IOException || error is System.IO.InvalidDataException || error is Newtonsoft.Json.JsonException)
                {
                    block = null;
                }
                return block == null ? (Func<string, string, string>)null : block.IdOfCheck;
            };
            // R377. Button 7 loads the plan of the latest real run of 4 Check for the NBH of the scope of the active document. An NBH scope takes every plot of
            // the plan is in scope, a list of plots places only the plots it names
            PlaceVillas.PlaceVillasBody.Load = context =>
            {
                PaneSettings settings = host.model.Settings ?? new PaneSettings();
                return Common.PlaceVillas.VillaLoad.Load(context, settings.Scope, settings.ConfigFile, settings.VillaMode, settings.LevelProfile);
            };
            host.model.RunRequested += number =>
            {
                runner.Number = number;
                host.runEvent.Raise();
            };
            // R446. Open folder of the box, on the UI thread of Revit, no API context needed
            host.model.OpenFolderRequested += folder => System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
            host.teller = new TellHandler();
            host.tellEvent = ExternalEvent.Create(host.teller);
            host.model.Told += text =>
            {
                host.teller.Text = text;
                host.tellEvent.Raise();
            };
            // R445. New project opens in the API context of Revit through an external event, as a run of a button does, so Save setup
            // writes the settings into the model at once in a transaction of its own. How it works needs no API context
            host.owner = application.MainWindowHandle;
            host.newProjectEvent = ExternalEvent.Create(new NewProjectHandler(host));
            host.model.NewProjectRequested += () => host.newProjectEvent.Raise();
            host.model.SurfacePicker = (inFolder, effective) => NewProject.RoadSurfacesDialog.Show(host.owner, inFolder, effective);
            host.model.HowItWorksRequested += () =>
            {
                if (HowItWorks.HowItWorksDialog.Show(host.owner, host.model.Document != null))
                {
                    host.newProjectEvent.Raise();
                }
            };
            host.SavedStartsChanged();
            Buttons.State = new PaneRunState(host.model, WorkRoot, WorkFolder.ReadIfThere);
            host.model.WorkFolderOf = path => WorkFolder.For(WorkRoot(), path);
            application.RegisterDockablePane(PaneId, PaneText.Title, host);
            application.ControlledApplication.DocumentOpened += host.OnOpened;
            application.ControlledApplication.DocumentCreated += host.OnCreated;
            application.ControlledApplication.DocumentClosing += host.OnClosing;
            application.ControlledApplication.DocumentClosed += host.OnClosed;
            application.ControlledApplication.DocumentSavedAs += host.OnSavedAs;
            application.ControlledApplication.DocumentChanged += host.OnChanged;
            application.ViewActivated += host.OnViewActivated;
            Current = host;
        }

        // R680. The work folder of the document the pane shows, null with none or with no saved path
        private string WorkFolderNow()
        {
            DocumentSession document = model.Document;
            return document == null || !document.HasPath ? null : WorkFolder.For(WorkRoot(), document.Path);
        }

        // R681. Choose project folder: the folder goes into the config copy of this new project, then StagesViewModel.FolderChosen runs the read on it
        // as a press of its row does, or step 1 first when the folder changed the config, R769. A refusal is said and nothing runs
        private void ChooseProjectFolder(StagesViewModel stages)
        {
            PaneSettings settings = model.Settings;
            if (settings == null)
            {
                return;
            }
            string chosen = new DialogPicker().Folder();
            if (string.IsNullOrEmpty(chosen))
            {
                return;
            }
            string before = ProjectFolderPick.Chosen(settings.ConfigFile);
            string refused = ProjectFolderPick.Write(settings.ConfigFile, WorkRoot(), chosen, WorkFolderNow());
            if (refused != null)
            {
                teller.Text = refused;
                tellEvent.Raise();
                return;
            }
            stages.FolderChosen(before, chosen);
        }

        // R681. Fix items: the Confirm window of R2-C1 over the latest read, as the end of a read opens it
        private void FixItems(string readFolder)
        {
            string work = WorkFolderNow();
            if (readFolder == null || work == null)
            {
                return;
            }
            string profile = System.IO.Path.Combine(readFolder, ConfirmProfile.ConfirmProfileStep.ProfileFile);
            // A locked or damaged proposal is said, as the end of a read of version 1 says it, and never reaches the UI thread of Revit
            string said;
            try
            {
                var session = new ConfirmProfile.ConfirmProfileSession(ConfirmProfile.ConfirmProfileModel.Read(Common.WorkFiles.ReadText(profile)), work, "fix-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
                ConfirmProfile.ConfirmProfileStep.ShowConfirm(owner, session);
                said = session.Result;
                // R771. The folder this Confirm holds for, so a quiet read of the Build keeps it only for the same folder
                if (said == ConfirmProfile.ConfirmProfileText.Saved)
                {
                    Fast7.ConfirmedFor.Write(work, Fast7.ConfirmedFor.ScanFolderOf(readFolder));
                }
            }
            catch (Exception failure)
            {
                said = TextTemplate.Fill(StageText.FixNotOpened, failure.Message);
            }
            if (said != null)
            {
                teller.Text = said;
                tellEvent.Raise();
            }
        }

        // R772, F25 of the short check. The pane hashes the outputs of a step that ran in Revit a moment after its end, so the next step of a build
        // that still waits on it runs once it no longer does, looked at every quarter second, and at most 10 seconds later, when the flow then
        // says why it cannot run
        private static void RunWhenHashed(Func<bool> waits, Action run)
        {
            if (!waits())
            {
                run();
                return;
            }
            DateTime until = DateTime.UtcNow.AddSeconds(10);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (sender, e) =>
            {
                if (waits() && DateTime.UtcNow < until)
                {
                    return;
                }
                timer.Stop();
                run();
            };
            timer.Start();
        }

        // R686. What the plan of the one Build reads, null with no saved model or no settings
        private SiteBuildPlace BuildPlace(StagesViewModel stages)
        {
            PaneSettings settings = model.Settings;
            DocumentSession document = model.Document;
            string work = WorkFolderNow();
            if (settings == null || document == null || work == null)
            {
                return null;
            }
            RulesWindow.ProjectRulesPlace rules = RulesPlace();
            return new SiteBuildPlace
            {
                ConfigFile = settings.ConfigFile, ReadFolder = StageRead.Latest(work), RulesFile = rules?.RulesFile, WorkFolder = work, ProjectMode = settings.Project.ProjectMode,
                PositionFile = SiteBuildPlan.PositionFileOf(settings.Project.PositionFile, settings.ConfigFile),
                Document = new Common.WorkerDocument { Path = document.Path, SessionId = document.SessionId },
            };
        }

        // R687. The 2D preview over the main window of Revit, true to go on
        private bool ShowPreview(SiteBuildResult plan)
        {
            var window = new SiteBuildPreviewWindow(plan);
            new System.Windows.Interop.WindowInteropHelper(window).Owner = owner;
            return window.ShowDialog() == true;
        }

        // Point 3 of R2-C3, the one ask of the first Build with no rules chosen. A WPF box, as a TaskDialog needs the API context of Revit
        private static bool AskRules(string text)
        {
            return System.Windows.MessageBox.Show(text, RibbonLayout.Tab, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;
        }

        // R682. Where the rules table of the active project reads and writes, null with no project
        private RulesWindow.ProjectRulesPlace RulesPlace()
        {
            PaneSettings settings = model.Settings;
            DocumentSession document = model.Document;
            if (settings == null || document == null)
            {
                return null;
            }
            return new RulesWindow.ProjectRulesPlace
            {
                ConfigFile = settings.ConfigFile, RulesProfile = settings.RulesFile, WorkRoot = WorkRoot(), WorkFolder = WorkFolderNow(),
                ProjectMode = settings.Project.ProjectMode, TemplateFolder = RulesWindow.ProjectRulesTable.InstalledTemplates(), Who = Environment.UserName,
                Document = new Common.WorkerDocument { Path = document.Path, SessionId = document.SessionId },
            };
        }

        // R682. Settings, Rules, Open: the rules table over the main window of Revit, then the pane reads the rules again
        private void OpenRules(StagesViewModel stages)
        {
            RulesWindow.ProjectRulesPlace place = RulesPlace();
            if (place == null)
            {
                return;
            }
            var window = new RulesWindow.ProjectRulesWindow(new RulesWindow.ProjectRulesViewModel(place));
            new System.Windows.Interop.WindowInteropHelper(window).Owner = owner;
            window.ShowDialog();
            rulesStamp = null;
            stages.Refresh();
        }

        // R683. The amber line, read again only when the config, the rules or their log changed, as the pane refreshes on every change of its model
        private string rulesStamp;
        private string rulesLine;

        private string RulesLineNow()
        {
            RulesWindow.ProjectRulesPlace place = RulesPlace();
            if (place == null)
            {
                return null;
            }
            string stamp = string.Join("|", new[] { place.ConfigFile, place.RulesFile, place.Folder == null ? null : System.IO.Path.Combine(place.Folder, RulesWindow.RulesFileNames.LogName) }
                .Select(path => string.IsNullOrEmpty(path) || !System.IO.File.Exists(path) ? path + "@none" : path + "@" + System.IO.File.GetLastWriteTimeUtc(path).Ticks));
            if (stamp != rulesStamp)
            {
                rulesStamp = stamp;
                rulesLine = RulesWindow.ProjectRulesTable.AmberLine(RulesWindow.ProjectRulesTable.Read(place), RulesWindow.ProjectRulesTable.HasRules(place));
            }
            return rulesLine;
        }

        private static string WorkRoot()
        {
            return WorkFolder.Root(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), WorkFolder.ReadIfThere);
        }

        // R445. The settings of the document shown, laid over by Save setup and written into that document at once, then loaded by the
        // pane as they were written. Cancel changes nothing. How it works pressed with no document open leaves New project shut, as its
        // pane button is grey then. With no model to write to, the document not found or read only, the pane keeps the setup as before
        // R445 and the next real run of a button writes it
        internal void OpenNewProject(UIApplication application)
        {
            DocumentSession document = model.Document;
            if (document == null)
            {
                return;
            }
            Document revit = DocumentOf(application, document);
            bool modelOpen = revit != null && !revit.IsReadOnly;
            string root = WorkRoot();
            ProjectSettings saved = NewProject.NewProjectDialog.Show(owner, document.Settings.Project, document.Title, root, WorkFolder.For(root, document.Path), new DialogPicker(), modelOpen);
            if (saved != null)
            {
                model.LoadSettings(new PaneSettings(Written(modelOpen ? revit : null, saved)));
            }
            SavedStartsChanged();
        }

        // R541. The rule values of the document. A Save names its rules copy in the settings and writes them at once as Save setup does, so the
        // hash of the Rules input moves and 4 Check goes back to Ready, the steps after it wait on it
        internal void OpenRuleValues(UIApplication application, Document revit, DocumentSession session, Action<IntPtr> manualRules)
        {
            string root = WorkRoot();
            string saved = RuleValues.RuleValuesBody.Show(application.MainWindowHandle, session.Settings, root, WorkFolder.For(root, session.Path), revit.IsReadOnly, manualRules, line => TaskDialog.Show(RibbonLayout.Tab, line));
            if (saved != null)
            {
                session.Settings.RulesFile = saved;
                var settings = new PaneSettings(Written(revit, session.Settings.Project));
                if (model.Document == session)
                {
                    model.LoadSettings(settings);
                }
            }
        }

        // The setup as the model holds it after the commit, the setup as saved with no model to write to, else the setup as saved with
        // the reason in the TaskDialog of the buttons
        private static ProjectSettings Written(Document revit, ProjectSettings saved)
        {
            string reason;
            try
            {
                SetupWriteResult result = SetupWrite.Write(saved, revit == null ? null : new Revit.RevitModel(revit), revit == null ? null : new Revit.SettingsEntity(revit));
                if (result.Outcome != SetupWriteOutcome.Failed)
                {
                    return result.Settings ?? saved;
                }
                reason = result.Reason;
            }
            // Review of FIX-1NOV-C: a refused argument or JSON would otherwise leave Execute with the setup lost and nothing said
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is System.IO.InvalidDataException
                || error is Newtonsoft.Json.JsonException || error is Autodesk.Revit.Exceptions.ApplicationException)
            {
                reason = error.Message;
            }
            TaskDialog.Show(RibbonLayout.Tab, TextTemplate.Fill(NewProject.NewProjectText.NotWritten, reason));
            return saved;
        }

        private Document DocumentOf(UIApplication application, DocumentSession document)
        {
            foreach (Document open in application.Application.Documents)
            {
                if (!open.IsFamilyDocument && !open.IsLinked && sessions.Find(open) == document)
                {
                    return open;
                }
            }
            return null;
        }

        private void SavedStartsChanged()
        {
            model.SetSavedStarts(SavedStarts.List(SavedStarts.Folder(WorkRoot())).Select(start => start.Name).ToList());
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            // R680. The pane in 3 stages docks, the pane of version 1 stays whole under its Advanced
            var stages = new StagesViewModel(model) { WorkFolderNow = WorkFolderNow };
            stages.ChooseFolderRequested += () => ChooseProjectFolder(stages);
            stages.FixItemsRequested += FixItems;
            stages.RulesRequested += () => OpenRules(stages);
            stages.RulesLineNow = RulesLineNow;
            var build = new SiteBuildFlow
            {
                Place = () => BuildPlace(stages), AskRules = AskRules, UseManual = () => RulesPlace() == null ? null : RulesWindow.ProjectRulesTable.UseManual(RulesPlace()),
                ShowPreview = ShowPreview,
            };
            // R688. The steps of version 1 one after another, each by RequestRun as a press of its row, and each end back here through Buttons.ChainEnded
            // R741. The read for the build is a read of step 2, so it goes by the name of that step
            Func<int, string> stepName = number => model.Steps.FirstOrDefault(step => step.Number == (number == BuildChain.ReadForCheck ? StagesViewModel.ReadDataStep : number))?.Name
                ?? number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var chain = new BuildChain(stepName,
                number => number <= model.Statuses.Count ? model.Statuses[number - 1] : new StepStatus(StepKind.None))
            {
                ReadFirst = () => Fast5.BuildRead.Needed(WorkFolderNow()),
            };
            // R771, F6. Every step 2 of a build is one the chain put back, as BuildChain.Order holds none, and it reads with no window
            Fast7.QuietRead.Now = () => chain.Current == StagesViewModel.ReadDataStep;
            string planLine = null;
            // R688, R741. Each end of a step of the build, from Buttons.ChainEnded or from the read for the build, shows its line and runs the next
            Func<int, Common.Flow.RunEnd, string, bool> endOf = null;
            Action<int> runStep = number =>
            {
                if (number != BuildChain.ReadForCheck)
                {
                    model.RequestRun(number);
                    return;
                }
                PaneSettings settings = model.Settings;
                DocumentSession document = model.Document;
                string work = WorkFolderNow();
                if (settings == null || document == null || work == null)
                {
                    endOf(number, Common.Flow.RunEnd.Stopped, SiteBuildText.NoRead);
                    return;
                }
                Fast5.BuildRead.Start(settings, work, new Common.WorkerDocument { Path = document.Path, SessionId = document.SessionId }, ReadDataBody.Worker, stepName(number),
                    (done, message) => endOf(number, done ? Common.Flow.RunEnd.Committed : Common.Flow.RunEnd.Stopped, message));
            };
            stages.BuildRequested += () =>
            {
                if (chain.Running)
                {
                    return;
                }
                // R740, F5 of run 1. The rows the folder of stage 1 gives, before the plan and the steps that read them
                if (model.Settings != null)
                {
                    Fast5.BuildRead.FillRows(model.Settings, model.PickInput);
                }
                bool goOn = build.Run(stages.PreviewFirst, out string line);
                planLine = line;
                int? first = goOn ? chain.Start() : null;
                stages.ShowBuild(first.HasValue ? line + " " + chain.Progress : line);
                rulesStamp = null;
                stages.Refresh();
                if (first.HasValue)
                {
                    runStep(first.Value);
                }
            };
            // R689. Card 3 reads the newest plan of the work folder again only when it changed
            string resultsStamp = null;
            StageResults results = null;
            stages.ResultsNow = () =>
            {
                string plan = StageResults.LatestPlan(WorkFolderNow());
                string stamp = plan == null ? "none" : plan + "@" + System.IO.File.GetLastWriteTimeUtc(plan).Ticks;
                if (stamp != resultsStamp)
                {
                    resultsStamp = stamp;
                    results = StageResults.Read(plan);
                }
                return results;
            };
            // R690. Update from report of version 1 from card 3, the command of its ribbon button
            stages.UpdateRequested += () =>
            {
                runner.Command = nameof(Commands.UpdateFromReportCommand);
                runEvent.Raise();
            };
            endOf = (number, end, message) =>
            {
                if (!chain.Ended(number, end, message, out int? next, out string line))
                {
                    return false;
                }
                stages.ShowBuild(planLine + " " + line);
                // F12, R760 of FAST-7: a step that stopped says so in a box, the read for the build too
                stages.ShowStop(end, line);
                stages.Refresh();
                if (next.HasValue)
                {
                    int step = next.Value;
                    RunWhenHashed(() => chain.WaitsOnCommitted(step), () => runStep(step));
                }
                return true;
            };
            Buttons.ChainEnded = (number, outcome) => endOf(number, outcome.End, outcome.Message);
            var page = new StagesPage(stages, new SitePanePage(model));
            probe.Post = action => page.Dispatcher.BeginInvoke(action);
            data.FrameworkElement = page;
            data.VisibleByDefault = true;
            // DockablePaneState of Revit 2024 gives a docked pane MinimumWidth only, 200 by default, and a size for a floating pane
            // only, so the least width is the one lever for the 380 the Sizes of docs/design/12-ui-spec.md start the pane at, QL13
            // of steps/lane-b.md
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
                MinimumWidth = (int)(double)ThemeDictionary.Load()["SA.Size.Pane"],
            };
        }

        private void OnOpened(object sender, DocumentOpenedEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
                RearmFor(e.Document);
                DocumentSession session = sessions.Opened(e.Document, e.Document.PathName, e.Document.Title);
                // J1. The settings kept in the project come back when it opens, a JSON R367 refuses is named and left
                string refused = e.Document.IsFamilyDocument ? null : session.LoadFrom(new Revit.SettingsEntity(e.Document));
                Show(sender as Application);
                if (refused != null)
                {
                    TaskDialog.Show(RibbonLayout.Tab, refused);
                }
            }
        }

        // R494. Edit Family and a link raise the same events, only a project brings a hidden pane back
        private void RearmFor(Document document)
        {
            if (!document.IsFamilyDocument && !document.IsLinked)
            {
                firstShow.Rearm();
            }
        }

        private void OnCreated(object sender, DocumentCreatedEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
                RearmFor(e.Document);
                sessions.Opened(e.Document, e.Document.PathName, e.Document.Title);
                Show(sender as Application);
            }
        }

        private void OnClosing(object sender, DocumentClosingEventArgs e)
        {
            sessions.Closing(e.DocumentId, e.Document);
        }

        private void OnClosed(object sender, DocumentClosedEventArgs e)
        {
            sessions.Closed(e.DocumentId, e.Status == RevitAPIEventStatus.Succeeded);
            Show(sender as Application);
        }

        // R445, breaker of FIX-1NOV-C. An undo or a redo can take back the write of Save setup or of a real run, so the pane reads the
        // Settings entity of that document again. A commit changes nothing here, the writer loads the pane itself
        private void OnChanged(object sender, DocumentChangedEventArgs e)
        {
            if (e.Operation != UndoOperation.TransactionUndone && e.Operation != UndoOperation.TransactionRedone)
            {
                return;
            }
            Document document = e.GetDocument();
            if (document == null || document.IsFamilyDocument)
            {
                return;
            }
            DocumentSession session = sessions.Undone(document, new Revit.SettingsEntity(document));
            if (session != null && session == model.Document)
            {
                model.LoadSettings(session.Settings);
            }
        }

        // A new project saved under a name gets its path and title, so the pane shows them and its steps. A plain save keeps both,
        // so DocumentSaved needs nothing
        private void OnSavedAs(object sender, DocumentSavedAsEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded && e.Document != null && sessions.SavedAs(e.Document, e.Document.PathName, e.Document.Title) != null)
            {
                Show(sender as Application);
            }
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            Document now = e.CurrentActiveView == null ? null : e.CurrentActiveView.Document;
            Document before = e.PreviousActiveView == null ? null : e.PreviousActiveView.Document;
            if (now == null || before == null || !now.Equals(before))
            {
                Load(now);
            }
            ShowOnce(sender as UIApplication, now);
        }

        // The first project view of the session shows the pane when Revit keeps it hidden. A pane Revit will not show stays as it is,
        // the view goes on
        private void ShowOnce(UIApplication application, Document now)
        {
            if (application == null)
            {
                return;
            }
            try
            {
                DockablePane pane = null;
                if (firstShow.Due(now != null && now.IsValidObject && !now.IsFamilyDocument, () => (pane = application.GetDockablePane(PaneId)).IsShown()))
                {
                    pane.Show();
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // GetDockablePane throws while the pane is not created yet, and Show may refuse, the user can open it from the tab
            }
        }

        // R494. The button Show pane. GetDockablePane and Show throw an ApplicationException while Revit has no pane to show, as with no
        // project open on a Revit that keeps panes for documents, and then the button tells it in one line
        internal bool ShowByHand(UIApplication application)
        {
            try
            {
                DockablePane pane = application.GetDockablePane(PaneId);
                pane.Show();
                return pane.IsShown();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return false;
            }
        }

        private void Show(Application application)
        {
            UIDocument active = application == null ? null : new UIApplication(application).ActiveUIDocument;
            Load(active == null ? null : active.Document);
        }

        // A family document is no project, so the pane shows none for it, as the ribbon keeps its buttons grey
        private void Load(Document document)
        {
            if (document == null || !document.IsValidObject || document.IsFamilyDocument)
            {
                model.Load(null);
                return;
            }
            model.Load(sessions.Seen(document, document.PathName, document.Title));
            (Buttons.State as PaneRunState)?.RefreshNbhCodes();
        }

        internal List<Common.OpenDocument> OpenDocuments(UIApplication application)
        {
            object active = application.ActiveUIDocument == null ? null : application.ActiveUIDocument.Document;
            foreach (Document document in application.Application.Documents)
            {
                if (!document.IsFamilyDocument && !document.IsLinked)
                {
                    sessions.Seen(document, document.PathName, document.Title);
                }
            }
            return sessions.Documents(active);
        }
    }

    // The box button of the pane runs a ribbon button through the one entry of Buttons
    internal sealed class RunButtonHandler : IExternalEventHandler
    {
        public int Number { get; set; }

        // R690. A button with no number, run by its command once, as Update from report of card 3
        public string Command { get; set; }

        public void Execute(UIApplication app)
        {
            string command = Command;
            Command = null;
            if (command != null)
            {
                Buttons.Run(command, app);
                return;
            }
            try
            {
                Buttons.Run(Number, app);
            }
            catch (Exception failure)
            {
                // R688. A step of the one Build that throws stops the build at that step, then the throw goes on as before
                Buttons.ChainEnded?.Invoke(Number, new Common.Flow.RunOutcome(Common.Flow.RunEnd.Stopped, failure.Message, null));
                throw;
            }
        }

        public string GetName()
        {
            return "SiteAdaptation.RunButton";
        }
    }

    // R445. New project in the API context of Revit, so Save setup writes the model at once
    internal sealed class NewProjectHandler : IExternalEventHandler
    {
        private readonly PaneHost host;

        public NewProjectHandler(PaneHost host)
        {
            this.host = host;
        }

        public void Execute(UIApplication app)
        {
            host.OpenNewProject(app);
        }

        public string GetName()
        {
            return "SiteAdaptation.NewProject";
        }
    }

    // A message of the pane in the TaskDialog the ribbon buttons use, shown in the API context of Revit as a run of a button is
    internal sealed class TellHandler : IExternalEventHandler
    {
        public string Text { get; set; }

        public void Execute(UIApplication app)
        {
            TaskDialog.Show(RibbonLayout.Tab, Text);
        }

        public string GetName()
        {
            return "SiteAdaptation.Tell";
        }
    }

    // docs/design/02-folders.md line 42. The answers of the workers go to the document of their request in the API context of
    // Revit, whatever document is active. Since 5c each answer goes on with the run of its button there (R375)
    internal sealed class AnswerHandler : IExternalEventHandler
    {
        private readonly PaneHost host;

        public AnswerHandler(PaneHost host)
        {
            this.host = host;
        }

        public void Execute(UIApplication app)
        {
            List<RoutedAnswer> routed = host.Answers.Route(host.OpenDocuments(app));
            host.Routed.AddRange(routed);
            LaunchedWorker.Hand(routed);
        }

        public string GetName()
        {
            return "SiteAdaptation.Answer";
        }
    }
}
