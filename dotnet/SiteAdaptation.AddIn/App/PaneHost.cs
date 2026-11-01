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
        private ExternalEvent answerEvent;
        private ExternalEvent tellEvent;
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
            var teller = new TellHandler();
            host.tellEvent = ExternalEvent.Create(teller);
            host.model.Told += text =>
            {
                teller.Text = text;
                host.tellEvent.Raise();
            };
            // Lane C NEWPROJ-UI. New project and How it works open from the pane on the UI thread of Revit. Neither needs the API context,
            // since Save setup changes the settings in memory only and the next real run writes them (R367)
            host.owner = application.MainWindowHandle;
            host.model.NewProjectRequested += host.OpenNewProject;
            host.model.HowItWorksRequested += () =>
            {
                if (HowItWorks.HowItWorksDialog.Show(host.owner, host.model.Document != null))
                {
                    host.OpenNewProject();
                }
            };
            host.SavedStartsChanged();
            Buttons.State = new PaneRunState(host.model, WorkRoot, WorkFolder.ReadIfThere);
            application.RegisterDockablePane(PaneId, PaneText.Title, host);
            application.ControlledApplication.DocumentOpened += host.OnOpened;
            application.ControlledApplication.DocumentCreated += host.OnCreated;
            application.ControlledApplication.DocumentClosing += host.OnClosing;
            application.ControlledApplication.DocumentClosed += host.OnClosed;
            application.ControlledApplication.DocumentSavedAs += host.OnSavedAs;
            application.ViewActivated += host.OnViewActivated;
            Current = host;
        }

        private static string WorkRoot()
        {
            return WorkFolder.Root(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), WorkFolder.ReadIfThere);
        }

        // The settings of the document shown, laid over by Save setup, the pane takes them as a pick. Cancel and a document with no
        // settings yet change nothing. How it works pressed with no document open leaves New project shut, as its pane button is grey then
        private void OpenNewProject()
        {
            DocumentSession document = model.Document;
            if (document == null)
            {
                return;
            }
            string root = WorkRoot();
            ProjectSettings saved = NewProject.NewProjectDialog.Show(owner, document.Settings.Project, document.Title, root, WorkFolder.For(root, document.Path), new DialogPicker());
            if (saved != null)
            {
                model.LoadSettings(new PaneSettings(saved));
            }
            SavedStartsChanged();
        }

        private void SavedStartsChanged()
        {
            model.SetSavedStarts(SavedStarts.List(SavedStarts.Folder(WorkRoot())).Select(start => start.Name).ToList());
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            var page = new SitePanePage(model);
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

        private void OnCreated(object sender, DocumentCreatedEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded && e.Document != null)
            {
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

        public void Execute(UIApplication app)
        {
            Buttons.Run(Number, app);
        }

        public string GetName()
        {
            return "SiteAdaptation.RunButton";
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
