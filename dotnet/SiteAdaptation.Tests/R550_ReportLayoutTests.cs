using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Report;
using SiteAdaptation.Common.Tags;
using SiteAdaptation.Common.Update;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R550 to R554, Q307. The file name by ISO 19650, the wall ids the model read gives and one count for pane and report. The sheets themselves
    // through ReportExcelTests, on the same made up plan
    public sealed class R550_ReportLayoutTests
    {
        private static JObject Config(params string[] without)
        {
            var block = new JObject { ["projectName"] = "P", ["project"] = "00101", ["originator"] = "PSN", ["volume"] = "<nbh>", ["level"] = "XXX", ["type"] = "SCH", ["role"] = "TU", ["number"] = "090000", ["note"] = "not a field" };
            foreach (string name in without)
            {
                block.Remove(name);
            }
            return new JObject { ["addin"] = new JObject { ["report"] = new JObject { ["fileName"] = block } } };
        }

        [Fact]
        public void R550_the_name_follows_the_past_schedule_with_the_nbh_in_the_volume()
        {
            Assert.Equal("00101-PSN-012-XXX-SCH-TU-090000.xlsx", ReportFile.Name("012", ReportFile.Fields(Config(), null)));
        }

        [Fact]
        public void R550_update_from_report_finds_the_nbh_in_the_iso_name_and_the_old_names()
        {
            Assert.Equal("012", UpdateWorkerRequest.NbhOf(@"C:\w\00101-PSN-012-XXX-SCH-TU-090000.xlsx"));
            Assert.Equal("012", UpdateWorkerRequest.NbhOf(@"C:\w\012_Report.xlsx"));
            Assert.Equal("012", UpdateWorkerRequest.NbhOf(@"C:\w\012_Levels_parsons2024Rev6.xlsx"));
            Assert.Null(UpdateWorkerRequest.NbhOf(@"C:\w\report.xlsx"));
        }

        [Fact]
        public void Q307_the_one_count_is_the_issues_and_the_warnings_of_a_run()
        {
            var run = new SiteAdaptation.Common.Settings.ButtonRun { Time = "2026-11-03T11:00:00.000Z", Issues = new List<string> { "a", "b" } };
            run.Counts[SiteAdaptation.Common.Settings.ButtonRun.WarningsCount] = 488;
            Assert.Equal(490, run.IssueCount);
            Assert.Equal(490, SiteAdaptation.Common.Steps.StepRun.From(run, null).Issues);
        }

        [Fact]
        public void R550_a_config_with_no_block_keeps_the_old_name()
        {
            Assert.Null(ReportFile.Fields(new JObject(), null));
            Assert.Equal("012_Report.xlsx", ReportFile.Name("012", null));
        }

        [Fact]
        public void R550_a_field_the_user_set_goes_over_config_and_the_note_is_no_field()
        {
            SortedDictionary<string, string> fields = ReportFile.Fields(Config(), new Dictionary<string, string> { ["originator"] = "ABC" });
            Assert.Equal("00101-ABC-013-XXX-SCH-TU-090000.xlsx", ReportFile.Name("013", fields));
            Assert.False(fields.ContainsKey("note"));
        }

        [Fact]
        public void R550_a_missing_field_is_refused_by_its_name()
        {
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => ReportFile.Fields(Config("role"), null));
            Assert.Contains("role", refused.Message);
        }

        [Fact]
        public void R550_a_field_that_makes_no_file_name_is_refused()
        {
            SortedDictionary<string, string> fields = ReportFile.Fields(Config(), new Dictionary<string, string> { ["level"] = "a/b" });
            Assert.Throws<InvalidDataException>(() => ReportFile.Name("012", fields));
        }

        [Fact]
        public void Q307_the_model_read_gives_each_wall_its_ids_apart_from_the_plot_rows()
        {
            var elements = new List<KeyValuePair<long, PlacedTag>>
            {
                Tagged(11, "101", PlacedKinds.Link, ""),
                Tagged(21, "101", PlacedKinds.BoundaryWall, "101+102|e1|p1"),
                Tagged(22, "101", PlacedKinds.BoundaryWall, "101+102|e1|p2"),
            };
            JObject counts = RevitCounts.FromElements(elements, new string[0], null);
            Assert.Equal(new long[] { 21 }, RevitCounts.WallIds(counts, "101", "101+102|e1|p1"));
            Assert.Equal(new long[] { 22 }, RevitCounts.WallIds(counts, "101", "101+102|e1|p2"));
            Assert.Empty(RevitCounts.WallIds(counts, "101", "none"));
            Assert.Empty(RevitCounts.WallIds(null, "101", "101+102|e1|p1"));
            Assert.Equal(1L, (long)counts["plots"]["101"]["links"]);
        }

        private static KeyValuePair<long, PlacedTag> Tagged(long id, string plot, string kind, string part)
        {
            return new KeyValuePair<long, PlacedTag>(id, new PlacedTag(1, new TagKey(plot, kind, part), "run", "profile", id.ToString(), new PlacedValues(new Dictionary<string, double[]>())));
        }
    }
}
