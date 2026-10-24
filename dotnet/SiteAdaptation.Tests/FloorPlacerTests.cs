using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The floor step of Place from plan on a fake. The fake keeps the floors it is asked to make and reads them back the way Revit would
    // give them: the top at level plus height offset, and each shape corner at that top plus its own offset. Nothing here says what Revit does
    public class FloorPlacerTests
    {
        private static readonly double[] Position = { 0, 0, 5 };
        private static readonly double[] SharedPosition = { 1000, 2000, 5 };
        private const string PlotParameter = "Plot";

        internal sealed class Made
        {
            public IList<FloorLoop> Loops;
            public string Type;
            public string Level;
            public double HeightOffset;
            public Dictionary<int, double> CornerOffsets = new Dictionary<int, double>();
            public bool ShapeEditing;
            public Dictionary<string, string> Values = new Dictionary<string, string>();
        }

        internal sealed class FakeFloors : IFloorTarget
        {
            public SurveyReading SurveyValue = new SurveyReading { Position = Position, SharedPosition = SharedPosition, AngleToTrueNorthDeg = 0 };
            public List<FloorTypeEntry> Types = new List<FloorTypeEntry>
            {
                new FloorTypeEntry { Name = "Slab G", Key = "t1" },
                new FloorTypeEntry { Name = "Yard Y", Key = "t2" },
                new FloorTypeEntry { Name = "Ramp B", Key = "t3" },
                new FloorTypeEntry { Name = "Other", Key = "t4" },
            };
            public List<LevelEntry> LevelList = new List<LevelEntry>
            {
                new LevelEntry { Name = "L1", Key = "l1", ElevationM = 0 },
                new LevelEntry { Name = "L2", Key = "l2", ElevationM = 4 },
            };
            public List<ExistingFloor> Existing = new List<ExistingFloor>();
            public Dictionary<long, Made> Created = new Dictionary<long, Made>();
            public int Begins;
            public int Commits;
            public int RollBacks;
            public int FailCreateAt = -1;
            public bool CommitFails;
            public bool PlotMissing;
            public double TopError;
            public List<string> WarningLines = new List<string>();
            private Dictionary<long, Made> saved;
            private long nextId = 500;
            private int createCalls;

            public string DocumentTitle
            {
                get { return "work.rvt"; }
            }

            public IList<string> Warnings
            {
                get { return WarningLines; }
            }

            public SurveyReading Survey()
            {
                return SurveyValue;
            }

            public IList<FloorTypeEntry> FloorTypes()
            {
                return Types;
            }

            public IList<LevelEntry> Levels()
            {
                return LevelList;
            }

            public IList<ExistingFloor> Floors(string plotParameter)
            {
                var all = new List<ExistingFloor>(Existing);
                foreach (KeyValuePair<long, Made> made in Created)
                {
                    string value;
                    made.Value.Values.TryGetValue(plotParameter, out value);
                    all.Add(new ExistingFloor { Id = made.Key, TypeName = Types.First(type => type.Key == made.Value.Type).Name, PlotValue = value });
                }
                return all;
            }

            public void Begin()
            {
                Begins++;
                saved = new Dictionary<long, Made>(Created);
            }

            public long CreateFloor(IList<FloorLoop> loops, string typeKey, string levelKey)
            {
                if (createCalls++ == FailCreateAt)
                {
                    throw new InvalidOperationException("Revit could not make the floor, the loop crosses itself");
                }
                long id = nextId++;
                Created[id] = new Made { Loops = loops, Type = typeKey, Level = levelKey };
                return id;
            }

            public int Regenerations;

            public void Regenerate()
            {
                Regenerations++;
            }

            public void SetHeightOffset(long floorId, double metres)
            {
                Created[floorId].HeightOffset = metres;
            }

            private double Top(Made made)
            {
                return LevelList.First(level => level.Key == made.Level).ElevationM + made.HeightOffset;
            }

            public IList<double[]> ShapeCorners(long floorId)
            {
                Made made = Created[floorId];
                made.ShapeEditing = true;
                // Revit lists the vertices in its own order, here the reverse of the loop
                return made.Loops[0].Segments.Select(segment => new[] { segment.Start[0], segment.Start[1], Top(made) }).Reverse().ToList();
            }

            public void SetCornerOffset(long floorId, int corner, double metres)
            {
                Created[floorId].CornerOffsets[corner] = metres;
            }

            public FloorReadBack ReadBack(long floorId)
            {
                Made made = Created[floorId];
                if (!made.ShapeEditing)
                {
                    return new FloorReadBack { TopMax = Top(made) + TopError };
                }
                List<double[]> corners = made.Loops[0].Segments.Select(segment => segment.Start).Reverse()
                    .Select((point, index) => new[] { point[0], point[1], Top(made) + (made.CornerOffsets.ContainsKey(index) ? made.CornerOffsets[index] : 0) + TopError }).ToList();
                return new FloorReadBack { TopMax = corners.Max(corner => corner[2]), Corners = corners };
            }

            public ParameterWrite WriteText(long floorId, string name, string value)
            {
                if (PlotMissing)
                {
                    return ParameterWrite.Missing;
                }
                Created[floorId].Values[name] = value;
                return ParameterWrite.Written;
            }

            public CommitResult Commit()
            {
                Commits++;
                if (CommitFails)
                {
                    Created = saved;
                    return new CommitResult { Committed = false, Reason = "RolledBack" };
                }
                return new CommitResult { Committed = true, Reason = "Committed" };
            }

            public void RollBack()
            {
                RollBacks++;
                if (saved != null)
                {
                    Created = saved;
                }
            }
        }

        private static JArray Square(double x0, double y0, double x1, double y1)
        {
            return new JArray(new JArray(SharedPosition[0] + x0, SharedPosition[1] + y0), new JArray(SharedPosition[0] + x1, SharedPosition[1] + y0),
                new JArray(SharedPosition[0] + x1, SharedPosition[1] + y1), new JArray(SharedPosition[0] + x0, SharedPosition[1] + y1));
        }

        private static JObject Value(object value, string source = "pastJob")
        {
            return new JObject { ["value"] = value == null ? JValue.CreateNull() : JToken.FromObject(value), ["source"] = source };
        }

        private static JObject Floor(string type, string level, double levelElevation, JArray points, double? z, string plotValue, JArray arcs = null)
        {
            var floor = new JObject
            {
                ["type"] = Value(type),
                ["level"] = Value(level),
                ["levelElevation"] = Value(levelElevation),
                ["outline"] = Value(new JObject { ["loops"] = new JArray(new JObject { ["points"] = points, ["arcs"] = arcs ?? new JArray() }) }),
                ["plotValue"] = Value(plotValue, "computed"),
            };
            if (z.HasValue)
            {
                floor["z"] = Value(z.Value, "computed");
                floor["heightOffset"] = Value(z.Value - levelElevation, "computed");
            }
            return floor;
        }

        private static JObject PlotEntry(string plot, double x0, double ffl = 10.0)
        {
            JArray ramp = Square(x0 + 10, 0, x0 + 12, 10);
            var corners = new JArray();
            double[] lifts = { 9.8, 9.81, 9.82, 9.83 };
            for (int index = 0; index < 4; index++)
            {
                corners.Add(new JArray(ramp[index][0], ramp[index][1], lifts[index]));
            }
            JObject rampFloor = Floor("Ramp B", "L1", 0, ramp, null, "VD2-" + plot);
            rampFloor["corners"] = Value(corners);
            rampFloor["heightOffset"] = Value(9.8, "computed");
            return new JObject
            {
                ["plot"] = plot,
                ["floors"] = new JObject
                {
                    ["generic"] = Floor("Slab G", "L1", 0, Square(x0, 0, x0 + 10, 10), ffl, "VD2-" + plot),
                    ["yard"] = Floor("Yard Y", "L2", 4, Square(x0 - 5, 0, x0, 10), ffl - 0.1, "VD2-" + plot),
                    ["ramp"] = rampFloor,
                },
            };
        }

        internal static JObject PlanJson(params JObject[] plots)
        {
            return new JObject
            {
                ["format"] = PlanFile.FormatName,
                ["formatVersion"] = PlanFile.Version,
                ["surveyPoint"] = new JObject { ["position"] = new JArray(Position), ["sharedPosition"] = new JArray(SharedPosition) },
                ["angleToTrueNorthDeg"] = 0,
                ["prototypeFolder"] = @"C:\Prototypes",
                ["floorSettings"] = new JObject
                {
                    ["types"] = new JObject { ["generic"] = new JArray("Slab G"), ["yard"] = new JArray("Yard Y"), ["ramp"] = new JArray("Ramp A", "Ramp B") },
                    ["levels"] = new JObject { ["generic"] = "L1", ["yard"] = "L2", ["ramp"] = "L1" },
                    ["plotParameter"] = PlotParameter,
                },
                ["plots"] = new JArray(plots),
            };
        }

        private static PlanFile Plan(JObject json)
        {
            return PlanFile.Parse(json.ToString());
        }

        private static PlanFile TwoPlots()
        {
            return Plan(PlanJson(PlotEntry("101", 0), PlotEntry("102", 50)));
        }

        private static FloorReport Run(PlanFile plan, FakeFloors target, bool real = true, ICollection<string> plots = null)
        {
            return FloorPlacer.Run(plan, @"C:\x\plan.json", target, real, plots);
        }

        [Fact]
        public void R113_a_floor_type_the_document_lacks_stops_the_step_before_any_change()
        {
            var target = new FakeFloors();
            target.Types.RemoveAll(type => type.Name == "Ramp B");
            FloorReport report = Run(TwoPlots(), target);
            Assert.True(report.Stopped);
            Assert.Contains("no floor type Ramp B", report.Failure);
            Assert.Equal(0, target.Begins);
            Assert.Empty(target.Created);
            Assert.Equal(report.FloorsBefore, report.FloorsAfter);
        }

        [Fact]
        public void R113_a_missing_level_a_moved_level_or_a_type_of_another_kind_stops_the_step()
        {
            var noLevel = new FakeFloors();
            noLevel.LevelList.RemoveAll(level => level.Name == "L2");
            Assert.Contains("no level L2", Run(TwoPlots(), noLevel).Failure);
            var moved = new FakeFloors();
            moved.LevelList[1].ElevationM = 4.01;
            Assert.Contains("level L2 is at 4.01 m, the plan says 4 m", Run(TwoPlots(), moved).Failure);
            JObject json = PlanJson(PlotEntry("101", 0));
            json["plots"][0]["floors"]["yard"]["type"]["value"] = "Slab G";
            var wrongKind = new FakeFloors();
            FloorReport report = Run(Plan(json), wrongKind);
            Assert.True(report.Stopped);
            Assert.Contains("Slab G is not a yard type of floorSettings", report.Failure);
            Assert.Equal(0, noLevel.Begins + moved.Begins + wrongKind.Begins);
        }

        [Fact]
        public void R113_a_plan_with_no_floor_settings_or_a_plot_list_with_an_unknown_plot_stops()
        {
            JObject json = PlanJson(PlotEntry("101", 0));
            json.Remove("floorSettings");
            Assert.Contains("no floorSettings", Run(Plan(json), new FakeFloors()).Failure);
            JObject noLevels = PlanJson(PlotEntry("101", 0));
            ((JObject)noLevels["floorSettings"]["levels"]).Remove("ramp");
            Assert.Contains("no level for ramp", Run(Plan(noLevels), new FakeFloors()).Failure);
            FloorReport unknown = Run(TwoPlots(), new FakeFloors(), true, new[] { "101", "999" });
            Assert.True(unknown.Stopped);
            Assert.Contains("no plot 999", unknown.Failure);
        }

        [Fact]
        public void R113_coordinates_that_do_not_match_stop_like_the_link_step()
        {
            var target = new FakeFloors();
            target.SurveyValue = new SurveyReading { Position = Position, SharedPosition = new[] { 1000.5, 2000, 5 }, AngleToTrueNorthDeg = 0 };
            FloorReport report = Run(TwoPlots(), target);
            Assert.True(report.Stopped);
            Assert.False(report.Coordinates.Match);
            Assert.Empty(target.Created);
        }

        [Fact]
        public void R114_the_plot_value_is_written_on_generic_yard_and_ramp()
        {
            var target = new FakeFloors();
            FloorReport report = Run(TwoPlots(), target);
            Assert.False(report.Stopped);
            Assert.False(report.RolledBack);
            Assert.Equal(6, report.Counts.Placed);
            Assert.Equal(6, report.Counts.PlotValuesWritten);
            Assert.Equal(new[] { "VD2-101", "VD2-101", "VD2-101", "VD2-102", "VD2-102", "VD2-102" }, target.Created.Values.Select(made => made.Values[PlotParameter]).ToArray());
            Assert.Equal(new Dictionary<string, int> { { "generic", 2 }, { "yard", 2 }, { "ramp", 2 } }, report.Counts.PlacedByKind);
            Assert.Equal(0, report.FloorsBefore);
            Assert.Equal(6, report.FloorsAfter);
            // Twice per run whatever the number of floors
            Assert.Equal(2, target.Regenerations);
        }

        [Fact]
        public void R114_a_floor_of_the_same_kind_and_plot_value_is_skipped_and_a_second_run_places_nothing()
        {
            var target = new FakeFloors();
            target.Existing.Add(new ExistingFloor { Id = 1, TypeName = "Yard Y", PlotValue = "VD2-101" });
            // A floor of another kind with that plot value does not count, nor one of a type floorSettings does not list
            target.Existing.Add(new ExistingFloor { Id = 2, TypeName = "Other", PlotValue = "VD2-102" });
            FloorReport first = Run(TwoPlots(), target);
            Assert.Equal(5, first.Counts.Placed);
            SkippedFloor skipped = Assert.Single(first.Skipped);
            Assert.Equal("101", skipped.Plot);
            Assert.Equal("yard", skipped.Kind);
            Assert.StartsWith("already placed", skipped.Reason);
            Assert.Equal(1, first.FloorsBefore);
            FloorReport second = Run(TwoPlots(), target);
            Assert.Equal(0, second.Counts.Placed);
            Assert.Equal(6, second.Counts.Skipped);
            Assert.All(second.Skipped, item => Assert.StartsWith("already placed", item.Reason));
            Assert.Equal(second.FloorsBefore, second.FloorsAfter);
            Assert.Equal(6, second.FloorsAfter);
        }

        [Fact]
        public void R114_a_plot_value_that_cannot_be_written_rolls_the_whole_step_back()
        {
            var target = new FakeFloors { PlotMissing = true };
            FloorReport report = Run(TwoPlots(), target);
            Assert.True(report.RolledBack);
            Assert.Contains("Plot was not written, Missing", report.Failure);
            Assert.Empty(target.Created);
            Assert.Empty(report.Placed);
        }

        [Fact]
        public void R114_ramp_corners_are_lifted_from_the_lowest_one_to_their_own_z_whatever_order_the_editor_gives()
        {
            var target = new FakeFloors();
            FloorReport report = Run(Plan(PlanJson(PlotEntry("101", 0))), target);
            Assert.False(report.RolledBack, report.Failure);
            Made ramp = target.Created.Values.Single(made => made.Type == "t3");
            Assert.Equal(9.8, ramp.HeightOffset, 9);
            // The editor gives the corners in reverse, so its corner 0 is plan corner 3 at 9.83
            Assert.Equal(new[] { 0.03, 0.02, 0.01, 0.0 }, Enumerable.Range(0, 4).Select(index => Math.Round(ramp.CornerOffsets[index], 9)).ToArray());
        }

        [Fact]
        public void R115_heights_are_the_top_above_the_level_and_the_loop_sits_flat_at_the_level()
        {
            var target = new FakeFloors();
            Run(Plan(PlanJson(PlotEntry("101", 0))), target);
            Made generic = target.Created.Values.Single(made => made.Type == "t1");
            Made yard = target.Created.Values.Single(made => made.Type == "t2");
            Assert.Equal(10.0, generic.HeightOffset, 9);
            // The yard top is the FFL minus 0.1, on a level at 4 m
            Assert.Equal(9.9 - 4, yard.HeightOffset, 9);
            Assert.All(yard.Loops[0].Segments, segment => Assert.Equal(4.0, segment.Start[2]));
            // Internal is shared minus the survey offset, the yard starts at -5, 0
            Assert.Equal(new[] { -5.0, 0, 4 }, yard.Loops[0].Segments[0].Start);
        }

        [Fact]
        public void R115_with_shared_elevation_2_m_above_internal_the_heights_come_down_by_2_m()
        {
            JObject json = PlanJson(PlotEntry("101", 0));
            json["surveyPoint"]["sharedPosition"][2] = 7.0;
            // The plan holds shared level elevations, the document internal ones
            foreach (JToken floor in ((JObject)json["plots"][0]["floors"]).Properties().Select(property => property.Value))
            {
                floor["levelElevation"]["value"] = (double)floor["levelElevation"]["value"] + 2;
                floor["heightOffset"]["value"] = (double)floor["heightOffset"]["value"] - 2;
            }
            var target = new FakeFloors { SurveyValue = new SurveyReading { Position = Position, SharedPosition = new double[] { 1000, 2000, 7 }, AngleToTrueNorthDeg = 0 } };
            FloorReport report = Run(Plan(json), target);
            Assert.False(report.Stopped, report.Failure);
            Assert.False(report.RolledBack, report.Failure);
            Assert.Equal(10.0 - 2, target.Created.Values.Single(made => made.Type == "t1").HeightOffset, 9);
            Assert.Equal(9.8 - 2, target.Created.Values.Single(made => made.Type == "t3").HeightOffset, 9);
        }

        [Fact]
        public void R115_an_arc_stays_an_arc_through_the_middle_of_its_sweep()
        {
            JObject json = PlanJson(PlotEntry("101", 0));
            // Edge 1 of the Generic floor runs from (10, 0) to (10, 10) and bulges out to (15, 5)
            json["plots"][0]["floors"]["generic"]["outline"]["value"]["loops"][0]["arcs"] = new JArray(new JObject
            {
                ["edge"] = 1, ["center"] = new JArray(SharedPosition[0] + 10, SharedPosition[1] + 5), ["radius"] = 5.0, ["ccw"] = true,
            });
            var target = new FakeFloors();
            FloorReport report = Run(Plan(json), target);
            Made generic = target.Created.Values.Single(made => made.Type == "t1");
            FloorSegment arc = generic.Loops[0].Segments[1];
            Assert.NotNull(arc.Mid);
            Assert.Equal(15.0, arc.Mid[0], 9);
            Assert.Equal(5.0, arc.Mid[1], 9);
            Assert.All(new[] { 0, 2, 3 }, edge => Assert.Null(generic.Loops[0].Segments[edge].Mid));
            Assert.Equal(1, report.Placed.Single(floor => floor.Kind == "generic").Arcs);
            double[] clockwise = FloorPlacer.ArcMiddle(new double[] { 10, 0, 0 }, new double[] { 10, 10, 0 }, new double[] { 10, 5, 0 }, 5, false);
            Assert.Equal(5.0, clockwise[0], 9);
        }

        [Fact]
        public void R115_a_loop_with_an_edge_under_1_mm_or_an_arc_on_no_edge_is_skipped_with_its_reason()
        {
            JObject json = PlanJson(PlotEntry("101", 0));
            JArray points = (JArray)json["plots"][0]["floors"]["generic"]["outline"]["value"]["loops"][0]["points"];
            points.Insert(1, new JArray((double)points[0][0] + 0.0005, (double)points[0][1]));
            json["plots"][0]["floors"]["yard"]["outline"]["value"]["loops"][0]["arcs"] = new JArray(new JObject { ["edge"] = 7, ["center"] = new JArray(0, 0), ["radius"] = 1.0, ["ccw"] = true });
            FloorReport report = Run(Plan(json), new FakeFloors(), false);
            Assert.Equal("an outline edge is shorter than 1 mm", report.Skipped.Single(item => item.Kind == "generic").Reason);
            Assert.StartsWith("an outline arc has no edge", report.Skipped.Single(item => item.Kind == "yard").Reason);
        }

        [Fact]
        public void R116_the_dry_run_changes_nothing_and_lists_every_floor_it_would_place()
        {
            var target = new FakeFloors();
            FloorReport report = Run(TwoPlots(), target, false);
            Assert.Equal("dryRun", report.Mode);
            Assert.Equal(0, target.Begins);
            Assert.Empty(target.Created);
            Assert.Equal(6, report.Placed.Count);
            Assert.All(report.Placed, floor => Assert.Null(floor.InstanceId));
            Assert.Equal(0, report.FloorsAfter);
        }

        [Fact]
        public void R116_a_failure_in_the_middle_rolls_back_every_floor_of_the_run()
        {
            var target = new FakeFloors { FailCreateAt = 3 };
            FloorReport report = Run(TwoPlots(), target);
            Assert.True(report.RolledBack);
            Assert.Contains("loop crosses itself", report.Failure);
            Assert.Equal(1, target.RollBacks);
            Assert.Empty(target.Created);
            Assert.Empty(report.Placed);
            Assert.Equal(report.FloorsBefore, report.FloorsAfter);
        }

        [Fact]
        public void R116_a_read_back_off_by_more_than_1_mm_or_a_commit_that_fails_rolls_back()
        {
            var off = new FakeFloors { TopError = 0.002 };
            FloorReport report = Run(TwoPlots(), off);
            Assert.True(report.RolledBack);
            Assert.Contains("does not match the plan", report.Failure);
            Assert.Empty(off.Created);
            var close = new FakeFloors { TopError = 0.0009 };
            Assert.False(Run(TwoPlots(), close).RolledBack);
            var commit = new FakeFloors { CommitFails = true };
            FloorReport failed = Run(TwoPlots(), commit);
            Assert.True(failed.RolledBack);
            Assert.Empty(failed.Placed);
        }

        [Fact]
        public void R116_a_ramp_whose_highest_point_is_not_its_highest_corner_rolls_back()
        {
            var planned = new PlanFloorCheck { Corners = new List<double[]> { new double[] { 0, 0, 9.8 }, new double[] { 2, 0, 9.83 } } };
            var right = new FloorReadBack { TopMax = 9.8304, Corners = new List<double[]> { new double[] { 0, 0, 9.8 }, new double[] { 2, 0, 9.83 } } };
            Assert.Null(FloorPlacer.Mismatch(right, planned));
            var wrong = new FloorReadBack { TopMax = 9.84, Corners = right.Corners };
            Assert.Contains("highest point", FloorPlacer.Mismatch(wrong, planned));
            Assert.Contains("highest point", FloorPlacer.Mismatch(new FloorReadBack { Corners = right.Corners }, planned));
        }

        [Fact]
        public void R116_a_plot_filter_places_only_the_listed_plots_and_counts_the_rest()
        {
            var target = new FakeFloors();
            FloorReport report = Run(Plan(PlanJson(PlotEntry("101", 0), PlotEntry("102", 50), PlotEntry("103", 100))), target, true, new[] { "102" });
            Assert.Equal(3, report.Counts.Placed);
            Assert.All(report.Placed, floor => Assert.Equal("102", floor.Plot));
            Assert.Equal(2, report.Counts.FilteredOut);
            Assert.Equal(new List<string> { "102" }, report.PlotFilter);
        }

        [Fact]
        public void R116_a_plot_without_a_floor_kind_or_listed_twice_is_skipped_with_its_reason()
        {
            JObject json = PlanJson(PlotEntry("101", 0), PlotEntry("101", 50));
            ((JObject)json["plots"][0]["floors"]).Remove("generic");
            FloorReport report = Run(Plan(json), new FakeFloors(), false);
            Assert.Equal("the plan has no generic floor for this plot", report.Skipped.First().Reason);
            Assert.Equal(3, report.Skipped.Count(item => item.Reason.Contains("more than once")));
            Assert.Equal(2, report.Placed.Count);
        }
    }
}
