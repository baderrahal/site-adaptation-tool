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
    // Lane B 5b, the frozen Revit of 5b. A probe started Revit outside the test host and its own watcher left a security box open.
    // R108 the watcher starts before Revit and reads the tool from the installed manifests, R104 the freeze guard and the heartbeat
    public class FreezeGuardTests
    {
        private static string RepoPath(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }

        private static string Quoted(string text)
        {
            return "'" + text.Replace("'", "''") + "'";
        }

        private static string NewFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sa_freeze_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        // One command in PowerShell 7 with the freeze guard and the path functions loaded, run from the folder given
        private static Tuple<int, string, string> Pwsh(string command, string workingFolder = null)
        {
            string text = "$ErrorActionPreference = 'Stop'\n. " + Quoted(RepoPath("dotnet", "SiteAdaptation.TestHost", "TestHostPaths.ps1"))
                + "\n. " + Quoted(RepoPath("dotnet", "SiteAdaptation.TestHost", "FreezeGuard.ps1")) + "\n" + command;
            var info = new ProcessStartInfo("pwsh", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(text)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingFolder ?? Path.GetTempPath(),
            };
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                Assert.True(process.WaitForExit(90000), "pwsh did not end in 90 s");
                return Tuple.Create(process.ExitCode, output.Trim(), error.Trim());
            }
        }

        private static JObject PwshJson(string command, string workingFolder = null)
        {
            Tuple<int, string, string> run = Pwsh(command, workingFolder);
            Assert.True(run.Item1 == 0, run.Item3 + "\n" + run.Item2);
            return JObject.Parse(run.Item2);
        }

        private static string Script()
        {
            return File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
        }

        private static void Manifest(string folder, string file, string name, string id, string assembly, string vendor = "SITEADAPT")
        {
            File.WriteAllText(Path.Combine(folder, file), "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<RevitAddIns>\n  <AddIn Type=\"Application\">\n    <Name>" + name
                + "</Name>\n    <Assembly>" + assembly + "</Assembly>\n    <AddInId>" + id + "</AddInId>\n    <VendorId>" + vendor + "</VendorId>\n  </AddIn>\n</RevitAddIns>\n");
        }

        // The four repo manifests installed under their ids, each DLL at a place no code names, and a stranger's add-in beside them
        private static string FakeAddins()
        {
            string addins = NewFolder();
            foreach (string repoManifest in new[]
            {
                RepoPath("dotnet", "RevitPrototypeReader", "SiteAdaptation.addin"),
                RepoPath("dotnet", "SiteAdaptation.Placer", "SiteAdaptationPlacer_Revit2024.addin"),
                RepoPath("dotnet", "SiteAdaptation.TestHost", "SiteAdaptationTestHost_Revit2024.addin"),
                RepoPath("dotnet", "SiteAdaptation.AddIn", "SiteAdaptationAddIn_Revit2024.addin"),
            })
            {
                var xml = System.Xml.Linq.XDocument.Load(repoManifest);
                System.Xml.Linq.XElement addIn = xml.Root.Element("AddIn");
                string dll = Path.GetFileName(addIn.Element("Assembly").Value);
                Manifest(addins, "Installed_" + Path.GetFileName(repoManifest), addIn.Element("Name").Value, addIn.Element("AddInId").Value,
                    Path.Combine(addins, "Moved_" + Path.GetFileNameWithoutExtension(dll), dll));
            }
            Manifest(addins, "RcrcGreen.addin", "RCRC Green", "11111111-2222-3333-4444-555555555555", @"RcrcGreen\RcrcGreen.Revit.dll", "RCRC");
            return addins;
        }

        [Fact]
        public void R108_the_watcher_waits_for_the_pid_file_written_after_it_started()
        {
            var reads = new Queue<string>(new[] { null, "", "not a pid", "  4242\r\n" });
            DateTime now = new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
            int pauses = 0;

            int? pid = WatcherStart.WaitForPid(() => reads.Count > 0 ? reads.Dequeue() : null, () => now, now.AddMinutes(1), () => pauses++);

            Assert.Equal(4242, pid);
            Assert.Equal(3, pauses);

            // No pid by the deadline gives none, so the watcher ends with nothing to watch
            int late = 0;
            Assert.Null(WatcherStart.WaitForPid(() => null, () => now.AddSeconds(late++ * 30), now.AddMinutes(1), () => { }));
            Assert.Null(WatcherStart.ReadPid("0"));
            Assert.Null(WatcherStart.ReadPid("-5"));
        }

        // Bader, 29 Oct 2026: in a test host run no other add-in may block Revit. The RCRC Green box as Revit 2024 showed it in the
        // runs of 5b gets Do Not Load against the installed tool, the add-in of the probe among the tool, and each answer is kept
        [Fact]
        public void R108_any_add_in_that_is_not_site_adaptation_gets_do_not_load_and_each_answer_is_logged()
        {
            var texts = new[]
            {
                "The publisher of this add-in could not be verified. What do you want to do?",
                "Name:                RCRC Green\r\nPublisher:           Unknown Publisher\r\nLocation:            C:\\Users\\x\\AppData\\Roaming\\Autodesk\\Revit\\Addins\\2024\\RcrcGreen\\RcrcGreen.Revit.dll\r\nIssuer:                 None",
            };
            SecurityDialogEntry rcrc = SecurityDialogs.Read("Security - Unsigned Add-In", "#32770", texts, new[] { "Always Load", "Load Once", "Do Not Load" });
            var ids = new[] { "c330760d-8384-4e69-b4a2-2fe26e0c45fe", "0edeabc3-f57d-4177-8427-b90d0dbf73d0", "5b5b5b5b-2222-4333-8444-555566667777" };
            var marks = new[] { "Site Adaptation", "SiteAdaptation.AddIn.dll", "Site Adaptation test host", "SiteAdaptation.TestHost.dll", "SA 5b hang probe", "ProbeHang.dll" };
            var paths = new[] { @"C:\Users\x\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationAddIn\SiteAdaptation.AddIn.dll" };

            Assert.Equal("RCRC Green", rcrc.AddInName);
            WatchAction action = SecurityDialogs.Decide(rcrc, ids, marks, paths);
            Assert.Equal(WatchAction.DoNotLoad, action);
            Assert.Equal(SecurityDialogs.DoNotLoad, SecurityDialogs.ButtonFor(action));

            // The watcher adds every box it answered to its report and writes the report at once, before the next box
            string program = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs"));
            int click = program.IndexOf("if (label != null && Click(window, entry, label))", StringComparison.Ordinal);
            int add = program.IndexOf("report.Dialogs.Add(entry);", click, StringComparison.Ordinal);
            int write = program.IndexOf("JsonOut.Write(options.Out, report);", add, StringComparison.Ordinal);
            Assert.True(click > 0 && add > click && write > add);
        }

        [Fact]
        public void R108_the_watcher_takes_a_pid_file_and_writes_its_report_before_it_waits()
        {
            string program = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs"));

            Assert.Contains("case \"--pid-file\":", program);
            Assert.Equal("waiting for the pid of Revit", WatcherStart.WaitingResult);
            int ready = program.IndexOf("report.Result = WatcherStart.WaitingResult", StringComparison.Ordinal);
            int wait = program.IndexOf("WatcherStart.WaitForPid(", StringComparison.Ordinal);
            Assert.True(ready > 0 && wait > ready, "the ready report comes before the wait for the pid");
        }

        [Fact]
        public void R108_the_run_script_starts_the_watcher_before_revit_and_hands_it_the_pid_after()
        {
            string script = Script();
            int watcher = script.IndexOf("$watcher = Start-WatcherFirst", StringComparison.Ordinal);
            int revit = script.IndexOf("$process = [Diagnostics.Process]::Start($info)", StringComparison.Ordinal);
            int pid = script.IndexOf("Set-WatchedPid -Path $pidPath -ProcessId $process.Id", StringComparison.Ordinal);

            Assert.True(watcher > 0, "the watcher is started by Start-WatcherFirst");
            Assert.True(revit > watcher, "Revit starts after the watcher");
            Assert.True(pid > revit, "the pid goes to the watcher after Revit started");
            Assert.Contains("'--pid-file', $pidPath", script);
            Assert.DoesNotContain("'--pid', $process.Id", script);
            Assert.Contains(". (Join-Path $PSScriptRoot 'FreezeGuard.ps1')", script);
        }

        [WindowsFact]
        public void R108_start_watcher_first_waits_for_the_ready_report_from_any_folder_and_fails_when_the_watcher_ends_first()
        {
            string folder = NewFolder();
            string other = NewFolder();
            string ready = Path.Combine(folder, "watch.json");
            // A stand in for the watcher, it writes its report after 2 s and then waits as the watcher waits for the pid
            JObject run = PwshJson(
                "$exe = (Get-Command pwsh).Source\n"
                + "$p = Start-WatcherFirst -WatcherExe $exe -Arguments @('-NoProfile', '-Command', \"Start-Sleep 2; Set-Content -LiteralPath '" + ready.Replace("'", "''") + "' -Value '{}'; Start-Sleep 20\") -ReadyPath "
                + Quoted(ready) + " -TimeoutSeconds 30\n"
                + "$alive = -not $p.HasExited\n"
                + "Stop-Process -Id $p.Id -Force\n"
                + "[pscustomobject]@{ alive = $alive; ready = (Test-Path " + Quoted(ready) + "); at = (Get-Location).Path } | ConvertTo-Json", other);

            Assert.True((bool)run["alive"]);
            Assert.True((bool)run["ready"]);
            Assert.Equal(other.TrimEnd('\\'), ((string)run["at"]).TrimEnd('\\'));

            Tuple<int, string, string> early = Pwsh(
                "$exe = (Get-Command pwsh).Source\n"
                + "Start-WatcherFirst -WatcherExe $exe -Arguments @('-NoProfile', '-Command', 'exit 2') -ReadyPath " + Quoted(Path.Combine(folder, "never.json")) + " -TimeoutSeconds 30", other);
            Assert.NotEqual(0, early.Item1);
            Assert.Contains("the dialog watcher ended before it was ready", early.Item3 + early.Item2);
        }

        [WindowsFact]
        public void R108_the_tool_add_ins_are_named_by_the_installed_manifests_and_their_dlls_where_those_put_them()
        {
            string addins = FakeAddins();
            JObject tool = PwshJson("Get-ToolAddIns -Repo " + Quoted(RepoPath()) + " -Addins " + Quoted(addins) + " | ConvertTo-Json -Depth 3");
            List<string> marks = tool["marks"].Values<string>().ToList();
            List<string> paths = tool["paths"].Values<string>().ToList();

            Assert.Equal(4, tool["ids"].Count());
            Assert.Contains("Site Adaptation", marks);
            Assert.Contains("SiteAdaptation.AddIn.dll", marks);
            Assert.DoesNotContain("RCRC Green", marks);
            Assert.Contains(Path.Combine(addins, "Moved_SiteAdaptation.AddIn", "SiteAdaptation.AddIn.dll"), paths);
            Assert.All(paths, path => Assert.Contains(Path.DirectorySeparatorChar + "Moved_", path));
            Assert.All(tool["installed"].Values<string>(), file => Assert.StartsWith(Path.Combine(addins, "Installed_"), file));

            // An add-in of the tool with no installed manifest stops the run before Revit starts
            File.Delete(Directory.GetFiles(addins, "Installed_SiteAdaptationAddIn_Revit2024.addin").Single());
            Tuple<int, string, string> missing = Pwsh("Get-ToolAddIns -Repo " + Quoted(RepoPath()) + " -Addins " + Quoted(addins));
            Assert.NotEqual(0, missing.Item1);
            Assert.Contains("c330760d-8384-4e69-b4a2-2fe26e0c45fe", missing.Item3 + missing.Item2);
            Assert.Contains("is not installed", missing.Item3 + missing.Item2);
        }

        [WindowsFact]
        public void R108_paths_code_names_no_dll_of_the_tool()
        {
            string paths = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "TestHostPaths.ps1"));

            Assert.DoesNotContain("$ToolDlls", paths);
            foreach (string dll in new[] { "RevitPrototypeReader.dll", "SiteAdaptation.Placer.dll", "SiteAdaptation.TestHost.dll", "SiteAdaptation.AddIn.dll" })
            {
                Assert.DoesNotContain(dll, paths);
            }
        }

        [WindowsFact]
        public void R108_a_probe_is_read_from_its_manifest_given_from_the_folder_the_run_starts_in()
        {
            string folder = NewFolder();
            Manifest(folder, "Probe5b.addin", "SA 5b probe", "5b5b5b5b-0000-4000-8000-000000000001", Path.Combine(folder, "Probe5b.dll"), "SAPROBE");
            JObject probe = PwshJson("Get-ProbeAddIn -Manifest 'Probe5b.addin' | ConvertTo-Json", folder);

            Assert.Equal("5b5b5b5b-0000-4000-8000-000000000001", (string)probe["id"]);
            Assert.Equal("SA 5b probe", (string)probe["name"]);
            Assert.Equal(Path.Combine(folder, "Probe5b.dll"), (string)probe["path"]);
            Assert.Equal(Path.Combine(folder, "Probe5b.addin"), (string)probe["manifest"]);
            Assert.Equal("Probe5b", (string)probe["code"]);

            string script = Script();
            Assert.Contains("[string]$ProbeManifest", script);
            Assert.Contains("$probe = Get-ProbeAddIn -Manifest $ProbeManifest", script);
            Assert.Contains("$info.Environment['SITEADAPT_PROBE_LOG'] = $logPath", script);
            Assert.Contains("$info.Environment['SITEADAPT_PROBE_HEARTBEAT'] = $beatPath", script);
            Assert.Contains("Remove-Item -LiteralPath $probeInstalled", script);
        }

        [WindowsFact]
        public void R104_the_freeze_guard_names_the_first_limit_a_run_crosses()
        {
            JObject reasons = PwshJson(
                "$s = [datetime]'2026-10-29T09:00:00Z'\n"
                + "$r = [ordered]@{}\n"
                + "$r.fine = Get-FreezeReason -Start $s -Now $s.AddMinutes(4) -FirstStepAt $null -LastBeatAt $null -NotRespondingSince $null -HasJob $true\n"
                + "$r.noStep = Get-FreezeReason -Start $s -Now $s.AddMinutes(5.1) -FirstStepAt $null -LastBeatAt $s.AddMinutes(5) -NotRespondingSince $null -HasJob $true\n"
                + "$r.noBeat = Get-FreezeReason -Start $s -Now $s.AddMinutes(20) -FirstStepAt $s.AddMinutes(1) -LastBeatAt $s.AddMinutes(9) -NotRespondingSince $null -HasJob $true\n"
                + "$r.neverBeat = Get-FreezeReason -Start $s -Now $s.AddMinutes(10.5) -FirstStepAt $s.AddMinutes(1) -LastBeatAt $null -NotRespondingSince $null -HasJob $true\n"
                + "$r.beatOk = Get-FreezeReason -Start $s -Now $s.AddMinutes(20) -FirstStepAt $s.AddMinutes(1) -LastBeatAt $s.AddMinutes(11) -NotRespondingSince $null -HasJob $true\n"
                + "$r.hung = Get-FreezeReason -Start $s -Now $s.AddMinutes(12) -FirstStepAt $s.AddMinutes(1) -LastBeatAt $s.AddMinutes(12) -NotRespondingSince $s.AddMinutes(6.9) -HasJob $true\n"
                + "$r.hungShort = Get-FreezeReason -Start $s -Now $s.AddMinutes(12) -FirstStepAt $s.AddMinutes(1) -LastBeatAt $s.AddMinutes(12) -NotRespondingSince $s.AddMinutes(8) -HasJob $true\n"
                + "$r.dryHung = Get-FreezeReason -Start $s -Now $s.AddMinutes(12) -FirstStepAt $null -LastBeatAt $null -NotRespondingSince $s.AddMinutes(6) -HasJob $false\n"
                + "$r.dryFine = Get-FreezeReason -Start $s -Now $s.AddMinutes(12) -FirstStepAt $null -LastBeatAt $null -NotRespondingSince $null -HasJob $false\n"
                + "$r.longHung = Get-FreezeReason -Start $s -Now $s.AddMinutes(12) -FirstStepAt $s.AddMinutes(1) -LastBeatAt $s.AddMinutes(12) -NotRespondingSince $s.AddMinutes(6) -HasJob $true -NotRespondingMinutes 30\n"
                + "[pscustomobject]$r | ConvertTo-Json");

            Assert.Equal(JTokenType.Null, reasons["fine"].Type);
            Assert.Equal("no first log step within 5 minutes of the Revit start", (string)reasons["noStep"]);
            Assert.Equal("no heartbeat for 10 minutes", (string)reasons["noBeat"]);
            Assert.Equal("no heartbeat for 10 minutes", (string)reasons["neverBeat"]);
            Assert.Equal(JTokenType.Null, reasons["beatOk"].Type);
            Assert.Equal("Revit not responding for 5 minutes", (string)reasons["hung"]);
            Assert.Equal(JTokenType.Null, reasons["hungShort"].Type);
            Assert.Equal("Revit not responding for 5 minutes", (string)reasons["dryHung"]);
            Assert.Equal(JTokenType.Null, reasons["dryFine"].Type);
            Assert.Equal(JTokenType.Null, reasons["longHung"].Type);
        }

        [WindowsFact]
        public void R104_the_freeze_clock_reads_the_first_step_and_the_heartbeat_written_since_the_start()
        {
            string folder = NewFolder();
            string log = Path.Combine(folder, "testhost_X1_log.json");
            string beat = Path.Combine(folder, "testhost_X1_heartbeat.json");
            File.WriteAllText(log, "{\"steps\": []}");
            JObject before = PwshJson("$c = Read-FreezeClock -LogPath " + Quoted(log) + " -BeatPath " + Quoted(beat) + " -Start ([datetime]::Now.AddMinutes(-1))\n"
                + "[pscustomobject]@{ step = $c.firstStepAt; beat = $c.lastBeatAt } | ConvertTo-Json");
            Assert.Equal(JTokenType.Null, before["step"].Type);
            Assert.Equal(JTokenType.Null, before["beat"].Type);

            File.WriteAllText(log, "{\"steps\": [{\"name\": \"open\", \"startedUtc\": \"2026-10-29T09:01:00Z\"}]}");
            File.WriteAllText(beat, "{\"utc\": \"2026-10-29T09:02:00Z\"}");
            JObject after = PwshJson("$c = Read-FreezeClock -LogPath " + Quoted(log) + " -BeatPath " + Quoted(beat) + " -Start ([datetime]::Now.AddMinutes(-1))\n"
                + "[pscustomobject]@{ step = ($null -ne $c.firstStepAt); beat = ($null -ne $c.lastBeatAt) } | ConvertTo-Json");
            Assert.True((bool)after["step"]);
            Assert.True((bool)after["beat"]);

            // Files of an earlier run, written before this start, never count
            JObject stale = PwshJson("$c = Read-FreezeClock -LogPath " + Quoted(log) + " -BeatPath " + Quoted(beat) + " -Start ([datetime]::Now.AddMinutes(5))\n"
                + "[pscustomobject]@{ step = $c.firstStepAt; beat = $c.lastBeatAt } | ConvertTo-Json");
            Assert.Equal(JTokenType.Null, stale["step"].Type);
            Assert.Equal(JTokenType.Null, stale["beat"].Type);
        }

        [WindowsFact]
        public void R104_a_frozen_run_logs_the_windows_and_the_journal_tail_ends_that_process_only_and_fails_its_step()
        {
            string folder = NewFolder();
            string journals = NewFolder();
            string log = Path.Combine(folder, "testhost_X1_log.json");
            File.WriteAllText(log, "{\"steps\": [{\"name\": \"done\", \"result\": \"ok\", \"endedUtc\": \"2026-10-29T09:01:00Z\"}, {\"name\": \"hang\", \"startedUtc\": \"2026-10-29T09:01:00Z\"}]}");
            File.WriteAllText(Path.Combine(journals, "journal.0001.txt"), "old journal");
            File.SetLastWriteTimeUtc(Path.Combine(journals, "journal.0001.txt"), DateTime.UtcNow.AddHours(-2));
            File.WriteAllLines(Path.Combine(journals, "journal.0002.txt"), Enumerable.Range(1, 60).Select(line => "' line " + line));

            JObject result = PwshJson(
                "$exe = (Get-Command pwsh).Source\n"
                + "$frozen = Start-Process -FilePath $exe -ArgumentList @('-NoProfile', '-Command', 'Add-Type -AssemblyName System.Windows.Forms; $f = [Windows.Forms.Form]::new(); $f.Text = ''freeze probe window''; $null = $f.Handle; Start-Sleep 120') -PassThru -WindowStyle Hidden\n"
                + "$other = Start-Process -FilePath $exe -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep 120') -PassThru -WindowStyle Hidden\n"
                + "for ($i = 0; $i -lt 60 -and @(Get-ProcessWindows -ProcessId $frozen.Id | Where-Object title -eq 'freeze probe window').Count -eq 0; $i++) { Start-Sleep -Milliseconds 500 }\n"
                + "$stop = Stop-FrozenRevit -Process $frozen -Reason 'Revit not responding for 5 minutes' -LogPath " + Quoted(log) + " -JournalFolder " + Quoted(journals)
                + " -Start ([datetime]::Now.AddMinutes(-30)) -TailLines 40\n"
                + "$frozen.WaitForExit(20000) | Out-Null\n"
                + "$otherAlive = -not $other.HasExited\n"
                + "Stop-Process -Id $other.Id -Force\n"
                + "[pscustomobject]@{ frozenExited = $frozen.HasExited; otherAlive = $otherAlive; stop = $stop } | ConvertTo-Json -Depth 6");

            Assert.True((bool)result["frozenExited"]);
            Assert.True((bool)result["otherAlive"]);
            Assert.Contains(result["stop"]["windows"], window => (string)window["title"] == "freeze probe window" && !string.IsNullOrEmpty((string)window["class"]));
            Assert.EndsWith("journal.0002.txt", (string)result["stop"]["journal"]);
            List<string> tail = result["stop"]["journalTail"].Values<string>().ToList();
            Assert.Equal(40, tail.Count);
            Assert.Equal("' line 60", tail.Last());

            JObject written = JObject.Parse(File.ReadAllText(log));
            Assert.Equal("ok", (string)written["steps"][0]["result"]);
            Assert.Equal("failed", (string)written["steps"][1]["result"]);
            Assert.Equal("freeze guard: Revit not responding for 5 minutes", (string)written["steps"][1]["detail"]);
            Assert.Equal("failed", (string)written["result"]);
            Assert.Equal("Revit not responding for 5 minutes", (string)written["freeze"]["reason"]);
            Assert.Equal(40, written["freeze"]["journalTail"].Count());
            Assert.NotEmpty(written["freeze"]["windows"]);
        }

        [Fact]
        public void R104_the_run_script_looks_for_a_freeze_in_every_check_and_stops_with_its_reason()
        {
            string script = Script();
            int skip = script.IndexOf("if (-not $due.check) {", StringComparison.Ordinal);
            int clock = script.IndexOf("$freezeClock = Read-FreezeClock -LogPath $logPath -BeatPath $beatPath -Start $start", StringComparison.Ordinal);
            int reason = script.IndexOf("$freeze = Get-FreezeReason", StringComparison.Ordinal);
            int stop = script.IndexOf("$frozen = Stop-FrozenRevit -Process $process -Reason $freeze -LogPath $logPath", StringComparison.Ordinal);

            Assert.True(skip > 0 && clock > skip && reason > clock && stop > reason);
            Assert.Contains("$stopReason = 'freeze guard: ' + $freeze", script);
            Assert.Contains("[int]$FirstStepMinutes = 5", script);
            Assert.Contains("[int]$HeartbeatMinutes = 10", script);
            Assert.Contains("[int]$NotRespondingMinutes = 5", script);
            Assert.Contains("$notRespondingSince", script);
        }

        [Fact]
        public void R104_the_watchdog_beats_on_every_look_and_the_beat_names_the_step()
        {
            var log = new TestHostLog();
            DateTime now = new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
            var runner = new TestHostRunner(log, _ => { }, () => now);
            var beats = new List<string>();
            var watchdog = new TestHostWatchdog(runner, now.AddMinutes(27), () => now, _ => { }, TimeSpan.FromHours(1), beats.Add);

            watchdog.Look();
            runner.Run(new[] { new JobStep { Name = "open", LimitMinutes = 2, Action = () => { watchdog.Look(); return null; } } });
            watchdog.Dispose();

            Assert.Equal(2, beats.Count);
            JObject first = JObject.Parse(beats[0]);
            JObject second = JObject.Parse(beats[1]);
            Assert.Equal(JTokenType.Null, first["step"].Type);
            Assert.Equal("open", (string)second["step"]);
            Assert.Equal(now, (DateTime)second["utc"]);
            Assert.Equal(Process.GetCurrentProcess().Id, (int)second["pid"]);
        }

        [Fact]
        public void R104_the_job_session_writes_the_heartbeat_beside_its_log()
        {
            string session = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "JobSession.cs"));

            Assert.Contains("TestHostGuard.IsUpgrade(job) ? \"upgrade_heartbeat\" : \"heartbeat\"", session);
            Assert.Contains("new TestHostWatchdog(runner, deadlineUtc, () => DateTime.UtcNow, Kill, TimeSpan.FromSeconds(10), Beat)", session);
            // Joined by the separator of the system, so the net8.0 leg on Linux holds it too
            Assert.Equal(Path.Combine(@"C:\Docs", "SiteAdaptation", "testhost_C10_heartbeat.json"), TestHostGuard.OutputPath(@"C:\Docs", "C10", "heartbeat"));
        }
    }
}
