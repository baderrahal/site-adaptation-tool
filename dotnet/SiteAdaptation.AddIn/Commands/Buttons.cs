using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Flow;
using SiteAdaptation.AddIn.Revit;

namespace SiteAdaptation.AddIn
{
    // R371, R382, docs/design/08-install.md. The one entry of a button, the ribbon command and later the test host call it, so no run
    // logic is copied. Each button runs through the flow of R382, and in 5a every body still says it is not built yet
    public static class Buttons
    {
        // The pane side sets it at start. No button with a body runs before then
        public static IRunState State { get; set; }

        public static string NotBuiltText(int number)
        {
            return RunTexts.NotBuilt(RibbonLayout.Button(number).Label);
        }

        public static Result Run(int number, UIApplication application)
        {
            Document document = application.ActiveUIDocument == null ? null : application.ActiveUIDocument.Document;
            var flow = new ButtonFlow(State, new RevitModel(document), new TaskDialogConfirm(), InputHash.Of, () => RunIds.New(number, DateTime.Now));
            RunOutcome outcome = flow.Run(number, Bodies.Of(number), document == null ? null : document.PathName);
            if (outcome.Message != null)
            {
                TaskDialog.Show(RibbonLayout.Tab, outcome.Message);
            }
            return outcome.End == RunEnd.Cancelled ? Result.Cancelled : Result.Succeeded;
        }
    }
}
