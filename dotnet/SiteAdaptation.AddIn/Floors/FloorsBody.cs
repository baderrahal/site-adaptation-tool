using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.Floors
{
    // R378. The body of button 5 Floors and ramps on the active document: FloorsRun of Common with the Revit side, the plan of the last
    // real run of 4 Check for the scope of the pane settings of the document
    public static class FloorsBody
    {
        public static ButtonBody For(UIApplication application, PaneSettings settings)
        {
            Document document = application.ActiveUIDocument.Document;
            SettingsScope scope = settings?.Scope;
            return new FloorsRun(new RevitFloorDocument(document), context => FloorLoad.Floors(context, scope)).Body();
        }
    }

    // R379. The body of button 6 Levels on the active document, with the worker CadReader.exe beside the add-in for levelExcel, waited
    // for inside the transaction as ReportWorkerClient waits for the report, ended by WorkerLaunch at the time limit of the request
    public static class LevelsBody
    {
        public static ButtonBody For(UIApplication application, PaneSettings settings)
        {
            Document document = application.ActiveUIDocument.Document;
            SettingsScope scope = settings?.Scope;
            var worker = new WorkerDocument { Path = document.PathName, SessionId = "levels" };
            return new LevelsRun(new RevitFloorDocument(document), context => FloorLoad.Levels(context, scope, worker), Report.ReportWorkerClient.Run).Body();
        }
    }
}
