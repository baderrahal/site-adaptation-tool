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
        public void R459_floors_take_their_known_warnings_from_the_config_the_build_read_and_no_block_gives_none()
        {
            Assert.Null(FloorLoad.Floors(Context(5), Nbh).KnownWarnings);

            string config = Path.Combine(work, "config", "project.json");
            JObject settings = JObject.Parse(File.ReadAllText(config));
            settings["floors"] = new JObject
            {
                ["plotParameter"] = "Plot", ["types"] = new JObject { ["generic"] = new JArray("G"), ["yard"] = new JArray("Y") },
                ["knownWarnings"] = new JObject { ["entries"] = new JArray(new JObject { ["text"] = "Overlap.", ["samePlot"] = true, ["rolePairs"] = JArray.Parse("[[\"generic\", \"yard\"]]") }) },
            };
            File.WriteAllText(config, settings.ToString());
            Assert.True(FloorLoad.Floors(Context(5), Nbh).KnownWarnings.Knows("Overlap."));

            settings["floors"]["knownWarnings"] = new JObject { ["entries"] = 1 };
            File.WriteAllText(config, settings.ToString());
            Assert.Contains("floors.knownWarnings", Assert.Throws<InvalidDataException>(() => FloorLoad.Floors(Context(5), Nbh)).Message);

            File.Delete(config);
            Assert.Null(FloorLoad.Floors(Context(5), Nbh).KnownWarnings);
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

        // A LandXML of one triangle and the config that names it, in the request of the build as the road surface of the role
        private string RoadSurfaceInRequest(bool inRequest = true)
        {
            string xml = Write(Path.Combine(work, "Road.xml"), "<LandXML><Units><Metric linearUnit=\"meter\" /></Units><Surfaces><Surface name=\"Road\"><Definition><Pnts><P id=\"1\">0 0 1</P><P id=\"2\">0 10 1</P><P id=\"3\">10 10 1</P></Pnts><Faces><F>1 2 3</F></Faces></Definition></Surface></Surfaces></LandXML>");
            string config = Path.Combine(work, "config", "project.json");
            JObject read = JObject.Parse(File.ReadAllText(config));
            read["siteBuild"] = new JObject { ["snapM"] = 0.005 };
            read["floors"] = new JObject { ["roadTopography"] = new JObject { ["typeName"] = "Road surface", ["surface"] = "Road", ["marginM"] = 5.0, ["limitMm"] = 5.0, ["minCoverShare"] = 0.5, ["pointGapM"] = 0.01, ["maxPoints"] = 100 } };
            File.WriteAllText(config, read.ToString());
            if (inRequest)
            {
                WorkerRequest request = WorkerRequest.Read(WorkerFiles.RequestPath(folder, RunId));
                request.Inputs = request.Inputs.Concat(new[] { new WorkerFile { Role = WorkerRunner.RoadSurfaceRole + "Road", Path = xml, Sha256 = FileHash.Sha256(xml) } }).ToList();
                request.Write(folder);
            }
            return xml;
        }

        [Fact]
        public void R572_floors_take_the_surface_the_build_read_with_its_mark_only_with_the_tick_on()
        {
            string xml = RoadSurfaceInRequest();

            Assert.Null(FloorLoad.Floors(Context(5), Nbh).RoadTopography);
            RoadSurfaceRead read = FloorLoad.Floors(Context(5), Nbh, true).RoadTopography();

            Assert.Equal("Road surface", read.Settings.TypeName);
            Assert.Equal(1, read.Surface.VisibleFaceCount);
            Assert.Equal("Road.xml", read.Mark.Name);
            Assert.Equal(FileHash.Sha256(xml), read.Mark.Sha256);
        }

        [Fact]
        public void R572_a_surface_the_build_did_not_read_or_one_changed_since_is_refused_by_name()
        {
            RoadSurfaceInRequest(false);
            Assert.Contains("read no road surface Road", Assert.Throws<InvalidDataException>(() => FloorLoad.Floors(Context(5), Nbh, true).RoadTopography()).Message);

            File.AppendAllText(RoadSurfaceInRequest(), " ");
            Assert.Contains("changed since the run of 4 Check", Assert.Throws<InvalidDataException>(() => FloorLoad.Floors(Context(5), Nbh, true).RoadTopography()).Message);
        }

        [Fact]
        public void R572_a_config_with_no_block_is_refused_by_the_name_of_the_key()
        {
            Assert.Contains("floors.roadTopography.typeName", Assert.Throws<InvalidDataException>(() => FloorLoad.Floors(Context(5), Nbh, true).RoadTopography()).Message);
        }
    }
}
