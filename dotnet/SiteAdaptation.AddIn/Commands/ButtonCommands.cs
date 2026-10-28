using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SiteAdaptation.AddIn.Commands
{
    // R371. One command per ribbon button, each only hands its number to Buttons.Run

    [Transaction(TransactionMode.Manual)]
    public sealed class SetUpSiteCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(1, commandData.Application);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class ReadDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(2, commandData.Application);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class CheckCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(3, commandData.Application);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class PlaceVillasCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(4, commandData.Application);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class FloorsAndRampsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(5, commandData.Application);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class LevelsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(6, commandData.Application);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class WallsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(7, commandData.Application);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class ReportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Buttons.Run(8, commandData.Application);
        }
    }

    // The buttons stay grey with no project open
    public sealed class ProjectAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            Document document = applicationData.ActiveUIDocument == null ? null : applicationData.ActiveUIDocument.Document;
            return RibbonLayout.IsAvailable(document != null, document != null && document.IsFamilyDocument);
        }
    }
}
