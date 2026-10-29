using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Scope;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R366, docs/design/03a-buttons-shared-and-1.md Scope. The scope of a button from the settings and the plot table, every stop
    // before any change with a message naming the token or the neighbourhood
    public class R366_ScopeTests
    {
        private static readonly ScopePlot[] Table =
        {
            new ScopePlot("12", "013", false),
            new ScopePlot("2290", "002", true),
            new ScopePlot("2291", "002", true),
            new ScopePlot("2293", "002", true),
            new ScopePlot("2299", "002", true),
            new ScopePlot("0042", "001", true),
            new ScopePlot("43", "001", true),
            new ScopePlot("500", "005", false),
        };

        private static SettingsScope Plots(string text)
        {
            return new SettingsScope { Kind = ScopeKinds.Plots, Plots = text };
        }

        private static SettingsScope Nbh(string nbh)
        {
            return new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = nbh };
        }

        [Fact]
        public void R366_whole_project_takes_every_unit_plot_of_the_plot_table_in_number_order()
        {
            ScopeResult result = ButtonScope.Resolve(3, new SettingsScope(), Table);

            Assert.True(result.Ok);
            Assert.Equal(ScopeKinds.Project, result.Kind);
            Assert.Equal(new[] { "42", "43", "2290", "2291", "2293", "2299" }, result.Plots);
        }

        [Fact]
        public void R366_a_neighbourhood_takes_the_unit_plots_of_its_area_key()
        {
            ScopeResult result = ButtonScope.Resolve(5, Nbh("002"), Table);

            Assert.True(result.Ok);
            Assert.Equal(new[] { "2290", "2291", "2293", "2299" }, result.Plots);
        }

        [Fact]
        public void R366_a_neighbourhood_with_no_unit_plot_stops_with_its_name()
        {
            ScopeResult result = ButtonScope.Resolve(5, Nbh("013"), Table);

            Assert.False(result.Ok);
            Assert.Empty(result.Plots);
            Assert.Equal(new[] { "Neighbourhood 013 holds no unit plot in the plot table" }, result.Stops);
        }

        [Fact]
        public void R366_an_im_letter_is_no_area_key_and_stops_as_a_neighbourhood_with_no_unit_plot()
        {
            ScopeResult result = ButtonScope.Resolve(4, Nbh("IMC"), Table);

            Assert.Equal(new[] { "Neighbourhood IMC holds no unit plot in the plot table" }, result.Stops);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public void R366_a_neighbourhood_scope_with_no_neighbourhood_chosen_stops(string nbh)
        {
            ScopeResult result = ButtonScope.Resolve(4, Nbh(nbh), Table);

            Assert.Equal(new[] { "Neighbourhood: no neighbourhood is chosen" }, result.Stops);
        }

        [Fact]
        public void R366_a_plot_list_parts_tokens_by_commas_spaces_and_line_breaks()
        {
            ScopeResult result = ButtonScope.Resolve(6, Plots("2290,2291 2293\r\n43\n2299\t42"), Table);

            Assert.True(result.Ok);
            Assert.Equal(new[] { "42", "43", "2290", "2291", "2293", "2299" }, result.Plots);
        }

        [Fact]
        public void R366_a_plot_list_drops_leading_zeros_and_counts_a_number_given_twice_once()
        {
            ScopeResult result = ButtonScope.Resolve(6, Plots("042, 42 0043 43"), Table);

            Assert.True(result.Ok);
            Assert.Equal(new[] { "42", "43" }, result.Plots);
        }

        [Fact]
        public void R366_a_range_holds_every_number_between_its_ends_the_plot_table_holds()
        {
            ScopeResult result = ButtonScope.Resolve(7, Plots("2290-2299"), Table);

            Assert.True(result.Ok);
            Assert.Equal(new[] { "2290", "2291", "2293", "2299" }, result.Plots);
        }

        [Fact]
        public void R366_ranges_that_overlap_count_each_plot_once_and_a_range_of_one_number_is_a_range()
        {
            ScopeResult result = ButtonScope.Resolve(7, Plots("2290-2293 2291-2299 0042-42"), Table);

            Assert.True(result.Ok);
            Assert.Equal(new[] { "42", "2290", "2291", "2293", "2299" }, result.Plots);
        }

        [Fact]
        public void R366_a_range_compares_numbers_by_value_not_by_text()
        {
            ScopeResult result = ButtonScope.Resolve(7, Plots("9-500"), Table);

            Assert.True(result.Ok);
            Assert.Equal(new[] { "42", "43" }, result.Plots);
        }

        [Fact]
        public void R366_a_range_written_high_to_low_stops_with_the_token()
        {
            ScopeResult result = ButtonScope.Resolve(7, Plots("2299-2290"), Table);

            Assert.False(result.Ok);
            Assert.Empty(result.Plots);
            Assert.Equal(new[] { "Plot list: 2299-2290 is a range written high to low" }, result.Stops);
        }

        [Theory]
        [InlineData("22a")]
        [InlineData("-2290")]
        [InlineData("2290-")]
        [InlineData("2290-2291-2293")]
        [InlineData("2290--2291")]
        [InlineData("-")]
        [InlineData("2290.5")]
        [InlineData("+43")]
        public void R366_a_token_that_is_no_number_and_no_range_stops_with_the_token(string token)
        {
            ScopeResult result = ButtonScope.Resolve(3, Plots("2290 " + token), Table);

            Assert.False(result.Ok);
            Assert.Empty(result.Plots);
            Assert.Equal(new[] { "Plot list: " + token + " is no number and no range" }, result.Stops);
        }

        [Fact]
        public void R366_a_number_the_plot_table_holds_no_plot_of_stops_with_the_number()
        {
            ScopeResult result = ButtonScope.Resolve(3, Plots("2290 2292"), Table);

            Assert.Equal(new[] { "Plot list: the plot table holds no plot 2292" }, result.Stops);
            Assert.Empty(result.Plots);
        }

        [Fact]
        public void R366_a_range_the_plot_table_holds_no_plot_of_stops_with_the_range()
        {
            ScopeResult result = ButtonScope.Resolve(3, Plots("2300-2400"), Table);

            Assert.Equal(new[] { "Plot list: the plot table holds no plot of 2300-2400" }, result.Stops);
        }

        [Fact]
        public void R366_a_token_whose_plots_are_no_unit_plot_stops_with_the_token()
        {
            ScopeResult result = ButtonScope.Resolve(3, Plots("500 12-13"), Table);

            Assert.Equal(new[] { "Plot list: 500 holds no unit plot in the plot table", "Plot list: 12-13 holds no unit plot in the plot table" }, result.Stops);
        }

        [Fact]
        public void R366_a_range_with_unit_and_other_plots_takes_the_unit_plots_only()
        {
            ScopeResult result = ButtonScope.Resolve(3, Plots("1-100"), Table);

            Assert.True(result.Ok);
            Assert.Equal(new[] { "42", "43" }, result.Plots);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" , \r\n ,")]
        public void R366_an_empty_plot_list_stops(string text)
        {
            ScopeResult result = ButtonScope.Resolve(2, Plots(text), Table);

            Assert.Equal(new[] { "Plot list: the list is empty" }, result.Stops);
        }

        [Fact]
        public void R366_every_bad_token_is_named_in_the_order_typed()
        {
            ScopeResult result = ButtonScope.Resolve(3, Plots("x 2299-2290 9999 43"), Table);

            Assert.Equal(new[]
            {
                "Plot list: x is no number and no range",
                "Plot list: 2299-2290 is a range written high to low",
                "Plot list: the plot table holds no plot 9999",
            }, result.Stops);
        }

        [Fact]
        public void R366_whole_project_with_no_unit_plot_stops()
        {
            ScopeResult result = ButtonScope.Resolve(3, new SettingsScope(), new[] { new ScopePlot("12", "013", false) });

            Assert.Equal(new[] { "Whole project: the plot table holds no unit plot" }, result.Stops);
        }

        [Fact]
        public void R366_button_1_always_works_on_the_whole_project_and_never_stops_on_the_scope()
        {
            ScopeResult bad = ButtonScope.Resolve(1, Plots("2299-2290 x"), Table);
            ScopeResult before = ButtonScope.Resolve(1, Nbh("013"), new ScopePlot[0]);

            Assert.True(bad.Ok);
            Assert.Equal(ScopeKinds.Project, bad.Kind);
            Assert.Equal(new[] { "42", "43", "2290", "2291", "2293", "2299" }, bad.Plots);
            Assert.True(before.Ok);
            Assert.Empty(before.Plots);
        }

        [Fact]
        public void R366_a_plot_table_number_with_leading_zeros_is_matched_by_its_whole_number()
        {
            ScopeResult result = ButtonScope.Resolve(3, Plots("42"), Table);

            Assert.Equal(new[] { "42" }, result.Plots);
        }

        [Fact]
        public void R366_the_scope_key_is_the_same_for_the_same_plots_however_typed()
        {
            Assert.Equal(ButtonScope.Key(3, Plots("2290-2299, 043 42")), ButtonScope.Key(3, Plots("42\n43 42 02290-2299")));
            Assert.NotEqual(ButtonScope.Key(3, Plots("2290-2299")), ButtonScope.Key(3, Plots("2290-2298")));
            Assert.NotEqual(ButtonScope.Key(3, Nbh("002")), ButtonScope.Key(3, Nbh("003")));
            Assert.NotEqual(ButtonScope.Key(3, new SettingsScope()), ButtonScope.Key(3, Nbh("002")));
            Assert.Equal(ButtonScope.Key(1, new SettingsScope()), ButtonScope.Key(1, Plots("43")));
        }

        [Fact]
        public void R366_a_number_check_drops_leading_zeros_and_refuses_what_is_no_whole_number()
        {
            Assert.Equal("42", ButtonScope.WholeNumber("0042"));
            Assert.Equal("0", ButtonScope.WholeNumber("000"));
            Assert.Null(ButtonScope.WholeNumber("4 2"));
            Assert.Null(ButtonScope.WholeNumber(""));
            Assert.Null(ButtonScope.WholeNumber("٤٢"));
        }

        [Fact]
        public void R366_a_scope_kind_not_of_the_list_is_refused()
        {
            Assert.Throws<System.ArgumentException>(() => ButtonScope.Resolve(3, new SettingsScope { Kind = "zone" }, Table));
        }

        // A second method: every whole number of each range counted one by one against the unit plots, on random tables and lists
        [Fact]
        public void R366_a_plot_list_gives_the_plots_a_count_of_every_number_in_its_ranges_gives()
        {
            var random = new System.Random(366);
            int matches = 0;
            for (int round = 0; round < 500; round++)
            {
                var table = Enumerable.Range(1, 40).Select(_ => new ScopePlot(random.Next(1, 300).ToString("D" + random.Next(1, 5)), "001", random.Next(4) > 0)).ToList();
                var tokens = Enumerable.Range(0, random.Next(1, 5)).Select(_ =>
                {
                    int low = random.Next(1, 300);
                    return random.Next(2) == 0 ? low.ToString() : low + "-" + (low + random.Next(0, 60));
                }).ToList();
                var units = new HashSet<int>(table.Where(plot => plot.IsUnit).Select(plot => int.Parse(plot.Number)));
                var any = new HashSet<int>(table.Select(plot => int.Parse(plot.Number)));
                var expected = new SortedSet<int>();
                bool stops = false;
                foreach (string token in tokens)
                {
                    int[] ends = token.Split('-').Select(int.Parse).ToArray();
                    var held = Enumerable.Range(ends[0], ends[ends.Length - 1] - ends[0] + 1).Where(any.Contains).ToList();
                    stops |= held.Count == 0 || !held.Any(units.Contains);
                    expected.UnionWith(held.Where(units.Contains));
                }

                ScopeResult result = ButtonScope.Resolve(3, Plots(string.Join(", ", tokens)), table);

                Assert.Equal(!stops, result.Ok);
                Assert.Equal(stops ? new string[0] : expected.Select(number => number.ToString()).ToArray(), result.Plots);
                matches++;
            }
            Assert.Equal(500, matches);
        }
    }
}
