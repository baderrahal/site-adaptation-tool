using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Review;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R376, Q117. The rows of the check table of button 4 from a made up checkRows step, no real plot, level or coordinate
    public sealed class R376_CheckTableTests
    {
        internal static JObject Block()
        {
            return JObject.Parse(@"{
  'types': ['Plot cut', 'Floor', 'Hand', 'Unit code', 'Position', 'Level'],
  'fields': { 'prototypeFile': { 'check': 'Unit code', 'past': null, 'why': 'no field gives a prototype file' } }
}");
        }

        internal static JObject Step()
        {
            return JObject.Parse(@"{
  'plots': {
    '1': { 'unit': 'VL4', 'nbh': '012', 'rows': [
      { 'check': 'Level', 'field': 'heightM', 'from': 'override', 'use': 'Past', 'rule': 'R278', 'toolValue': 0.2, 'past': { 'writes': 'gapToRuleM', 'value': { 'gapToRuleM': 0.05 }, 'why': null }, 'why': null },
      { 'check': 'Hand', 'field': 'mirrored', 'from': 'missing', 'use': 'Rule', 'rule': null, 'toolValue': null, 'past': { 'writes': 'trueOrFalse', 'value': null, 'why': null }, 'why': 'no link' } ] },
    '2': { 'unit': 'TH2', 'nbh': '012', 'rows': [] },
    '3': { 'unit': 'VL4', 'nbh': '012', 'rows': [] },
    '7': { 'unit': 'VL4', 'nbh': '012', 'rows': [
      { 'check': 'Hand', 'field': 'mirrored', 'from': 'missing', 'use': 'Rule', 'rule': null, 'toolValue': null, 'past': { 'writes': 'trueOrFalse', 'value': null, 'why': null }, 'why': 'no link' } ] },
    '9': { 'unit': 'VL4', 'nbh': '013', 'rows': [] }
  },
  'tables': { 'assumed': { }, 'chips': { 'All': 3, 'followEveryRule': 3 } }
}");
        }

        internal static PastJob Past()
        {
            return new PastJob
            {
                KeyJoin = JObject.Parse("{ 'keys': { 'KX': { 'job': 'kx', 'join': { 'plots': { '2': { 'status': 'cut', 'face': null, 'number': null }, '1': { 'status': 'joined' }, '9': { 'status': 'cut' } } } } } }"),
                Links = new Dictionary<string, KeyLink>
                {
                    ["1"] = new KeyLink { Code = "KX", Model = "kx-site.rvt", File = "P-ARC-VL4-A.rvt", Mirrored = true },
                    ["3"] = new KeyLink { Code = "KX", Model = "kx-site.rvt", File = "P-ARC-TH2-A.rvt", Mirrored = false },
                },
            };
        }

        [Fact]
        public void R376_the_rows_of_one_nbh_open_on_past_for_an_applied_override_and_list_a_plot_marked_wrong_as_team_check()
        {
            var wrong = new[] { new ReviewCheck { Plot = "3", Check = ReviewCheckValue.Wrong, Note = "the face is cut" } };

            CheckTableView view = CheckTableBuild.Compose("012", Step(), Block(), wrong, null, null, true);

            Assert.Equal("4 Check, NBH 012, 4 plots", view.Title);
            Assert.Equal(new[] { "1|Hand|Rule", "1|Level|Past", "3|Team check|Rule", "7|Hand|Rule" }, view.Rows.Select(row => row.Plot + "|" + row.Check + "|" + row.OpenUse));
            Assert.Equal(Enumerable.Range(0, 4), view.Rows.Select(row => row.Index));
            Assert.Contains("the face is cut", view.Rows.Single(row => row.Check == CheckTableBuild.TeamCheck).PastWhy);
            Assert.False(view.Rows.Single(row => row.Check == CheckTableBuild.TeamCheck).PastOffered);
            Assert.True(view.Rows.Single(row => row.Check == "Level").PastOffered);
            // Until the lookup of the manual rules is wired a row shows no id
            Assert.All(view.Rows, row => Assert.Null(row.ManualRuleId));
            // Plots 2 and 3 had no row of the build, 3 has the team check now
            Assert.Equal(1, view.FollowEveryRule);
            Assert.Equal("1 plots follow every rule", view.FollowLine);
            Assert.Equal(new[] { "All 4", "Plot cut 0", "Floor 0", "Hand 2", "Unit code 0", "Position 0", "Level 1", "Team check 1", "Assumed 0" }, view.Counts.Select(item => item.Key + " " + item.Value));
            Assert.True(view.SaveAllowed);
        }

        [Fact]
        public void R376_every_row_names_its_manual_rule_through_the_seam_by_check_type_and_field()
        {
            Func<string, string, string> lookup = (check, field) => check == "Hand" ? "M07" : check == CheckTableBuild.TeamCheck ? "M01" : null;
            var wrong = new[] { new ReviewCheck { Plot = "3", Check = ReviewCheckValue.Wrong, Note = "" } };

            CheckTableView view = CheckTableBuild.Compose("012", Step(), Block(), wrong, null, lookup, false);

            Assert.Equal(new[] { "M07", null, "M01", "M07" }, view.Rows.Select(row => row.ManualRuleId));
            Assert.False(view.SaveAllowed);
        }

        [Fact]
        public void R376_Q117_with_no_past_job_plot_cut_shows_no_rows_and_one_line_says_so_and_no_key_holds_a_plot()
        {
            CheckTableView view = CheckTableBuild.Compose("012", Step(), Block(), null, null, null, true);

            Assert.DoesNotContain(view.Rows, row => row.Check == "Plot cut");
            Assert.Equal(CheckTableBuild.NoPastJob, view.NoPastJobLine);
            CheckRow missing = view.Rows.First(row => row.From == "missing");
            Assert.False(missing.PastOffered);
            Assert.Equal("no answer key holds plot 1", missing.PastWhy);
        }

        [Fact]
        public void R376_Q117_with_the_past_job_the_plot_cut_and_key_unit_code_rows_are_built_and_past_is_filled_from_the_key()
        {
            CheckTableView view = CheckTableBuild.Compose("012", Step(), Block(), null, Past(), null, true);

            Assert.Null(view.NoPastJobLine);
            // Plot 9 is cut in the key but of NBH 013, so the table of 012 does not list it
            CheckRow cut = view.Rows.Single(row => row.Check == "Plot cut");
            Assert.Equal("2", cut.Plot);
            Assert.False(cut.PastOffered);
            Assert.Contains("cut", cut.Why.ToString());
            CheckRow unit = view.Rows.Single(row => row.Check == "Unit code");
            Assert.Equal("3", unit.Plot);
            Assert.Equal("no field gives a prototype file", unit.PastWhy);
            // The missing hand row of plot 1 takes its past value from the link of the key, plot 7 no key holds
            CheckRow hand = view.Rows.Single(row => row.Plot == "1" && row.Check == "Hand");
            Assert.True(hand.PastOffered);
            Assert.Equal(true, hand.PastValue.Value<bool>());
            Assert.Equal("KX", hand.Key);
            Assert.Equal("kx-site.rvt", hand.Model);
            CheckRow seven = view.Rows.Single(row => row.Plot == "7");
            Assert.False(seven.PastOffered);
            Assert.Equal("no answer key holds plot 7", seven.PastWhy);
            Assert.Equal(new[] { "1", "1", "2", "3", "7" }, view.Rows.Select(row => row.Plot));
        }

        [Fact]
        public void R376_the_whole_project_is_one_table_with_nbh_all_in_its_title()
        {
            CheckTableView view = CheckTableBuild.Compose(null, Step(), Block(), null, null, null, true);

            Assert.Equal("4 Check, NBH all, 5 plots", view.Title);
        }
    }
}
