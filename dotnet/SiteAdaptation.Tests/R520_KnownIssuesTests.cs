using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R520, R521, round KNOWN-3NOV-A. Made up names, no real plot or level
    public sealed class R520_KnownIssuesTests
    {
        private static SavedStart Saved(string name, ProjectSettings job)
        {
            return new SavedStart(name, name + ".json", PreSetting.Export(job));
        }

        [Fact]
        public void R520_the_saved_project_of_a_setup_is_the_one_of_its_config_rules_and_level_profile()
        {
            var job = new ProjectSettings { ConfigFile = @"Y:\job\project.json", RulesFile = @"Y:\job\rules.json", LevelProfile = "made" };
            var other = new ProjectSettings { ConfigFile = @"Y:\other\project.json", RulesFile = @"Y:\job\rules.json", LevelProfile = "made" };
            SavedStart[] starts = { Saved("Other", other), Saved("Job", job) };

            Assert.Equal("Job", SavedStarts.CameFrom(job, starts).Name);
            Assert.Equal("Job", SavedStarts.CameFrom(new ProjectSettings { ConfigFile = @"y:\JOB\project.json", RulesFile = @"Y:\job\RULES.json", LevelProfile = "made" }, starts).Name);
        }

        [Fact]
        public void R520_a_setup_of_another_config_rules_or_level_profile_or_of_no_config_came_from_none()
        {
            SavedStart[] starts = { Saved("Job", new ProjectSettings { ConfigFile = @"Y:\job\project.json", RulesFile = @"Y:\job\rules.json", LevelProfile = "made" }) };

            Assert.Null(SavedStarts.CameFrom(new ProjectSettings { ConfigFile = @"Y:\copy\project.json", RulesFile = @"Y:\job\rules.json", LevelProfile = "made" }, starts));
            Assert.Null(SavedStarts.CameFrom(new ProjectSettings { ConfigFile = @"Y:\job\project.json", RulesFile = @"Y:\other\rules.json", LevelProfile = "made" }, starts));
            Assert.Null(SavedStarts.CameFrom(new ProjectSettings { ConfigFile = @"Y:\job\project.json", RulesFile = @"Y:\job\rules.json", LevelProfile = "other" }, starts));
            Assert.Null(SavedStarts.CameFrom(new ProjectSettings(), new[] { Saved("Blank", new ProjectSettings()) }));
            Assert.Null(SavedStarts.CameFrom(new ProjectSettings { ConfigFile = @"Y:\job\project.json" }, new SavedStart[0]));
        }

        private static JObject Face(string located, string official)
        {
            return new JObject { ["baseplanLabel"] = "F" + located, ["nbhId"] = "012", ["locationPlanNumber"] = located, ["officialNumber"] = official, ["typologyRev9"] = "VL4" };
        }

        [Fact]
        public void R521_review_takes_the_official_number_where_the_location_plan_gives_two()
        {
            var table = new JObject { ["a"] = Face("216|2312", "2312") };
            Assert.Equal(new[] { "2312" }, ReviewTable.Rows(table, "012", null, null, null).Select(row => row.PlotNo));
        }

        [Fact]
        public void R521_a_plot_of_one_number_keeps_it_and_two_numbers_with_no_official_stay_as_they_read()
        {
            var table = new JObject { ["a"] = Face("216", "2312"), ["b"] = Face("7|8", null) };
            Assert.Equal(new[] { "216", "7|8" }, ReviewTable.Rows(table, "012", null, null, null).Select(row => row.PlotNo));
        }

        [Fact]
        public void R521_a_mark_saved_under_the_old_number_stays_on_its_plot_and_is_saved_under_the_new()
        {
            IReadOnlyList<ReviewRow> rows = ReviewTable.Rows(new JObject { ["a"] = Face("216|2312", "2312") }, "012", null, null, null);
            var saved = new[] { new ReviewCheck { Plot = "216|2312", Check = ReviewCheckValue.Ok, RunId = "run", PlotTableHash = "hash" } };

            var state = new ReviewState(rows, saved, "run", "hash");

            Assert.Equal(1, state.CheckedCount);
            Assert.Equal(new[] { "2312" }, state.ToChecks().Select(check => check.Plot));
        }

        [Fact]
        public void R521_a_mark_under_the_new_number_wins_over_one_under_the_old()
        {
            IReadOnlyList<ReviewRow> rows = ReviewTable.Rows(new JObject { ["a"] = Face("216|2312", "2312") }, "012", null, null, null);
            var saved = new[] { new ReviewCheck { Plot = "216|2312", Check = ReviewCheckValue.Ok, RunId = "run", PlotTableHash = "hash" }, new ReviewCheck { Plot = "2312", Check = ReviewCheckValue.Wrong, RunId = "run", PlotTableHash = "hash" } };

            var state = new ReviewState(rows, saved, "run", "hash");

            Assert.Equal(new[] { ReviewCheckValue.Wrong }, state.ToChecks().Select(check => check.Check));
        }

        [Fact]
        public void R521_a_wrong_on_a_plot_of_two_numbers_reaches_check()
        {
            ReviewRow row = ReviewTable.Rows(new JObject { ["a"] = Face("216|2312", "2312") }, "012", null, null, null).Single();
            JObject step = R376_CheckTableTests.Wrapped(JObject.Parse("{ 'plots': { '2312': { 'unit': 'VL4', 'nbh': '012', 'rows': [] } }, 'tables': { 'assumed': { }, 'chips': { } } }"));
            var wrong = new[] { new ReviewCheck { Plot = row.PlotNo, Check = ReviewCheckValue.Wrong, Note = "made up" } };

            CheckTableView view = CheckTableBuild.Compose("012", step, R376_CheckTableTests.Block(), wrong, null, null, true);

            Assert.Equal(new[] { "2312" }, view.Rows.Where(item => item.Check == CheckTableBuild.TeamCheck).Select(item => item.Plot));
        }

        private static JObject Config()
        {
            return new JObject
            {
                ["majority"] = new JObject
                {
                    ["groups"] = new JObject { ["native"] = new JArray("n1", "n2"), ["parsons"] = new JArray("p1") },
                    ["overrideDefaultSources"] = new JArray("parsons"),
                },
            };
        }

        private static JObject HandStep()
        {
            const string row = "{ 'check': 'Hand', 'field': 'mirrored', 'from': 'missing', 'use': 'Rule', 'rule': null, 'toolValue': null, 'past': { 'writes': 'trueOrFalse', 'value': null, 'why': null }, 'why': 'no link' }";
            return R376_CheckTableTests.Wrapped(JObject.Parse("{ 'plots': { '1': { 'unit': 'VL4', 'nbh': '012', 'rows': [ " + row + " ] }, '2': { 'unit': 'VL4', 'nbh': '012', 'rows': [ " + row + " ] } }, 'tables': { 'assumed': { }, 'chips': { } } }"));
        }

        [Fact]
        public void R482_a_front_edge_override_reads_front_edge_by_hand_and_edge_n_of_the_plot_outline()
        {
            Assert.Equal("Front edge by hand", CheckShown.RuleName("parsons frontEdge"));
            Assert.Equal("edge 4 of the plot outline", CheckShown.Past(JToken.Parse("{\"edge\":3}")));
        }
    }
}
