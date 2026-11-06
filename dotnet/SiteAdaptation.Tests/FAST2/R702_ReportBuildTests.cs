using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    // R702, R703. The whole report built from the made up plan of ReportExcelTests, once as a new project and once in the past job mode
    public sealed class R702_ReportBuildTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R702_" + Guid.NewGuid().ToString("N"));

        public R702_ReportBuildTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private ReportBook Book(string mode)
        {
            JObject config = ReportExcelTests.MadeConfig();
            config["projectRules"] = new JObject { ["pastJobMode"] = "pastMode" };
            config["designBasis"] = new JObject { ["mode"] = mode };
            string plots = Path.Combine(folder, "plots.csv");
            File.WriteAllText(plots, ReportExcelTests.PlotsCsv);
            var inputs = new ReportInputs
            {
                Plan = ReportExcelTests.MadePlan(), Rules = ReportExcelTests.MadeRules(), Config = config, Overrides = ReportExcelTests.MadeOverrides(),
                CheckRows = ReportExcelTests.MadeCheckRows(), Summary = ReportExcelTests.MadeSummary(),
                PlotRows = CsvRows.Read(plots, "locationPlanNumber", "zone", "nbhId", "typologyRev9"), RunId = "r1", Profile = "parsons2024Rev6", Now = new DateTime(2026, 11, 6),
            };
            return ReportSheets.Build(inputs).Books.First(book => book.Nbh == "012");
        }

        private static List<object[]> Rows(ReportBook book, string sheet)
        {
            return book.Sheets.Single(item => item.Name == sheet).Tables.SelectMany(table => table.Rows).ToList();
        }

        private static IEnumerable<string> Texts(ReportBook book, string sheet)
        {
            return Rows(book, sheet).SelectMany(row => row).OfType<string>();
        }

        [Fact]
        public void R702_a_new_project_report_holds_no_past_share_and_no_weak_rule_and_the_past_job_mode_keeps_them()
        {
            ReportBook fresh = Book("newMode");
            ReportTable box = fresh.Sheets.Single(item => item.Name == ReportExcelText.SummarySheet).Tables.Single(table => table.Title == ReportExcelText.RulesBox);
            Assert.NotEmpty(box.Rows);
            Assert.All(box.Rows, row => Assert.Equal(new object[] { ReportExcelText.NoPastShare, ReportExcelText.NoPastShare }, new[] { row[2], row[3] }));
            foreach (string sheet in new[] { ReportExcelText.AssumedSheet, ReportExcelText.RulesSheet, ReportExcelText.IssuesSheet })
            {
                Assert.DoesNotContain(Texts(fresh, sheet), text => text.Contains(" share "));
            }
            Assert.DoesNotContain(Rows(fresh, ReportExcelText.IssuesSheet), row => (row[2] as string) == ReportExcelText.WeakRuleCheck);

            ReportBook past = Book("pastMode");
            ReportTable pastBox = past.Sheets.Single(item => item.Name == ReportExcelText.SummarySheet).Tables.Single(table => table.Title == ReportExcelText.RulesBox);
            Assert.Contains(pastBox.Rows, row => row[2] is double);
            Assert.Contains(Rows(past, ReportExcelText.IssuesSheet), row => (row[2] as string) == ReportExcelText.WeakRuleCheck);
            Assert.Contains(Texts(past, ReportExcelText.AssumedSheet), text => text.Contains(" share "));
        }

        [Fact]
        public void R703_plot_schedule_floors_and_walls_name_the_sources_of_each_written_row_before_the_ids()
        {
            ReportBook book = Book("newMode");
            int schedule = Array.IndexOf(ReportExcelText.ScheduleColumns, ReportExcelText.SourceColumn);
            Assert.Equal("Villa ID", ReportExcelText.ScheduleColumns[schedule + 1]);
            List<object[]> rows = Rows(book, ReportExcelText.ScheduleSheet);
            Assert.Equal("computed, override, estimate, locationPlan", rows[0][schedule]);
            // The plot the plan could not write has no source, it has no value
            Assert.Null(rows.Last()[schedule]);
            Assert.Equal(ReportExcelText.SourceColumn, ReportExcelText.FloorColumns[ReportExcelText.FloorColumns.Length - 2]);
            Assert.All(Rows(book, ReportExcelText.FloorsSheet), row => Assert.Equal("computed", row[row.Length - 2]));
            Assert.Equal(ReportExcelText.SourceColumn, ReportExcelText.WallColumns[ReportExcelText.WallColumns.Length - 2]);
            Assert.All(Rows(book, ReportExcelText.WallsSheet), row => Assert.Equal("rule, locationPlan", row[row.Length - 2]));
        }
    }
}
