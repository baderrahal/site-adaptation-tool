using System;
using System.IO;
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
            if (commandName == nameof(Commands.SiteAdaptationRulesCommand))
            {
                return Rules(application);
            }
            return Run(RibbonLayout.ButtonOf(commandName), application);
        }

        // R403. The window changes no model element, so it takes no flow, no step and no saved project, only the rules file the pane
        // holds for the active document
        private static Result Rules(UIApplication application)
        {
            Document document = application.ActiveUIDocument == null ? null : application.ActiveUIDocument.Document;
            Pane.DocumentSession session = document == null ? null : PaneHost.Current.Sessions.Seen(document, document.PathName, document.Title);
            try
            {
                RulesWindow.RulesWindowBody.Show(application.MainWindowHandle, session?.Settings?.RulesFile);
            }
            catch (FileNotFoundException error)
            {
                TaskDialog.Show(RibbonLayout.Tab, error.Message + ": " + error.FileName);
            }
            catch (InvalidDataException error)
            {
                TaskDialog.Show(RibbonLayout.Tab, error.Message);
            }
            return Result.Succeeded;
        }

        private static Result Run(RibbonButtonSpec button, UIApplication application)
        {
            Document document = application.ActiveUIDocument == null ? null : application.ActiveUIDocument.Document;
            var flow = new ButtonFlow(State, new RevitModel(document), new TaskDialogConfirm(), InputHash.Of, () => RunIds.New(button.Number, DateTime.Now));
            Func<ButtonBody> readData = () => ReadDataBody.For(PaneHost.Current.Sessions.Seen(document, document.PathName, document.Title));
            Func<ButtonBody> reviewData = () =>
            {
                Pane.DocumentSession session = document == null ? null : PaneHost.Current.Sessions.Seen(document, document.PathName, document.Title);
                ReviewData.ReviewDataBody.Nbh = () => session?.Settings?.Scope?.Kind == Common.Settings.ScopeKinds.Nbh ? session.Settings.Scope.Nbh : null;
                return ReviewData.ReviewDataBody.For(application.MainWindowHandle);
            };
            Func<ButtonBody> checkTable = () => CheckTable.CheckTableBody.For(PaneHost.Current.Sessions.Seen(document, document.PathName, document.Title), application.MainWindowHandle);
            // Null until the pane sets the load, so this class names the Revit side only here, where Revit types are at home
            Func<ButtonBody> placeVillas = PlaceVillas.PlaceVillasBody.IsSet ? (Func<ButtonBody>)(() => PlaceVillas.PlaceVillasBody.For(application)) : null;
            ButtonBody body = button.Number == 2 && document != null ? Bodies.Of(button, null, readData)
                : button.Number == 3 ? Bodies.Of(button, null, null, reviewData)
                : button.Number == 4 && document != null ? Bodies.Of(button, null, null, null, checkTable)
                : button.Number == 7 && document != null ? Bodies.Of(button, null, null, null, null, placeVillas) : Bodies.Of(button, () => SetUpSiteBody.For(application));
            RunOutcome outcome = flow.Run(button.Number, button.Label, body, document == null ? null : document.PathName, later => Ended(application, document, button.Number, later));
            // R367. The pane takes the settings again from the entity button 1 wrote, only after its commit
            if (button.Number == 1 && outcome.End == RunEnd.Committed && document != null)
            {
                PaneHost.Current.Sessions.Find(document)?.LoadFrom(new SettingsEntity(document));
            }
            if (outcome.Message != null)
            {
                TaskDialog.Show(RibbonLayout.Tab, outcome.Message);
            }
            return outcome.End == RunEnd.Cancelled ? Result.Cancelled : Result.Succeeded;
        }

        // R375. The end of a worker, in the API context of Revit, goes to the document of its run, the active one or not, and to
        // none once that document closed
        private static void Ended(UIApplication application, Document document, int number, RunOutcome later)
        {
            if (document == null || !document.IsValidObject)
            {
                return;
            }
            Document active = application.ActiveUIDocument == null ? null : application.ActiveUIDocument.Document;
            if (later.Record != null)
            {
                if (active != null && active.Equals(document))
                {
                    State.Committed(number, later.Record);
                }
                else
                {
                    Pane.PaneRunState.Record(PaneHost.Current.Sessions.Find(document), number, later.Record);
                }
            }
            if (later.Message != null)
            {
                TaskDialog.Show(RibbonLayout.Tab, later.Message);
            }
        }
    }

    // R382. The body of each button. Button 1 is built in 5b and runs once its window is set, button 2 in 5c once its worker is set,
    // buttons 3, 4 and 7 once their windows and loads are set, every other one stops with the text of R371 through the flow
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
            return Of(button, setUp, null);
        }

        // R375. readData makes the body of button 2 on the document of the run, built once App sets the worker
        public static ButtonBody Of(RibbonButtonSpec button, Func<ButtonBody> setUp, Func<ButtonBody> readData)
        {
            return Of(button, setUp, readData, null);
        }

        // R402. reviewData makes the body of button 3 with the main window of Revit as owner, built once App sets the window and the load
        public static ButtonBody Of(RibbonButtonSpec button, Func<ButtonBody> setUp, Func<ButtonBody> readData, Func<ButtonBody> reviewData)
        {
            return Of(button, setUp, readData, reviewData, null);
        }

        // R376. checkTable makes the body of button 4 on the document of the run, built once App sets the window
        public static ButtonBody Of(RibbonButtonSpec button, Func<ButtonBody> setUp, Func<ButtonBody> readData, Func<ButtonBody> reviewData, Func<ButtonBody> checkTable)
        {
            return Of(button, setUp, readData, reviewData, checkTable, null);
        }

        // R377. placeVillas makes the body of button 7 on the active document, given only once the pane set the load of its inputs
        public static ButtonBody Of(RibbonButtonSpec button, Func<ButtonBody> setUp, Func<ButtonBody> readData, Func<ButtonBody> reviewData, Func<ButtonBody> checkTable,
            Func<ButtonBody> placeVillas)
        {
            if (button.Number == 4 && CheckTable.CheckTableBody.IsSet && checkTable != null)
            {
                return checkTable();
            }
            if (button.Number == 7 && placeVillas != null)
            {
                return placeVillas();
            }
            if (button.Number == 3 && ReviewData.ReviewDataBody.IsSet && reviewData != null)
            {
                return reviewData();
            }
            if (button.Number == 1 && SetUpSiteBody.Dialog != null && setUp != null)
            {
                return setUp();
            }
            if (button.Number == 2 && ReadDataBody.Worker != null && readData != null)
            {
                return readData();
            }
            return new ButtonBody { NotBuilt = RunTexts.NotBuilt(button.Label) };
        }
    }
}
