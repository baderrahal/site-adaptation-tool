using System;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.Common;

namespace SiteAdaptation.Placer
{
    // Place from plan. A dry run unless the second choice is taken, and the real run is one transaction (R86)
    [Transaction(TransactionMode.Manual)]
    public class PlaceFromPlanCommand : IExternalCommand
    {
        private const string Title = "Place from plan";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument active = commandData.Application.ActiveUIDocument;
            if (active == null || active.Document.IsFamilyDocument)
            {
                TaskDialog.Show(Title, "Open the project document to place into first.");
                return Result.Cancelled;
            }
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SiteAdaptation");
            string planPath = PickPlan(folder);
            if (planPath == null)
            {
                return Result.Cancelled;
            }
            string reportPath = Path.Combine(folder, "placer_report.json");
            PlanFile plan;
            try
            {
                plan = PlanFile.Read(planPath);
            }
            catch (Exception failure)
            {
                PlacerReport unread = Common.Placer.StoppedReport("none", planPath, active.Document.Title, "The plan file could not be read: " + failure.Message);
                JsonOut.Write(reportPath, unread);
                TaskDialog.Show(Title, unread.Failure + "\n" + reportPath);
                return Result.Failed;
            }

            var choice = new TaskDialog(Title)
            {
                MainInstruction = "How should " + Path.GetFileName(planPath) + " run?",
                MainContent = "Document: " + active.Document.Title,
                CommonButtons = TaskDialogCommonButtons.Cancel,
            };
            choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry run", "Changes nothing. The report lists what would be placed and what would be skipped.");
            choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Place for real", "Places every plot in one transaction. Any failure rolls all of it back.");
            TaskDialogResult answer = choice.Show();
            if (answer != TaskDialogResult.CommandLink1 && answer != TaskDialogResult.CommandLink2)
            {
                return Result.Cancelled;
            }

            bool real = answer == TaskDialogResult.CommandLink2;
            var target = new RevitPlacerTarget(active.Document);
            PlacerReport report;
            try
            {
                report = Common.Placer.Run(plan, planPath, target, real);
            }
            catch (Exception failure)
            {
                // Run rolls its own transaction back, so only a failure outside it lands here
                target.RollBack();
                report = Common.Placer.StoppedReport(real ? "real" : "dryRun", planPath, active.Document.Title, "The run failed, nothing stays: " + failure.Message);
                report.RolledBack = real;
            }
            JsonOut.Write(reportPath, report);
            TaskDialog.Show(Title, Summary(report) + "\n" + reportPath);
            return Result.Succeeded;
        }

        private static string Summary(PlacerReport report)
        {
            string mode = report.Mode == "real" ? "Real run" : "Dry run, nothing was changed";
            if (report.Stopped)
            {
                return mode + ". Stopped: " + report.Failure;
            }
            string text = mode + ". Plots in plan " + report.Counts.PlotsInPlan + ", "
                + (report.Mode == "real" ? "placed " : "would place ") + report.Counts.Placed + ", skipped " + report.Counts.Skipped
                + ", parameters written " + report.Counts.ParametersWritten + ", issues " + report.Counts.Issues + ".";
            if (report.RolledBack)
            {
                text += "\nRolled back: " + report.Failure;
            }
            return text;
        }

        private static string PickPlan(string folder)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Plan file";
                dialog.Filter = "Plan files (*.json)|*.json";
                dialog.InitialDirectory = Directory.Exists(folder) ? folder : "";
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }
    }

    public class ProjectOpen : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData.ActiveUIDocument != null && !applicationData.ActiveUIDocument.Document.IsFamilyDocument;
        }
    }
}
