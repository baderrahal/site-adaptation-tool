using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Round ADDIN PR 0b on fakes: the image step limit of the test host comes from config/project.json through the job (R302)
    public class RoundAddinTests
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private const string KeyCopy = @"C:\Docs\round\work\site-000001_TEST-IMK.rvt";
        private static readonly DateTime Now = new DateTime(2026, 10, 25, 6, 0, 0, DateTimeKind.Utc);

        private static string Image(Action<JObject> change = null)
        {
            var job = new JObject
            {
                ["kind"] = "image",
                ["token"] = Token,
                ["code"] = "IMK",
                ["model"] = KeyCopy,
                ["imageFolder"] = @"C:\Docs\round\images",
                ["imageLevel"] = "Level 1",
                ["area"] = new JArray(1000.0, 2000.0, 1300.0, 2250.0),
                ["imageMinutes"] = 30,
                ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            };
            change?.Invoke(job);
            return job.ToString();
        }

        private static GuardResult Check(string text)
        {
            return TestHostGuard.Check(text, Token, Now, path => true, path => true);
        }

        private static string RepoText(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray()));
        }

        [Fact]
        public void R302_the_image_step_takes_the_limit_the_job_names()
        {
            GuardResult result = Check(Image());

            Assert.True(result.Act, result.Reason);
            Assert.Equal(30, TestHostGuard.ImageStepMinutes(result.Job));
            Assert.Equal(45, TestHostGuard.ImageStepMinutes(Check(Image(job => job["imageMinutes"] = 45)).Job));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(TestHostGuard.MaxImageMinutes + 1)]
        public void R302_an_image_job_with_no_image_limit_or_one_out_of_range_is_refused(double minutes)
        {
            Assert.Contains("imageMinutes", Check(Image(job => job.Remove("imageMinutes"))).Reason);
            Assert.False(Check(Image(job => job["imageMinutes"] = minutes)).Act);
        }

        [Fact]
        public void R302_image_minutes_go_with_an_image_job_only()
        {
            string read = new JObject
            {
                ["kind"] = "read", ["token"] = Token, ["code"] = "IMK", ["model"] = KeyCopy, ["imageMinutes"] = 30, ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            }.ToString();

            Assert.Contains("image job only", Check(read).Reason);
            Assert.Throws<InvalidOperationException>(() => TestHostGuard.ImageStepMinutes(new TestHostJob { Kind = "read", ImageMinutes = 30 }));
            Assert.Throws<InvalidOperationException>(() => TestHostGuard.ImageStepMinutes(new TestHostJob { Kind = "image" }));
        }

        [Fact]
        public void R302_the_total_of_an_image_job_may_run_to_the_usual_cap_plus_its_image_minutes_and_no_further()
        {
            Assert.True(Check(Image(job => job["totalMinutes"] = TestHostGuard.MaxTotalMinutes + 30)).Act);
            Assert.False(Check(Image(job => job["totalMinutes"] = TestHostGuard.MaxTotalMinutes + 30.5)).Act);
            string read = new JObject
            {
                ["kind"] = "read", ["token"] = Token, ["code"] = "IMK", ["model"] = KeyCopy, ["totalMinutes"] = TestHostGuard.MaxTotalMinutes + 1, ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            }.ToString();
            Assert.False(Check(read).Act);
        }

        [Fact]
        public void R302_a_job_with_no_total_runs_its_image_step_plus_the_margin_and_never_less_than_the_image_step()
        {
            Assert.Equal(40, TestHostGuard.TotalMinutesOf(Check(Image(job => job["imageMarginMinutes"] = 10)).Job));
            Assert.Equal(30 + TestHostGuard.DefaultTotalMinutes, TestHostGuard.TotalMinutesOf(Check(Image()).Job));
            Assert.Equal(35, TestHostGuard.TotalMinutesOf(Check(Image(job => { job["imageMarginMinutes"] = 10; job["totalMinutes"] = 35; })).Job));
            Assert.Equal(TestHostGuard.DefaultTotalMinutes, TestHostGuard.TotalMinutesOf(new TestHostJob { Kind = "read" }));
            Assert.True(TestHostGuard.TotalMinutesOf(Check(Image(job => job["imageMarginMinutes"] = 10)).Job) > TestHostGuard.ImageStepMinutes(Check(Image()).Job));
        }

        [Fact]
        public void R302_an_image_margin_goes_with_an_image_job_only_and_must_be_above_0()
        {
            Assert.False(Check(Image(job => job["imageMarginMinutes"] = 0)).Act);
            Assert.False(Check(Image(job => job["imageMarginMinutes"] = TestHostGuard.MaxTotalMinutes + 1)).Act);
            string read = new JObject
            {
                ["kind"] = "read", ["token"] = Token, ["code"] = "IMK", ["model"] = KeyCopy, ["imageMarginMinutes"] = 10, ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            }.ToString();
            Assert.Contains("image job only", Check(read).Reason);
        }

        [Fact]
        public void R302_the_session_deadline_and_a_plain_image_run_take_their_limits_from_the_image_step_and_margin()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string script = RepoText("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");

            Assert.Contains("deadlineUtc = log.StartedUtc.AddMinutes(TestHostGuard.TotalMinutesOf(job))", session);
            Assert.DoesNotContain("job.TotalMinutes ?? TestHostGuard.DefaultTotalMinutes", session);
            Assert.Contains("$imageMargin = [double]$config.testHost.imageMarginMinutes", script);
            Assert.Contains("if (-not $PSBoundParameters.ContainsKey('TimeoutMinutes'))", script);
            Assert.Contains("$roundJob.imageMarginMinutes = $imageMargin", script);
            Assert.DoesNotContain("$imageMinutes + 30", script);
        }

        [Fact]
        public void R303_the_watcher_goes_on_after_the_first_step_until_revit_exits_or_its_time_ends()
        {
            DateTime end = Now.AddMinutes(43);

            Assert.Null(SecurityDialogs.StopReason(true, true, false, Now, end, 43));
            Assert.Null(SecurityDialogs.StopReason(true, false, true, Now, end, 43));
            Assert.Equal("the test host wrote its first log step", SecurityDialogs.StopReason(true, true, true, Now, end, 43));
            Assert.Equal("Revit exited", SecurityDialogs.StopReason(false, true, false, Now, end, 43));
            Assert.Equal("watched 43 minutes", SecurityDialogs.StopReason(true, true, false, end.AddSeconds(1), end, 43));
        }

        [Fact]
        public void R303_after_the_first_step_the_watcher_clicks_nothing_and_stops_revit_on_nothing()
        {
            foreach (WatchAction action in Enum.GetValues(typeof(WatchAction)))
            {
                Assert.Equal(SecurityDialogs.ButtonFor(action) == null, SecurityDialogs.StopsRevit(action, false));
                Assert.Equal(SecurityDialogs.ButtonFor(action), SecurityDialogs.ClickFor(action, false));
                Assert.False(SecurityDialogs.StopsRevit(action, true));
                Assert.Null(SecurityDialogs.ClickFor(action, true));
            }
        }

        [Fact]
        public void R303_a_job_window_is_logged_again_only_when_its_class_title_or_visible_state_changes()
        {
            string key = WatcherRules.JobWindowKey(42, "HwndWrapper", "Autodesk Revit 2024.3 - [Home]", true);

            Assert.Equal(key, WatcherRules.JobWindowKey(42, "HwndWrapper", "Autodesk Revit 2024.3 - [Home]", true));
            Assert.NotEqual(key, WatcherRules.JobWindowKey(42, "HwndWrapper", "Autodesk Revit 2024.3 - [Home]", false));
            Assert.NotEqual(key, WatcherRules.JobWindowKey(42, "HwndWrapper", "Autodesk Revit 2024.3 - [Level 1]", true));
            Assert.NotEqual(key, WatcherRules.JobWindowKey(42, "#32770", "Autodesk Revit 2024.3 - [Home]", true));
            Assert.NotEqual(key, WatcherRules.JobWindowKey(43, "HwndWrapper", "Autodesk Revit 2024.3 - [Home]", true));
        }

        [Fact]
        public void R303_the_start_phase_worker_ends_first_and_its_stop_wins_over_revit_exited()
        {
            var results = new System.Collections.Generic.List<string>();
            var notes = new System.Collections.Generic.List<string>();
            var stopping = System.Threading.Tasks.Task.Run(() =>
            {
                System.Threading.Thread.Sleep(300);
                return WatcherRules.StoppedRevit;
            });

            Assert.Equal(WatcherRules.StoppedRevit, WatcherRules.End(stopping, TimeSpan.FromSeconds(10), "Revit exited", notes.Add, results.Add));
            Assert.Empty(results);
            Assert.Empty(notes);
            var quiet = System.Threading.Tasks.Task.FromResult(WatcherRules.KeepWatching);
            Assert.Equal(WatcherRules.Answered, WatcherRules.End(quiet, TimeSpan.FromSeconds(10), "Revit exited", notes.Add, results.Add));
            Assert.Equal(WatcherRules.Answered, WatcherRules.End(null, TimeSpan.FromSeconds(10), "Revit exited", notes.Add, results.Add));
            Assert.Equal(new[] { "Revit exited", "Revit exited" }, results);
        }

        [Fact]
        public void R303_a_faulted_or_held_start_phase_worker_is_logged_with_its_exception_and_gives_a_non_zero_exit()
        {
            var notes = new System.Collections.Generic.List<string>();
            var results = new System.Collections.Generic.List<string>();
            System.Threading.Tasks.Task<int> faulted = System.Threading.Tasks.Task.Run(new Func<int>(() => throw new InvalidOperationException("window gone mid read")));

            Assert.Equal(WatcherRules.WorkerFailed, WatcherRules.AfterWorker(faulted, TimeSpan.FromSeconds(10), notes.Add));
            Assert.Contains("window gone mid read", notes.Single());
            Assert.Equal(WatcherRules.WorkerFailed, WatcherRules.End(faulted, TimeSpan.FromSeconds(10), "Revit exited", notes.Add, results.Add));
            Assert.Empty(results);
            Assert.NotEqual(0, WatcherRules.WorkerFailed);
            Assert.NotEqual(WatcherRules.StoppedRevit, WatcherRules.WorkerFailed);
            using (var release = new System.Threading.ManualResetEventSlim(false))
            {
                var held = System.Threading.Tasks.Task.Run(() =>
                {
                    release.Wait();
                    return WatcherRules.KeepWatching;
                });
                Assert.Equal(WatcherRules.WorkerFailed, WatcherRules.AfterWorker(held, TimeSpan.FromMilliseconds(100), notes.Add));
                Assert.Contains("did not end", notes.Last());
                release.Set();
                Assert.Equal(WatcherRules.KeepWatching, WatcherRules.AfterWorker(held, TimeSpan.FromSeconds(10), notes.Add));
            }
            Assert.Equal(WatcherRules.KeepWatching, WatcherRules.AfterWorker(null, TimeSpan.Zero, notes.Add));
        }

        [Fact]
        public void R303_a_report_write_is_retried_once_and_a_write_that_fails_twice_goes_to_stderr_and_never_throws()
        {
            int calls = 0;
            var errors = new System.Collections.Generic.List<string>();

            Assert.True(WatcherRules.WriteWithRetry(() => { if (++calls == 1) throw new IOException("in use"); }, TimeSpan.FromMilliseconds(10), errors.Add));
            Assert.Equal(2, calls);
            Assert.Empty(errors);
            calls = 0;
            Assert.False(WatcherRules.WriteWithRetry(() => { calls++; throw new IOException("locked"); }, TimeSpan.FromMilliseconds(10), errors.Add));
            Assert.Equal(2, calls);
            Assert.Contains("locked", errors.Single());
            calls = 0;
            Assert.True(WatcherRules.WriteWithRetry(() => calls++, TimeSpan.FromMilliseconds(10), errors.Add));
            Assert.Equal(1, calls);
        }

        // The text of one method of a source file, from its signature to the next private static member
        private static string MethodText(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, signature);
            int end = source.IndexOf("        private static ", start + signature.Length, StringComparison.Ordinal);
            return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
        }

        [Fact]
        public void R303_the_job_phase_lists_windows_by_win32_only_with_no_ui_automation_call_no_button_read_and_no_click()
        {
            string watcher = RepoText("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs");
            string job = MethodText(watcher, "private static void ListJobWindows(");
            string tops = MethodText(watcher, "private static List<TopWindow> TopWindows(");

            Assert.Contains("TopWindows(options.Pid)", job);
            Assert.Contains("value.JobWindows.Add(", job);
            foreach (string banned in new[] { "Automation", "Judge(", "Click(", "ReadParts(", "Dialogs.Add(", "Buttons" })
            {
                Assert.DoesNotContain(banned, job);
                Assert.DoesNotContain(banned, tops);
            }
            Assert.Contains("EnumWindows(", tops);
            Assert.Contains("GetWindowThreadProcessId(", tops);
            Assert.Contains("IsWindowVisible(", tops);
            Assert.DoesNotContain("EnumChildWindows", watcher);
            Assert.DoesNotContain("AutomationElement.FromHandle", watcher);
            Assert.DoesNotContain("ReadByAutomation", watcher);
            // The worker started before the first step looks at no further window once the job phase is on
            Assert.Contains("if (phase.On)", MethodText(watcher, "private static int StartPhase("));
        }

        [Fact]
        public void R303_the_main_loop_ends_through_the_worker_and_every_report_write_is_retried()
        {
            string watcher = RepoText("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs");
            string loop = MethodText(watcher, "private static int Watch(");

            Assert.Contains("return WatcherRules.End(uia, WorkerWait, why,", loop);
            Assert.Contains("WatcherRules.AfterWorker(uia, TimeSpan.Zero,", loop);
            Assert.DoesNotContain("value.Result = why", loop);
            Assert.DoesNotContain("uia.IsFaulted ? KeepWatching", loop);
            Assert.Contains("uia = System.Threading.Tasks.Task.Run(() => StartPhase(options, report, started, startSeen, phase))", loop);
            Assert.Contains("WatcherRules.WriteWithRetry(() => JsonOut.Write(path, report)", MethodText(watcher, "private static void Record("));
            Assert.Single(watcher.Split(new[] { "JsonOut.Write(" }, StringSplitOptions.None).Skip(1));
            Assert.Contains("value.StoppedOn = entry", MethodText(watcher, "private static int Judge("));
        }

        [Fact]
        public void R303_the_run_script_counts_security_dialogs_from_the_start_phase_only_and_names_a_stop_only_when_the_watcher_stopped_revit()
        {
            string script = RepoText("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");

            Assert.Contains("'--minutes', [string]$TimeoutMinutes, '--watch-job', 'true'", script);
            Assert.DoesNotContain("'--minutes', '10'", script);
            Assert.Contains("$null = $watcher.Handle", script);
            Assert.Contains("-NotePropertyName securityDialogs -NotePropertyValue @($watch.dialogs)", script);
            Assert.Contains("jobWindows = @($watch.jobWindows)", script);
            Assert.Contains("\"Security dialogs: $(@($watch.dialogs).Count), job windows: $(@($watch.jobWindows).Count)", script);
            Assert.Contains("if ($watcherCode -eq 3 -and $watch.stoppedOn)", script);
            Assert.DoesNotContain("$_.action -ne 'DoNotLoad' -and $_.action -ne 'LoadOnceOurs'", script);
            Assert.Contains("watcherExitCode = $watcherCode", script);
        }

        [Fact]
        public void R302_the_image_step_limit_is_no_constant_and_the_run_script_reads_it_from_the_config()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string script = RepoText("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            string config = RepoText("config", "project.json");

            Assert.Contains("Name = \"image\", LimitMinutes = TestHostGuard.ImageStepMinutes(job)", session);
            Assert.DoesNotContain("Name = \"image\", LimitMinutes = 10", session);
            Assert.Contains("$roundJob.imageMinutes = $imageMinutes", script);
            Assert.Contains("[double]$config.testHost.imageStepMinutes", script);
            Assert.Equal(30, (double)JObject.Parse(config)["testHost"]["imageStepMinutes"]);
        }
    }
}
