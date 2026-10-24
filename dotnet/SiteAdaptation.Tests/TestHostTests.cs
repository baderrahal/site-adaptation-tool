using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The placer filter, the guard of the test host and its step runner, on fakes. Nothing here runs Revit
    public class TestHostTests
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private const string Model = @"C:\Models\site_TEST-C10.rvt";
        private const string PlanPath = @"C:\Docs\plan.json";
        private const string Prototype = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt";
        private static readonly DateTime Now = new DateTime(2026, 10, 24, 6, 0, 0, DateTimeKind.Utc);

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

        // R100 placer filter

        private static PlanFile MixedPlan()
        {
            return PlacerTests.Plan(
                PlacerTests.Plot("1", 1010, 2010, 7, file: "VD2.rvt"),
                PlacerTests.Plot("2", 1020, 2020, 7, file: "DP4.rvt"),
                PlacerTests.Plot("3", 1030, 2030, 7, file: "VD2.rvt"));
        }

        [Fact]
        public void R100_a_filter_places_only_the_plots_of_its_file_without_case_and_counts_the_rest()
        {
            var target = new PlacerTests.FakeTarget();

            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", target, true, "dp4.RVT");

            Assert.False(report.Stopped);
            Assert.False(report.RolledBack);
            Assert.Equal(new[] { "2" }, report.Placed.Select(plot => plot.Plot));
            Assert.Empty(report.Skipped);
            Assert.Equal(2, report.Counts.FilteredOut);
            Assert.Equal(3, report.Counts.PlotsInPlan);
            Assert.Single(target.Loads);
            Assert.EndsWith("DP4.rvt", target.Loads[0]);
            Assert.Equal("dp4.RVT", report.PrototypeFilter);
        }

        [Fact]
        public void R100_the_dry_run_filters_the_same_way()
        {
            var target = new PlacerTests.FakeTarget();

            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", target, false, "VD2.rvt");

            Assert.Equal(new[] { "1", "3" }, report.Placed.Select(plot => plot.Plot));
            Assert.Equal(1, report.Counts.FilteredOut);
            Assert.Equal(0, target.Begins);
            Assert.Empty(target.Loads);
        }

        [Theory]
        [InlineData("XX.rvt")]
        [InlineData(@"C:\Prototypes\DP4.rvt")]
        [InlineData("")]
        [InlineData("  ")]
        public void R100_a_filter_no_plot_uses_or_that_is_not_a_file_name_stops_and_places_nothing(string filter)
        {
            var target = new PlacerTests.FakeTarget();

            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", target, true, filter);

            Assert.True(report.Stopped);
            Assert.Contains("nothing was placed", report.Failure);
            Assert.Empty(report.Placed);
            Assert.Equal(0, target.Begins);
        }

        [Fact]
        public void R100_no_filter_tries_every_plot_as_before()
        {
            PlacerReport all = Placer.Run(MixedPlan(), "plan.json", new PlacerTests.FakeTarget(), true, null);
            PlacerReport before = Placer.Run(MixedPlan(), "plan.json", new PlacerTests.FakeTarget(), true);

            Assert.Equal(3, all.Counts.Placed);
            Assert.Equal(0, all.Counts.FilteredOut);
            Assert.Null(all.PrototypeFilter);
            Assert.Equal(before.Placed.Select(plot => plot.Plot), all.Placed.Select(plot => plot.Plot));
        }

        [Fact]
        public void R100_the_first_entry_of_a_plot_number_is_found_over_the_whole_plan()
        {
            PlanFile plan = PlacerTests.Plan(
                PlacerTests.Plot("7", 1010, 2010, 7, file: "VD2.rvt"),
                PlacerTests.Plot("7", 1020, 2020, 7, file: "DP4.rvt"));

            PlacerReport report = Placer.Run(plan, "plan.json", new PlacerTests.FakeTarget(), true, "DP4.rvt");

            Assert.Empty(report.Placed);
            SkippedPlot skipped = Assert.Single(report.Skipped);
            Assert.Contains("more than once", skipped.Reason);
        }

        [Fact]
        public void R100_plots_of_other_files_already_in_the_document_are_left_out_and_not_skipped()
        {
            var target = new PlacerTests.FakeTarget();
            target.PlotNumbers.AddRange(new[] { "1", "3" });

            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", target, true, "DP4.rvt");

            Assert.Empty(report.Skipped);
            Assert.Single(report.Placed);
        }

        [Fact]
        public void R100_the_report_file_holds_the_filter_and_the_count_left_out()
        {
            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", new PlacerTests.FakeTarget(), false, "DP4.rvt");

            JObject json = JObject.Parse(JsonOut.ToJson(report));

            Assert.Equal("DP4.rvt", (string)json["prototypeFilter"]);
            Assert.Equal(2, (int)json["counts"]["filteredOut"]);
        }

        [Fact]
        public void R100_the_button_offers_the_filter_and_calls_the_shared_entry()
        {
            string command = RepoText("dotnet", "SiteAdaptation.Placer", "PlaceFromPlanCommand.cs");

            Assert.Contains("PlaceFromPlan.Run(active.Document, plan, planPath, real, prototypeFile)", command);
            Assert.Contains("The plots of one prototype file", command);
            Assert.DoesNotContain("Common.Placer.Run(", command);
        }

        // R101 test host guard

        private static string Job(Action<JObject> change = null)
        {
            var job = new JObject
            {
                ["token"] = Token,
                ["code"] = "C10",
                ["model"] = Model,
                ["prototypeFile"] = Prototype,
                ["plan"] = PlanPath,
                ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            };
            change?.Invoke(job);
            return job.ToString();
        }

        private static GuardResult Check(string text, string token = Token, Func<string, bool> exists = null)
        {
            return TestHostGuard.Check(text, token, Now, exists ?? (path => true));
        }

        [Fact]
        public void R101_without_a_token_or_a_job_file_the_test_host_never_wakes()
        {
            Assert.False(TestHostGuard.ShouldWake(null, true));
            Assert.False(TestHostGuard.ShouldWake("", true));
            Assert.False(TestHostGuard.ShouldWake(Token, false));
            Assert.True(TestHostGuard.ShouldWake(Token, true));
        }

        [Fact]
        public void R101_a_good_job_is_accepted_with_its_values()
        {
            GuardResult result = Check(Job());

            Assert.True(result.Act, result.Reason);
            Assert.Equal("C10", result.Job.Code);
            Assert.Equal(Model, result.Job.Model);
            Assert.Equal(Prototype, result.Job.PrototypeFile);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("short")]
        [InlineData("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d7")]
        [InlineData("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6x")]
        [InlineData("A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6")]
        [InlineData("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d-")]
        public void R101_a_session_token_that_is_missing_short_or_not_the_job_token_is_refused(string token)
        {
            Assert.False(Check(Job(), token).Act);
        }

        [Fact]
        public void R101_a_job_without_a_token_or_with_another_one_is_refused()
        {
            Assert.False(Check(Job(job => job.Remove("token"))).Act);
            Assert.False(Check(Job(job => job["token"] = "")).Act);
            Assert.False(Check(Job(job => job["token"] = Token.ToUpperInvariant())).Act);
        }

        [Theory]
        [InlineData(61)]
        [InlineData(600)]
        [InlineData(-6)]
        public void R101_a_job_older_than_an_hour_or_from_the_future_is_refused(int minutesAgo)
        {
            Assert.False(Check(Job(job => job["createdUtc"] = Now.AddMinutes(-minutesAgo).ToString("o"))).Act);
        }

        [Fact]
        public void R101_a_job_with_no_date_is_refused()
        {
            Assert.False(Check(Job(job => job.Remove("createdUtc"))).Act);
        }

        [Theory]
        [InlineData(@"C:\Models\site.rvt")]
        [InlineData(@"C:\Models\site_TEST-TH1.rvt")]
        [InlineData(@"C:\Models\_TEST-C10.rvt")]
        [InlineData(@"C:\Models\site_TEST-C10.rvt.bak")]
        [InlineData(@"C:\Models\site_TEST-C10_TEST-C10.rvt")]
        [InlineData(@"Models\site_TEST-C10.rvt")]
        [InlineData(@"C:Models\site_TEST-C10.rvt")]
        [InlineData(@"C:\Models\..\Other\site_TEST-C10.rvt")]
        [InlineData("")]
        public void R101_only_a_copy_named_for_its_code_is_ever_opened(string model)
        {
            Assert.False(Check(Job(job => job["model"] = model)).Act);
        }

        [Fact]
        public void R101_a_missing_model_key_is_refused()
        {
            Assert.False(Check(Job(job => job.Remove("model"))).Act);
        }

        [Theory]
        [InlineData("RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt")]
        [InlineData(@"C:\P\RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt")]
        [InlineData("RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rfa")]
        [InlineData("")]
        public void R101_the_prototype_file_must_be_the_keo_file_of_the_code(string file)
        {
            Assert.False(Check(Job(job => job["prototypeFile"] = file)).Act);
        }

        [Theory]
        [InlineData("c10")]
        [InlineData("C10X1")]
        [InlineData("C1/")]
        [InlineData("")]
        public void R101_a_code_that_is_not_a_prototype_code_is_refused(string code)
        {
            Assert.False(Check(Job(job => job["code"] = code)).Act);
        }

        [Fact]
        public void R101_a_model_or_plan_that_does_not_exist_or_a_plan_that_is_not_full_json_is_refused()
        {
            Assert.False(Check(Job(), exists: path => path != Model).Act);
            Assert.False(Check(Job(), exists: path => path != PlanPath).Act);
            Assert.False(Check(Job(job => job["plan"] = "plan.json")).Act);
            Assert.False(Check(Job(job => job["plan"] = @"C:\Docs\plan.txt")).Act);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{")]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("\"text\"")]
        public void R101_a_job_file_that_does_not_read_is_refused(string text)
        {
            Assert.False(Check(text).Act);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(30)]
        public void R101_a_total_limit_outside_0_to_29_minutes_is_refused(double minutes)
        {
            Assert.False(Check(Job(job => job["totalMinutes"] = minutes)).Act);
        }

        [Fact]
        public void R101_the_original_of_a_copy_is_its_name_without_the_test_tag()
        {
            Assert.Equal(@"C:\Models\site.rvt", TestHostGuard.OriginalOf(Model));
            Assert.Null(TestHostGuard.OriginalOf(@"C:\Models\site.rvt"));
        }

        [Fact]
        public void R101_the_app_reads_the_token_then_the_job_file_and_loads_nothing_before()
        {
            string app = RepoText("dotnet", "SiteAdaptation.TestHost", "TestHostApp.cs");
            int token = app.IndexOf("GetEnvironmentVariable(TokenVariable)", StringComparison.Ordinal);
            int file = app.IndexOf("File.Exists(jobPath)", StringComparison.Ordinal);
            int resolve = app.IndexOf("AssemblyResolve += Resolve", StringComparison.Ordinal);
            int wake = app.IndexOf("Wake(application, token, jobPath);", StringComparison.Ordinal);

            Assert.True(token > 0 && token < file && file < resolve && resolve < wake);
            Assert.Equal(TestHostGuard.TokenVariable, "SITEADAPTATION_TESTHOST_TOKEN");
            Assert.Contains("\"" + TestHostGuard.TokenVariable + "\"", app);
            Assert.Contains("\"" + TestHostGuard.JobFileName + "\"", app);
        }

        [Fact]
        public void R101_the_job_is_checked_then_claimed_before_any_event_is_subscribed()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            int check = session.IndexOf("TestHostGuard.Check(", StringComparison.Ordinal);
            int refuse = session.IndexOf("if (!guard.Act)", StringComparison.Ordinal);
            int claim = session.IndexOf("File.Delete(jobPath);", StringComparison.Ordinal);
            int subscribe = session.IndexOf("session.Subscribe();", StringComparison.Ordinal);

            Assert.True(check > 0 && check < refuse && refuse < claim && claim < subscribe);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(session, @"\+= OnFirstIdling").Cast<object>());
        }

        [Fact]
        public void R101_the_test_host_runs_the_button_code_of_the_placer_and_the_reader_and_no_copy()
        {
            string steps = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSteps.cs");
            string project = RepoText("dotnet", "SiteAdaptation.TestHost", "SiteAdaptation.TestHost.csproj");
            string reader = RepoText("dotnet", "RevitPrototypeReader", "ExportPrototypesCommand.cs");

            Assert.Contains("PlaceFromPlan.Run(document, plan, job.Plan, real, job.PrototypeFile)", steps);
            Assert.Contains("PrototypeReading.ReadOpenDocument(app, document)", steps);
            Assert.Contains("PrototypeReading.ReadDocument(active, keyNames, options)", reader);
            Assert.DoesNotContain("RevitExtractor", steps);
            Assert.DoesNotContain("RevitPlacerTarget", steps);
            Assert.DoesNotContain("Placer.Run(", steps.Replace("PlaceFromPlan.Run(", ""));
            Assert.Contains("SiteAdaptation.Placer.csproj\" Private=\"false\"", project);
            Assert.Contains("RevitPrototypeReader.csproj\" Private=\"false\"", project);
            Assert.Contains("SiteAdaptation.Common.csproj\" Private=\"false\"", project);
        }

        // R102 test host steps and log

        private sealed class Clock
        {
            public DateTime Now = TestHostTests.Now;
        }

        private static TestHostRunner Runner(TestHostLog log, Clock clock, List<string> saves)
        {
            return new TestHostRunner(log, value => saves.Add(JsonOut.ToJson(value)), () => clock.Now);
        }

        [Fact]
        public void R102_steps_run_in_order_and_each_is_logged_when_it_starts_and_ends()
        {
            var log = new TestHostLog();
            var clock = new Clock();
            var saves = new List<string>();
            var order = new List<string>();
            TestHostRunner runner = Runner(log, clock, saves);

            bool ok = runner.Run(new[]
            {
                new JobStep { Name = "a", LimitMinutes = 1, Action = () => { order.Add("a"); clock.Now = clock.Now.AddSeconds(30); return "one"; } },
                new JobStep { Name = "b", LimitMinutes = 1, Action = () => { order.Add("b"); return "two"; } },
            });

            Assert.True(ok);
            Assert.Equal(new[] { "a", "b" }, order);
            Assert.Equal(new[] { "ok", "ok" }, log.Steps.Select(step => step.Result));
            Assert.Equal(30.0, log.Steps[0].Seconds);
            Assert.Equal("one", log.Steps[0].Detail);
            Assert.Equal("ok", log.Result);
            Assert.True(saves.Count >= 5);
            Assert.Contains("\"name\": \"a\"", saves[0]);
        }

        [Fact]
        public void R102_a_failed_step_keeps_the_full_exception_and_skips_the_rest_but_the_always_steps()
        {
            var log = new TestHostLog();
            var ran = new List<string>();
            TestHostRunner runner = Runner(log, new Clock(), new List<string>());

            bool ok = runner.Run(new[]
            {
                new JobStep { Name = "open", Action = () => { throw new InvalidOperationException("no file"); } },
                new JobStep { Name = "place", Action = () => { ran.Add("place"); return null; } },
                new JobStep { Name = "close", Always = true, Action = () => { ran.Add("close"); return "closed"; } },
            });

            Assert.False(ok);
            Assert.Equal(new[] { "close" }, ran);
            Assert.Equal(new[] { "failed", "skipped", "ok" }, log.Steps.Select(step => step.Result));
            Assert.Contains("InvalidOperationException: no file", log.Steps[0].Exception);
            Assert.Contains(" at ", log.Steps[0].Exception);
            Assert.Equal("failed", log.Result);
        }

        [Fact]
        public void R102_the_watchdog_writes_the_timeout_and_then_kills_once_when_a_step_runs_over()
        {
            var log = new TestHostLog();
            var clock = new Clock();
            var saves = new List<string>();
            TestHostRunner runner = Runner(log, clock, saves);
            var kills = new List<string>();
            var watchdog = new TestHostWatchdog(runner, clock.Now.AddMinutes(27), () => clock.Now, kills.Add, TimeSpan.FromHours(1));

            runner.Run(new[]
            {
                new JobStep
                {
                    Name = "open", LimitMinutes = 2, Action = () =>
                    {
                        clock.Now = clock.Now.AddMinutes(1);
                        watchdog.Look();
                        clock.Now = clock.Now.AddMinutes(2);
                        watchdog.Look();
                        watchdog.Look();
                        return null;
                    },
                },
            });
            watchdog.Dispose();

            Assert.Single(kills);
            Assert.Contains("step open ran over its limit of 2 minutes", kills[0]);
            Assert.Contains("\"result\": \"timeout\"", saves.First(text => text.Contains("ran over")));
        }

        [Fact]
        public void R102_the_whole_job_has_a_limit_and_a_late_exit_after_the_job_keeps_its_result()
        {
            var log = new TestHostLog();
            var clock = new Clock();
            TestHostRunner runner = Runner(log, clock, new List<string>());
            var kills = new List<string>();
            var watchdog = new TestHostWatchdog(runner, clock.Now.AddMinutes(10), () => clock.Now, kills.Add, TimeSpan.FromHours(1));

            runner.Run(new[] { new JobStep { Name = "read", LimitMinutes = 20, Action = () => { clock.Now = clock.Now.AddMinutes(5); return null; } } });
            watchdog.MoveDeadline(clock.Now.AddMinutes(5));
            clock.Now = clock.Now.AddMinutes(4);
            watchdog.Look();
            Assert.Empty(kills);
            clock.Now = clock.Now.AddMinutes(2);
            watchdog.Look();
            watchdog.Dispose();

            Assert.Equal(new[] { "Revit did not exit in time" }, kills);
            Assert.Equal("ok", log.Result);

            var second = new TestHostLog();
            var late = new Clock();
            TestHostRunner slow = Runner(second, late, new List<string>());
            var stops = new List<string>();
            var guard = new TestHostWatchdog(slow, late.Now.AddMinutes(10), () => late.Now, stops.Add, TimeSpan.FromHours(1));
            slow.Run(new[] { new JobStep { Name = "open", LimitMinutes = 20, Action = () => { late.Now = late.Now.AddMinutes(11); guard.Look(); return null; } } });
            guard.Dispose();

            Assert.Single(stops);
            Assert.Contains("total limit, in step open", stops[0]);
        }

        [Fact]
        public void R102_a_dialog_gets_the_first_answer_revit_takes_and_a_missing_link_is_ignored()
        {
            var offered = new List<int>();

            int? answer = DialogAnswers.Answer("taskDialog", "Some_Unknown_Dialog", code => { offered.Add(code); return code == DialogAnswers.Close; });

            Assert.Equal(DialogAnswers.Close, answer);
            Assert.Equal(new[] { DialogAnswers.Ok, DialogAnswers.Close }, offered);
            Assert.Equal(DialogAnswers.SecondLink, DialogAnswers.For("taskDialog", "TaskDialog_Unresolved_References")[0]);
            Assert.Null(DialogAnswers.Answer("messageBox", null, code => false));
            Assert.Equal(new[] { DialogAnswers.Ok }, DialogAnswers.For("dialogBox", "Dialog_Something"));
        }

        [Fact]
        public void R102_the_outputs_are_testhost_files_and_never_prototypes_json()
        {
            string path = TestHostGuard.OutputPath(@"C:\Docs", "C10", "prototypes");
            string steps = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSteps.cs");

            Assert.Equal(@"C:\Docs\SiteAdaptation\testhost_C10_prototypes.json", path);
            Assert.Equal(@"C:\Docs\SiteAdaptation\testhost_C10_log.json", TestHostGuard.OutputPath(@"C:\Docs", "C10", "log"));
            Assert.DoesNotContain("\"prototypes.json\"", steps);
            Assert.DoesNotContain("PlaceFromPlan.ReportPath", steps);
            Assert.DoesNotContain("SaveAs(original", steps);
        }

        [Fact]
        public void R102_the_job_steps_run_in_the_order_of_the_rule()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string[] names = { "unloadLinks", "openModel", "removePrototypeType", "dryRun", "realRun", "saveAfterPlace", "keepOnlyPrototype", "read", "close", "jobFile" };
            int[] at = names.Select(name => session.IndexOf("Name = \"" + name + "\"", StringComparison.Ordinal)).ToArray();

            Assert.All(at, index => Assert.True(index > 0));
            Assert.Equal(at.OrderBy(index => index), at);
            Assert.Contains("Name = \"close\", LimitMinutes = 5, Always = true", session);
        }

        [Fact]
        public void R102_the_unload_step_marks_every_revit_link_but_the_prototype_and_writes_before_the_open()
        {
            string steps = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSteps.cs");

            Assert.Contains("TransmissionData.ReadTransmissionData(path)", steps);
            Assert.Contains("data.SetDesiredReferenceData(id, reference.GetPath(), reference.PathType, keep)", steps);
            Assert.Contains("data.IsTransmitted = true", steps);
            Assert.Contains("TransmissionData.WriteTransmissionData(path, data)", steps);
            Assert.Contains("DetachFromCentralOption.DetachAndDiscardWorksets", steps);
        }
    }
}
