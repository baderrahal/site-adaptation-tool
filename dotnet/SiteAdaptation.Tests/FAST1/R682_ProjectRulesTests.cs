#if NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.Common;
using SiteAdaptation.Common.ProjectRules;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R682, R683, R684. The rules table of a project and its amber line, on a made up project in the temp folder and the manual template of the repo
    public sealed class R682_ProjectRulesTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "R682_" + Guid.NewGuid().ToString("N"));

        public R682_ProjectRulesTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private ProjectRulesPlace Place(string projects = "New projects")
        {
            string workRoot = Path.Combine(root, "work");
            string project = Path.Combine(workRoot, projects, "Made up");
            string config = Path.Combine(project, "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(config));
            Directory.CreateDirectory(Path.Combine(project, "rules"));
            JArray ids = new JArray(Enumerable.Range(1, 15).Select(at => "M" + at.ToString("00")).ToArray());
            // The blocks the runner of the worker reads, as RulesCommandTests gives them, made up values
            File.WriteAllText(config, new JObject
            {
                ["inputRoot"] = Path.Combine(root, "input"),
                ["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 },
                ["addin"] = new JObject { ["workerTimeLimitMinS"] = 0.5, ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) } },
                ["designBasis"] = new JObject { ["mode"] = "made up new", ["modes"] = new JArray("made up old", "made up new") },
                ["projectRules"] = new JObject { ["manualName"] = "a made up manual", ["pastJobMode"] = "made up old", ["ruleIds"] = ids },
            }.ToString());
            string work = Path.Combine(workRoot, "model");
            return new ProjectRulesPlace
            {
                ConfigFile = config, RulesProfile = Path.Combine(project, "rules", "profile.json"), WorkRoot = workRoot, WorkFolder = work,
                TemplateFolder = Path.Combine(ThemeGateTests.RepoRoot(), "rules", "templates"), Who = "made up person",
                Document = new WorkerDocument { Path = Path.Combine(work, "a.rvt"), SessionId = "s1" },
            };
        }

        [Fact]
        public void R682_the_names_of_the_rules_file_are_those_of_common()
        {
            Assert.Equal(ProjectRulesFile.FileName, RulesFileNames.FileName);
            Assert.Equal(ProjectRulesFile.LogName, RulesFileNames.LogName);
            Assert.Equal(ProjectRulesFile.Confirmed, RulesFileNames.Confirmed);
            Assert.Equal(ProjectRulesFile.BeforeBuild, RulesFileNames.BeforeBuild);
            Assert.Equal(ProjectRulesFile.Warn, RulesFileNames.Warn);
            Assert.Equal(ProjectRulesFile.Block, RulesFileNames.Block);
            foreach (JObject rule in ProjectRulesFile.FixedRules())
            {
                Assert.True(RulesFileNames.IsFixed((string)rule["id"]), (string)rule["id"]);
            }
            Assert.False(RulesFileNames.IsFixed("M01"));
            Assert.False(ProjectRulesFile.IsFixed(RulesFileNames.BeforeBuild));
        }

        [Fact]
        public void R682_a_typed_value_takes_the_kind_of_the_value_it_replaces()
        {
            Assert.Equal(JTokenType.Integer, ProjectRulesTable.Parse("5", new JValue(3), out _).Type);
            Assert.Equal(1.5, (double)ProjectRulesTable.Parse("1.5", null, out _));
            Assert.Equal("abc", (string)ProjectRulesTable.Parse(" abc ", null, out _));
            Assert.Equal("5", (string)ProjectRulesTable.Parse("5", new JValue("text"), out _));
            Assert.Equal(new JArray(1, 2), ProjectRulesTable.Parse("[1,2]", new JArray(3), out _));
            // Broken on purpose: a list that does not read gives no value and the reason
            Assert.Null(ProjectRulesTable.Parse("[1,", new JArray(3), out string why));
            Assert.False(string.IsNullOrEmpty(why));
            Assert.Null(ProjectRulesTable.Parse("{}", new JArray(3), out _));
            Assert.Equal(string.Empty, ProjectRulesTable.ValueText(null));
            Assert.Equal("[1,2]", ProjectRulesTable.ValueText(new JArray(1, 2)));
        }

        [Fact]
        public void R682_only_a_new_projects_copy_under_the_work_root_takes_rules()
        {
            ProjectRulesPlace shipped = Place("ShippedSet");
            Assert.Equal(ProjectRulesText.OwnCopyOnly, ProjectRulesTable.Refusal(shipped));
            Assert.Equal(ProjectRulesText.OwnCopyOnly, ProjectRulesTable.UseManual(shipped));
            Assert.False(File.Exists(shipped.RulesFile));
            ProjectRulesPlace unsaved = Place();
            unsaved.WorkFolder = null;
            Assert.Equal(ProjectRulesText.NotSaved, ProjectRulesTable.Refusal(unsaved));
            ProjectRulesPlace noTemplate = Place();
            noTemplate.TemplateFolder = Path.Combine(root, "none");
            Assert.Equal(ProjectRulesText.NoTemplate, ProjectRulesTable.UseManual(noTemplate));
            Assert.False(File.Exists(noTemplate.RulesFile));
        }

        [Fact]
        public void R682_R683_the_manual_template_then_a_confirm_a_change_and_before_build_go_through_the_commands_of_common()
        {
            ProjectRulesPlace place = Place();
            Assert.Equal(ProjectRulesText.AmberNone, ProjectRulesTable.AmberLine(ProjectRulesTable.Read(place), false));
            var model = new ProjectRulesViewModel(place);
            Assert.True(model.NoRules);
            Assert.Equal(15, model.Rows.Count);
            Assert.All(model.Rows, row => Assert.False(row.Editable));

            model.UseManualCommand.Execute(null);
            Assert.True(model.Line == ProjectRulesText.Chosen, model.Line);
            Assert.True(model.HasRules);
            Assert.True(File.Exists(place.RulesFile));
            Assert.Equal("15 of 15 rules are not confirmed. Open Rules in Settings.", ProjectRulesTable.AmberLine(ProjectRulesTable.Read(place), true));
            // A second setup is refused by the command, nothing changed
            Assert.StartsWith("Nothing changed: ", ProjectRulesTable.UseManual(place));

            ProjectRuleRow first = model.Rows.First(row => row.Id == "M01");
            Assert.Equal(ProjectRulesText.Confirm, first.ButtonText);
            first.SaveCommand.Execute(null);
            Assert.Equal(TextTemplate.Fill(ProjectRulesText.Saved, first.Name), model.Line);
            ProjectRuleRow confirmed = model.Rows.First(row => row.Id == "M01");
            Assert.Equal(RulesFileNames.Confirmed, confirmed.Status);
            Assert.Equal("rule", confirmed.Source);

            ProjectRuleRow second = model.Rows.First(row => row.Id == "M02");
            second.Text = "A made up value";
            Assert.Equal(ProjectRulesText.Save, second.ButtonText);
            second.SaveCommand.Execute(null);
            ProjectRuleRow changed = model.Rows.First(row => row.Id == "M02");
            Assert.Equal("A made up value", (string)changed.Value);
            Assert.Equal("person entered", changed.Source);
            Assert.Equal(RulesFileNames.Confirmed, changed.Status);
            Assert.Equal("13 of 15 rules are not confirmed. Open Rules in Settings.", ProjectRulesTable.AmberLine(ProjectRulesTable.Read(place), true));

            // The rows are the rules a build takes, ruleIds of config. The fixed rules of R663 are no row, RulesForBuild.Check keeps them
            Assert.Equal(15, model.Rows.Count);
            Assert.DoesNotContain(model.Rows, row => row.Fixed);

            model.BeforeBuildIndex = 1;
            Assert.Equal(RulesFileNames.Block, ProjectRulesTable.BeforeBuild(place));
            Assert.Equal(1, model.BeforeBuildIndex);
            Assert.Equal(TextTemplate.Fill(ProjectRulesText.Saved, ProjectRulesText.BeforeBuild), model.Line);
            Assert.Equal(4, File.ReadAllLines(Path.Combine(place.Folder, RulesFileNames.LogName)).Length);

            // Review of PR 2: a config copy with no rule list stops a build, so the amber line says why and is never empty
            JObject config = JObject.Parse(File.ReadAllText(place.ConfigFile));
            ((JObject)config["projectRules"]).Remove("ruleIds");
            File.WriteAllText(place.ConfigFile, config.ToString());
            RulesForBuildAnswer blocked = ProjectRulesTable.Read(place);
            Assert.False(string.IsNullOrEmpty(blocked.BlockingLine));
            Assert.Equal(blocked.BlockingLine, ProjectRulesTable.AmberLine(blocked, true));
            // A hand edit that makes Before Build a list reads as warn and throws nothing
            JObject rules = JObject.Parse(File.ReadAllText(place.RulesFile));
            rules[RulesFileNames.BeforeBuild] = new JArray("block");
            File.WriteAllText(place.RulesFile, rules.ToString());
            Assert.Equal(RulesFileNames.Warn, ProjectRulesTable.BeforeBuild(place));
        }

        // The draft shot of the window: a set up project shows each rule with its value and the line of the last change
        [Fact]
        public void R682_the_window_lays_out_the_rules_of_a_set_up_project()
        {
            OnSta(() =>
            {
                ProjectRulesWindow window = Shots.LaidOutProjectRules();
                var model = (ProjectRulesViewModel)window.DataContext;
                Assert.True(model.HasRules);
                Assert.Equal(4, model.Rows.Count);
                Assert.Equal(RulesFileNames.Confirmed, model.Rows[0].Status);
                Assert.Equal(TextTemplate.Fill(ProjectRulesText.Saved, model.Rows[0].Name), model.Line);
                Assert.Equal(4, ((System.Windows.Controls.ItemsControl)window.FindName("RuleRows")).Items.Count);
                window.Close();
            });
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.True(failure == null, failure?.ToString());
        }

        [Fact]
        public void R683_the_pane_shows_one_amber_line_while_rules_are_not_confirmed()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var stages = (StagesViewModel)Shots.LaidOutStages(false).DataContext;
                    Assert.False(stages.HasRulesLine);
                    string line = "2 of 15 rules are not confirmed. Open Rules in Settings.";
                    stages.RulesLineNow = () => line;
                    stages.Refresh();
                    Assert.True(stages.HasRulesLine);
                    Assert.Equal(line, stages.RulesLine);
                    bool asked = false;
                    stages.RulesRequested += () => asked = true;
                    stages.RulesCommand.Execute(null);
                    Assert.True(asked);
                    line = null;
                    stages.Refresh();
                    Assert.False(stages.HasRulesLine);
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.True(failure == null, failure?.ToString());
        }
    }
}
#endif
