#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R760, F12 of run 1 of FAST-3. A Build that stops says the step and the reason in a box of card 2, made up values only
    public sealed class R760_StopBoxTests
    {
        [Fact]
        public void R760_a_stop_or_a_cancel_shows_the_box_and_the_next_press_of_build_clears_it()
        {
            Fast7Sta.Run(() =>
            {
                StagesPage page = Shots.LaidOutStages(true);
                var stages = (StagesViewModel)page.DataContext;
                var box = (Border)page.FindName("StopBox");
                Assert.False(stages.HasStop);
                Assert.Equal(Visibility.Collapsed, box.Visibility);

                // A step that ended well or goes on outside Revit leaves the box shut
                stages.ShowStop(RunEnd.Committed, "Step 2 of 6: 4 Check.");
                stages.ShowStop(RunEnd.Started, "Step 2 of 6: 4 Check.");
                Assert.False(stages.HasStop);

                string stopped = TextTemplate.Fill(SiteBuildText.StepStopped, "4 Check", "Made up reason.");
                stages.ShowStop(RunEnd.Stopped, stopped);
                Assert.True(stages.HasStop);
                Assert.Equal(stopped, stages.StopLine);
                page.UpdateLayout();
                Assert.Equal(Visibility.Visible, box.Visibility);
                Assert.Contains(stopped, Fast7Sta.Visuals(box).OfType<TextBlock>().Select(text => text.Text));

                // The box sits in card 2, above its long line
                var card2 = (Border)page.FindName("Card2Box");
                Assert.Contains(box, Fast7Sta.Visuals(card2));

                string cancelled = TextTemplate.Fill(SiteBuildText.StepCancelled, "3 Review data");
                stages.ShowStop(RunEnd.Cancelled, cancelled);
                Assert.Equal(cancelled, stages.StopLine);
                stages.ShowStop(RunEnd.RolledBack, stopped);
                Assert.Equal(stopped, stages.StopLine);

                // The next press of Build clears the box before the Revit side runs it
                bool pressed = false;
                bool shutFirst = false;
                stages.BuildRequested += () =>
                {
                    pressed = true;
                    shutFirst = !stages.HasStop;
                };
                Assert.True(stages.Card2.Command.CanExecute(null));
                stages.Card2.Command.Execute(null);
                Assert.True(pressed);
                Assert.True(shutFirst);
                Assert.Null(stages.StopLine);
            });
        }

        [Fact]
        public void R760_the_box_is_drawn_in_the_shot_of_card_2()
        {
            Fast7Sta.Run(() =>
            {
                StagesPage page = Shots.LaidOutStages(true, stages => stages.ShowStop(RunEnd.Stopped, Shots.SampleStop));
                var box = (Border)page.FindName("StopBox");
                Assert.Equal(Visibility.Visible, box.Visibility);
                Assert.True(box.ActualHeight > 0);
            });
        }
    }

    // The STA thread a WPF page needs, and the walk of its visual tree, for the tests of FAST-7
    internal static class Fast7Sta
    {
        public static void Run(Action action)
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

        public static IEnumerable<DependencyObject> Visuals(DependencyObject root)
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
    }
}
#endif
