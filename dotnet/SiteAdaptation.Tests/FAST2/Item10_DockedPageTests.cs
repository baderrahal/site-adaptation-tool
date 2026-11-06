using System.IO;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    // FAST-2 item 10. The names of Common hold the page the docked pane shows, the one the test host looks for, beside the old page
    public class Item10_DockedPageTests
    {
        [Fact]
        public void Item10_the_docked_page_of_common_is_the_page_of_the_stages_the_host_looks_for_and_the_old_page_stays()
        {
            string root = R700_SharedParametersNeedTests.RepoRoot();
            Assert.EndsWith(".StagesPage", AddinRunNames.DockedPage);
            Assert.EndsWith(".SitePanePage", AddinRunNames.Page);
            Assert.True(File.Exists(Path.Combine(root, "dotnet", "SiteAdaptation.AddIn", "Pane", "StagesPage.xaml")));
            Assert.Contains("x:Class=\"" + AddinRunNames.DockedPage + "\"", File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.AddIn", "Pane", "StagesPage.xaml")));
            Assert.Contains("\"" + AddinRunNames.DockedPage + "\"", File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.TestHost", "AddinProbe.cs")));
        }
    }
}
