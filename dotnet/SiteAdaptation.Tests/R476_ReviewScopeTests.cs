using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Review;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R476. 3 Review data lists the plots 4 Check builds. The hand run of NBH 012 listed 379 rows against 337 of Check, the 42 are land with
    // a location plan number and no official number and no villa type
    public sealed class R476_ReviewScopeTests
    {
        private static JObject Plot(string label, string number, string official, string client)
        {
            return new JObject { ["baseplanLabel"] = label, ["nbhId"] = "012", ["locationPlanNumber"] = number, ["officialNumber"] = official, ["clientType"] = client };
        }

        [Fact]
        public void R476_a_plot_with_no_official_number_is_left_out_and_a_unit_plot_stays()
        {
            var table = new JObject { ["2289"] = Plot("2289", "1", "2289", "VL4"), ["12"] = Plot("12", "7", null, "NCC"), ["21"] = Plot("21", "25", null, "MIXEDUSE COMMERCIAL") };
            Assert.Equal(new[] { "2289" }, ReviewTable.Rows(table, "012", null, null, null).Select(row => row.CadLabel));
        }

        [Fact]
        public void R476_a_table_with_no_field_official_number_keeps_every_row()
        {
            var table = new JObject { ["5"] = new JObject { ["baseplanLabel"] = "5", ["nbhId"] = "012", ["locationPlanNumber"] = "5" } };
            Assert.Single(ReviewTable.Rows(table, "012", null, null, null));
        }
    }
}
