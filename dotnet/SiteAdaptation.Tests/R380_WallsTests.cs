using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using SiteAdaptation.Common.Tags;
using SiteAdaptation.Common.BoundaryWalls;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R380. Button 8 Walls on a fake document: one BW wall per plan wall on its line, base and top with its tag, the issues of Q071 and the
    // garden walls, the second run, the walls placed by hand, the stops, the dry run and the load of the plan. No Revit type
    public class R380_WallsTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "r380_" + Guid.NewGuid().ToString("N"));

        public R380_WallsTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private WallsRun Make(FakeWalls document, IReadOnlyList<PlanWallItem> walls = null, Func<string, bool> inScope = null, ICollection<string> lastPlaced = null)
        {
            return new WallsRun(document, context => new WallInputs
            {
                Plan = FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102")), Walls = walls ?? WallPlans.Two(), PlanPath = Path.Combine(work, "plan_012.json"),
                Profile = "parsons2024Rev6", InScope = inScope, BoundaryType = "^BW$", LastPlaced = lastPlaced ?? new HashSet<string>(StringComparer.Ordinal),
            });
        }

        private RunContext Context(string runId = "8_1", bool dry = false)
        {
            return new RunContext(8, "8 Walls", runId, work, dry ? Path.Combine(work, "dry", runId) : null, true, new RunInput[0], new Dictionary<string, string>());
        }

        private static IReadOnlyList<string> Apply(WallsRun run, RunContext context)
        {
            ButtonBody body = run.Body();
            body.Preview(context);
            return body.Apply(context);
        }

        [Fact]
        public void R380_places_one_bw_wall_per_plan_wall_on_its_line_base_and_top_with_its_tag()
        {
            var document = new FakeWalls();
            Apply(Make(document), Context());

            Assert.Equal(2, document.Items.Count);
            FakeWalls.Wall shared = document.Items[0];
            Assert.Equal("1", shared.TypeKey);
            Assert.Equal(new[] { 10.0, 0.0 }, shared.Start);
            Assert.Equal(new[] { 10.0, 20.0 }, shared.End);
            Assert.Equal(10.4, shared.Base, 9);
            Assert.Equal(12.2, shared.Top, 9);
            Assert.Equal(new TagKey("101", PlacedKinds.BoundaryWall, "101+102|e1|p1"), shared.Tag.Key);
            Assert.All(document.Items, wall => Assert.Equal(wall.UniqueId, wall.Tag.PlacedId));
            Assert.Equal(1, document.Calls.Count(call => call == "regenerate"));
        }

        [Fact]
        public void R380_every_wall_top_is_an_issue_until_Q071_and_the_result_says_no_garden_wall_is_placed()
        {
            IReadOnlyList<string> issues = Apply(Make(new FakeWalls()), Context());

            Assert.Equal(2, issues.Count(issue => issue.Contains("Q071")));
            Assert.Contains(issues, issue => issue.StartsWith("Garden walls: none placed", StringComparison.Ordinal));
        }

        [Fact]
        public void R380_two_pieces_of_one_edge_and_plots_keep_two_tag_keys()
        {
            var walls = new List<PlanWallItem>
            {
                WallPlans.Wall("101", 1, new[] { 1010.0, 2000.0 }, new[] { 1010.0, 2008.0 }, "101", "102"),
                WallPlans.Wall("101", 1, new[] { 1010.0, 2012.0 }, new[] { 1010.0, 2020.0 }, "101", "102"),
            };
            var document = new FakeWalls();

            Apply(Make(document, walls), Context());

            Assert.Equal(new[] { "101+102|e1|p1", "101+102|e1|p2" }, document.Items.Select(wall => wall.Tag.Key.Part));
        }

        [Fact]
        public void R380_a_second_run_skips_every_wall_equal_to_the_plan()
        {
            var document = new FakeWalls();
            Apply(Make(document), Context("8_1"));
            document.Calls.Clear();

            RunSummary summary = Make(document).Body().Preview(Context("8_2"));
            Apply(Make(document), Context("8_2"));

            Assert.Equal(2, document.Items.Count);
            Assert.DoesNotContain(document.Calls, call => call.StartsWith("create", StringComparison.Ordinal) || call.StartsWith("set", StringComparison.Ordinal));
            Assert.Contains("place 0, change 0, already placed 2", summary.Counts);
        }

        [Fact]
        public void R380_a_wall_the_plan_changed_is_changed_in_place_keeping_its_unique_id()
        {
            var document = new FakeWalls();
            Apply(Make(document), Context("8_1"));
            string id = document.Items[0].UniqueId;
            List<PlanWallItem> changed = WallPlans.Two();
            changed[0].Top = 12.45;
            changed[0].Ends[1] = new[] { 1010.0, 2018.0 };

            Apply(Make(document, changed), Context("8_2"));

            Assert.Equal(2, document.Items.Count);
            Assert.Equal(12.45, document.Of(id).Top, 9);
            Assert.Equal(new[] { 10.0, 18.0 }, document.Of(id).End);
            Assert.Equal("8_2", document.Of(id).Tag.Run);
            Assert.Equal(id, document.Of(id).Tag.PlacedId);
        }

        [Fact]
        public void R380_a_wall_changed_by_hand_is_listed_and_left()
        {
            var document = new FakeWalls();
            Apply(Make(document), Context("8_1"));
            FakeWalls.Wall wall = document.Items[1];
            wall.Height = 3.0;
            document.Calls.Clear();

            IReadOnlyList<string> issues = Apply(Make(document), Context("8_2"));

            Assert.DoesNotContain(document.Calls, call => call.StartsWith("set", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("plot 101 wall 101|e2|p1: changed by hand in top, left as it is"));
        }

        [Fact]
        public void R380_an_untagged_wall_is_listed_and_never_changed_and_one_on_a_plan_wall_skips_it()
        {
            var document = new FakeWalls();
            FakeWalls.Wall onPlan = document.ByHand(new[] { 10.0, 20.0 }, new[] { 0.0, 20.0 }, 10.0, 11.0);
            FakeWalls.Wall elsewhere = document.ByHand(new[] { 50.0, 0.0 }, new[] { 50.0, 9.0 }, 10.0, 11.0);

            IReadOnlyList<string> issues = Apply(Make(document), Context());

            Assert.Equal(3, document.Items.Count);
            Assert.Null(onPlan.Tag);
            Assert.Equal(11.0, onPlan.Top, 9);
            Assert.Contains(issues, issue => issue.Contains("plot 101 wall 101|e2|p1: already placed, not by this tool, element " + onPlan.UniqueId));
            Assert.Contains(issues, issue => issue.Contains("element " + elsewhere.UniqueId + " of type BW has no tag and lies on no wall of the plan, left as it is"));
        }

        [Fact]
        public void R380_stops_before_any_change_when_the_wall_type_BW_is_missing()
        {
            var document = new FakeWalls();
            document.Types.RemoveAt(0);

            var stop = Assert.Throws<InvalidDataException>(() => Make(document).Body().Preview(Context()));

            Assert.Contains("the document has no wall type of ^BW$", stop.Message);
            Assert.Empty(document.Items);
        }

        [Fact]
        public void R380_stops_before_any_change_when_the_document_lacks_the_yard_level()
        {
            var document = new FakeWalls();
            document.LevelList[0].Name = "Level 1";

            var stop = Assert.Throws<InvalidDataException>(() => Make(document).Body().Preview(Context()));

            Assert.Contains("the document has no level Level 0", stop.Message);
        }

        [Fact]
        public void R380_a_wall_that_reads_back_off_the_plan_by_more_than_the_tolerance_fails_the_run()
        {
            var document = new FakeWalls { Drift = 2 * DesignValues.Current.WallReadBackM };

            var failure = Assert.Throws<InvalidOperationException>(() => Apply(Make(document), Context()));

            Assert.Contains("the wall does not match the plan, base, top off", failure.Message);
        }

        [Fact]
        public void R380_the_read_back_tolerance_is_designValues_walls_readBackM()
        {
            Assert.Equal(0.001, DesignValues.Current.WallReadBackM);
        }

        [Fact]
        public void R380_the_dry_run_lists_each_wall_and_changes_nothing()
        {
            var document = new FakeWalls();

            RunSummary summary = Make(document).Body().Preview(Context("8_1", true));

            Assert.Empty(document.Items);
            Assert.Empty(document.Calls);
            Assert.Contains("place 2", summary.Counts);
            Assert.Contains(summary.PerPlot, line => line.StartsWith("plot 101 wall 101+102|e1|p1: place BW on Level 0", StringComparison.Ordinal));
        }

        [Fact]
        public void R380_the_run_writes_its_record_with_every_wall_that_stands()
        {
            RunContext context = Context();
            Apply(Make(new FakeWalls()), context);

            JObject record = JObject.Parse(File.ReadAllText(WorkFiles.TemporaryName(Path.Combine(work, WallsRun.RecordName("012")), context.RunId)));

            Assert.Equal(new[] { "101|101+102|e1|p1", "101|101|e2|p1" }, WallsRun.Placed(record).OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal(2, (int)record["counts"]["place"]);
        }

        [Fact]
        public void R380_a_plot_list_places_the_walls_that_stand_on_its_plots()
        {
            var document = new FakeWalls();

            Apply(Make(document, inScope: plot => plot == "102"), Context());

            Assert.Equal(new[] { "101+102|e1|p1" }, document.Items.Select(wall => wall.Tag.Key.Part));
        }

        [Fact]
        public void R380_parses_the_walls_of_the_plan_as_build_plan_writes_them()
        {
            List<PlanWallItem> walls = WallPlan.Parse(new JObject { ["walls"] = WallPlans.Json(WallPlans.Two()) });

            Assert.Equal(2, walls.Count);
            Assert.Equal(new[] { "101", "102" }, walls[0].Plots);
            Assert.Equal(new[] { 1010.0, 2020.0 }, walls[0].Ends[1]);
            Assert.Equal(12.2, walls[1].Top);
            var bad = new JObject { ["walls"] = new JArray(new JObject { ["plot"] = "101", ["edge"] = 1 }) };
            Assert.Contains("wall 0 of the plan cannot be read", Assert.Throws<InvalidDataException>(() => WallPlan.Parse(bad)).Message);
        }

        [Fact]
        public void R380_walls_need_step_7()
        {
            Assert.Equal(7, StepStates.Needs(8));
        }

        [Fact]
        public void R380_walls_take_the_plan_of_the_last_check_its_profile_the_last_record_of_8_and_the_BW_pattern()
        {
            const string runId = "4_20261031-100000-000";
            string folder = Path.Combine(work, CheckRun.FolderPrefix + runId);
            Directory.CreateDirectory(folder);
            string config = Path.Combine(work, "project.json");
            File.WriteAllText(config, new JObject { ["majority"] = new JObject { ["wallTypes"] = new JObject { ["boundary"] = "^BW$" } } }.ToString());
            JObject plan = JObject.FromObject(FloorPlans.Plan(FloorPlans.Plot("101")), Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }));
            plan["walls"] = WallPlans.Json(WallPlans.Two());
            string planPath = Path.Combine(folder, CheckRun.PlanName("012"));
            File.WriteAllText(planPath, plan.ToString());
            File.WriteAllText(Path.Combine(folder, CheckRun.RecordFile), CheckRun.Record(runId, new Dictionary<string, string> { [planPath] = FileHash.Sha256(planPath) }).ToString());
            new WorkerRequest { FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Command = "build", LevelProfile = "parsons2024Rev6", OutputFolder = folder }.Write(folder);
            File.WriteAllText(Path.Combine(work, WallsRun.RecordName("012")), new JObject { ["placed"] = new JArray("101|101|e2|p1") }.ToString());

            WallInputs inputs = WallLoad.Walls(Context(), new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" }, config);

            Assert.Equal(2, inputs.Walls.Count);
            Assert.Equal("parsons2024Rev6", inputs.Profile);
            Assert.Equal("^BW$", inputs.BoundaryType);
            Assert.Contains("101|101|e2|p1", inputs.LastPlaced);
            File.WriteAllText(config, "{}");
            Assert.Contains("majority.wallTypes.boundary", Assert.Throws<InvalidDataException>(() => WallLoad.Walls(Context(), new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" }, config)).Message);
        }
    }
}
