using System;
using System.IO;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Report;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R472. Update from report takes the plan 4 Check wrote into its own folder, found by the run log of the last real run of 4 Check as 9 Report
    // finds it, never a plan_<NBH>.json of the work folder root (Q278)
    public sealed class R472_UpdatePlanTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "r472_" + Guid.NewGuid().ToString("N"));

        public R472_UpdatePlanTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        // A run of 4 Check as CheckRun writes it: the plan in check_4_<run id>, the run log run_<run id>.json naming it with its NBH and SHA-256
        internal static string WriteCheckRun(string workFolder, string runId, string nbh, string planText, bool committed = true)
        {
            string folder = Path.Combine(workFolder, "check_" + runId);
            Directory.CreateDirectory(folder);
            string plan = Path.Combine(folder, "plan_" + nbh + ".json");
            File.WriteAllText(plan, planText);
            var log = new JObject
            {
                ["runId"] = runId, ["committed"] = committed,
                ["outputs"] = new JArray(new JObject { ["role"] = "plan", ["nbh"] = nbh, ["path"] = Path.Combine("check_" + runId, "plan_" + nbh + ".json"), ["sha256"] = FileHash.Sha256(plan) }),
            };
            File.WriteAllText(Path.Combine(workFolder, "run_" + runId + ".json"), log.ToString());
            return plan;
        }

        [Fact]
        public void R472_the_plan_is_the_one_the_last_committed_check_lists_never_one_of_the_work_folder_root()
        {
            File.WriteAllText(Path.Combine(work, "plan_012.json"), "{\"root\": true}");
            string old = WriteCheckRun(work, "4_20261101-080000-000", "012", "{\"run\": 1}");
            string last = WriteCheckRun(work, "4_20261101-090000-000", "012", "{\"run\": 2}");
            // A later run that did not commit is passed over, as 9 Report passes it over
            WriteCheckRun(work, "4_20261101-100000-000", "012", "{\"run\": 3}", false);

            Assert.Equal(last, ReportRun.CheckRun.PlanOf(work, "012", File.ReadAllText).Path);
            Assert.NotEqual(old, last);
        }

        [Fact]
        public void R472_no_check_run_another_nbh_and_a_plan_changed_since_each_stop_with_one_line()
        {
            Assert.Contains("run 4 Check first", Assert.Throws<InvalidOperationException>(() => ReportRun.CheckRun.PlanOf(work, "012", File.ReadAllText)).Message);
            string plan = WriteCheckRun(work, "4_20261101-090000-000", "012", "{\"run\": 1}");
            Assert.Contains("lists no single plan of NBH 013", Assert.Throws<InvalidOperationException>(() => ReportRun.CheckRun.PlanOf(work, "013", File.ReadAllText)).Message);
            File.WriteAllText(plan, "{\"run\": 9}");
            Assert.Contains("SHA-256 differs", Assert.Throws<InvalidOperationException>(() => ReportRun.CheckRun.PlanOf(work, "012", File.ReadAllText)).Message);
        }

        [Fact]
        public void R472_an_override_list_changed_since_the_check_does_not_stop_the_plan()
        {
            WriteCheckRun(work, "4_20261101-090000-000", "012", "{\"run\": 1}");
            // CheckRun.Find holds the override list the run read, PlanOf holds the plan alone, so an Apply of Update before it does not stop a second read
            JObject log = JObject.Parse(File.ReadAllText(Path.Combine(work, "run_4_20261101-090000-000.json")));
            log["inputs"] = new JArray(new JObject { ["role"] = "overrides", ["path"] = "overrides.json", ["sha256"] = "0" });
            File.WriteAllText(Path.Combine(work, "run_4_20261101-090000-000.json"), log.ToString());

            Assert.True(File.Exists(ReportRun.CheckRun.PlanOf(work, "012", File.ReadAllText).Path));
        }
    }
}
