using System;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Update;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Update;

namespace SiteAdaptation.AddIn
{
    // R402, 5f. The body of the button Update from report on the active document of Revit: UpdateFromReportRun of the add-in with the real
    // window and the worker CadReader.exe. The window is made here and not set by App, a test host sets Dialog to a window of its own
    public static class UpdateFromReportBody
    {
        public static Func<IntPtr, IUpdateFromReportDialog> Dialog { get; set; } = owner => new UpdateFromReportDialog(owner);

        public static ButtonBody For(UIApplication application)
        {
            Autodesk.Revit.DB.Document document = application.ActiveUIDocument.Document;
            string path = document.PathName;
            Pane.DocumentSession session = PaneHost.Current.Sessions.Seen(document, document.PathName, document.Title);
            var run = new UpdateFromReportRun(Dialog(application.MainWindowHandle), WorkFiles.ReadText, UpdateWorkerClient.Read,
                new WorkerDocument { Path = path, SessionId = "update-from-report" }) { ModePick = () => session.Settings.Project.ProjectMode, RulesFile = () => session.Settings.RulesFile };
            return run.Body();
        }
    }
}
