#if NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
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
        public void R761_on_an_empty_model_card_1_runs_set_up_site_and_choose_project_folder_waits_for_it()
        {
            Fast7Sta.Run(() =>
            {
                StagesPage page = Shots.LaidOutStages(false);
                var stages = (StagesViewModel)page.DataContext;
                stages.Model.SetSteps(Empty());

                Assert.False(stages.Card1.Command.CanExecute(null));
                Assert.Equal(StageText.SetUpBeforeFolder, stages.Card1.Line);
                Assert.True(stages.SetUpCommand.CanExecute(null));
                Assert.Equal(RibbonLayout.Button(1).Label, stages.SetUpButton);
                var asked = new List<int>();
                stages.Model.RunRequested += asked.Add;
                stages.SetUpCommand.Execute(null);
                Assert.Equal(new[] { 1 }, asked);

                // The button stands in card 1, before Choose project folder
                var card1 = (System.Windows.Controls.Border)page.FindName("Card1Box");
                var buttons = Fast7Sta.Visuals(card1).OfType<Button>().ToList();
                int setUp = buttons.IndexOf((Button)page.FindName("SetUpButton"));
                int choose = buttons.FindIndex(button => Equals(button.Content, StageText.ChooseFolder));
                Assert.True(setUp >= 0 && setUp < choose, setUp + " " + choose);

                // Once step 1 ran, Choose project folder may run and the line of the read comes back
                List<StepStatus> ran = Empty();
                ran[0] = new StepStatus(StepKind.Done);
                ran[1] = new StepStatus(StepKind.Ready);
                stages.Model.SetSteps(ran);
                Assert.True(stages.Card1.Command.CanExecute(null));
                Assert.Equal(StageText.NoRead, stages.Card1.Line);
                // Done with issues counts as run too
                ran[0] = new StepStatus(StepKind.Issues, 2);
                stages.Model.SetSteps(ran);
                Assert.True(stages.Card1.Command.CanExecute(null));
            });
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

                var card1 = (System.Windows.Controls.Border)page.FindName("Card1Box");
                Assert.Contains((Button)page.FindName("ReviewButton"), Fast7Sta.Visuals(card1).OfType<Button>());
            });
        }
    }
}
#endif
