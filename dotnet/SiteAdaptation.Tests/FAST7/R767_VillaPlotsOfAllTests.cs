#if NETFRAMEWORK
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common.Scan;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R767, F9 of run 1 of FAST-3. The head of the check window counts the villa plots of all numbered plots once R730 listed some apart.
    // Made up values only
    public sealed class R767_VillaPlotsOfAllTests
    {
        private const string Check = "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Made up.\"}],\"files\":[]}";

        [Fact]
        public void R767_a_check_with_plots_listed_apart_says_villa_plots_of_all_plots()
        {
            string plots = "{\"plots\":[{\"number\":\"1\"},{\"number\":\"2\"},{\"number\":\"3\"}],\"otherPlots\":[{\"number\":\"4\",\"why\":\"made up\"}]}";
            DataCheckResult result = DataCheckRows.ReadResult(Check, plots);
            Assert.Equal(3, result.PlotsFound);
            Assert.Equal(1, result.OtherPlots);
            var model = new ScanResultsViewModel(new ScanViewData(), result, true);
            Assert.Equal("3 of 4", model.PlotsFound);
            Assert.Equal(DataCheckText.VillaPlotsOfAll, model.PlotsFoundLabel);
        }

        [Fact]
        public void R767_with_no_plot_listed_apart_or_outside_the_check_the_head_stays_as_before()
        {
            DataCheckResult all = DataCheckRows.ReadResult(Check, "{\"plots\":[{\"number\":\"1\"},{\"number\":\"2\"}]}");
            Assert.Equal(0, all.OtherPlots);
            var model = new ScanResultsViewModel(new ScanViewData(), all, true);
            Assert.Equal("2", model.PlotsFound);
            Assert.Equal(ScanResultsText.PlotsFound, model.PlotsFoundLabel);

            // The read of version 1 shows its own count, even with a check that listed plots apart beside it
            DataCheckResult apart = DataCheckRows.ReadResult(Check, "{\"plots\":[{\"number\":\"1\"}],\"otherPlots\":[{\"number\":\"2\"}]}");
            var read = new ScanResultsViewModel(new ScanViewData(), apart, false);
            Assert.Equal(ScanResultsText.PlotsFound, read.PlotsFoundLabel);
            Assert.Equal("0", read.PlotsFound);

            Assert.Equal(0, DataCheckRows.ReadResult(Check, null).OtherPlots);
        }
    }
}
#endif
