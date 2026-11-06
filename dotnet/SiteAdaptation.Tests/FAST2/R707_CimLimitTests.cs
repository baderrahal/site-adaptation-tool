using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    // R707. The tests of R310 whose CIM call must end take testHost.cimTestTimeoutSeconds, a run of the test host keeps cimTimeoutSeconds
    public class R707_CimLimitTests
    {
        [Fact]
        public void R707_the_test_limit_is_over_the_slowest_call_measured_and_inside_what_a_sample_takes_and_a_run_keeps_its_own()
        {
            string root = R700_SharedParametersNeedTests.RepoRoot();
            JObject host = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(root, "config", "project.json")))["testHost"];
            int test = (int)host["cimTestTimeoutSeconds"];
            // The slowest list of the processes this PC took on 6 Nov 2026 was 35.8 s, Get-WindowSample takes 1 to 60 s
            Assert.InRange(test, 36, 60);
            Assert.Equal(2, (int)host["cimTimeoutSeconds"]);
            string samples = File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.TestHost", "WindowSamples.ps1"));
            Assert.Contains("[ValidateRange(1, 60)][int]$TimeoutSeconds", samples);
            Assert.Contains("CimTestSeconds()", File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.Tests", "ParsonsModelTests.cs")));
        }
    }
}
