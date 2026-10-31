using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common.BoundaryWalls;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.BoundaryWalls
{
    // R380. The body of button 8 Walls on the active document: WallsRun of Common with the Revit side, the plan of the last real run of
    // 4 Check for the scope of the pane settings of the document and the pattern of the wall type BW from its config file
    public static class WallsBody
    {
        public static ButtonBody For(UIApplication application, PaneSettings settings)
        {
            Document document = application.ActiveUIDocument.Document;
            SettingsScope scope = settings?.Scope;
            string config = settings?.ConfigFile;
            return new WallsRun(new RevitWallDocument(document), context => WallLoad.Walls(context, scope, config)).Body();
        }
    }
}
