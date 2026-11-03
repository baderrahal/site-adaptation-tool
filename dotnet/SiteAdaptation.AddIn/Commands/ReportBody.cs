using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Report;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Report;
using SiteAdaptation.Common.SetUp;

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
            DocumentSession session = PaneHost.Current.Sessions.Seen(document, document.PathName, document.Title);
            var run = new ReportRun(PaneText.Settings, WorkFiles.ReadText, (levelPlots, levelRun) => RevitCountsReader.Read(document, levelPlots, levelRun),
                ReportWorkerClient.Run, new WorkerDocument { Path = document.PathName, SessionId = "report" }) { StandingFloors = () => RevitCountsReader.FloorIds(document), ModePick = () => session.Settings.Project.ProjectMode };
            run.FieldsPick = () => session.Settings.Project.ReportFields;
            run.LastRun = step => session.LastRun(step);
            run.RulesPick = () => session.Settings.RulesFile;
            // R441, the line Current files of the setup after the lines of the report
            return CurrentFiles.WithReportLine(run.Body(), () => session.Settings.Project);
        }
    }
}
