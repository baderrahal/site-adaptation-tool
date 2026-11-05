using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.ProjectRules;
using Xunit;

namespace SiteAdaptation.Tests.ProjectRules
{
    // R660 to R663, through the runner as the worker runs them. Every name and number here is made up
    public sealed class RulesCommandTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "R660_" + Guid.NewGuid().ToString("N"));
        private readonly string project;
        private readonly string work;
        private readonly string input;

        public RulesCommandTests()
        {
            project = Path.Combine(root, "project");
            work = Path.Combine(root, "work");
            input = Path.Combine(root, "input root");
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(work);
            Directory.CreateDirectory(input);
        }

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(root, true);
        }

        private string RulesPath => Path.Combine(project, "project_rules.json");

        private string LogPath => Path.Combine(project, "rules_log.jsonl");

        private string Config()
        {
            string path = Path.Combine(root, "config.json");
            File.WriteAllText(path, new JObject
            {
                ["inputRoot"] = input,
                ["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 },
                ["addin"] = new JObject { ["workerTimeLimitMinS"] = 0.5, ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) } },
                ["designBasis"] = new JObject { ["mode"] = "made up old", ["modes"] = new JArray("made up old", "made up new") },
                ["projectRules"] = new JObject { ["pastJobMode"] = "made up old", ["ruleNames"] = new JObject { ["M1"] = "One rule" } },
            }.ToString());
            return path;
        }

        private string Template(params JObject[] rows)
        {
            string path = Path.Combine(root, "template_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, new JObject { ["rules"] = new JArray(rows) }.ToString());
            return path;
        }

        private static JObject Missing(string id)
        {
            return new JObject { ["id"] = id, ["name"] = "A made up rule", ["value"] = null, ["source"] = "missing" };
        }

        private static JObject Valued(string id, JToken value, string source = "rule")
        {
            return new JObject { ["id"] = id, ["name"] = "A made up rule", ["value"] = value, ["source"] = source };
        }

        private int Run(string command, string who, string ruleId, JToken newValue, string mode, params (string Role, string Path)[] inputs)
        {
            var files = new List<WorkerFile> { new WorkerFile { Role = "config", Path = Config(), Sha256 = "" } };
            files.AddRange(inputs.Select(item => new WorkerFile { Role = item.Role, Path = item.Path, Sha256 = "" }));
            var request = new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = "r1", Document = new WorkerDocument { Path = Path.Combine(work, "a.rvt"), SessionId = "s1" }, Command = command,
                Inputs = files, Scope = new WorkerScope { Kind = "project" }, OutputFolder = work, TimeLimitS = 60, Who = who, RuleId = ruleId, NewValue = newValue, ProjectMode = mode,
            };
            var commands = new Dictionary<string, WorkerCommand>();
            ProjectRulesCommands.Add(commands);
            return WorkerRunner.Run(request.Write(work), commands, () => 9000.0);
        }

        private int Setup(string template, string who = "made up person", string folder = null, string mode = null)
        {
            return Run("rulesSetup", who, null, null, mode, ("template", template), ("projectFolder", folder ?? project));
        }

        private int Change(string ruleId, JToken newValue, string who = "made up person", string rules = null)
        {
            return Run("rulesChange", who, ruleId, newValue, null, ("rules", rules ?? RulesPath));
        }

        private string Message()
        {
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(work, "r1")).Message;
        }

        private JObject Rules()
        {
            return JObject.Parse(File.ReadAllText(RulesPath));
        }

        private JObject Rule(string id)
        {
            return ((JArray)Rules()["rules"]).OfType<JObject>().Single(rule => (string)rule["id"] == id);
        }

        private List<JObject> Log()
        {
            return File.ReadAllLines(LogPath).Select(JObject.Parse).ToList();
        }

        [Fact]
        public void R660_setup_writes_every_rule_not_confirmed_on_warn_with_the_fixed_rules_and_logs_who()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"), Valued("M2", 2.5))));
            JObject rules = Rules();
            Assert.Equal("warn", (string)rules["beforeBuild"]);
            Assert.Equal("not confirmed", (string)Rule("M1")["status"]);
            Assert.Equal("not confirmed", (string)Rule("M2")["status"]);
            Assert.Equal("missing", (string)Rule("M1")["source"]);
            Assert.Equal(JTokenType.Null, Rule("M1")["value"].Type);
            Assert.False(string.IsNullOrEmpty((string)Rule("M1")["marked"]));
            Assert.Equal("rule", (string)Rule("M2")["source"]);
            string[] fixedIds = ((JArray)rules["rules"]).OfType<JObject>().Where(rule => (bool?)rule["fixed"] == true).Select(rule => (string)rule["id"]).ToArray();
            Assert.Contains("fixed.saudiChecks", fixedIds);
            Assert.Contains("fixed.noGuessedValue", fixedIds);
            Assert.Contains("fixed.everyChangeLogged", fixedIds);
            JObject line = Log().Single();
            Assert.Equal("made up person", (string)line["who"]);
            Assert.False(string.IsNullOrEmpty((string)line["when"]));
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(work, "r1"));
            Assert.Equal(2, answer.Counts["rules"]);
            Assert.Equal(1, answer.Counts["missing"]);
        }

        [Fact]
        public void R660_a_second_setup_changes_nothing_of_what_a_person_entered()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            Assert.Equal(0, Change("M1", 7.0));
            string before = File.ReadAllText(RulesPath);
            Assert.Equal(2, Setup(Template(Missing("M1"))));
            Assert.Contains("already holds", Message());
            Assert.Equal(before, File.ReadAllText(RulesPath));
        }

        [Fact]
        public void R660_past_measures_are_refused_unless_the_request_names_the_past_job_mode()
        {
            string past = Template(Valued("P1", "made up rule name", "past"));
            // The mode of config alone is the past mode here, and a request with no mode is still refused, so a past template is never taken by default
            Assert.Equal(2, Setup(past));
            Assert.Contains("past job", Message());
            Assert.False(File.Exists(RulesPath));
            Assert.Equal(2, Setup(past, mode: "made up new"));
            Assert.False(File.Exists(RulesPath));
            Assert.Equal(0, Setup(past, mode: "made up old"));
            Assert.Equal("past", (string)Rule("P1")["source"]);
            Assert.Equal("not confirmed", (string)Rule("P1")["status"]);
        }

        [Theory]
        [InlineData("value with no source")]
        [InlineData("value with a person source")]
        [InlineData("no value with a rule source")]
        [InlineData("a fixed id")]
        [InlineData("the setting id")]
        [InlineData("a repeated id")]
        [InlineData("no rules")]
        public void R660_a_template_that_does_not_hold_up_writes_nothing(string fault)
        {
            string template;
            switch (fault)
            {
                case "value with no source": template = Template(new JObject { ["id"] = "M1", ["value"] = 1.0 }); break;
                case "value with a person source": template = Template(Valued("M1", 1.0, "person entered")); break;
                case "no value with a rule source": template = Template(Valued("M1", null)); break;
                case "a fixed id": template = Template(Missing("fixed.saudiChecks")); break;
                case "the setting id": template = Template(Missing("beforeBuild")); break;
                case "a repeated id": template = Template(Missing("M1"), Missing("M1")); break;
                default: template = Template(); break;
            }
            Assert.Equal(2, Setup(template));
            Assert.Contains("template", Message());
            Assert.False(File.Exists(RulesPath));
        }

        [Fact]
        public void R660_setup_with_no_person_writes_nothing()
        {
            Assert.Equal(2, Setup(Template(Missing("M1")), who: "  "));
            Assert.False(File.Exists(RulesPath));
            Assert.False(File.Exists(LogPath));
        }

        [Fact]
        public void R660_setup_refuses_a_real_input_folder_the_repo_and_the_templates_folder()
        {
            string template = Template(Missing("M1"));
            string inside = Path.Combine(input, "a");
            Directory.CreateDirectory(inside);
            Assert.Equal(2, Setup(template, folder: inside));
            Assert.Contains("input folder", Message());
            Assert.False(File.Exists(Path.Combine(inside, "project_rules.json")));

            Assert.Equal(2, Setup(template, folder: ThemeGateTests.RepoRoot()));
            Assert.Contains("git", Message());

            string shipped = Path.Combine(root, "rules", "templates");
            Directory.CreateDirectory(shipped);
            Assert.Equal(2, Setup(template, folder: shipped));
            Assert.Contains("read only", Message());
            Assert.False(File.Exists(Path.Combine(shipped, "project_rules.json")));
        }

        [Fact]
        public void R661_a_change_logs_who_when_old_and_new_and_the_value_becomes_person_entered_and_confirmed()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"), Valued("M2", 2.5))));
            Assert.Equal(0, Change("M2", 3.5));
            JObject line = Log().Last();
            Assert.Equal("made up person", (string)line["who"]);
            Assert.Equal("M2", (string)line["rule"]);
            Assert.Equal(2.5, (double)line["old"]);
            Assert.Equal(3.5, (double)line["new"]);
            // The raw line, as a plain parse turns the text into a date and drops the mark of UTC
            Assert.Matches("\"when\":\"\\d{4}-\\d{2}-\\d{2}T[0-9:.]+Z\"", File.ReadAllLines(LogPath).Last());
            Assert.Equal(3.5, (double)Rule("M2")["value"]);
            Assert.Equal("person entered", (string)Rule("M2")["source"]);
            Assert.Equal("confirmed", (string)Rule("M2")["status"]);
            Assert.Equal("made up person", (string)Rule("M2")["confirmedBy"]);
            Assert.Equal("not confirmed", (string)Rule("M1")["status"]);
        }

        [Fact]
        public void R661_a_value_entered_for_a_missing_rule_clears_its_mark_and_logs_old_as_null()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            Assert.Equal(0, Change("M1", 123.0));
            Assert.Equal(JTokenType.Null, Log().Last()["old"].Type);
            Assert.Null(Rule("M1")["marked"]);
            Assert.Equal("person entered", (string)Rule("M1")["source"]);
        }

        [Theory]
        [InlineData(2.0, "the same double")]
        [InlineData(2L, "a whole number for the same value")]
        public void R661_the_value_it_already_holds_confirms_it_and_keeps_its_source(object same, string why)
        {
            Assert.Equal(0, Setup(Template(Valued("M1", 2.0))));
            Assert.Equal(0, Change("M1", JToken.FromObject(same)));
            Assert.True((string)Rule("M1")["source"] == "rule", why);
            Assert.Equal("confirmed", (string)Rule("M1")["status"]);
            Assert.Equal(2, Log().Count);
        }

        [Fact]
        public void R661_a_text_that_looks_like_a_date_stays_as_typed_when_another_rule_changes()
        {
            Assert.Equal(0, Setup(Template(Valued("M1", "2020-01-02"), Missing("M2"))));
            Assert.Equal(0, Change("M2", 1.0));
            Assert.Contains("\"2020-01-02\"", File.ReadAllText(RulesPath));
        }

        [Fact]
        public void R661_the_shipped_templates_and_any_other_file_name_are_refused_and_stay_as_they_were()
        {
            string real = Path.Combine(ThemeGateTests.RepoRoot(), "rules", "templates", "manual.json");
            string before = File.ReadAllText(real);
            Assert.Equal(2, Change("M01", 1.0, rules: real));
            Assert.Contains("read only", Message());
            Assert.Equal(before, File.ReadAllText(real));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(real), "rules_log.jsonl")));

            string folder = Path.Combine(root, "RULES", "Templates");
            Directory.CreateDirectory(folder);
            string named = Path.Combine(folder, "project_rules.json");
            File.WriteAllText(named, new JObject { ["rules"] = new JArray(Missing("M1")) }.ToString());
            string held = File.ReadAllText(named);
            Assert.Equal(2, Change("M1", 1.0, rules: named));
            Assert.Contains("read only", Message());
            Assert.Equal(held, File.ReadAllText(named));

            string copy = Path.Combine(project, "manual.json");
            File.WriteAllText(copy, held);
            Assert.Equal(2, Change("M1", 1.0, rules: copy));
            Assert.Contains("only", Message());
            Assert.Equal(held, File.ReadAllText(copy));
        }

        [Fact]
        public void R661_a_rules_file_in_a_real_input_folder_is_refused()
        {
            string inside = Path.Combine(input, "project_rules.json");
            File.WriteAllText(inside, new JObject { ["rules"] = new JArray(Missing("M1")) }.ToString());
            string held = File.ReadAllText(inside);
            Assert.Equal(2, Change("M1", 1.0, rules: inside));
            Assert.Equal(held, File.ReadAllText(inside));
            Assert.False(File.Exists(Path.Combine(input, "rules_log.jsonl")));
        }

        [Theory]
        [InlineData("fixed.saudiChecks")]
        [InlineData("fixed.noGuessedValue")]
        [InlineData("fixed.everyChangeLogged")]
        public void R663_a_fixed_rule_is_refused_and_nothing_is_logged(string id)
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            string before = File.ReadAllText(RulesPath);
            int lines = Log().Count;
            Assert.Equal(2, Change(id, false));
            Assert.Contains("fixed", Message());
            Assert.Equal(before, File.ReadAllText(RulesPath));
            Assert.Equal(lines, Log().Count);
        }

        [Fact]
        public void R663_a_fixed_rule_is_still_refused_when_the_file_was_edited_to_say_it_is_not()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            JObject edited = Rules();
            foreach (JObject rule in ((JArray)edited["rules"]).OfType<JObject>())
            {
                rule.Remove("fixed");
            }
            File.WriteAllText(RulesPath, edited.ToString());
            Assert.Equal(2, Change("fixed.everyChangeLogged", false));
            Assert.Contains("fixed", Message());
        }

        [Theory]
        [InlineData("no person")]
        [InlineData("no rule")]
        [InlineData("no value")]
        [InlineData("unknown rule")]
        public void R661_a_change_that_does_not_name_all_it_needs_changes_nothing_and_logs_nothing(string fault)
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            string before = File.ReadAllText(RulesPath);
            int lines = Log().Count;
            int code = fault == "no person" ? Change("M1", 1.0, who: null)
                : fault == "no rule" ? Change(null, 1.0)
                : fault == "no value" ? Change("M1", null)
                : Change("not a rule", 1.0);
            Assert.Equal(2, code);
            Assert.Equal(before, File.ReadAllText(RulesPath));
            Assert.Equal(lines, Log().Count);
        }

        [WindowsFact]
        public void R663_a_write_that_fails_says_so_in_the_log_and_leaves_the_rules_and_no_temporary_file()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            string before = File.ReadAllText(RulesPath);
            File.SetAttributes(RulesPath, FileAttributes.ReadOnly);
            Assert.NotEqual(0, Change("M1", 1.0));
            File.SetAttributes(RulesPath, FileAttributes.Normal);
            Assert.Equal(before, File.ReadAllText(RulesPath));
            List<JObject> log = Log();
            Assert.Equal(3, log.Count);
            Assert.Null(log[1]["failed"]);
            Assert.False(string.IsNullOrEmpty((string)log[2]["failed"]));
            Assert.Equal("M1", (string)log[2]["rule"]);
            Assert.Empty(Directory.GetFiles(project).Where(WorkFiles.IsTemporary));
        }

        [WindowsFact]
        public void R663_a_change_while_another_holds_the_log_is_refused_and_changes_nothing()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            string before = File.ReadAllText(RulesPath);
            using (new FileStream(LogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                Assert.Equal(2, Change("M1", 1.0));
            }
            Assert.Equal(before, File.ReadAllText(RulesPath));
            Assert.Single(Log());
        }

        [Fact]
        public void R663_a_line_cut_short_by_a_stop_does_not_swallow_the_next_change()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            File.AppendAllText(LogPath, "{\"who\":\"cut sho");
            Assert.Equal(0, Change("M1", 1.0));
            string[] lines = File.ReadAllLines(LogPath);
            Assert.Equal(3, lines.Length);
            Assert.Equal("M1", (string)JObject.Parse(lines[2])["rule"]);
        }

        [Fact]
        public void R662_Before_Build_changes_by_the_command_with_a_log_line_and_only_to_warn_or_block()
        {
            Assert.Equal(0, Setup(Template(Missing("M1"))));
            Assert.Equal(0, Change("beforeBuild", "block"));
            Assert.Equal("block", (string)Rules()["beforeBuild"]);
            JObject line = Log().Last();
            Assert.Equal("beforeBuild", (string)line["rule"]);
            Assert.Equal("warn", (string)line["old"]);
            Assert.Equal("block", (string)line["new"]);
            Assert.Equal(2, Change("beforeBuild", "stop"));
            Assert.Equal("block", (string)Rules()["beforeBuild"]);
            Assert.Equal(2, Change("beforeBuild", 1.0));
            Assert.Equal("block", (string)Rules()["beforeBuild"]);
        }
    }
}
