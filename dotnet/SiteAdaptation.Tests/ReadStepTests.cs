using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CadReader;
using CSMath;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R348 to R350. The read steps of the worker on made up drawings, LandXML files and workbooks built in code (R10), the C# side of
    // python/tests/test_read_steps.py on the same made up data, on net48 and net8.0
    public sealed class ReadStepTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r348_" + Guid.NewGuid().ToString("N"));

        public ReadStepTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        private static JObject Shape(string label, string type, double? area, string kind, params double[] xy)
        {
            var vertices = new JArray();
            for (int index = 0; index < xy.Length; index += 2)
            {
                vertices.Add(new JArray(xy[index], xy[index + 1]));
            }
            return new JObject { ["kind"] = kind, ["plot"] = label, ["vertices"] = vertices, ["type"] = type == null ? JValue.CreateNull() : (JToken)type, ["area"] = area.HasValue ? (JToken)area.Value : JValue.CreateNull() };
        }

        // The made up baseplan of python/tests/test_read_steps.py
        private static JArray Plots()
        {
            return new JArray
            {
                Shape("1", "DP1", 100.0, "plot_shape", 1000, 5000, 1002, 5000, 1002, 5003, 1000, 5003),
                Shape("02", "OS", 50.0, "plot_shape", 0, 0, 10, 0, 10, 10),
                Shape("1", "DP1", 0.5, "plot_shape", 1500, 4500, 1501, 4500, 1501, 4501),
                Shape("3a", null, null, "plot_shape", 0, 30, 10, 30, 10, 40),
                Shape("5", "DP1", 100.0, "polyline", 0, 60, 10, 60),
                Shape("4", "DP1", 0.0, "plot_shape", 0, 50, 10, 50),
            };
        }

        private static double[][] Ring(object value)
        {
            return ((IEnumerable<double[]>)value).ToArray();
        }

        [Fact]
        public void R348_the_labels_step_holds_every_plot_shape_of_a_label_as_read()
        {
            var found = ReadSteps.LabelStep(Plots());
            Assert.Equal(new[] { "1", "02", "3a", "4" }, found.Keys.ToArray());
            var rings = (List<List<double[]>>)found["1"]["rings"];
            Assert.Equal(2, rings.Count);
            Assert.Equal(new[] { 1000.0, 5000 }, rings[0][0]);
            Assert.Equal(new[] { 1501.0, 4501 }, rings[1][2]);
            Assert.Equal(new object[] { 100.0, 0.5 }, (List<object>)found["1"]["areas"]);
            Assert.Equal(new[] { "DP1", "DP1" }, ((List<JToken>)found["1"]["types"]).Select(type => (string)type));
            Assert.True((bool)found["1"]["whole"]);
            Assert.True((bool)found["02"]["whole"]);
            Assert.False((bool)found["3a"]["whole"]);
            Assert.Equal(JTokenType.Null, ((List<JToken>)found["3a"]["types"])[0].Type);
            Assert.Null(((List<object>)found["3a"]["areas"])[0]);
        }

        private static JArray Blocks()
        {
            return new JArray
            {
                new JObject { ["name"] = "Villa_C10", ["layer"] = "MP_VL_C10", ["handle"] = "A1", ["insertPoint"] = new JArray(1.0, 2.0), ["scale"] = new JArray(-1, 1, 1), ["rotationDeg"] = 30.5,
                              ["mirrored"] = true, ["outline"] = new JArray(new JArray(0, 0), new JArray(4, 0), new JArray(4, 3)) },
                new JObject { ["name"] = "JM", ["layer"] = "MP_BLDG_Footprint", ["handle"] = "A1", ["insertPoint"] = new JArray(5.0, 6.0), ["scale"] = new JArray(1, 1, 1), ["rotationDeg"] = 0.0,
                              ["mirrored"] = false },
                new JObject { ["name"] = "DP2", ["layer"] = "MP_DP2", ["handle"] = "B7", ["insertPoint"] = new JArray(7.25, 8.5), ["scale"] = new JArray(1, 1, 1), ["rotationDeg"] = 359.999,
                              ["mirrored"] = false, ["outline"] = new JArray(new JArray(1, 1), new JArray(2, 1), new JArray(2, 2), new JArray(1, 2)) },
            };
        }

        [Fact]
        public void R348_the_blocks_step_holds_every_block_by_its_handle_a_handle_met_again_by_its_count()
        {
            var found = ReadSteps.BlockStep(Blocks());
            Assert.Equal(new[] { "A1", "A1#2", "B7" }, found.Keys.ToArray());
            Assert.Equal("Villa_C10", (string)(JToken)found["A1"]["name"]);
            Assert.True((bool)(JToken)found["A1"]["mirrored"]);
            Assert.Equal("[-1,1,1]", ((JToken)found["A1"]["scale"]).ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(new[] { 1.0, 2.0 }, (double[])found["A1"]["insertPoint"]);
            Assert.Equal(30.5, (double?)found["A1"]["rotationDeg"]);
            Assert.Equal(new[] { 4.0, 3 }, ((List<double[]>)found["A1"]["outline"])[2]);
            Assert.Null(found["A1#2"]["outline"]);
        }

        private static JArray Pieces()
        {
            return new JArray
            {
                new JObject { ["kind"] = "polyline", ["layer"] = "RD-CURB-LINE", ["handle"] = "C1", ["pts"] = new JArray(new JArray(0, 0, 0), new JArray(10, 0, 0), new JArray(10, 5, 0)), ["closed"] = true },
                new JObject { ["kind"] = "line", ["layer"] = "Ext Road$1$rd-curb-line", ["handle"] = "C2", ["pts"] = new JArray(new JArray(20, 0, 0), new JArray(30, 0, 0)) },
                new JObject { ["kind"] = "polyline", ["layer"] = "ROW", ["handle"] = "R1", ["pts"] = new JArray(new JArray(0, 10, 0), new JArray(50, 10, 0)), ["closed"] = false },
                new JObject { ["kind"] = "polyline", ["layer"] = "RD-CURB-LINE", ["handle"] = "C3", ["pts"] = new JArray(new JArray(5, 5, 0)), ["closed"] = false },
                new JObject { ["kind"] = "arc", ["layer"] = "RD-CURB-LINE", ["handle"] = "C4", ["center"] = new JArray(0, 0), ["radius"] = 1 },
                new JObject { ["kind"] = "polyline", ["layer"] = "RD-CURB-LINE-OLD", ["handle"] = "C5", ["pts"] = new JArray(new JArray(0, 0, 0), new JArray(1, 1, 0)), ["closed"] = false },
            };
        }

        [Fact]
        public void R348_the_edge_steps_take_the_lines_read_road_edges_takes_by_their_place_in_the_read()
        {
            var curbs = ReadSteps.EdgeStep(Pieces(), new[] { "RD-CURB-LINE" });
            Assert.Equal(new[] { "0", "1" }, curbs.Keys.ToArray());
            Assert.Equal("C1", (string)(JToken)curbs["0"]["handle"]);
            Assert.True((bool)(JToken)curbs["0"]["closed"]);
            Assert.Equal(new[] { new[] { 0.0, 0 }, new[] { 10.0, 0 }, new[] { 10.0, 5 }, new[] { 0.0, 0 } }, ((List<double[]>)curbs["0"]["points"]).ToArray());
            Assert.Equal("Ext Road$1$rd-curb-line", (string)(JToken)curbs["1"]["layer"]);
            Assert.Equal(JTokenType.Null, ((JToken)curbs["1"]["closed"]).Type);
            var rows = ReadSteps.EdgeStep(Pieces(), new[] { "ROW" });
            Assert.Equal("R1", (string)(JToken)rows.Single().Value["handle"]);
            Assert.Throws<InvalidDataException>(() => ReadSteps.EdgeStep(Pieces(), new string[0]));
        }

        // The road and ground surfaces of python/tests/landxml_fixture.py, a P holds northing, easting, elevation
        private static string Fixture(string name, string points, string faces, string unit = "meter")
        {
            return "<?xml version=\"1.0\"?><LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\"><Units><Metric linearUnit=\"" + unit + "\"></Metric></Units>"
                + "<CoordinateSystem epsgCode=\"32637\"></CoordinateSystem><Surfaces><Surface name=\"" + name + "\"><Definition surfType=\"TIN\" elevMin=\"1\" elevMax=\"40\"><Pnts>"
                + points + "</Pnts><Faces>" + faces + "</Faces></Definition></Surface></Surfaces></LandXML>";
        }

        private const string RoadPoints = "<P id=\"11\">5000 1000 10</P><P id=\"12\">5000 1010 20</P><P id=\"13\">5010 1010 30</P><P id=\"14\">5010 1000 20</P><P id=\"15\">5000 1020 30</P><P id=\"16\">5010 1020 40</P>";
        private const string RoadFaces = "<F n=\"2 0 0\">11 12 13</F><F n=\"2 0 0\">11 13 14</F><F i=\"1\">12 15 16</F><F n=\"2 0 0\">12 16 13</F><F n=\"2 0 0\">11 12 99</F>";
        private const string GroundPoints = "<P id=\"20\">4000 0 5</P><P id=\"21\">4000 2000 5</P><P id=\"22\">6000 1000 5</P>";
        private const string GroundFaces = "<F n=\"2 0 0\">20 21 22</F>";

        private List<string> WriteRoads(string unit = "meter")
        {
            string road = Path.Combine(folder, "A__Road.xml"), ground = Path.Combine(folder, "B__Ground.xml");
            File.WriteAllText(road, Fixture("Road", RoadPoints, RoadFaces));
            File.WriteAllText(ground, Fixture("Ground", GroundPoints, GroundFaces, unit));
            return new List<string> { road, ground };
        }

        [Fact]
        public void R349_the_surfaces_step_holds_the_unit_counts_bounds_and_levels_of_each_surface()
        {
            var found = ReadSteps.SurfaceStep(WriteRoads().Select(LandXmlReader.Read));
            Assert.Equal(new[] { "Road", "Ground" }, found.Keys.ToArray());
            Dictionary<string, object> road = found["Road"];
            Assert.Equal("A__Road.xml", road["file"]);
            Assert.Equal("meter", road["linearUnit"]);
            Assert.Equal("32637", road["epsgCode"]);
            Assert.Equal(new object[] { 6, 5, 3, 1, 1, 0 }, new[] { "pointCount", "faceCount", "visibleFaceCount", "invisibleFaceCount", "badFaceCount", "badPointCount" }.Select(key => road[key]));
            Assert.Equal("{\"zMin\":\"1\",\"zMax\":\"40\"}", ((JToken)road["declaredZRange"]).ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(new[] { 1000.0, 5000 }, (double[])road["bboxMin"]);
            Assert.Equal(new[] { 1020.0, 5010 }, (double[])road["bboxMax"]);
            Assert.Equal(10.0, road["zMin"]);
            Assert.Equal(40.0, road["zMax"]);
        }

        [Fact]
        public void R349_the_road_levels_step_gives_each_face_vertex_the_level_of_the_first_surface_that_covers_it()
        {
            var surfaces = WriteRoads().Select(path => LandXmlReader.Read(path)).Select(file => new TinSurface(file.Surfaces[0].Name, file.Surfaces[0].Points, file.Surfaces[0].Faces)).ToList();
            var plotsCsv = CsvRows.Parse("baseplanLabel,locationPlanNumber\n1,101\n02,102\n4,104\n");
            var faces = ReadSteps.FaceRings(Plots(), plotsCsv);
            Assert.Equal(new[] { "101" }, faces.Keys.ToArray());
            var found = ReadSteps.RoadLevelStep(new[] { "101", "103" }, faces, surfaces, 0.005);
            Assert.Equal(Enumerable.Range(0, 7).Select(index => "101/" + index), found.Keys);
            Assert.Equal(10.0, (double)(double?)found["101/0"]["level"], 9);
            Assert.Equal("Road", found["101/0"]["surface"]);
            Assert.Equal(15.0, (double)(double?)found["101/2"]["level"], 9);
            Assert.Equal(new[] { 1500.0, 4500 }, (double[])found["101/4"]["point"]);
            Assert.Equal(5.0, (double)(double?)found["101/4"]["level"], 9);
            Assert.Equal("Ground", found["101/4"]["surface"]);
        }

        private static Dictionary<string, object> Row(params object[] pairs)
        {
            var row = new Dictionary<string, object>();
            for (int index = 0; index < pairs.Length; index += 2)
            {
                row[(string)pairs[index]] = pairs[index + 1];
            }
            return row;
        }

        [Fact]
        public void R350_the_area_key_step_gives_the_zone_and_neighbourhood_of_each_plot_of_the_dlus_and_the_sales_sheet()
        {
            var dlus = new[] { Row("plot", 101L, "zone", "Zone 2", "nbh", "012"), Row("plot", "102", "zone", 3L, "nbh", "01a"), Row("plot", 103L, "zone", "-", "nbh", null),
                               Row("plot", 101.0, "zone", "Zone 2", "nbh", "013") };
            var sales = new[] { Row("plot", 102L, "zone", 3.0, "nbh", "01A"), Row("plot", 104L, "zone", 4L, "nbh", "020") };
            var found = ReadSteps.AreaKeyStep(SheetRows.ByPlot(dlus), SheetRows.ByPlot(sales));
            Assert.Equal(new[] { "101", "102", "104" }, found.Keys.ToArray());
            Assert.Equal(new object[] { true, "Zone 2", "013", true, false, null, null, false }, found["101"].Values.ToArray());
            Assert.Equal(new object[] { true, "Zone 3", "01A", false, true, "Zone 3", "01A", false }, found["102"].Values.ToArray());
            Assert.Equal(new[] { "inDlus", "zone", "nbhId", "twoRows", "inSales", "salesZone", "salesNbhId", "salesTwoRows" }, found["102"].Keys.ToArray());
            Assert.False((bool)found["104"]["inDlus"]);
            Assert.Equal("020", found["104"]["salesNbhId"]);
        }

        [Fact]
        public void R350_the_setback_and_sales_steps_hold_the_rows_as_the_python_readers_give_them()
        {
            var setbacks = ReadSteps.DlusSetbackStep(new[] { Row("plot", 101L, "typology", "DP2", "front", 4.5, "rear", "-"), Row("plot", 101.0, "typology", "DP2", "front", 3L) });
            Assert.Equal(new[] { "101", "101#2" }, setbacks.Keys.ToArray());
            Assert.Equal("{\"plotCell\":101,\"typology\":\"DP2\",\"front\":4.5,\"rear\":\"-\"}", Text(setbacks["101"]));
            Assert.Equal("{\"plotCell\":101.0,\"typology\":\"DP2\",\"front\":3}", Text(setbacks["101#2"]));
            var rows = new JObject { ["101"] = new JObject { ["Plot No"] = 101, ["Orientation"] = "EN", ["null"] = 7 } };
            var found = ReadSteps.SalesStep(rows, new[] { Row("plot", 101L, "typology", "DP2", "landWidth", 12.5), Row("plot", "x", "typology", "VL1") });
            Assert.Equal(new[] { "101" }, found.Keys.ToArray());
            Assert.Equal("{\"row\":{\"Plot No\":101,\"Orientation\":\"EN\",\"null\":7},\"plain\":{\"plot\":101,\"typology\":\"DP2\",\"landWidth\":12.5}}", Text(found["101"]));
        }

        private static string Text(Dictionary<string, object> fields)
        {
            return new JObject(fields.Select(pair => new JProperty(pair.Key, pair.Value is JToken token ? token : pair.Value == null ? JValue.CreateNull() : new JValue(pair.Value))))
                .ToString(Newtonsoft.Json.Formatting.None);
        }

        [Theory]
        [InlineData(4.0, "4.0")]
        [InlineData(0.1 + 0.2, "0.30000000000000004")]
        [InlineData(1e16, "1e+16")]
        [InlineData(1234567890123456.0, "1234567890123456.0")]
        [InlineData(0.0001, "0.0001")]
        [InlineData(0.00001, "1e-05")]
        [InlineData(-1.5e-07, "-1.5e-07")]
        [InlineData(12.5, "12.5")]
        [InlineData(570191.037, "570191.037")]
        public void R350_a_float_cell_reads_as_python_str_gives_it(double value, string text)
        {
            Assert.Equal(text, SheetRows.PyRepr(value));
        }

        // The bits Python float() gives for each text: net48 double.Parse read 2264.253152 as the next double up, and its format R wrote
        // the double of 2264.2531520000002 as 2264.253152, 6 cells of the sales sheet on net48 in the first parity run of 4c
        [Theory]
        [InlineData("2264.253152", 4657197560411427285L)]
        [InlineData("2264.2531520000002", 4657197560411427286L)]
        [InlineData("83.950882000000007", 4635607935075817146L)]
        [InlineData("244.62051400000001", 4642810835749377722L)]
        [InlineData("1E3", 4652007308841189376L)]
        [InlineData("-0.5", -4620693217682128896L)]
        [InlineData("123456789012345678901234567890", 5042042089369253694L)]
        [InlineData("4.9406564584124654E-324", 1L)]
        [InlineData("0", 0L)]
        public void R350_a_number_text_reads_as_the_double_python_float_gives(string text, long bits)
        {
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(ExactDouble.Parse(text)));
            double value = BitConverter.Int64BitsToDouble(bits);
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(ExactDouble.Parse(ExactDouble.Text(value))));
        }

        [Fact]
        public void R350_an_output_of_the_worker_writes_every_double_so_it_reads_back_the_same_on_every_runtime()
        {
            double value = BitConverter.Int64BitsToDouble(4657197560411427286L);
            Assert.Equal("2264.2531520000002", ExactDouble.Text(value));
            Assert.Equal("25.0", ExactDouble.Text(25.0));
            string text = new JObject { ["x"] = value, ["y"] = 25.0, ["z"] = double.NaN }.ToString(Newtonsoft.Json.Formatting.None, new ExactDouble.Converter());
            Assert.Equal("{\"x\":2264.2531520000002,\"y\":25.0,\"z\":null}", text);
            Assert.Throws<FormatException>(() => ExactDouble.Parse("12a"));
        }

        [Fact]
        public void R350_plain_values_plot_numbers_zones_and_neighbourhoods_are_those_of_the_python_readers()
        {
            // Python round on the binary value: 2.675 is a little under, 0.0000005 ties to the even 0.0
            Assert.Equal(2.67, SheetRows.PyRound(2.675, 2));
            Assert.Equal(0.0, SheetRows.PyRound(0.0000005, 6));
            Assert.Equal(0.000002, SheetRows.PyRound(0.0000015, 6));
            Assert.Equal(4.123457, SheetRows.PyRound(4.1234567, 6));
            Assert.Equal("x y", SheetRows.Plain("\u001c x y \t"));
            Assert.Null(SheetRows.Plain("   "));
            Assert.Equal(7L, SheetRows.Plain(7L));
            Assert.Equal("101", SheetRows.PlotNumber(101.0));
            Assert.Null(SheetRows.PlotNumber(101.5));
            Assert.Equal("101", SheetRows.PlotNumber("0101"));
            Assert.Equal("12", SheetRows.PlotNumber(" 12 "));
            Assert.Null(SheetRows.PlotNumber(true));
            Assert.Null(SheetRows.PlotNumber("12a"));
            Assert.Equal("0", SheetRows.PlotNumber("000"));
            Assert.Equal("Zone 4", SheetRows.ZoneText(4.0));
            Assert.Equal("Zone 4", SheetRows.ZoneText("Zone 04"));
            Assert.Null(SheetRows.ZoneText("-"));
            Assert.Null(SheetRows.ZoneText(0L));
            Assert.Equal("01A", SheetRows.NbhText(" 01a "));
            Assert.Null(SheetRows.NbhText("n/a"));
            Assert.Equal("101.5", SheetRows.PlotText(101.5));
        }

        [Fact]
        public void R350_sheet_rows_start_under_the_header_row_drop_a_row_with_no_plot_and_refuse_a_column_the_header_lacks()
        {
            var rows = new List<object[]>
            {
                new object[] { "title", null, null },
                new object[] { " Plot No ", "Zone", "NBH" },
                new object[] { 101L, 2.12345678, " 012 " },
                new object[] { null, 3L, "013" },
                new object[] { "  ", 3L, "013" },
            };
            var columns = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("plot", "Plot No"), new KeyValuePair<string, string>("zone", "Zone"), new KeyValuePair<string, string>("nbh", "NBH") };
            List<Dictionary<string, object>> found = SheetRows.Rows("Sales", rows, 2, columns);
            Assert.Equal("{\"plot\":101,\"zone\":2.123457,\"nbh\":\"012\"}", SheetRows.ToJson(found.Single()).ToString(Newtonsoft.Json.Formatting.None));
            columns.Add(new KeyValuePair<string, string>("width", "Land Width (m)"));
            Assert.Contains("Sales has no column Land Width (m)", Assert.Throws<InvalidDataException>(() => SheetRows.Rows("Sales", rows, 2, columns)).Message);
            // The sales rows by plot, the header of the first row, a later row of one plot over an earlier one
            var sales = SheetRows.SalesRows(new List<object[]> { new object[] { "Plot No", null, "Orientation" }, new object[] { 101L, 7L, "EN" }, new object[] { "x", 1L, "SE" }, new object[] { 101.0, 8L, "WS" } }, "Plot No");
            Assert.Equal("{\"101\":{\"Plot No\":101.0,\"null\":8,\"Orientation\":\"WS\"}}", sales.ToString(Newtonsoft.Json.Formatting.None));
        }

        // A made up workbook with a sheet of every kind of cell openpyxl tells apart, written with DocumentFormat.OpenXml
        private string Workbook(string name, IEnumerable<KeyValuePair<string, Func<SheetData>>> sheets, string dimension = "A1:D5")
        {
            string path = Path.Combine(folder, name);
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart book = document.AddWorkbookPart();
                book.Workbook = new Workbook();
                var shared = book.AddNewPart<SharedStringTablePart>();
                shared.SharedStringTable = new SharedStringTable(
                    new SharedStringItem(new Text("Plot No")),
                    new SharedStringItem(new Run(new Text("Orien")), new Run(new Text("tation"))),
                    new SharedStringItem(new Text(" 012 ")),
                    new SharedStringItem(new Text("Zone")));
                var styles = book.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = new Stylesheet(new Fonts(new Font()), new Fills(new Fill()), new Borders(new Border()),
                    new CellFormats(new CellFormat { NumberFormatId = 0 }, new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true }));
                var list = book.Workbook.AppendChild(new Sheets());
                uint id = 1;
                foreach (KeyValuePair<string, Func<SheetData>> sheet in sheets)
                {
                    var part = book.AddNewPart<WorksheetPart>();
                    part.Worksheet = new Worksheet(new SheetDimension { Reference = dimension }, sheet.Value());
                    list.Append(new Sheet { Id = book.GetIdOfPart(part), SheetId = id++, Name = sheet.Key });
                }
            }
            return path;
        }

        private static Cell Cell(string reference, string value, CellValues? type = null, uint? style = null)
        {
            var cell = new Cell { CellReference = reference, CellValue = new CellValue(value) };
            if (type.HasValue)
            {
                cell.DataType = type.Value;
            }
            if (style.HasValue)
            {
                cell.StyleIndex = style.Value;
            }
            return cell;
        }

        private static SheetData Kinds()
        {
            var inline = new Cell { CellReference = "C2", DataType = CellValues.InlineString, InlineString = new InlineString(new Text("in"), new Run(new Text("line"))) };
            var formula = new Cell { CellReference = "D2", CellFormula = new CellFormula("A2*2"), CellValue = new CellValue("202") };
            return new SheetData(
                new Row(Cell("A1", "0", CellValues.SharedString), Cell("B1", "1", CellValues.SharedString), Cell("C1", "2", CellValues.SharedString), new Cell { CellReference = "E1", StyleIndex = 1 }) { RowIndex = 1 },
                new Row(Cell("A2", "101"), Cell("B2", "5.5"), inline, formula) { RowIndex = 2 },
                new Row(Cell("A4", "1E3"), Cell("B4", "1", CellValues.Boolean), Cell("C4", "45000", null, 1), Cell("D4", "#N/A", CellValues.Error)) { RowIndex = 4 });
        }

        [Fact]
        public void R350_a_sheet_is_read_as_openpyxl_reads_it_read_only_with_values_only()
        {
            string path = Workbook("kinds.xlsx", new[] { new KeyValuePair<string, Func<SheetData>>("Kinds", Kinds) });
            XlsxSheet.Sheet sheet = XlsxSheet.Read(path, "Kinds");
            // Every row from row 1 up to the last row of the file, row 3 missing as a row of no values, each four wide by the dimension
            Assert.Equal(4, sheet.Rows.Count);
            Assert.All(sheet.Rows, row => Assert.Equal(4, row.Length));
            Assert.Equal(new object[] { "Plot No", "Orientation", " 012 ", null }, sheet.Rows[0]);
            Assert.Equal(new object[] { 101L, 5.5, "inline", 202L }, sheet.Rows[1]);
            Assert.Equal(new object[] { null, null, null, null }, sheet.Rows[2]);
            Assert.Equal(1000.0, sheet.Rows[3][0]);
            Assert.Equal(true, sheet.Rows[3][1]);
            Assert.Equal("2023-03-15T00:00:00", ((CellTime)sheet.Rows[3][2]).Iso);
            Assert.Equal("#N/A", sheet.Rows[3][3]);
            Assert.Equal(1, sheet.DateCells);
            Assert.Throws<InvalidDataException>(() => XlsxSheet.Read(path, "None such"));
        }

        [Fact]
        public void R350_a_date_serial_reads_as_openpyxl_from_excel_gives_it()
        {
            Assert.Equal("2023-03-15T00:00:00", XlsxSheet.FromExcel(45000, false).Iso);
            Assert.Equal("2023-03-15T12:00:00", XlsxSheet.FromExcel(45000.5, false).Iso);
            Assert.Equal("1900-01-01T00:00:00", XlsxSheet.FromExcel(1, false).Iso);
            Assert.Equal("1900-03-01T00:00:00", XlsxSheet.FromExcel(61, false).Iso);
            Assert.Equal("06:00:00", XlsxSheet.FromExcel(0.25, false).Iso);
            Assert.Equal("1904-01-02T00:00:00", XlsxSheet.FromExcel(1, true).Iso);
            Assert.True(XlsxSheet.IsDate("yyyy-mm-dd"));
            Assert.False(XlsxSheet.IsDate("0.00"));
            Assert.False(XlsxSheet.IsDate("\"days\" 0"));
            Assert.Equal(new int?[] { 1, 1, 26, 3204 }, XlsxSheet.Boundaries("A1:Z3204"));
            Assert.Equal(new int?[] { 1, 1, 1, 1 }, XlsxSheet.Boundaries("A1"));
        }

        // A made up config of the repo config with the road surfaces of the made up LandXML files
        private string Config(Action<JObject> change = null)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["siteBuild"]["roadSurfaces"] = new JArray("A__Road", "B__Ground");
            change?.Invoke(config);
            string path = Path.Combine(folder, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
        }

        private static string LayersFile()
        {
            return Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json");
        }

        // A made up baseplan: one plot of 10 by 20 m on the plot line layer with its label 12 inside, in the units given
        private string Drawing(string name, UnitsType units)
        {
            var document = new CadDocument();
            document.Header.InsUnits = units;
            var line = new Layer("MP_PLOT_LINE");
            var text = new Layer("MP_PLOT_TEXT");
            var curb = new Layer("RD-CURB-LINE");
            document.Layers.Add(line);
            document.Layers.Add(text);
            document.Layers.Add(curb);
            double[] corners = { 1000, 5000, 1010, 5000, 1010, 5020, 1000, 5020 };
            for (int corner = 0; corner < 4; corner++)
            {
                int next = (corner + 1) % 4;
                document.Entities.Add(new Line { StartPoint = new XYZ(corners[corner * 2], corners[corner * 2 + 1], 0), EndPoint = new XYZ(corners[next * 2], corners[next * 2 + 1], 0), Layer = line });
            }
            document.Entities.Add(new MText { Value = "DP1\\P12", InsertPoint = new XYZ(1005, 5010, 0), Layer = text });
            document.Entities.Add(new Line { StartPoint = new XYZ(1000, 4990, 0), EndPoint = new XYZ(1010.0004, 4990, 0), Layer = curb });
            string path = Path.Combine(folder, name);
            using (var writer = new DwgWriter(path, document))
            {
                writer.Write();
            }
            return path;
        }

        private WorkerAnswer Run(WorkerRequest request, int expected)
        {
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, request.RunId));
            Assert.True(code == expected, "exit " + code + ": " + answer.Message);
            return answer;
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        private WorkerRequest Request(string runId, List<WorkerFile> inputs, params string[] steps)
        {
            return new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "read", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" }, Inputs = inputs,
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = Path.Combine(folder, "out"), Steps = steps.ToList(), StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
        }

        [Fact]
        public void R347_q084_the_drawings_are_read_largest_measured_peak_first_and_each_released_before_the_next_read()
        {
            string basePath = Drawing("Base.dwg", UnitsType.Meters), roadPath = Drawing("Road.dwg", UnitsType.Meters);
            // The made up road drawing measured at 3,000 MiB and the baseplan at 100, so the road goes first though the baseplan comes first in the request
            string config = Config(item =>
            {
                var files = (JArray)item["addin"]["workerPeakMiB"]["files"];
                files.Add(new JObject { ["name"] = "Road.dwg", ["bytes"] = new FileInfo(roadPath).Length, ["peakMiB"] = 3000 });
                files.Add(new JObject { ["name"] = "Base.dwg", ["bytes"] = new FileInfo(basePath).Length, ["peakMiB"] = 100 });
            });
            AddinConfig addin = AddinConfig.Load(config);
            var inputs = new List<WorkerFile> { Input("config", config), Input("cadLayers", LayersFile()), Input("baseplan", basePath), Input("curbs", roadPath) };
            Assert.Equal(new[] { "curbs", "baseplan" }, WorkerMemory.ReadOrder(inputs, new[] { "baseplan", "villaBlocks", "curbs" }, addin));
            // Drawings with no measured peak take a peak scaled by their size, two made up files under the smallest measured size the
            // smallest peak, and drawings of one peak keep the order of the roles
            var unmeasured = new List<WorkerFile> { new WorkerFile { Role = "villaBlocks", Path = basePath }, new WorkerFile { Role = "baseplan", Path = roadPath } };
            Assert.Equal(new[] { "baseplan", "villaBlocks" }, WorkerMemory.ReadOrder(unmeasured, new[] { "baseplan", "villaBlocks", "curbs" }, AddinConfig.Load(WorkerRunTests.RepoConfig())));
            WorkerAnswer answer = Run(Request("q84", inputs), 0);
            Assert.Equal(1, answer.Counts["drawing curbs read as"]);
            Assert.Equal(2, answer.Counts["drawing baseplan read as"]);
            Assert.Equal(2, answer.Counts["memory released after a drawing"]);
            Assert.True(WorkerMemory.Release() > 0);
        }

        [Fact]
        public void R348_the_worker_read_writes_the_read_of_a_drawing_in_meters_at_the_decimals_of_r2_and_its_steps()
        {
            string config = Config();
            var inputs = new List<WorkerFile> { Input("config", config), Input("cadLayers", LayersFile()), Input("baseplan", Drawing("Base.dwg", UnitsType.Meters)), Input("curbs", Drawing("Road.dwg", UnitsType.Meters)) };
            WorkerRequest request = Request("r1", inputs, ReadSteps.Labels, ReadSteps.Curbs, ReadSteps.Rows);
            Run(request, 0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1"));
            Assert.Equal("done", answer.Status);
            JObject read = RunStamp.Read(Path.Combine(folder, "out", "read_baseplan.json"));
            Assert.Equal("Meters", (string)read["units"]);
            JToken face = read["plots"].Single(item => (string)item["kind"] == "plot_shape");
            Assert.Equal("12", (string)face["plot"]);
            JObject labels = RunStamp.Read(Path.Combine(folder, "steps", "step_labels_csharp.json"));
            Assert.Equal(new[] { "12" }, ((JObject)labels["plots"]).Properties().Select(item => item.Name));
            JObject curbs = RunStamp.Read(Path.Combine(folder, "steps", "step_curbs_csharp.json"));
            // The curb end at 1010.0004 is written by R2 at 1010, as CadReader writes pieces.json, so a step takes what a build reads
            Assert.Equal("[[1000,4990],[1010,4990]]", curbs["plots"]["0"]["raw"]["points"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Empty(((JObject)RunStamp.Read(Path.Combine(folder, "steps", "step_rows_csharp.json"))["plots"]).Properties());
            Assert.Equal(1, answer.Counts["labels items"]);
            // A build takes the read of the worker as its baseplan, baseplanRead, the face of plot 12 numbered 101 by plots.csv
            string data = Path.Combine(folder, "plots.csv"), labelsCsv = Path.Combine(folder, "labels.csv");
            File.WriteAllText(data, "baseplanLabel,locationPlanNumber\n12,101\n");
            File.WriteAllText(labelsCsv, "number,code,kind,nbh\n101,DP1,clientCode,012\n");
            var build = new WorkerRequest
            {
                FormatVersion = 1, RunId = "r1b", Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { Input("config", config), Input("baseplanRead", Path.Combine(folder, "out", "read_baseplan.json")), Input("plotsCsv", data), Input("labels", labelsCsv) },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = Path.Combine(folder, "out"), Steps = new List<string> { FaceStep.Name }, StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            Run(build, 0);
            JObject faces = RunStamp.Read(Path.Combine(folder, "steps", "step_faces_csharp.json"));
            Assert.True((bool)faces["plots"]["101"]["raw"]["face"]);
            Assert.Equal(200.0, (double)faces["plots"]["101"]["raw"]["area"], 9);
            build.RunId = "r1c";
            build.Inputs.Add(Input("baseplanPlots", Path.Combine(folder, "out", "read_baseplan.json")));
            Assert.Contains("names two baseplans", Run(build, 2).Message);
        }

        // H1 of the fifth review of PR 55: the read steps are of the whole project, the scope read_steps.py writes, where a read of the
        // neighbourhood of a labels.csv with no column nbh gave its road levels of no plot, done
        [Fact]
        public void R348_the_read_steps_are_of_the_whole_project_as_read_steps_py_writes_them_and_another_scope_is_refused()
        {
            var inputs = new List<WorkerFile> { Input("config", Config()), Input("cadLayers", LayersFile()), Input("baseplan", Drawing("Base.dwg", UnitsType.Meters)) };
            WorkerRequest request = Request("h1", inputs, ReadSteps.Labels);
            request.Scope = new WorkerScope { Kind = "nbh", Nbh = "012" };
            Run(request, 2);
            Assert.Equal("the read steps are of the whole project, the scope project of read_steps.py, a read of the steps labels with the scope nbh=012 is refused (R348)",
                WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "h1")).Message);
            Assert.False(File.Exists(Path.Combine(folder, "steps", "step_labels_csharp.json")));
        }

        [Fact]
        public void R348_a_drawing_not_in_meters_or_kept_for_reference_only_is_refused_with_exit_2_and_nothing_is_written()
        {
            string config = Config();
            string millimetres = Drawing("Base mm.dwg", UnitsType.Millimeters);
            WorkerRequest request = Request("r2", new List<WorkerFile> { Input("config", config), Input("cadLayers", LayersFile()), Input("baseplan", millimetres) }, ReadSteps.Labels);
            Run(request, 2);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r2"));
            Assert.Equal("refused", answer.Status);
            Assert.Contains("Base mm.dwg is in Millimeters by its header InsUnits", answer.Message);
            Assert.False(File.Exists(Path.Combine(folder, "out", "read_baseplan.json")));
            Assert.False(File.Exists(Path.Combine(folder, "steps", "step_labels_csharp.json")));
            string reference = Drawing("221229_MP-SK-01-03-001_R06_0000.dwg", UnitsType.Meters);
            request = Request("r3", new List<WorkerFile> { Input("config", config), Input("cadLayers", LayersFile()), Input("villaBlocks", reference) }, ReadSteps.Blocks);
            Run(request, 2);
            Assert.Contains("kept for reference only", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r3")).Message);
            // A drawing read needs the layer list, a read step of the command build is refused
            request = Request("r4", new List<WorkerFile> { Input("config", config), Input("baseplan", Drawing("Base.dwg", UnitsType.Meters)) }, ReadSteps.Labels);
            Run(request, 2);
            Assert.Contains("needs the input cadLayers", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r4")).Message);
            request = Request("r5", new List<WorkerFile> { Input("config", config), Input("cadLayers", LayersFile()), Input("baseplan", Drawing("Base.dwg", UnitsType.Meters)) }, ReadSteps.Labels);
            request.Command = "build";
            Run(request, 2);
            Assert.Contains("the step labels is a step of the command read", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r5")).Message);
        }

        [Fact]
        public void R348_the_build_takes_the_read_of_the_worker_and_refuses_a_baseplan_output_not_in_meters()
        {
            string plots = Path.Combine(folder, "read_baseplan.json");
            File.WriteAllText(plots, new JObject { ["units"] = "Millimeters", ["plots"] = new JArray() }.ToString());
            Assert.Contains("is of a drawing in Millimeters", CadUnits.Refusal(plots, JObject.Parse(File.ReadAllText(plots))));
            Assert.Null(CadUnits.Refusal(plots, new JObject { ["units"] = "Meters" }));
            // A CadReader plots.json carries no units, its layers.json beside it does, and with none the units are not known
            string bare = Path.Combine(folder, "plots.json");
            File.WriteAllText(bare, "{\"plots\": []}");
            Assert.Contains("has no layers.json of its read beside it", CadUnits.Refusal(bare, JObject.Parse(File.ReadAllText(bare))));
            File.WriteAllText(Path.Combine(folder, "layers.json"), "{\"units\": \"Meters\"}");
            Assert.Null(CadUnits.Refusal(bare, JObject.Parse(File.ReadAllText(bare))));
            // The build of the step faces refuses a baseplan output of a drawing in Millimeters with exit 2 before it uses it
            string data = Path.Combine(folder, "plots.csv"), labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(data, "baseplanLabel,locationPlanNumber\n1,101\n");
            File.WriteAllText(labels, "number,code,kind,nbh\n101,DP1,clientCode,012\n");
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "b1", Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { Input("config", WorkerRunTests.RepoConfig()), Input("baseplanPlots", plots), Input("plotsCsv", data), Input("labels", labels) },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, Steps = new List<string> { FaceStep.Name }, StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            Assert.Contains("read_baseplan.json is of a drawing in Millimeters", Run(request, 2).Message);
        }

        // R349 by R344: the snap is held against snapMaxM as plan_rules.road_snap holds it, before any surface is read, and a corner
        // exactly at the snap off the edge of a road surface is covered, one further off takes no level and no surface sideways
        [Theory]
        [InlineData("0.02")]
        [InlineData("-0.001")]
        [InlineData("true")]
        [InlineData("\"0.005\"")]
        [InlineData("NaN")]
        public void R349_r344_the_worker_read_refuses_a_snap_that_is_no_distance_of_0_m_to_snap_max(string snap)
        {
            string config = Config(item => item["siteBuild"]["snapM"] = JToken.Parse(snap));
            List<string> roads = WriteRoads();
            var inputs = new List<WorkerFile> { Input("config", config), Input("roadSurface:A__Road", roads[0]), Input("roadSurface:B__Ground", roads[1]) };
            WorkerAnswer answer = Run(Request("n1", inputs, ReadSteps.Surfaces), 2);
            Assert.Contains("siteBuild.snapM must be a finite number from 0 m to snapMaxM", answer.Message);
            Assert.False(File.Exists(Path.Combine(folder, "out", "read_roadSurfaces.json")));
        }

        [Fact]
        public void R349_r344_a_corner_exactly_at_the_snap_is_covered_and_one_past_it_takes_no_road_sideways()
        {
            var settings = new ReadSettings(JObject.Parse("{\"siteBuild\": {\"snapM\": 0.005, \"snapMaxM\": 0.01}}"));
            Assert.Equal(0.005, settings.SnapM);
            // The road surface of the fixture starts at easting 1000 between northing 5000 and 5010, nothing lies west of it
            TinSurface road = TinSurface.Of(LandXmlReader.Read(WriteRoads()[0]).Surfaces[0]);
            KeyValuePair<double?, string> near = TinSurface.RoadLevel(new[] { road }, 999.996, 5005, settings.SnapM);
            Assert.Equal("Road", near.Value);
            Assert.Equal(15.0, near.Key.Value, 9);
            Assert.Null(TinSurface.RoadLevel(new[] { road }, 999.994, 5005, settings.SnapM).Key);
            Assert.Throws<InvalidDataException>(() => new ReadSettings(JObject.Parse("{\"siteBuild\": {\"snapM\": 0.005}}")).SnapM);
        }

        [Fact]
        public void R349_the_worker_read_takes_the_road_surfaces_of_config_in_their_order_and_refuses_one_not_in_meter()
        {
            string config = Config();
            List<string> roads = WriteRoads();
            string baseplan = Drawing("Base.dwg", UnitsType.Meters);
            string plotsCsv = Path.Combine(folder, "plots.csv"), labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(plotsCsv, "baseplanLabel,locationPlanNumber\n12,101\n");
            File.WriteAllText(labels, "number,code,kind,nbh\n101,DP1,clientCode,012\n");
            var inputs = new List<WorkerFile> { Input("config", config), Input("cadLayers", LayersFile()), Input("baseplan", baseplan), Input("plotsCsv", plotsCsv), Input("labels", labels),
                                                Input("roadSurface:B__Ground", roads[1]), Input("roadSurface:A__Road", roads[0]) };
            Run(Request("s1", inputs, ReadSteps.Surfaces, ReadSteps.RoadLevels), 0);
            JObject surfaces = RunStamp.Read(Path.Combine(folder, "steps", "step_surfaces_csharp.json"));
            Assert.Equal(new[] { "Road", "Ground" }, ((JObject)surfaces["plots"]).Properties().Select(item => item.Name));
            JObject levels = RunStamp.Read(Path.Combine(folder, "steps", "step_roadLevels_csharp.json"));
            // The four corners of plot 12: the south two on the road surface, the north two past it on the ground surface
            Assert.Equal(new[] { "101/0", "101/1", "101/2", "101/3" }, ((JObject)levels["plots"]).Properties().Select(item => item.Name));
            Assert.Equal(new[] { "Ground", "Ground", "Road", "Road" }, ((JObject)levels["plots"]).Properties().Select(item => (string)item.Value["raw"]["surface"]).OrderBy(name => name));
            Assert.Equal(new[] { 5.0, 5.0 }, ((JObject)levels["plots"]).Properties().Where(item => (string)item.Value["raw"]["surface"] == "Ground").Select(item => (double)item.Value["raw"]["level"]));
            // A surface of the order missing from the request, and a LandXML in foot, are refused
            Run(Request("s2", inputs.Take(6).ToList(), ReadSteps.Surfaces), 2);
            Assert.Contains("not in the request A__Road", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "s2")).Message);
            roads = WriteRoads("foot");
            inputs[5] = Input("roadSurface:B__Ground", roads[1]);
            Run(Request("s3", inputs, ReadSteps.Surfaces), 2);
            Assert.Contains("B__Ground.xml is in foot", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "s3")).Message);
        }

        // The DLUS workbook and the sales workbook with the sheets of dataBook of the repo config
        private KeyValuePair<string, string> Workbooks()
        {
            JObject book = (JObject)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["dataBook"];
            JObject area = (JObject)book["data5"]["areaKey"]["dlus"], areaSales = (JObject)book["data5"]["areaKey"]["sales"];
            JObject setbacks = (JObject)book["rev9"]["dlus"], sales = (JObject)book["rev9"]["sales"];
            Func<IEnumerable<object[]>, Func<SheetData>> rows = items => () =>
            {
                var data = new SheetData();
                uint index = 1;
                foreach (object[] item in items)
                {
                    var row = new Row { RowIndex = index };
                    for (int column = 0; column < item.Length; column++)
                    {
                        if (item[column] == null)
                        {
                            continue;
                        }
                        string reference = (char)('A' + column) + index.ToString(CultureInfo.InvariantCulture);
                        row.Append(item[column] is string text
                            ? new Cell { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) }
                            : new Cell { CellReference = reference, CellValue = new CellValue(Convert.ToString(item[column], CultureInfo.InvariantCulture)) });
                    }
                    data.Append(row);
                    index++;
                }
                return data;
            };
            string dlus = Workbook("dlus.xlsx", new[]
            {
                new KeyValuePair<string, Func<SheetData>>((string)area["sheet"], rows(new[]
                {
                    new object[] { (string)area["columns"]["plot"], (string)area["columns"]["zone"], (string)area["columns"]["nbh"] },
                    new object[] { 101, "Zone 2", "012" }, new object[] { "102", 3, "01a" },
                })),
            }, "A1:H4");
            string salesBook = Workbook("sales.xlsx", new[]
            {
                new KeyValuePair<string, Func<SheetData>>((string)areaSales["sheet"], rows(new[]
                {
                    new object[] { (string)sales["columns"]["plot"], (string)sales["columns"]["typology"], (string)sales["columns"]["typologyOld"], (string)sales["columns"]["nbh"],
                                   (string)sales["columns"]["zone"], (string)sales["columns"]["landWidth"], (string)sales["columns"]["landLength"], "Orientation" },
                    new object[] { 101, "DP2", "D02", "012", 2, 12.5, 25, "EN" },
                })),
                new KeyValuePair<string, Func<SheetData>>((string)setbacks["sheet"], rows(new[]
                {
                    new object[] { "title" }, new object[0],
                    new[] { "plot", "typology", "nbh", "front", "right", "left", "rear" }.Select(key => (object)(string)setbacks["columns"][key]).ToArray(),
                    new object[] { 101, "DP2", "012", 4.5, 0, "1.5", null },
                })),
            }, "A1:H4");
            return new KeyValuePair<string, string>(dlus, salesBook);
        }

        [Fact]
        public void R350_the_worker_read_gives_the_area_key_the_setbacks_and_the_sales_rows_of_the_sheets_of_config()
        {
            KeyValuePair<string, string> books = Workbooks();
            var inputs = new List<WorkerFile> { Input("config", Config()), Input("dlus", books.Key), Input("sales", books.Value) };
            Run(Request("w1", inputs, ReadSteps.AreaKey, ReadSteps.DlusSetbacks, ReadSteps.Sales), 0);
            JObject area = RunStamp.Read(Path.Combine(folder, "steps", "step_areaKey_csharp.json"));
            Assert.Equal(new[] { "101", "102" }, ((JObject)area["plots"]).Properties().Select(item => item.Name));
            Assert.Equal("Zone 3", (string)area["plots"]["102"]["raw"]["zone"]);
            Assert.Equal("01A", (string)area["plots"]["102"]["raw"]["nbhId"]);
            Assert.Equal("Zone 2", (string)area["plots"]["101"]["raw"]["salesZone"]);
            JObject setbacks = RunStamp.Read(Path.Combine(folder, "steps", "step_dlusSetbacks_csharp.json"));
            Assert.Equal("{\"plotCell\":101,\"typology\":\"DP2\",\"nbh\":\"012\",\"front\":4.5,\"right\":0,\"left\":\"1.5\",\"rear\":null}",
                setbacks["plots"]["101"]["raw"].ToString(Newtonsoft.Json.Formatting.None));
            JObject sales = RunStamp.Read(Path.Combine(folder, "steps", "step_sales_csharp.json"));
            Assert.Equal("EN", (string)sales["plots"]["101"]["raw"]["row"]["Orientation"]);
            Assert.Equal(12.5, (double)sales["plots"]["101"]["raw"]["plain"]["landWidth"]);
            Assert.True(File.Exists(Path.Combine(folder, "out", "read_workbooks.json")));
        }
    }
}
