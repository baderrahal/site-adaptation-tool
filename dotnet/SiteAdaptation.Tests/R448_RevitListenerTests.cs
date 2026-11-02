using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R448. The job folder of the Revit listener, with no Revit: the queue, the result, the check of a job and the scripts. What needs
    // Revit is listed as not run in steps/revit-listener.md
    public sealed class R448_RevitListenerTests : IDisposable
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private static readonly DateTime Start = new DateTime(2026, 11, 2, 6, 0, 0, DateTimeKind.Utc);

        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-listener-" + Guid.NewGuid().ToString("N"));
        private readonly RevitListenerSetting setting;
        private readonly RevitListenerQueue queue;
        private readonly RevitListenerLoop loop;
        private DateTime now = Start;

        public R448_RevitListenerTests()
        {
            setting = Setting(root, "1");
            queue = new RevitListenerQueue(setting);
            queue.Prepare();
            loop = new RevitListenerLoop(queue, RevitListenerCheck.Read, () => now);
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static RevitListenerSetting Setting(string folder, string slot, string inputRoots = null)
        {
            var values = new Dictionary<string, string>
            {
                { RevitListenerSetting.RootVariable, folder },
                { RevitListenerSetting.SlotVariable, slot },
                { RevitListenerSetting.InputRootsVariable, inputRoots },
            };
            return RevitListenerSetting.Read(name => values.TryGetValue(name, out string value) ? value : null);
        }

        private static string JobText(string runId, IEnumerable<string> inputs = null, string model = null)
        {
            var job = new JObject { ["runId"] = runId, ["buttons"] = new JArray(1, 2), ["model"] = model ?? Path.Combine("run", "P1-TEST-NEW.rvt") };
            if (inputs != null)
            {
                job["inputs"] = new JArray(inputs.ToArray<object>());
            }
            return job.ToString();
        }

        private string Drop(string runId, string text = null, DateTime? arrival = null)
        {
            string path = Path.Combine(setting.InFolder, runId + RevitListenerQueue.JobTail);
            File.WriteAllText(path, text ?? JobText(runId));
            File.SetLastWriteTimeUtc(path, arrival ?? now);
            return path;
        }

        private JObject Result(string runId)
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(setting.OutFolder, runId + RevitListenerQueue.ResultTail)));
        }

        private static string RepoFile(params string[] parts)
        {
            return Path.Combine(new[] { ThemeGateTests.RepoRoot() }.Concat(parts).ToArray());
        }

        private static string HostFile(string name)
        {
            return RepoFile("dotnet", "SiteAdaptation.TestHost", name);
        }

        private static string Quoted(string text)
        {
            return "'" + text.Replace("'", "''") + "'";
        }

        private static Tuple<int, string, string> Pwsh(string command)
        {
            string text = "$ErrorActionPreference = 'Stop'\n. " + Quoted(HostFile("TestHostPaths.ps1")) + "\n. " + Quoted(HostFile("FreezeGuard.ps1"))
                + "\n. " + Quoted(HostFile("ListenerPaths.ps1")) + "\n" + command;
            var info = new ProcessStartInfo("pwsh", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(text)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(info))
            {
                var error = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return Tuple.Create(process.ExitCode, output, error.Result);
            }
        }

        [Fact]
        public void R448_the_listener_is_off_with_the_setting_unset()
        {
            Assert.Null(RevitListenerSetting.Read(name => null));
            Assert.Null(RevitListenerSetting.Read(name => name == RevitListenerSetting.RootVariable ? "  " : null));
            Assert.Null(RevitListenerSetting.Read(name => name == RevitListenerSetting.SlotVariable ? "1" : null));

            // TestHostApp keeps the names as text, a type of Common must not load before the resolver. With no root it runs on as before
            string app = File.ReadAllText(HostFile("TestHostApp.cs"));
            Assert.Contains("private const string ListenerVariable = \"" + RevitListenerSetting.RootVariable + "\"", app);
            int token = app.IndexOf("if (string.IsNullOrEmpty(token))", StringComparison.Ordinal);
            int listener = app.IndexOf("if (string.IsNullOrEmpty(jobPath) && !string.IsNullOrEmpty(listenerRoot))", StringComparison.Ordinal);
            int job = app.IndexOf("if (string.IsNullOrEmpty(jobPath) || !File.Exists(jobPath))", StringComparison.Ordinal);
            Assert.True(token > 0 && token < listener && listener < job, "token, then the listener, then the job file as before");
        }

        [Theory]
        [InlineData("1", null)]
        [InlineData("2", null)]
        [InlineData("0", "slot")]
        [InlineData("a", "slot")]
        [InlineData("", "slot")]
        public void R448_the_slot_is_a_value_of_the_setting_and_names_the_folders(string slot, string refused)
        {
            RevitListenerSetting one = Setting(root, slot);
            if (refused != null)
            {
                Assert.Contains(refused, one.Refusal());
                return;
            }
            Assert.Null(one.Refusal());
            Assert.Equal(Path.Combine(root, "in-" + slot), one.InFolder);
            Assert.Equal(Path.Combine(root, "out-" + slot), one.OutFolder);
        }

        [Fact]
        public void R448_a_folder_root_inside_an_input_root_is_refused()
        {
            string inputs = Path.Combine(root, "real");
            Assert.Contains("input root", Setting(Path.Combine(inputs, "jobs"), "1", root + "|" + inputs).Refusal());
            Assert.Contains("input root", Setting(inputs.ToUpperInvariant(), "1", inputs).Refusal());
            Assert.Null(Setting(inputs + "2", "1", inputs).Refusal());
        }

        [Fact]
        public void R448_a_job_in_gives_a_result_out()
        {
            string dropped = Drop("run1");
            loop.Look();
            Assert.False(File.Exists(dropped));
            Assert.True(File.Exists(Path.Combine(setting.ClaimedFolder, "run1" + RevitListenerQueue.JobTail)));

            RevitListenerRun run = loop.Take();
            Assert.Equal("run1", run.Job.RunId);
            Assert.Equal(new[] { 1, 2 }, run.Job.Buttons);
            now = now.AddSeconds(90);
            loop.End(run, RevitListenerResult.Ok, null);
            Assert.False(File.Exists(Path.Combine(setting.OutFolder, "run1" + RevitListenerQueue.ResultTail)));
            loop.Look();

            JObject result = Result("run1");
            Assert.Equal("run1", (string)result["runId"]);
            Assert.Equal("ok", (string)result["result"]);
            Assert.Null((string)result["error"]);
            Assert.Equal(90, (double)result["seconds"]);
            Assert.Empty(Directory.GetFiles(setting.OutFolder, "*" + RevitListenerFiles.WritingTail));
        }

        [Fact]
        public void R448_a_failed_job_writes_the_error_and_the_next_job_runs()
        {
            Drop("bad1", arrival: now);
            Drop("next", arrival: now.AddSeconds(1));
            loop.Look();
            RevitListenerRun failed = loop.Take();
            Assert.Null(loop.Take());
            loop.End(failed, RevitListenerResult.Failed, "open: it threw");
            loop.Look();

            Assert.Equal("failed", (string)Result("bad1")["result"]);
            Assert.Equal("open: it threw", (string)Result("bad1")["error"]);
            Assert.Equal("next", loop.Take().Job.RunId);
        }

        [Fact]
        public void R448_the_error_of_a_failed_job_names_its_failed_steps_with_the_first_line_only()
        {
            var log = new TestHostLog();
            log.Steps.Add(new TestHostStep { Name = "open", Result = "ok" });
            log.Steps.Add(new TestHostStep { Name = "7", Result = "failed", Exception = "System.Exception: it threw\r\n   at Somewhere" });
            log.Steps.Add(new TestHostStep { Name = "8", Result = "skipped" });
            Assert.Equal("7: System.Exception: it threw", RevitListenerResult.ErrorOf(log));
            Assert.Null(RevitListenerResult.ErrorOf(new TestHostLog()));
        }

        [Fact]
        public void R448_a_job_past_its_limit_writes_the_error_once_and_the_next_job_waits_for_revit()
        {
            Drop("slow", arrival: now);
            Drop("next", arrival: now.AddSeconds(1));
            loop.Look();
            RevitListenerRun slow = loop.Take();
            // The watchdog on its own thread while Revit is still in the step
            loop.End(slow, RevitListenerResult.Timeout, "step 7 ran over its limit of 20 minutes");
            loop.Look();
            Assert.Equal("timeout", (string)Result("slow")["result"]);
            // Revit comes back from the step, its end changes nothing
            loop.End(slow, RevitListenerResult.Ok, null);
            loop.Look();
            Assert.Equal("timeout", (string)Result("slow")["result"]);
            Assert.Single(Directory.GetFiles(setting.OutFolder, "slow.result*"));
            Assert.Equal("next", loop.Take().Job.RunId);
        }

        [Fact]
        public void R448_past_the_limit_only_the_steps_that_always_run_are_left()
        {
            bool stop = false;
            var ran = new List<string>();
            var steps = new[]
            {
                new JobStep { Name = "open" },
                new JobStep { Name = "7" },
                new JobStep { Name = "write", Always = true },
            };
            foreach (JobStep step in RevitListenerSteps.Until(steps, () => stop))
            {
                ran.Add(step.Name);
                stop = true;
            }
            Assert.Equal(new[] { "open", "write" }, ran);
        }

        [Fact]
        public void R448_jobs_run_first_in_first_out_by_arrival_and_one_at_a_time()
        {
            Drop("zeta", arrival: now);
            Drop("alpha", arrival: now.AddSeconds(5));
            Drop("mid", arrival: now.AddSeconds(2));
            var order = new List<string>();
            for (int look = 0; look < 3; look++)
            {
                loop.Look();
                RevitListenerRun run = loop.Take();
                order.Add(run.Job.RunId);
                loop.Look();
                Assert.Null(loop.Take());
                Assert.Equal(2 - look, Directory.GetFiles(setting.InFolder, "*" + RevitListenerQueue.JobTail).Length);
                loop.End(run, RevitListenerResult.Ok, null);
            }
            Assert.Equal(new[] { "zeta", "mid", "alpha" }, order);
        }

        [Theory]
        [InlineData("{ this is no json")]
        [InlineData("")]
        [InlineData("null")]
        [InlineData("{\"runId\": \"another\"}")]
        [InlineData("{\"runId\": \"../up\"}")]
        public void R448_a_bad_job_file_gets_an_error_result_and_the_queue_goes_on(string text)
        {
            Drop("broken", text, now);
            Drop("good", arrival: now.AddSeconds(1));
            loop.Look();
            Assert.Equal("refused", (string)Result("broken")["result"]);
            Assert.False(string.IsNullOrEmpty((string)Result("broken")["error"]));
            Assert.True(File.Exists(Path.Combine(setting.ClaimedFolder, "broken" + RevitListenerQueue.JobTail)));
            Assert.Equal("good", loop.Take().Job.RunId);
        }

        [Fact]
        public void R448_a_half_written_job_file_is_not_taken()
        {
            string writing = Path.Combine(setting.InFolder, "run1" + RevitListenerQueue.JobTail + RevitListenerFiles.WritingTail);
            File.WriteAllText(writing, "{ \"runId\": \"ru");
            loop.Look();
            Assert.Null(loop.Take());
            Assert.True(File.Exists(writing));
            Assert.Empty(Directory.GetFiles(setting.OutFolder, "*" + RevitListenerQueue.ResultTail));

            File.WriteAllText(writing, JobText("run1"));
            File.Move(writing, Path.Combine(setting.InFolder, "run1" + RevitListenerQueue.JobTail));
            loop.Look();
            Assert.Equal("run1", loop.Take().Job.RunId);
        }

        [WindowsFact]
        public void R448_a_job_file_its_writer_still_holds_is_not_taken_and_the_next_one_is()
        {
            string held = Drop("held", "{ \"runId\": \"he", now);
            Drop("whole", arrival: now.AddSeconds(1));
            using (new FileStream(held, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                loop.Look();
                Assert.True(File.Exists(held));
                Assert.Equal("whole", loop.Take().Job.RunId);
            }
        }

        [Fact]
        public void R448_no_job_runs_twice()
        {
            Drop("run1");
            var other = new RevitListenerQueue(setting);
            RevitListenerClaim first = queue.Next();
            Assert.Equal("run1", first.Stem);
            Assert.Null(other.Next());
            Assert.Null(queue.Next());

            loop.Look();
            Assert.Null(loop.Take());
        }

        [Fact]
        public void R448_a_second_job_of_one_run_id_never_overwrites_the_first_result()
        {
            foreach (string outcome in new[] { RevitListenerResult.Ok, RevitListenerResult.Failed })
            {
                Drop("again");
                loop.Look();
                loop.End(loop.Take(), outcome, null);
                loop.Look();
            }
            Assert.Equal("ok", (string)Result("again")["result"]);
            Assert.Equal(2, Directory.GetFiles(setting.OutFolder, "again*" + RevitListenerQueue.ResultTail).Length);
            Assert.Equal(2, Directory.GetFiles(setting.ClaimedFolder, "again*" + RevitListenerQueue.JobTail).Length);
        }

        [Fact]
        public void R448_the_job_keeps_the_marks_of_its_inputs_before_and_after_and_a_change_fails_it()
        {
            string kept = Path.Combine(root, "kept.txt");
            string changed = Path.Combine(root, "changed.txt");
            File.WriteAllText(kept, "one");
            File.WriteAllText(changed, "two");
            Drop("marks", JobText("marks", new[] { kept, changed }));
            loop.Look();
            RevitListenerRun run = loop.Take();
            File.WriteAllText(changed, "three, longer");
            loop.End(run, RevitListenerResult.Ok, null);
            loop.Look();

            JObject result = Result("marks");
            Assert.Equal("failed", (string)result["result"]);
            Assert.Contains("D19", (string)result["error"]);
            Assert.Equal(new[] { "changed.txt" }, result["inputsChanged"].Select(name => (string)name).ToArray());
            Assert.Equal(2, result["inputsBefore"].Count());
            Assert.Equal(2, result["inputsAfter"].Count());
            Assert.Equal(FileHash.Sha256(kept), (string)result["inputsBefore"][0]["sha256"], ignoreCase: true);
            Assert.Equal((string)result["inputsBefore"][0]["sha256"], (string)result["inputsAfter"][0]["sha256"]);
        }

        [Fact]
        public void R448_an_input_that_is_not_there_refuses_the_job_before_it_runs()
        {
            Drop("gone", JobText("gone", new[] { Path.Combine(root, "not-there.txt") }));
            loop.Look();
            Assert.Null(loop.Take());
            Assert.Equal("refused", (string)Result("gone")["result"]);
        }

        [Fact]
        public void R448_the_timing_file_of_the_run_folder_goes_beside_the_result_and_its_absence_is_noted()
        {
            string folder = Path.Combine(root, "run");
            Directory.CreateDirectory(folder);
            string model = Path.Combine(folder, "P1-TEST-NEW.rvt");
            Drop("none", JobText("none", model: model));
            loop.Look();
            loop.End(loop.Take(), RevitListenerResult.Ok, null);
            loop.Look();
            Assert.Null((string)Result("none")["timing"]);
            Assert.Contains("no run_timing", (string)Result("none")["timingNote"]);

            File.WriteAllText(Path.Combine(folder, "run_timing_with.json"), "{\"any\": \"shape\"}");
            Drop("with", JobText("with", model: model));
            loop.Look();
            loop.End(loop.Take(), RevitListenerResult.Ok, null);
            loop.Look();
            Assert.Equal("run_timing_with.json", (string)Result("with")["timing"]);
            Assert.True(File.Exists(Path.Combine(setting.OutFolder, "run_timing_with.json")));
        }

        [Fact]
        public void R448_a_heartbeat_is_written_with_no_job_and_with_one()
        {
            string beat = Path.Combine(setting.OutFolder, RevitListenerQueue.HeartbeatFile);
            loop.Step = () => "open";
            loop.Look();
            Assert.Equal("idle", (string)JObject.Parse(File.ReadAllText(beat))["state"]);

            Drop("run1");
            now = now.AddSeconds(RevitListenerLoop.BeatSeconds);
            loop.Look();
            loop.Take();
            now = now.AddSeconds(RevitListenerLoop.BeatSeconds);
            loop.Look();
            JObject running = JObject.Parse(File.ReadAllText(beat));
            Assert.Equal("run1", (string)running["runId"]);
            Assert.Equal("open", (string)running["step"]);
            Assert.Equal(Process.GetCurrentProcess().Id, (int)running["pid"]);
        }

        private JObject GoodJob(string model, string config)
        {
            string template = Path.Combine(root, "t.rte");
            string pane = Path.Combine(Path.GetDirectoryName(model), "pane_settings.json");
            File.WriteAllText(template, "");
            File.WriteAllText(pane, "{}");
            Directory.CreateDirectory(Path.GetDirectoryName(config));
            File.WriteAllText(config, "{}");
            return new JObject
            {
                ["kind"] = TestHostGuard.AddinRunKind,
                ["token"] = Token,
                ["code"] = "P1",
                ["runId"] = "run1",
                ["model"] = model,
                ["config"] = config,
                ["template"] = template,
                ["buttons"] = new JArray(1),
                ["nbh"] = "012",
                ["paneSettings"] = pane,
                ["createdUtc"] = DateTime.UtcNow.ToString("o"),
            };
        }

        private RevitListenerCheck Check(JObject job, string inputRoots = null)
        {
            var claim = new RevitListenerClaim { Stem = "run1", Text = job.ToString() };
            return RevitListenerCheck.Of(claim, Token, root, inputRoots == null ? new string[0] : inputRoots.Split('|'), DateTime.UtcNow, File.Exists, Directory.Exists);
        }

        private string RunFolder()
        {
            string folder = Path.Combine(TestHostGuard.SiteAdaptationFolder(root), "addin_run", "P1_stamp");
            Directory.CreateDirectory(folder);
            return folder;
        }

        [WindowsFact]
        public void R448_a_job_the_run_can_carry_out_passes_the_check_of_a_run_job_and_may_name_a_model_copy_that_is_there()
        {
            string model = Path.Combine(RunFolder(), "P1-TEST-NEW.rvt");
            string config = Path.Combine(Path.GetDirectoryName(model), "config", "project.json");
            RevitListenerCheck made = Check(GoodJob(model, config));
            Assert.True(made.Act, made.Reason);

            File.WriteAllText(model, "the key copy");
            RevitListenerCheck copy = Check(GoodJob(model, config));
            Assert.True(copy.Act, copy.Reason);
            Assert.Equal("run1", copy.Job.RunId);
        }

        [WindowsTheory]
        [InlineData("token", "not the token")]
        [InlineData("buttons", "buttons")]
        [InlineData("kind", "addinRun")]
        [InlineData("config", "config")]
        [InlineData("createdUtc", "younger")]
        [InlineData("inputs", "full path")]
        public void R448_a_job_the_run_check_refuses_never_runs(string field, string reason)
        {
            string model = Path.Combine(RunFolder(), "P1-TEST-NEW.rvt");
            JObject job = GoodJob(model, Path.Combine(Path.GetDirectoryName(model), "config", "project.json"));
            switch (field)
            {
                case "token":
                    job["token"] = "b1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
                    break;
                case "buttons":
                    job.Remove("buttons");
                    break;
                case "kind":
                    job["kind"] = TestHostGuard.ReadKind;
                    break;
                case "config":
                    job["config"] = Path.Combine(root, "project.json");
                    break;
                case "createdUtc":
                    job["createdUtc"] = DateTime.UtcNow.AddHours(-2).ToString("o");
                    break;
                default:
                    job["inputs"] = new JArray("relative.txt");
                    break;
            }
            RevitListenerCheck check = Check(job);
            Assert.False(check.Act);
            Assert.Contains(reason, check.Reason);
        }

        [WindowsFact]
        public void R448_a_model_or_a_config_inside_an_input_root_is_refused()
        {
            string model = Path.Combine(RunFolder(), "P1-TEST-NEW.rvt");
            JObject job = GoodJob(model, Path.Combine(Path.GetDirectoryName(model), "config", "project.json"));
            RevitListenerCheck check = Check(job, Path.GetDirectoryName(model));
            Assert.False(check.Act);
            Assert.Contains("input root", check.Reason);
        }

        [WindowsFact]
        public void R448_a_model_outside_the_work_root_is_refused()
        {
            string outside = Path.Combine(root, "elsewhere");
            Directory.CreateDirectory(outside);
            string model = Path.Combine(outside, "P1-TEST-NEW.rvt");
            RevitListenerCheck check = Check(GoodJob(model, Path.Combine(outside, "config", "project.json")));
            Assert.False(check.Act);
            Assert.Contains("work root", check.Reason);
        }

        [Fact]
        public void R448_the_user_install_never_names_the_listener()
        {
            string[] words = { "Listener", "SITEADAPT_LISTENER", "SiteAdaptation.TestHost", "Send-RevitJob", "SA-revit-jobs" };
            string install = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.AddIn", "Install-SiteAdaptation.ps1"));
            string manifest = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.AddIn", "SiteAdaptationAddIn_Revit2024.addin"));
            foreach (string word in words)
            {
                Assert.True(install.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0, "the install names " + word);
                Assert.True(manifest.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0, "the manifest names " + word);
            }
            foreach (string source in Directory.GetFiles(RepoFile("dotnet", "SiteAdaptation.AddIn"), "*.cs", SearchOption.AllDirectories))
            {
                if (source.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) < 0)
                {
                    Assert.DoesNotContain("RevitListener", File.ReadAllText(source));
                }
            }
            // The one place it lives is the test host, whose own install the user never runs
            Assert.Contains("RevitListener.Start", File.ReadAllText(HostFile("TestHostApp.cs")));
        }

        // QL20. Revit refuses a handler added inside its own event, and a listener that posts the exit is no listener
        [Fact]
        public void R448_every_handler_is_added_once_from_on_startup_and_revit_is_never_told_to_exit()
        {
            string listener = File.ReadAllText(HostFile("RevitListener.cs"));
            int subscribe = listener.IndexOf("private void Subscribe()", StringComparison.Ordinal);
            int looks = listener.IndexOf("private void Looks()", StringComparison.Ordinal);
            string body = listener.Substring(subscribe, looks - subscribe);
            Assert.Equal(5, body.Split(new[] { "+= On" }, StringSplitOptions.None).Length - 1);
            Assert.Equal(5, listener.Split(new[] { "+= On" }, StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("-=", listener);
            Assert.DoesNotContain("ExitRevit", listener);
            Assert.DoesNotContain("PostCommand", listener);
            Assert.DoesNotContain("Kill(", listener);
            Assert.Contains("new ButtonsRunHost(job, Runner)", listener);
        }

        [Fact]
        public void R448_the_start_script_takes_the_slot_of_the_lock_and_stays_as_long_as_its_revit()
        {
            string start = File.ReadAllText(HostFile("Start-RevitListener.ps1"));
            Assert.Contains("Get-ListenerSlot", start);
            Assert.Contains("SITEADAPT_LISTENER_ROOT", start);
            Assert.Contains("SITEADAPT_LISTENER_SLOT", start);
            Assert.Contains("SITEADAPT_LISTENER_INPUT_ROOTS", start);
            Assert.Contains("$info.Environment.Remove('SITEADAPT_JOB')", start);
            Assert.Contains("Start-WatcherFirst", start);
            Assert.Contains("$process.WaitForExit(", start);
            Assert.Contains("SITE_ADAPTATION_WINDOW_SLOT", File.ReadAllText(HostFile("ListenerPaths.ps1")));
            Assert.Contains("ListenerJobOut", File.ReadAllText(HostFile("Run-TestHost.ps1")));
            Assert.Contains("-ListenerJobOut", File.ReadAllText(HostFile("Send-RevitJob.ps1")));
        }

        [Fact]
        public void R448_the_scripts_refuse_a_run_with_no_slot_and_build_the_folders_from_the_setting()
        {
            string none = "$env:SITE_ADAPTATION_WINDOW_SLOT = $null\ntry { Get-ListenerSlot }\ncatch { 'refused: ' + $_.Exception.Message }";
            Assert.Contains("refused", Pwsh(none).Item2);
            Assert.Contains("2", Pwsh("$env:SITE_ADAPTATION_WINDOW_SLOT = '2'\nGet-ListenerSlot").Item2);
            Tuple<int, string, string> folders = Pwsh("(Get-ListenerFolders -Root " + Quoted(root) + " -Slot '2').out");
            Assert.Equal(0, folders.Item1);
            Assert.Equal(Path.Combine(root, "out-2"), folders.Item2.Trim());
            Assert.NotEqual(0, Pwsh("Get-ListenerFolders -Root " + Quoted(root) + " -Slot 'x'").Item1);
        }

        [Fact]
        public void R448_the_send_function_writes_a_whole_job_the_queue_takes_and_refuses_a_run_id_used_before()
        {
            string send = "$job = [ordered]@{ runId = 'run1'\nbuttons = @(1, 2)\nmodel = 'P1-TEST-NEW.rvt' }\n"
                + "Write-ListenerJob -Folders (Get-ListenerFolders -Root " + Quoted(root) + " -Slot '1') -Job $job";
            Tuple<int, string, string> first = Pwsh(send);
            Assert.True(first.Item1 == 0, first.Item3);
            Assert.Empty(Directory.GetFiles(setting.InFolder, "*" + RevitListenerFiles.WritingTail));

            loop.Look();
            RevitListenerRun run = loop.Take();
            Assert.Equal("run1", run.Job.RunId);
            Assert.Equal(new[] { 1, 2 }, run.Job.Buttons);

            Tuple<int, string, string> second = Pwsh(send);
            Assert.NotEqual(0, second.Item1);
            Assert.Contains("run1", second.Item3);

            // The result the loop writes, read back the way the send script reads it
            loop.End(run, RevitListenerResult.Ok, null);
            loop.Look();
            Tuple<int, string, string> seen = Pwsh("$seen = Wait-ListenerResult -Folders (Get-ListenerFolders -Root " + Quoted(root) + " -Slot '1') -RunId 'run1' -ProcessId $PID -TimeoutSeconds 2 -Start (Get-Date)\n"
                + "$seen.state + ' ' + $seen.result.result + ' ' + $seen.result.runId");
            Assert.Equal("result ok run1", seen.Item2.Trim());
        }

        [Fact]
        public void R448_the_send_wait_gives_the_result_a_timeout_or_a_revit_that_is_gone()
        {
            string folders = "$folders = Get-ListenerFolders -Root " + Quoted(root) + " -Slot '1'\n";
            string wait = "Wait-ListenerResult -Folders $folders -RunId 'run1' -ProcessId {0} -TimeoutSeconds 2 -Start (Get-Date)";

            Tuple<int, string, string> timedOut = Pwsh(folders + "(" + string.Format(wait, "$PID") + ").state");
            Assert.Equal("timeout", timedOut.Item2.Trim());

            Tuple<int, string, string> gone = Pwsh(folders + "(" + string.Format(wait, "999999") + ").state");
            Assert.Equal("gone", gone.Item2.Trim());

            Tuple<int, string, string> failure = Pwsh(folders + "Write-ListenerFailure -Folders $folders -RunId 'run1' -Reason 'freeze guard: it hung'\n"
                + "$seen = " + string.Format(wait, "$PID") + "\n$seen.state\n$seen.result.error");
            Assert.True(failure.Item1 == 0, failure.Item3);
            Assert.Contains("result", failure.Item2);
            Assert.Contains("freeze guard: it hung", failure.Item2);
        }
    }
}
