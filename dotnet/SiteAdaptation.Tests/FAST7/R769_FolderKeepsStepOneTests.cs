#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R769, F24 of the short check of 7 Nov. The config is an input of step 1. The same folder chosen again leaves it as it is, so step 1 stays done
    // and the read runs, and a new folder runs step 1 again before the read. Made up values only
    public sealed class R769_FolderKeepsStepOneTests : IDisposable
    {
        private readonly string workRoot = Path.Combine(Path.GetTempPath(), "R769_" + Guid.NewGuid().ToString("N"));

        public R769_FolderKeepsStepOneTests()
        {
            Directory.CreateDirectory(workRoot);
        }

        public void Dispose()
        {
            Directory.Delete(workRoot, true);
        }

        [Fact]
        public void R769_the_same_folder_chosen_again_leaves_the_config_byte_for_byte_and_a_new_one_writes_it()
        {
            string copy = Path.Combine(workRoot, ShippedSet.ProjectsFolder, "Made up", "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            string folderA = Path.Combine(workRoot, "folder A");
            string folderB = Path.Combine(workRoot, "folder B");
            Directory.CreateDirectory(folderA);
            Directory.CreateDirectory(folderB);
            // Written by hand in its own layout, so a rewrite of the same value would change its bytes
            File.WriteAllText(copy, "{ \"keep\": 7, \"inputRoot\": " + new JValue(folderA + Path.DirectorySeparatorChar).ToString(Newtonsoft.Json.Formatting.None) + " }");
            byte[] before = File.ReadAllBytes(copy);
            string work = Path.Combine(workRoot, "model");

            Assert.Null(ProjectFolderPick.Write(copy, workRoot, folderA, work));
            Assert.Equal(before, File.ReadAllBytes(copy));
            Assert.Null(ProjectFolderPick.Write(copy, workRoot, folderA.ToUpperInvariant(), work));
            Assert.Equal(before, File.ReadAllBytes(copy));

            // Broken on purpose: another folder is written
            Assert.Null(ProjectFolderPick.Write(copy, workRoot, folderB, work));
            Assert.NotEqual(before, File.ReadAllBytes(copy));
            Assert.Equal(Path.GetFullPath(folderB), (string)JObject.Parse(File.ReadAllText(copy))["inputRoot"]);
            Assert.Equal(7, (int)JObject.Parse(File.ReadAllText(copy))["keep"]);
        }

        [Fact]
        public void R769_after_the_folder_the_read_runs_only_when_step_1_is_done_on_the_config_as_it_is()
        {
            List<StepStatus> Steps(StepKind first)
            {
                var steps = new List<StepStatus> { new StepStatus(first, first == StepKind.Issues ? 2 : 0) };
                steps.AddRange(Enumerable.Range(1, 8).Select(before => new StepStatus(StepKind.NeedsStep, before)));
                return steps;
            }
            Assert.Equal(StagesViewModel.ReadDataStep, StagesViewModel.StepAfterFolder(Steps(StepKind.Done)));
            Assert.Equal(StagesViewModel.ReadDataStep, StagesViewModel.StepAfterFolder(Steps(StepKind.Issues)));
            // A new folder made step 1 out of date, so it runs again first, never the box "2 Read data needs step 1"
            Assert.Equal(StagesViewModel.SetUpStep, StagesViewModel.StepAfterFolder(Steps(StepKind.Ready)));
            Assert.Equal(StagesViewModel.SetUpStep, StagesViewModel.StepAfterFolder(Steps(StepKind.NeedsSettings)));
            Assert.Equal(StagesViewModel.SetUpStep, StagesViewModel.StepAfterFolder(new StepStatus[0]));
            Assert.Equal(StagesViewModel.SetUpStep, StagesViewModel.StepAfterFolder(null));

            // With the folder of before: the same folder keeps the statuses, another folder or none before runs step 1 though step 1 still says done
            Assert.Equal(StagesViewModel.ReadDataStep, StagesViewModel.StepAfterFolder(Steps(StepKind.Done), Path.Combine(Path.GetTempPath(), "made up", "folder A") + Path.DirectorySeparatorChar, Path.Combine(Path.GetTempPath(), "made up", "folder A").ToUpperInvariant()));
            Assert.Equal(StagesViewModel.SetUpStep, StagesViewModel.StepAfterFolder(Steps(StepKind.Done), Path.Combine(Path.GetTempPath(), "made up", "folder A"), Path.Combine(Path.GetTempPath(), "made up", "folder B")));
            Assert.Equal(StagesViewModel.SetUpStep, StagesViewModel.StepAfterFolder(Steps(StepKind.Done), null, Path.Combine(Path.GetTempPath(), "made up", "folder B")));
            Assert.Equal(StagesViewModel.SetUpStep, StagesViewModel.StepAfterFolder(Steps(StepKind.Ready), Path.Combine(Path.GetTempPath(), "made up", "folder A"), Path.Combine(Path.GetTempPath(), "made up", "folder A")));
        }
    }
}
#endif
