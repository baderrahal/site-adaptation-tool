using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.CheckTable;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R376. What the check table window decides apart from the window: the chips, the find box, the Use of each row, what changed and
    // the manual rule lookup by check type and field. Common, so it runs on net48 and net8.0
    public sealed class R376_CheckTableStateTests
    {
        private static CheckTableView View()
        {
            return CheckTableBuild.Compose("012", R376_CheckTableTests.Step(), R376_CheckTableTests.Block(), null, R376_CheckTableTests.Past(), (check, field) => check == "Hand" ? "M01" : null, true);
        }

        [Fact]
        public void R376_the_chip_All_lists_every_row_a_check_type_chip_its_rows_and_Assumed_no_row_and_the_find_box_narrows_by_plot()
        {
            var state = new CheckTableState(View());

            Assert.Equal(View().Rows.Count, state.Filter("All", "").Count);
            Assert.Equal(new[] { "Hand", "Hand" }, state.Filter("Hand", "").Select(row => row.Check));
            Assert.Empty(state.Filter("Assumed", ""));
            Assert.Equal(new[] { "7" }, state.Filter("All", "7").Select(row => row.Plot));
            Assert.Equal(2, state.CountOf("Hand"));
            Assert.Equal(0, state.CountOf("no such chip"));
        }

        [Fact]
        public void R376_a_row_opens_on_its_use_past_is_refused_where_it_is_not_offered_and_only_a_changed_row_is_a_choice()
        {
            var state = new CheckTableState(View());
            CheckRow level = state.Rows.Single(row => row.Check == "Level");
            CheckRow hand = state.Rows.First(row => row.Check == "Hand" && !row.PastOffered);

            Assert.Equal("Past", state.UseOf(level.Index));
            Assert.False(state.Changed);
            Assert.Throws<ArgumentException>(() => { state.Set(hand.Index, CheckUse.Past); });
            Assert.Throws<ArgumentException>(() => { state.Set(hand.Index, "Keep"); });
            Assert.Equal(hand.PastWhy, state.PastTip(hand));
            Assert.Null(state.PastTip(level));

            state.Set(hand.Index, CheckUse.Skip);
            state.Set(level.Index, CheckUse.Rule);
            state.Set(level.Index, CheckUse.Past);

            Assert.True(state.Changed);
            Assert.Equal(new[] { hand.Index + "|Skip" }, state.Choices().Select(choice => choice.Index + "|" + choice.Use));
            state.Set(hand.Index, hand.OpenUse);
            Assert.False(state.Changed);
            Assert.Empty(state.Choices());
        }

        [Fact]
        public void R376_after_a_save_nothing_is_changed_until_the_next_edit_and_the_save_keeps_the_choices_against_the_opening_use()
        {
            var state = new CheckTableState(View());
            CheckRow hand = state.Rows.First(row => row.Check == "Hand");
            state.Set(hand.Index, CheckUse.Skip);

            state.MarkSaved();

            Assert.False(state.Changed);
            Assert.Equal(1, state.Choices().Count);
            state.Set(hand.Index, CheckUse.Rule);
            Assert.True(state.Changed);
        }

        [Fact]
        public void R376_the_use_a_row_would_get_is_its_value_for_Rule_Past_and_Skip_and_the_callback_overrides_it()
        {
            CheckRow level = new CheckTableState(View()).Rows.Single(row => row.Check == "Level");

            Assert.Equal("0.2", CheckTableState.Gives(level, CheckUse.Rule, null));
            Assert.Equal("{\"gapToRuleM\":0.05}", CheckTableState.Gives(level, CheckUse.Past, null));
            Assert.Equal("", CheckTableState.Gives(level, CheckUse.Skip, null));
            Assert.Equal("given Past", CheckTableState.Gives(level, CheckUse.Past, (row, use) => "given " + use));
            Assert.Equal("", CheckTableState.Gives(new CheckRow(), CheckUse.Rule, null));
        }

        [Fact]
        public void R376_the_info_line_counts_the_overrides_of_the_list_and_those_on_the_nbh()
        {
            var overrides = Newtonsoft.Json.Linq.JObject.Parse("{ 'overrides': [ { 'plot': '1', 'field': 'heightM' }, { 'plot': '9', 'field': 'heightM' }, { 'plot': '2', 'field': 'entryM' } ] }");

            CheckTableInfo info = CheckTableInfo.Of("parsons2024Rev6", "list.json", overrides, R376_CheckTableTests.Step(), "012");

            Assert.Equal("parsons2024Rev6", info.LevelProfile);
            Assert.Equal("list.json", info.OverrideFile);
            Assert.Equal(3, info.Overrides);
            Assert.Equal(2, info.OnNbh);
            Assert.Equal(3, CheckTableInfo.Of("p", "l", overrides, R376_CheckTableTests.Step(), null).OnNbh);
            Assert.Equal(0, CheckTableInfo.Of("p", "l", null, R376_CheckTableTests.Step(), "012").Overrides);
        }

        [Fact]
        public void R376_the_manual_rule_of_a_row_comes_from_the_checks_key_of_each_rule_in_the_profile_by_check_type_then_by_type_and_field()
        {
            var profile = Newtonsoft.Json.Linq.JObject.Parse(@"{ 'manualRules': { 'manual': { 'name': 'n', 'revision': 'r', 'olderRevision': 'o', 'number': '1', 'file': 'f', 'folders': [] }, 'rules': [
  { 'id': 'M01', 'text': 't', 'group': 'g', 'manualRef': 'm', 'sectionPage': 's', 'pastJob': 'p', 'pastJobState': 'matches', 'checks': ['Hand', 'Level:steps'] },
  { 'id': 'M02', 'text': 't', 'group': 'g', 'manualRef': 'm', 'sectionPage': 's', 'pastJob': 'p', 'pastJobState': 'matches', 'checks': ['Level'] },
  { 'id': 'M03', 'text': 't', 'group': 'g', 'manualRef': 'm', 'sectionPage': 's', 'pastJob': 'p', 'pastJobState': 'matches' } ] } }");
            var block = SiteAdaptation.Common.ManualRules.ManualRuleBlock.Parse(profile);

            Assert.Equal("M01", block.IdOfCheck("Hand", "mirrored"));
            Assert.Equal("M01", block.IdOfCheck("Hand", null));
            Assert.Equal("M01", block.IdOfCheck("Level", "steps"));
            Assert.Equal("M02", block.IdOfCheck("Level", "heightM"));
            Assert.Null(block.IdOfCheck("Floor", "rampType"));
            Assert.Null(block.IdOfCheck(null, null));

            profile["manualRules"]["rules"][2]["checks"] = new Newtonsoft.Json.Linq.JArray("Hand");
            Assert.Contains("Hand", Assert.Throws<System.IO.InvalidDataException>(() => SiteAdaptation.Common.ManualRules.ManualRuleBlock.Parse(profile)).Message);
        }

        [Fact]
        public void R376_the_rules_profile_of_the_repo_maps_each_check_type_of_the_table_to_one_manual_rule_that_names_4_Check()
        {
            var block = SiteAdaptation.Common.ManualRules.ManualRuleBlock.FromFile(System.IO.Path.Combine(ThemeGateTests.RepoRoot(), "rules", "roshn-makkah.json"));

            foreach (string check in new[] { "Hand", "Unit code", "Floor", "Position", CheckTableBuild.TeamCheck })
            {
                string id = block.IdOfCheck(check, null);
                Assert.NotNull(id);
                Assert.Contains("4 Check", block.Find(id).CheckedIn);
            }
            Assert.Equal("M10", block.IdOfCheck("Level", "steps"));
            // The other fields of Level and Plot cut have no rule of 4 Check in the manual, so their rows show no id
            Assert.Null(block.IdOfCheck("Level", "heightM"));
            Assert.Null(block.IdOfCheck("Plot cut", null));
        }
    }
}
