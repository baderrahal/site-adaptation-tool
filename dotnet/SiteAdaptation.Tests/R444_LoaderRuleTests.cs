using System;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R444, Lane C DEMO-FIX. When the loader of a button stands and what its clock reads, on both runners
    public class R444_LoaderRuleTests
    {
        private static readonly IntPtr Main = new IntPtr(100);
        private static readonly IntPtr Loader = new IntPtr(200);
        private static readonly IntPtr Dialog = new IntPtr(300);

        [Fact]
        public void R444_the_loader_stands_only_after_two_seconds_and_only_while_revit_is_in_front()
        {
            Assert.False(LoaderRule.Shown(TimeSpan.FromSeconds(1.9), true));
            Assert.True(LoaderRule.Shown(TimeSpan.FromSeconds(2), true));
            Assert.False(LoaderRule.Shown(TimeSpan.FromMinutes(3), false));
        }

        [Fact]
        public void R444_revit_its_hang_ghost_and_the_loader_count_as_in_front_and_a_window_of_a_step_does_not()
        {
            Assert.True(LoaderRule.MainInFront(Main, Main, Loader, "Revit"));
            Assert.True(LoaderRule.MainInFront(Loader, Main, Loader, "HwndWrapper"));
            Assert.True(LoaderRule.MainInFront(new IntPtr(400), Main, Loader, LoaderRule.GhostClass));
            Assert.False(LoaderRule.MainInFront(Dialog, Main, Loader, "HwndWrapper"));
            Assert.False(LoaderRule.MainInFront(Dialog, Main, Loader, "#32770"));
            Assert.False(LoaderRule.MainInFront(IntPtr.Zero, Main, Loader, null));
            Assert.False(LoaderRule.MainInFront(Main, IntPtr.Zero, Loader, "Revit"));
        }

        [Fact]
        public void R444_the_clock_reads_minutes_and_seconds()
        {
            Assert.Equal("0:00", LoaderRule.Clock(TimeSpan.Zero));
            Assert.Equal("0:07", LoaderRule.Clock(TimeSpan.FromSeconds(7.9)));
            Assert.Equal("1:05", LoaderRule.Clock(TimeSpan.FromSeconds(65)));
            Assert.Equal("72:00", LoaderRule.Clock(TimeSpan.FromHours(1.2)));
            Assert.Equal("0:00", LoaderRule.Clock(TimeSpan.FromSeconds(-3)));
        }
    }
}
