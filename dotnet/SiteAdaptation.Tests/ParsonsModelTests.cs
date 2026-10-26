using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Round ADDIN Phase 1, the 7 Parsons models: the memory samples of every test host window (R310), the site facts of the reader (R311)
    // and the source of a site upgrade (R204)
    public class ParsonsModelTests
    {
        private static string RepoPath(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }

        // Runs one command in PowerShell 7 with the sample functions loaded and gives what it printed
        private static string Pwsh(string command)
        {
            string functions = RepoPath("dotnet", "SiteAdaptation.TestHost", "WindowSamples.ps1");
            // An unknown command or a failed call stops the script, so a test never runs on past a missing function
            string text = "$ErrorActionPreference = 'Stop'\n. '" + functions.Replace("'", "''") + "'\n" + command;
            var info = new ProcessStartInfo("pwsh", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(text)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                Assert.True(process.WaitForExit(60000), "pwsh did not end in 60 s");
                Assert.True(process.ExitCode == 0, "pwsh failed: " + error);
                return output;
            }
        }

        [Fact]
        public void R310_the_peaks_are_the_largest_samples_also_past_2_gib()
        {
            string json = Pwsh(
                "$samples = @(\n"
                + "  [ordered]@{ privateBytes = [long]1900MB; peakPrivateBytes = [long]1950MB; workingSetBytes = [long]1800MB; peakWorkingSetBytes = [long]1850MB; responding = $true; freeBytes = [long]7GB },\n"
                + "  [ordered]@{ privateBytes = [long]6400MB; peakPrivateBytes = [long]11000MB; workingSetBytes = [long]5700MB; peakWorkingSetBytes = [long]5800MB; responding = $false; freeBytes = [long]1100MB },\n"
                + "  [ordered]@{ privateBytes = [long]10900MB; peakPrivateBytes = [long]10950MB; workingSetBytes = [long]4900MB; peakWorkingSetBytes = [long]6000MB; responding = $false; freeBytes = [long]2200MB })\n"
                + "Get-WindowPeaks -Samples $samples -FloorGB 2 | ConvertTo-Json -Compress");
            JObject peaks = JObject.Parse(json);

            Assert.Equal(3, (int)peaks["samples"]);
            // The true peaks take the peaks Windows keeps between two samples, the sampled ones are kept apart
            Assert.Equal(11000, (long)peaks["peakPrivateMib"]);
            Assert.Equal(6000, (long)peaks["peakWorkingSetMib"]);
            Assert.Equal(10900, (long)peaks["sampledPrivateMib"]);
            Assert.Equal(5700, (long)peaks["sampledWorkingSetMib"]);
            Assert.Equal(1.07, (double)peaks["minFreeGib"], 2);
            Assert.Equal(1, (int)peaks["samplesUnderFloor"]);
            Assert.Equal(2, (int)peaks["notResponding"]);
        }

        [Fact]
        public void R310_the_peaks_windows_kept_to_the_exit_count_when_they_pass_every_sample()
        {
            string json = Pwsh(
                "$samples = @([ordered]@{ privateBytes = [long]800MB; peakPrivateBytes = [long]850MB; workingSetBytes = [long]900MB; peakWorkingSetBytes = [long]950MB; responding = $true; freeBytes = [long]5GB })\n"
                + "$exit = [ordered]@{ peakPrivateBytes = [long]3000MB; peakWorkingSetBytes = [long]2900MB }\n"
                + "Get-WindowPeaks -Samples $samples -FloorGB 2 -Exit $exit | ConvertTo-Json -Compress");
            JObject peaks = JObject.Parse(json);

            Assert.Equal(3000, (long)peaks["peakPrivateMib"]);
            Assert.Equal(2900, (long)peaks["peakWorkingSetMib"]);
            Assert.Equal(800, (long)peaks["sampledPrivateMib"]);
            Assert.True((bool)peaks["throughExit"]);
            Assert.Equal(1, (int)peaks["samples"]);

            JObject without = JObject.Parse(Pwsh(
                "Get-WindowPeaks -Samples @([ordered]@{ privateBytes = [long]800MB; peakPrivateBytes = [long]850MB; workingSetBytes = [long]900MB; peakWorkingSetBytes = [long]950MB; responding = $true; freeBytes = [long]5GB }) -FloorGB 2 | ConvertTo-Json -Compress"));
            Assert.False((bool)without["throughExit"]);
            Assert.Equal(850, (long)without["peakPrivateMib"]);
        }

        [Fact]
        public void R310_the_exit_peaks_give_the_peak_also_when_no_sample_was_taken()
        {
            foreach (string samples in new[] { "@()", "$null" })
            {
                JObject peaks = JObject.Parse(Pwsh(
                    "$exit = [ordered]@{ peakPrivateBytes = [long]3000MB; peakWorkingSetBytes = [long]2900MB }\n"
                    + "Get-WindowPeaks -Samples " + samples + " -FloorGB 2 -Exit $exit | ConvertTo-Json -Compress"));

                Assert.Equal(0, (int)peaks["samples"]);
                Assert.Equal(3000, (long)peaks["peakPrivateMib"]);
                Assert.Equal(2900, (long)peaks["peakWorkingSetMib"]);
                Assert.True((bool)peaks["throughExit"]);
                Assert.Equal(JTokenType.Null, peaks["sampledPrivateMib"].Type);
                Assert.Equal(JTokenType.Null, peaks["minFreeGib"].Type);
            }
        }

        [WindowsFact]
        public void R310_a_sample_keeps_the_process_memory_when_a_cim_call_fails_and_the_peaks_count_its_free_memory_as_missing()
        {
            // A function of that name hides the cmdlet in the scope of the sample functions, so each CIM call fails in turn
            foreach (string failing in new[] { "Win32_OperatingSystem", "Win32_Process" })
            {
                JObject sample = JObject.Parse(Pwsh(
                    "function Get-CimInstance { [CmdletBinding()] param([string]$ClassName, [string]$Filter, [string[]]$Property, [int]$OperationTimeoutSec) if ($ClassName -eq '" + failing + "') { throw 'no CIM' } CimCmdlets\\Get-CimInstance @PSBoundParameters }\n"
                    + "Get-WindowSample -Process (Get-Process -Id $PID) -TimeoutSeconds 2 | ConvertTo-Json -Compress"));

                Assert.True((long)sample["privateBytes"] > 0, failing);
                Assert.True((long)sample["peakPrivateBytes"] > 0, failing);
                Assert.True((long)sample["workingSetBytes"] > 0, failing);
                bool os = failing == "Win32_OperatingSystem";
                Assert.Equal(os ? JTokenType.Null : JTokenType.Integer, sample["freeBytes"].Type);
                Assert.Equal(os ? JTokenType.Integer : JTokenType.Null, sample["childPrivateBytes"].Type);
                Assert.Contains("no CIM", (string)sample[os ? "freeError" : "childError"]);
            }

            JObject peaks = JObject.Parse(Pwsh(
                "$samples = @(\n"
                + "  [ordered]@{ privateBytes = [long]900MB; peakPrivateBytes = [long]900MB; workingSetBytes = [long]1000MB; peakWorkingSetBytes = [long]1000MB; responding = $true; freeBytes = $null; freeError = 'no CIM' },\n"
                + "  [ordered]@{ privateBytes = [long]800MB; peakPrivateBytes = [long]950MB; workingSetBytes = [long]900MB; peakWorkingSetBytes = [long]950MB; responding = $true; freeBytes = [long]5GB })\n"
                + "Get-WindowPeaks -Samples $samples -FloorGB 2 | ConvertTo-Json -Compress"));
            Assert.Equal(2, (int)peaks["samples"]);
            Assert.Equal(950, (long)peaks["peakPrivateMib"]);
            Assert.Equal(5.0, (double)peaks["minFreeGib"], 2);
            Assert.Equal(0, (int)peaks["samplesUnderFloor"]);
            Assert.Equal(1, (int)peaks["freeMissing"]);
        }

        [Fact]
        public void R310_each_cim_call_takes_the_limit_it_is_given_and_a_timed_out_call_is_written_as_not_read_without_blocking()
        {
            // A CIM call that runs to its limit and then times out, as Get-CimInstance does with -OperationTimeoutSec, and one with no
            // limit that would never return. The sample asks with the limit, is written with both calls not read and ends near twice it
            JObject result = JObject.Parse(Pwsh(
                "$global:limits = @()\n"
                + "function Get-CimInstance { [CmdletBinding()] param([string]$ClassName, [string[]]$Property, [int]$OperationTimeoutSec)\n"
                + "  $global:limits += $OperationTimeoutSec\n"
                + "  if ($OperationTimeoutSec -lt 1) { Start-Sleep 20 }\n"
                + "  Start-Sleep -Seconds $OperationTimeoutSec\n"
                + "  throw 'Timed out' }\n"
                + "$clock = [Diagnostics.Stopwatch]::StartNew()\n"
                + "$sample = Get-WindowSample -Process (Get-Process -Id $PID) -TimeoutSeconds 1\n"
                + "[ordered]@{ sample = $sample; seconds = $clock.Elapsed.TotalSeconds; limits = $global:limits } | ConvertTo-Json -Compress -Depth 5"));

            Assert.Equal(new[] { 1, 1 }, result["limits"].Select(limit => (int)limit));
            Assert.InRange((double)result["seconds"], 1.9, 10);
            JToken sample = result["sample"];
            Assert.True((long)sample["privateBytes"] > 0);
            Assert.Equal(JTokenType.Null, sample["freeBytes"].Type);
            Assert.Equal(JTokenType.Null, sample["childProcesses"].Type);
            Assert.Equal(JTokenType.Null, sample["childPrivateBytes"].Type);
            Assert.Contains("Timed out", (string)sample["freeError"]);
            Assert.Contains("Timed out", (string)sample["childError"]);

            // With no limit there is no sample, so a call can never run on unbounded
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "WindowSamples.ps1"));
            Assert.Equal(2, script.Split(new[] { "Get-CimInstance " }, StringSplitOptions.None).Length - 1);
            Assert.Equal(2, script.Split(new[] { "-OperationTimeoutSec $TimeoutSeconds" }, StringSplitOptions.None).Length - 1);
        }

        [WindowsFact]
        public void R310_helpers_running_before_the_window_are_listed_by_name_and_private_memory_and_never_counted_as_its_children()
        {
            // A helper with 100 MiB of its own runs before the window starts, as the LocalDB of Steel Connections did from the first
            // window of Phase 1 on. The window starts nothing of that name, so its child figures leave the helper out
            JObject result = JObject.Parse(Pwsh(
                "$path = (Get-Process -Id $PID).Path\n"
                + "$info = [Diagnostics.ProcessStartInfo]::new($path, @('-NoProfile', '-Command', '$b = [byte[]]::new(100MB)\n[Array]::Fill($b, [byte]1)\nStart-Sleep 30'))\n"
                + "$info.UseShellExecute = $false\n"
                + "$info.CreateNoWindow = $true\n"
                + "$helper = [Diagnostics.Process]::Start($info)\n"
                + "for ($i = 0; $i -lt 60 -and (Get-Process -Id $helper.Id).PrivateMemorySize64 -lt 100MB; $i++) { Start-Sleep -Milliseconds 500 }\n"
                + "$helpers = @(Get-RunningHelpers -Names @('pwsh', 'no-such-helper'))\n"
                + "$windowInfo = [Diagnostics.ProcessStartInfo]::new($path, @('-NoProfile', '-Command', 'Start-Sleep 30'))\n"
                + "$windowInfo.UseShellExecute = $false\n"
                + "$windowInfo.CreateNoWindow = $true\n"
                + "$window = [Diagnostics.Process]::Start($windowInfo)\n"
                + "Start-Sleep 2\n"
                + "$sample = Get-WindowSample -Process $window -TimeoutSeconds 2\n"
                + "$helper.Kill()\n"
                + "$window.Kill()\n"
                + "[ordered]@{ helperId = $helper.Id; helpers = $helpers; sample = $sample; none = @(Get-RunningHelpers -Names @()).Count } | ConvertTo-Json -Compress -Depth 5"));

            JToken helper = result["helpers"].Single(item => (int)item["id"] == (int)result["helperId"]);
            Assert.Equal("pwsh", (string)helper["name"]);
            Assert.True((long)helper["privateMib"] >= 100);
            Assert.True(DateTime.Parse((string)helper["startUtc"]).Year >= 2026);
            Assert.DoesNotContain(result["helpers"], item => (string)item["name"] == "no-such-helper");
            Assert.Equal(0, (int)result["none"]);
            Assert.DoesNotContain("pwsh", (string)result["sample"]["childNames"] ?? string.Empty);
            Assert.True((long)result["sample"]["childPrivateBytes"] < 100L * 1024 * 1024);
        }

        [Fact]
        public void R310_the_run_checks_the_deadline_first_in_every_pass_and_writes_the_helpers_at_the_window_start()
        {
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            JObject config = JObject.Parse(File.ReadAllText(RepoPath("config", "project.json")));

            // The limit of a CIM call comes from the config, and two calls at their limit fit in the 5 s of the checks
            int limit = (int)config["testHost"]["cimTimeoutSeconds"];
            Assert.Equal(2, limit);
            Assert.True(2 * limit < 5);
            Assert.Contains("$cimSeconds = [int]$config.testHost.cimTimeoutSeconds", script);
            Assert.Contains("$sample = Get-WindowSample -Process $process -TimeoutSeconds $cimSeconds", script);

            // In every pass the deadline is looked at right after the wait, before the clocks and before a sample
            int wait = script.IndexOf("$process.WaitForExit((Get-LookWait -Clock $clock -Now (Get-Date)))", StringComparison.Ordinal);
            int deadline = script.IndexOf("if ($now -gt $deadline)", StringComparison.Ordinal);
            int due = script.IndexOf("$due = Get-DueLooks -Clock $clock -Now $now", StringComparison.Ordinal);
            int sample = script.IndexOf("if ($due.sample) {", StringComparison.Ordinal);
            Assert.True(wait > 0 && deadline > wait && due > deadline && sample > due);
            Assert.Equal(1, script.Split(new[] { "if ($now -gt $deadline)" }, StringSplitOptions.None).Length - 1);

            // The helpers already running are listed before Revit starts and go into the memory block of run.json in full
            Assert.NotNull(config["testHost"]["helperProcesses"]);
            Assert.Contains("RevitWorker", config["testHost"]["helperProcesses"].Select(name => (string)name));
            Assert.Contains("sqlservr", config["testHost"]["helperProcesses"].Select(name => (string)name));
            int helpers = script.IndexOf("$helpersAtStart = @(Get-RunningHelpers -Names @($config.testHost.helperProcesses))", StringComparison.Ordinal);
            Assert.True(helpers > 0 && helpers < script.IndexOf("$process = [Diagnostics.Process]::Start($info)", StringComparison.Ordinal));
            Assert.Contains("$peaks.helpersAtStart = @($helpersAtStart)", script);
            Assert.Contains("} | ConvertTo-Json -Depth 5 | Set-Content -Path $runPath -Encoding utf8", script);
        }

        [WindowsFact]
        public void R310_a_sample_counts_the_processes_the_window_started_and_their_memory()
        {
            JObject sample = JObject.Parse(Pwsh(
                "$info = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path, @('-NoProfile', '-Command', '$b = [byte[]]::new(200MB); [Array]::Fill($b, [byte]1); Start-Sleep 30'))\n"
                + "$info.UseShellExecute = $false\n"
                + "$child = [Diagnostics.Process]::Start($info)\n"
                + "for ($i = 0; $i -lt 60 -and (Get-Process -Id $child.Id).PrivateMemorySize64 -lt 200MB; $i++) { Start-Sleep -Milliseconds 500 }\n"
                + "$sample = Get-WindowSample -Process (Get-Process -Id $PID) -TimeoutSeconds 2\n"
                + "$child.Kill()\n"
                + "$sample | ConvertTo-Json -Compress"));

            Assert.True((int)sample["childProcesses"] >= 1);
            Assert.True((long)sample["childPrivateBytes"] >= 200L * 1024 * 1024);
            Assert.True((long)sample["childWorkingSetBytes"] >= 200L * 1024 * 1024);
            Assert.Contains("pwsh", (string)sample["childNames"]);

            JObject peaks = JObject.Parse(Pwsh(
                "$samples = @(\n"
                + "  [ordered]@{ privateBytes = [long]900MB; peakPrivateBytes = [long]900MB; workingSetBytes = [long]1000MB; peakWorkingSetBytes = [long]1000MB; responding = $true; freeBytes = [long]5GB; childProcesses = 3; childPrivateBytes = [long]300MB; childWorkingSetBytes = [long]350MB; childNames = 'CefSharp.BrowserSubprocess' },\n"
                + "  [ordered]@{ privateBytes = [long]800MB; peakPrivateBytes = [long]950MB; workingSetBytes = [long]900MB; peakWorkingSetBytes = [long]950MB; responding = $true; freeBytes = [long]5GB; childProcesses = 1; childPrivateBytes = [long]100MB; childWorkingSetBytes = [long]120MB; childNames = 'x' },\n"
                + "  [ordered]@{ privateBytes = [long]700MB; peakPrivateBytes = [long]950MB; workingSetBytes = [long]800MB; peakWorkingSetBytes = [long]950MB; responding = $true; freeBytes = [long]5GB; childPrivateBytes = $null; childError = 'no CIM' })\n"
                + "Get-WindowPeaks -Samples $samples -FloorGB 2 | ConvertTo-Json -Compress"));
            Assert.Equal(3, (int)peaks["childProcessesMax"]);
            Assert.Equal(300, (long)peaks["sampledChildPrivateMib"]);
            Assert.Equal(350, (long)peaks["sampledChildWorkingSetMib"]);
            Assert.Equal(1200, (long)peaks["sampledTotalPrivateMib"]);
            Assert.Equal(1, (int)peaks["childMissing"]);
        }

        [Fact]
        public void R310_the_checks_of_the_run_keep_their_own_interval_and_the_samples_theirs()
        {
            // Two minutes of the wait loop on a clock that moves by the wait it is given: the checks every 5 s whatever the sample interval
            foreach (int sampleSeconds in new[] { 60, 5, 1 })
            {
                JObject counts = JObject.Parse(Pwsh(
                    "$now = [datetime]'2026-10-25T08:00:00'\n"
                    + "$end = $now.AddSeconds(120)\n"
                    + "$clock = New-LookClock -Start $now -CheckSeconds 5 -SampleSeconds " + sampleSeconds + "\n"
                    + "$checks = 0; $taken = 0; $waits = @(); $turns = 0\n"
                    + "while ($now -lt $end -and $turns++ -lt 1000) { $wait = Get-LookWait -Clock $clock -Now $now; $waits += $wait; $now = $now.AddMilliseconds($wait); $due = Get-DueLooks -Clock $clock -Now $now; if ($due.check) { $checks++ }; if ($due.sample) { $taken++ } }\n"
                    + "[ordered]@{ checks = $checks; samples = $taken; longest = ($waits | Measure-Object -Maximum).Maximum } | ConvertTo-Json -Compress"));

                Assert.Equal(24, (int)counts["checks"]);
                Assert.Equal(120 / sampleSeconds, (int)counts["samples"]);
                Assert.Equal(Math.Min(5, sampleSeconds) * 1000, (int)counts["longest"]);
            }
        }

        [WindowsFact]
        public void R310_the_exit_peaks_of_a_real_process_are_read_after_it_ended_and_hold_what_it_took()
        {
            JObject exit = JObject.Parse(Pwsh(
                "$info = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path, @('-NoProfile', '-Command', '$b = [byte[]]::new(300MB); [Array]::Fill($b, [byte]1)'))\n"
                + "$info.UseShellExecute = $false\n"
                + "$child = [Diagnostics.Process]::Start($info)\n"
                + "$child.WaitForExit()\n"
                + "[ordered]@{ exited = $child.HasExited; peaks = (Get-ExitPeaks -Process $child) } | ConvertTo-Json -Compress"));

            Assert.True((bool)exit["exited"]);
            Assert.True((long)exit["peaks"]["peakPrivateBytes"] >= 300L * 1024 * 1024);
            Assert.True((long)exit["peaks"]["peakWorkingSetBytes"] >= 300L * 1024 * 1024);

            // The words of the counters against the peaks .NET gives for a live process, so the index of each peak is right
            JObject live = JObject.Parse(Pwsh(
                "$self = Get-Process -Id $PID\n"
                + "$peaks = Get-ExitPeaks -Process $self\n"
                + "$self.Refresh()\n"
                + "[ordered]@{ peaks = $peaks; private = $self.PeakPagedMemorySize64; working = $self.PeakWorkingSet64 } | ConvertTo-Json -Compress"));
            const long slack = 32L * 1024 * 1024;
            Assert.InRange((long)live["peaks"]["peakPrivateBytes"], (long)live["private"] - slack, (long)live["private"]);
            Assert.InRange((long)live["peaks"]["peakWorkingSetBytes"], (long)live["working"] - slack, (long)live["working"]);
        }

        [WindowsFact]
        public void R310_no_sample_gives_no_peak_and_a_sample_of_a_real_process_holds_every_field()
        {
            foreach (string samples in new[] { "@()", "$null", "@($null)" })
            {
                JObject none = JObject.Parse(Pwsh("Get-WindowPeaks -Samples " + samples + " -FloorGB 2 | ConvertTo-Json -Compress"));
                Assert.Equal(0, (int)none["samples"]);
                Assert.Equal(JTokenType.Null, none["peakPrivateMib"].Type);
                Assert.Equal(JTokenType.Null, none["minFreeGib"].Type);
                Assert.False((bool)none["throughExit"]);
                Assert.Equal(0, (int)none["notResponding"]);
            }

            JObject sample = JObject.Parse(Pwsh("Get-WindowSample -Process (Get-Process -Id $PID) -TimeoutSeconds 2 | ConvertTo-Json -Compress"));
            foreach (string field in new[] { "privateBytes", "peakPrivateBytes", "workingSetBytes", "peakWorkingSetBytes", "freeBytes" })
            {
                Assert.True((long)sample[field] > 0, field);
            }
            Assert.Equal(JTokenType.Boolean, sample["responding"].Type);
            Assert.True(DateTime.Parse((string)sample["utc"]).Year >= 2026);
        }

        [Fact]
        public void R310_the_run_script_samples_every_window_from_the_config_and_prints_the_peaks_of_the_samples()
        {
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            JObject config = JObject.Parse(File.ReadAllText(RepoPath("config", "project.json")));

            Assert.Equal(5, (int)config["testHost"]["sampleSeconds"]);
            Assert.Contains(". (Join-Path $PSScriptRoot 'WindowSamples.ps1')", script);
            Assert.Contains("$sampleSeconds = [int]$config.testHost.sampleSeconds", script);
            // The watcher, refusal, log and start screen checks keep the 5 s they had before R310, the samples follow the config and the
            // deadline is looked at in every pass
            Assert.Contains("$checkSeconds = 5", script);
            Assert.Contains("$clock = New-LookClock -Start $start -CheckSeconds $checkSeconds -SampleSeconds $sampleSeconds", script);
            Assert.Contains("$process.WaitForExit((Get-LookWait -Clock $clock -Now (Get-Date)))", script);
            Assert.DoesNotContain("WaitForExit($sampleSeconds", script);
            int loop = script.IndexOf("$due = Get-DueLooks -Clock $clock -Now $now", StringComparison.Ordinal);
            int skip = script.IndexOf("if (-not $due.check) {", StringComparison.Ordinal);
            Assert.True(loop > 0 && skip > loop);
            foreach (string check in new[] { "if ($watcher.HasExited -and $watcher.ExitCode -eq 3)", "if ($refused -and (Test-Path $refused)", "$stopReason = \"the test host wrote no log in $NoLogMinutes minutes\"", "$quietSince.AddSeconds(60)" })
            {
                Assert.True(script.IndexOf(check, StringComparison.Ordinal) > skip, check);
            }
            Assert.True(script.IndexOf("if ($due.sample) {", StringComparison.Ordinal) > loop);
            Assert.Contains("$sample = Get-WindowSample -Process $process", script);
            Assert.Contains("Add-Content -Path $samplesPath", script);
            Assert.Contains("$peaks = Get-WindowPeaks -Samples $samples -FloorGB ([double]$config.siteBuild.minFreeGB)", script);
            Assert.Contains("$sampleFailures.Add(", script);
            Assert.Contains("$peaks.sampleFailures = @($sampleFailures)", script);
            Assert.Contains("$exitPeaks = Get-ExitPeaks -Process $process", script);
            Assert.Contains("-Exit $exitPeaks", script);
            Assert.Contains("peakMb     = $peaks.peakWorkingSetMib", script);
            Assert.Contains("memory     = $peaks", script);
            // One unit everywhere: the byte counts are divided by 1MB and 1GB of PowerShell, which are MiB and GiB, and named so
            Assert.Contains("peak private {2} MiB and peak working set {3} MiB", script);
            Assert.Contains("least free {7} GiB", script);
            Assert.DoesNotContain(" MB ", script.Substring(script.IndexOf("\"Memory of {0}", StringComparison.Ordinal)));
            string functions = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "WindowSamples.ps1"));
            Assert.DoesNotContain("Mb ", functions);
            Assert.DoesNotContain("Gb ", functions);
            // The samples are appended, so the file of an earlier run of the name moves aside first, also for a site upgrade whose
            // name is <code>_upgrade like an upgrade but whose switch -Upgrade is not given
            Assert.Contains("$old = @(Get-ChildItem $docs -Filter ('testhost_' + $name + '_*.json*') | Where-Object { $name -ne $Code -or $_.Name -notlike ('testhost_' + $Code + '_upgrade_*') })", script);
            Assert.True(script.IndexOf("$old | Move-Item -Destination $aside", StringComparison.Ordinal) < script.IndexOf("Add-Content -Path $samplesPath", StringComparison.Ordinal));
            // The old peak froze under 2 GiB: Max(Int32, Int32) threw past it and an empty catch hid the throw
            Assert.DoesNotContain("[Math]::Max($peak", script);
            Assert.DoesNotContain("$peak = 0", script);
        }

        private static RawSite Site()
        {
            return new RawSite
            {
                ActiveSite = "Internal",
                Sites = new List<string> { "Site B", "Internal" },
                PlaceName = "Makkah",
                ProjectPositionFeet = new[] { 1000 * UnitMath.FeetPerMetre, -500 * UnitMath.FeetPerMetre, 10 * UnitMath.FeetPerMetre },
                AngleRadians = -Math.PI / 2,
                Levels = new List<RawLevel>
                {
                    new RawLevel { Name = "Level 2", ElementId = 12, ElevationFeet = 3 * UnitMath.FeetPerMetre, ProjectElevationFeet = 3 * UnitMath.FeetPerMetre, ElevationBase = "Project Base Point" },
                    new RawLevel { Name = "Level 1", ElementId = 11, ElevationFeet = 0, ProjectElevationFeet = 0, ElevationBase = "Project Base Point" },
                    new RawLevel { Name = "Ground", ElementId = 10, ElevationFeet = 0, ProjectElevationFeet = 0, ElevationBase = "Survey Point" },
                },
                UnitSystem = "Metric",
                UnitTypeIds = new Dictionary<string, string>
                {
                    ["length"] = "autodesk.unit.unit:millimeters-1.0.1",
                    ["area"] = "autodesk.unit.unit:squareMeters-1.0.1",
                    ["angle"] = "autodesk.unit.unit:degrees-1.0.1",
                    ["slope"] = "autodesk.unit.unit:percentage-1.0.0",
                },
                LengthAccuracy = 1,
                Categories = new List<RawCategory>
                {
                    new RawCategory { Name = "Walls", Type = "Model" },
                    new RawCategory { Name = "Floors", Type = "Model" },
                    new RawCategory { Name = "Floors", Type = "Model" },
                    new RawCategory { Name = "Spot Elevations", Type = "Annotation" },
                    new RawCategory { Name = null, Type = null },
                },
            };
        }

        [Fact]
        public void R311_the_project_position_of_the_internal_origin_is_in_metres_and_its_angle_in_degrees_from_0_to_360()
        {
            SiteFacts facts = SiteFacts.Build(Site());

            Assert.Equal(1000, facts.ProjectPosition.EastWest, 9);
            Assert.Equal(-500, facts.ProjectPosition.NorthSouth, 9);
            Assert.Equal(10, facts.ProjectPosition.Elevation, 9);
            Assert.Equal(270, facts.ProjectPosition.AngleDeg, 9);
            Assert.Equal("Internal", facts.ActiveSite);
            Assert.Equal(new[] { "Internal", "Site B" }, facts.Sites);
            Assert.Equal("Makkah", facts.PlaceName);
        }

        [Fact]
        public void R311_levels_are_in_metres_by_elevation_then_name_as_the_api_gives_both_elevations_with_their_base()
        {
            List<LevelFact> levels = SiteFacts.Build(Site()).Levels;

            Assert.Equal(new[] { "Ground", "Level 1", "Level 2" }, levels.Select(level => level.Name));
            Assert.Equal(3, levels[2].Elevation, 9);
            Assert.Equal(3, levels[2].ProjectElevation, 9);
            Assert.Equal(12, levels[2].ElementId);
            Assert.Equal("Survey Point", levels[0].ElevationBase);
        }

        [Fact]
        public void R311_units_keep_the_name_of_each_unit_type_id_and_the_system_the_project_was_made_in()
        {
            UnitFacts units = SiteFacts.Build(Site()).Units;

            Assert.Equal("Metric", units.System);
            Assert.Equal("millimeters", units.Length);
            Assert.Equal("squareMeters", units.Area);
            Assert.Equal("degrees", units.Angle);
            Assert.Equal("percentage", units.Slope);
            Assert.Equal(1, units.LengthAccuracy);
            Assert.Equal("feetFractionalInches", SiteFacts.UnitName("autodesk.unit.unit:feetFractionalInches-1.0.1"));
            Assert.Equal("odd", SiteFacts.UnitName("odd"));
            Assert.Null(SiteFacts.UnitName(null));
        }

        [Theory]
        [InlineData("autodesk.unit.unit:meters-1.0.0", "METRIC")]
        [InlineData("autodesk.unit.unit:millimeters-1.0.1", "METRIC")]
        [InlineData("autodesk.unit.unit:centimeters-1.0.1", "METRIC")]
        [InlineData("autodesk.unit.unit:decimeters-1.0.1", "METRIC")]
        [InlineData("autodesk.unit.unit:metersCentimeters-1.0.0", "METRIC")]
        [InlineData("autodesk.unit.unit:feet-1.0.1", "IMPERIAL")]
        [InlineData("autodesk.unit.unit:inches-1.0.1", "IMPERIAL")]
        [InlineData("autodesk.unit.unit:feetFractionalInches-1.0.0", "IMPERIAL")]
        [InlineData("autodesk.unit.unit:fractionalInches-1.0.0", "IMPERIAL")]
        [InlineData("autodesk.unit.unit:usSurveyFeet-1.0.0", "IMPERIAL")]
        [InlineData("autodesk.unit.unit:stationingMeters-1.0.0", null)]
        [InlineData(null, null)]
        public void R311_the_length_unit_of_the_project_units_now_gives_its_system_beside_the_system_the_project_was_made_in(string lengthId, string system)
        {
            RawSite raw = Site();
            // A project made metric and set to feet later keeps METRIC as DisplayUnitSystem, its length format says what it has now
            raw.UnitSystem = "METRIC";
            raw.UnitTypeIds["length"] = lengthId;
            UnitFacts units = SiteFacts.Build(raw).Units;

            Assert.Equal("METRIC", units.System);
            Assert.Equal(system, units.LengthSystem);
            Assert.Equal(SiteFacts.UnitName(lengthId), units.Length);
        }

        [Theory]
        [InlineData(-1e-16, 0.0)]
        [InlineData(1e-16, 0.0)]
        [InlineData(2 * Math.PI - 1e-10, 0.0)]
        [InlineData(-2 * Math.PI + 1e-10, 0.0)]
        [InlineData(4 * Math.PI, 0.0)]
        [InlineData(-Math.PI / 2, 270.0)]
        [InlineData(Math.PI, 180.0)]
        [InlineData(-1e-8, 359.999999)]
        public void R311_the_angle_to_true_north_is_from_0_to_under_360_after_its_rounding(double radians, double degrees)
        {
            double angle = SiteFacts.AngleDegrees(radians);

            Assert.Equal(degrees, angle, 9);
            Assert.True(angle >= 0 && angle < 360);
            // Negative zero would print as -0
            Assert.NotEqual(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(angle));
        }

        [Fact]
        public void R311_the_angle_keeps_6_decimals_for_1_mm_at_5_km_and_the_length_accuracy_is_written_unrounded()
        {
            // 1 mm across 5 km is 0.0000115 degrees, 3 decimals of R2 would write 0
            double mm = Math.Atan2(0.001, 5000);
            RawSite raw = Site();
            raw.AngleRadians = mm;
            raw.LengthAccuracy = 0.0001;
            SiteFacts facts = SiteFacts.Build(raw);
            Assert.Equal(0.000011, facts.ProjectPosition.AngleDeg, 9);

            string json = JsonOut.ToJson(facts);
            Assert.Contains("\"angleDeg\": 1.1E-05", json);
            Assert.Contains("\"lengthAccuracy\": 0.0001", json);
            Assert.Contains("\"eastWest\": 1000.0", json);
            raw.AngleRadians = UnitMath.DegreesToRadians(12.3456789);
            Assert.Contains("\"angleDeg\": 12.345679", JsonOut.ToJson(SiteFacts.Build(raw)));
            Assert.Equal(6, SiteFacts.AngleDecimals);
        }

        [Fact]
        public void R311_elements_are_counted_by_category_type_and_name_and_those_with_no_category_apart()
        {
            List<CategoryCount> categories = SiteFacts.Build(Site()).Categories;

            Assert.Equal(new[] { "Annotation Spot Elevations 1", "Model Floors 2", "Model Walls 1", "(none) (no category) 1" },
                categories.Select(item => item.Type + " " + item.Name + " " + item.Count));
        }

        [Fact]
        public void R311_a_part_that_did_not_read_is_null_with_its_error_beside_it_and_an_empty_part_stays_empty()
        {
            RawSite raw = Site();
            raw.ActiveSite = null;
            raw.Sites = null;
            raw.PlaceName = null;
            raw.ProjectPositionFeet = null;
            raw.AngleRadians = null;
            raw.Levels = null;
            raw.UnitSystem = null;
            raw.UnitTypeIds = null;
            raw.LengthAccuracy = null;
            raw.Categories = null;
            raw.NotRead = new Dictionary<string, string> { ["units"] = "u failed", ["levels"] = "l failed", ["sites"] = "s failed", [SitePart.ProjectPosition] = "p failed", ["categories"] = "c failed" };
            SiteFacts facts = SiteFacts.Build(raw);
            Assert.Null(facts.Sites);
            Assert.Null(facts.ProjectPosition);
            Assert.Null(facts.Levels);
            Assert.Null(facts.Units);
            Assert.Null(facts.Categories);
            Assert.Equal(new[] { "categories", "levels", "projectPosition", "sites", "units" }, facts.NotRead.Keys);
            string json = JsonOut.ToJson(facts);
            foreach (string part in new[] { "sites", "projectPosition", "levels", "units", "categories" })
            {
                Assert.Contains("\"" + part + "\": null", json);
            }
            Assert.Contains("\"levels\": \"l failed\"", json);

            // A project with no level and no element has empty lists and no notRead
            RawSite empty = Site();
            empty.Levels = new List<RawLevel>();
            empty.Categories = new List<RawCategory>();
            empty.Sites = new List<string>();
            SiteFacts none = SiteFacts.Build(empty);
            Assert.Empty(none.Levels);
            Assert.Empty(none.Categories);
            Assert.Empty(none.Sites);
            Assert.Null(none.NotRead);
            string emptyJson = JsonOut.ToJson(none);
            Assert.Contains("\"levels\": []", emptyJson);
            Assert.DoesNotContain("notRead", emptyJson);

            var entry = new DocumentEntry { File = "site.rvt", Kind = "project", AngleToTrueNorthDeg = 0, Prototypes = new List<PrototypeRow>(), Site = SiteFacts.Build(Site()) };
            string entryJson = JsonOut.ToJson(entry);
            Assert.True(entryJson.IndexOf("\"prototypes\"", StringComparison.Ordinal) < entryJson.IndexOf("\"site\"", StringComparison.Ordinal));
            Assert.Contains("\"activeSite\": \"Internal\"", entryJson);
            Assert.Contains("\"eastWest\": 1000.0", entryJson);
        }

        [Fact]
        public void R311_the_not_read_key_of_each_part_is_its_field_name_and_a_sites_part_not_read_writes_every_field_as_null()
        {
            // Every key a part takes in notRead is a field of the site JSON, so projectPosition and never "project position"
            JObject full = JObject.Parse(JsonOut.ToJson(SiteFacts.Build(Site())));
            Assert.Equal(new[] { "sites", "projectPosition", "levels", "units", "categories" }, SitePart.All);
            foreach (string part in SitePart.All)
            {
                Assert.True(full.Property(part) != null, part);
            }

            // The sites part stands for activeSite, sites and placeName, so all three are written as null when it did not read
            RawSite raw = Site();
            raw.ActiveSite = null;
            raw.Sites = null;
            raw.PlaceName = null;
            raw.NotRead = new Dictionary<string, string> { [SitePart.Sites] = "s failed" };
            JObject json = JObject.Parse(JsonOut.ToJson(SiteFacts.Build(raw)));
            foreach (string field in new[] { "activeSite", "sites", "placeName" })
            {
                Assert.True(json.Property(field) != null, field);
                Assert.Equal(JTokenType.Null, json[field].Type);
            }
            Assert.Equal("s failed", (string)json["notRead"]["sites"]);
            Assert.Equal(JTokenType.Object, json["projectPosition"].Type);

            // The reader names each part by these keys, never by a text of its own
            string revit = File.ReadAllText(RepoPath("dotnet", "RevitPrototypeReader", "RevitSite.cs"));
            foreach (string name in new[] { "Sites", "ProjectPosition", "Levels", "Units", "Categories" })
            {
                Assert.Contains("Guard(SitePart." + name + ", raw, errors, () =>", revit);
            }
            Assert.DoesNotContain("Guard(\"", revit);
        }

        [Fact]
        public void R311_the_reader_fills_the_raw_site_in_revit_and_builds_it_in_common()
        {
            string extractor = File.ReadAllText(RepoPath("dotnet", "RevitPrototypeReader", "RevitExtractor.cs"));
            string revit = File.ReadAllText(RepoPath("dotnet", "RevitPrototypeReader", "RevitSite.cs"));

            Assert.Contains("entry.Site = SiteFacts.Build(RevitSite.Read(doc, errors));", extractor);
            Assert.Contains("ActiveProjectLocation", revit);
            Assert.Contains("ProjectElevation", revit);
            Assert.Contains("SpecTypeId.Length", revit);
            Assert.Contains("units.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId", revit);
            Assert.Contains("raw.NotRead[part] = ", revit);
            Assert.DoesNotContain("FeetToMetres", revit);
            Assert.DoesNotContain("Transaction", revit);
        }

        [Fact]
        public void R204_a_site_upgrade_may_name_its_source_copy_in_the_work_folder_and_its_target_follows_that_name()
        {
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            int at = script.IndexOf("'siteUpgrade' {", StringComparison.Ordinal);
            Assert.True(at > 0);
            string block = script.Substring(at, script.IndexOf("'read' {", at, StringComparison.Ordinal) - at);

            Assert.Contains("$original = if ($round.source) { [string]$round.source } else { Join-Path $work $build.answerKey.model }", block);
            Assert.Contains("if ((Split-Path $original) -ne $work) {", block);
            Assert.Contains("$target = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($original) + '_TEST-' + $Code + '.rvt')", block);
            Assert.True(block.IndexOf("(Split-Path $original) -ne $work", StringComparison.Ordinal) < block.IndexOf("$roundJob.source = $original", StringComparison.Ordinal));
            Assert.DoesNotContain("$keyStem", block);
        }
    }
}
