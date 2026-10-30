using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Report;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Report;

namespace SiteAdaptation.AddIn
{
    // R381, 5e. The body of button 9 Report on the active document of Revit: ReportRun of Common with the counts RevitCountsReader reads from the
    // tags of the model and the worker CadReader.exe. Until 4 Check is built no run log of 4 lies in the work folder, so on a real model it
    // stops there with 9 Report needs step 4
    public static class ReportBody
    {
        public static ButtonBody For(UIApplication application)
        {
            Document document = application.ActiveUIDocument.Document;
            var run = new ReportRun(PaneText.Settings, WorkFiles.ReadText, (levelPlots, levelRun) => RevitCountsReader.Read(document, levelPlots, levelRun),
                ReportWorkerClient.Run, new WorkerDocument { Path = document.PathName, SessionId = "report" });
            return run.Body();
        }
    }
}
