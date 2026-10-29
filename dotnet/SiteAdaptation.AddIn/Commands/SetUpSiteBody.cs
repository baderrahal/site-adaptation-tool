using System;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.AddIn
{
    // R373, R374, R383. The body of button 1 on the active document of Revit, SetUpRun of Common with the Revit side, the window and
    // the tag step of 5b, which does nothing until 5c fills it from R368
    public static class SetUpSiteBody
    {
        // The window of button 1 for its owner, the Revit main window. The main session sets it at the merge of 5b to
        // owner => new SetUpSite.SetUpSiteDialog(owner). Null keeps button 1 not built
        public static Func<IntPtr, ISetUpSiteDialog> Dialog { get; set; }

        public static ButtonBody For(UIApplication application)
        {
            var run = new SetUpRun(new RevitSiteDocument(application.ActiveUIDocument.Document, application.Application), Dialog(application.MainWindowHandle), new NoTags(),
                WorkFiles.ReadText, PaneText.Settings, null);
            return run.Body();
        }
    }
}
