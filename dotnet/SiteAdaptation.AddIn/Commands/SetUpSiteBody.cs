using System;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.AddIn
{
    // R373, R374, R383, R367, R441, R454. The body of button 1 on the active document of Revit, SetUpRun of Common with the Revit side, the window,
    // the tag step and the Settings entity, written with the settings the pane holds for the document (J1, J19)
    public static class SetUpSiteBody
    {
        // The window of button 1 for its owner, the Revit main window, set by App at start. The test host sets a window of its own
        // for a run with no click. Null keeps button 1 not built
        public static Func<IntPtr, ISetUpSiteDialog> Dialog { get; set; }

        public static ButtonBody For(UIApplication application)
        {
            Autodesk.Revit.DB.Document document = application.ActiveUIDocument.Document;
            DocumentSession session = PaneHost.Current.Sessions.Seen(document, document.PathName, document.Title);
            // R441, the section Current files: the ticks and the rules file chosen go into the settings of the pane before the record
            ISetUpSiteDialog dialog = new CurrentFilesDialog(Dialog(application.MainWindowHandle), () => session.Settings.Project, next =>
            {
                session.Settings.Project.CurrentFiles = next.CurrentFiles;
                session.Settings.RulesFile = next.RulesFile;
            }, folder => System.IO.Directory.GetFiles(folder, "*.json"), WorkFiles.ReadText);
            var run = new SetUpRun(new RevitSiteDocument(document, application.Application), dialog, new NoTags(),
                WorkFiles.ReadText, PaneText.Settings, PaneText.SharedParameters)
                .Recording(() => session.Settings.Project, new SettingsEntity(document), () => DateTime.UtcNow)
                .ReadingCad(context => CoordinateRequest.Run(context, ReadDataBody.Inputs(session.Settings), ReadDataBody.Roles, PaneText.Settings,
                    new WorkerDocument { Path = session.Path, SessionId = session.SessionId }, Report.ReportWorkerClient.Run));
            return run.Body();
        }
    }
}
