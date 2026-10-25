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
