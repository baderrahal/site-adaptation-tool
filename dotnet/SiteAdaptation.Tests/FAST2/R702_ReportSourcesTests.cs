using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    public class R702_ReportSourcesTests
    {
        private static JObject Config(string mode, string past = "pastMode")
        {
            var config = new JObject { ["designBasis"] = new JObject { ["mode"] = mode } };
            if (past != null)
            {
                config["projectRules"] = new JObject { ["pastJobMode"] = past };
            }
            return config;
        }

        [Fact]
        public void R702_only_a_mode_other_than_the_past_job_mode_is_a_new_project()
        {
            Assert.True(ReportSheets.NewProject(Config("newMode")));
            Assert.False(ReportSheets.NewProject(Config("pastMode")));
            // A config that names no past job mode, or no mode, keeps the shares
            Assert.False(ReportSheets.NewProject(Config("newMode", null)));
            Assert.False(ReportSheets.NewProject(new JObject { ["projectRules"] = new JObject { ["pastJobMode"] = "pastMode" } }));
            Assert.False(ReportSheets.NewProject(null));
        }

        [Fact]
        public void R703_a_row_names_each_source_once_in_the_order_of_the_tags_and_a_value_with_none_is_missing()
        {
            var computed = new JObject { ["value"] = 1.0, ["source"] = "computed" };
            var rule = new JObject { ["value"] = 2.0, ["source"] = "keyRules" };
            var person = new JObject { ["value"] = "A", ["source"] = "user pick" };
            var past = new JObject { ["value"] = 3.0, ["source"] = "pastJob" };
            var empty = new JObject { ["value"] = null };
            var other = new JObject { ["value"] = 4.0, ["source"] = "footprint" };
            Assert.Equal("computed, rule, person entered, past, missing, footprint", ReportSheets.SourcesOf(other, empty, past, person, rule, computed, computed));
            Assert.Equal("missing", ReportSheets.SourcesOf(null, empty));
            Assert.Null(ReportSheets.SourcesOf(null, new JValue(1.0)));
            // A value that holds a value but no source names nothing, the tool never guesses one
            Assert.Null(ReportSheets.SourcesOf(new JObject { ["value"] = 5.0 }));
        }
    }
}
