using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;
using Xunit.Abstractions;

namespace SiteAdaptation.Tests
{
    // Shared coordinates, floor measurements, parameter details, link state, spot hosts and the output size.
    // Test doubles prove the logic and the JSON shape. They say nothing about what Revit returns
    public class FloorLinkRelationTests
    {
        private readonly ITestOutputHelper output;

        public FloorLinkRelationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static double[] P(double x, double y, double z = 0)
        {
            return new[] { x, y, z };
        }

        private static BoundaryEdge Edge(double x1, double y1, double x2, double y2, double z = 0)
        {
            return new BoundaryEdge { Start = P(x1, y1, z), End = P(x2, y2, z) };
        }

        private static List<BoundaryEdge> Square(double x0, double y0, double x1, double y1)
        {
            return new List<BoundaryEdge>
            {
                Edge(x0, y0, x1, y0),
                Edge(x1, y0, x1, y1),
                Edge(x1, y1, x0, y1),
                Edge(x0, y1, x0, y0),
            };
        }

        private static string[] Errors(JObject document)
        {
            return document["errors"] == null ? new string[0] : document["errors"].Select(line => (string)line).ToArray();
        }

        // R51 shared coordinates

        [Fact]
        public void R51_offset_is_shared_position_minus_position_on_each_axis()
        {
            double[] offset = SharedCoordinates.Offset(new[] { 10.0, 20, 5 }, new[] { 110.5, 18, 5 });

            Assert.Equal(new[] { 100.5, -2, 0 }, offset);
        }

        [Fact]
        public void R51_feet_become_metres_and_the_offset_is_added_on_each_axis()
        {
            double[] shared = SharedCoordinates.ToShared(new[] { UnitMath.FeetPerMetre, 2 * UnitMath.FeetPerMetre, 0 }, new[] { 100.0, -50, 0 });

            Assert.Equal(101, shared[0], 9);
            Assert.Equal(-48, shared[1], 9);
            Assert.Equal(0, shared[2], 9);
        }

        [Fact]
        public void R51_a_point_already_in_metres_is_shifted_by_the_offset()
        {
            Assert.Equal(new[] { 101.0, 52, 8 }, SharedCoordinates.Shift(new[] { 1.0, 2, 8 }, new[] { 100.0, 50, 0 }));
        }

        [Fact]
        public void R51_unit_change_covers_length_area_volume_and_angle()
        {
            Assert.Equal(1, UnitMath.FromInternal(UnitMath.FeetPerMetre, ValueUnit.Length), 9);
            Assert.Equal(1, UnitMath.FromInternal(UnitMath.FeetPerMetre * UnitMath.FeetPerMetre, ValueUnit.Area), 9);
            Assert.Equal(1, UnitMath.FromInternal(Math.Pow(UnitMath.FeetPerMetre, 3), ValueUnit.Volume), 9);
            Assert.Equal(180, UnitMath.FromInternal(Math.PI, ValueUnit.Angle), 9);
            Assert.Equal(7.5, UnitMath.FromInternal(7.5, ValueUnit.None));
            Assert.Equal(new[] { "m", "m2", "m3", "deg", null }, new[] { ValueUnit.Length, ValueUnit.Area, ValueUnit.Volume, ValueUnit.Angle, ValueUnit.None }.Select(UnitMath.UnitName));
        }

        [Fact]
        public void R51_box_corners_are_eight_points_bottom_four_then_top_four()
        {
            List<double[]> corners = BoxMath.Corners(P(0, 0, 1), P(4, 3, 2));

            Assert.Equal(8, corners.Count);
            Assert.Equal(new[] { 0.0, 0, 1 }, corners[0]);
            Assert.Equal(new[] { 4.0, 3, 1 }, corners[2]);
            Assert.Equal(new[] { 0.0, 0, 2 }, corners[4]);
            Assert.Equal(new[] { 0.0, 3, 2 }, corners[7]);
            Assert.Equal(4, corners.Count(corner => corner[2] == 1));
        }

        // R52 floor measurements

        [Fact]
        public void R52_elevation_range_is_lowest_and_highest_z_and_no_points_give_none()
        {
            ElevationRange range = ElevationRange.Of(new[] { P(0, 0, 83.1), P(4, 0, 83.25), P(4, 3, 83.3), P(0, 3, 83.15) });

            Assert.Equal(83.1, range.Min);
            Assert.Equal(83.3, range.Max);
            Assert.Null(ElevationRange.Of(new double[0][]));
        }

        [Fact]
        public void R52_a_bottom_face_is_one_that_looks_down_and_a_side_face_is_not()
        {
            Assert.True(FloorMath.IsBottomFace(-1));
            Assert.True(FloorMath.IsBottomFace(-0.9));
            Assert.False(FloorMath.IsBottomFace(0));
            Assert.False(FloorMath.IsBottomFace(-0.0005));
            Assert.False(FloorMath.IsBottomFace(1));
        }

        [Fact]
        public void R52_outline_of_one_rectangle_is_one_closed_loop_of_four_points()
        {
            int open;
            List<List<double[]>> loops = OutlineLoops.Build(Square(0, 0, 4, 3), out open);

            Assert.Single(loops);
            Assert.Equal(0, open);
            Assert.Equal(4, loops[0].Count);
            Assert.Equal(new[] { 0.0, 0, 0 }, loops[0][0]);
        }

        [Fact]
        public void R52_an_edge_found_on_two_faces_is_inside_the_outline_and_is_dropped()
        {
            var edges = new List<BoundaryEdge>
            {
                Edge(0, 0, 4, 0), Edge(4, 0, 4, 3), Edge(4, 3, 0, 0),
                Edge(0, 0, 4, 3), Edge(4, 3, 0, 3), Edge(0, 3, 0, 0),
            };
            int open;
            List<List<double[]>> loops = OutlineLoops.Build(edges, out open);

            Assert.Single(loops);
            Assert.Equal(4, loops[0].Count);
            Assert.Equal(0, open);
            double area = Math.Abs(FloorMath.SignedArea(loops[0]));
            Assert.Equal(12, area, 9);
        }

        [Fact]
        public void R52_a_shared_edge_is_dropped_whichever_way_the_second_face_runs_it()
        {
            var edges = new List<BoundaryEdge>
            {
                Edge(0, 0, 4, 0), Edge(4, 0, 4, 3), Edge(4, 3, 0, 0),
                Edge(4, 3, 0, 0), Edge(0, 3, 4, 3), Edge(0, 0, 0, 3),
            };
            int open;
            List<List<double[]>> loops = OutlineLoops.Build(edges, out open);

            Assert.Single(loops);
            Assert.Equal(12, Math.Abs(FloorMath.SignedArea(loops[0])), 9);
        }

        [Fact]
        public void R52_a_hole_is_its_own_loop()
        {
            var edges = Square(0, 0, 10, 10);
            edges.AddRange(Square(3, 3, 6, 6));
            int open;
            List<List<double[]>> loops = OutlineLoops.Build(edges, out open);

            Assert.Equal(2, loops.Count);
            Assert.Equal(0, open);
            Assert.Equal(new[] { 100.0, 9 }, loops.Select(loop => Math.Abs(FloorMath.SignedArea(loop))).OrderByDescending(area => area).ToArray());
        }

        [Fact]
        public void R52_points_cut_out_of_a_curve_stay_in_the_loop_in_order_even_when_the_edge_runs_backwards()
        {
            var arc = new BoundaryEdge { Start = P(4, 0), End = P(0, 0), Via = new List<double[]> { P(3, -0.5), P(2, -0.7), P(1, -0.5) } };
            var edges = new List<BoundaryEdge> { Edge(0, 3, 0, 0), arc, Edge(4, 0, 4, 3), Edge(4, 3, 0, 3) };
            int open;
            List<List<double[]>> loops = OutlineLoops.Build(edges, out open);

            Assert.Single(loops);
            Assert.Equal(new[] { 0.0, 3, 0 }, loops[0][0]);
            Assert.Equal(new[] { 0.0, 0, 0 }, loops[0][1]);
            Assert.Equal(new[] { 1.0, -0.5, 0 }, loops[0][2]);
            Assert.Equal(new[] { 3.0, -0.5, 0 }, loops[0][4]);
            Assert.Equal(7, loops[0].Count);
        }

        [Fact]
        public void R52_a_chain_that_does_not_come_back_is_counted_and_not_written()
        {
            var edges = Square(0, 0, 4, 3).Take(3).ToList();
            int open;
            List<List<double[]>> loops = OutlineLoops.Build(edges, out open);

            Assert.Empty(loops);
            Assert.Equal(1, open);
        }

        [Fact]
        public void R52_end_points_within_a_millimetre_join_and_points_further_apart_do_not()
        {
            int open;
            var close = new List<BoundaryEdge> { Edge(0, 0, 4, 0), Edge(4.0004, 0, 4, 3), Edge(4, 3.0004, 0, 3), Edge(0, 3, 0.0004, 0) };
            Assert.Single(OutlineLoops.Build(close, out open));
            Assert.Equal(0, open);

            var far = new List<BoundaryEdge> { Edge(0, 0, 4, 0), Edge(4.01, 0, 4, 3), Edge(4, 3, 0, 3), Edge(0, 3, 0, 0) };
            Assert.Empty(OutlineLoops.Build(far, out open));
            Assert.True(open >= 1);
        }

        private static FloorRow Measured(long id)
        {
            return new FloorRow
            {
                TypeName = "Generic 150mm",
                ElementId = id,
                Level = "Level 1",
                HeightOffset = 0.1,
                TopElevation = 83.2,
                SketchBoundary = new List<double[]> { P(0, 0, 150), P(10, 0, 150), P(10, 5, 150), P(0, 5, 150) },
                BoundaryEnds = new List<double[]> { P(0, 0, 150), P(10, 0, 150), P(10, 5, 150), P(0, 5, 150) },
                Parameters = new Dictionary<string, object> { { "Plot", "VD2-2441" } },
            };
        }

        private static JToken OneFloor(FloorRow floor, out JObject document)
        {
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.Add(floor);
            document = LinkWallRampTests.Run(host);
            return document["floors"].Single();
        }

        [Fact]
        public void R52_floor_measurements_are_written_in_shared_metres()
        {
            FloorRow floor = Measured(1);
            floor.TypeId = 77;
            floor.LevelElevation = 83.1;
            floor.TopElevationRange = new ElevationRange { Min = 83.2, Max = 83.3 };
            floor.BottomElevationRange = new ElevationRange { Min = 83.05, Max = 83.15 };
            floor.BoundingBox = new BoxInfo { Min = P(1000, 2000, 83.05), Max = P(1010, 2005, 83.3) };
            floor.Outline = new List<List<double[]>> { new List<double[]> { P(1000, 2000, 83.2), P(1010, 2000, 83.25), P(1010, 2005, 83.3), P(1000, 2005, 83.25) } };
            floor.Workset = "Shared Levels and Grids";
            floor.PhaseCreated = "New Construction";
            floor.DesignOption = "Option 1";
            JObject document;
            JToken written = OneFloor(floor, out document);

            Assert.Equal(77, (long)written["typeId"]);
            Assert.Equal(83.1, (double)written["levelElevation"], 3);
            Assert.Equal(83.2, (double)written["topElevationRange"]["min"], 3);
            Assert.Equal(83.3, (double)written["topElevationRange"]["max"], 3);
            Assert.Equal(83.05, (double)written["bottomElevationRange"]["min"], 3);
            Assert.Equal(new[] { 1010.0, 2005, 83.3 }, LinkWallRampTests.Numbers(written["boundingBox"]["max"]));
            Assert.Single(written["outline"]);
            Assert.Equal(4, written["outline"][0].Count());
            Assert.Equal(new[] { 1010.0, 2000, 83.25 }, LinkWallRampTests.Numbers(written["outline"][0][1]));
            Assert.Equal("Shared Levels and Grids", (string)written["workset"]);
            Assert.Equal("New Construction", (string)written["phaseCreated"]);
            Assert.Equal("Option 1", (string)written["designOption"]);
        }

        [Fact]
        public void R52_a_floor_with_no_new_measurement_has_exactly_the_keys_it_had_before()
        {
            JObject document;
            JToken written = OneFloor(Measured(1), out document);

            string[] keys = ((JObject)written).Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] before = { "corners", "cornersSource", "elementId", "heightOffset", "level", "parameters", "plotNumber", "sketchBoundary", "topElevation", "typeName", "unit" };
            Assert.Equal(before, keys);
        }

        [Fact]
        public void R52_the_old_corner_and_sketch_fields_are_not_changed_by_the_new_ones()
        {
            FloorRow floor = Measured(1);
            floor.Outline = new List<List<double[]>> { new List<double[]> { P(0, 0, 83.2), P(10, 0, 83.2), P(10, 5, 83.2) } };
            floor.TopElevationRange = new ElevationRange { Min = 83.2, Max = 83.2 };
            JObject document;
            JToken written = OneFloor(floor, out document);

            Assert.Equal("boundary", (string)written["cornersSource"]);
            Assert.Equal(4, written["corners"].Count());
            Assert.Equal(150, (double)written["sketchBoundary"][0][2], 3);
            Assert.Equal(83.2, (double)written["topElevation"], 3);
        }

        // R53 parameter details and floor types

        [Fact]
        public void R53_a_length_parameter_is_written_in_metres_with_its_shown_text_and_unit()
        {
            ParameterDetail detail = ParameterFormat.Make("Thickness", "FLOOR_ATTR_THICKNESS_PARAM", "Double", "150", 0.15 * UnitMath.FeetPerMetre, ValueUnit.Length);

            Assert.Equal("Thickness", detail.Name);
            Assert.Equal("FLOOR_ATTR_THICKNESS_PARAM", detail.BuiltIn);
            Assert.Equal("Double", detail.Storage);
            Assert.Equal("150", detail.Text);
            Assert.Equal(0.15, detail.Number.Value, 9);
            Assert.Equal("m", detail.Unit);
        }

        [Fact]
        public void R53_an_area_and_an_angle_are_converted_and_an_integer_keeps_its_value_with_no_unit()
        {
            ParameterDetail area = ParameterFormat.Make("Area", null, "Double", "10 m2", 10 * UnitMath.FeetPerMetre * UnitMath.FeetPerMetre, ValueUnit.Area);
            ParameterDetail angle = ParameterFormat.Make("Angle", null, "Double", "90", Math.PI / 2, ValueUnit.Angle);
            ParameterDetail count = ParameterFormat.Make("Count", null, "Integer", "3", 3, ValueUnit.None);

            Assert.Equal(10, area.Number.Value, 9);
            Assert.Equal("m2", area.Unit);
            Assert.Equal(90, angle.Number.Value, 9);
            Assert.Equal("deg", angle.Unit);
            Assert.Equal(3, count.Number.Value);
            Assert.Null(count.Unit);
        }

        [Fact]
        public void R53_a_text_parameter_has_no_number_and_a_parameter_that_is_not_built_in_has_no_built_in_name()
        {
            ParameterDetail text = ParameterFormat.Make("Plot", "INVALID", "String", "VD2-2441", null, ValueUnit.None);

            Assert.Null(text.Number);
            Assert.Null(text.Unit);
            Assert.Null(text.BuiltIn);
            Assert.Equal("VD2-2441", text.Text);
            Assert.Null(ParameterFormat.Make("x", "", "String", "a", null, ValueUnit.None).BuiltIn);
        }

        [Fact]
        public void R53_details_are_sorted_by_name_and_a_name_held_twice_keeps_both()
        {
            var details = new[]
            {
                ParameterFormat.Make("Mark", null, "String", "b", null, ValueUnit.None),
                ParameterFormat.Make("Area", null, "Double", "1", 1, ValueUnit.None),
                ParameterFormat.Make("Mark", null, "String", "a", null, ValueUnit.None),
            };
            List<ParameterDetail> collected = ParameterFormat.Collect(details);

            Assert.Equal(new[] { "Area", "Mark", "Mark (2)" }, collected.Select(item => item.Name).ToArray());
            Assert.Equal(new[] { "1", "b", "a" }, collected.Select(item => item.Text).ToArray());
        }

        [Fact]
        public void R53_each_parameter_detail_is_one_line_in_the_file_and_numbers_follow_the_three_decimal_rule()
        {
            FloorRow floor = Measured(1);
            floor.ParameterDetails = new List<ParameterDetail>
            {
                ParameterFormat.Make("Area", "HOST_AREA_COMPUTED", "Double", "10.5", 10.5 * UnitMath.FeetPerMetre * UnitMath.FeetPerMetre, ValueUnit.Area),
                ParameterFormat.Make("Offset", null, "Double", "1", 1, ValueUnit.Length),
            };
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.Add(floor);
            var entry = new DocumentEntry { File = "site.rvt", Kind = "project", Prototypes = new List<PrototypeRow>() };
            ModelReader.ReadSections(host, entry, new ReaderOptions());
            string json = JsonOut.ToJson(entry);

            Assert.Contains("{\"name\":\"Area\",\"builtIn\":\"HOST_AREA_COMPUTED\",\"storage\":\"Double\",\"text\":\"10.5\",\"number\":10.5,\"unit\":\"m2\"}", json);
            Assert.Contains("{\"name\":\"Offset\",\"storage\":\"Double\",\"text\":\"1\",\"number\":0.305,\"unit\":\"m\"}", json);
        }

        [Fact]
        public void R53_floor_types_are_listed_once_with_name_id_and_every_type_parameter()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.FloorTypeList.Add(new FloorTypeRow
            {
                TypeId = 77,
                Name = "Generic 150mm",
                ParameterDetails = new List<ParameterDetail> { ParameterFormat.Make("Default Thickness", "FLOOR_ATTR_DEFAULT_THICKNESS_PARAM", "Double", "150", 0.15 * UnitMath.FeetPerMetre, ValueUnit.Length) },
            });
            JObject document = LinkWallRampTests.Run(host);

            JToken type = document["floorTypes"].Single();
            Assert.Equal(77, (long)type["typeId"]);
            Assert.Equal("Generic 150mm", (string)type["name"]);
            Assert.Equal(0.15, (double)type["parameterDetails"][0]["number"], 3);
            Assert.Equal("FLOOR_ATTR_DEFAULT_THICKNESS_PARAM", (string)type["parameterDetails"][0]["builtIn"]);
        }

        [Fact]
        public void R53_a_source_that_fails_on_floor_types_costs_that_list_only()
        {
            var host = new FailingTypesSource();
            host.FloorList.Add(Measured(1));
            JObject document = LinkWallRampTests.Run(host);

            Assert.Single(document["floors"]);
            Assert.Empty(document["floorTypes"]);
            Assert.Contains(Errors(document), line => line.StartsWith("floorTypes: ", StringComparison.Ordinal));
        }

        private sealed class FailingTypesSource : IModelSource
        {
            private readonly LinkWallRampTests.FakeSource inner = new LinkWallRampTests.FakeSource();

            public List<FloorRow> FloorList { get { return inner.FloorList; } }
            public IList<string> Errors { get { return inner.Errors; } }
            public IEnumerable<SourceLink> Links() { return inner.Links(); }
            public IEnumerable<PrototypeRow> Instances() { return inner.Instances(); }
            public IEnumerable<WallRow> Walls() { return inner.Walls(); }
            public IEnumerable<FloorRow> Floors() { return inner.Floors(); }
            public IEnumerable<RampRow> Ramps() { return inner.Ramps(); }
            public IEnumerable<SpotRow> SpotElevations() { return inner.SpotElevations(); }
            public IEnumerable<FloorTypeRow> FloorTypes() { throw new InvalidOperationException("no types"); }
        }

        // R54 link state

        private static SourceLink StateLink(bool loaded, LinkWallRampTests.FakeSource inside)
        {
            SourceLink link = LinkWallRampTests.Link("villa.rvt", P(1000, 2000, 83), 90, false, inside);
            link.ElementId = 9001;
            link.Status = loaded ? "Loaded" : "Unloaded";
            link.Loaded = loaded;
            link.ParameterDetails = new List<ParameterDetail> { ParameterFormat.Make("CASE", null, "String", "C1", null, ValueUnit.None) };
            if (loaded)
            {
                link.BoxCorners = BoxMath.Corners(P(1000, 2000, 83), P(1012, 2020, 92));
                link.TransformShared = LinkMath.Describe(P(1000, 2000, 83), new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 }, LinkWallRampTests.North);
            }
            return link;
        }

        [Fact]
        public void R54_a_loaded_link_has_status_corners_shared_transform_and_parameter_details()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(StateLink(true, new LinkWallRampTests.FakeSource()));
            JObject document = LinkWallRampTests.Run(host, new ReaderOptions());

            JToken link = document["links"].Single();
            Assert.Equal(9001, (long)link["elementId"]);
            Assert.Equal("Loaded", (string)link["status"]);
            Assert.True((bool)link["loaded"]);
            Assert.Equal(8, link["boundingBoxCorners"].Count());
            Assert.Equal(new[] { 1012.0, 2020, 92 }, LinkWallRampTests.Numbers(link["boundingBoxCorners"][6]));
            Assert.Equal(new[] { 1000.0, 2000, 83 }, LinkWallRampTests.Numbers(link["transformShared"]["origin"]));
            Assert.Equal(90, (double)link["transformShared"]["rotationDeg"], 3);
            Assert.Equal("CASE", (string)link["parameterDetails"][0]["name"]);
            Assert.Equal(new[] { 1000.0, 2000, 83 }, LinkWallRampTests.Numbers(link["transform"]["origin"]));
        }

        [Fact]
        public void R54_a_link_that_is_not_loaded_says_so_and_has_no_corners_and_no_shared_transform()
        {
            var host = new LinkWallRampTests.FakeSource();
            SourceLink link = StateLink(false, null);
            link.Open = null;
            host.LinkList.Add(link);
            JObject document = LinkWallRampTests.Run(host, new ReaderOptions());

            JToken written = document["links"].Single();
            Assert.False((bool)written["loaded"]);
            Assert.Equal("Unloaded", (string)written["status"]);
            Assert.Null(written["boundingBoxCorners"]);
            Assert.Null(written["transformShared"]);
            Assert.Equal(9001, (long)written["elementId"]);
            Assert.Contains("Link is not loaded", written["errors"].Select(line => (string)line));
        }

        [Fact]
        public void R54_nothing_inside_a_link_is_read_by_default_and_no_contents_are_written()
        {
            var inside = new LinkWallRampTests.FakeSource();
            inside.InstanceList.Add(LinkWallRampTests.Villa("V1", false));
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(StateLink(true, inside));
            JObject document = LinkWallRampTests.Run(host, new ReaderOptions());

            Assert.Equal(0, inside.InstanceCalls);
            Assert.Null(document["linkedFiles"]);
            Assert.Null(document["links"].Single()["contentsRef"]);
        }

        [Fact]
        public void R54_the_link_state_code_never_opens_the_linked_document()
        {
            string text = File.ReadAllText(RepoFile("dotnet", "RevitPrototypeReader", "RevitModelSource.cs"));
            int start = text.IndexOf("private void ReadLinkState", StringComparison.Ordinal);
            Assert.True(start > 0);
            int end = text.IndexOf("        public IEnumerable<PrototypeRow> Instances", start, StringComparison.Ordinal);
            string body = text.Substring(start, end - start);

            Assert.DoesNotContain("GetLinkDocument", body);
            Assert.DoesNotContain("Open(", body);
            Assert.DoesNotContain("FilteredElementCollector", body);
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

        // R55 spot hosts and grouped failure lines

        [Fact]
        public void R55_a_spot_with_a_host_gets_the_host_element_category_and_type()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.SpotList.Add(new SpotRow
            {
                ElementId = 5,
                Origin = P(10, 20, 83),
                View = "Site",
                Host = new SpotHost { ElementId = 321, Category = "Floors", TypeName = "Generic 150mm" },
            });
            JObject document = LinkWallRampTests.Run(host);

            JToken spot = document["spotElevations"].Single();
            Assert.Equal(321, (long)spot["host"]["elementId"]);
            Assert.Equal("Floors", (string)spot["host"]["category"]);
            Assert.Equal("Generic 150mm", (string)spot["host"]["typeName"]);
            Assert.Null(spot["hostFailure"]);
            Assert.Empty(Errors(document));
        }

        [Fact]
        public void R55_spots_that_fail_the_same_way_give_one_error_line_and_stay_in_the_list()
        {
            var host = new LinkWallRampTests.FakeSource();
            for (int id = 1; id <= 7; id++)
            {
                host.SpotList.Add(new SpotRow { ElementId = id, Origin = P(id, 0, 83), HostFailure = "the references are not available" });
            }
            JObject document = LinkWallRampTests.Run(host);

            Assert.Equal(7, document["spotElevations"].Count());
            string[] lines = Errors(document);
            Assert.Single(lines);
            Assert.Equal("spotElevations host: the references are not available on 7 elements, first 5 element ids 1, 2, 3, 4, 5", lines[0]);
        }

        [Fact]
        public void R55_two_different_reasons_give_two_lines_and_a_single_failure_names_its_id()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.SpotList.Add(new SpotRow { ElementId = 1, Origin = P(1, 0, 83), HostFailure = "no reference points to an element" });
            host.SpotList.Add(new SpotRow { ElementId = 2, Origin = P(2, 0, 83), HostFailure = "InvalidOperationException: boom" });
            host.SpotList.Add(new SpotRow { ElementId = 3, Origin = P(3, 0, 83), Host = new SpotHost { ElementId = 4 } });
            JObject document = LinkWallRampTests.Run(host);

            string[] lines = Errors(document);
            Assert.Equal(2, lines.Length);
            Assert.Contains("spotElevations host: InvalidOperationException: boom on 1 element, element ids 2", lines);
            Assert.Contains("spotElevations host: no reference points to an element on 1 element, element ids 1", lines);
        }

        [Fact]
        public void R55_grouped_lines_cap_the_ids_and_keep_the_full_count()
        {
            var ids = Enumerable.Range(1, 12).Select(id => (long?)id).ToList();

            Assert.Equal("floor outline: 1 edge chain(s) did not close on 12 elements, first 5 element ids 1, 2, 3, 4, 5", ErrorLines.Grouped("floor outline", "1 edge chain(s) did not close", ids));
            Assert.Equal("x: y on 2 elements, element ids 1, 2", ErrorLines.Grouped("x", "y", ids.Take(2).ToList()));
        }

        [Fact]
        public void R55_the_add_in_reads_every_new_floor_part_inside_its_own_try()
        {
            string text = File.ReadAllText(RepoFile("dotnet", "RevitPrototypeReader", "RevitModelSource.cs"));
            int start = text.IndexOf("private void Measure(", StringComparison.Ordinal);
            Assert.True(start > 0);
            int end = text.IndexOf("private List<ParameterDetail> DetailsOf", start, StringComparison.Ordinal);
            string body = text.Substring(start, end - start);

            Assert.True(body.Split(new[] { "Guarded(" }, StringSplitOptions.None).Length - 1 >= 7);
        }

        // R56 output size

        private static List<ParameterDetail> Details(int count, string prefix)
        {
            var details = new List<ParameterDetail>();
            string[] storages = { "Double", "String", "Integer", "ElementId" };
            for (int index = 0; index < count; index++)
            {
                string storage = storages[index % 4];
                string builtIn = index % 3 == 0 ? null : "FLOOR_" + prefix.ToUpperInvariant() + "_PARAM_" + index;
                bool number = storage == "Double" || storage == "Integer";
                details.Add(ParameterFormat.Make(prefix + " parameter number " + index, builtIn, storage, "value " + index, number ? (double?)index * 1.2345 : null, storage == "Double" ? ValueUnit.Length : ValueUnit.None));
            }
            return ParameterFormat.Collect(details);
        }

        private static Dictionary<string, object> OldParameters(int count, string prefix)
        {
            var parameters = new Dictionary<string, object>();
            for (int index = 0; index < count; index++)
            {
                parameters[prefix + " parameter number " + index] = index % 2 == 0 ? (object)("value " + index) : index * 1.2345;
            }
            return parameters;
        }

        // The round 7 run in Revit wrote 5.2 MB for 1010 floors, 926 walls, 338 links and 987 spots. The parameter count per element
        // is set so the made up file is that size before the new fields. The details hold the same parameters as the old dictionary,
        // so they use the same count. The size of the added part is then an estimate for the real model and not a measurement
        private const int ParametersPerElement = 34;

        [Fact]
        public void R56_a_made_up_model_the_size_of_the_real_one_stays_under_twenty_megabytes()
        {
            const double RealRoundSevenMegabytes = 5.2;
            const double TargetMegabytes = 20;
            var host = new LinkWallRampTests.FakeSource();
            for (int index = 0; index < 1010; index++)
            {
                FloorRow floor = Measured(10000 + index);
                floor.Parameters = OldParameters(ParametersPerElement, "Floor");
                floor.Parameters["Plot"] = "VD2-" + (2000 + index);
                host.FloorList.Add(floor);
            }
            for (int index = 0; index < 926; index++)
            {
                host.WallList.Add(new WallRow { TypeName = "Wall type " + index % 12, ElementId = 20000 + index, Length = 5.5, Height = 3, Parameters = OldParameters(ParametersPerElement, "Wall") });
            }
            for (int index = 0; index < 338; index++)
            {
                SourceLink link = LinkWallRampTests.Link("villa " + index + ".rvt", P(1000 + index, 2000, 83), 90, index % 2 == 0, null);
                link.Parameters = OldParameters(ParametersPerElement, "Link");
                host.LinkList.Add(link);
            }
            for (int index = 0; index < 987; index++)
            {
                host.SpotList.Add(new SpotRow { ElementId = 50000 + index, Origin = P(1000 + index, 2000, 83), View = "Site Plan" });
            }
            string before = Serialize(host);

            foreach (FloorRow floor in host.FloorList)
            {
                floor.TypeId = 77;
                floor.LevelElevation = 83.1;
                floor.TopElevationRange = new ElevationRange { Min = 83.2123, Max = 83.3123 };
                floor.BottomElevationRange = new ElevationRange { Min = 83.0123, Max = 83.1123 };
                floor.BoundingBox = new BoxInfo { Min = P(1000.123, 2000.123, 83.0123), Max = P(1010.123, 2005.123, 83.3123) };
                floor.Outline = new List<List<double[]>>
                {
                    Enumerable.Range(0, 8).Select(point => P(1000.123 + point, 2000.123 + point, 83.2123)).ToList(),
                    Enumerable.Range(0, 8).Select(point => P(1002.123 + point, 2002.123 + point, 83.2123)).ToList(),
                    Enumerable.Range(0, 8).Select(point => P(1003.123 + point, 2003.123 + point, 83.2123)).ToList(),
                };
                floor.Workset = "Shared Levels and Grids";
                floor.PhaseCreated = "New Construction";
                floor.ParameterDetails = Details(ParametersPerElement, "Floor");
            }
            foreach (SourceLink link in host.LinkList)
            {
                link.ElementId = 9000;
                link.Status = "Loaded";
                link.Loaded = true;
                link.BoxCorners = BoxMath.Corners(P(1000.123, 2000.123, 83.123), P(1012.123, 2020.123, 92.123));
                link.TransformShared = LinkMath.Describe(P(1000.123, 2000.123, 83.123), new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 }, LinkWallRampTests.North);
                link.ParameterDetails = Details(ParametersPerElement, "Link");
            }
            foreach (SpotRow spot in host.SpotList)
            {
                spot.Host = new SpotHost { ElementId = 321, Category = "Floors", TypeName = "Generic 150mm" };
            }
            for (int index = 0; index < 20; index++)
            {
                host.FloorTypeList.Add(new FloorTypeRow { TypeId = index, Name = "Type " + index, ParameterDetails = Details(ParametersPerElement + 30, "Type") });
            }
            string after = Serialize(host);

            double beforeMb = before.Length / 1048576.0;
            double afterMb = after.Length / 1048576.0;
            double addedMb = afterMb - beforeMb;
            output.WriteLine("made up model before the new fields " + beforeMb.ToString("0.00") + " MB, with them " + afterMb.ToString("0.00") + " MB, added " + addedMb.ToString("0.00") + " MB");
            output.WriteLine("real round 7 file " + RealRoundSevenMegabytes + " MB plus the added size is about " + (RealRoundSevenMegabytes + addedMb).ToString("0.00") + " MB");
            Assert.True(afterMb < TargetMegabytes, "the made up model is " + afterMb.ToString("0.00") + " MB");
            Assert.True(RealRoundSevenMegabytes + addedMb < TargetMegabytes, "the estimate for the real model is " + (RealRoundSevenMegabytes + addedMb).ToString("0.00") + " MB");
        }

        private static string Serialize(LinkWallRampTests.FakeSource host)
        {
            var entry = new DocumentEntry { File = "site.rvt", Kind = "project", Prototypes = new List<PrototypeRow>() };
            ModelReader.ReadSections(host, entry, new ReaderOptions());
            return JsonOut.ToJson(new PrototypesFile { Tool = "RevitPrototypeReader", Documents = new List<DocumentEntry> { entry } });
        }
    }
}
