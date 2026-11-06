using System.IO;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R701, FAST-1 item 8, Q570. The listener reads no floor since FAST-2 PR 242, so the start script must not hand one to Revit either
    public sealed class R701_ListenerScriptTests
    {
        [Fact]
        public void R701_the_listener_start_script_gives_no_free_memory_floor()
        {
            string start = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", "Start-RevitListener.ps1"));
            Assert.DoesNotContain("SITEADAPT_LISTENER_MIN_FREE_GB", start);
            Assert.DoesNotContain("minFreeGB", start);
            Assert.Contains("SITEADAPT_LISTENER_SLOT", start);
        }
    }
}
