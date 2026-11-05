using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;
using Xunit;

namespace SiteAdaptation.Tests.ProjectRules
{
    // R664, what siteBuild of R2-C2-build-1 calls, tested here as siteBuild is not built yet. Every name and number here is made up
    public sealed class RulesForBuildTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R664_" + Guid.NewGuid().ToString("N"));

        public RulesForBuildTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static JObject Config(string mode = "made up new")
        {
            return new JObject
            {
                ["designBasis"] = new JObject { ["mode"] = mode, ["modes"] = new JArray("made up old", "made up new") },
                ["projectRules"] = new JObject { ["pastJobMode"] = "made up old", ["ruleNames"] = new JObject { ["M1"] = "One rule", ["M2"] = "Two rule" } },
            };
        }

        private static JObject Confirmed(string id, JToken value, string source = "person entered")
        {
            return new JObject { ["id"] = id, ["name"] = "A made up rule", ["value"] = value, ["source"] = source, ["status"] = "confirmed", ["confirmedBy"] = "made up person" };
        }

        private static JObject[] AllFixed()
        {
            return new[] { "fixed.saudiChecks", "fixed.noGuessedValue", "fixed.everyChangeLogged" }.Select(id => new JObject { ["id"] = id, ["name"] = id, ["value"] = true, ["source"] = "rule", ["status"] = "fixed", ["fixed"] = true }).ToArray();
        }

        private string Write(string beforeBuild, params JObject[] rows)
        {
            string path = Path.Combine(folder, "project_rules.json");
            var file = new JObject { ["rules"] = new JArray(rows.Concat(AllFixed())) };
            if (beforeBuild != null)
            {
                file["beforeBuild"] = beforeBuild;
            }
            File.WriteAllText(path, file.ToString());
            return path;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void R664_no_rules_asks_once_and_gives_every_rule_missing_and_marked(string path)
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config());
            Assert.False(string.IsNullOrEmpty(answer.RulesChoiceLine));
            Assert.Null(answer.BlockingLine);
            Assert.Equal(new[] { "M1", "M2" }, answer.Rules.Select(rule => rule.Id));
            Assert.All(answer.Rules, rule =>
            {
                Assert.Null(rule.Value);
                Assert.Equal("missing", rule.Source);
                Assert.Null(rule.Rule);
                Assert.Equal("not confirmed", rule.Status);
                Assert.False(string.IsNullOrEmpty(rule.Marked));
            });
        }

        [Fact]
        public void R664_with_the_shipped_config_no_rules_gives_all_fifteen_missing_and_never_a_past_measure()
        {
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            RulesForBuildAnswer answer = RulesForBuild.Check(null, config);
            Assert.Equal(15, answer.Rules.Count);
            Assert.All(answer.Rules, rule =>
            {
                Assert.Null(rule.Value);
                Assert.Equal("missing", rule.Source);
                Assert.False(string.IsNullOrEmpty(rule.Marked));
                Assert.DoesNotContain("past", rule.Id);
            });
        }

        [Fact]
        public void R664_a_config_that_names_no_rules_says_so_and_does_not_pretend_there_are_none()
        {
            Exception failure = Assert.Throws<InvalidDataException>(() => RulesForBuild.Check(null, new JObject()));
            Assert.Contains("projectRules.ruleNames", failure.Message);
        }

        [Theory]
        [InlineData("warn", false)]
        [InlineData("block", true)]
        [InlineData(null, false)]
        public void R662_rules_not_confirmed_block_under_block_and_warn_otherwise_warn_the_default(string setting, bool blocks)
        {
            JObject open = Confirmed("M1", 1.0);
            open["status"] = "not confirmed";
            RulesForBuildAnswer answer = RulesForBuild.Check(Write(setting, open, Confirmed("M2", 2.0)), Config());
            Assert.Null(answer.RulesChoiceLine);
            if (blocks)
            {
                Assert.Contains("1 of 2", answer.BlockingLine);
                Assert.Empty(answer.Issues);
            }
            else
            {
                Assert.Null(answer.BlockingLine);
                Assert.Contains(answer.Issues, line => line.Contains("1 of 2") && line.Contains("not confirmed"));
            }
            Assert.Equal(1.0, (double)answer.Rules.Single(rule => rule.Id == "M1").Value);
        }

        [Theory]
        [InlineData("warn")]
        [InlineData("block")]
        public void R662_all_confirmed_gives_no_line_under_either_setting(string setting)
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write(setting, Confirmed("M1", 1.0), Confirmed("M2", 2.0)), Config());
            Assert.Null(answer.BlockingLine);
            Assert.Null(answer.RulesChoiceLine);
            Assert.Empty(answer.Issues);
            Assert.All(answer.Rules, rule => Assert.Equal("confirmed", rule.Status));
        }

        [Theory]
        [InlineData("stop")]
        [InlineData("WARN")]
        public void R662_a_setting_that_is_neither_stops_and_never_reads_as_warn(string setting)
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write(setting, Confirmed("M1", 1.0), Confirmed("M2", 2.0)), Config());
            Assert.Contains("neither warn nor block", answer.BlockingLine);
        }

        [Fact]
        public void R664_a_rule_source_names_its_rule_id_and_a_person_value_names_none()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("warn", Confirmed("M1", 1.0, "rule"), Confirmed("M2", 2.0)), Config());
            Assert.Equal("M1", answer.Rules.Single(rule => rule.Id == "M1").Rule);
            Assert.Null(answer.Rules.Single(rule => rule.Id == "M2").Rule);
        }

        [Fact]
        public void R664_confirmed_with_no_person_behind_it_is_not_confirmed()
        {
            JObject edited = Confirmed("M1", 1.0);
            edited.Remove("confirmedBy");
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("block", edited, Confirmed("M2", 2.0)), Config());
            Assert.Equal("not confirmed", answer.Rules.Single(rule => rule.Id == "M1").Status);
            Assert.NotNull(answer.BlockingLine);
        }

        [Fact]
        public void R664_a_missing_value_is_never_confirmed_by_an_edit_of_the_file()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("block", Confirmed("M1", null, "rule"), Confirmed("M2", 3.0, "missing")), Config());
            Assert.All(answer.Rules, rule =>
            {
                Assert.Null(rule.Value);
                Assert.Equal("missing", rule.Source);
                Assert.Equal("not confirmed", rule.Status);
                Assert.False(string.IsNullOrEmpty(rule.Marked));
            });
            Assert.Contains("2 of 2", answer.BlockingLine);
            Assert.Contains(answer.Issues, line => line.Contains("no value"));
        }

        [Fact]
        public void R664_a_past_value_is_dropped_outside_past_job_mode_and_kept_inside_it()
        {
            string path = Write("warn", Confirmed("M1", 9.0, "past"), Confirmed("M2", 2.0));
            RulesForBuildAnswer fresh = RulesForBuild.Check(path, Config("made up new"));
            RuleForBuild dropped = fresh.Rules.Single(rule => rule.Id == "M1");
            Assert.Null(dropped.Value);
            Assert.Equal("missing", dropped.Source);
            Assert.Contains("past", dropped.Marked);
            Assert.Contains(fresh.Issues, line => line.Contains("M1") && line.Contains("past"));

            RuleForBuild kept = RulesForBuild.Check(path, Config("made up old")).Rules.Single(rule => rule.Id == "M1");
            Assert.Equal(9.0, (double)kept.Value);
            Assert.Equal("past", kept.Source);
        }

        [Fact]
        public void R664_a_config_with_no_past_mode_never_lets_a_past_value_through()
        {
            JObject config = Config();
            ((JObject)config["designBasis"]).Remove("mode");
            ((JObject)config["projectRules"]).Remove("pastJobMode");
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("warn", Confirmed("M1", 9.0, "past"), Confirmed("M2", 2.0)), config);
            Assert.Null(answer.Rules.Single(rule => rule.Id == "M1").Value);
        }

        [Fact]
        public void R664_a_source_the_tool_does_not_know_is_not_used_and_is_said()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("warn", Confirmed("M1", 1.0, "guess"), Confirmed("M2", 2.0)), Config());
            RuleForBuild rule = answer.Rules.Single(item => item.Id == "M1");
            Assert.Null(rule.Value);
            Assert.Equal("missing", rule.Source);
            Assert.Contains(answer.Issues, line => line.Contains("M1") && line.Contains("source"));
        }

        [Fact]
        public void R664_a_rule_the_file_lost_is_missing_and_marked_and_takes_no_template_value()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("warn", Confirmed("M1", 1.0)), Config());
            RuleForBuild lost = answer.Rules.Single(rule => rule.Id == "M2");
            Assert.Null(lost.Value);
            Assert.Equal("missing", lost.Source);
            Assert.False(string.IsNullOrEmpty(lost.Marked));
        }

        [Fact]
        public void R663_a_fixed_rule_turned_off_or_lost_in_the_file_is_said_and_is_no_rule_of_the_build()
        {
            string path = Path.Combine(folder, "project_rules.json");
            JObject[] rows = AllFixed();
            rows[0]["value"] = false;
            File.WriteAllText(path, new JObject { ["beforeBuild"] = "warn", ["rules"] = new JArray(new[] { Confirmed("M1", 1.0), Confirmed("M2", 2.0), rows[0], rows[1] }) }.ToString());
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config());
            Assert.Equal(2, answer.Issues.Count(line => line.Contains("fixed rule") && line.Contains("keeps it on")));
            Assert.DoesNotContain(answer.Rules, rule => rule.Id.StartsWith("fixed.", StringComparison.Ordinal));
        }

        [Fact]
        public void R664_a_rules_file_not_on_disk_broken_or_empty_stops_in_plain_words_with_every_rule_missing()
        {
            string path = Path.Combine(folder, "project_rules.json");
            var cases = new (Action Make, string Word)[]
            {
                (() => { }, "not on disk"),
                (() => File.WriteAllText(path, "{ not json"), "cannot be read"),
                (() => File.WriteAllText(path, "{\"rules\": []}"), "no rules"),
                (() => File.WriteAllText(path, "{\"rules\": [{\"name\": \"no id\"}]}"), "listed twice"),
                (() => File.WriteAllText(path, "{\"rules\": [{\"id\": \"M1\"}, {\"id\": \"M1\"}]}"), "listed twice"),
            };
            foreach ((Action make, string word) in cases)
            {
                make();
                RulesForBuildAnswer answer = RulesForBuild.Check(path, Config());
                Assert.Contains(word, answer.BlockingLine);
                Assert.DoesNotContain("{", answer.BlockingLine);
                Assert.Null(answer.RulesChoiceLine);
                Assert.Equal(2, answer.Rules.Count);
                Assert.All(answer.Rules, rule => Assert.Equal("missing", rule.Source));
            }
        }
    }
}
