using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R117. The master and floor jobs of the test host on fakes: the guard on the job text and the backups a save leaves
    public class FloorJobTests
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private const string Folder = @"C:\Models";
        private const string PlanPath = @"C:\Docs\plan.json";
        private const string Master = @"C:\Models\site_TEST-MASTER.rvt";
        private const string Work = @"C:\Models\site_TEST-WORK-S1.rvt";
        private static readonly DateTime Now = new DateTime(2026, 10, 24, 6, 0, 0, DateTimeKind.Utc);

        private static JObject Job(string kind, string code, string model, Action<JObject> change = null)
        {
            var job = new JObject
            {
                ["kind"] = kind,
                ["token"] = Token,
                ["code"] = code,
                ["model"] = model,
                ["plan"] = PlanPath,
                ["createdUtc"] = Now.AddMinutes(-1).ToString("o"),
            };
            if (kind == TestHostGuard.MasterKind)
            {
                job["deleteFloors"] = "all";
            }
            if (change != null)
            {
                change(job);
            }
            return JobNames.AddTo(job);
        }

        private static GuardResult Check(JObject job)
        {
            return TestHostGuard.Check(job.ToString(), Token, Now, path => path == PlanPath || path == Master || path == Work || path == @"C:\Models\site_TEST-WORK-S8.rvt");
        }

        [Fact]
        public void R117_a_master_job_with_code_MST_on_the_master_file_that_deletes_all_floors_is_run()
        {
            GuardResult result = Check(Job("master", "MST", Master));
            Assert.True(result.Act, result.Reason);
        }

        [Fact]
        public void R117_a_master_job_on_another_name_with_another_code_or_without_deleting_all_is_refused()
        {
            Assert.Contains("_TEST-MASTER.rvt", Check(Job("master", "MST", Work)).Reason);
            Assert.Contains("code MST", Check(Job("master", "MS", @"C:\Models\site_TEST-MASTER.rvt")).Reason);
            Assert.Contains("deletes all floors", Check(Job("master", "MST", Master, job => job["deleteFloors"] = "plots")).Reason);
            Assert.Contains("names no prototype file", Check(Job("master", "MST", Master, job => job["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt")).Reason);
            // The site model itself is never a master
            Assert.False(Check(Job("master", "MST", @"C:\Models\site.rvt")).Act);
        }

        [Fact]
        public void R117_a_floors_job_runs_only_on_the_work_file_of_its_scenario_code()
        {
            Assert.True(Check(Job("floors", "S1", Work)).Act);
            Assert.Contains("_TEST-WORK-S2.rvt", Check(Job("floors", "S2", Work)).Reason);
            Assert.Contains("scenario code", Check(Job("floors", "C10", @"C:\Models\site_TEST-WORK-C10.rvt")).Reason);
            Assert.False(Check(Job("floors", "S1", Master)).Act);
            Assert.False(Check(Job("floors", "S1", @"C:\Models\site_TEST-WORK-S1_TEST-WORK-S1.rvt")).Act);
        }

        [Fact]
        public void R117_the_floor_options_are_checked_and_each_goes_only_where_it_means_something()
        {
            Assert.True(Check(Job("floors", "S1", Work, job =>
            {
                job["deleteFloors"] = "plots";
                job["deletePlots"] = new JArray("2406", "2441");
                job["runTwice"] = true;
            })).Act);
            Assert.True(Check(Job("floors", "S1", Work, job => job["plots"] = new JArray("2406"))).Act);
            Assert.Contains("goes with deletePlots", Check(Job("floors", "S1", Work, job => job["deleteFloors"] = "plots")).Reason);
            Assert.Contains("goes with deletePlots", Check(Job("floors", "S1", Work, job => job["deletePlots"] = new JArray("2406"))).Reason);
            Assert.Contains("different plot numbers", Check(Job("floors", "S1", Work, job => { job["deleteFloors"] = "plots"; job["deletePlots"] = new JArray("x"); })).Reason);
            Assert.Contains("all or plots", Check(Job("floors", "S1", Work, job => job["deleteFloors"] = "some")).Reason);
            Assert.Contains("different plot numbers", Check(Job("floors", "S1", Work, job => job["plots"] = new JArray("2406", "2406"))).Reason);
            Assert.Contains("different plot numbers", Check(Job("floors", "S1", Work, job => job["plots"] = new JArray("24a"))).Reason);
            Assert.Contains("different plot numbers", Check(Job("floors", "S1", Work, job => job["plots"] = new JArray())).Reason);
            Assert.Contains("does not run twice", Check(Job("floors", "S1", Work, job => { job["dryRunOnly"] = true; job["runTwice"] = true; })).Reason);
            Assert.Contains("stop or rollBack", Check(Job("floors", "S1", Work, job => job["expect"] = "fail")).Reason);
            Assert.True(Check(Job("floors", "S1", Work, job => job["expect"] = "rollBack")).Act);
            // A place job of round 15a takes none of them
            JObject place = Job("place", "C10", @"C:\Models\site_TEST-C10.rvt", job => job["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt");
            place["dryRunOnly"] = true;
            Assert.Contains("go with a floors", TestHostGuard.Check(place.ToString(), Token, Now, path => true).Reason);
        }

        [Fact]
        public void R117_links_and_floors_names_a_prototype_file_and_floors_alone_names_none()
        {
            Assert.True(Check(Job("linksAndFloors", "S8", @"C:\Models\site_TEST-WORK-S8.rvt", job => job["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt")).Act);
            Assert.Contains("prototype file is not", Check(Job("linksAndFloors", "S8", @"C:\Models\site_TEST-WORK-S8.rvt")).Reason);
            Assert.Contains("names no prototype file", Check(Job("floors", "S1", Work, job => job["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt")).Reason);
        }

        [Fact]
        public void R117_an_unknown_kind_is_refused()
        {
            Assert.Contains("job kind is not", Check(Job("delete", "S1", Work)).Reason);
        }

        [Fact]
        public void R117_the_master_of_a_work_file_sits_next_to_it_and_the_original_is_the_site_model()
        {
            Assert.Equal(Master, TestHostGuard.MasterOf(Work));
            Assert.Equal(@"C:\Models\site.rvt", TestHostGuard.OriginalOf(Work));
            Assert.Equal(@"C:\Models\site.rvt", TestHostGuard.OriginalOf(Master));
        }

        [Fact]
        public void R117_backups_are_the_four_digit_files_and_the_backup_folder_of_the_copy_and_nothing_else()
        {
            string[] names =
            {
                "site_TEST-WORK-S1.rvt", "site_TEST-WORK-S1.0001.rvt", "site_TEST-WORK-S1.0002.RVT", "site_TEST-WORK-S1.01.rvt",
                "site_TEST-WORK-S11.0001.rvt", "site_TEST-MASTER.0001.rvt", "site.0001.rvt", "site_TEST-WORK-S1.0001.rvt.bak",
            };
            Assert.Equal(new[] { @"C:\Models\site_TEST-WORK-S1.0001.rvt", @"C:\Models\site_TEST-WORK-S1.0002.RVT" }, TestHostFiles.BackupFiles(Work, names));
            Assert.Equal(@"C:\Models\site_TEST-WORK-S1_backup", TestHostFiles.BackupFolder(Work));
            // The site model is no test copy, so none of its backups is ever found and its backup folder is never named
            Assert.Empty(TestHostFiles.BackupFiles(@"C:\Models\site.rvt", names));
            Assert.Null(TestHostFiles.BackupFolder(@"C:\Models\site.rvt"));
            Assert.Throws<InvalidOperationException>(() => TestHostFiles.DeleteBackups(@"C:\Models\site.rvt"));
        }

        [Fact]
        public void R117_deleting_the_backups_of_a_copy_leaves_the_copy_the_master_and_the_site_model()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sa_r117_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string work = Path.Combine(folder, "site_TEST-WORK-S1.rvt");
                foreach (string name in new[] { "site.rvt", "site.0001.rvt", "site_TEST-MASTER.rvt", "site_TEST-MASTER.0001.rvt", "site_TEST-WORK-S1.rvt", "site_TEST-WORK-S1.0001.rvt" })
                {
                    File.WriteAllText(Path.Combine(folder, name), "x");
                }
                Directory.CreateDirectory(Path.Combine(folder, "site_backup"));
                Directory.CreateDirectory(Path.Combine(folder, "site_TEST-WORK-S1_backup"));
                File.WriteAllText(Path.Combine(folder, "site_TEST-WORK-S1_backup", "incremental.dat"), "x");

                string line = TestHostFiles.DeleteBackups(work);

                Assert.Equal("backup files deleted 1, backup folder deleted", line);
                Assert.Equal(new[] { "site.0001.rvt", "site.rvt", "site_TEST-MASTER.0001.rvt", "site_TEST-MASTER.rvt", "site_TEST-WORK-S1.rvt" },
                    Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
                Assert.Equal(new[] { "site_backup" }, Directory.GetDirectories(folder).Select(Path.GetFileName).ToArray());
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R117_the_session_runs_the_floor_steps_in_order_and_every_save_deletes_its_backup()
        {
            string session = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.TestHost", "JobSession.cs"));
            string steps = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.TestHost", "JobSteps.cs"));
            string floors = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.TestHost", "FloorSteps.cs"));
            string[] order = { "\"unloadLinks\"", "\"removePrototypeType\"", "\"deleteFloors\"", "\"dryRun\"", "\"realRun\"", "\"realRun2\"", "\"save\"", "\"keepOnlyPrototype\"", "\"read\"", "\"close\"" };
            int body = session.IndexOf("private IEnumerable<JobStep> FloorJobSteps", StringComparison.Ordinal);
            int[] at = order.Select(name => session.IndexOf(name, body, StringComparison.Ordinal)).ToArray();
            Assert.All(at, index => Assert.True(index > body));
            Assert.Equal(at.OrderBy(index => index).ToArray(), at);
            Assert.Contains("FileIdentity.CheckCopy(guard.Job.Model, TestHostGuard.MasterOf(guard.Job.Model))", session);
            Assert.DoesNotContain("document.Save()", steps);
            Assert.Contains("MaximumBackups = 1", floors);
            Assert.Contains("TestHostFiles.DeleteBackups(model)", floors);
        }

        private static string RepoFile(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }
    }
}
