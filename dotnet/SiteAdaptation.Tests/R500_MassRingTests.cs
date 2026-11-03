using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.PlaceVillas;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R500 to R504, MASS-2NOV-D. The mass ring from made up walls, metres. No Revit, so these run on net48 and net8.0
    public class R500_MassRingTests
    {
        private static readonly double[][] Slab = { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 8.0 }, new[] { 0.0, 8.0 } };

        private static RingLimits Limits(double shortest = 0.004, double sample = 0.05, double parallelSine = 0.001, double onLine = 0.001)
        {
            return new RingLimits { OutsideM = 1.0, InsideM = 0.5, MinHeightM = 2.3, ShortestEdgeM = shortest, CornerGapM = 1.0, SlotM = 0.3, ArcStepM = 0.1, BottomAboveM = 0.1, SampleM = sample, ParallelSine = parallelSine, OnLineM = onLine };
        }

        // A wall whose exterior face runs from a to b, its curve width/2 behind it
        private static RingWall Face(string id, double ax, double ay, double bx, double by, double width, double ex, double ey, double height = 3.0, string function = "Exterior", double bottom = 0)
        {
            return new RingWall
            {
                Id = id, Function = function, Start = new[] { ax - ex * width / 2, ay - ey * width / 2 }, End = new[] { bx - ex * width / 2, by - ey * width / 2 },
                Width = width, Exterior = new[] { ex, ey }, BottomM = bottom, HeightM = height,
            };
        }

        // The four faces of the slab box
        private static List<RingWall> Box(double width = 0.2)
        {
            return new List<RingWall>
            {
                Face("south", 0, 0, 10, 0, width, 0, -1), Face("east", 10, 0, 10, 8, width, 1, 0), Face("north", 10, 8, 0, 8, width, 0, 1), Face("west", 0, 8, 0, 0, width, -1, 0),
            };
        }

        private static WallRingBuild Build(IEnumerable<RingWall> walls, double shortest = 0.004, IReadOnlyList<double[]> slab = null)
        {
            return WallRingBuilder.Build(walls, slab ?? Slab, Limits(shortest));
        }

        private static string Text(IEnumerable<double[]> corners)
        {
            return string.Join(" ", corners.Select(corner => Math.Round(corner[0], 6).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) + "," + Math.Round(corner[1], 6).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).OrderBy(point => point, StringComparer.Ordinal));
        }

        private static List<double[]> Corners(WallRingBuild build)
        {
            Assert.True(build.Reason == null, build.Reason);
            return build.Edges.Select(edge => edge.Start).ToList();
        }

        private static double Area(WallRingBuild build)
        {
            return RingMath.Area(RingEdges.Points(build.Edges, 0.1));
        }

        private static string Why(WallRingBuild build, string id)
        {
            return build.LeftOut.Single(wall => wall.Id == id).Why;
        }

        [Fact]
        public void R502_a_box_at_90_degrees_gives_its_four_corners_exact_and_walks_counter_clockwise()
        {
            WallRingBuild build = Build(Box());
            Assert.Equal(Text(Slab), Text(Corners(build)));
            Assert.Equal(80.0, Area(build), 9);
            Assert.Equal(new[] { "east", "north", "south", "west" }, build.Used);
            Assert.Empty(build.BackFace);
        }

        [Fact]
        public void R502_other_angles_cross_at_the_exact_corner()
        {
            double turn = Math.PI / 6, cos = Math.Cos(turn), sin = Math.Sin(turn);
            double[] Turned(double x, double y) => new[] { x * cos - y * sin, x * sin + y * cos };
            RingWall Tilted(string id, double ax, double ay, double bx, double by, double ex, double ey)
            {
                double[] a = Turned(ax, ay), b = Turned(bx, by), e = Turned(ex, ey);
                return Face(id, a[0], a[1], b[0], b[1], 0.2, e[0], e[1]);
            }
            var walls = new List<RingWall> { Tilted("s", 0, 0, 10, 0, 0, -1), Tilted("e", 10, 0, 10, 8, 1, 0), Tilted("n", 10, 8, 0, 8, 0, 1), Tilted("w", 0, 8, 0, 0, -1, 0) };
            double[][] slab = Slab.Select(point => Turned(point[0], point[1])).ToArray();
            Assert.Equal(Text(slab), Text(Corners(Build(walls, slab: slab))));

            // A 45 degree corner cut off the box
            double root = Math.Sqrt(0.5);
            var cut = new List<RingWall> { Face("s", 0, 0, 8, 0, 0.2, 0, -1), Face("c", 8, 0, 10, 2, 0.2, root, -root), Face("e", 10, 2, 10, 8, 0.2, 1, 0), Face("n", 10, 8, 0, 8, 0.2, 0, 1), Face("w", 0, 8, 0, 0, 0.2, -1, 0) };
            Assert.Equal(Text(new[] { new[] { 0.0, 0.0 }, new[] { 8.0, 0.0 }, new[] { 10.0, 2.0 }, new[] { 10.0, 8.0 }, new[] { 0.0, 8.0 } }), Text(Corners(Build(cut))));
        }

        // The Location Line setting does not move the curve in these files (RAN, 1,906 walls), so the setting is no input
        [Fact]
        public void R501_the_face_lies_half_the_width_from_the_curve_along_the_exterior_vector_and_two_widths_meet_at_one_corner()
        {
            List<RingWall> walls = Box(0.21);
            walls[1] = Face("east", 10, 0, 10, 8, 0.12, 1, 0);
            Assert.Equal(Text(Slab), Text(Corners(Build(walls))));

            // A flip is the other exterior vector on the same curve, the face moves by the width
            RingWall flipped = Face("east", 10, 0, 10, 8, 0.12, 1, 0);
            flipped.Start = new[] { 10.06, 0.0 };
            flipped.End = new[] { 10.06, 8.0 };
            walls[1] = flipped;
            flipped.Exterior = new[] { -1.0, 0.0 };
            WallRingBuild inward = Build(walls);
            Assert.Equal(new[] { "east" }, inward.BackFace);
            Assert.Contains(Corners(inward), corner => Math.Abs(corner[0] - 10.12) < 1e-9);
        }

        [Fact]
        public void R501_an_inner_layer_gives_way_to_the_outer_face_before_it()
        {
            List<RingWall> walls = Box();
            walls.Add(Face("inner south", 0.3, 0.3, 9.7, 0.3, 0.12, 0, -1));
            walls.Add(Face("inner east to the house", 9.7, 0.3, 9.7, 7.7, 0.12, -1, 0));
            WallRingBuild build = Build(walls);
            Assert.Equal(Text(Slab), Text(Corners(build)));
            Assert.Contains("no stretch of its faces sees the outside", Why(build, "inner south"));
            Assert.Contains("no stretch of its faces sees the outside", Why(build, "inner east to the house"));
            Assert.Empty(build.Skipped);
            Assert.Empty(build.BackFace);
        }

        [Fact]
        public void R501_a_one_layer_wall_with_its_exterior_to_the_house_gives_its_far_face()
        {
            List<RingWall> walls = Box();
            walls[3] = Face("party", 0.12, 8, 0.12, 0, 0.12, 1, 0);
            WallRingBuild build = Build(walls);
            Assert.Equal(new[] { "party" }, build.BackFace);
            Assert.Equal(Text(Slab), Text(Corners(build)));
        }

        [Fact]
        public void R500_yard_garden_and_inner_walls_stay_out_and_each_says_why()
        {
            List<RingWall> walls = Box();
            walls.Add(Face("garden", 0, -1.5, 10, -1.5, 0.15, 0, -1));
            walls.Add(Face("low near", 0, -0.5, 4, -0.5, 0.1, 0, -1, 1.8));
            walls.Add(Face("interior", 2, -0.2, 4, -0.2, 0.1, 0, -1, 3.0, "Interior"));
            walls.Add(Face("above", 2, -0.3, 4, -0.3, 0.1, 0, -1, 3.0, "Exterior", 0.15));
            walls.Add(Face("deep", 2, 3, 8, 3, 0.1, 0, -1));
            WallRingBuild build = Build(walls);
            Assert.Equal(Text(Slab), Text(Corners(build)));
            Assert.Contains("wallRingOutsideM", Why(build, "garden"));
            Assert.Contains("wallRingMinHeightM", Why(build, "low near"));
            Assert.Contains("function Interior", Why(build, "interior"));
            Assert.Contains("wallRingBottomAboveM", Why(build, "above"));
            Assert.Contains("wallRingInsideM", Why(build, "deep"));
            Assert.DoesNotContain("deep", build.Used);
        }

        // The copies of a wall in other options of a file: only the main model and the primary of each set stand in what a link shows
        [Fact]
        public void R500_a_wall_of_an_option_that_is_not_primary_is_only_counted_and_a_primary_option_used_is_named()
        {
            List<RingWall> walls = Box();
            walls[3].Option = "Shared <primary>";
            walls[3].OptionPrimary = true;
            RingWall copy = Face("copy", 0, 8, 0, 0, 0.2, -1, 0);
            copy.Option = "Expansion Joint";
            RingWall further = Face("further out", -0.4, 9, -0.4, -1, 0.2, -1, 0);
            further.Option = "10 cm inside";
            walls.Add(copy);
            walls.Add(further);
            WallRingBuild build = Build(walls);
            Assert.Equal(Text(Slab), Text(Corners(build)));
            Assert.Equal(2, build.LeftByOption);
            Assert.Equal(new[] { "Shared <primary>" }, build.PrimaryOptions);
            Assert.DoesNotContain(build.LeftOut, wall => wall.Id == "copy" || wall.Id == "further out");
            JObject check = (JObject)MassFiles.Sidecar("X.rvt", "aa", "key", WallRing.Choose(build, Slab, 0.9, 1.3, 0.1))["check"];
            Assert.Equal(2, (int)check["wallsLeftByOption"]);
            Assert.Equal("Shared <primary>", (string)check["primaryOptions"][0]);
        }

        [Theory]
        [InlineData(false, false, true)]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        public void R500_a_link_shows_the_main_model_and_the_primary_option(bool inOption, bool primary, bool shown)
        {
            Assert.Equal(shown, DesignOptions.Shown(inOption, primary));
        }

        [Fact]
        public void R501_an_edited_profile_stands_on_the_ground_only_where_a_level_line_lies_on_it()
        {
            // Along the bottom to t 2, up, across at 2.5 m to t 4, down, on to the end, the top back, as a real sketch runs. It starts past the curve
            var profile = new List<double[]>
            {
                new[] { -0.1, 0.0, 2.0, 0.0 }, new[] { 2.0, 0.0, 2.0, 2.5 }, new[] { 2.0, 2.5, 4.0, 2.5 }, new[] { 4.0, 2.5, 4.0, 0.0 }, new[] { 4.0, 0.0, 10.0, 0.0 }, new[] { 10.0, 0.0, 10.0, 6.0 }, new[] { 10.0, 6.0, -0.1, 6.0 }, new[] { -0.1, 6.0, -0.1, 0.0 },
            };
            List<double[]> stretches = WallProfiles.GroundStretches(profile, 0.1, 0.001);
            Assert.Equal(2, stretches.Count);
            Assert.Equal(new[] { -0.1, 2.0 }, stretches[0]);
            Assert.Equal(new[] { 4.0, 10.0 }, stretches[1]);

            // A step of the bottom within the limit is still the ground and one stretch, a wall held up over the limit has none
            Assert.Single(WallProfiles.GroundStretches(new[] { new[] { 0.0, 0.0, 2.0, 0.0 }, new[] { 2.0, 0.0, 2.0, 0.075 }, new[] { 2.0, 0.075, 5.0, 0.075 } }, 0.1, 0.001));
            Assert.Empty(WallProfiles.GroundStretches(new[] { new[] { 0.0, 2.5, 5.0, 2.5 } }, 0.1, 0.001));
        }

        // The wall across a recess is cut by its profile, not by its door, so the walls of the recess meet the ring
        [Fact]
        public void R501_a_wall_with_a_gap_at_the_ground_gives_two_stretches_and_the_recess_walls_join_at_8_corners()
        {
            List<RingWall> walls = Box();
            walls.Add(Face("side a", 4, 0, 4, 0.4, 0.2, 1, 0));
            walls.Add(Face("back with its door", 4, 0.4, 6, 0.4, 0.2, 0, -1));
            walls.Add(Face("side b", 6, 0.4, 6, 0, 0.2, -1, 0));
            WallRingBuild across = Build(walls);
            Assert.Equal(4, across.Edges.Count);

            walls[0].Profile = new List<double[]> { new[] { 0.0, 0.0, 4.0, 0.0 }, new[] { 4.0, 0.0, 4.0, 2.5 }, new[] { 4.0, 2.5, 6.0, 2.5 }, new[] { 6.0, 2.5, 6.0, 0.0 }, new[] { 6.0, 0.0, 10.0, 0.0 }, new[] { 10.0, 0.0, 10.0, 6.0 }, new[] { 10.0, 6.0, 0.0, 6.0 }, new[] { 0.0, 6.0, 0.0, 0.0 } };
            WallRingBuild recess = Build(walls);
            Assert.Equal(8, recess.Edges.Count);
            Assert.Equal(79.2, Area(recess), 9);
            Assert.Contains("south", recess.Used);
            Assert.Contains("back with its door", recess.Used);
            Assert.Single(recess.Used, id => id == "south");
        }

        [Fact]
        public void R501_a_wall_whose_profile_never_reaches_the_ground_is_left_out_and_says_so()
        {
            List<RingWall> walls = Box();
            RingWall lintel = Face("lintel", 2, 0.2, 6, 0.2, 0.2, 0, -1);
            lintel.Profile = new List<double[]> { new[] { 0.0, 2.5, 4.0, 2.5 }, new[] { 4.0, 2.5, 4.0, 3.0 }, new[] { 4.0, 3.0, 0.0, 3.0 }, new[] { 0.0, 3.0, 0.0, 2.5 } };
            walls.Add(lintel);
            Assert.Contains("wallRingBottomAboveM", Why(Build(walls), "lintel"));
        }

        // The measure of R504 reads the real faces and nothing the ring was built from
        [Fact]
        public void R504_the_build_measures_each_edge_against_the_real_face_and_counts_connecting_and_unmeasured_edges_apart()
        {
            List<RingWall> walls = Box();
            foreach (RingWall wall in walls)
            {
                wall.ExteriorFace = new[] { wall.Start[0] + wall.Exterior[0] * wall.Width / 2, wall.Start[1] + wall.Exterior[1] * wall.Width / 2, wall.Exterior[0], wall.Exterior[1] };
            }
            walls[1].ExteriorFace = new[] { 10.002, 0.0, 1.0, 0.0 };
            walls[2].ExteriorFace = null;
            WallRingBuild build = Build(walls);
            Assert.Equal(0.002, build.LargestOffFaceM.Value, 9);
            Assert.Equal(1, build.OffFaceUnmeasured);
            Assert.Equal(0, build.ConnectingEdges);

            var stepped = new List<RingWall>
            {
                Face("south", 0, 0, 10, 0, 0.2, 0, -1), Face("east", 10, 0, 10, 8.4, 0.2, 1, 0), Face("north out", 10, 8.4, 6, 8.4, 0.2, 0, 1), Face("north", 6, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
            };
            Assert.Equal(1, Build(stepped).ConnectingEdges);
            Assert.Null(Build(stepped).LargestOffFaceM);
        }

        [Fact]
        public void R504_the_sidecar_holds_the_ring_against_the_slab_also_when_the_slab_was_chosen()
        {
            double[][] wide = { new[] { 0.0, 0.0 }, new[] { 20.0, 0.0 }, new[] { 20.0, 8.0 }, new[] { 0.0, 8.0 } };
            WallRingBuild over = new WallRingBuild { Edges = Enumerable.Range(0, 4).Select(i => new RingEdge(wide[i], wide[(i + 1) % 4])).ToList() };
            MassBase chosen = WallRing.Choose(over, Slab, 0.9, 1.3, 0.1);
            Assert.Equal(WallRing.FromSlab, chosen.Source);
            JObject check = (JObject)MassFiles.Sidecar("X.rvt", "aa", "key", chosen)["check"];
            Assert.Equal(160.0, (double)check["ringAreaM2"], 9);
            Assert.Equal(80.0, (double)check["slabAreaM2"], 9);
            Assert.Equal(2.0, (double)check["share"], 9);
        }

        // The outer skin of a house can start at the FFL, 75 mm over the plane of the SSL slab, with the inner skin on the plane. The ring takes the skin
        [Fact]
        public void R500_an_outer_wall_based_at_the_ffl_is_kept_and_the_inner_skin_gives_way_to_it()
        {
            var walls = new List<RingWall>
            {
                Face("outer south", 0, 0, 10, 0, 0.2, 0, -1, 3.0, "Exterior", 0.075), Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north", 10, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
                Face("inner south", 0.3, 0.3, 9.7, 0.3, 0.12, 0, -1),
            };
            WallRingBuild build = Build(walls);
            Assert.Contains("outer south", build.Used);
            Assert.Equal(Text(Slab), Text(Corners(build)));
            walls[0] = Face("outer south", 0, 0, 10, 0, 0.2, 0, -1, 3.0, "Exterior", 0.5);
            Assert.Contains("wallRingBottomAboveM", Why(Build(walls), "outer south"));
        }

        // A notch the slab does not follow: its walls stand far inside the slab at one end and reach its edge at the other
        [Fact]
        public void R500_a_notch_the_slab_does_not_follow_stays_by_the_end_that_reaches_the_slab_edge()
        {
            var walls = new List<RingWall>
            {
                Face("south", 3, 0, 10, 0, 0.2, 0, -1), Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north", 10, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 3, 0.2, -1, 0),
                Face("notch top", 0, 3, 3, 3, 0.2, 0, -1), Face("notch side", 3, 3, 3, 0, 0.2, -1, 0),
            };
            WallRingBuild build = Build(walls);
            Assert.Equal(6, Corners(build).Count);
            Assert.Equal(80.0 - 9.0, Area(build), 9);
        }

        [Fact]
        public void R502_a_ring_that_does_not_close_says_at_which_wall_and_the_slab_is_flagged_with_that_reason()
        {
            List<RingWall> walls = Box();
            walls.RemoveAt(3);
            WallRingBuild build = Build(walls);
            Assert.Empty(build.Edges);
            Assert.Contains("do not close", build.Reason);
            Assert.Contains("wall north", build.Reason);
            MassBase chosen = WallRing.Choose(build, Slab, 0.9, 1.3, 0.1);
            Assert.True(chosen.Flagged);
            Assert.Equal(WallRing.FromSlab, chosen.Source);
            Assert.Equal(build.Reason, chosen.Reason);
            Assert.Equal(Slab.Length, chosen.Ring.Count);
            Assert.Equal(Slab.Length, chosen.Edges.Count);
        }

        [Fact]
        public void R502_collinear_pieces_of_any_width_are_one_edge_with_all_their_walls()
        {
            var walls = new List<RingWall>
            {
                Face("piece", 0, 0, 0.08, 0, 0.21, 0, -1), Face("copy of it", 0, 0, 0.08, 0, 0.21, 0, -1), Face("long", 0.08, 0, 6, 0, 0.21, 0, -1), Face("other width", 6, 0, 10, 0, 0.12, 0, -1),
                Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north", 10, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
            };
            WallRingBuild build = Build(walls);
            Assert.Equal(4, build.Edges.Count);
            Assert.Equal(Text(Slab), Text(Corners(build)));
            Assert.Contains("piece", build.Used);
            Assert.Contains("copy of it", build.Used);
            Assert.Contains("other width", build.Used);
        }

        [Fact]
        public void R502_a_wall_that_runs_past_a_corner_or_stops_short_of_it_still_gives_the_crossing()
        {
            var walls = new List<RingWall>
            {
                Face("past", -0.3, 0, 10.4, 0, 0.2, 0, -1), Face("short", 10, 0.6, 10, 7.5, 0.2, 1, 0), Face("north", 9.7, 8, 0.4, 8, 0.2, 0, 1), Face("west", 0, 8.2, 0, -0.2, 0.2, -1, 0),
            };
            Assert.Equal(Text(Slab), Text(Corners(Build(walls))));
        }

        [Fact]
        public void R502_a_corner_further_than_the_corner_gap_does_not_close()
        {
            List<RingWall> walls = Box();
            walls[1] = Face("east", 10, 1.5, 10, 8, 0.2, 1, 0);
            WallRingBuild build = Build(walls);
            Assert.Contains("do not close", build.Reason);
            Assert.Contains("wall south", build.Reason);
        }

        [Fact]
        public void R502_a_wall_on_the_line_of_an_edge_but_past_its_corner_is_not_used()
        {
            List<RingWall> walls = Box();
            walls.Add(Face("past", -0.3, 0, 0, 0, 0.2, 0, -1));
            WallRingBuild build = Build(walls);
            Assert.Equal(Text(Slab), Text(Corners(build)));
            Assert.DoesNotContain("past", build.Used);
            Assert.Contains("past a corner", Why(build, "past"));
        }

        // A fin clear of the walk: the ring stands, and the wall it passed by is named as skipped, not as shut in
        [Fact]
        public void R502_a_wall_the_walk_passed_by_is_named_as_skipped_and_reaches_the_sidecar()
        {
            List<RingWall> walls = Box();
            walls.Add(Face("fin", 4, -0.5, 5, -0.5, 0.1, 0, -1));
            WallRingBuild build = Build(walls);
            Assert.Null(build.Reason);
            Assert.Equal("fin", build.Skipped.Single().Id);
            Assert.Contains("walk passed it by", Why(build, "fin"));
            MassBase chosen = WallRing.Choose(build, Slab, 0.9, 1.3, 0.1);
            Assert.Equal(new[] { "fin" }, chosen.Skipped);
            Assert.Equal(new[] { "fin" }, MassFiles.Base(MassFiles.Sidecar("X.rvt", "aa", "key", chosen)).Skipped);
        }

        // A merge that leaves a corner far from the wall ends it should join has not closed the ring, whatever the crossing says
        [Fact]
        public void R503_a_corner_a_merge_moved_past_the_corner_gap_does_not_close()
        {
            var walls = new List<RingWall>
            {
                Face("south", 0.02, 0, 10, 0, 0.2, 0, -1), Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north", 10, 8, 0, 8, 0.2, 0, 1), Face("west long", 0, 8, 0, 3, 0.2, -1, 0), Face("west short", 0.02, 3, 0.02, 0, 0.2, -1, 0),
            };
            Assert.Null(Build(walls).Reason);
            Assert.Contains("stands over wallRingCornerGapM", Build(walls, 0.05).Reason);
        }

        [Fact]
        public void R503_an_arc_over_half_a_turn_is_kept()
        {
            double root = Math.Sqrt(2);
            var arc = new RingWall
            {
                Id = "arc", Function = "Exterior", Width = 0.2, Centre = new[] { 0.0, 0.0 }, CounterClockwise = true, HeightM = 3,
                Start = new[] { 1.9 * Math.Cos(Math.PI / 4), 1.9 * Math.Sin(Math.PI / 4) }, End = new[] { 1.9 * Math.Cos(-Math.PI / 4), 1.9 * Math.Sin(-Math.PI / 4) }, Exterior = new[] { -1.0, 0.0 },
            };
            // The two lines leave the circle along its tangent and meet at a right angle
            var walls = new List<RingWall> { arc, LineFace("lower", root, -root, 2 * root, 0), LineFace("upper", 2 * root, 0, root, root) };
            double[][] slab = { new[] { -2.0, -2.0 }, new[] { 2 * root, -2.0 }, new[] { 2 * root, 2.0 }, new[] { -2.0, 2.0 } };
            WallRingBuild build = Build(walls, slab: slab);
            Assert.Null(build.Reason);
            Assert.Equal(3, build.Edges.Count);
            Assert.Contains("arc", build.Used);
            Assert.Equal(0.75 * Math.PI * 4 + 4, RingMath.Area(RingEdges.Points(build.Edges, 0.01)), 2);
        }

        // A straight wall with its exterior on the right of a to b
        private static RingWall LineFace(string id, double ax, double ay, double bx, double by)
        {
            double length = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            return Face(id, ax, ay, bx, by, 0.2, (by - ay) / length, -(bx - ax) / length);
        }

        // Each tuned value moves a result. Two faces 0.5 mm apart are one line or a step by wallRingOnLineM
        [Fact]
        public void R502_the_on_line_limit_decides_when_two_faces_are_one_line()
        {
            var walls = new List<RingWall>
            {
                Face("south", 0, 0, 10, 0, 0.2, 0, -1), Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north a", 10, 8, 5, 8, 0.2, 0, 1), Face("north b", 5, 8.0005, 0, 8.0005, 0.2, 0, 1), Face("west", 0, 8.0005, 0, 0, 0.2, -1, 0),
            };
            Assert.Equal(4, WallRingBuilder.Build(walls, Slab, Limits(0.0001)).Edges.Count);
            Assert.Equal(6, WallRingBuilder.Build(walls, Slab, Limits(0.0001, onLine: 0.0001)).Edges.Count);
        }

        [Fact]
        public void R502_the_parallel_limit_decides_when_two_faces_cross()
        {
            double tilt = 0.0005;
            var walls = new List<RingWall>
            {
                Face("south", 0, 0, 10, 0, 0.2, 0, -1), Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north a", 10, 8, 5, 8, 0.2, 0, 1), Face("north b", 5, 8, 0, 8 + 5 * tilt, 0.2, tilt, 1), Face("west", 0, 8 + 5 * tilt, 0, 0, 0.2, -1, 0),
            };
            Assert.Equal(4, WallRingBuilder.Build(walls, Slab, Limits(0.0001)).Edges.Count);
            Assert.Equal(5, WallRingBuilder.Build(walls, Slab, Limits(0.0001, parallelSine: 0.0001)).Edges.Count);
        }

        // A pier close before the middle of the south face: tried in one long piece the face shows nothing, in short pieces it shows both sides of the pier
        [Fact]
        public void R501_the_sample_length_decides_what_stretch_of_a_face_is_seen()
        {
            List<RingWall> walls = Box();
            walls.Add(Face("pier", 4, -0.2, 6, -0.2, 0.2, 0, -1));
            Assert.Null(WallRingBuilder.Build(walls, Slab, Limits()).Reason);
            Assert.NotNull(WallRingBuilder.Build(walls, Slab, Limits(sample: 20)).Reason);
        }

        [Fact]
        public void R502_parallel_neighbours_at_a_step_get_a_connecting_edge_the_outer_keeping_its_length()
        {
            // The north face steps out by 0.4 from x 6 on, walking west it steps in, walking east it would step out
            var walls = new List<RingWall>
            {
                Face("south", 0, 0, 10, 0, 0.2, 0, -1), Face("east", 10, 0, 10, 8.4, 0.2, 1, 0), Face("north out", 10, 8.4, 6, 8.4, 0.2, 0, 1), Face("north", 6.5, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
            };
            WallRingBuild build = Build(walls);
            Assert.Equal(Text(new[] { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 8.4 }, new[] { 6.0, 8.4 }, new[] { 6.0, 8.0 }, new[] { 0.0, 8.0 } }), Text(Corners(build)));
        }

        [Fact]
        public void R502_a_recess_whose_side_walls_face_each_other_stays_in_the_ring()
        {
            var walls = new List<RingWall>
            {
                Face("south a", 0, 0, 4, 0, 0.2, 0, -1), Face("side a", 4, 0, 4, 0.4, 0.2, 1, 0), Face("back", 4, 0.4, 6, 0.4, 0.2, 0, -1), Face("side b", 6, 0.4, 6, 0, 0.2, -1, 0), Face("south b", 6, 0, 10, 0, 0.2, 0, -1),
                Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north", 10, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
            };
            WallRingBuild build = Build(walls);
            Assert.Equal(8, build.Edges.Count);
            Assert.Equal(79.2, Area(build), 9);
        }

        [Fact]
        public void R502_a_pier_beside_the_house_is_walked_round_and_the_narrow_slot_behind_it_stays_shut()
        {
            // The pier stands 0.4 off the east wall for its lower 2 m, the slot between is under wallRingSlotM
            var walls = new List<RingWall>
            {
                Face("south", 0, 0, 10.4, 0, 0.2, 0, -1), Face("pier out", 10.4, 0, 10.4, 2, 0.2, 1, 0), Face("pier in", 10.2, 2, 10.2, 0.1, 0.1, -1, 0),
                Face("east", 10, 0.1, 10, 8, 0.1, 1, 0), Face("north", 10, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
            };
            WallRingBuild build = Build(walls);
            Assert.Equal(Text(new[] { new[] { 0.0, 0.0 }, new[] { 10.4, 0.0 }, new[] { 10.4, 2.0 }, new[] { 10.0, 2.0 }, new[] { 10.0, 8.0 }, new[] { 0.0, 8.0 } }), Text(Corners(build)));
        }

        [Fact]
        public void R503_an_edge_under_the_shortest_edge_goes_into_its_neighbour_and_over_it_stays()
        {
            var walls = new List<RingWall>
            {
                Face("south", 0, 0, 10, 0, 0.2, 0, -1), Face("east", 10, 0, 10, 8, 0.2, 1, 0), Face("north long", 10, 8.02, 0.4, 8.02, 0.2, 0, 1), Face("north short", 0.4, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
            };
            WallRingBuild kept = Build(walls, 0.01);
            Assert.Equal(6, kept.Edges.Count);
            WallRingBuild merged = Build(walls, 0.05);
            Assert.Equal(4, merged.Edges.Count);
            Assert.Contains(Corners(merged), corner => Math.Abs(corner[1] - 8.02) < 1e-9 && Math.Abs(corner[0]) < 1e-9);
            Assert.DoesNotContain("north short", merged.Used);
            Assert.Contains("the ring does not touch it", Why(merged, "north short"));
            Assert.Empty(merged.Skipped);
        }

        [Fact]
        public void R503_a_chamfer_under_the_shortest_edge_goes_and_its_neighbours_cross()
        {
            double root = Math.Sqrt(0.5);
            var walls = new List<RingWall>
            {
                Face("south", 0, 0, 9.98, 0, 0.2, 0, -1), Face("chamfer", 9.98, 0, 10, 0.02, 0.1, root, -root), Face("east", 10, 0.02, 10, 8, 0.2, 1, 0), Face("north", 10, 8, 0, 8, 0.2, 0, 1), Face("west", 0, 8, 0, 0, 0.2, -1, 0),
            };
            Assert.Equal(Text(Slab), Text(Corners(Build(walls, 0.05))));
        }

        [Fact]
        public void R503_arcs_stay_arcs_and_cut_into_points_only_for_a_reader_of_points()
        {
            // A stadium: two straight faces and two half circles of radius 2 at the face, each wall with its centre line 0.1 inside
            RingWall Arc(string id, double cx, double from, double to, double ex)
            {
                return new RingWall
                {
                    Id = id, Function = "Exterior", Width = 0.2, Centre = new[] { cx, 0.0 }, CounterClockwise = true, HeightM = 3,
                    Start = new[] { cx + 1.9 * Math.Cos(from), 1.9 * Math.Sin(from) }, End = new[] { cx + 1.9 * Math.Cos(to), 1.9 * Math.Sin(to) }, Exterior = new[] { ex, 0.0 },
                };
            }
            var walls = new List<RingWall>
            {
                Face("south", 0, -2, 6, -2, 0.2, 0, -1), Arc("right", 6, -Math.PI / 2, Math.PI / 2, 1), Face("north", 6, 2, 0, 2, 0.2, 0, 1), Arc("left", 0, Math.PI / 2, 3 * Math.PI / 2, -1),
            };
            double[][] slab = { new[] { -2.0, -2.0 }, new[] { 8.0, -2.0 }, new[] { 8.0, 2.0 }, new[] { -2.0, 2.0 } };
            WallRingBuild build = Build(walls, slab: slab);
            Assert.Null(build.Reason);
            Assert.Equal(4, build.Edges.Count);
            Assert.Equal(2, build.Edges.Count(edge => edge.IsArc));
            RingEdge right = build.Edges.Single(edge => edge.IsArc && edge.Centre[0] > 3);
            Assert.True(right.CounterClockwise);
            Assert.Equal(2.0, Math.Sqrt(Math.Pow(right.Start[0] - 6, 2) + right.Start[1] * right.Start[1]), 9);
            Assert.Equal(8.0, RingEdges.Middle(right)[0], 9);

            List<double[]> points = RingEdges.Points(build.Edges, 0.1);
            Assert.True(points.Count > 100);
            Assert.Equal(6 * 4 + Math.PI * 4, RingMath.Area(points), 1);

            MassBase chosen = WallRing.Choose(build, slab, 0.9, 1.3, 0.1);
            Assert.Equal(WallRing.FromWalls, chosen.Source);
            Assert.Equal(2, chosen.Edges.Count(edge => edge.IsArc));

            JObject sidecar = MassFiles.Sidecar("X.rvt", "aa", "key", chosen);
            MassBase read = MassFiles.Base(sidecar);
            Assert.Equal(2, read.Edges.Count(edge => edge.IsArc));
            Assert.Equal(chosen.Ring.Count, read.Ring.Count);
            Assert.Equal(right.Centre, read.Edges.Single(edge => edge.IsArc && edge.Centre[0] > 3).Centre);
        }

        [Fact]
        public void R503_a_ring_drawn_clockwise_is_turned_for_the_solid_its_arcs_with_it()
        {
            var clockwise = new List<RingEdge>
            {
                new RingEdge(new[] { -1.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 0.0 }, false), new RingEdge(new[] { 1.0, 0.0 }, new[] { -1.0, 0.0 }),
            };
            List<RingEdge> turned = RingEdges.CounterClockwise(clockwise);
            Assert.True(RingMath.Area(RingEdges.Points(turned, 0.1)) > 0);
            Assert.True(turned[1].CounterClockwise);
            Assert.Equal(1.0, turned[1].Start[0]);
        }

        private static WallRingBuild RingOf(params double[][] corners)
        {
            return new WallRingBuild { Edges = Enumerable.Range(0, corners.Length).Select(i => new RingEdge(corners[i], corners[(i + 1) % corners.Length])).ToList() };
        }

        [Fact]
        public void R504_the_area_share_holds_at_both_ends_and_past_them_the_slab_is_flagged_with_the_share()
        {
            double[][] Wide(double share) => new[] { new[] { 0.0, 0.0 }, new[] { 10.0 * share, 0.0 }, new[] { 10.0 * share, 8.0 }, new[] { 0.0, 8.0 } };
            Assert.Equal(WallRing.FromWalls, WallRing.Choose(RingOf(Wide(0.9)), Slab, 0.9, 1.3, 0.1).Source);
            Assert.Equal(WallRing.FromWalls, WallRing.Choose(RingOf(Wide(1.3)), Slab, 0.9, 1.3, 0.1).Source);
            MassBase small = WallRing.Choose(RingOf(Wide(0.89)), Slab, 0.9, 1.3, 0.1);
            Assert.True(small.Flagged);
            Assert.Contains("under wallRingMinShare", small.Reason);
            MassBase big = WallRing.Choose(RingOf(Wide(1.31)), Slab, 0.9, 1.3, 0.1);
            Assert.True(big.Flagged);
            Assert.Contains("over wallRingMaxShare", big.Reason);
        }

        [Fact]
        public void R504_a_ring_that_crosses_itself_or_misses_the_slab_middle_is_flagged_with_why()
        {
            MassBase crossed = WallRing.Choose(RingOf(new[] { 0.0, 0.0 }, new[] { 10.0, 8.0 }, new[] { 10.0, 0.0 }, new[] { 0.0, 8.0 }), Slab, 0.0, 9.0, 0.1);
            Assert.True(crossed.Flagged);
            Assert.Contains("crosses itself", crossed.Reason);
            MassBase away = WallRing.Choose(RingOf(new[] { 20.0, 0.0 }, new[] { 30.0, 0.0 }, new[] { 30.0, 8.0 }, new[] { 20.0, 8.0 }), Slab, 0.9, 1.3, 0.1);
            Assert.True(away.Flagged);
            Assert.Contains("middle of the slab", away.Reason);
            Assert.Contains("no exterior wall", WallRing.Choose(new WallRingBuild(), Slab, 0.9, 1.3, 0.1).Reason);
        }

        private static JObject Block()
        {
            return new JObject
            {
                ["villaModeDefault"] = "masses", ["solidHeightM"] = 3.0, ["baseBoundary"] = "lowestFloor", ["footprintLimitMm"] = 100.0, ["footprintSampleStepMm"] = 10.0, ["linkFolder"] = "masses", ["massFolder"] = "C:\\masses",
                ["wallRingMinShare"] = 0.9, ["wallRingMaxShare"] = 1.3, ["wallRingOutsideM"] = 1.0, ["wallRingInsideM"] = 0.5, ["wallRingMinHeightM"] = 2.3, ["wallRingShortestEdgeM"] = 0.05, ["wallRingCornerGapM"] = 1.0, ["wallRingSlotM"] = 0.3, ["wallRingArcStepM"] = 0.1, ["wallRingBottomAboveM"] = 0.1, ["wallRingSampleM"] = 0.05, ["wallRingParallelSine"] = 0.001, ["wallRingOnLineM"] = 0.001,
            };
        }

        private static LightModeConfig Config(JObject block)
        {
            return LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = block } });
        }

        [Theory]
        [InlineData("wallRingOutsideM")]
        [InlineData("wallRingInsideM")]
        [InlineData("wallRingMinHeightM")]
        [InlineData("wallRingShortestEdgeM")]
        [InlineData("wallRingCornerGapM")]
        [InlineData("wallRingSlotM")]
        [InlineData("wallRingArcStepM")]
        [InlineData("wallRingBottomAboveM")]
        [InlineData("wallRingSampleM")]
        [InlineData("wallRingParallelSine")]
        [InlineData("wallRingOnLineM")]
        public void R504_each_limit_is_read_from_config_refused_by_name_when_missing_and_in_the_source_key(string key)
        {
            string before = MassFiles.SourceKey(Config(Block()));
            JObject moved = Block();
            moved[key] = 0.777;
            Assert.NotEqual(before, MassFiles.SourceKey(Config(moved)));
            JObject missing = Block();
            missing.Remove(key);
            Assert.Contains(key, Assert.Throws<InvalidDataException>(() => Config(missing)).Message);
        }

        // A mass of an earlier reader was made by another rule, it must be made again
        [Fact]
        public void R504_a_sidecar_of_the_reader_before_is_stale()
        {
            JObject sidecar = MassFiles.Sidecar("X.rvt", "aa", "key", new MassBase(Slab, WallRing.FromSlab, "test"));
            Assert.False(MassFiles.Stale(sidecar, "aa", "key"));
            sidecar["readerVersion"] = MassFiles.ReaderVersion - 1;
            Assert.True(MassFiles.Stale(sidecar, "aa", "key"));
        }

        [Fact]
        public void R500_the_sidecar_says_which_walls_the_ring_took_and_which_it_left_out_and_why()
        {
            List<RingWall> walls = Box();
            walls.Add(Face("garden", 0, -1.5, 10, -1.5, 0.15, 0, -1));
            MassBase chosen = WallRing.Choose(Build(walls), Slab, 0.9, 1.3, 0.1);
            JObject sidecar = MassFiles.Sidecar("X.rvt", "aa", "key", chosen);
            Assert.Equal(4, ((JArray)sidecar["wallsUsed"]).Count);
            Assert.Equal("garden", (string)sidecar["wallsLeftOut"][0]["id"]);
            Assert.Contains("wallRingOutsideM", (string)sidecar["wallsLeftOut"][0]["why"]);
            Assert.Empty((JArray)sidecar["wallsBackFace"]);
        }
    }
}
