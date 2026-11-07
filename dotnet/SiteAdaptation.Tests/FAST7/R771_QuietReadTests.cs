#if NETFRAMEWORK
using System.Collections.Generic;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R771, F6 of run 1 of FAST-3. A step 2 the Build puts back runs once in a Build and quietly. Made up values only
    public sealed class R771_QuietReadTests
    {
        [Fact]
        public void R771_a_step_the_build_put_back_is_never_put_back_twice_and_current_names_the_step_running()
        {
            var state = new Dictionary<int, StepStatus> { [4] = new StepStatus(StepKind.NeedsStep, 3), [3] = new StepStatus(StepKind.NeedsStep, 2), [2] = new StepStatus(StepKind.Ready) };
            var chain = new BuildChain(number => "Step" + number, number => state.TryGetValue(number, out StepStatus held) ? held : new StepStatus(StepKind.Done));
            Assert.Null(chain.Current);
            Assert.Equal(2, chain.Start());
            Assert.Equal(2, chain.Current);

            // Broken on purpose: step 2 ran, yet step 3 still waits on it, as the scope change of F23 leaves it. The build does not run it again
            state[2] = new StepStatus(StepKind.Ready);
            Assert.True(chain.Ended(2, RunEnd.Committed, null, out int? next, out _));
            Assert.Equal(3, next);
            Assert.Equal(3, chain.Current);

            // A new build may put it back again, once
            Assert.True(chain.Ended(3, RunEnd.Stopped, "3 Review data needs step 2", out next, out _));
            Assert.Null(chain.Current);
            Assert.Equal(2, chain.Start());
        }
    }
}
#endif
