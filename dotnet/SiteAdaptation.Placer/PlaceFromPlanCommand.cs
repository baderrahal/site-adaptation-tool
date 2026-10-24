using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            string reportPath = PlaceFromPlan.ReportPath;
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
            string prototypeFile;
            if (!PickFilter(plan, out prototypeFile))
            {
                return Result.Cancelled;
            }
            PlacerReport report = PlaceFromPlan.Run(active.Document, plan, planPath, real, prototypeFile);
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
                + (report.PrototypeFilter == null ? "" : "left out by the filter " + report.Counts.FilteredOut + ", ")
                + (report.Mode == "real" ? "placed " : "would place ") + report.Counts.Placed + ", skipped " + report.Counts.Skipped
                + ", parameters written " + report.Counts.ParametersWritten + ", issues " + report.Counts.Issues + ".";
            if (report.RolledBack)
            {
                text += "\nRolled back: " + report.Failure;
            }
            return text;
        }

        // R102. Every plot, or the plots of one prototype file of the plan. False when the choice is cancelled
        private static bool PickFilter(PlanFile plan, out string prototypeFile)
        {
            prototypeFile = null;
            var files = (plan.Plots ?? new List<PlanPlot>())
                .Where(plot => plot != null && plot.PrototypeFile != null && !string.IsNullOrWhiteSpace(plot.PrototypeFile.Value))
                .Select(plot => plot.PrototypeFile.Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var which = new TaskDialog(Title)
            {
                MainInstruction = "Which plots should be placed?",
                CommonButtons = TaskDialogCommonButtons.Cancel,
            };
            which.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Every plot of the plan");
            which.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "The plots of one prototype file", files.Count + " file(s) in the plan");
            TaskDialogResult answer = which.Show();
            if (answer == TaskDialogResult.CommandLink1)
            {
                return true;
            }
            if (answer != TaskDialogResult.CommandLink2)
            {
                return false;
            }
            using (var form = new System.Windows.Forms.Form { Text = Title, Width = 520, Height = 150, FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog, StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen, MinimizeBox = false, MaximizeBox = false })
            {
                var list = new System.Windows.Forms.ComboBox { Left = 12, Top = 12, Width = 480, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
                list.Items.AddRange(files.Cast<object>().ToArray());
                if (files.Count > 0)
                {
                    list.SelectedIndex = 0;
                }
                var ok = new System.Windows.Forms.Button { Text = "OK", Left = 336, Top = 60, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new System.Windows.Forms.Button { Text = "Cancel", Left = 417, Top = 60, Width = 75, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new System.Windows.Forms.Control[] { list, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog() != DialogResult.OK || list.SelectedItem == null)
                {
                    return false;
                }
                prototypeFile = (string)list.SelectedItem;
                return true;
            }
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
