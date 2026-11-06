#if NETFRAMEWORK
using System.Collections.Generic;
using System.IO;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R688. The one Build runs set up site, check, floors and ramps, levels, villas and walls one after another and stops only on a step that did not end well
    public sealed class R688_BuildChainTests
    {
        // The state of the 9 steps, done by default, which a test changes as a run of a step would
        private readonly Dictionary<int, StepStatus> state = new Dictionary<int, StepStatus>();

        private BuildChain Chain()
        {
            return new BuildChain(number => "Step" + number, number => state.TryGetValue(number, out StepStatus held) ? held : new StepStatus(StepKind.Done));
        }

        // Runs the build to its end, each step ending well, and gives the steps in the order they ran and the last line
        private static List<int> RunAll(BuildChain chain, int first, out string last, string messageAt5 = null)
        {
            var ran = new List<int> { first };
            int current = first;
            last = null;
            while (true)
            {
                Assert.True(chain.Ended(current, RunEnd.Committed, current == 5 ? messageAt5 : null, out int? next, out last));
                if (!next.HasValue)
                {
                    return ran;
                }
                current = next.Value;
                ran.Add(current);
            }
        }

        [Fact]
        public void R688_on_a_project_whose_stage_1_is_done_the_build_runs_check_to_walls_with_one_progress_line_and_says_the_messages()
        {
            BuildChain chain = Chain();
            Assert.Equal(4, chain.Start());
            Assert.Equal("Step 1 of 5: Step4.", chain.Progress);
            // A second Build while one runs starts nothing
            Assert.Null(chain.Start());
            List<int> ran = RunAll(chain, 4, out string last, "Made up message");
            Assert.Equal(new[] { 4, 5, 6, 7, 8 }, ran.ToArray());
            // The message of a step that ended well is said at the end, not only counted
            Assert.Equal("Build done: 5 steps, 1 issues. Step5: Made up message", last);
            Assert.False(chain.Running);
        }

        [Fact]
        public void R688_set_up_site_runs_when_it_has_not_run_and_a_step_that_waits_on_an_earlier_one_runs_it_first()
        {
            state[1] = new StepStatus(StepKind.Ready);
            BuildChain chain = Chain();
            Assert.Equal(1, chain.Start());
            Assert.Equal("Step 1 of 6: Step1.", chain.Progress);
            // Review of PR 5: set up site leaves the read and the review out of date, so check waits on step 3, which waits on step 2
            state[1] = new StepStatus(StepKind.Done);
            state[4] = new StepStatus(StepKind.NeedsStep, 3);
            state[3] = new StepStatus(StepKind.NeedsStep, 2);
            state[2] = new StepStatus(StepKind.Ready);
            Assert.True(chain.Ended(1, RunEnd.Committed, null, out int? next, out string line));
            Assert.Equal(2, next);
            Assert.Equal("Step 2 of 8: Step2.", line);
            state[2] = new StepStatus(StepKind.Done);
            state[3] = new StepStatus(StepKind.Ready);
            Assert.True(chain.Ended(2, RunEnd.Committed, null, out next, out line));
            Assert.Equal(3, next);
            state[3] = new StepStatus(StepKind.Done);
            state[4] = new StepStatus(StepKind.Ready);
            List<int> ran = RunAll(chain, 3, out string last);
            Assert.Equal(new[] { 3, 4, 5, 6, 7, 8 }, ran.ToArray());
            Assert.Equal("Build done: 8 steps, 0 issues.", last);
        }

        [Fact]
        public void R688_a_step_that_waits_on_a_later_step_adds_nothing_and_waits_run_back_to_the_first_step_waited_on()
        {
            // Broken on purpose: a status that names a later step adds nothing, the flow then stops the step with its reason
            state[4] = new StepStatus(StepKind.NeedsStep, 9);
            BuildChain chain = Chain();
            Assert.Equal(4, chain.Start());
            // Each wait names an earlier step, so the waits end at the first step that waits on none, here set up site that ran already
            state[4] = new StepStatus(StepKind.NeedsStep, 3);
            state[3] = new StepStatus(StepKind.NeedsStep, 2);
            state[2] = new StepStatus(StepKind.NeedsStep, 1);
            state[1] = new StepStatus(StepKind.Done);
            BuildChain waiting = Chain();
            Assert.Equal(1, waiting.Start());
            Assert.Equal("Step 1 of 8: Step1.", waiting.Progress);
        }

        [Fact]
        public void R688_a_step_that_did_not_end_well_stops_the_build_and_says_where_and_why()
        {
            BuildChain chain = Chain();
            chain.Start();
            Assert.True(chain.Ended(4, RunEnd.Committed, null, out int? next, out string line));
            Assert.Equal(5, next);
            // Work outside Revit: the build waits for the end
            Assert.True(chain.Ended(5, RunEnd.Started, null, out next, out line));
            Assert.Null(next);
            Assert.Equal("Step 2 of 5: Step5.", line);
            Assert.True(chain.Running);
            Assert.True(chain.Ended(5, RunEnd.Stopped, "Made up reason", out next, out line));
            Assert.Null(next);
            Assert.Equal("Build stopped at Step5: Made up reason", line);
            Assert.False(chain.Running);

            chain.Start();
            Assert.True(chain.Ended(4, RunEnd.Cancelled, null, out next, out line));
            Assert.Equal("Build stopped at Step4, cancelled.", line);

            chain.Start();
            Assert.True(chain.Ended(4, RunEnd.RolledBack, " ", out next, out line));
            Assert.Equal("Build stopped at Step4: " + SiteBuildText.NoReason, line);
        }

        [Fact]
        public void R688_the_end_of_a_run_that_is_no_step_of_the_build_is_not_taken()
        {
            BuildChain chain = Chain();
            // No build runs: a press of a row of Advanced keeps its own dialog
            Assert.False(chain.Ended(4, RunEnd.Committed, "m", out int? next, out string line));
            chain.Start();
            // Broken on purpose: step 9 or a step out of turn is no end of the build
            Assert.False(chain.Ended(9, RunEnd.Committed, "m", out next, out line));
            Assert.False(chain.Ended(5, RunEnd.Committed, "m", out next, out line));
            Assert.True(chain.Running);
            Assert.Equal("Step 1 of 5: Step4.", chain.Progress);
        }

        [Fact]
        public void R688_buttons_hands_the_end_of_a_step_to_the_build_before_any_dialog()
        {
            string buttons = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Commands", "Buttons.cs"));
            Assert.Contains("bool taken = outcome.End != RunEnd.Started && ChainEnded != null && ChainEnded(button.Number, outcome);", buttons);
            Assert.Contains("if (outcome.Message != null && !taken)", buttons);
            Assert.Contains("bool taken = ChainEnded != null && ChainEnded(number, later);", buttons);
            Assert.Contains("if (later.Message != null && !taken)", buttons);
        }
    }
}
#endif
