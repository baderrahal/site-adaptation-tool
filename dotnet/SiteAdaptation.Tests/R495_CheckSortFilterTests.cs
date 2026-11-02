using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R495. Sort by a column and filter by field in the 4 Check window, apart from the window. A filter hides rows and never drops them
    // from a save, a count or Apply. Common only, so it runs on net48 and net8.0
    public sealed class R495_CheckSortFilterTests
    {
        private static CheckTableView NbhView()
        {
            var rows = new List<CheckRow>();
            for (int at = 0; at < 1110; at++)
            {
                bool fail = at < 670;
                rows.Add(new CheckRow
                {
                    Index = rows.Count, Plot = (2000 + at % 300).ToString(), Nbh = "012", Check = "Floor", Field = "rampJoint", From = "joint", OpenUse = "Rule",
                    Why = "corner 2 shared with plots 2290, gap " + (fail ? "0.012" : "0.000") + " m, " + (fail ? "Fail" : "Pass") + ", passes at most 0.001 m",
                });
            }
            for (int at = 0; at < 40; at++)
            {
                rows.Add(new CheckRow { Index = rows.Count, Plot = (3000 + at).ToString(), Nbh = "012", Check = "Hand", Field = "mirrored", From = "missing", OpenUse = "Rule", Why = "no link" });
            }
            rows.Add(new CheckRow { Index = rows.Count, Plot = "9", Nbh = "012", Check = "Team check", Field = null, From = "team", OpenUse = "Rule", Why = "front edge on the other side" });
            return new CheckTableView
            {
                Rows = rows,
                Counts = new[] { new KeyValuePair<string, int>("All", rows.Count), new KeyValuePair<string, int>("Floor", 1110), new KeyValuePair<string, int>("Hand", 40) },
            };
        }

        [Fact]
        public void R495_the_field_filter_lists_the_rows_of_one_field_and_the_sheet_numbers_hold_on_nbh_012()
        {
            var state = new CheckTableState(NbhView());

            Assert.Equal(1110, state.Filter("All", "", "rampJoint").Count);
            Assert.Equal(670, state.Filter("All", "Fail", "rampJoint").Count);
            Assert.Equal(440, state.Filter("All", "Pass", "rampJoint").Count);
            Assert.Equal(40, state.Filter("All", "", "mirrored").Count);
            Assert.Equal(1151, state.Filter("All", "", null).Count);
            Assert.Equal(1151, state.Filter("All", "", "").Count);
            Assert.Equal(40, state.Filter("Hand", "", "mirrored").Count);
            Assert.Empty(state.Filter("Hand", "", "rampJoint"));
        }

        [Fact]
        public void R495_the_field_list_names_each_field_with_its_row_count_in_name_order_and_leaves_out_a_row_of_no_field()
        {
            var state = new CheckTableState(NbhView());

            Assert.Equal(new[] { "mirrored 40", "rampJoint 1110" }, state.Fields().Select(item => item.Key + " " + item.Value));
        }

        [Fact]
        public void R495_a_number_in_the_find_box_finds_plots_and_a_word_finds_whole_words_of_the_note()
        {
            var state = new CheckTableState(NbhView());

            Assert.Equal(new[] { "9" }, state.Filter("All", "9", null).Where(row => row.Plot == "9").Select(row => row.Plot));
            Assert.DoesNotContain(state.Filter("All", "2290", null), row => row.Plot == "9");
            Assert.Equal(40, state.Filter("All", "no link", null).Count);
            Assert.Equal(1, state.Filter("All", "FRONT edge", null).Count);
            Assert.Equal(0, state.Filter("All", "pas", "rampJoint").Count);
        }

        [Fact]
        public void R495_a_filter_hides_rows_and_never_drops_a_use_from_the_choices_the_counts_or_the_answer()
        {
            var state = new CheckTableState(NbhView());
            CheckRow hidden = state.Filter("All", "", "mirrored").First();
            state.Set(hidden.Index, CheckUse.Skip);
            List<CheckRow> shown = state.Filter("All", "", "rampJoint").ToList();

            Assert.DoesNotContain(shown, row => row.Index == hidden.Index);
            Assert.Equal(CheckUse.Skip, state.UseOf(hidden.Index));
            Assert.Equal(new[] { hidden.Index }, state.Choices().Select(choice => choice.Index));
            Assert.True(state.Changed);
            Assert.Equal(1151, state.Rows.Count);
            Assert.Equal(1110, state.CountOf("Floor"));
        }

        [Fact]
        public void R495_a_header_click_sorts_up_a_second_click_reverses_and_another_column_starts_up()
        {
            string[] plots = { "98", "2291", "12", "x7", "100" };
            CheckSort sort = CheckSort.None;
            Assert.Equal(plots, sort.Apply(plots, plot => plot));

            sort = sort.Click("plot");
            Assert.Equal("plot", sort.Column);
            Assert.False(sort.Descending);
            Assert.Equal(new[] { "12", "98", "100", "2291", "x7" }, sort.Apply(plots, plot => plot));

            sort = sort.Click("plot");
            Assert.True(sort.Descending);
            Assert.Equal(new[] { "x7", "2291", "100", "98", "12" }, sort.Apply(plots, plot => plot));

            sort = sort.Click("nbh");
            Assert.Equal("nbh", sort.Column);
            Assert.False(sort.Descending);
        }

        [Fact]
        public void R495_equal_values_keep_their_order_in_both_directions_and_a_sort_drops_no_row()
        {
            var rows = new[] { ("a", "1"), ("b", "1"), ("c", "0"), ("d", "1") };
            CheckSort up = CheckSort.None.Click("use");
            CheckSort down = up.Click("use");

            Assert.Equal(new[] { "c", "a", "b", "d" }, up.Apply(rows, row => row.Item2).Select(row => row.Item1));
            Assert.Equal(new[] { "a", "b", "d", "c" }, down.Apply(rows, row => row.Item2).Select(row => row.Item1));
            Assert.Equal(4, down.Apply(rows, row => row.Item2).Count);
        }

        [Fact]
        public void R495_numbers_sort_by_value_and_words_after_them_with_case_ignored_and_a_missing_text_first()
        {
            var sort = CheckSort.None.Click("c");
            string[] values = { "0.2", "-1", "Zed", "apple", "", "10", "none" };

            Assert.Equal(new[] { "", "-1", "0.2", "10", "apple", "none", "Zed" }, sort.Apply(values, value => value));
        }
    }
}
