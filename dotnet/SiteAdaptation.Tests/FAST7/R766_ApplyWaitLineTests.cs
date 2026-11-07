#if NETFRAMEWORK
using System.Collections.Generic;
using System.ComponentModel;
using SiteAdaptation.AddIn.SetUpSite;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R766, F2 of run 1 of FAST-3. A line beside the grey Apply of Set up site says the dry run is needed first, then the tick of the site
    // drawing. Made up values only
    public sealed class R766_ApplyWaitLineTests
    {
        private static SetUpSiteView Cad(bool dryRunDone, bool siteTicked)
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            view.PositionSource = "cad";
            view.DryRunDone = dryRunDone;
            view.NothingDiffers = false;
            view.Stops.Clear();
            view.CurrentFiles = new List<CurrentFileRow> { new CurrentFileRow { Input = InputNames.Baseplan, FileName = "made up site.dwg", Site = true, Current = siteTicked } };
            return view;
        }

        [Fact]
        public void R766_before_the_dry_run_the_line_asks_for_it_then_for_the_tick_then_says_nothing()
        {
            var model = new SetUpSiteViewModel(Cad(false, false));
            Assert.False(model.CanApply);
            Assert.Equal(SetUpSiteText.DryRunFirst, model.ApplyWaitLine);

            // The dry run is done but the site drawing is not ticked Current
            model.Load(Cad(true, false));
            Assert.False(model.CanApply);
            Assert.Equal(SetUpSiteText.TickSiteFirst, model.ApplyWaitLine);

            // Ticked: Apply turns on and the line goes, and the tick tells the window the line changed
            var changed = new List<string>();
            ((INotifyPropertyChanged)model).PropertyChanged += (sender, e) => changed.Add(e.PropertyName);
            model.CurrentFiles[0].Current = true;
            Assert.True(model.CanApply);
            Assert.Equal(string.Empty, model.ApplyWaitLine);
            Assert.Contains(nameof(SetUpSiteViewModel.ApplyWaitLine), changed);
        }

        // Review 1 of PR 2: where nothing differed, another ramp slope asks for a dry run, so the line asks for it too
        [Fact]
        public void R766_another_slope_after_nothing_differs_asks_for_the_dry_run()
        {
            SetUpSiteView view = Cad(false, true);
            view.NothingDiffers = true;
            var model = new SetUpSiteViewModel(view);
            Assert.True(model.CanApply);
            Assert.Equal(string.Empty, model.ApplyWaitLine);
            Assert.True(model.RampChoices.Count > 1);
            model.RampIndex = model.RampIndex == 0 ? 1 : 0;
            Assert.False(model.CanApply);
            Assert.Equal(SetUpSiteText.DryRunFirst, model.ApplyWaitLine);
        }

        [Fact]
        public void R766_a_stop_speaks_in_its_own_box_so_the_line_stays_empty()
        {
            SetUpSiteView view = Cad(false, false);
            view.Stops.Add("Made up stop.");
            var model = new SetUpSiteViewModel(view);
            Assert.False(model.CanApply);
            Assert.True(model.StopsShown);
            Assert.Equal(string.Empty, model.ApplyWaitLine);
        }
    }
}
#endif
