using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NUnit.Framework;

namespace SiteAdaptation.RevitTests
{
    // SETUP-sign. Opens a copy of a demo model in the real Revit 2024 that ricaun.RevitTest starts, and counts its floors two ways that
    // share no call: a collector of the Floor class, and the rows of a floor schedule made in the copy. The model is never opened in
    // place: the file named by SITEADAPT_REVIT_TEST_MODEL is copied to a new file beside it, the copy is opened, closed without saving
    // and deleted, so the model of the person is never written. The path must lie under the work root of %APPDATA%\SiteAdaptation\machine.json
    public sealed class FloorCountTests
    {
        public const string ModelVariable = "SITEADAPT_REVIT_TEST_MODEL";
        public const string ExpectedVariable = "SITEADAPT_REVIT_TEST_FLOORS";

        private static string WorkRoot()
        {
            string machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SiteAdaptation", "machine.json");
            string text = File.ReadAllText(machine);
            // The one string value of workRoot, read without a JSON library so this project needs none
            int at = text.IndexOf("\"workRoot\"", StringComparison.Ordinal);
            int open = text.IndexOf('"', text.IndexOf(':', at) + 1);
            int close = text.IndexOf('"', open + 1);
            return text.Substring(open + 1, close - open - 1).Replace("\\\\", "\\");
        }

        [Test]
        public void The_floors_of_a_copy_of_the_model_are_counted_by_a_collector_and_by_a_schedule(UIApplication uiapp)
        {
            string model = Environment.GetEnvironmentVariable(ModelVariable);
            Assert.That(model, Is.Not.Null.And.Not.Empty, ModelVariable + " names the copy of the model to open");
            string root = Path.GetFullPath(WorkRoot()).TrimEnd('\\') + "\\";
            Assert.That(Path.GetFullPath(model).StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True, "the model must lie under the work root " + root);

            string copy = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(model)), "revit-test-" + Guid.NewGuid().ToString("N") + ".rvt");
            File.Copy(model, copy);
            Document document = null;
            try
            {
                document = uiapp.Application.OpenDocumentFile(copy);

                int collector = new FilteredElementCollector(document).OfClass(typeof(Floor)).WhereElementIsNotElementType().GetElementCount();

                int rows;
                using (var transaction = new Transaction(document, "floor count schedule"))
                {
                    transaction.Start();
                    ViewSchedule schedule = ViewSchedule.CreateSchedule(document, new ElementId(BuiltInCategory.OST_Floors));
                    SchedulableField field = schedule.Definition.GetSchedulableFields().First(f => f.FieldType == ScheduleFieldType.Instance);
                    schedule.Definition.AddField(field);
                    schedule.Definition.IsItemized = true;
                    rows = schedule.GetTableData().GetSectionData(SectionType.Body).NumberOfRows;
                    // The schedule lives in the copy only, nothing of it is saved
                    transaction.RollBack();
                }

                TestContext.Out.WriteLine("floors by collector: " + collector + ", floors by schedule rows: " + rows);
                Assert.That(rows, Is.EqualTo(collector), "the schedule and the collector count the same floors");
                string expected = Environment.GetEnvironmentVariable(ExpectedVariable);
                if (!string.IsNullOrEmpty(expected))
                {
                    Assert.That(collector, Is.EqualTo(int.Parse(expected)), "the floors the model is known to hold");
                }
            }
            finally
            {
                if (document != null)
                {
                    document.Close(false);
                }
                File.Delete(copy);
            }
        }
    }
}
