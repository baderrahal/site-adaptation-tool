using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.Common.Flow;
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
            return Run(RibbonLayout.Button(number), application);
        }

        // A button with no number, found by its command, takes the same flow. In 5a it has no body, so the flow stops on its
        // not built text before it asks for a step (13-workflow-changes.md, 5c and 5f build them)
        public static Result Run(string commandName, UIApplication application)
        {
            return Run(RibbonLayout.ButtonOf(commandName), application);
        }

        private static Result Run(RibbonButtonSpec button, UIApplication application)
        {
            Document document = application.ActiveUIDocument == null ? null : application.ActiveUIDocument.Document;
            var flow = new ButtonFlow(State, new RevitModel(document), new TaskDialogConfirm(), InputHash.Of, () => RunIds.New(button.Number, DateTime.Now));
            ButtonBody body = Bodies.Of(button, () => SetUpSiteBody.For(application));
            // 5f. Update from report has no number, so Bodies.Of leaves it not built, its body is made here on the active document
            if (document != null && button == RibbonLayout.ButtonOf("UpdateFromReportCommand") && UpdateFromReportBody.Dialog != null)
            {
                body = UpdateFromReportBody.For(application);
            }
            RunOutcome outcome = flow.Run(button.Number, button.Label, body, document == null ? null : document.PathName);
            if (outcome.Message != null)
            {
                TaskDialog.Show(RibbonLayout.Tab, outcome.Message);
            }
            return outcome.End == RunEnd.Cancelled ? Result.Cancelled : Result.Succeeded;
        }
    }

    // R382. The body of each button. Button 1 is built in 5b and runs once its window is set, every other one stops with the text of
    // R371 through the flow
    public static class Bodies
    {
        public static ButtonBody Of(int number)
        {
            return Of(RibbonLayout.Button(number));
        }

        public static ButtonBody Of(RibbonButtonSpec button)
        {
            return Of(button, null);
        }

        // setUp makes the body of button 1 on the active document. It is a delegate so this class names no Revit type, which the
        // tests load none of
        public static ButtonBody Of(RibbonButtonSpec button, Func<ButtonBody> setUp)
        {
            if (button.Number == 1 && SetUpSiteBody.Dialog != null && setUp != null)
            {
                return setUp();
            }
            return new ButtonBody { NotBuilt = RunTexts.NotBuilt(button.Label) };
        }
    }
}
