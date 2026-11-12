using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R490, R491, R492, R493. Road surfaces named in order, each corner on the first that covers it, the pick of Set up and the stop on plots with no
    // road level, all made up in code. python/tests/test_r490_road_surfaces.py holds the same made up names and the same lines
    public sealed class R490_RoadSurfacesTests : IDisposable
    {
        private const string First = "Made Up Job__Road A", Second = "Made Up Job__Road B", Third = "Made Up Job__Road C";
        private const string Stop = "Zone 2: 3 of 5 plots have no road level on the road surfaces named (Road A, Road B). What to do: add the road surface of this zone in Set up. (R493)";

        private readonly string root = Path.Combine(Path.GetTempPath(), "r490-" + Guid.NewGuid().ToString("N"));

        public R490_RoadSurfacesTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static string Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        // config/project.json under rev6 with road in designBasis.roadLevel in place of its surfaces and its limit
        private static JObject RoadConfig(JObject road)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            JObject block = (JObject)config["designBasis"]["roadLevel"];
            block.Remove("surfaces");
            block.Remove("noRoadStopShare");
            block.Merge(road);
            return config;
        }

        private static DesignBasis Road(JObject road)
        {
            return DesignBasis.Of(RoadConfig(road), () => null);
        }

        private static string Refused(JObject road)
        {
            return Assert.Throws<InvalidDataException>(() => Road(road)).Message;
        }

        private static string PastMode()
        {
            return ((JArray)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["designBasis"]["modes"]).Select(mode => (string)mode).Single(mode => mode != DesignBasis.Rev6);
        }

        private static Dictionary<string, Dictionary<string, string>> Rows(params string[] zones)
        {
            return zones.Select((zone, at) => new { zone, at }).ToDictionary(item => (item.at + 1).ToString(CultureInfo.InvariantCulture),
                item => new Dictionary<string, string> { ["zone"] = item.zone });
        }

        private static DesignBasis StopBasis(double share, params string[] surfaces)
        {
            return Road(new JObject { ["surfaces"] = new JArray(surfaces.Cast<object>().ToArray()), ["noRoadStopShare"] = share });
        }

        // R490

        [Fact]
        public void R490_the_surfaces_of_design_basis_road_level_are_read_in_their_order_and_a_build_takes_them()
        {
            DesignBasis basis = Road(new JObject { ["surfaces"] = new JArray(Second, First) });
            Assert.Equal(new[] { Second, First }, basis.Surfaces);
            JObject profile = BuildRules.LevelProfile(JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules())), null);
            Assert.Equal(new[] { Second, First }, Fronts.RoadNames(profile, ReadSettings.Load(WorkerRunTests.RepoConfig()), basis));
        }

        [Fact]
        public void R490_an_old_config_with_one_surface_reads_as_a_list_of_one()
        {
            Assert.Equal(new[] { First }, Road(new JObject { ["surface"] = First }).Surfaces);
        }

        [Fact]
        public void R490_both_keys_no_value_and_a_bad_list_are_refused_by_name_with_the_lines_of_python()
        {
            Assert.Equal(DesignBasis.BothSurfaces, Refused(new JObject { ["surface"] = First, ["surfaces"] = new JArray(First) }));
            Assert.Equal(DesignBasis.NoSurfaces, Refused(new JObject()));
            Assert.Equal(DesignBasis.NoSurfaces, Refused(new JObject { ["surface"] = "" }));
            foreach (JToken bad in new JToken[] { "one text", new JArray(), new JArray(""), new JArray(First, First), new JArray(First, 1), JValue.CreateNull(), new JObject() })
            {
                Assert.Equal(DesignBasis.BadSurfaces, Refused(new JObject { ["surfaces"] = bad }));
            }
            string python = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "python", "design_basis.py"));
            foreach (string line in new[] { DesignBasis.BothSurfaces, DesignBasis.NoSurfaces, DesignBasis.BadSurfaces })
            {
                Assert.Contains("\"" + line + "\"", python);
            }
        }

        [Fact]
        public void R490_the_past_job_reads_none_of_it()
        {
            JObject config = RoadConfig(new JObject { ["surface"] = 5, ["surfaces"] = "broken", ["noRoadStopShare"] = "broken" });
            config["designBasis"]["mode"] = PastMode();
            DesignBasis basis = DesignBasis.Of(config, () => null);
            Assert.Null(basis.Surfaces);
            JObject profile = BuildRules.LevelProfile(JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules())), null);
            ReadSettings read = ReadSettings.Load(WorkerRunTests.RepoConfig());
            Assert.Equal(read.RoadSurfaces, Fronts.RoadNames(profile, read, basis));
            Assert.Equal(read.RoadSurfaces, read.ReadRoads(new[] { First }));
        }

        [Fact]
        public void R490_the_config_of_the_repo_names_the_six_of_site_build_in_their_order_and_the_limit_with_its_why()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            JObject road = (JObject)config["designBasis"]["roadLevel"];
            Assert.Equal(config["siteBuild"]["roadSurfaces"], road["surfaces"], JToken.EqualityComparer);
            Assert.Equal(6, ((JArray)road["surfaces"]).Count);
            Assert.Null(road.Property("surface"));
            Assert.Equal(0.1, (double)road["noRoadStopShare"]);
            Assert.Contains("missing surface", (string)road["noRoadStopWhy"]);
        }

        // R491, the worker build on made up LandXML files, no new loop

        private static WorkerRequest Surfaced(LevelFloorStepTests steps, string runId, IEnumerable<Tuple<string, double, double, double>> surfaces, Action<JObject> change, params string[] names)
        {
            WorkerRequest request = steps.Request(runId, names);
            WorkerFile settings = request.Inputs.Single(input => input.Role == "config");
            JObject config = JObject.Parse(File.ReadAllText(settings.Path));
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["designBasis"]["roadLevel"]["surfaces"] = new JArray(surfaces.Select(surface => (object)surface.Item1).ToArray());
            change?.Invoke(config);
            File.WriteAllText(settings.Path, config.ToString());
            settings.Sha256 = FileHash.Sha256(settings.Path);
            request.Inputs.RemoveAll(input => input.Role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal));
            foreach (Tuple<string, double, double, double> surface in surfaces)
            {
                string path = steps.Surface(surface.Item1, surface.Item2, -20.0, surface.Item3, 30.0, surface.Item4);
                request.Inputs.Add(new WorkerFile { Role = WorkerRunner.RoadSurfaceRole + surface.Item1, Path = path, Sha256 = FileHash.Sha256(path) });
            }
            string floors = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json");
            request.Inputs.Add(new WorkerFile { Role = BuildSteps.BandsRole, Path = floors, Sha256 = FileHash.Sha256(floors) });
            return request;
        }

        private static int RunWorker(WorkerRequest request)
        {
            return WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0);
        }

        private static JToken FrontOf(WorkerRequest request, string plot)
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(request.StepsFolder, "step_fronts_csharp.json")))["plots"][plot]["raw"];
        }

        [Fact]
        public void R491_a_plot_takes_each_front_corner_from_the_surface_under_it_and_the_surfaces_name_both()
        {
            using (var steps = new LevelFloorStepTests())
            {
                // Plot 3 runs from x 24 to x 36, the two surfaces meet at x 30, so one front corner lies on each
                WorkerRequest request = Surfaced(steps, "t1", new[] { Tuple.Create(First, -60.0, 30.0, 10.0), Tuple.Create(Second, 30.0, 200.0, 20.0) }, null, "fronts");
                Assert.Equal(0, RunWorker(request));
                JToken front = FrontOf(request, "3");
                var taken = new Dictionary<string, double> { [(string)front["surfaceLeft"]] = (double)front["roadLevelLeft"], [(string)front["surfaceRight"]] = (double)front["roadLevelRight"] };
                Assert.Equal(new Dictionary<string, double> { ["Road A"] = 10.0, ["Road B"] = 20.0 }, taken);
                // The plan of the same surfaces names both for the plot
                WorkerRequest planned = PlanRequest(steps, "t1p", new[] { Tuple.Create(First, -60.0, 30.0, 10.0), Tuple.Create(Second, 30.0, 200.0, 20.0) }, null);
                Assert.True(RunWorker(planned) == 0, Answer(planned).Message);
                JObject plan = JObject.Parse(File.ReadAllText(Path.Combine(planned.OutputFolder, BuildPlan.FileName(null))));
                JToken plot = plan["plots"].Single(item => (string)item["plot"] == "3");
                Assert.Equal(new[] { "Road A", "Road B" }, plot["roadSurfaces"].Select(name => (string)name).OrderBy(name => name, StringComparer.Ordinal));
            }
        }

        [Fact]
        public void R491_a_corner_under_both_takes_the_first_of_the_list_and_swapping_the_order_swaps_it()
        {
            using (var steps = new LevelFloorStepTests())
            {
                Tuple<string, double, double, double> a = Tuple.Create(First, -60.0, 200.0, 10.0), b = Tuple.Create(Second, -60.0, 200.0, 20.0);
                WorkerRequest first = Surfaced(steps, "t2", new[] { a, b }, null, "fronts");
                Assert.Equal(0, RunWorker(first));
                Assert.Equal("Road A", (string)FrontOf(first, "4")["surfaceLeft"]);
                Assert.Equal(10.0, (double)FrontOf(first, "4")["roadLevelRight"]);
                WorkerRequest swapped = Surfaced(steps, "t3", new[] { b, a }, null, "fronts");
                Assert.Equal(0, RunWorker(swapped));
                Assert.Equal("Road B", (string)FrontOf(swapped, "4")["surfaceLeft"]);
                Assert.Equal(20.0, (double)FrontOf(swapped, "4")["roadLevelRight"]);
            }
        }

        [Fact]
        public void R491_a_corner_under_none_stays_no_road_level()
        {
            using (var steps = new LevelFloorStepTests())
            {
                // Plot 6 runs from x 60 to x 72, the surfaces end at x 66
                WorkerRequest request = Surfaced(steps, "t4", new[] { Tuple.Create(First, -60.0, 30.0, 10.0), Tuple.Create(Second, 30.0, 66.0, 20.0) }, null, "fronts");
                Assert.Equal(0, RunWorker(request));
                JToken listed = FrontOf(request, "6")["noRoadLevel"];
                Assert.Equal("6", (string)listed["plot"]);
                Assert.Equal(new[] { "Road A", "Road B" }, listed["tried"].Select(name => (string)name));
                Assert.Single((JArray)listed["corners"]);
            }
        }

        // R492, the pick of Set up

        [Fact]
        public void R492_the_pick_of_the_request_replaces_the_list_under_rev6_and_the_past_job_ignores_it()
        {
            using (var steps = new LevelFloorStepTests())
            {
                Tuple<string, double, double, double> a = Tuple.Create(First, -60.0, 200.0, 10.0), b = Tuple.Create(Second, -60.0, 200.0, 20.0);
                WorkerRequest picked = Surfaced(steps, "p1", new[] { a, b }, null, "fronts");
                picked.RoadSurfaces = new List<string> { Second, First };
                Assert.Equal(0, RunWorker(picked));
                Assert.Equal("Road B", (string)FrontOf(picked, "4")["surfaceLeft"]);
                // A pick that is no list of the surfaces of the request is refused by name
                WorkerRequest bad = Surfaced(steps, "p2", new[] { a, b }, null, "fronts");
                bad.RoadSurfaces = new List<string> { First, First };
                Assert.Equal(WorkerExit.Refused, RunWorker(bad));
                Assert.Equal(DesignBasis.BadPick, WorkerAnswer.Read(WorkerFiles.AnswerPath(bad.OutputFolder, "p2")).Message);

                // The past job: the request with a pick gives the same step as the request with none
                WorkerRequest plain = steps.Request("p3", "fronts");
                Assert.Equal(0, RunWorker(plain));
                string before = JObject.Parse(File.ReadAllText(Path.Combine(plain.StepsFolder, "step_fronts_csharp.json")))["plots"].ToString();
                WorkerRequest past = steps.Request("p4", "fronts");
                past.RoadSurfaces = new List<string> { Second };
                Assert.Equal(0, RunWorker(past));
                Assert.Equal(before, JObject.Parse(File.ReadAllText(Path.Combine(past.StepsFolder, "step_fronts_csharp.json")))["plots"].ToString());
            }
        }

        [Fact]
        public void R492_a_request_with_no_pick_leaves_the_field_out_of_its_json_and_with_one_holds_it_in_order()
        {
            var request = new WorkerRequest { FormatVersion = 1, RunId = "j1", Command = "build", Inputs = new List<WorkerFile>() };
            Assert.Null(JObject.Parse(request.ToJson())["roadSurfaces"]);
            Assert.DoesNotContain("roadSurfaces", request.ToJson());
            request.RoadSurfaces = new List<string> { Second, First };
            Assert.Equal(new[] { Second, First }, JObject.Parse(request.ToJson())["roadSurfaces"].Select(name => (string)name));
            string path = Write(Path.Combine(root, "request.json"), request.ToJson());
            Assert.Equal(new[] { Second, First }, WorkerRequest.Read(path).RoadSurfaces);
        }

        [Fact]
        public void R492_the_settings_key_is_left_out_while_null_stays_format_version_1_and_is_checked_as_its_other_fields()
        {
            string none = SettingsJson.Write(new ProjectSettings());
            Assert.DoesNotContain("roadSurfaces", none);
            Assert.Equal(1, (int)JObject.Parse(none)["formatVersion"]);
            Assert.Null(SettingsJson.Read(none).RoadSurfaces);
            string text = SettingsJson.Write(new ProjectSettings { RoadSurfaces = new List<string> { Second, First } });
            Assert.Equal(1, (int)JObject.Parse(text)["formatVersion"]);
            Assert.Equal(new[] { Second, First }, SettingsJson.Read(text).RoadSurfaces);
            foreach (string bad in new[] { "[]", "[\"\"]", "[null]", "[\"a\", \"a\"]", "[1]", "[true]", "\"a\"" })
            {
                Assert.Throws<InvalidDataException>(() => SettingsJson.Read("{ \"formatVersion\": 1, \"roadSurfaces\": " + bad + " }"));
            }
        }

        [Fact]
        public void R492_a_pre_setting_carries_the_pick_and_save_setup_applies_it_keeping_every_other_value()
        {
            var job = new ProjectSettings { RoadSurfaces = new List<string> { Second }, LevelProfile = "made up", DoorLevelSource = DoorLevelSources.CivilFile };
            string exported = PreSetting.Export(job);
            Assert.Equal(new[] { Second }, SettingsJson.Read(exported).RoadSurfaces);
            var into = new ProjectSettings { RoadSurfaces = new List<string> { First } };
            PreSetting.Import(exported, into);
            Assert.Equal(new[] { Second }, into.RoadSurfaces);

            ProjectSettings saved = SetupSave.Apply(new SetupDraft { RoadSurfaces = new[] { Second, First } }, job);
            Assert.Equal(new[] { Second, First }, saved.RoadSurfaces);
            Assert.Equal("made up", saved.LevelProfile);
            Assert.Equal(DoorLevelSources.CivilFile, saved.DoorLevelSource);
            Assert.Equal(new[] { Second }, SetupSave.Apply(new SetupDraft(), job).RoadSurfaces);
            Assert.Equal(new[] { Second }, job.RoadSurfaces);
        }

        [Fact]
        public void R492_road_surface_choices_lists_the_folder_the_effective_order_and_the_mode()
        {
            string folder = Path.Combine(root, "roads");
            Write(Path.Combine(folder, "b road.xml"), "x");
            Write(Path.Combine(folder, "A road.xml"), "x");
            Write(Path.Combine(folder, "other.dwg"), "x");
            Write(Path.Combine(folder, "other.xmlx"), "x");
            Assert.Equal(new[] { "A road", "b road" }, RoadSurfaceChoices.InFolder(folder, null));
            Assert.Empty(RoadSurfaceChoices.InFolder(null, null));
            Assert.Empty(RoadSurfaceChoices.InFolder(Path.Combine(root, "none"), null));
            // A name siteBuild.roadFolder.notTaken refuses is not offered, its reason holds the whole name
            JObject notTaken = JObject.Parse("{ 'siteBuild': { 'roadFolder': { 'notTaken': { 'ground': 'A road and more are ground' } } } }");
            Assert.Equal(new[] { "b road" }, RoadSurfaceChoices.InFolder(folder, notTaken));
            Assert.Equal("A road and more are ground", RoadSurfaceChoices.NotTakenWhy(notTaken, "Job__A road"));
            Assert.Null(RoadSurfaceChoices.NotTakenWhy(notTaken, "Job__A roads"));
            Assert.Null(RoadSurfaceChoices.NotTakenWhy(null, "Job__A road"));

            JObject list = JObject.Parse("{ 'designBasis': { 'mode': 'rev6', 'roadLevel': { 'surfaces': ['x', 'y'] } } }");
            JObject one = JObject.Parse("{ 'designBasis': { 'mode': 'other', 'roadLevel': { 'surface': 'x' } } }");
            JObject neither = JObject.Parse("{ 'designBasis': 5 }");
            Assert.Equal(new[] { "x", "y" }, RoadSurfaceChoices.Named(list, null));
            Assert.Equal(new[] { "y" }, RoadSurfaceChoices.Named(list, new[] { "y" }));
            Assert.Equal(new[] { "x" }, RoadSurfaceChoices.Named(one, null));
            Assert.Empty(RoadSurfaceChoices.Named(neither, null));
            Assert.True(RoadSurfaceChoices.IsRev6(list));
            Assert.False(RoadSurfaceChoices.IsRev6(one));
            Assert.False(RoadSurfaceChoices.IsRev6(neither));
            Assert.Null(RoadSurfaceChoices.ForRequest(one, new[] { "y" }));
            Assert.Null(RoadSurfaceChoices.ForRequest(list, null));
            Assert.Equal(new[] { "y" }, RoadSurfaceChoices.ForRequest(list, new[] { "y" }));
        }

        // The made up inputs of button 2 as R473_ReadPicksTests makes them, on a config under the mode with the surfaces, and three files in the road folder
        private List<RunInput> ReadPane(string work, string mode, params string[] surfaces)
        {
            string input = Path.Combine(root, "input");
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = input;
            config["designBasis"]["mode"] = mode;
            config["designBasis"]["roadLevel"]["surfaces"] = new JArray(surfaces.Cast<object>().ToArray());
            string configPath = Write(Path.Combine(root, "config", "project.json"), config.ToString());
            Write(Path.Combine(root, "config", "cad_layers.json"), "{}");
            var inputs = new List<RunInput>
            {
                new RunInput("Settings", configPath, false),
                new RunInput("Baseplan", Write(Path.Combine(input, "baseplan.dwg"), "a made up baseplan"), false),
                new RunInput("Villa blocks", Write(Path.Combine(input, "blocks.dwg"), "made up blocks"), false),
                new RunInput("Curbs", Write(Path.Combine(input, "curbs.dwg"), "made up curbs"), false),
                new RunInput("DLUS schedule", Write(Path.Combine(input, "dlus.xlsx"), "a made up DLUS"), false),
                new RunInput("Sales schedule", Write(Path.Combine(input, "sales.xlsx"), "a made up sales"), false),
                new RunInput("Plot numbers", Write(Path.Combine(input, "plan.pdf"), "a made up page"), false),
            };
            string roads = Path.Combine(input, "roads");
            foreach (string name in new[] { First, Second, Third }.Concat(((JArray)config["siteBuild"]["roadSurfaces"]).Select(item => (string)item)))
            {
                Write(Path.Combine(roads, name + ".xml"), "<LandXML/>");
            }
            inputs.Add(new RunInput("Road surface", roads, true));
            foreach (JProperty hood in ((JObject)config["connect"]["neighbourhoods"]).Properties().Where(hood => hood.Value.Type == JTokenType.Object))
            {
                Write(Path.Combine(input, "01_Output", "1.11 References", (string)hood.Value["schedule"]), "a made up schedule of " + hood.Name);
            }
            string book = Path.Combine(root, "SiteAdaptation", "data_book");
            Write(Path.Combine(book, "plots.csv"), "label\n1\n");
            Write(Path.Combine(book, "location_plan", "labels.csv"), "number\n2301\n");
            Write(Path.Combine(book, "phase1_border.csv"), "x,y\n0,0\n");
            Directory.CreateDirectory(work);
            // R840, R847. The read for the build takes its plots from the read of the folder, here a made up one with one made up plot
            string read = Path.Combine(work, "read_made");
            Write(Path.Combine(read, ReadDataRun.PlotTableFile), "{\"plots\":[{\"baseplanLabel\":\"MU-1\",\"officialNumber\":\"9001\",\"nbhId\":\"Z9\"}]}");
            Write(Path.Combine(read, ReadDataRun.RecordFile), new JObject { ["format"] = ReadDataRun.RecordFormat, ["runId"] = "2_20200101-000000-000", ["inputs"] = new JArray(), ["outputs"] = new JArray() }.ToString());
            return inputs;
        }

        private static readonly Dictionary<string, string> ReadRoles = new Dictionary<string, string>
        {
            ["Settings"] = "config", ["Baseplan"] = "baseplan", ["Villa blocks"] = "villaBlocks", ["Curbs"] = "curbs", ["Road surface"] = "roadSurface",
            ["DLUS schedule"] = "dlus", ["Sales schedule"] = "sales", ["Plot numbers"] = "plotNumbers",
        };

        private static List<string> RoadRoles(IEnumerable<string> roles)
        {
            return roles.Where(role => role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal)).Select(role => role.Substring(WorkerRunner.RoadSurfaceRole.Length)).ToList();
        }

        [Fact]
        public void R492_read_inputs_under_rev6_are_the_effective_list_a_name_not_in_the_folder_stops_and_the_past_job_stays_on_site_build()
        {
            string work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            List<RunInput> pane = ReadPane(work, DesignBasis.Rev6, First, Second);
            Assert.Equal(new[] { First, Second }, RoadRoles(ReadInputs.ForBuild(pane, ReadRoles, work).Inputs.Select(input => input.Role)));
            Assert.Equal(new[] { Third, First }, RoadRoles(ReadInputs.ForBuild(pane, ReadRoles, work, new[] { Third, First }).Inputs.Select(input => input.Role)));
            ReadPlan missing = ReadInputs.ForBuild(pane, ReadRoles, work, new[] { "Made Up Job__Road Z" });
            Assert.Contains("Road surface: Made Up Job__Road Z.xml of the road surfaces named is not in the folder ", Assert.Single(missing.Stops));
            Assert.Equal("Road surface: config names no road surface and Set up picked none (R490)", Assert.Single(ReadInputs.ForBuild(ReadPane(work, DesignBasis.Rev6), ReadRoles, work).Stops));

            List<RunInput> past = ReadPane(work, PastMode(), First);
            JObject config = JObject.Parse(File.ReadAllText(past[0].Path));
            List<string> site = ((JArray)config["siteBuild"]["roadSurfaces"]).Select(name => (string)name).ToList();
            Assert.Equal(site, RoadRoles(ReadInputs.ForBuild(past, ReadRoles, work, new[] { Third }).Inputs.Select(input => input.Role)));
        }

        private sealed class HeldWorker : IWorkerStart
        {
            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                ended(new WorkerAnswer { Status = WorkerExit.Status(WorkerExit.Failed), Message = "the stand-in reads nothing" });
            }
        }

        // R484, R847. The read of 2 Read data under the mode of the settings on a config of the past job: the mode in the request of the folder read

        [Fact]
        public void R484_the_read_run_takes_the_mode_of_the_settings_on_a_past_job_config_and_a_project_with_none_reads_the_config()
        {
            string work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            WorkerRequest Started(string mode)
            {
                List<RunInput> pane = ReadPane(work, PastMode(), First, Second);
                var worker = new HeldWorker();
                var run = new ReadDataRun(ReadRoles, worker, new WorkerDocument { Path = @"C:\Models\site.rvt", SessionId = "s1" }, () => new SettingsScope(), () => new ProjectSettings()) { ModePick = () => mode };
                run.Body().Start(new RunContext(2, "2 Read data", "2_20261103-000001-001", work, null, true, pane, new Dictionary<string, string>()), later => { });
                return Assert.Single(worker.Requests);
            }

            WorkerRequest rev6 = Started(DesignBasis.Rev6);
            Assert.Equal(DesignBasis.Rev6, rev6.ProjectMode);
            Assert.Equal(ReadDataRun.DataCheckCommand, rev6.Command);
            Assert.Empty(RoadRoles(rev6.Inputs.Select(input => input.Role)));
            WorkerRequest none = Started(null);
            Assert.DoesNotContain("projectMode", none.ToJson());
            Assert.Equal(ReadDataRun.DataCheckCommand, none.Command);
        }

        [Fact]
        public void R484_a_change_of_mode_gives_a_new_read_folder_as_a_change_of_order_does()
        {
            string work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            List<RunInput> pane = ReadPane(work, PastMode(), First, Second);
            Assert.NotEqual(ReadInputs.ForBuild(pane, ReadRoles, work).Hash, ReadInputs.ForBuild(pane, ReadRoles, work, null, DesignBasis.Rev6).Hash);
            Assert.Equal(ReadInputs.ForBuild(pane, ReadRoles, work).Hash, ReadInputs.ForBuild(pane, ReadRoles, work, null, PastMode()).Hash);
        }

        [Fact]
        public void R484_read_inputs_stop_on_a_mode_outside_the_modes_by_name()
        {
            string work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            Assert.StartsWith("the project mode manual7 is no mode of designBasis.modes", Assert.Single(ReadInputs.ForBuild(ReadPane(work, PastMode(), First), ReadRoles, work, null, "manual7").Stops));
        }

        // 4 Check on made up files as R376_CheckRunTests makes them, the worker a stand-in, the read holding the three made up surfaces

        private sealed class Builder : IWorkerStart
        {
            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();

            public WorkerAnswer Answer { get; set; }

            public WorkerAnswer Run(WorkerRequest request)
            {
                Requests.Add(request);
                Directory.CreateDirectory(request.StepsFolder);
                File.WriteAllText(Path.Combine(request.StepsFolder, StepFile.FileName(CheckRows.StepName, "csharp")), R376_CheckTableTests.Step().ToString());
                return Answer ?? new WorkerAnswer { RunId = request.RunId, Status = WorkerExit.Status(WorkerExit.Done), Outputs = new List<WorkerFile>() };
            }

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                ended(Run(request));
            }
        }

        private sealed class CheckFiles
        {
            public string Work;
            public string Config;
            public string Prototypes;
        }

        private static readonly Dictionary<string, string> CheckRoles = new Dictionary<string, string> { ["Settings"] = CheckRun.Config, ["Prototypes"] = CheckRun.Prototypes };

        private CheckFiles CheckSite(string mode, params string[] named)
        {
            var files = new CheckFiles { Work = Path.Combine(root, "work"), Config = Path.Combine(root, "repo", "config", "project.json"), Prototypes = Path.Combine(root, "prototypes") };
            string config = Path.GetDirectoryName(files.Config);
            var settings = new JObject
            {
                ["profile"] = "rules/made-up.json",
                ["majority"] = JObject.Parse("{ 'groups': { 'native': ['n1'], 'parsons': ['kx'] }, 'overrideDefaultSources': ['parsons'], 'linkPlotParameter': 'Plot' }"),
                ["addin"] = JObject.Parse("{ 'workerTimeLimitS': { 'build': 60 } }"),
                ["checkRows"] = R376_CheckTableTests.Block(),
                ["designBasis"] = new JObject { ["mode"] = mode, ["modes"] = new JArray(DesignBasis.Rev6, "pastMade"), ["roadLevel"] = new JObject { ["surfaces"] = new JArray(named.Cast<object>().ToArray()) } },
            };
            Write(files.Config, settings.ToString());
            Write(Path.Combine(config, "floor_outlines.json"), "{}");
            Write(Path.Combine(config, "prototype_offsets.json"), "{}");
            Write(Path.Combine(config, "overrides", "made-up.json"), "{ \"note\": \"made up\", \"overrides\": [ {\"plot\": \"1\", \"field\": \"heightM\", \"value\": {\"gapToRuleM\": 0.05}, \"source\": \"parsons\", \"model\": \"kx-site.rvt\", \"key\": \"kx\"} ] }");
            Write(Path.Combine(root, "repo", "rules", "made-up.json"), "{ 'levelProfiles': { 'default': 'p', 'p': { 'job': 'parsons', 'frontRoad': 1, 'rampSlopePercent': 1, 'rampTypes': {}, 'height': 1 } } }");
            Write(Path.Combine(files.Prototypes, "P-ARC-VL4-A.rvt"), "made up");
            Write(Path.Combine(files.Work, "site.json"), "{}");
            string plots = Write(Path.Combine(root, "plots.csv"), "locationPlanNumber\n1\n"), labels = Write(Path.Combine(root, "labels.csv"), "label\n");
            string read = Path.Combine(files.Work, ReadDataRun.FolderPrefix + "0123");
            foreach (string name in CheckRun.ReadFiles.Values.Concat(new[] { ReadDataRun.PlotTableFile }))
            {
                Write(Path.Combine(read, name), "{ 'plots': [] }");
            }
            var inputs = new List<Tuple<string, string>> { Tuple.Create("config", files.Config), Tuple.Create("plotsCsv", plots), Tuple.Create("labels", labels) };
            foreach (string name in new[] { First, Second, Third })
            {
                inputs.Add(Tuple.Create(WorkerRunner.RoadSurfaceRole + name, Write(Path.Combine(root, "roads", name + ".xml"), "<LandXML/>")));
            }
            var record = new JObject
            {
                ["format"] = ReadDataRun.RecordFormat, ["runId"] = "R2-1",
                ["inputs"] = new JArray(inputs.Select(item => new JObject { ["role"] = item.Item1, ["path"] = item.Item2, ["sha256"] = FileHash.Sha256(item.Item2) })),
            };
            Write(Path.Combine(read, ReadDataRun.RecordFile), record.ToString());
            return files;
        }

        private static RunContext CheckContext(CheckFiles files, string runId, string dry = null)
        {
            var inputs = new[] { new RunInput("Settings", files.Config, false), new RunInput("Prototypes", files.Prototypes, true) };
            return new RunContext(4, "4 Check", runId, files.Work, dry, true, inputs, new Dictionary<string, string>());
        }

        private static SettingsScope Nbh()
        {
            return new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" };
        }

        private sealed class NoTable : ICheckTableDialog
        {
            public CheckTableAnswer Show(CheckTableView view, CheckTableCallbacks callbacks)
            {
                return new CheckTableAnswer();
            }
        }

        private static CheckRun Check(Builder builder, IReadOnlyList<string> pick, bool dry = false)
        {
            return new CheckRun(CheckRoles, builder, dry ? (path, folder, request) => builder.Run(request) : (Func<string, string, WorkerRequest, WorkerAnswer>)null, new WorkerDocument(), Nbh, new NoTable(), null, null)
            {
                RoadPick = () => pick,
            };
        }

        [Fact]
        public void R492_check_under_rev6_passes_only_the_road_roles_of_the_list_in_its_order_and_the_request_names_no_pick()
        {
            CheckFiles files = CheckSite(DesignBasis.Rev6, Second, First);
            var builder = new Builder();
            RunLater end = null;
            ButtonBody body = Check(builder, null).Body();
            body.Start(CheckContext(files, "R4-1"), later => end = later);
            Assert.Null(end.Failure);
            WorkerRequest request = Assert.Single(builder.Requests);
            Assert.Equal(new[] { Second, First }, RoadRoles(request.Inputs.Select(input => input.Role)));
            Assert.Null(request.RoadSurfaces);
            Assert.DoesNotContain("roadSurfaces", request.ToJson());
        }

        [Fact]
        public void R492_check_with_a_pick_takes_its_order_though_the_read_held_another_and_names_it_in_the_request()
        {
            CheckFiles files = CheckSite(DesignBasis.Rev6, First, Second);
            var builder = new Builder();
            RunLater end = null;
            Check(builder, new[] { Third, First }).Body().Start(CheckContext(files, "R4-2"), later => end = later);
            Assert.Null(end.Failure);
            WorkerRequest request = Assert.Single(builder.Requests);
            Assert.Equal(new[] { Third, First }, RoadRoles(request.Inputs.Select(input => input.Role)));
            Assert.Equal(new[] { Third, First }, request.RoadSurfaces);
        }

        [Fact]
        public void R484_check_takes_the_mode_of_the_settings_on_a_past_job_config_and_names_it_in_the_request()
        {
            CheckFiles files = CheckSite("pastMade", Second, First);
            var builder = new Builder();
            RunLater end = null;
            CheckRun run = Check(builder, new[] { Third, First });
            run.ModePick = () => DesignBasis.Rev6;
            run.Body().Start(CheckContext(files, "R4-m1"), later => end = later);
            Assert.Null(end.Failure);
            WorkerRequest request = Assert.Single(builder.Requests);
            Assert.Equal(DesignBasis.Rev6, request.ProjectMode);
            Assert.Equal(new[] { Third, First }, request.RoadSurfaces);
            Assert.Equal(new[] { Third, First }, RoadRoles(request.Inputs.Select(input => input.Role)));
        }

        [Fact]
        public void R484_check_under_the_past_job_mode_on_a_rev6_config_takes_every_road_role_and_no_pick()
        {
            CheckFiles files = CheckSite(DesignBasis.Rev6, Second);
            var builder = new Builder();
            RunLater end = null;
            CheckRun run = Check(builder, new[] { Third });
            run.ModePick = () => "pastMade";
            run.Body().Start(CheckContext(files, "R4-m2"), later => end = later);
            Assert.Null(end.Failure);
            WorkerRequest request = Assert.Single(builder.Requests);
            Assert.Equal("pastMade", request.ProjectMode);
            Assert.Null(request.RoadSurfaces);
            Assert.Equal(new[] { First, Second, Third }, RoadRoles(request.Inputs.Select(input => input.Role)));
        }

        [Fact]
        public void R484_check_stops_on_a_mode_outside_the_modes_by_name()
        {
            CheckFiles files = CheckSite("pastMade", First);
            CheckRun run = Check(new Builder(), null);
            run.ModePick = () => "manual7";
            Assert.StartsWith("the project mode manual7 is no mode of designBasis.modes", Assert.Single(run.Plan(CheckContext(files, "R4-m3")).Stops));
        }

        [Fact]
        public void R492_check_stops_on_a_name_of_the_list_the_read_does_not_hold()
        {
            CheckFiles files = CheckSite(DesignBasis.Rev6, First);
            var builder = new Builder();
            InvalidDataException stop = Assert.Throws<InvalidDataException>(() => Check(builder, new[] { First, "Made Up Job__Road Z" }).Body().Preview(CheckContext(files, "R4-3")));
            Assert.Equal("2 Read data did not read the road surface Road Z. Run 2 Read data again", stop.Message);
            Assert.Empty(builder.Requests);
        }

        [Fact]
        public void R492_check_of_the_past_job_passes_every_road_role_of_the_read_and_ignores_the_pick()
        {
            CheckFiles files = CheckSite(PastMode(), First);
            var builder = new Builder();
            RunLater end = null;
            Check(builder, new[] { Second }).Body().Start(CheckContext(files, "R4-4"), later => end = later);
            Assert.Null(end.Failure);
            WorkerRequest request = Assert.Single(builder.Requests);
            Assert.Equal(new[] { First, Second, Third }, RoadRoles(request.Inputs.Select(input => input.Role)));
            Assert.DoesNotContain("roadSurfaces", request.ToJson());
        }

        // R493

        [Fact]
        public void R493_the_message_of_a_zone_above_the_share_is_the_plain_line_of_python()
        {
            Assert.Equal(Stop, StopBasis(0.1, First, Second).NoRoadStop(new[] { "1", "2", "3", "4", "5" }, new[] { "1", "2", "3" }, Rows("Zone 2", "Zone 2", "Zone 2", "Zone 2", "Zone 2")));
            Assert.Contains("%s: %d of %d plots have no road level on the road surfaces named (%s). What to do: add the road surface of this zone in Set up.",
                File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "python", "design_basis.py")));
        }

        [Fact]
        public void R493_exactly_at_the_share_does_not_stop_and_above_does()
        {
            Dictionary<string, Dictionary<string, string>> rows = Rows(Enumerable.Repeat("Zone 2", 10).ToArray());
            List<string> reached = rows.Keys.ToList();
            Assert.Null(StopBasis(0.1, First).NoRoadStop(reached, new[] { "1" }, rows));
            Assert.Contains("2 of 10 plots", StopBasis(0.1, First).NoRoadStop(reached, new[] { "1", "2" }, rows));
            Assert.Null(StopBasis(0.0, First).NoRoadStop(reached, new string[0], rows));
            Assert.Null(StopBasis(1.0, First).NoRoadStop(reached, reached, rows));
        }

        [Fact]
        public void R493_one_sentence_per_zone_that_fails_in_zone_order_a_blank_zone_reads_no_zone()
        {
            Dictionary<string, Dictionary<string, string>> rows = Rows(new[] { "Zone 3", "Zone 3", "", "  ", "Zone 1", "Zone 1" }.Concat(Enumerable.Repeat("Zone 9", 10)).ToArray());
            string Line(string zone, int count, int all) => zone + ": " + count + " of " + all + " plots have no road level on the road surfaces named (Road A). What to do: add the road surface of this zone in Set up.";
            Assert.Equal(Line("Zone 1", 1, 2) + " " + Line("Zone 3", 1, 2) + " " + Line("no zone", 2, 2) + " (R493)",
                StopBasis(0.1, First).NoRoadStop(rows.Keys.ToList(), new[] { "1", "3", "4", "5", "7" }, rows));
        }

        [Fact]
        public void R493_the_limit_is_read_under_rev6_and_a_config_with_none_stops_at_any_plot()
        {
            Assert.Equal(0.25, Road(new JObject { ["surfaces"] = new JArray(First), ["noRoadStopShare"] = 0.25 }).NoRoadStopShare);
            DesignBasis old = Road(new JObject { ["surface"] = First });
            Assert.Equal(0.0, old.NoRoadStopShare);
            Dictionary<string, Dictionary<string, string>> rows = Rows(Enumerable.Repeat("Zone 2", 1000).ToArray());
            Assert.Contains("1 of 1000 plots", old.NoRoadStop(rows.Keys.ToList(), new[] { "1" }, rows));
            Assert.Null(old.NoRoadStop(rows.Keys.ToList(), new string[0], rows));
        }

        [Theory]
        [InlineData("true", "True")]
        [InlineData("false", "False")]
        [InlineData("\"0.1\"", "'0.1'")]
        [InlineData("null", "None")]
        [InlineData("-0.01", "-0.01")]
        [InlineData("1.5", "1.5")]
        [InlineData("NaN", "nan")]
        [InlineData("[]", "[]")]
        public void R493_a_limit_that_is_no_number_in_0_to_1_or_a_bool_is_refused_by_name(string bad, string shown)
        {
            Assert.Equal("designBasis.roadLevel.noRoadStopShare must be a number from 0 to 1, it holds " + shown + " (R493)",
                Refused(new JObject { ["surfaces"] = new JArray(First), ["noRoadStopShare"] = JToken.Parse(bad) }));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("0.0")]
        [InlineData("1.0")]
        public void R493_both_ends_of_the_limit_are_a_value(string edge)
        {
            Assert.Equal((double)JToken.Parse(edge), Road(new JObject { ["surfaces"] = new JArray(First), ["noRoadStopShare"] = JToken.Parse(edge) }).NoRoadStopShare);
        }

        [Fact]
        public void R493_the_past_job_never_stops()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["designBasis"]["mode"] = PastMode();
            DesignBasis past = DesignBasis.Of(config, () => null);
            Dictionary<string, Dictionary<string, string>> rows = Rows("Zone 2", "Zone 2");
            Assert.Null(past.NoRoadStop(rows.Keys.ToList(), rows.Keys.ToList(), rows));
        }

        // The whole worker build with its plan: the steps of the plan, site.json of button 1 and an override list, on the surfaces and the limit given
        private static WorkerRequest PlanRequest(LevelFloorStepTests steps, string runId, IEnumerable<Tuple<string, double, double, double>> surfaces, Action<JObject> change)
        {
            WorkerRequest request = Surfaced(steps, runId, surfaces, change, BuildPlan.Steps);
            JObject config = JObject.Parse(File.ReadAllText(request.Inputs.Single(input => input.Role == "config").Path));
            string list = Write(Path.Combine(request.OutputFolder, "overrides.json"), new JObject
            {
                ["overrides"] = new JArray(new JObject { ["plot"] = "3", ["field"] = "heightM", ["value"] = JObject.Parse("{\"gapToRuleM\": 0.1}"), ["source"] = "parsons", ["model"] = "a.rvt", ["key"] = "imc" }),
            }.ToString());
            request.Inputs.Add(new WorkerFile { Role = "overrides", Path = list, Sha256 = FileHash.Sha256(list) });
            var levels = new JObject();
            foreach (string kind in new[] { "generic", "yard", "ramp" })
            {
                levels[(string)config["floors"]["levels"][kind]] = 0.0;
            }
            string site = Write(Path.Combine(request.OutputFolder, "site.json"), new JObject
            {
                ["levels"] = levels, ["surveyPoint"] = JObject.Parse("{ 'position': [0, 0, 0], 'sharedPosition': [0, 0, 0] }"), ["angleToTrueNorthDeg"] = 0.0,
            }.ToString());
            request.Inputs.Add(new WorkerFile { Role = BuildPlan.SiteRole, Path = site, Sha256 = FileHash.Sha256(site) });
            return request;
        }

        private static WorkerAnswer Answer(WorkerRequest request)
        {
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, request.RunId));
        }

        [Fact]
        public void R493_the_worker_build_stops_before_its_plan_and_below_the_limit_writes_it_with_one_issue_line()
        {
            using (var steps = new LevelFloorStepTests())
            {
                // Plots 5 and 6 run past the end of the surface at x 50, so 2 of the 6 plots of Zone 2 have no road level
                Tuple<string, double, double, double>[] part = { Tuple.Create(First, -60.0, 50.0, 10.0) };
                WorkerRequest stopped = PlanRequest(steps, "s1", part, config => config["designBasis"]["roadLevel"]["noRoadStopShare"] = 0.1);
                Assert.Equal(WorkerExit.Refused, RunWorker(stopped));
                Assert.Equal("Zone 2: 2 of 6 plots have no road level on the road surfaces named (Road A). What to do: add the road surface of this zone in Set up. (R493)", Answer(stopped).Message);
                Assert.False(File.Exists(Path.Combine(stopped.OutputFolder, BuildPlan.FileName(null))));

                WorkerRequest below = PlanRequest(steps, "s2", part, config => config["designBasis"]["roadLevel"]["noRoadStopShare"] = 0.5);
                Assert.True(RunWorker(below) == 0, Answer(below).Message);
                Assert.True(File.Exists(Path.Combine(below.OutputFolder, BuildPlan.FileName(null))));
                Assert.Equal("2 plots have no road level and are listed in the plan: 5, 6 (R493)", Assert.Single(Answer(below).Issues, DesignBasis.IsNoRoad));

                WorkerRequest covered = PlanRequest(steps, "s3", new[] { Tuple.Create(First, -60.0, 200.0, 10.0) }, config => config["designBasis"]["roadLevel"]["noRoadStopShare"] = 0.1);
                Assert.True(RunWorker(covered) == 0, Answer(covered).Message);
                Assert.DoesNotContain(Answer(covered).Issues, DesignBasis.IsNoRoad);
            }
        }

        [Fact]
        public void R493_check_shows_the_stop_plainly_and_writes_no_record()
        {
            CheckFiles files = CheckSite(DesignBasis.Rev6, First, Second);
            var builder = new Builder { Answer = new WorkerAnswer { Status = WorkerExit.Status(WorkerExit.Refused), Message = Stop, Issues = new List<string>() } };
            RunLater end = null;
            Check(builder, null).Body().Start(CheckContext(files, "R4-5"), later => end = later);
            Assert.Equal("4 Check did not finish, nothing was kept: " + Stop, end.Failure);
            Assert.DoesNotContain("R280", end.Failure);
            Assert.Null(CheckRun.RecordOf(builder.Requests[0].OutputFolder));

            // Another refusal keeps the line of R280
            var other = new Builder { Answer = new WorkerAnswer { Status = WorkerExit.Status(WorkerExit.Refused), Message = "made up refusal" } };
            Check(other, null).Body().Start(CheckContext(files, "R4-6"), later => end = later);
            Assert.Contains("made up refusal (R280 when it names the override list)", end.Failure);
        }

        [Fact]
        public void R493_a_dry_run_shows_the_stop_plainly_and_below_the_limit_its_one_line()
        {
            CheckFiles files = CheckSite(DesignBasis.Rev6, First, Second);
            var stopped = new Builder { Answer = new WorkerAnswer { Status = WorkerExit.Status(WorkerExit.Refused), Message = Stop, Issues = new List<string>() } };
            string dry = Path.Combine(files.Work, "dry");
            InvalidDataException stop = Assert.Throws<InvalidDataException>(() => Check(stopped, null, true).Body().Preview(CheckContext(files, "4_20261102-000001-000", dry)));
            Assert.Equal(Stop, stop.Message);

            string line = DesignBasis.NoRoadLine(new[] { "5", "6" });
            Assert.Equal("2 plots have no road level and are listed in the plan: 5, 6 (R493)", line);
            Assert.Null(DesignBasis.NoRoadLine(new string[0]));
            var below = new Builder { Answer = new WorkerAnswer { Status = WorkerExit.Status(WorkerExit.Done), Issues = new List<string> { "another issue", line }, Outputs = new List<WorkerFile>() } };
            RunSummary summary = Check(below, null, true).Body().Preview(CheckContext(files, "4_20261102-000002-000", dry));
            Assert.Contains(line, summary.PerPlot);
            Assert.DoesNotContain("another issue", summary.PerPlot);
        }

        // Review 1 and breaker 1 of FIX-2NOV-D

        // Fix 1. The effective order under rev6 is in the hash of the read, the past job hash stays the hash of its roles and steps alone
        [Fact]
        public void R492_swapping_two_surfaces_gives_a_new_read_folder_and_the_hash_of_the_past_job_stays_byte_the_same()
        {
            string work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            List<RunInput> pane = ReadPane(work, DesignBasis.Rev6, First, Second);
            ReadPlan named = ReadInputs.ForBuild(pane, ReadRoles, work);
            ReadPlan swapped = ReadInputs.ForBuild(pane, ReadRoles, work, new[] { Second, First });
            Assert.NotEqual(ReadDataRun.FolderOf(work, named), ReadDataRun.FolderOf(work, swapped));
            Assert.Equal(named.Hash, ReadInputs.ForBuild(pane, ReadRoles, work, new[] { First, Second }).Hash);

            List<RunInput> past = ReadPane(work, PastMode(), First, Second);
            ReadPlan plain = ReadInputs.ForBuild(past, ReadRoles, work);
            var hashes = plain.Inputs.ToDictionary(input => input.Role, input => input.Sha256, StringComparer.Ordinal);
            hashes["steps"] = string.Join(",", plain.Steps);
            Assert.Equal(StepHash.Combine(hashes), plain.Hash);
            Assert.Equal(plain.Hash, ReadInputs.ForBuild(past, ReadRoles, work, new[] { Second }).Hash);
        }

        [Fact]
        public void R492_a_read_of_another_road_order_is_not_reused_and_one_of_the_same_order_or_of_the_past_job_is()
        {
            string work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            string folder = Path.Combine(root, "read");
            void Record(ReadPlan plan, string order)
            {
                var record = new JObject
                {
                    ["format"] = ReadDataRun.RecordFormat, ["runId"] = "R2-1", ["outputs"] = new JArray(),
                    ["inputs"] = new JArray(plan.Inputs.Select(input => new JObject { ["role"] = input.Role, ["path"] = input.Path, ["sha256"] = input.Sha256 })),
                };
                if (order != null)
                {
                    record["roadOrder"] = order;
                }
                Write(Path.Combine(folder, ReadDataRun.RecordFile), record.ToString());
            }

            ReadPlan swapped = ReadInputs.ForBuild(ReadPane(work, DesignBasis.Rev6, First, Second), ReadRoles, work, new[] { Second, First });
            Record(swapped, First + "\n" + Second);
            Assert.Null(ReadDataRun.Reusable(folder, swapped));
            Record(swapped, Second + "\n" + First);
            Assert.NotNull(ReadDataRun.Reusable(folder, swapped));
            ReadPlan past = ReadInputs.ForBuild(ReadPane(work, PastMode(), First, Second), ReadRoles, work);
            Record(past, null);
            Assert.NotNull(ReadDataRun.Reusable(folder, past));
        }

        // Fix 2. A surface siteBuild.roadFolder.notTaken names is no road surface

        private const string Ground = "Made Up Ground, Other Grading and More cover the corners but are ground";

        private static JObject WithNotTaken(JObject config)
        {
            config["siteBuild"]["roadFolder"]["notTaken"] = new JObject { ["ground"] = Ground, ["other"] = "a made up reason of no surface" };
            return config;
        }

        [Fact]
        public void R490_a_list_naming_a_not_taken_surface_is_refused_with_its_reason_and_the_past_job_reads_none_of_it()
        {
            JObject config = WithNotTaken(RoadConfig(new JObject { ["surfaces"] = new JArray(First, "Made Up Job__Other Grading") }));
            Assert.Equal("designBasis.roadLevel.surfaces names Made Up Job__Other Grading, which is no road surface: " + Ground + " (R490)",
                Assert.Throws<InvalidDataException>(() => DesignBasis.Of(config, () => null)).Message);
            // A longer name is another surface, the reason holds the whole name or nothing
            config = WithNotTaken(RoadConfig(new JObject { ["surfaces"] = new JArray("Made Up Job__Other Gradings") }));
            Assert.Single(DesignBasis.Of(config, () => null).Surfaces);
            config = WithNotTaken(RoadConfig(new JObject { ["surfaces"] = new JArray("Made Up Job__Other Grading") }));
            config["designBasis"]["mode"] = PastMode();
            Assert.Null(DesignBasis.Of(config, () => null).Surfaces);
        }

        [Fact]
        public void R490_the_list_of_the_repo_config_passes_under_rev6()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            Assert.Equal(6, DesignBasis.Of(config, () => null).Surfaces.Count);
        }

        [Fact]
        public void R492_a_pick_naming_a_not_taken_surface_is_refused_with_its_reason_in_the_read_and_the_build()
        {
            string work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            List<RunInput> pane = ReadPane(work, DesignBasis.Rev6, First);
            string configPath = pane[0].Path;
            File.WriteAllText(configPath, WithNotTaken(JObject.Parse(File.ReadAllText(configPath))).ToString());
            Write(Path.Combine(root, "input", "roads", "Made Up Job__Other Grading.xml"), "<LandXML/>");
            Assert.Equal("Road surface: Made Up Job__Other Grading is no road surface: " + Ground + " (R492)",
                Assert.Single(ReadInputs.ForBuild(pane, ReadRoles, work, new[] { First, "Made Up Job__Other Grading" }).Stops));

            DesignBasis basis = DesignBasis.Of(WithNotTaken(RoadConfig(new JObject { ["surfaces"] = new JArray(First) })), () => null);
            Assert.Equal("the road surfaces of the request name Made Up Job__Other Grading, which is no road surface: " + Ground + " (R492)",
                Assert.Throws<InvalidDataException>(() => basis.TakePick(new[] { "Made Up Job__Other Grading" })).Message);
        }

        // Fix 3. A unit plot with no face or no front is no missing surface: it stays listed, the share counts it only below the line

        private static void AddPlotWithNoFace(WorkerRequest request)
        {
            WorkerFile plots = request.Inputs.Single(input => input.Role == "plotsCsv"), labels = request.Inputs.Single(input => input.Role == "labels");
            File.AppendAllText(plots.Path, "7,7,DP2,,no,Zone 2\n");
            File.AppendAllText(labels.Path, "clientCode,7,012\n");
            plots.Sha256 = FileHash.Sha256(plots.Path);
            labels.Sha256 = FileHash.Sha256(labels.Path);
        }

        [Fact]
        public void R493_a_unit_plot_with_no_front_does_not_count_above_the_line_and_stays_in_no_road_level()
        {
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest covered = PlanRequest(steps, "f1", new[] { Tuple.Create(First, -60.0, 200.0, 10.0) }, config => config["designBasis"]["roadLevel"]["noRoadStopShare"] = 0.1);
                AddPlotWithNoFace(covered);
                Assert.True(RunWorker(covered) == 0, Answer(covered).Message);
                JObject plan = JObject.Parse(File.ReadAllText(Path.Combine(covered.OutputFolder, BuildPlan.FileName(null))));
                JToken listed = Assert.Single((JArray)plan["summary"]["noRoadLevel"]);
                Assert.Equal("7", (string)listed["plot"]);
                Assert.Empty((JArray)listed["corners"]);
                Assert.Equal("1 plots have no road level and are listed in the plan: 7 (R493)", Assert.Single(Answer(covered).Issues, DesignBasis.IsNoRoad));

                // Plots 5 and 6 lack a road at a corner, plot 7 has no front: 2 above the line, the 7 reached plots below it
                WorkerRequest part = PlanRequest(steps, "f2", new[] { Tuple.Create(First, -60.0, 50.0, 10.0) }, config => config["designBasis"]["roadLevel"]["noRoadStopShare"] = 0.1);
                AddPlotWithNoFace(part);
                Assert.Equal(WorkerExit.Refused, RunWorker(part));
                Assert.Equal("Zone 2: 2 of 7 plots have no road level on the road surfaces named (Road A). What to do: add the road surface of this zone in Set up. (R493)", Answer(part).Message);
            }
        }

        // Fix 4. A road role the effective list left out is no input of the build, so its change stops nothing, under the past job it still does

        [Fact]
        public void R492_a_surface_the_list_left_out_stops_4_check_no_more_when_it_changes_and_one_of_the_list_still_does()
        {
            CheckFiles files = CheckSite(DesignBasis.Rev6, First, Second);
            File.WriteAllText(Path.Combine(root, "roads", Third + ".xml"), "<LandXML changed=\"yes\"/>");
            var builder = new Builder();
            RunLater end = null;
            Check(builder, null).Body().Start(CheckContext(files, "R4-7"), later => end = later);
            Assert.Null(end.Failure);
            Assert.Equal(new[] { First, Second }, RoadRoles(Assert.Single(builder.Requests).Inputs.Select(input => input.Role)));

            File.WriteAllText(Path.Combine(root, "roads", First + ".xml"), "<LandXML changed=\"yes\"/>");
            Assert.Contains("changed since: " + First + ".xml", Assert.Throws<InvalidDataException>(() => Check(new Builder(), null).Body().Preview(CheckContext(files, "R4-8"))).Message);
        }

        [Fact]
        public void R492_under_the_past_job_every_road_surface_of_the_read_that_changes_still_stops_4_check()
        {
            CheckFiles files = CheckSite(PastMode(), First);
            File.WriteAllText(Path.Combine(root, "roads", Third + ".xml"), "<LandXML changed=\"yes\"/>");
            Assert.Contains("changed since: " + Third + ".xml", Assert.Throws<InvalidDataException>(() => Check(new Builder(), null).Body().Preview(CheckContext(files, "R4-9"))).Message);
        }

        // Fix 5. A pre-setting replaces the settings: one with no pick clears the pick, its bytes under the past job stay

        [Fact]
        public void R492_a_pre_setting_with_no_pick_clears_the_pick_and_a_project_with_none_keeps_its_bytes()
        {
            var into = new ProjectSettings { RoadSurfaces = new List<string> { First } };
            PreSetting.Import(PreSetting.Export(new ProjectSettings()), into);
            Assert.Null(into.RoadSurfaces);

            var none = new ProjectSettings { ConfigFile = "made up.json", LevelProfile = "made up" };
            string before = PreSetting.Export(none);
            PreSetting.Import(before, none);
            Assert.Equal(before, PreSetting.Export(none));
            Assert.DoesNotContain("roadSurfaces", SettingsJson.Write(none));
        }

        [Fact]
        public void R493_the_exit_of_a_stop_is_the_refusal_of_the_worker_and_its_status_refused()
        {
            Assert.Equal("refused", WorkerExit.Status(WorkerExit.Refused));
            Assert.True(DesignBasis.IsNoRoad(Stop));
            Assert.False(DesignBasis.IsNoRoad("made up refusal"));
            Assert.False(DesignBasis.IsNoRoad(null));
        }
    }
}
