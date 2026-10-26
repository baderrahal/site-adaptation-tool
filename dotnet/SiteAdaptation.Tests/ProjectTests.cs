using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Newtonsoft.Json;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R346. Common is the logic of the add-in and the worker, one netstandard2.0 library that loads in the net48 process of Revit
    // and in the net8.0 tests of the Linux runner, built against the Newtonsoft.Json Revit 2024 ships
    public class ProjectTests
    {
        [Fact]
        public void R346_common_is_a_netstandard2_0_library()
        {
            var target = typeof(JsonOut).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            Assert.Equal(".NETStandard,Version=v2.0", target.FrameworkName);
        }

        [Fact]
        public void R346_the_tests_run_the_newtonsoft_json_13_0_1_that_revit_2024_ships()
        {
            Assembly json = typeof(JsonConvert).Assembly;
            string file = json.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            Assert.StartsWith("13.0.1.", file);
            Assert.Equal("13.0.0.0", json.GetName().Version.ToString());
            AssemblyName referenced = typeof(JsonOut).Assembly.GetReferencedAssemblies().Single(name => name.Name == "Newtonsoft.Json");
            Assert.Equal("13.0.0.0", referenced.Version.ToString());
        }

        [Fact]
        public void R346_each_target_of_the_test_project_runs_on_its_own_runtime()
        {
            string runtime = RuntimeInformation.FrameworkDescription;
#if NETFRAMEWORK
            Assert.StartsWith(".NET Framework 4.8", runtime);
#else
            Assert.StartsWith(".NET 8.", runtime);
#endif
            Assert.False(string.IsNullOrEmpty(FileVersionInfo.GetVersionInfo(typeof(JsonOut).Assembly.Location).FileVersion));
        }
    }
}
