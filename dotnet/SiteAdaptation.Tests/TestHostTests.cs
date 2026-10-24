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

        // R102 placer filter

        private static PlanFile MixedPlan()
        {
            return PlacerTests.Plan(
                PlacerTests.Plot("1", 1010, 2010, 7, file: "VD2.rvt"),
                PlacerTests.Plot("2", 1020, 2020, 7, file: "DP4.rvt"),
                PlacerTests.Plot("3", 1030, 2030, 7, file: "VD2.rvt"));
        }

        [Fact]
        public void R102_a_filter_places_only_the_plots_of_its_file_without_case_and_counts_the_rest()
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
        public void R102_the_dry_run_filters_the_same_way()
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
        public void R102_a_filter_no_plot_uses_or_that_is_not_a_file_name_stops_and_places_nothing(string filter)
        {
            var target = new PlacerTests.FakeTarget();

            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", target, true, filter);

            Assert.True(report.Stopped);
            Assert.Contains("nothing was placed", report.Failure);
            Assert.Empty(report.Placed);
            Assert.Equal(0, target.Begins);
        }

        [Fact]
        public void R102_no_filter_tries_every_plot_as_before()
        {
            PlacerReport all = Placer.Run(MixedPlan(), "plan.json", new PlacerTests.FakeTarget(), true, null);
            PlacerReport before = Placer.Run(MixedPlan(), "plan.json", new PlacerTests.FakeTarget(), true);

            Assert.Equal(3, all.Counts.Placed);
            Assert.Equal(0, all.Counts.FilteredOut);
            Assert.Null(all.PrototypeFilter);
            Assert.Equal(before.Placed.Select(plot => plot.Plot), all.Placed.Select(plot => plot.Plot));
        }

        [Fact]
        public void R102_the_first_entry_of_a_plot_number_is_found_over_the_whole_plan()
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
        public void R102_plots_of_other_files_already_in_the_document_are_left_out_and_not_skipped()
        {
            var target = new PlacerTests.FakeTarget();
            target.PlotNumbers.AddRange(new[] { "1", "3" });

            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", target, true, "DP4.rvt");

            Assert.Empty(report.Skipped);
            Assert.Single(report.Placed);
        }

        [Fact]
        public void R102_the_report_file_holds_the_filter_and_the_count_left_out()
        {
            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", new PlacerTests.FakeTarget(), false, "DP4.rvt");

            JObject json = JObject.Parse(JsonOut.ToJson(report));

            Assert.Equal("DP4.rvt", (string)json["prototypeFilter"]);
            Assert.Equal(2, (int)json["counts"]["filteredOut"]);
        }

        [Fact]
        public void R102_the_button_offers_the_filter_and_calls_the_shared_entry()
        {
            string command = RepoText("dotnet", "SiteAdaptation.Placer", "PlaceFromPlanCommand.cs");

            Assert.Contains("PlaceFromPlan.Run(active.Document, plan, planPath, real, prototypeFile)", command);
            Assert.Contains("The plots of one prototype file", command);
            Assert.DoesNotContain("Common.Placer.Run(", command);
        }

        // R107 link instance name

        [Fact]
        public void R107_the_real_run_writes_the_link_instance_name_of_the_plan_and_counts_it()
        {
            var target = new PlacerTests.FakeTarget();

            PlacerReport report = Placer.Run(MixedPlan(), "plan.json", target, true, null);

            Assert.Equal(3, report.Counts.NamesWritten);
            Assert.Equal(new[] { "1 | VD2 | 1 | XX | XX", "2 | VD2 | 1 | XX | XX", "3 | VD2 | 1 | XX | XX" }, target.Names.OrderBy(pair => pair.Key).Select(pair => pair.Value));
            Assert.Empty(report.Issues);
            Assert.Equal("2 | VD2 | 1 | XX | XX", report.Placed[1].LinkName);
        }

        [Fact]
        public void R107_a_name_that_cannot_be_written_or_is_missing_is_an_issue_and_the_run_goes_on()
        {
            var target = new PlacerTests.FakeTarget { NameReadOnly = true };
            PlanFile plan = MixedPlan();
            plan.Plots[2].LinkName = null;

            PlacerReport report = Placer.Run(plan, "plan.json", target, true, null);

            Assert.False(report.RolledBack);
            Assert.Equal(3, report.Counts.Placed);
            Assert.Equal(0, report.Counts.NamesWritten);
            Assert.Equal(2, report.Issues.Count(issue => issue.Contains("Name was not written, ReadOnly")));
            Assert.Single(report.Issues, issue => issue.StartsWith("plot 3: the plan has no linkName"));
        }

        [Fact]
        public void R107_the_dry_run_lists_the_name_writes_none_and_flags_a_missing_one()
        {
            var target = new PlacerTests.FakeTarget();
            PlanFile plan = MixedPlan();
            plan.Plots[0].LinkName = new Sourced<string> { Value = " ", Source = "pastJob" };

            PlacerReport report = Placer.Run(plan, "plan.json", target, false, null);

            Assert.Empty(target.Names);
            Assert.Equal("3 | VD2 | 1 | XX | XX", report.Placed[2].LinkName);
            Assert.Single(report.Issues, issue => issue.StartsWith("plot 1: the plan has no linkName"));
        }

        [Fact]
        public void R107_the_revit_side_writes_the_built_in_instance_name_parameter()
        {
            string text = RepoText("dotnet", "SiteAdaptation.Placer", "RevitPlacerTarget.cs");

            Assert.Contains("element.get_Parameter(BuiltInParameter.RVT_LINK_INSTANCE_NAME)", text);
        }

        [Fact]
        public void R107_the_plan_reader_takes_link_name_from_plan_json()
        {
            PlanFile plan = PlanFile.Parse("{\"plots\": [{\"plot\": \"1\", \"linkName\": {\"value\": \"1 | C10 | 1 | XX | XX\", \"source\": \"pastJob\"}}]}");

            Assert.Equal("1 | C10 | 1 | XX | XX", plan.Plots[0].LinkName.Value);
        }

        // R103 test host guard

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

        // R109 upgrade job

        private const string Source = @"C:\Models\Prototypes\RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt";
        private const string Target = @"C:\Models\Prototypes_2024\RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt";

        private static string UpgradeJob(Action<JObject> change = null)
        {
            var job = new JObject
            {
                ["kind"] = "upgrade",
                ["token"] = Token,
                ["code"] = "C10",
                ["prototypeFile"] = Prototype,
                ["source"] = Source,
                ["target"] = Target,
                ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            };
            change?.Invoke(job);
            return job.ToString();
        }

        private static GuardResult CheckUpgrade(string text, Func<string, bool> exists = null, Func<string, bool> folder = null)
        {
            return TestHostGuard.Check(text, Token, Now, exists ?? (path => path == Source), folder ?? (path => true));
        }

        [Fact]
        public void R109_an_upgrade_job_with_a_source_and_a_new_target_of_the_same_name_is_accepted()
        {
            GuardResult result = CheckUpgrade(UpgradeJob());

            Assert.True(result.Act, result.Reason);
            Assert.Equal(TestHostGuard.UpgradeKind, result.Job.Kind);
            Assert.Equal(Target, result.Job.Target);
        }

        [Fact]
        public void R109_a_target_that_exists_is_in_the_source_folder_or_has_another_name_is_refused()
        {
            Assert.Contains("never overwritten", CheckUpgrade(UpgradeJob(), exists: path => true).Reason);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["target"] = @"C:\Models\Prototypes\RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt")).Act);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["target"] = @"C:\Models\Prototypes_2024\rre-wp-nsp-keo-arc-c10-zz-mod-1500.rvt")).Act);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["target"] = @"C:\Models\Prototypes_2024\Other.rvt")).Act);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["target"] = @"C:\Models\Prototypes_2024\..\Prototypes\RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt")).Act);
        }

        [Fact]
        public void R109_a_missing_source_or_target_folder_and_a_source_of_another_file_are_refused()
        {
            Assert.False(CheckUpgrade(UpgradeJob(), exists: path => false).Act);
            Assert.Contains("target folder does not exist", CheckUpgrade(UpgradeJob(), folder: path => false).Reason);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["source"] = @"C:\Models\Prototypes\RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt")).Act);
        }

        [Fact]
        public void R109_an_upgrade_job_names_no_model_or_plan_and_a_place_job_no_source_or_target()
        {
            Assert.False(CheckUpgrade(UpgradeJob(job => job["model"] = Model)).Act);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["plan"] = PlanPath)).Act);
            Assert.False(Check(Job(job => job["source"] = Source)).Act);
            Assert.False(Check(Job(job => job["target"] = Target)).Act);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["kind"] = "Upgrade")).Act);
            Assert.False(CheckUpgrade(UpgradeJob(job => job["kind"] = "save")).Act);
            Assert.True(Check(Job(job => job["kind"] = "place")).Act);
        }

        [Fact]
        public void R109_the_upgrade_opens_detached_keeps_worksets_saves_a_new_central_and_checks_the_version()
        {
            string steps = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSteps.cs");
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");

            Assert.Contains("DetachFromCentralOption.DetachAndPreserveWorksets", steps);
            Assert.Contains("new SaveAsOptions { OverwriteExistingFile = false, MaximumBackups = 1 }", steps);
            Assert.Contains("new WorksharingSaveAsOptions { SaveAsCentral = true }", steps);
            Assert.Contains("document.SaveAs(job.Target, save)", steps);
            Assert.Contains("if (info.Format != app.VersionNumber)", steps);
            Assert.DoesNotContain("SaveAs(job.Source", steps);
            string[] names = { "sourceInfo", "openSource", "saveTarget", "close", "targetInfo", "jobFile" };
            int[] at = names.Select(name => session.IndexOf("Name = \"" + name + "\"", StringComparison.Ordinal)).ToArray();
            Assert.All(at, index => Assert.True(index > 0));
            Assert.Equal(at.OrderBy(index => index), at);
            Assert.Contains("FileIdentity.CheckTargetFolder(Path.GetDirectoryName(guard.Job.Target))", session);
        }

        [Fact]
        public void R109_a_target_folder_under_a_junction_is_refused_and_a_plain_one_passes()
        {
            string folder = Path.Combine(Path.GetTempPath(), "testhost-" + Guid.NewGuid().ToString("N"));
            string real = Path.Combine(folder, "real");
            string junction = Path.Combine(folder, "junction");
            Directory.CreateDirectory(real);
            try
            {
                var make = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + junction + "\" \"" + real + "\"") { UseShellExecute = false, CreateNoWindow = true });
                make.WaitForExit();
                Assert.True(Directory.Exists(junction));

                Assert.Null(FileIdentity.CheckTargetFolder(real));
                Assert.Contains("link or junction", FileIdentity.CheckTargetFolder(junction));
                Assert.Contains("link or junction", FileIdentity.CheckTargetFolder(Path.Combine(junction, "inner")));
            }
            finally
            {
                if (Directory.Exists(junction))
                {
                    Directory.Delete(junction);
                }
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R103_without_a_token_or_a_job_file_the_test_host_never_wakes()
        {
            Assert.False(TestHostGuard.ShouldWake(null, true));
            Assert.False(TestHostGuard.ShouldWake("", true));
            Assert.False(TestHostGuard.ShouldWake(Token, false));
            Assert.True(TestHostGuard.ShouldWake(Token, true));
        }

        [Fact]
        public void R103_a_good_job_is_accepted_with_its_values()
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
        public void R103_a_session_token_that_is_missing_short_or_not_the_job_token_is_refused(string token)
        {
            Assert.False(Check(Job(), token).Act);
        }

        [Fact]
        public void R103_a_job_without_a_token_or_with_another_one_is_refused()
        {
            Assert.False(Check(Job(job => job.Remove("token"))).Act);
            Assert.False(Check(Job(job => job["token"] = "")).Act);
            Assert.False(Check(Job(job => job["token"] = Token.ToUpperInvariant())).Act);
        }

        [Theory]
        [InlineData(61)]
        [InlineData(600)]
        [InlineData(-6)]
        public void R103_a_job_older_than_an_hour_or_from_the_future_is_refused(int minutesAgo)
        {
            Assert.False(Check(Job(job => job["createdUtc"] = Now.AddMinutes(-minutesAgo).ToString("o"))).Act);
        }

        [Fact]
        public void R103_a_job_with_no_date_is_refused()
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
        public void R103_only_a_copy_named_for_its_code_is_ever_opened(string model)
        {
            Assert.False(Check(Job(job => job["model"] = model)).Act);
        }

        [Fact]
        public void R103_a_missing_model_key_is_refused()
        {
            Assert.False(Check(Job(job => job.Remove("model"))).Act);
        }

        [Theory]
        [InlineData("RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt")]
        [InlineData(@"C:\P\RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt")]
        [InlineData("RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rfa")]
        [InlineData("")]
        public void R103_the_prototype_file_must_be_the_keo_file_of_the_code(string file)
        {
            Assert.False(Check(Job(job => job["prototypeFile"] = file)).Act);
        }

        [Theory]
        [InlineData("c10")]
        [InlineData("C10X1")]
        [InlineData("C1/")]
        [InlineData("")]
        public void R103_a_code_that_is_not_a_prototype_code_is_refused(string code)
        {
            Assert.False(Check(Job(job => job["code"] = code)).Act);
        }

        [Fact]
        public void R103_a_model_or_plan_that_does_not_exist_or_a_plan_that_is_not_full_json_is_refused()
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
        public void R103_a_job_file_that_does_not_read_is_refused(string text)
        {
            Assert.False(Check(text).Act);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(30)]
        public void R103_a_total_limit_outside_0_to_29_minutes_is_refused(double minutes)
        {
            Assert.False(Check(Job(job => job["totalMinutes"] = minutes)).Act);
        }

        [Fact]
        public void R103_the_original_of_a_copy_is_its_name_without_the_test_tag()
        {
            Assert.Equal(@"C:\Models\site.rvt", TestHostGuard.OriginalOf(Model));
            Assert.Null(TestHostGuard.OriginalOf(@"C:\Models\site.rvt"));
        }

        [Fact]
        public void R103_the_app_reads_the_token_then_the_job_file_and_loads_nothing_before()
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
        public void R103_the_job_is_claimed_by_a_move_then_checked_and_only_then_subscribed()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            int move = session.IndexOf("File.Move(jobPath, claimed);", StringComparison.Ordinal);
            int read = session.IndexOf("File.ReadAllText(claimed)", StringComparison.Ordinal);
            int check = session.IndexOf("TestHostGuard.Check(", StringComparison.Ordinal);
            int copy = session.IndexOf("FileIdentity.CheckCopy(", StringComparison.Ordinal);
            int refuse = session.IndexOf("if (!guard.Act)", StringComparison.Ordinal);
            int subscribe = session.IndexOf("session.Subscribe();", StringComparison.Ordinal);

            Assert.True(move > 0 && move < read && read < check && check < copy && copy < refuse && refuse < subscribe);
            Assert.DoesNotContain("File.ReadAllText(jobPath)", session);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(session, @"\+= OnFirstIdling").Cast<object>());
        }

        [Fact]
        public void R103_a_failure_while_waking_never_reaches_revit()
        {
            string app = RepoText("dotnet", "SiteAdaptation.TestHost", "TestHostApp.cs");
            int wake = app.IndexOf("Wake(application, token, jobPath);", StringComparison.Ordinal);

            Assert.True(app.LastIndexOf("try", wake, StringComparison.Ordinal) > app.IndexOf("AssemblyResolve += Resolve", StringComparison.Ordinal));
            Assert.Contains("catch (Exception failure)", app.Substring(wake));
            Assert.Contains("testhost_refused.json", app.Substring(wake));
        }

        [Theory]
        [InlineData("totalMinutes")]
        [InlineData("code")]
        [InlineData("prototypeFile")]
        [InlineData("token")]
        public void R103_nan_and_a_final_newline_are_refused(string key)
        {
            string text = Job(job =>
            {
                if (key == "totalMinutes")
                {
                    job[key] = double.NaN;
                }
                else
                {
                    job[key] = (string)job[key] + "\n";
                }
            });

            Assert.False(Check(text).Act);
            Assert.False(Check(text, Token + "\n").Act);
        }

        [Theory]
        [InlineData(@"C:\Models\site.rvt:x_TEST-C10.rvt")]
        [InlineData(@"\\?\C:\Models\site_TEST-C10.rvt")]
        [InlineData(@"\\.\C:\Models\site_TEST-C10.rvt")]
        [InlineData(@"C:\Models\site_TEST-C10.rvt.")]
        [InlineData(@"C:\Models\site_TEST-C10.rvt ")]
        [InlineData(@"C:/Models/site_TEST-C10.rvt")]
        [InlineData(@"C:\Models\.\site_TEST-C10.rvt")]
        [InlineData(@"C:\Models\\site_TEST-C10.rvt")]
        public void R103_stream_device_and_changed_paths_are_refused(string model)
        {
            Assert.False(Check(Job(job => job["model"] = model)).Act);
            Assert.False(Check(Job(job => job["plan"] = model.Replace(".rvt", ".json"))).Act);
        }

        [Fact]
        public void R103_a_job_of_the_wrong_types_is_refused_and_the_check_never_throws()
        {
            Assert.False(Check(Job(job => job["code"] = 5)).Act);
            Assert.False(Check(Job(job => job["createdUtc"] = "yesterday")).Act);
            Assert.False(Check(Job(job => job["model"] = new JArray())).Act);
            GuardResult thrown = TestHostGuard.Check(Job(), Token, Now, path => { throw new IOException("disk"); });
            Assert.False(thrown.Act);
            Assert.Contains("could not be checked", thrown.Reason);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string link, string existing, IntPtr security);

        [Fact]
        public void R103_a_copy_must_be_a_file_of_its_own_and_not_a_hard_link_or_the_original()
        {
            string folder = Path.Combine(Path.GetTempPath(), "testhost-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string original = Path.Combine(folder, "site.rvt");
                string copy = Path.Combine(folder, "site_TEST-C10.rvt");
                string linked = Path.Combine(folder, "other_TEST-C10.rvt");
                File.WriteAllText(original, "model");
                File.Copy(original, copy);
                Assert.True(CreateHardLink(linked, original, IntPtr.Zero));

                Assert.Null(FileIdentity.CheckCopy(copy, original));
                Assert.Contains("hard links", FileIdentity.CheckCopy(linked, Path.Combine(folder, "other.rvt")));
                Assert.Contains("is the original", FileIdentity.CheckCopy(copy, copy));
                Assert.Contains("hard links", FileIdentity.CheckCopy(original, copy));
                Assert.Contains("could not be checked", FileIdentity.CheckCopy(Path.Combine(folder, "none_TEST-C10.rvt"), original));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R103_the_test_host_runs_the_button_code_of_the_placer_and_the_reader_and_no_copy()
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

        // R104 test host steps and log

        private sealed class Clock
        {
            public DateTime Now = TestHostTests.Now;
        }

        private static TestHostRunner Runner(TestHostLog log, Clock clock, List<string> saves)
        {
            return new TestHostRunner(log, value => saves.Add(JsonOut.ToJson(value)), () => clock.Now);
        }

        [Fact]
        public void R104_steps_run_in_order_and_each_is_logged_when_it_starts_and_ends()
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
        public void R104_a_failed_step_keeps_the_full_exception_and_skips_the_rest_but_the_always_steps()
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
        public void R104_the_watchdog_writes_the_timeout_and_then_kills_once_when_a_step_runs_over()
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
        public void R104_the_whole_job_has_a_limit_and_a_late_exit_after_the_job_keeps_its_result()
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
        public void R104_a_dialog_gets_the_first_answer_revit_takes_and_a_missing_link_is_ignored()
        {
            var offered = new List<int>();

            int? answer = DialogAnswers.Answer("taskDialog", "Some_Unknown_Dialog", code => { offered.Add(code); return code == DialogAnswers.Ok; });

            Assert.Equal(DialogAnswers.Ok, answer);
            Assert.Equal(new[] { DialogAnswers.Close, DialogAnswers.Cancel, DialogAnswers.Ok }, offered);
            Assert.Equal(DialogAnswers.SecondLink, DialogAnswers.For("taskDialog", "TaskDialog_Unresolved_References")[0]);
            Assert.Null(DialogAnswers.Answer("messageBox", null, code => false));
            Assert.Equal(new[] { DialogAnswers.Cancel, DialogAnswers.No, DialogAnswers.Ok }, DialogAnswers.For("messageBox", "Some_Box"));
            Assert.Equal(new[] { DialogAnswers.Cancel }, DialogAnswers.For("dialogBox", "Dialog_Something"));
        }

        [Fact]
        public void R104_an_unlisted_dialog_never_gets_a_command_link()
        {
            foreach (string kind in new[] { "taskDialog", "messageBox", "dialogBox" })
            {
                Assert.DoesNotContain(DialogAnswers.FirstLink, DialogAnswers.For(kind, "TaskDialog_Location_Position_Changed"));
                Assert.DoesNotContain(DialogAnswers.SecondLink, DialogAnswers.For(kind, "TaskDialog_Local_Changes_Not_Synchronized_With_Central"));
            }
        }

        [Fact]
        public void R104_errors_roll_back_only_in_the_test_host_transactions_and_unload_never_saves_link_positions()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string steps = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSteps.cs");

            Assert.Contains("RollBackSteps = new HashSet<string>(StringComparer.Ordinal) { \"removePrototypeType\", \"realRun\" }", session);
            Assert.Contains("if (error && RollBackSteps.Contains(runner.CurrentStep ?? \"\"))", session);
            Assert.Contains("type.Unload(new KeepLinkPositions())", steps);
            Assert.Contains("SaveModifiedLinksOptions.DoNotSaveLinks", steps);
            Assert.DoesNotContain("Unload(null)", steps);
        }

        [Fact]
        public void R104_the_outputs_are_testhost_files_and_never_prototypes_json()
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
        public void R104_the_job_steps_run_in_the_order_of_the_rule()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string[] names = { "unloadLinks", "openModel", "removePrototypeType", "dryRun", "realRun", "saveAfterPlace", "keepOnlyPrototype", "read", "close", "jobFile" };
            int place = session.IndexOf("yield break;", StringComparison.Ordinal);
            int[] at = names.Select(name => session.IndexOf("Name = \"" + name + "\"", place, StringComparison.Ordinal)).ToArray();

            Assert.All(at, index => Assert.True(index > 0));
            Assert.Equal(at.OrderBy(index => index), at);
            Assert.Contains("Name = \"close\", LimitMinutes = 5, Always = true", session);
        }

        [Fact]
        public void R104_the_unload_step_marks_every_revit_link_but_the_prototype_and_writes_before_the_open()
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
