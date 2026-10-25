using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Round 16 on fakes: one run per prototype code into one file and the links loaded for an image
    public class Round16Tests
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private const string Work = @"C:\Docs\round\work";
        private const string NewFile = Work + @"\site-IMC-TEST-NEW.rvt";
        private const string KeyCopy = Work + @"\site-000001_TEST-IMK.rvt";
        private const string Links = @"C:\Models\Prototypes_2024";
        private static readonly DateTime Now = new DateTime(2026, 10, 24, 6, 0, 0, DateTimeKind.Utc);

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

        private static GuardResult Check(string text, Func<string, bool> folder = null)
        {
            return TestHostGuard.Check(text, Token, Now, path => true, folder ?? (path => true));
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
        public void R234_an_image_job_may_load_its_links_from_a_link_folder_that_exists()
        {
            GuardResult result = Check(Image(job => { job["loadLinks"] = true; job["linkFolder"] = Links; }));

            Assert.True(result.Act, result.Reason);
            Assert.True(result.Job.LoadLinks);
            Assert.True(Check(Image(job => job["loadLinks"] = true)).Act);
            Assert.True(Check(Image(job => { job["model"] = NewFile; job["code"] = "I16"; job["loadLinks"] = true; })).Act);
        }

        [Fact]
        public void R234_a_link_folder_needs_load_links_a_folder_that_exists_and_an_image_job()
        {
            Assert.Contains("linkFolder goes with loadLinks", Check(Image(job => job["linkFolder"] = Links)).Reason);
            Assert.False(Check(Image(job => { job["loadLinks"] = true; job["linkFolder"] = Links; }), folder => folder != Links).Act);
            Assert.False(Check(Image(job => { job["loadLinks"] = true; job["linkFolder"] = "Prototypes_2024"; })).Act);
            string read = new JObject { ["kind"] = "read", ["token"] = Token, ["code"] = "IMK", ["model"] = KeyCopy, ["loadLinks"] = true, ["createdUtc"] = Now.AddMinutes(-2).ToString("o") }.ToString();
            Assert.Contains("image job only", Check(read).Reason);
        }

        [Fact]
        public void R234_the_links_are_set_to_load_before_the_open_and_only_the_transmission_data_changes()
        {
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string site = RepoText("dotnet", "SiteAdaptation.TestHost", "SiteSteps.cs");
            int start = session.IndexOf("private IEnumerable<JobStep> SiteJobSteps", StringComparison.Ordinal);

            Assert.True(session.IndexOf("Name = \"loadLinks\"", start, StringComparison.Ordinal) < session.IndexOf("Name = \"openModel\"", start, StringComparison.Ordinal));
            Assert.Contains("data.SetDesiredReferenceData(id, reference.GetPath(), reference.PathType, true)", site);
            Assert.Contains("data.SetDesiredReferenceData(id, ModelPathUtils.ConvertUserVisiblePathToModelPath(other), PathType.Absolute, true)", site);
            Assert.Contains("TransmissionData.WriteTransmissionData(path, data)", site);
        }

        [Fact]
        public void R231_one_run_per_code_places_the_plots_of_that_prototype_file_only_and_a_floors_job_takes_a_plot_list()
        {
            string script = RepoText("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            string job = JobNames.AddTo(new JObject
            {
                ["kind"] = "linksAndFloors", ["token"] = Token, ["code"] = "S161", ["model"] = NewFile, ["plan"] = @"C:\Docs\plan_imc_new.json",
                ["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-DT3-ZZ-MOD-3300.rvt", ["plots"] = new JArray("2289", "2290"), ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            }).ToString();

            Assert.Contains("$roundJob.plots = [string[]]@($plan.plots | Where-Object { $_.prototypeFile.value -eq $files[0] }", script);
            Assert.True(Check(job).Act);
        }
    }
}
