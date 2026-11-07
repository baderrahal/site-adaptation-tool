#if NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R765, F7 of run 1 of FAST-3. A model closed without save after Save setup has a read in its work folder and no setup. The cards say
    // the setup was not saved, never "Set up this project first" over the rows of a read. Made up values only
    public sealed class R765_SetupNotSavedTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "R765_" + Guid.NewGuid().ToString("N"));

        public R765_SetupNotSavedTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        [Fact]
        public void R765_with_no_setup_a_read_in_the_work_folder_turns_the_status_into_the_setup_was_not_saved()
        {
            var none = Enumerable.Repeat(new StepStatus(StepKind.NeedsSettings), 9).ToList();
            Assert.Equal(StageText.SetUpFirst, StageStatus.Of(1, none, true, false));
            Assert.Equal(StageText.SetUpFirst, StageStatus.Of(1, none, true, false, false));
            Assert.Equal(StageText.SetupNotSaved, StageStatus.Of(1, none, true, false, true));
            Assert.Equal(StageText.SetupNotSaved, StageStatus.Of(3, none, true, false, true));
            // No document is said first, a setup chosen goes by the steps as before
            Assert.Equal(StageText.NoDocument, StageStatus.Of(1, none, false, false, true));
            var done = Enumerable.Repeat(new StepStatus(StepKind.Done), 9).ToList();
            Assert.Equal(StageText.Done, StageStatus.Of(1, done, true, true, true));
        }

        [Fact]
        public void R765_the_pane_with_no_setup_shows_the_rows_of_the_read_under_the_line_that_the_setup_was_not_saved()
        {
            string read = Path.Combine(work, "read-a");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, "project_profile.json"), "{}");
            File.WriteAllText(Path.Combine(read, "data_check.json"), "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Made up line.\"}],\"files\":[]}");
            Fast7Sta.Run(() =>
            {
                // The sample pane with its setup gone, as a model closed without save opens again
                StagesPage page = SiteAdaptation.UiShots.Shots.LaidOutStages(false);
                PaneViewModel model = ((StagesViewModel)page.DataContext).Model;
                model.LoadSettings(new PaneSettings());
                var stages = new StagesViewModel(model) { WorkFolderNow = () => work };
                stages.Refresh();
                Assert.False(model.InUse);
                Assert.True(stages.HasRows);
                Assert.All(stages.Cards, card => Assert.Equal(StageText.SetupNotSaved, card.Status));
                Assert.DoesNotContain(stages.Cards, card => card.Status == StageText.SetUpFirst);

                // Broken on purpose: with the read gone the old words come back
                Directory.Delete(read, true);
                var fresh = new StagesViewModel(model) { WorkFolderNow = () => work };
                fresh.Refresh();
                Assert.All(fresh.Cards, card => Assert.Equal(StageText.SetUpFirst, card.Status));
            });
        }
    }
}
#endif
