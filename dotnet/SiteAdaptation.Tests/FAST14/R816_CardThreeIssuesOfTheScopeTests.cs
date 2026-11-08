#if NETFRAMEWORK
using System;
using System.Threading;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests.FAST14
{
    // R816, F34 of run 2 of 8 Nov. Card 2 said "Done, 881 issues", the issues of steps 4 to 8 on the scope, while card 3 said 2619, the issues
    // the build plan lists for the whole read. Once card 2 is done card 3 counts what card 2 counts. Made up counts only
    public sealed class R816_CardThreeIssuesOfTheScopeTests
    {
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

        private static StepStatus[] Steps(StepStatus eight)
        {
            return new[]
            {
                new StepStatus(StepKind.Done), new StepStatus(StepKind.Done), new StepStatus(StepKind.Done),
                new StepStatus(StepKind.Issues, 3), new StepStatus(StepKind.Issues, 40), new StepStatus(StepKind.Done),
                new StepStatus(StepKind.Issues, 5), eight, new StepStatus(StepKind.Ready),
            };
        }

        [Fact]
        public void R816_card_3_counts_the_issues_of_steps_4_to_8_on_the_scope_as_card_2_says_them()
        {
            OnSta(() =>
            {
                var stages = (StagesViewModel)Shots.LaidOutStages(false).DataContext;
                // The build plan of the whole read lists 300 issues
                var results = new StageResults { HasPlan = true };
                for (int issue = 0; issue < 300; issue++)
                {
                    results.Issues.Add(issue + ": made up");
                }
                stages.ResultsNow = () => results;
                stages.Model.SetSteps(Steps(new StepStatus(StepKind.Issues, 52)));
                stages.Refresh();

                Assert.Equal("Done, 100 issues", stages.Card2.Status);
                Assert.Equal("100 issues from the last build.", stages.IssuesLine);

                // Broken on purpose: with step 8 not run card 2 is not done, so card 3 keeps the count of the plan, as R689 gave it
                stages.Model.SetSteps(Steps(new StepStatus(StepKind.Ready)));
                stages.Refresh();
                Assert.NotEqual("Done, 100 issues", stages.Card2.Status);
                Assert.Equal("300 issues from the last build.", stages.IssuesLine);
            });
        }

        [Fact]
        public void R816_the_issues_of_a_stage_count_only_when_every_step_of_it_ran()
        {
            Assert.Equal(100, StageStatus.IssuesWhenDone(2, Steps(new StepStatus(StepKind.Issues, 52))));
            Assert.Equal(48, StageStatus.IssuesWhenDone(2, Steps(new StepStatus(StepKind.Done))));
            Assert.Null(StageStatus.IssuesWhenDone(2, Steps(new StepStatus(StepKind.NeedsStep, 7))));
            Assert.Null(StageStatus.IssuesWhenDone(2, new StepStatus[0]));
        }
    }
}
#endif
