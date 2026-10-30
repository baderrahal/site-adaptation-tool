using System;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.AddIn
{
    // R373, R374, R383, R367. The body of button 1 on the active document of Revit, SetUpRun of Common with the Revit side, the window,
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
            var run = new SetUpRun(new RevitSiteDocument(document, application.Application), Dialog(application.MainWindowHandle), new NoTags(),
                WorkFiles.ReadText, PaneText.Settings, null)
                .Recording(() => session.Settings.Project, new SettingsEntity(document), () => DateTime.UtcNow);
            return run.Body();
        }
    }
}
