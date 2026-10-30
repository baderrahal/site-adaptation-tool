using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R376. The plain rule name and the words of the past value in the check table. The list keeps its values as they are
    public sealed class R376_CheckShownTests
    {
        private static JObject Repo(params string[] path)
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(new[] { ThemeGateTests.RepoRoot() }.Concat(path).ToArray())));
        }

        // The decision of each entry of the override list and its cannot list, the question ids of the project file and the rule
        // names the plan check rows carry, which are all the codes a row of the table can give in its Rule column
        private static List<string> Codes()
        {
            JObject list = Repo("config", "overrides", "roshn-makkah.json");
            var codes = new List<string>();
            foreach (string part in new[] { "overrides", "cannot" })
            {
                codes.AddRange(((JArray)list[part]).Select(item => (string)item["decision"]));
            }
            codes.AddRange(((JObject)Repo("config", "project.json")["majority"]["decidedByQuestion"]).Properties().Where(item => item.Value is JObject).Select(item => item.Name));
            codes.AddRange(new[] { "ffl", "steps", "fittedOutlineRefit", "clusterSide", "linkOrigin" });
            return codes.Where(code => code != null).Distinct().ToList();
        }

        [Fact]
        public void R376_every_code_name_the_build_gives_has_a_plain_rule_name_of_at_most_six_words()
        {
            List<string> codes = Codes();

            Assert.True(codes.Count > 20, codes.Count.ToString());
            Assert.Empty(codes.Where(code => CheckShown.RuleName(code) == code));
            Assert.Empty(codes.Where(code => CheckShown.RuleName(code).Split(' ').Length > 6));
        }

        [Fact]
        public void R376_the_plain_name_is_found_by_the_code_name_with_or_without_the_source_of_the_decision()
        {
            Assert.Equal("FFL by the rule", CheckShown.RuleName("ffl"));
            Assert.Equal("FFL by the rule", CheckShown.RuleName("parsons ffl"));
            Assert.Equal("Steps by the rule", CheckShown.RuleName("steps"));
            Assert.Equal("Parameter CASE by the rule", CheckShown.RuleName("parsons parameter CASE"));
            Assert.Equal("Open question Q017", CheckShown.RuleName("Q017"));
            Assert.Equal("someCodeNoNameHolds", CheckShown.RuleName("someCodeNoNameHolds"));
            Assert.Equal("", CheckShown.RuleName(null));
        }

        [Theory]
        [InlineData("{\"gapToRuleM\":0.001}", "0.001 m above the rule")]
        [InlineData("{\"gapToRuleM\":-0.001}", "0.001 m below the rule")]
        [InlineData("{\"gapToRuleM\":0}", "on the rule")]
        [InlineData("{\"gapToRuleM\":[0.011,-0.001]}", "along the front 0.011 m, into the plot -0.001 m")]
        [InlineData("{\"gapToRuleDeg\":-2.5}", "2.5 degrees below the rule")]
        [InlineData("{\"gapToRuleDeg\":90}", "90 degrees above the rule")]
        [InlineData("true", "Mirrored")]
        [InlineData("false", "Not mirrored")]
        [InlineData("2", "2")]
        [InlineData("\"CASE D\"", "CASE D")]
        public void R376_a_past_value_is_shown_in_words_and_units(string json, string shown)
        {
            Assert.Equal(shown, CheckShown.Past(JToken.Parse(json)));
        }

        [Fact]
        public void R376_a_value_of_no_known_form_and_no_value_are_shown_as_they_are()
        {
            Assert.Equal("", CheckShown.Past(null));
            Assert.Equal("{\"other\":1}", CheckShown.Past(JToken.Parse("{\"other\":1}")));
        }

        [Fact]
        public void R376_the_value_the_override_list_keeps_is_not_changed_by_what_is_shown()
        {
            JToken value = JToken.Parse("{\"gapToRuleM\":[0.011,0.001]}");
            CheckShown.Past(value);

            Assert.Equal("{\"gapToRuleM\":[0.011,0.001]}", value.ToString(Newtonsoft.Json.Formatting.None));
        }

        [Fact]
        public void R376_the_side_box_gives_the_past_value_in_words()
        {
            var row = new CheckRow { Index = 1, PastValue = JToken.Parse("{\"gapToRuleM\":0.05}"), PastOffered = true, ToolValue = JToken.Parse("0.2") };

            Assert.Equal("0.05 m above the rule", CheckTableState.Gives(row, CheckUse.Past, null));
            Assert.Equal("0.2", CheckTableState.Gives(row, CheckUse.Rule, null));
        }
    }
}
