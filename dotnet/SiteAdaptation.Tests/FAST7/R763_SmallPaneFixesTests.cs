#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R763, F9 of run 1 of FAST-3: the gear of Settings, Report grey while card 3 waits, the folder dialog opens at the folder chosen before.
    // Made up values only
    public sealed class R763_SmallPaneFixesTests : IDisposable
    {
        private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "R763_" + Guid.NewGuid().ToString("N"));

        public R763_SmallPaneFixesTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            DialogPicker.Start = null;
            Directory.Delete(folder, true);
        }

        [Fact]
        public void R763_settings_is_a_gear_named_settings_and_opens_the_settings()
        {
            Fast7Sta.Run(() =>
            {
                StagesPage page = Shots.LaidOutStages(false);
                var stages = (StagesViewModel)page.DataContext;
                var gear = (Button)page.FindName("SettingsGear");
                Assert.IsType<System.Windows.Shapes.Path>(gear.Content);
                Assert.Equal(StageText.SettingsButton, gear.ToolTip);
                Assert.Equal(StageText.SettingsButton, AutomationProperties.GetName(gear));
                // No button of the head says the word, the gear stands for it
                Assert.DoesNotContain(Fast7Sta.Visuals(page).OfType<Button>(), button => Equals(button.Content, StageText.SettingsButton));
                Assert.False(stages.SettingsOpen);
                gear.Command.Execute(null);
                Assert.True(stages.SettingsOpen);
            });
        }

        [Fact]
        public void R763_report_is_grey_while_step_9_waits_and_runs_once_check_is_done()
        {
            Fast7Sta.Run(() =>
            {
                StagesPage page = Shots.LaidOutStages(false);
                var stages = (StagesViewModel)page.DataContext;
                var steps = new List<StepStatus> { new StepStatus(StepKind.Done), new StepStatus(StepKind.Done), new StepStatus(StepKind.Done), new StepStatus(StepKind.Ready) };
                steps.AddRange(Enumerable.Range(4, 4).Select(before => new StepStatus(StepKind.NeedsStep, before)));
                steps.Add(new StepStatus(StepKind.NeedsStep, 4));
                stages.Model.SetSteps(steps);
                Assert.Equal(TextTemplate.Fill(StageText.Waits, 2), stages.Card3.Status);
                Assert.False(stages.Card3.Command.CanExecute(null));

                steps[3] = new StepStatus(StepKind.Issues, 3);
                steps[8] = new StepStatus(StepKind.Ready);
                stages.Model.SetSteps(steps);
                Assert.True(stages.Card3.Command.CanExecute(null));
            });
        }

        [Fact]
        public void R763_the_folder_dialog_opens_at_the_project_folder_of_the_config()
        {
            string config = System.IO.Path.Combine(folder, "project.json");
            string chosen = System.IO.Path.Combine(folder, "made up project");
            File.WriteAllText(config, new JObject { ["inputRoot"] = chosen }.ToString());
            Assert.Equal(chosen, ProjectFolderPick.Chosen(config));
            Assert.Null(ProjectFolderPick.Chosen(null));
            Assert.Null(ProjectFolderPick.Chosen(System.IO.Path.Combine(folder, "none.json")));
            File.WriteAllText(config, "{ not json");
            Assert.Null(ProjectFolderPick.Chosen(config));
            File.WriteAllText(config, new JObject { ["inputRoot"] = "" }.ToString());
            Assert.Null(ProjectFolderPick.Chosen(config));
            File.WriteAllText(config, new JObject { ["inputRoot"] = new JObject() }.ToString());
            Assert.Null(ProjectFolderPick.Chosen(config));
        }

        [Fact]
        public void R763_choose_project_folder_sets_the_start_of_the_dialog_before_the_revit_side_opens_it()
        {
            Fast7Sta.Run(() =>
            {
                StagesPage page = Shots.LaidOutStages(false);
                var stages = (StagesViewModel)page.DataContext;
                // The sample settings name a config with no project folder, so the folder picked before in the session stays
                DialogPicker.Start = folder;
                string seen = null;
                stages.ChooseFolderRequested += () => seen = DialogPicker.Start;
                stages.Card1.Command.Execute(null);
                Assert.Equal(ProjectFolderPick.Chosen(stages.Model.Settings.ConfigFile) ?? folder, seen);
            });
        }
    }
}
#endif
