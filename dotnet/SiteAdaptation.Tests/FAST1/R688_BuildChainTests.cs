#if NETFRAMEWORK
using System.IO;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R688. The one Build runs set up site, check, floors and ramps, levels, villas and walls one after another and stops only on a step that did not end well
    public sealed class R688_BuildChainTests
    {
        private static BuildChain Chain()
        {
            return new BuildChain(number => "Step" + number);
        }

        [Fact]
        public void R688_the_steps_run_in_the_order_of_the_buttons_with_one_progress_line_and_the_messages_go_to_the_issues()
        {
            BuildChain chain = Chain();
            Assert.Equal(new[] { 1, 4, 5, 6, 7, 8 }, BuildChain.Order);
            Assert.Equal(1, chain.Start());
            Assert.Equal("Step 1 of 6: Step1.", chain.Progress);
            // A second Build while one runs starts nothing
            Assert.Null(chain.Start());
            var seen = new System.Collections.Generic.List<int> { 1 };
            int current = 1;
            for (int at = 0; at < 6; at++)
            {
                Assert.True(chain.Ended(current, RunEnd.Committed, at == 2 ? "Made up message" : null, out int? next, out string line));
                if (at < 5)
                {
                    Assert.Equal("Step " + (at + 2) + " of 6: Step" + next + ".", line);
                    current = next.Value;
                    seen.Add(current);
                }
                else
                {
                    Assert.Null(next);
                    Assert.Equal("Build done: 6 steps, 1 issues.", line);
                }
            }
            Assert.Equal(new[] { 1, 4, 5, 6, 7, 8 }, seen.ToArray());
            Assert.Equal(new[] { "Step5: Made up message" }, chain.Issues);
            Assert.False(chain.Running);
        }

        [Fact]
        public void R688_a_step_that_did_not_end_well_stops_the_build_and_says_where_and_why()
        {
            BuildChain chain = Chain();
            chain.Start();
            Assert.True(chain.Ended(1, RunEnd.Committed, null, out int? next, out string line));
            Assert.Equal(4, next);
            // Work outside Revit: the build waits for the end
            Assert.True(chain.Ended(4, RunEnd.Started, null, out next, out line));
            Assert.Null(next);
            Assert.Equal("Step 2 of 6: Step4.", line);
            Assert.True(chain.Running);
            Assert.True(chain.Ended(4, RunEnd.Stopped, "Made up reason", out next, out line));
            Assert.Null(next);
            Assert.Equal("Build stopped at Step4: Made up reason", line);
            Assert.False(chain.Running);

            chain.Start();
            Assert.True(chain.Ended(1, RunEnd.Cancelled, null, out next, out line));
            Assert.Equal("Build stopped at Step1, cancelled.", line);

            chain.Start();
            Assert.True(chain.Ended(1, RunEnd.RolledBack, " ", out next, out line));
            Assert.Equal("Build stopped at Step1: " + SiteBuildText.NoReason, line);
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
            Assert.False(chain.Ended(4, RunEnd.Committed, "m", out next, out line));
            Assert.True(chain.Running);
            Assert.Equal(1, chain.Current);
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
