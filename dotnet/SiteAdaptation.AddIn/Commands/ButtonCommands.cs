using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SiteAdaptation.AddIn.Commands
{
    // R371, R846. One command per ribbon button, each only hands its own name to Buttons.Run. A step has no command, the cards run it

    [Transaction(TransactionMode.Manual)]
    public sealed class SiteAdaptationRulesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(nameof(SiteAdaptationRulesCommand), commandData.Application);
        }
    }

    // R494. Opens the pane by hand, also with no project open
    [Transaction(TransactionMode.Manual)]
    public sealed class ShowPaneCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(nameof(ShowPaneCommand), commandData.Application);
        }
    }

    // R494. Show pane stays on with no project open and in a family document, the pane belongs to the session
    public sealed class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return true;
        }
    }

    // Site Adaptation Rules stays grey with no project open
    public sealed class ProjectAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            Document document = applicationData.ActiveUIDocument == null ? null : applicationData.ActiveUIDocument.Document;
            return RibbonLayout.IsAvailable(document != null, document != null && document.IsFamilyDocument);
        }
    }
}
