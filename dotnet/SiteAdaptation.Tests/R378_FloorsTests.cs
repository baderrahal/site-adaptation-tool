using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R378. Button 5 Floors and ramps on a fake document: the three floors of a plot with their plot value and tag, the ramp corners by
    // slab shape editing, the second run, the floors placed by hand, the stops and the dry run. No Revit type, so these run on net8.0 too
    public class R378_FloorsTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "r378_" + Guid.NewGuid().ToString("N"));

        public R378_FloorsTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private FloorsRun Make(FakeFloors document, PlanFile plan, Func<string, bool> inScope = null, ICollection<string> lastPlaced = null)
        {
            return new FloorsRun(document, context => new FloorInputs
            {
                Plan = plan, PlanPath = Path.Combine(work, "plan_012.json"), Profile = "parsons2024Rev6", InScope = inScope,
                LastPlaced = lastPlaced ?? new HashSet<string>(StringComparer.Ordinal),
            });
        }

        private RunContext Context(string runId = "5_1", bool dry = false)
        {
            return new RunContext(5, "5 Floors and ramps", runId, work, dry ? Path.Combine(work, "dry", runId) : null, true, new RunInput[0], new Dictionary<string, string>());
        }

        private static IReadOnlyList<string> Apply(FloorsRun run, RunContext context)
        {
            ButtonBody body = run.Body();
            body.Preview(context);
            return body.Apply(context);
        }

        private static FakeFloors.Floor Find(FakeFloors document, string plot, string kind)
        {
            return document.Items.Single(floor => floor.Tag != null && floor.Tag.Key.Plot == plot && floor.Tag.Key.Kind == kind);
        }

        [Fact]
        public void R378_places_the_generic_yard_and_ramp_floor_of_each_plot_with_its_plot_value_and_tag()
        {
            var document = new FakeFloors();
            Apply(Make(document, FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102"))), Context());

            Assert.Equal(6, document.Items.Count);
            Assert.All(document.Items, floor => Assert.Equal(floor.UniqueId, floor.Tag.PlacedId));
            Assert.Equal(new[] { "VD4-101", "VD4-101", "VD4-101", "VD4-102", "VD4-102", "VD4-102" }, document.Items.Select(floor => floor.Parameters["Plot"]));
            Assert.Equal(10.5, Find(document, "101", PlacedKinds.Generic).Top, 9);
            Assert.Equal(10.4, Find(document, "101", PlacedKinds.Yard).Top, 9);
            // The ramp is made flat at its lowest corner, then each corner is lifted to its own z whatever order the editor lists them in
            FakeFloors.Floor ramp = Find(document, "101", PlacedKinds.Ramp);
            Assert.Equal(10.0, ramp.Top, 9);
            Assert.Equal(new[] { 0.0, 0.1, 0.3, 0.2 }, ramp.CornerOffsets.Select(offset => Math.Round(offset, 9)));
            // Two regenerations a run, never one per floor (R116)
            Assert.Equal(2, document.Calls.Count(call => call == "regenerate"));
        }

        [Fact]
        public void R378_the_run_writes_its_record_with_every_floor_that_stands()
        {
            var document = new FakeFloors();
            RunContext context = Context();
            Apply(Make(document, FloorPlans.Plan(FloorPlans.Plot("101"))), context);

            string temporary = WorkFiles.TemporaryName(Path.Combine(work, FloorsRun.RecordName("012")), context.RunId);
            JObject record = JObject.Parse(File.ReadAllText(temporary));
            Assert.Equal(new[] { "101|generic", "101|ramp", "101|yard" }, FloorsRun.Placed(record).OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal(3, (int)record["counts"]["place"]);
        }

        [Fact]
        public void R378_a_second_run_skips_every_floor_equal_to_the_plan()
        {
            var document = new FakeFloors();
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            Apply(Make(document, plan), Context("5_1"));
            document.Calls.Clear();

            RunSummary summary = Make(document, plan).Body().Preview(Context("5_2"));
            Apply(Make(document, plan), Context("5_2"));

            Assert.Equal(3, document.Items.Count);
            Assert.DoesNotContain(document.Calls, call => call.StartsWith("create", StringComparison.Ordinal) || call.StartsWith("offset", StringComparison.Ordinal));
            Assert.Contains("place 0, move 0, already placed 3", summary.Counts);
        }

        [Fact]
        public void R378_a_floor_whose_level_the_plan_changed_is_changed_in_place_keeping_its_unique_id()
        {
            var document = new FakeFloors();
            Apply(Make(document, FloorPlans.Plan(FloorPlans.Plot("101"))), Context("5_1"));
            string generic = Find(document, "101", PlacedKinds.Generic).UniqueId;
            PlanFile changed = FloorPlans.Plan(FloorPlans.Plot("101"));
            changed.Plots[0].Floors.Generic.Z.Value = 10.75;
            changed.Plots[0].Floors.Ramp.Corners.Value[2][2] = 10.4;

            IReadOnlyList<string> issues = Apply(Make(document, changed), Context("5_2"));

            Assert.Equal(3, document.Items.Count);
            Assert.Equal(10.75, document.Of(generic).Top, 9);
            Assert.Equal(0.4, Find(document, "101", PlacedKinds.Ramp).CornerOffsets[2], 9);
            Assert.Equal("5_2", document.Of(generic).Tag.Run);
            Assert.Equal(generic, document.Of(generic).Tag.PlacedId);
            Assert.Empty(issues);
        }

        [Fact]
        public void R378_a_floor_whose_outline_the_plan_changed_is_listed_and_left_until_its_question_is_answered()
        {
            var document = new FakeFloors();
            Apply(Make(document, FloorPlans.Plan(FloorPlans.Plot("101"))), Context("5_1"));
            FakeFloors.Floor yard = Find(document, "101", PlacedKinds.Yard);
            PlanFile changed = FloorPlans.Plan(FloorPlans.Plot("101"));
            changed.Plots[0].Floors.Yard.Outline.Value.Loops[0].Points = FloorPlans.Square(1100, 2020, 10, 12);
            document.Calls.Clear();

            IReadOnlyList<string> issues = Apply(Make(document, changed), Context("5_2"));

            Assert.Equal(3, document.Items.Count);
            Assert.Equal("5_1", yard.Tag.Run);
            Assert.DoesNotContain(document.Calls, call => call.StartsWith("create", StringComparison.Ordinal) || call.StartsWith("offset", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("plot 101 yard: element " + yard.UniqueId + ", its outline changed in the plan, left as it is"));
        }

        [Fact]
        public void R378_a_floor_of_the_plot_and_kind_with_no_tag_is_skipped_listed_and_never_changed()
        {
            var document = new FakeFloors();
            FakeFloors.Floor hand = document.ByHand("1", "VD4-101", 9.9, new[] { 1100.0, 2000.0 }, new[] { 1110.0, 2000.0 }, new[] { 1110.0, 2020.0 });

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(FloorPlans.Plot("101"))), Context());

            Assert.Equal(3, document.Items.Count);
            Assert.Null(hand.Tag);
            Assert.Equal(9.9, hand.Top, 9);
            Assert.DoesNotContain(document.Items, floor => floor.Tag != null && floor.Tag.Key.Kind == PlacedKinds.Generic);
            Assert.Contains(issues, issue => issue.Contains("plot 101 generic") && issue.Contains("not by this tool") && issue.Contains(hand.UniqueId));
        }

        [Fact]
        public void R378_a_plot_with_no_yard_in_the_plan_gets_its_generic_floor_and_ramp_and_says_why()
        {
            var document = new FakeFloors();
            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(FloorPlans.Plot("101", yard: false))), Context());

            Assert.Equal(2, document.Items.Count);
            Assert.Contains(issues, issue => issue == "plot 101 yard: skipped, the plan has no yard floor for this plot");
        }

        [Fact]
        public void R378_a_floor_type_or_a_level_the_document_lacks_stops_before_any_change()
        {
            var document = new FakeFloors();
            document.Types.RemoveAll(type => type.Name == "Yard");
            var stop = Assert.Throws<InvalidDataException>(() => Make(document, FloorPlans.Plan(FloorPlans.Plot("101"))).Body().Preview(Context()));
            Assert.Contains("the document has no floor type Yard", stop.Message);

            var noLevel = new FakeFloors();
            noLevel.LevelList.Clear();
            stop = Assert.Throws<InvalidDataException>(() => Make(noLevel, FloorPlans.Plan(FloorPlans.Plot("101"))).Body().Apply(Context()));
            Assert.Contains("the document has no level Level 0", stop.Message);
            Assert.Empty(document.Items);
            Assert.Empty(noLevel.Items);
        }

        [Fact]
        public void R378_the_dry_run_lists_each_floor_it_would_place_and_changes_nothing()
        {
            var document = new FakeFloors();
            RunSummary summary = Make(document, FloorPlans.Plan(FloorPlans.Plot("101"))).Body().Preview(Context(dry: true));

            Assert.Empty(document.Items);
            Assert.Empty(document.Calls);
            Assert.Contains("place 3", summary.Counts);
            Assert.Contains("plot 101 generic: place Generic 150mm on Level 0, height offset 10.5 m", summary.PerPlot);
            Assert.Contains("plot 101 ramp: place Ramp 0.5 on Level 0, height offset 10 m, 4 corners by slab shape editing", summary.PerPlot);
        }

        [Fact]
        public void R378_a_plot_value_not_written_or_a_floor_read_back_off_the_plan_fails_so_the_flow_rolls_back()
        {
            var refusing = new FakeFloors { RefusePlot = true };
            var failure = Assert.Throws<InvalidOperationException>(() => Apply(Make(refusing, FloorPlans.Plan(FloorPlans.Plot("101"))), Context()));
            Assert.Contains("Plot was not written", failure.Message);

            var drifting = new FakeFloors { Drift = 0.002 };
            failure = Assert.Throws<InvalidOperationException>(() => Apply(Make(drifting, FloorPlans.Plan(FloorPlans.Plot("101"))), Context()));
            Assert.Contains("does not match the plan", failure.Message);
        }

        [Fact]
        public void R378_only_the_plots_of_the_scope_are_placed_and_a_run_with_none_fails()
        {
            var document = new FakeFloors();
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102"));
            Apply(Make(document, plan, number => number == "102"), Context());

            Assert.Equal(new[] { "102" }, document.Items.Select(floor => floor.Tag.Key.Plot).Distinct());

            var none = Assert.Throws<InvalidOperationException>(() => Apply(Make(new FakeFloors(), plan, number => false), Context()));
            Assert.Contains("nothing of the scope is placed or already placed", none.Message);
        }
    }
}
