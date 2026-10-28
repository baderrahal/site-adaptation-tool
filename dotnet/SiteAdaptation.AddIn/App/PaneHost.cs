using System;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System.Windows.Threading;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Theme;

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
            host.model.RunRequested += number =>
            {
                runner.Number = number;
                host.runEvent.Raise();
            };
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
                sessions.Opened(e.Document, e.Document.PathName, e.Document.Title);
                Show(sender as Application);
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

    // docs/design/02-folders.md line 42. The answers of the workers go to the document of their request in the API context of
    // Revit, whatever document is active. 5a routes them only, the run record they write comes with the buttons
    internal sealed class AnswerHandler : IExternalEventHandler
    {
        private readonly PaneHost host;

        public AnswerHandler(PaneHost host)
        {
            this.host = host;
        }

        public void Execute(UIApplication app)
        {
            host.Routed.AddRange(host.Answers.Route(host.OpenDocuments(app)));
        }

        public string GetName()
        {
            return "SiteAdaptation.Answer";
        }
    }
}
