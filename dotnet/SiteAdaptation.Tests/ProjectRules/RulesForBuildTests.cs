using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;
using Xunit;

namespace SiteAdaptation.Tests.ProjectRules
{
    // R662 to R664, what siteBuild of R2-C2-build-1 calls, tested here as siteBuild is not built yet. Every name and number here is made up, the log lines are written by hand
    // so each case is one fault. RulesCommandTests holds the same reads over a log the commands wrote
    public sealed class RulesForBuildTests : IDisposable
    {
        private const string Saudi = "The Saudi checks are not defined yet and none ran, see Q514.";
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R664_" + Guid.NewGuid().ToString("N"));

        public RulesForBuildTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private string RulesPath => Path.Combine(folder, "project_rules.json");

        private string LogPath => Path.Combine(folder, "rules_log.jsonl");

        private static JObject Config()
        {
            return new JObject
            {
                ["designBasis"] = new JObject { ["mode"] = "made up old", ["modes"] = new JArray("made up old", "made up new") },
                ["projectRules"] = new JObject { ["manualName"] = "a made up manual Rev 09", ["pastJobMode"] = "made up old", ["ruleIds"] = new JArray("M1", "M2") },
            };
        }

        private static JObject Shipped()
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
        }

        private static JObject Row(string id, JToken value, string source = "person entered", bool confirmed = true, string by = "made up person")
        {
            var row = new JObject { ["id"] = id, ["name"] = "A made up rule", ["value"] = value, ["source"] = source, ["status"] = confirmed ? "confirmed" : "not confirmed" };
            if (by != null && confirmed)
            {
                row["confirmedBy"] = by;
            }
            return row;
        }

        private static JObject[] AllFixed()
        {
            return new[]
            {
                new JObject { ["id"] = "fixed.saudiChecks", ["name"] = "The Saudi checks", ["value"] = JValue.CreateNull(), ["source"] = "missing", ["status"] = "fixed", ["fixed"] = true },
                new JObject { ["id"] = "fixed.noGuessedValue", ["name"] = "n", ["value"] = true, ["source"] = "rule", ["status"] = "fixed", ["fixed"] = true },
                new JObject { ["id"] = "fixed.everyChangeLogged", ["name"] = "n", ["value"] = true, ["source"] = "rule", ["status"] = "fixed", ["fixed"] = true },
            };
        }

        private static string Line(string rule, JToken value, string who = "made up person", string run = "r1", string failed = null)
        {
            var line = new JObject { ["who"] = who, ["when"] = "2020-01-02T03:04:05.0000000Z", ["run"] = run, ["rule"] = rule, ["old"] = JValue.CreateNull(), ["new"] = value };
            if (failed != null)
            {
                line["failed"] = failed;
            }
            return line.ToString(Newtonsoft.Json.Formatting.None);
        }

        // The rows, with their fixed rules, and a log that holds the value of each confirmed row, as the commands leave them
        private string Write(string beforeBuild, params JObject[] rows)
        {
            var file = new JObject { ["rules"] = new JArray(rows.Concat(AllFixed())) };
            if (beforeBuild != null)
            {
                file["beforeBuild"] = beforeBuild;
            }
            File.WriteAllText(RulesPath, file.ToString());
            var lines = new List<string> { Line("rulesSetup", "t.json") };
            lines.AddRange(rows.Where(row => (string)row["status"] == "confirmed").Select(row => Line((string)row["id"], row["value"])));
            if (beforeBuild == "block")
            {
                lines.Add(Line("beforeBuild", "block"));
            }
            File.WriteAllLines(LogPath, lines);
            return RulesPath;
        }

        private static List<string> Others(RulesForBuildAnswer answer)
        {
            return answer.Issues.Where(line => line != Saudi).ToList();
        }

        private static RuleForBuild Of(RulesForBuildAnswer answer, string id)
        {
            return answer.Rules.Single(rule => rule.Id == id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void R664_no_rules_asks_once_and_gives_every_rule_missing_and_marked(string path)
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.Contains("a made up manual Rev 09", answer.RulesChoiceLine);
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
        public void R664_with_the_shipped_config_no_rules_gives_all_fifteen_missing_and_the_line_names_the_manual()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(null, Shipped(), null);
            Assert.Equal(15, answer.Rules.Count);
            Assert.Contains("Rev 06", answer.RulesChoiceLine);
            Assert.Contains("that one manual", answer.RulesChoiceLine);
            Assert.All(answer.Rules, rule =>
            {
                Assert.Null(rule.Value);
                Assert.Equal("missing", rule.Source);
                Assert.False(string.IsNullOrEmpty(rule.Marked));
                Assert.DoesNotContain("past", rule.Id);
            });
        }

        [Fact]
        public void R664_a_config_with_no_list_gives_a_plain_line_with_or_without_a_rules_file_and_never_throws()
        {
            JObject noList = Shipped();
            ((JObject)noList["projectRules"]).Remove("ruleIds");
            JObject noManual = Shipped();
            ((JObject)noManual["projectRules"]).Remove("manualName");
            JObject older = Shipped();
            older.Remove("projectRules");
            JObject wrongKind = Shipped();
            noList["projectRules"]["ruleIds"]?.Remove();
            wrongKind["projectRules"]["ruleIds"] = new JArray(1, 2);
            string path = Write("warn", Row("M1", 1.0));
            foreach (JObject config in new[] { noList, noManual, older, wrongKind, new JObject() })
            {
                foreach (string rules in new[] { null, path })
                {
                    RulesForBuildAnswer answer = RulesForBuild.Check(rules, config, null);
                    Assert.Contains("names no rules", answer.BlockingLine);
                    Assert.DoesNotContain("{", answer.BlockingLine);
                    Assert.DoesNotContain("projectRules", answer.BlockingLine);
                    Assert.Null(answer.RulesChoiceLine);
                    Assert.Empty(answer.Rules);
                }
            }
        }

        [Fact]
        public void R663_every_build_says_no_Saudi_check_ran_and_no_line_says_they_are_on()
        {
            string path = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            var answers = new[] { RulesForBuild.Check(null, Config(), null), RulesForBuild.Check(path, Config(), null), RulesForBuild.Check(Path.Combine(folder, "gone.json"), Config(), null), RulesForBuild.Check(null, new JObject(), null) };
            Assert.All(answers, answer =>
            {
                Assert.Contains(Saudi, answer.Issues);
                Assert.DoesNotContain(answer.Issues, line => line.Contains("always run") || line.Contains("keeps it on"));
            });
        }

        [Theory]
        [InlineData("warn", false)]
        [InlineData("block", true)]
        [InlineData(null, false)]
        public void R662_rules_not_confirmed_block_under_block_and_warn_otherwise_warn_the_default(string setting, bool blocks)
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write(setting, Row("M1", 1.0, confirmed: false), Row("M2", 2.0)), Config(), null);
            Assert.Null(answer.RulesChoiceLine);
            if (blocks)
            {
                Assert.Contains("1 of 2", answer.BlockingLine);
                Assert.Empty(Others(answer));
            }
            else
            {
                Assert.Null(answer.BlockingLine);
                Assert.Contains(answer.Issues, line => line.Contains("1 of 2") && line.Contains("not confirmed"));
            }
            Assert.Equal(1.0, (double)Of(answer, "M1").Value);
        }

        [Theory]
        [InlineData("warn")]
        [InlineData("block")]
        public void R662_all_confirmed_with_the_log_behind_them_gives_no_line_under_either_setting(string setting)
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write(setting, Row("M1", 1.0), Row("M2", 2.0)), Config(), null);
            Assert.Null(answer.BlockingLine);
            Assert.Null(answer.RulesChoiceLine);
            Assert.Empty(Others(answer));
            Assert.All(answer.Rules, rule => Assert.Equal("confirmed", rule.Status));
        }

        [Theory]
        [InlineData("stop")]
        [InlineData("WARN")]
        public void R662_a_setting_that_is_neither_stops_and_never_reads_as_warn(string setting)
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write(setting, Row("M1", 1.0), Row("M2", 2.0)), Config(), null);
            Assert.Contains("neither warn nor block", answer.BlockingLine);
        }

        [Fact]
        public void R664_a_rule_source_names_its_rule_id_and_a_person_value_names_none()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("warn", Row("M1", 1.0, "rule"), Row("M2", 2.0)), Config(), null);
            Assert.Equal("M1", Of(answer, "M1").Rule);
            Assert.Null(Of(answer, "M2").Rule);
        }

        [Fact]
        public void R662_confirmed_with_no_person_behind_it_is_not_confirmed()
        {
            string path = Write("block", Row("M1", 1.0, by: null), Row("M2", 2.0));
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.Equal("not confirmed", Of(answer, "M1").Status);
            Assert.NotNull(answer.BlockingLine);
        }

        [Fact]
        public void R662_a_hand_edit_to_confirmed_with_no_log_line_is_not_confirmed_and_is_named()
        {
            string path = Write("block", Row("M1", 1.0), Row("M2", 2.0));
            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M2", 2.0), Line("beforeBuild", "block") });
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.Equal("not confirmed", Of(answer, "M1").Status);
            Assert.Equal("confirmed", Of(answer, "M2").Status);
            Assert.Contains(answer.Issues, line => line.Contains("M1") && line.Contains("changed outside the tool"));
            Assert.Contains("1 of 2", answer.BlockingLine);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void R662_a_value_the_last_log_line_does_not_hold_is_not_confirmed_and_is_named_whatever_its_status(bool saysConfirmed)
        {
            string path = Write("warn", Row("M1", 5.0, confirmed: saysConfirmed), Row("M2", 2.0));
            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 5.0), Line("M1", 4.0), Line("M2", 2.0) });
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.Equal("not confirmed", Of(answer, "M1").Status);
            Assert.Contains(answer.Issues, line => line.Contains("M1") && line.Contains("changed outside the tool"));
        }

        [Fact]
        public void R662_a_confirmed_row_that_names_another_person_than_the_log_is_not_confirmed()
        {
            string path = Write("warn", Row("M1", 1.0, by: "someone else"), Row("M2", 2.0));
            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 1.0, "made up person"), Line("M2", 2.0) });
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.Equal("not confirmed", Of(answer, "M1").Status);
            Assert.Contains(answer.Issues, line => line.Contains("M1") && line.Contains("another person"));
        }

        [Fact]
        public void R662_the_whole_and_decimal_form_of_a_value_are_the_same_value_as_the_log()
        {
            string path = Write("warn", Row("M1", 2L), Row("M2", new JArray(100L)));
            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 2.0), Line("M2", new JArray(100.0)) });
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.All(answer.Rules, rule => Assert.Equal("confirmed", rule.Status));
        }

        [Fact]
        public void R661_a_failed_line_cancels_the_line_before_it_of_that_run_and_rule_only()
        {
            string path = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            File.WriteAllLines(LogPath, new[]
            {
                Line("rulesSetup", "t.json"),
                Line("M1", 1.0, run: "same"), Line("M2", 2.0, run: "same"),
                Line("M1", 9.0, run: "same"), Line("M1", 9.0, run: "same", failed: "planted"),
            });
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.All(answer.Rules, rule => Assert.Equal("confirmed", rule.Status));
            Assert.Empty(Others(answer));

            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 1.0, run: "other"), Line("M2", 2.0), Line("M1", 1.0, run: "this"), Line("M1", 1.0, run: "this", failed: "planted") });
            Assert.Equal("confirmed", Of(RulesForBuild.Check(path, Config(), null), "M1").Status);

            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 1.0, run: "this"), Line("M1", 1.0, run: "this", failed: "planted"), Line("M2", 2.0) });
            RulesForBuildAnswer cancelled = RulesForBuild.Check(path, Config(), null);
            Assert.Equal("not confirmed", Of(cancelled, "M1").Status);
            Assert.Equal("confirmed", Of(cancelled, "M2").Status);
        }

        [Fact]
        public void R662_a_kill_after_the_line_reads_as_a_difference_and_is_said()
        {
            string path = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            File.AppendAllLines(LogPath, new[] { Line("M1", 9.0) });
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.Equal("not confirmed", Of(answer, "M1").Status);
            Assert.Contains(answer.Issues, line => line.Contains("M1") && line.Contains("a run stopped"));
        }

        [Fact]
        public void R662_a_line_cut_short_is_passed_over()
        {
            string path = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            File.AppendAllText(LogPath, "{\"who\":\"cut sho");
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.All(answer.Rules, rule => Assert.Equal("confirmed", rule.Status));
        }

        [Theory]
        [InlineData("warn", "block")]
        [InlineData("block", "warn")]
        public void R662_Before_Build_that_differs_from_the_last_logged_setting_stops_with_a_plain_line(string file, string logged)
        {
            string path = Write(file, Row("M1", 1.0), Row("M2", 2.0));
            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 1.0), Line("M2", 2.0), Line("beforeBuild", "warn"), Line("beforeBuild", logged) });
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.Contains("the log says " + logged, answer.BlockingLine);
        }

        [Fact]
        public void R662_block_by_hand_with_no_logged_setting_stops_and_warn_with_none_is_the_default()
        {
            string path = Write("block", Row("M1", 1.0), Row("M2", 2.0));
            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 1.0), Line("M2", 2.0) });
            Assert.Contains("the log says warn", RulesForBuild.Check(path, Config(), null).BlockingLine);
            string kept = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            Assert.Null(RulesForBuild.Check(kept, Config(), null).BlockingLine);
        }

        [Fact]
        public void R662_a_failed_setting_line_is_not_the_last_logged_setting()
        {
            string path = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            File.AppendAllLines(LogPath, new[] { Line("beforeBuild", "block"), Line("beforeBuild", "block", failed: "planted") });
            Assert.Null(RulesForBuild.Check(path, Config(), null).BlockingLine);
        }

        [Fact]
        public void R662_with_the_log_gone_every_row_is_not_confirmed_and_it_says_so()
        {
            string path = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            File.Delete(LogPath);
            RulesForBuildAnswer answer = RulesForBuild.Check(path, Config(), null);
            Assert.All(answer.Rules, rule => Assert.Equal("not confirmed", rule.Status));
            Assert.Contains(answer.Issues, line => line.Contains("log") && line.Contains("gone"));
            Assert.Contains(answer.Issues, line => line.Contains("2 of 2"));
            Assert.Null(answer.BlockingLine);

            string blocked = Write("block", Row("M1", 1.0), Row("M2", 2.0));
            File.Delete(LogPath);
            Assert.Contains("2 of 2", RulesForBuild.Check(blocked, Config(), null).BlockingLine);
        }

        [WindowsFact]
        public void R662_the_log_held_by_a_change_can_still_be_read()
        {
            string path = Write("warn", Row("M1", 1.0), Row("M2", 2.0));
            using (new FileStream(LogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                Assert.All(RulesForBuild.Check(path, Config(), null).Rules, rule => Assert.Equal("confirmed", rule.Status));
            }
        }

        [Fact]
        public void R664_a_missing_value_is_never_confirmed_by_an_edit_of_the_file()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("block", Row("M1", null, "rule"), Row("M2", 3.0, "missing")), Config(), null);
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

        [Theory]
        [InlineData("\"\"")]
        [InlineData("\"   \"")]
        [InlineData("[]")]
        [InlineData("{}")]
        [InlineData("[\"\"]")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        public void R664_a_blank_in_the_file_counts_as_missing(string raw)
        {
            string path = Write("warn", Row("M1", "PLANT"), Row("M2", 2.0));
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"PLANT\"", raw));
            RuleForBuild rule = Of(RulesForBuild.Check(path, Config(), null), "M1");
            Assert.Null(rule.Value);
            Assert.Equal("missing", rule.Source);
            Assert.Equal("not confirmed", rule.Status);
            Assert.False(string.IsNullOrEmpty(rule.Marked));
        }

        [Fact]
        public void R664_a_past_row_is_dropped_for_a_project_with_no_mode_under_the_shipped_config_and_kept_only_in_past_job_mode()
        {
            JObject shipped = Shipped();
            string pastMode = (string)shipped["projectRules"]["pastJobMode"];
            // The fault this holds: the shipped config says past job mode, so a project with no mode must not read it from there
            Assert.Equal(pastMode, (string)shipped["designBasis"]["mode"]);
            string path = Write("warn", Row("M1", 9.0, "past"), Row("M2", 2.0));
            foreach (string mode in new[] { null, (string)shipped["designBasis"]["newProjectMode"] })
            {
                RulesForBuildAnswer fresh = RulesForBuild.Check(path, shipped, mode);
                RuleForBuild dropped = Of(fresh, "M1");
                Assert.Null(dropped.Value);
                Assert.Equal("missing", dropped.Source);
                Assert.Contains("past", dropped.Marked);
                Assert.Contains(fresh.Issues, line => line.Contains("M1") && line.Contains("past"));
            }
            RuleForBuild kept = Of(RulesForBuild.Check(path, shipped, pastMode), "M1");
            Assert.Equal(9.0, (double)kept.Value);
            Assert.Equal("past", kept.Source);
        }

        [Fact]
        public void R664_a_config_with_no_past_mode_never_lets_a_past_value_through()
        {
            JObject config = Config();
            ((JObject)config["projectRules"]).Remove("pastJobMode");
            string path = Write("warn", Row("M1", 9.0, "past"), Row("M2", 2.0));
            Assert.Null(Of(RulesForBuild.Check(path, config, "made up old"), "M1").Value);
            Assert.Null(Of(RulesForBuild.Check(path, config, null), "M1").Value);
        }

        [Fact]
        public void R664_a_source_the_tool_does_not_know_is_not_used_and_is_said()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("warn", Row("M1", 1.0, "guess"), Row("M2", 2.0)), Config(), null);
            RuleForBuild rule = Of(answer, "M1");
            Assert.Null(rule.Value);
            Assert.Equal("missing", rule.Source);
            Assert.Contains(answer.Issues, line => line.Contains("M1") && line.Contains("source"));
        }

        [Fact]
        public void R664_a_rule_the_file_lost_is_missing_and_marked_and_takes_no_template_value()
        {
            RulesForBuildAnswer answer = RulesForBuild.Check(Write("warn", Row("M1", 1.0)), Config(), null);
            RuleForBuild lost = Of(answer, "M2");
            Assert.Null(lost.Value);
            Assert.Equal("missing", lost.Source);
            Assert.False(string.IsNullOrEmpty(lost.Marked));
        }

        [Fact]
        public void R663_a_fixed_rule_changed_or_lost_in_the_file_is_said_and_is_no_rule_of_the_build()
        {
            JObject[] rows = AllFixed();
            rows[1]["value"] = false;
            File.WriteAllText(RulesPath, new JObject { ["beforeBuild"] = "warn", ["rules"] = new JArray(new[] { Row("M1", 1.0), Row("M2", 2.0), rows[0], rows[1] }) }.ToString());
            File.WriteAllLines(LogPath, new[] { Line("rulesSetup", "t.json"), Line("M1", 1.0), Line("M2", 2.0) });
            RulesForBuildAnswer answer = RulesForBuild.Check(RulesPath, Config(), null);
            Assert.Equal(2, answer.Issues.Count(line => line.Contains("fixed rule") && line.Contains("goes by its own")));
            Assert.DoesNotContain(answer.Rules, rule => rule.Id.StartsWith("fixed.", StringComparison.Ordinal));
        }

        [Fact]
        public void R664_a_rules_file_not_on_disk_broken_or_empty_stops_in_plain_words_with_every_rule_missing()
        {
            var cases = new (Action Make, string Word)[]
            {
                (() => { }, "not on disk"),
                (() => File.WriteAllText(RulesPath, "{ not json"), "cannot be read"),
                (() => File.WriteAllText(RulesPath, "{\"rules\": []}"), "no rules"),
                (() => File.WriteAllText(RulesPath, "{\"rules\": [{\"name\": \"no id\"}]}"), "listed twice"),
                (() => File.WriteAllText(RulesPath, "{\"rules\": [{\"id\": \"M1\"}, {\"id\": \"M1\"}]}"), "listed twice"),
            };
            foreach ((Action make, string word) in cases)
            {
                make();
                RulesForBuildAnswer answer = RulesForBuild.Check(RulesPath, Config(), null);
                Assert.Contains(word, answer.BlockingLine);
                Assert.DoesNotContain("{", answer.BlockingLine);
                Assert.Null(answer.RulesChoiceLine);
                Assert.Equal(2, answer.Rules.Count);
                Assert.All(answer.Rules, rule => Assert.Equal("missing", rule.Source));
            }
        }
    }
}
