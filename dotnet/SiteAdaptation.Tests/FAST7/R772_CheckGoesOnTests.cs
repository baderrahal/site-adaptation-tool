#if NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R772, F25 of the short check of 7 Nov. The Build looped on 4 Check: right after Check committed, the statuses of the pane still said step 5 needs
    // step 4, so the chain put Check back, 5 runs. Made up values only
    public sealed class R772_CheckGoesOnTests
    {
        [Fact]
        public void R772_a_step_that_committed_in_the_build_is_never_put_back_and_the_build_goes_on()
        {
            var state = new Dictionary<int, StepStatus> { [4] = new StepStatus(StepKind.Ready) };
            var chain = new BuildChain(number => "Step" + number, number => state.TryGetValue(number, out StepStatus held) ? held : new StepStatus(StepKind.Done));
            Assert.Equal(4, chain.Start());

            // Broken on purpose: Check committed, and the statuses have not caught up, step 5 still waits on step 4
            state[5] = new StepStatus(StepKind.NeedsStep, 4);
            Assert.True(chain.Ended(4, RunEnd.Committed, null, out int? next, out string line));
            Assert.Equal(5, next);
            Assert.Equal(5, chain.Current);

            // A step that did not commit in this build is still put back once, as R688 asks
            var other = new Dictionary<int, StepStatus> { [4] = new StepStatus(StepKind.NeedsStep, 3), [3] = new StepStatus(StepKind.Ready) };
            var waiting = new BuildChain(number => "Step" + number, number => other.TryGetValue(number, out StepStatus held) ? held : new StepStatus(StepKind.Done));
            Assert.Equal(3, waiting.Start());
        }

        [Fact]
        public void R772_the_check_window_has_a_close_of_its_own()
        {
            Fast7Sta.Run(() =>
            {
                CheckTableWindow window = Shots.LaidOutCheck(Shots.SampleCheckView(false));
                var close = (Button)window.FindName("CloseButton");
                Assert.NotNull(close);
                Assert.Equal(CheckTableText.Close, close.Content);
                Assert.Equal(Visibility.Visible, close.Visibility);
                Assert.True(close.IsEnabled);
            });
        }
    }
}
#endif
