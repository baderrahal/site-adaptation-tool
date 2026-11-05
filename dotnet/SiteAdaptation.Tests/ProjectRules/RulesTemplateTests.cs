using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace SiteAdaptation.Tests.ProjectRules
{
    // R660. The shipped templates and the config lines that go with them. The block manualRules is read here by the gate alone, a run never reads it
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

        private static JObject Block()
        {
            return (JObject)Read("rules", "roshn-makkah.json")["manualRules"];
        }

        private static JObject Manual()
        {
            return Read("rules", "templates", "manual.json");
        }

        [Fact]
        public void R660_every_text_of_the_manual_template_equals_the_block_word_for_word_as_a_rule_value()
        {
            JObject[] rows = Rows(Manual());
            JObject[] block = Rows(Block());
            Assert.Equal(15, rows.Length);
            Assert.Equal(block.Select(rule => (string)rule["id"]), rows.Select(rule => (string)rule["id"]));
            Assert.Equal(block.Select(rule => (string)rule["text"]), rows.Select(rule => (string)rule["value"]));
            Assert.All(rows, rule => Assert.Equal("rule", (string)rule["source"]));
        }

        [Fact]
        public void R660_the_design_table_holds_the_same_text_as_the_template()
        {
            string design = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "15-manual-rules.md"));
            foreach (JObject rule in Rows(Manual()))
            {
                Match row = Regex.Match(design, "^\\| " + (string)rule["id"] + " \\| ([^|]+) \\|", RegexOptions.Multiline);
                Assert.True(row.Success, (string)rule["id"]);
                Assert.Equal((string)rule["value"], row.Groups[1].Value.Trim());
            }
        }

        [Fact]
        public void R660_limits_copy_the_values_of_the_block_and_a_rule_with_none_has_no_field()
        {
            foreach (JObject source in Rows(Block()))
            {
                JObject row = Rows(Manual()).Single(rule => (string)rule["id"] == (string)source["id"]);
                JArray values = (JArray)source["values"];
                if (values.Count == 0)
                {
                    Assert.Null(row["limits"]);
                }
                else
                {
                    Assert.True(JToken.DeepEquals(values, row["limits"]), (string)source["id"]);
                }
            }
            Assert.Null(Rows(Manual()).Single(rule => (string)rule["id"] == "M01")["limits"]);
            Assert.Null(Rows(Manual()).Single(rule => (string)rule["id"] == "M04")["limits"]);
        }

        [Fact]
        public void R660_only_M14_and_M15_carry_pdfDiffers_naming_their_question()
        {
            JObject[] rows = Rows(Manual());
            Assert.Equal(new[] { "M14", "M15" }, rows.Where(rule => rule["pdfDiffers"] != null).Select(rule => (string)rule["id"]));
            Assert.Contains("Q149", (string)rows.Single(rule => (string)rule["id"] == "M14")["pdfDiffers"]);
            Assert.Contains("Q150", (string)rows.Single(rule => (string)rule["id"] == "M15")["pdfDiffers"]);
        }

        [Fact]
        public void R660_no_word_of_the_column_pastJob_is_in_the_manual_template()
        {
            string text = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "rules", "templates", "manual.json"));
            Assert.DoesNotContain("pastJob", text);
            Assert.DoesNotContain("past", text, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("keyRules", text);
            Assert.DoesNotContain("measured", text, System.StringComparison.OrdinalIgnoreCase);
            foreach (JObject rule in Rows(Block()))
            {
                string column = (string)rule["pastJob"];
                Assert.False(string.IsNullOrEmpty(column));
                Assert.DoesNotContain(column, text);
            }
        }

        [Fact]
        public void R660_the_template_names_its_manual_with_its_number_and_revision_and_config_gives_the_same_words()
        {
            JObject manual = (JObject)Block()["manual"];
            string name = (string)Manual()["manual"];
            Assert.Contains((string)manual["number"], name);
            Assert.Contains((string)manual["revision"], name);
            Assert.Contains("Rev 06", name);
            Assert.Equal(name, (string)Read("config", "project.json")["projectRules"]["manualName"]);
        }

        [Fact]
        public void R660_the_name_of_a_rule_carries_no_value()
        {
            Assert.All(Rows(Manual()), rule => Assert.DoesNotContain(((string)rule["name"]).ToCharArray(), char.IsDigit));
        }

        [Fact]
        public void R664_the_rules_a_build_needs_are_the_rules_of_the_manual_template()
        {
            JArray ids = (JArray)Read("config", "project.json")["projectRules"]["ruleIds"];
            Assert.Equal(Rows(Manual()).Select(rule => (string)rule["id"]), ids.Select(id => (string)id));
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
