#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R680, R681. The pane in 3 stages of FAST-1-build-1, made up values only. The add-in is a net48 WPF library, so these run on net48
    public sealed class R680_StagePaneTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R680_" + Guid.NewGuid().ToString("N"));

        public R680_StagePaneTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static List<StepStatus> Steps(params StepKind[] kinds)
        {
            return kinds.Select(kind => new StepStatus(kind, kind == StepKind.Issues ? 2 : kind == StepKind.NeedsStep ? 3 : 0)).ToList();
        }

        [Fact]
        public void R680_a_stage_reads_the_steps_of_version_1_it_holds()
        {
            var none = Steps(Enumerable.Repeat(StepKind.NeedsSettings, 9).ToArray());
            Assert.Equal(StageText.NoDocument, StageStatus.Of(1, none, false, false));
            Assert.Equal(StageText.SetUpFirst, StageStatus.Of(1, none, true, false));
            var steps = Steps(StepKind.Done, StepKind.Done, StepKind.Issues, StepKind.Ready, StepKind.NeedsStep, StepKind.NeedsStep, StepKind.NeedsStep, StepKind.NeedsStep, StepKind.NeedsStep);
            Assert.Equal("Done, 2 issues", StageStatus.Of(1, steps, true, true));
            Assert.Equal(StageText.Ready, StageStatus.Of(2, steps, true, true));
            // Step 9 waits on step 3 of stage 1
            Assert.Equal("Waits for stage 1", StageStatus.Of(3, steps, true, true));
            var done = Steps(Enumerable.Repeat(StepKind.Done, 9).ToArray());
            Assert.Equal(StageText.Done, StageStatus.Of(2, done, true, true));
            // Broken on purpose: one step of stage 2 not done keeps the stage from Done
            done[6] = new StepStatus(StepKind.Ready);
            Assert.Equal(StageText.Ready, StageStatus.Of(2, done, true, true));
        }

        [Fact]
        public void R681_the_latest_read_is_the_newest_folder_holding_a_data_check_and_a_proposal()
        {
            Assert.Null(StageRead.Latest(folder));
            Assert.Null(StageRead.Latest(Path.Combine(folder, "none")));
            string older = Path.Combine(folder, "read-a");
            string newer = Path.Combine(folder, "read-b");
            string half = Path.Combine(folder, "read-c");
            foreach (string read in new[] { older, newer, half })
            {
                Directory.CreateDirectory(read);
                File.WriteAllText(Path.Combine(read, "data_check.json"), "{}");
            }
            File.WriteAllText(Path.Combine(older, "project_profile.json"), "{}");
            File.WriteAllText(Path.Combine(newer, "project_profile.json"), "{}");
            File.SetLastWriteTimeUtc(Path.Combine(older, "data_check.json"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(newer, "data_check.json"), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(half, "data_check.json"), new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(newer, StageRead.Latest(folder));
            Assert.Empty(StageRead.Read(null).Rows);
            Assert.Null(StageRead.Read(null).Stopped);
            // A data check that does not read gives no row, no throw and the reason
            Assert.Empty(StageRead.Read(older).Rows);
            Assert.Equal(AddIn.ScanResults.DataCheckRows.NoNeeds, StageRead.Read(older).Stopped);
            File.WriteAllText(Path.Combine(newer, "data_check.json"), StoppedCheck);
            Assert.Equal("Made up reason", StageRead.Read(newer).Stopped);
            Assert.Single(StageRead.Read(newer).Rows);
        }

        private const string StoppedCheck = "{\"stopped\":\"Made up reason\",\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"weak\",\"line\":\"Made up\"}]}";

        // Review 1 of PR 228: the worker writes data_check.json when the check stops too. The card says why and Fix items stays off,
        // as the Confirm window of R2-C1 refuses a stopped check. Broken on purpose against a check that finished
        [Fact]
        public void R681_a_stopped_read_says_why_in_card_1_and_fix_items_stays_off()
        {
            string work = Path.Combine(folder, "work");
            string read = Path.Combine(work, "read-a");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, "project_profile.json"), "{}");
            File.WriteAllText(Path.Combine(read, "data_check.json"), StoppedCheck);
            OnSta(() =>
            {
                StagesPage page = Shots.LaidOutStages(false);
                var stages = (StagesViewModel)page.DataContext;
                stages.WorkFolderNow = () => work;
                stages.Refresh();
                Assert.Equal(TextTemplate.Fill(StageText.ReadStopped, "Made up reason"), stages.Card1.Line);
                Assert.False(stages.FixItemsCommand.CanExecute(null));
                Assert.True(stages.HasRows);
                File.WriteAllText(Path.Combine(read, "data_check.json"), StoppedCheck.Replace("\"stopped\":\"Made up reason\",", ""));
                File.SetLastWriteTimeUtc(Path.Combine(read, "data_check.json"), DateTime.UtcNow.AddMinutes(1));
                stages.Refresh();
                Assert.Equal(string.Empty, stages.Card1.Line);
                Assert.True(stages.FixItemsCommand.CanExecute(null));
            });
        }

        [Fact]
        public void R681_choose_project_folder_writes_only_the_config_copy_of_a_new_project()
        {
            string workRoot = Path.Combine(folder, "work");
            string copy = Path.Combine(workRoot, "New projects", "Made up", "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.WriteAllText(copy, new JObject { ["inputRoot"] = "C:\\old", ["keep"] = 7 }.ToString());
            string shipped = Path.Combine(folder, "ShippedSet", "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(shipped));
            File.WriteAllText(shipped, "{}");
            string chosen = Path.Combine(folder, "project folder");
            Directory.CreateDirectory(chosen);
            string work = Path.Combine(workRoot, "model");

            Assert.Equal(StageText.FolderOwnCopyOnly, ProjectFolderPick.Write(shipped, workRoot, chosen, work));
            Assert.Equal("{}", File.ReadAllText(shipped));
            Assert.Equal(StageText.FolderHoldsWork, ProjectFolderPick.Write(copy, workRoot, folder, work));
            Assert.Null(ProjectFolderPick.Write(copy, workRoot, chosen, work));
            JObject written = JObject.Parse(File.ReadAllText(copy));
            Assert.Equal(Path.GetFullPath(chosen), (string)written["inputRoot"]);
            Assert.Equal(7, (int)written["keep"]);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(copy), "*.choosing"));
        }

        [Fact]
        public void R680_the_pane_shows_3_cards_in_order_settings_closed_and_advanced_closed_holding_the_old_pane()
        {
            OnSta(() =>
            {
                StagesPage page = Shots.LaidOutStages(false);
                var stages = (StagesViewModel)page.DataContext;
                var titles = Visuals(page).OfType<TextBlock>().Select(text => text.Text).ToList();
                int one = titles.IndexOf(StageText.Stage1);
                int two = titles.IndexOf(StageText.Stage2);
                int three = titles.IndexOf(StageText.Stage3);
                Assert.True(one >= 0 && one < two && two < three, string.Join("|", titles));
                Expander advanced = Visuals(page).OfType<Expander>().Single();
                Assert.Equal(StageText.Advanced, advanced.Header);
                Assert.False(advanced.IsExpanded);
                // A Frame gives back its page once it navigates, which the dispatcher runs
                var frame = (Frame)page.FindName("OldPane");
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.IsType<SitePanePage>(frame.Content);
                Assert.Equal(9, ((SitePanePage)frame.Content).DataContext is PaneViewModel model ? model.Steps.Count : 0);
                Assert.False(stages.SettingsOpen);
                stages.SettingsCommand.Execute(null);
                Assert.True(stages.SettingsOpen);
                // Build is not wired yet, so its button cannot run and the card says so
                Assert.False(stages.Card2.Command.CanExecute(null));
                Assert.Equal(StageText.BuildNotWired, stages.Card2.Line);
                Assert.Equal(new[] { StageText.ChooseFolder, StageText.Build, StageText.Report }, stages.Cards.Select(card => card.Button).ToArray());
                Assert.False(stages.HasRows);
                Assert.False(stages.FixItemsCommand.CanExecute(null));
                // Review of Lead on PR 228: a fresh pane with no read says so in card 1 and the line is drawn
                Assert.Equal(StageText.NoRead, stages.Card1.Line);
                Assert.Contains(StageText.NoRead, Visuals(page).OfType<TextBlock>().Select(text => text.Text));
            });
        }

        // Review of Lead on PR 228: the Revit test host finds the docked page, which is the pane in 3 stages since FAST-1
        [Fact]
        public void R680_the_revit_test_host_looks_for_the_stages_page()
        {
            string probe = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", "AddinProbe.cs"));
            Assert.Contains("DockedPage = \"" + typeof(StagesPage).FullName + "\"", probe);
            Assert.Contains("TypeOf(DockedPage)", probe);
        }

        [Fact]
        public void R680_report_asks_for_step_9_and_choose_folder_asks_the_revit_side()
        {
            OnSta(() =>
            {
                StagesPage page = Shots.LaidOutStages(true);
                var stages = (StagesViewModel)page.DataContext;
                var asked = new List<int>();
                stages.Model.RunRequested += asked.Add;
                bool chose = false;
                stages.ChooseFolderRequested += () => chose = true;
                stages.Card3.Command.Execute(null);
                stages.Card1.Command.Execute(null);
                Assert.Equal(new[] { 9 }, asked);
                Assert.True(chose);
                Assert.True(stages.HasRows);
                Assert.Equal(3, Visuals(page).OfType<ContentPresenter>().Count(presenter => presenter.Content is AddIn.ScanResults.DataCheckRow));
                // Review of Lead on PR 228: the plain line of each need is drawn under it, not a tooltip alone
                List<string> drawn = Visuals(page).OfType<TextBlock>().Select(text => text.Text).ToList();
                foreach (AddIn.ScanResults.DataCheckRow row in stages.CheckRows)
                {
                    Assert.Contains(row.Line, drawn);
                }
            });
        }

        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int at = 0; at < count; at++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, at);
                yield return child;
                foreach (DependencyObject below in Visuals(child))
                {
                    yield return below;
                }
            }
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
            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException("on the STA thread: " + failure);
            }
        }
    }
}
#endif
