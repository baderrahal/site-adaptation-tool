using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R378, R379. The inputs of buttons 5 and 6 from the work folder: the plan the last real run of 4 Check names, its level profile, the
    // last record of 5 and every input of that build the worker command levelExcel takes, each held by its SHA-256
    public class R379_LevelLoadTests : IDisposable
    {
        private const string RunId = "4_20261031-100000-000";
        private readonly string work = Path.Combine(Path.GetTempPath(), "r379load_" + Guid.NewGuid().ToString("N"));
        private readonly string folder;
        private readonly string overrides;

        public R379_LevelLoadTests()
        {
            folder = Path.Combine(work, CheckRun.FolderPrefix + RunId);
            string steps = Path.Combine(folder, "steps");
            Directory.CreateDirectory(steps);
            string config = Write(Path.Combine(work, "config", "project.json"), new JObject { ["addin"] = new JObject { ["workerTimeLimitS"] = new JObject { ["levelExcel"] = 60 } } }.ToString());
            string rules = Write(Path.Combine(work, "rules.json"), "{}");
            overrides = Write(Path.Combine(work, "overrides.json"), "{}");
            string plots = Write(Path.Combine(work, "plots.csv"), "locationPlanNumber,zone,nbhId\n");
            JObject plan = JObject.FromObject(FloorPlans.Plan(FloorPlans.Plot("101")), Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }));
            plan["missing"] = new JArray(new JObject { ["plot"] = "102", ["written"] = true, ["missing"] = new JArray(new JObject { ["key"] = "ffl", ["why"] = "no road level at a front corner" }) });
            string planPath = Write(Path.Combine(folder, CheckRun.PlanName("012")), plan.ToString());
            var outputs = new List<string> { planPath };
            foreach (string step in FloorLoad.StepRoles)
            {
                outputs.Add(Write(Path.Combine(steps, "step_" + step + "_csharp.json"), "{}"));
            }
            File.WriteAllText(Path.Combine(folder, CheckRun.RecordFile), CheckRun.Record(RunId, outputs.ToDictionary(path => path, FileHash.Sha256)).ToString());
            var request = new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = RunId, Command = "build", LevelProfile = "parsons2024Rev6", OutputFolder = folder, StepsFolder = steps,
                Inputs = new[] { ("config", config), ("rules", rules), ("overrides", overrides), ("plotsCsv", plots) }.Select(item => new WorkerFile { Role = item.Item1, Path = item.Item2, Sha256 = FileHash.Sha256(item.Item2) }).ToList(),
            };
            request.Write(folder);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static string Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        private RunContext Context(int button)
        {
            return new RunContext(button, button + " Levels", "6_1", work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        private static SettingsScope Nbh => new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" };

        [Fact]
        public void R379_levels_take_every_input_of_the_build_by_its_role_and_the_reason_of_a_plot_with_no_ffl()
        {
            LevelRunInputs inputs = FloorLoad.Levels(Context(6), Nbh, new WorkerDocument { Path = "Site.rvt" });

            Assert.Equal(new[] { "plan", "config", "rules", "overrides", "plotsCsv", "clusters", "levels", "hands" }, inputs.ExcelInputs.Select(file => file.Role));
            Assert.Equal("parsons2024Rev6", inputs.Profile);
            Assert.Equal(60, inputs.TimeLimitS);
            Assert.Equal("ffl: no road level at a front corner", inputs.NoFfl["102"]);
            Assert.Single(inputs.Plan.Plots);
        }

        [Fact]
        public void R379_an_input_of_the_build_changed_since_4_check_stops_levels()
        {
            File.AppendAllText(overrides, " ");

            var stop = Assert.Throws<InvalidDataException>(() => FloorLoad.Levels(Context(6), Nbh, null));

            Assert.Contains("the overrides overrides.json changed since the run of 4 Check that built plan_012.json", stop.Message);
        }

        [Fact]
        public void R378_floors_take_the_plan_its_level_profile_and_the_last_record_of_5()
        {
            File.WriteAllText(Path.Combine(work, FloorsRun.RecordName("012")), new JObject { ["placed"] = new JArray("101|generic") }.ToString());

            FloorInputs inputs = FloorLoad.Floors(Context(5), Nbh);

            Assert.Equal("parsons2024Rev6", inputs.Profile);
            Assert.Contains("101|generic", inputs.LastPlaced);
            Assert.Null(inputs.InScope);
            var none = Assert.Throws<InvalidDataException>(() => FloorLoad.Floors(Context(5), new SettingsScope { Kind = ScopeKinds.Project }));
            Assert.Contains("works on one NBH", none.Message);
        }
    }
}
