#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
using Newtonsoft.Json.Linq;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R761 and R762, F1 and F3 of run 1 of FAST-3. Card 1 holds step 1 Set up site and step 3 Review data, made up values only
    public sealed class R761_CardOneStepsTests
    {
        // A model with no step run yet, as an empty model shows it: step 1 ready, every later step waits on the one before
        private static List<StepStatus> Empty()
        {
            var steps = new List<StepStatus> { new StepStatus(StepKind.Ready) };
            steps.AddRange(Enumerable.Range(1, 8).Select(before => new StepStatus(StepKind.NeedsStep, before)));
            return steps;
        }

        [Fact]
        public void R761_on_an_empty_model_card_1_runs_set_up_site_and_choose_project_folder_runs_it_first()
        {
            string workRoot = Path.Combine(Path.GetTempPath(), "R761_" + Guid.NewGuid().ToString("N"));
            string copy = Path.Combine(workRoot, Common.Settings.ShippedSet.ProjectsFolder, "Made up", "config", "project.json");
            string chosen = Path.Combine(workRoot, "made up project folder");
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            Directory.CreateDirectory(chosen);
            File.WriteAllText(copy, new JObject { ["keep"] = 7 }.ToString());
            try
            {
                Fast7Sta.Run(() =>
                {
                    StagesPage page = Shots.LaidOutStages(false);
                    var stages = (StagesViewModel)page.DataContext;
                    stages.Model.SetSteps(Empty());

                    Assert.True(stages.Card1.Command.CanExecute(null));
                    Assert.Equal(StageText.SetUpBeforeFolder, stages.Card1.Line);
                    Assert.True(stages.SetUpCommand.CanExecute(null));
                    Assert.Equal(RibbonLayout.Button(1).Label, stages.SetUpButton);
                    var asked = new List<int>();
                    stages.Model.RunRequested += asked.Add;
                    stages.SetUpCommand.Execute(null);
                    Assert.Equal(new[] { 1 }, asked);

                    // The button stands in card 1, before Choose project folder
                    var card1 = (Border)page.FindName("Card1Box");
                    var buttons = Fast7Sta.Visuals(card1).OfType<Button>().ToList();
                    int setUp = buttons.IndexOf((Button)page.FindName("SetUpButton"));
                    int choose = buttons.FindIndex(button => Equals(button.Content, StageText.ChooseFolder));
                    Assert.True(setUp >= 0 && setUp < choose, setUp + " " + choose);

                    // R768. Before step 1, Choose project folder writes the folder into the config copy and runs step 1, never the read
                    bool readAsked = false;
                    stages.ChooseFolderRequested += () => readAsked = true;
                    stages.WorkRootNow = () => workRoot;
                    stages.PickFolder = () => chosen;
                    stages.Model.Settings.ConfigFile = copy;
                    asked.Clear();
                    stages.Card1.Command.Execute(null);
                    Assert.Equal(new[] { 1 }, asked);
                    Assert.False(readAsked);
                    Assert.Equal(Path.GetFullPath(chosen), (string)JObject.Parse(File.ReadAllText(copy))["inputRoot"]);
                    Assert.Equal(7, (int)JObject.Parse(File.ReadAllText(copy))["keep"]);

                    // Broken on purpose: a config that is no copy of a new project is refused, said in card 1, and nothing runs
                    stages.Model.Settings.ConfigFile = Path.Combine(Path.GetTempPath(), "not a copy", "project.json");
                    asked.Clear();
                    stages.Card1.Command.Execute(null);
                    Assert.Empty(asked);
                    Assert.Equal(StageText.FolderOwnCopyOnly, stages.Card1.Line);
                    // Cancel of the dialog clears the line and runs nothing
                    stages.PickFolder = () => null;
                    stages.Card1.Command.Execute(null);
                    Assert.Empty(asked);
                    Assert.Equal(StageText.SetUpBeforeFolder, stages.Card1.Line);

                    // Once step 1 ran, Choose project folder asks the Revit side for the read, and the line of the read comes back
                    List<StepStatus> ran = Empty();
                    ran[0] = new StepStatus(StepKind.Done);
                    ran[1] = new StepStatus(StepKind.Ready);
                    stages.Model.SetSteps(ran);
                    Assert.Equal(StageText.NoRead, stages.Card1.Line);
                    stages.Card1.Command.Execute(null);
                    Assert.True(readAsked);
                    Assert.Empty(asked);
                    // Done with issues counts as run too
                    readAsked = false;
                    ran[0] = new StepStatus(StepKind.Issues, 2);
                    stages.Model.SetSteps(ran);
                    stages.Card1.Command.Execute(null);
                    Assert.True(readAsked);
                });
            }
            finally
            {
                Directory.Delete(workRoot, true);
            }
        }

        [Fact]
        public void R762_card_1_runs_review_data_once_the_read_is_done_and_not_before()
        {
            Fast7Sta.Run(() =>
            {
                StagesPage page = Shots.LaidOutStages(false);
                var stages = (StagesViewModel)page.DataContext;
                stages.Model.SetSteps(Empty());
                Assert.False(stages.ReviewCommand.CanExecute(null));

                List<StepStatus> read = Empty();
                read[0] = new StepStatus(StepKind.Done);
                read[1] = new StepStatus(StepKind.Done);
                read[2] = new StepStatus(StepKind.Ready);
                stages.Model.SetSteps(read);
                Assert.True(stages.ReviewCommand.CanExecute(null));
                Assert.Equal(RibbonLayout.Button(3).Label, stages.ReviewButton);
                var asked = new List<int>();
                stages.Model.RunRequested += asked.Add;
                stages.ReviewCommand.Execute(null);
                Assert.Equal(new[] { 3 }, asked);

                var card1 = (Border)page.FindName("Card1Box");
                Assert.Contains((Button)page.FindName("ReviewButton"), Fast7Sta.Visuals(card1).OfType<Button>());
            });
        }
    }
}
#endif
