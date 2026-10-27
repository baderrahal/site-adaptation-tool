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
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray()));
        }

        [WindowsFact]
        public void R302_the_image_step_takes_the_limit_the_job_names()
        {
            GuardResult result = Check(Image());

            Assert.True(result.Act, result.Reason);
            Assert.Equal(30, TestHostGuard.ImageStepMinutes(result.Job));
            Assert.Equal(45, TestHostGuard.ImageStepMinutes(Check(Image(job => job["imageMinutes"] = 45)).Job));
        }

        [WindowsTheory]
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

        [WindowsFact]
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

        [WindowsFact]
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
        public void R304_the_startup_config_of_an_exe_has_a_short_name_in_obj_so_a_clone_in_a_deep_folder_builds()
        {
            string targets = RepoText("dotnet", "Directory.Build.targets");
            const string shortName = "runtime.config";

            Assert.Contains("BeforeTargets=\"_WriteAppConfigWithSupportedRuntime\"", targets);
            Assert.Contains("<_GenerateSupportedRuntimeIntermediateAppConfig>$(IntermediateOutputPath)" + shortName + "</_GenerateSupportedRuntimeIntermediateAppConfig>", targets);
            // The clone of the review whose path to the default name was 271 characters, MAX_PATH is 260
            int defaultName = "SiteAdaptation.DialogWatcher.exe.withSupportedRuntime.config".Length;
            Assert.True(271 - defaultName + shortName.Length < 260);
            foreach (string project in new[] { "SiteAdaptation.DialogWatcher", "SiteAdaptation.C3DWatcher" })
            {
                Assert.Contains("<OutputType>Exe</OutputType>", RepoText("dotnet", project, project + ".csproj"));
            }
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
