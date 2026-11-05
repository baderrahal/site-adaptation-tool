using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace SiteAdaptation.Tests.ProjectRules
{
    // R660. The shipped templates and the config lines that go with them
    public sealed class RulesTemplateTests
    {
        private static JObject Read(params string[] parts)
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(new[] { ThemeGateTests.RepoRoot() }.Concat(parts).ToArray())));
        }

        private static JObject[] Rows(JObject file)
        {
            return ((JArray)file["rules"]).OfType<JObject>().ToArray();
        }

        [Fact]
        public void R660_the_manual_template_holds_every_rule_of_the_block_with_no_value_and_marked()
        {
            JObject[] rows = Rows(Read("rules", "templates", "manual.json"));
            string[] block = Rows((JObject)Read("rules", "roshn-makkah.json")["manualRules"]).Select(rule => (string)rule["id"]).ToArray();
            Assert.Equal(block, rows.Select(rule => (string)rule["id"]));
            Assert.All(rows, rule =>
            {
                Assert.Equal(JTokenType.Null, rule["value"].Type);
                Assert.Equal("missing", (string)rule["source"]);
                Assert.False(string.IsNullOrEmpty((string)rule["marked"]));
            });
        }

        [Fact]
        public void R660_no_name_of_the_manual_template_carries_a_value()
        {
            Assert.All(Rows(Read("rules", "templates", "manual.json")), rule => Assert.DoesNotContain(((string)rule["name"]).ToCharArray(), char.IsDigit));
        }

        [Fact]
        public void R664_the_rules_a_build_needs_are_the_rules_of_the_manual_template()
        {
            JArray ids = (JArray)Read("config", "project.json")["projectRules"]["ruleIds"];
            Assert.Equal(Rows(Read("rules", "templates", "manual.json")).Select(rule => (string)rule["id"]), ids.Select(id => (string)id));
        }

        [Fact]
        public void R660_the_past_template_is_all_past_and_each_row_is_the_measure_of_the_profile()
        {
            JObject[] rows = Rows(Read("rules", "templates", "past-job.json"));
            JObject keyRules = (JObject)Read("rules", "roshn-makkah.json")["levelProfiles"]["parsons2024Rev6"]["keyRules"];
            JProperty[] measures = keyRules.Properties().Where(item => item.Value is JObject).ToArray();
            Assert.Equal(measures.Length, rows.Length);
            Assert.All(rows, rule => Assert.Equal("past", (string)rule["source"]));
            foreach (JProperty measure in measures)
            {
                JObject row = rows.Single(rule => (string)rule["id"] == "past." + measure.Name);
                Assert.Equal((string)measure.Value["rule"], (string)row["value"]);
                Assert.True(JToken.DeepEquals(measure.Value["share"] ?? JValue.CreateNull(), row["share"]), measure.Name);
                Assert.True(JToken.DeepEquals(measure.Value["plots"] ?? JValue.CreateNull(), row["plots"]), measure.Name);
            }
        }

        [Fact]
        public void R660_the_past_job_mode_of_the_rules_is_a_mode_of_config()
        {
            JObject config = Read("config", "project.json");
            Assert.Contains((string)config["projectRules"]["pastJobMode"], ((JArray)config["designBasis"]["modes"]).Select(mode => (string)mode));
            Assert.NotEqual((string)config["projectRules"]["pastJobMode"], (string)config["designBasis"]["newProjectMode"]);
        }
    }
}
