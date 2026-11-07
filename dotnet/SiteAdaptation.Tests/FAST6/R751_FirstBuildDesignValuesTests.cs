#if NETFRAMEWORK
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests.FAST6
{
    // R751, F4 of run 1 of FAST-3. DesignValues is process wide and this test process always finds the config of the repo, so the plan is made
    // in Windows PowerShell, the .NET Framework 4.8 Revit runs on, whose program folder has no config/project.json above it as Revit's has
    // none. The add-in and Common load from a copy of this build outside the repo, and nothing ran before the plan, as in a fresh session
    public sealed class R751_FirstBuildDesignValuesTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "R751_" + Guid.NewGuid().ToString("N"));

        public R751_FirstBuildDesignValuesTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static string Square(double east, double north)
        {
            return "[[" + east + "," + north + "],[" + (east + 10) + "," + north + "],[" + (east + 10) + "," + (north + 20) + "],[" + east + "," + (north + 20) + "]]";
        }

        // The made up project of R686 with the design values of the repo, but its own decimals, so the plan shows whose values it took
        private string Config(string name, int? decimals)
        {
            JObject config = SiteBuildSamples.Config();
            config["inputRoot"] = Path.Combine(root, "input");
            config["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 };
            config["addin"] = new JObject { ["workerTimeLimitMinS"] = 0.5, ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) } };
            config["designBasis"] = new JObject { ["mode"] = "made up new", ["modes"] = new JArray("made up old", "made up new") };
            config["projectRules"] = new JObject { ["manualName"] = "a made up manual", ["pastJobMode"] = "made up old", ["ruleIds"] = new JArray("M1") };
            if (decimals.HasValue)
            {
                JObject values = (JObject)JObject.Parse(File.ReadAllText(R405_DesignValuesTests.RepoConfigPath()))[DesignValues.Block].DeepClone();
                values["rounding"]["decimals"] = decimals.Value;
                config[DesignValues.Block] = values;
            }
            string file = Path.Combine(root, name, "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, config.ToString());
            return file;
        }

        private const string Script = @"param($dir, $config, $read, $work, $bare)
$ErrorActionPreference = 'Stop'
foreach ($name in 'Newtonsoft.Json.dll', 'SiteAdaptation.Common.dll', 'SiteAdaptation.AddIn.dll') { [void][Reflection.Assembly]::LoadFrom((Join-Path $dir $name)) }
'base=' + [AppDomain]::CurrentDomain.BaseDirectory
'knownBefore=' + [SiteAdaptation.Common.DesignValues]::Known
function Plan($file) {
    $place = New-Object SiteAdaptation.AddIn.Pane.SiteBuildPlace
    $place.ConfigFile = $file
    $place.ReadFolder = $read
    $place.WorkFolder = $work
    $document = New-Object SiteAdaptation.Common.WorkerDocument
    $document.Path = Join-Path $work 'a.rvt'
    $document.SessionId = 's1'
    $place.Document = $document
    [SiteAdaptation.AddIn.Pane.SiteBuildPlan]::Make($place)
}
$bareResult = Plan $bare
'bareFailure=' + $bareResult.Failure
'knownAfterBare=' + [SiteAdaptation.Common.DesignValues]::Known
$result = Plan $config
'failure=' + $result.Failure
'folder=' + $result.Folder
'decimals=' + [SiteAdaptation.Common.DesignValues]::Current.Decimals
";

        [Fact]
        public void R751_the_first_build_of_a_fresh_session_with_no_config_above_the_host_makes_its_plan_with_the_values_of_its_config()
        {
            // The add-in, Common and Newtonsoft.Json of this build, outside the repo
            string host = Path.Combine(root, "host");
            Directory.CreateDirectory(host);
            foreach (string dll in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
            {
                File.Copy(dll, Path.Combine(host, Path.GetFileName(dll)));
            }
            Assert.Null(DesignValues.RepoConfig(host));
            string work = Path.Combine(root, "work");
            string read = Path.Combine(work, "read-a");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, "plots.json"), "{\"plots\":[{\"number\":\"1\",\"villaType\":\"T1\",\"outline\":" + Square(400000, 1000000) + "},{\"number\":\"2\",\"villaType\":\"T1\",\"outline\":" + Square(400010, 1000000) + "}]}");
            File.WriteAllText(Path.Combine(read, "project_profile.json"), "{}");
            string config = Config("project", 5);
            string bare = Config("bare", null);
            string script = Path.Combine(root, "first_build.ps1");
            File.WriteAllText(script, Script);

            string shell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(shell), "no Windows PowerShell at " + shell);
            var start = new ProcessStartInfo(shell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" \"" + host + "\" \"" + config + "\" \"" + read + "\" \"" + work + "\" \"" + bare + "\"")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = root,
            };
            string output;
            string error;
            using (Process shellRun = Process.Start(start))
            {
                var errorRead = shellRun.StandardError.ReadToEndAsync();
                output = shellRun.StandardOutput.ReadToEnd();
                error = errorRead.Result;
                Assert.True(shellRun.WaitForExit(120000), "Windows PowerShell did not end in 2 minutes");
                Assert.True(shellRun.ExitCode == 0, output + error);
            }
            string Line(string key) => output.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith(key + "=", StringComparison.Ordinal))?.Substring(key.Length + 1);

            // The host is as Revit: no config/project.json above it, no design value known before the Build
            Assert.Null(DesignValues.RepoConfig(Line("base")));
            Assert.Equal("False", Line("knownBefore"));
            // siteBuild reads no design value itself, so a config of the project with none plans too, and no value came from elsewhere
            Assert.True(string.IsNullOrEmpty(Line("bareFailure")), output + error);
            Assert.Equal("False", Line("knownAfterBare"));
            // Broken before R751, seen by a revert of WorkerRequest.ToJson: the write of the request stopped with no config of the run gave the design values (R405)
            Assert.True(string.IsNullOrEmpty(Line("failure")), output + error);
            Assert.True(File.Exists(Path.Combine(Line("folder"), "build_plan.json")), output + error);
            // The values the run took are those of the config of the project, not of any repo
            Assert.Equal("5", Line("decimals"));
        }
    }
}
#endif
