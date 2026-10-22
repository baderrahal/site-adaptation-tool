using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;
using Xunit.Abstractions;

namespace SiteAdaptation.Tests
{
    // Corners, slope, spot elevations, plot numbers, the link name parse and the contents default.
    // Test doubles prove the logic and the JSON shape. They say nothing about what Revit returns
    public class RampCornerTests
    {
        private readonly ITestOutputHelper output;

        public RampCornerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static double[] P(double x, double y, double z = 0)
        {
            return new[] { x, y, z };
        }

        // A ramp floor whose sketch plane is at 150 and whose real corners differ by up to 0.2 in height
        private static FloorRow Ramp(long id, ShapeEditing shape, double top = 83.2)
        {
            return new FloorRow
            {
                TypeName = "Ramps 0.5%",
                ElementId = id,
                Level = "Level 1",
                TopElevation = top,
                SketchBoundary = new List<double[]> { P(0, 0, 150), P(10, 0, 150), P(10, 5, 150), P(0, 5, 150) },
                BoundaryEnds = new List<double[]> { P(0, 0, 150), P(10, 0, 150), P(10, 5, 150), P(0, 5, 150) },
                Shape = shape,
                Parameters = new Dictionary<string, object> { { "Plot", "VD2-2441" } },
            };
        }

        private static ShapeEditing Edited()
        {
            return new ShapeEditing
            {
                Enabled = true,
                Corners = new List<double[]> { P(0, 0, 83.1), P(10, 0, 83.25), P(10, 5, 83.3), P(0, 5, 83.15) },
                Others = new List<double[]>(),
            };
        }

        private static JToken OneFloor(FloorRow floor, out JObject document)
        {
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.Add(floor);
            document = LinkWallRampTests.Run(host);
            return document["floors"].Single();
        }

        private static string[] Errors(JObject document)
        {
            return document["errors"] == null ? new string[0] : document["errors"].Select(line => (string)line).ToArray();
        }

        [Fact]
        public void R34_floor_writes_sketchBoundary_and_never_a_key_called_boundary()
        {
            JObject document;
            JToken floor = OneFloor(Ramp(1, Edited()), out document);

            Assert.Equal(4, floor["sketchBoundary"].Count());
            Assert.Null(floor["boundary"]);
            Assert.Equal(150, (double)floor["sketchBoundary"][0][2], 3);
            Assert.DoesNotContain("\"boundary\":", JsonOut.ToJson(document));
        }

        [Fact]
        public void R34_each_corner_has_x_y_and_its_own_z_and_the_sketch_z_is_not_used()
        {
            JObject document;
            JToken floor = OneFloor(Ramp(1, Edited()), out document);

            Assert.Equal(4, floor["corners"].Count());
            Assert.Equal(new[] { 0.0, 0, 83.1 }, LinkWallRampTests.Numbers(floor["corners"][0]));
            Assert.Equal(new[] { 10.0, 5, 83.3 }, LinkWallRampTests.Numbers(floor["corners"][2]));
            double lowest = floor["corners"].Min(corner => (double)corner[2]);
            double highest = floor["corners"].Max(corner => (double)corner[2]);
            Assert.Equal(0.2, highest - lowest, 3);
            Assert.All(floor["corners"], corner => Assert.NotEqual(150, (double)corner[2]));
        }

        [Fact]
        public void R34_corner_values_are_rounded_to_three_decimals()
        {
            ShapeEditing shape = Edited();
            shape.Corners[1] = P(10.00049, 0, 83.254999);
            JObject document;
            JToken floor = OneFloor(Ramp(1, shape), out document);

            Assert.Equal(new[] { 10.0, 0, 83.255 }, LinkWallRampTests.Numbers(floor["corners"][1]));
        }

        [Fact]
        public void R35_shape_editing_vertices_are_the_corners_when_shape_editing_is_on()
        {
            JObject document;
            JToken floor = OneFloor(Ramp(1, Edited()), out document);

            Assert.Equal("shapeEditor", (string)floor["cornersSource"]);
            Assert.Equal(83.25, (double)floor["corners"][1][2], 3);
            Assert.Null(floor["shapePoints"]);
            Assert.Empty(Errors(document));
        }

        [Fact]
        public void R35_boundary_end_points_at_the_top_elevation_are_used_when_shape_editing_is_off()
        {
            JObject document;
            JToken floor = OneFloor(Ramp(1, new ShapeEditing { Enabled = false }, 83.2), out document);

            Assert.Equal("boundary", (string)floor["cornersSource"]);
            Assert.Equal(4, floor["corners"].Count());
            Assert.All(floor["corners"], corner => Assert.Equal(83.2, (double)corner[2], 3));
            Assert.Equal(new[] { 10.0, 5, 83.2 }, LinkWallRampTests.Numbers(floor["corners"][2]));
        }

        [Fact]
        public void R35_a_source_with_no_shape_information_falls_back_to_the_boundary()
        {
            JObject document;
            JToken floor = OneFloor(Ramp(1, null), out document);

            Assert.Equal("boundary", (string)floor["cornersSource"]);
        }

        [Fact]
        public void R35_shape_editing_with_fewer_than_three_corners_falls_back_to_the_boundary()
        {
            ShapeEditing shape = Edited();
            shape.Corners = shape.Corners.Take(2).ToList();
            JObject document;
            JToken floor = OneFloor(Ramp(1, shape), out document);

            Assert.Equal("boundary", (string)floor["cornersSource"]);
            Assert.Equal(4, floor["corners"].Count());
        }

        [Fact]
        public void R35_shape_editing_that_is_off_is_not_used_even_when_it_holds_vertices()
        {
            ShapeEditing shape = Edited();
            shape.Enabled = false;
            JObject document;
            JToken floor = OneFloor(Ramp(1, shape), out document);

            Assert.Equal("boundary", (string)floor["cornersSource"]);
            Assert.All(floor["corners"], corner => Assert.Equal(83.2, (double)corner[2], 3));
        }

        [Fact]
        public void R35_boundary_corners_are_the_edge_starts_and_not_the_points_cut_out_of_a_spline()
        {
            var edges = new List<BoundaryEdge>
            {
                new BoundaryEdge { Start = P(0, 0), End = P(10, 0) },
                new BoundaryEdge { Start = P(10, 0), End = P(10, 10), Via = new List<double[]> { P(12, 3), P(12, 7) } },
                new BoundaryEdge { Start = P(10, 10), End = P(0, 10) },
                new BoundaryEdge { Start = P(0, 10), End = P(0, 0) },
            };
            List<double[]> vertices;
            List<FloorArc> arcs;
            List<double[]> ends;
            Assert.True(FloorMath.TryOuter(new IList<BoundaryEdge>[] { edges }, out vertices, out arcs, out ends));
            Assert.Equal(6, vertices.Count);
            Assert.Equal(4, ends.Count);

            List<double[]> corners;
            string source;
            Assert.True(FloorMath.TryCorners(null, ends, 2.5, out corners, out source));
            Assert.Equal(4, corners.Count);
            Assert.All(corners, corner => Assert.Equal(2.5, corner[2]));
        }

        [Fact]
        public void R35_floor_with_no_shape_and_no_boundary_has_no_corners_and_one_error_line()
        {
            var floor = new FloorRow { TypeName = "Generic", ElementId = 7, TopElevation = 3, Parameters = new Dictionary<string, object> { { "Plot", "DT1-5" } } };
            JObject document;
            JToken written = OneFloor(floor, out document);

            Assert.Null(written["corners"]);
            Assert.Null(written["cornersSource"]);
            Assert.Equal(new[] { "floor 7: no corners found" }, Errors(document));
        }

        [Fact]
        public void R35_floor_with_a_boundary_and_no_top_elevation_has_no_corners()
        {
            FloorRow floor = Ramp(8, new ShapeEditing { Enabled = false });
            floor.TopElevation = null;
            JObject document;
            JToken written = OneFloor(floor, out document);

            Assert.Null(written["corners"]);
            Assert.Equal(new[] { "floor 8: no corners found" }, Errors(document));
        }

        [Fact]
        public void R35_shape_points_that_are_not_corners_are_listed_only_when_there_are_some()
        {
            ShapeEditing shape = Edited();
            shape.Others.Add(P(5, 2.5, 83.4));
            JObject document;
            JToken floor = OneFloor(Ramp(1, shape), out document);

            Assert.Equal(4, floor["corners"].Count());
            Assert.Equal(new[] { 5.0, 2.5, 83.4 }, LinkWallRampTests.Numbers(floor["shapePoints"].Single()));
        }

        [Fact]
        public void R35_slope_info_holds_the_slope_parameter_and_the_slope_arrow_flag()
        {
            FloorRow sloped = Ramp(1, new ShapeEditing { Enabled = false });
            sloped.Slope = new SlopeInfo { Parameter = 0.02, Arrow = true };
            FloorRow plain = Ramp(2, new ShapeEditing { Enabled = false });
            plain.Slope = new SlopeInfo { Parameter = null, Arrow = false };
            FloorRow unread = Ramp(3, new ShapeEditing { Enabled = false });
            unread.Slope = new SlopeInfo();
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.AddRange(new[] { sloped, plain, unread });
            JToken floors = LinkWallRampTests.Run(host)["floors"];

            Assert.Equal(0.02, (double)floors[0]["slope"]["parameter"], 3);
            Assert.True((bool)floors[0]["slope"]["arrow"]);
            Assert.Equal(JTokenType.Null, floors[1]["slope"]["parameter"].Type);
            Assert.False((bool)floors[1]["slope"]["arrow"]);
            Assert.Equal(JTokenType.Null, floors[2]["slope"]["arrow"].Type);
        }

        [Fact]
        public void R35_tally_counts_floors_by_shape_editing_slope_and_neither()
        {
            FloorRow shaped = Ramp(1, Edited());
            shaped.CornersSource = FloorMath.FromShapeEditor;
            shaped.Slope = new SlopeInfo { Arrow = false };
            FloorRow arrow = Ramp(2, null);
            arrow.CornersSource = FloorMath.FromBoundary;
            arrow.Slope = new SlopeInfo { Arrow = true };
            FloorRow parameter = Ramp(3, null);
            parameter.CornersSource = FloorMath.FromBoundary;
            parameter.Slope = new SlopeInfo { Parameter = 0.005 };
            FloorRow both = Ramp(4, Edited());
            both.CornersSource = FloorMath.FromShapeEditor;
            both.Slope = new SlopeInfo { Arrow = true };
            FloorRow neither = Ramp(5, null);
            neither.CornersSource = FloorMath.FromBoundary;
            neither.Slope = new SlopeInfo { Arrow = false };
            FloorRow unread = Ramp(6, null);

            FloorTally tally = FloorMath.Tally(new[] { shaped, arrow, parameter, both, neither, unread });

            Assert.Equal(6, tally.Floors);
            Assert.Equal(2, tally.ShapeEditing);
            Assert.Equal(3, tally.Slope);
            Assert.Equal(2, tally.Neither);
        }

        [Fact]
        public void R36_spot_elevations_are_written_on_the_document_entry_with_the_fields_that_were_read()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.SpotList.Add(new SpotRow { ElementId = 90, Origin = P(1.25, 2.5, 83.4), Value = 83.4, View = "Site Plan", Target = 4001 });
            host.SpotList.Add(new SpotRow { ElementId = 91, Origin = P(3, 4, 82.9) });
            JObject document = LinkWallRampTests.Run(host);

            JToken spots = document["spotElevations"];
            Assert.Equal(2, spots.Count());
            Assert.Equal(90, (long)spots[0]["elementId"]);
            Assert.Equal(new[] { 1.25, 2.5, 83.4 }, LinkWallRampTests.Numbers(spots[0]["origin"]));
            Assert.Equal(83.4, (double)spots[0]["value"], 3);
            Assert.Equal("Site Plan", (string)spots[0]["view"]);
            Assert.Equal(4001, (long)spots[0]["target"]);
            Assert.Null(spots[1]["value"]);
            Assert.Null(spots[1]["view"]);
            Assert.Null(spots[1]["target"]);
        }

        [Fact]
        public void R36_a_model_with_no_spot_elevations_gets_an_empty_list()
        {
            JObject document = LinkWallRampTests.Run(new LinkWallRampTests.FakeSource());

            Assert.Empty(document["spotElevations"]);
        }

        [Fact]
        public void R36_a_failure_reading_spot_elevations_does_not_lose_the_floors()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.Add(Ramp(1, Edited()));
            host.SpotsFailure = () => { throw new InvalidOperationException("no spots"); };
            JObject document = LinkWallRampTests.Run(host);

            Assert.Single(document["floors"]);
            Assert.Empty(document["spotElevations"]);
            Assert.Contains("spotElevations: InvalidOperationException: no spots", Errors(document));
        }

        [Fact]
        public void R37_plot_text_is_split_at_the_dash_into_unit_and_number()
        {
            string unit;
            string number;
            Assert.True(PlotParts.TryParse("VD2-2441", out unit, out number));
            Assert.Equal("VD2", unit);
            Assert.Equal("2441", number);
            Assert.True(PlotParts.TryParse(" TH3(0) - 12 ", out unit, out number));
            Assert.Equal("TH3(0)", unit);
            Assert.Equal("12", number);
            Assert.True(PlotParts.TryParse("A-B-77", out unit, out number));
            Assert.Equal("A-B", unit);
            Assert.Equal("77", number);
            foreach (object bad in new object[] { "2441", "VD2-", "VD2-24a", "-2441", "", "   ", null })
            {
                Assert.False(PlotParts.TryParse(bad, out unit, out number), "parsed " + bad);
                Assert.Null(unit);
                Assert.Null(number);
            }
        }

        [Fact]
        public void R37_floor_gets_plotNumber_and_unit_from_its_Plot_parameter()
        {
            JObject document;
            JToken floor = OneFloor(Ramp(1, Edited()), out document);

            Assert.Equal("2441", (string)floor["plotNumber"]);
            Assert.Equal("VD2", (string)floor["unit"]);
            Assert.Empty(Errors(document));
        }

        [Fact]
        public void R37_floor_with_no_parseable_Plot_gets_explicit_nulls_and_an_error_line()
        {
            FloorRow missing = Ramp(1, Edited());
            missing.Parameters.Remove("Plot");
            FloorRow odd = Ramp(2, Edited());
            odd.Parameters["Plot"] = "no dash here";
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.AddRange(new[] { missing, odd });
            JObject document = LinkWallRampTests.Run(host);

            foreach (JToken floor in document["floors"])
            {
                Assert.Equal(JTokenType.Null, floor["plotNumber"].Type);
                Assert.Equal(JTokenType.Null, floor["unit"].Type);
            }
            Assert.Equal(new[] { "floor 2: Plot parameter does not read as unit and number", "Plot parameter is missing on 1 floor of type Ramps 0.5%, element ids 1" }, Errors(document));
        }

        private static FloorRow NoPlotFloor(long id, string type)
        {
            FloorRow floor = Ramp(id, Edited());
            floor.TypeName = type;
            floor.Parameters.Remove("Plot");
            return floor;
        }

        private static string[] NoPlotErrors(params FloorRow[] floors)
        {
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.AddRange(floors);
            return Errors(LinkWallRampTests.Run(host));
        }

        [Fact]
        public void R50_floors_with_no_Plot_parameter_get_one_line_per_type_with_the_full_count_and_five_ids()
        {
            var floors = new List<FloorRow>();
            for (long id = 10; id < 17; id++)
            {
                floors.Add(NoPlotFloor(id, "yard"));
            }
            string[] errors = NoPlotErrors(floors.ToArray());

            Assert.Equal(new[] { "Plot parameter is missing on 7 floors of type yard, first 5 element ids 10, 11, 12, 13, 14" }, errors);
        }

        [Fact]
        public void R50_each_type_gets_its_own_line_in_type_order_and_up_to_five_ids_are_all_named()
        {
            string[] errors = NoPlotErrors(
                NoPlotFloor(5, "yard"),
                NoPlotFloor(1, "Generic 150mm"),
                NoPlotFloor(6, "yard"),
                NoPlotFloor(2, "Generic 150mm"),
                NoPlotFloor(7, "yard"),
                NoPlotFloor(8, "yard"),
                NoPlotFloor(9, "yard"));

            Assert.Equal(
                new[]
                {
                    "Plot parameter is missing on 2 floors of type Generic 150mm, element ids 1, 2",
                    "Plot parameter is missing on 5 floors of type yard, element ids 5, 6, 7, 8, 9",
                },
                errors);
        }

        [Fact]
        public void R50_a_floor_that_has_a_Plot_value_gets_no_missing_line_and_a_bad_value_keeps_its_own_line()
        {
            FloorRow good = Ramp(1, Edited());
            FloorRow odd = Ramp(2, Edited());
            odd.Parameters["Plot"] = "bad";
            string[] errors = NoPlotErrors(good, odd, NoPlotFloor(3, "yard"));

            Assert.Equal(new[] { "floor 2: Plot parameter does not read as unit and number", "Plot parameter is missing on 1 floor of type yard, element ids 3" }, errors);
        }

        [Fact]
        public void R50_a_floor_with_no_type_name_is_grouped_as_no_type_and_the_null_fields_stay_explicit()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.FloorList.Add(NoPlotFloor(4, null));
            JObject document = LinkWallRampTests.Run(host);

            Assert.Equal(new[] { "Plot parameter is missing on 1 floor of type (no type), element ids 4" }, Errors(document));
            Assert.Equal(JTokenType.Null, document["floors"][0]["plotNumber"].Type);
            Assert.Equal(JTokenType.Null, document["floors"][0]["unit"].Type);
        }

        [Fact]
        public void R37_wall_gets_the_fields_only_when_it_holds_a_Plot_parameter()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.WallList.Add(new WallRow { TypeName = "BW", ElementId = 1, Parameters = new Dictionary<string, object> { { "Plot", "VL4-1319" } } });
            host.WallList.Add(new WallRow { TypeName = "BW", ElementId = 2, Parameters = new Dictionary<string, object> { { "Mark", "x" } } });
            host.WallList.Add(new WallRow { TypeName = "BW", ElementId = 3, Parameters = new Dictionary<string, object> { { "Plot", "bad" } } });
            JObject document = LinkWallRampTests.Run(host);

            Assert.Equal("1319", (string)document["walls"][0]["plotNumber"]);
            Assert.Equal("VL4", (string)document["walls"][0]["unit"]);
            Assert.Null(document["walls"][1]["plotNumber"]);
            Assert.Null(document["walls"][1]["unit"]);
            Assert.Null(document["walls"][2]["plotNumber"]);
            Assert.Equal(new[] { "wall 3: Plot parameter does not read as unit and number" }, Errors(document));
        }

        private const string VillaRaw = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt : 2325 | C10 | 1 | XX | XX : location <Not Shared>";

        private static JToken LinkOf(string rawName, Dictionary<string, object> parameters)
        {
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(new SourceLink
            {
                File = "Villa.rvt",
                Name = rawName,
                Parameters = parameters,
                Transform = LinkMath.Describe(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, LinkWallRampTests.North),
            });
            return LinkWallRampTests.Run(host)["links"].Single();
        }

        private static void AssertVilla(JToken link)
        {
            Assert.Equal("2325", (string)link["nameParts"]["plot"]);
            Assert.Equal("C10", (string)link["nameParts"]["prototype"]);
            Assert.Equal("1", (string)link["nameParts"]["steps"]);
            Assert.Equal("XX", (string)link["nameParts"]["facadeColor"]);
            Assert.Equal("XX", (string)link["nameParts"]["designOption"]);
        }

        [Fact]
        public void R38_name_parts_come_from_the_shared_parameter_RVT_Name_when_it_exists()
        {
            Dictionary<string, object> parameters = LinkWallRampTests.FullParameters("2325", "C10");
            parameters[LinkFields.RvtName] = "2325 | C10 | 1 | XX | XX";
            // The raw name differs on purpose, so only the shared parameter can give the expected parts
            string other = "File.rvt : 9999 | ZZ | 9 | QQ | QQ : location <Not Shared>";
            JToken link = LinkOf(other, parameters);

            AssertVilla(link);
            Assert.Equal(other, (string)link["rawName"]);
            Assert.Null(link["name"]);
        }

        [Fact]
        public void R38_without_the_shared_parameter_the_file_name_prefix_and_the_location_suffix_are_cut_from_the_raw_name()
        {
            JToken link = LinkOf(VillaRaw, LinkWallRampTests.FullParameters("2325", "C10"));

            AssertVilla(link);
            Assert.Equal(VillaRaw, (string)link["rawName"]);
            Assert.Equal(new[] { "Link is not loaded" }, link["errors"].Select(line => (string)line).ToArray());
        }

        [Fact]
        public void R38_a_blank_shared_parameter_falls_back_to_the_raw_name()
        {
            Dictionary<string, object> parameters = LinkWallRampTests.FullParameters("2325", "C10");
            parameters[LinkFields.RvtName] = "   ";
            AssertVilla(LinkOf(VillaRaw, parameters));
        }

        [Fact]
        public void R38_other_location_words_and_a_design_option_with_text_are_cut_the_same_way()
        {
            JToken link = LinkOf("01101-PSN-ZZZ-XXX-LAN-ZZ-001000.rvt : 2406 | TH3(0) | 1 | XX | YY : location <Shared>", null);

            Assert.Equal("2406", (string)link["nameParts"]["plot"]);
            Assert.Equal("TH3(0)", (string)link["nameParts"]["prototype"]);
            Assert.Equal("YY", (string)link["nameParts"]["designOption"]);
        }

        [Fact]
        public void R38_a_name_with_no_prefix_and_no_suffix_is_left_as_it_is()
        {
            Assert.Equal("1 | VX1 | 1 | XX | XX", LinkFields.CleanName("1 | VX1 | 1 | XX | XX", null));
            Assert.Equal("2325 | C10 | 1 | XX | XX", LinkFields.CleanName(VillaRaw, new Dictionary<string, object>()));
            Assert.Equal("from parameter", LinkFields.CleanName("raw", new Dictionary<string, object> { { LinkFields.RvtName, " from parameter " } }));
            Assert.Null(LinkFields.CleanName(null, null));
        }

        [Fact]
        public void R39_the_default_reads_the_link_instances_only_and_writes_no_contents()
        {
            var loaded = new LinkWallRampTests.FakeSource();
            loaded.InstanceList.Add(LinkWallRampTests.Villa("never read", false));
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(new SourceLink
            {
                File = "Type A.rvt",
                Name = "1 | VX1 | 1 | XX | XX",
                Parameters = LinkWallRampTests.FullParameters("1"),
                Transform = LinkMath.Describe(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, LinkWallRampTests.North),
                Open = () => loaded,
            });
            var entry = new DocumentEntry { File = "site.rvt", Kind = "project", Prototypes = new List<PrototypeRow>() };
            ModelReader.ReadSections(host, entry);

            Assert.Equal(LinkContents.None, new ReaderOptions().LinkContents);
            Assert.Null(entry.LinkedFiles);
            Assert.Equal(0, loaded.InstanceCalls);
            Assert.Null(entry.Links.Single().ContentsRef);
            Assert.Equal("1", (string)entry.Links.Single().PlotNumber);
        }

        // A dummy site with the counts of the real run: 337 villa links to 13 files, 337 plots with three floors each and spots.
        // The real size is only known after the add-in runs on the real model
        [Fact]
        public void R3_dummy_site_shows_the_size_of_the_default_and_of_the_contents_option()
        {
            var host = new LinkWallRampTests.FakeSource();
            var linked = new LinkWallRampTests.FakeSource();
            for (int index = 0; index < 60; index++)
            {
                linked.InstanceList.Add(LinkWallRampTests.Villa("m" + index, index % 2 == 0));
            }
            for (int plot = 0; plot < 337; plot++)
            {
                string number = (2300 + plot).ToString();
                bool loadedHere = plot % 7 == 0;
                Dictionary<string, object> parameters = LinkWallRampTests.FullParameters(number, "VD2");
                parameters[LinkFields.RvtName] = number + " | VD2 | 1 | XX | XX";
                host.LinkList.Add(new SourceLink
                {
                    File = "Villa " + (plot % 13) + ".rvt",
                    Name = "Villa " + (plot % 13) + ".rvt : " + number + " | VD2 | 1 | XX | XX : location <Not Shared>",
                    Parameters = parameters,
                    Transform = LinkMath.Describe(new[] { 1.0, 2, 3 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, LinkWallRampTests.North),
                    Open = () => loadedHere ? linked : null,
                });
                FloorRow ramp = Ramp(plot * 3, Edited());
                ramp.Slope = new SlopeInfo { Arrow = false };
                host.FloorList.Add(ramp);
                host.FloorList.Add(Ramp(plot * 3 + 1, new ShapeEditing { Enabled = false }));
                host.FloorList.Add(Ramp(plot * 3 + 2, null));
                host.SpotList.Add(new SpotRow { ElementId = plot, Origin = P(1.5, 2.5, 83.4), Value = 83.4, View = "Site Plan", Target = plot * 3 });
            }
            int none = JsonOut.ToJson(LinkWallRampTests.Run(host, new ReaderOptions())).Length;
            int once = JsonOut.ToJson(LinkWallRampTests.Run(host, new ReaderOptions { LinkContents = LinkContents.Once })).Length;
            output.WriteLine("dummy site, 337 links, 1011 floors, 337 spots: default none " + none + " characters, once " + once + " characters");

            Assert.True(none < once);
            Assert.True(none < 1500000, "the default output grew to " + none);
        }
    }
}
