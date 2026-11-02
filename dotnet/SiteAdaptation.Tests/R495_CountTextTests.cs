using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R495. The word plot follows the count in the texts of the 4 Check window. Common only, so it runs on net48 and net8.0
    public sealed class R495_CountTextTests
    {
        [Fact]
        public void R495_a_count_of_one_in_a_filled_text_reads_singular_and_11_stays_plural()
        {
            Assert.Equal("a, 1 override on b", CountText.Singular("a, 1 overrides on b", "override"));
            Assert.Equal("11 overrides", CountText.Singular("11 overrides", "override"));
            Assert.Equal("2,001 overrides", CountText.Singular("2,001 overrides", "override"));
            Assert.Equal("1 issue", CountText.Singular("1 issues", "issue"));
        }

        [Fact]
        public void R495_a_count_over_a_thousand_is_parted_as_the_other_lines_of_the_window()
        {
            Assert.Equal("2,964 plots", CountText.Plots(2964));
            Assert.Equal("1,110 plots follow every rule", CountText.FollowEveryRule(1110));
        }

        [Theory]
        [InlineData(0, "0 plots")]
        [InlineData(1, "1 plot")]
        [InlineData(2, "2 plots")]
        [InlineData(1110, "1110 plots")]
        public void R495_the_word_plot_is_plural_by_count_except_for_one(int count, string expected)
        {
            Assert.Equal(expected, CountText.Plots(count));
        }

        [Fact]
        public void R495_the_thousands_form_parts_the_count_by_commas_as_the_door_level_row_writes_it()
        {
            Assert.Equal("1,234 plots", CountText.Plots(1234));
            Assert.Equal("1 plot", CountText.Plots(1));
        }

        [Fact]
        public void R495_the_title_and_the_follow_line_of_a_table_of_one_plot_read_1_plot()
        {
            JObject step = R376_CheckTableTests.Wrapped(JObject.Parse("{'plots': {'2291': {'unit': 'VL4', 'nbh': '012', 'rows': []}}, 'tables': {'assumed': {}, 'chips': {'All': 0, 'followEveryRule': 1}}}"));
            CheckTableView view = CheckTableBuild.Compose("012", step, new JObject(), null, null, null, true);

            Assert.Equal("4 Check, NBH 012, 1 plot", view.Title);
            Assert.Equal("1 plot follows every rule", view.FollowLine);
        }

        [Fact]
        public void R495_the_door_level_row_names_one_plot_in_the_singular()
        {
            Assert.Equal("Civil entry file (1 plot)", DoorLevelPick.Label(DoorLevelSources.CivilFile, 1));
            Assert.Equal("Road level plus slope (2 plots)", DoorLevelPick.Label(DoorLevelSources.RoadPlusSlope, 2));
        }
    }
}
