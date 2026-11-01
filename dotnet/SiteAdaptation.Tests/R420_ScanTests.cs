using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CadReader;
using CSMath;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R420 to R427. The scanner on dummy drawings, sheets and pages made here, layers A to Z and codes X1 to X4 of a dummy profile
    public class R420_ScanTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sat-" + Guid.NewGuid().ToString("N"));

        public R420_ScanTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static JObject Profile()
        {
            return new JObject
            {
                ["typologies"] = new JObject { ["X1"] = new JObject { ["keo_file"] = "X1" }, ["X2"] = new JObject { ["keo_file"] = "X1" }, ["X3"] = new JObject() },
                ["typology_codes"] = new JObject { ["rows"] = new JArray(new JObject { ["old"] = "X3", ["new"] = "X4" }, new JObject { ["old"] = "X1", ["new"] = "X1" }, new JObject { ["old"] = "X3-0 (Q)", ["new"] = "X5" }) },
                ["scan"] = new JObject
                {
                    ["labelPattern"] = "^(?:(.*?)\\s+)?(\\d{1,6})$", ["tokenSplit"] = "[^A-Za-z0-9]+", ["plotNumberShifts"] = new JArray(0, 10), ["pairMinSegments"] = 2, ["pairThreads"] = 2, ["pairTimeCapS"] = 600,
                    ["pdfJoinGapPt"] = 0.3, ["pdfLineTolerancePt"] = 0.05, ["pdfPairDyPt"] = new JArray(1.3, 1.8), ["pdfPairDxPt"] = 1.5,
                },
            };
        }

        private static readonly ScanSettings Settings = ScanSettings.From(Profile());

        private static HashSet<string> Codes()
        {
            return ScanLogic.KnownCodes(Profile(), null, new string[0]);
        }

        private static Layer Layer(CadDocument document, string name)
        {
            if (!document.Layers.TryGetValue(name, out Layer layer))
            {
                layer = new Layer(name);
                document.Layers.Add(layer);
            }
            return layer;
        }

        private static Line Line(CadDocument document, string layer, double x1, double y1, double x2, double y2)
        {
            return new Line { StartPoint = new XYZ(x1, y1, 0), EndPoint = new XYZ(x2, y2, 0), Layer = Layer(document, layer) };
        }

        // A square of side 10 from x, y, its bottom and left on one layer and its top and right on another
        private static IEnumerable<Entity> Square(CadDocument document, string first, string second, double x, double y)
        {
            yield return Line(document, first, x, y, x + 10, y);
            yield return Line(document, first, x, y, x, y + 10);
            yield return Line(document, second, x, y + 10, x + 10, y + 10);
            yield return Line(document, second, x + 10, y, x + 10, y + 10);
        }

        private static TextEntity Text(CadDocument document, string layer, string value, double x, double y)
        {
            return new TextEntity { Value = value, InsertPoint = new XYZ(x, y, 0), Height = 1, Layer = Layer(document, layer) };
        }

        private static Insert Insert(CadDocument document, string layer, string block, double x, double y)
        {
            BlockRecord record;
            if (!document.BlockRecords.TryGetValue(block, out record))
            {
                record = new BlockRecord(block);
                record.Entities.Add(new Point { Location = new XYZ(0, 0, 0), Layer = Layer(document, "0") });
                document.BlockRecords.Add(record);
            }
            return new Insert(record) { InsertPoint = new XYZ(x, y, 0), Layer = Layer(document, layer) };
        }

        private static void Add(CadDocument document, IEnumerable<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                document.Entities.Add(entity);
            }
        }

        private static JObject Scan(CadDocument document, string name)
        {
            return CadScan.Inventory(document, name, name, 1, "00", Settings.PairMinSegments);
        }

        // A drawing of plots numbered by texts on T, each a square on A
        private static CadDocument Plots(params object[] plots)
        {
            var document = new CadDocument();
            for (int index = 0; index < plots.Length; index += 3)
            {
                double x = Convert.ToDouble(plots[index + 1]), y = Convert.ToDouble(plots[index + 2]);
                Add(document, Square(document, "A", "A", x, y));
                document.Entities.Add(Text(document, "T", (string)plots[index], x + 5, y + 5));
            }
            return document;
        }

        // The prototype codes stand for the codes of the prototype file names a scan is given, the share for majority.strongShare
        private static JObject Result(IEnumerable<JObject> cads, IEnumerable<ScanWorkbook> excels = null, IEnumerable<ScanPdf> pdfs = null, IEnumerable<string> prototypes = null,
            double share = 0.9)
        {
            var prototypeCodes = new HashSet<string>(prototypes ?? new[] { "X1", "X2", "X3", "X4" }, StringComparer.Ordinal);
            return ScanLogic.Result(Settings, Codes(), prototypeCodes, share, ScanLogic.OldNewPairs(Profile()), cads, excels ?? new ScanWorkbook[0], pdfs ?? new ScanPdf[0], new JArray());
        }

        private static JObject RowOf(JObject result, string kind, string source)
        {
            return ((JArray)result["rows"]).Cast<JObject>().Single(row => (string)row["kind"] == kind && (string)row["source"] == source);
        }

        private static List<string> Checks(JObject result, string what)
        {
            return ((JArray)result["checks"]).Where(check => (string)check["what"] == what).Select(check => (string)check["source"] + "|" + (string)check["plot"] + "|" + (string)check["value"]).ToList();
        }

        [Fact]
        public void R429_layer_pairs_past_the_time_cap_are_not_traced_and_written_as_not_read()
        {
            var document = new CadDocument();
            foreach (string layer in new[] { "A", "B", "C", "D" })
            {
                for (int line = 0; line <= 3; line++)
                {
                    document.Entities.Add(Line(document, layer, 3 * line, -1, 3 * line, 10));
                    document.Entities.Add(Line(document, layer, -1, 3 * line, 10, 3 * line));
                }
            }
            JObject cad = CadScan.Inventory(document, "grid.dxf", "grid.dxf", 1, "00", 2, 2, 1e-9, null);
            Assert.Equal(4, ((JArray)cad["candidates"]).Count);
            Assert.Equal(new[] { "A + B", "A + C", "A + D", "B + C", "B + D", "C + D" }, ((JArray)cad["notTraced"]).Select(item => (string)item).ToArray());
            JObject entry = ((JArray)cad["notRead"]).Cast<JObject>().Single();
            Assert.Equal("grid.dxf|layer pairs", (string)entry["source"]);
            Assert.Equal("6 of 6 not traced, time cap 0 s, listed under notTraced", (string)entry["value"]);
            JObject result = Result(new[] { cad });
            Assert.Contains("grid.dxf|layer pairs||6 of 6 not traced, time cap 0 s, listed under notTraced", Checks(result, "notRead"));
            // A file the cap left pairs of proposes no outline, the same file traced whole proposes one
            JObject capped = Result(new[] { JObject.Parse(Scan(Plots("1", 0, 0, "2", 20, 0), "two.dxf").ToString()) });
            JObject plots = JObject.Parse(Scan(Plots("1", 0, 0, "2", 20, 0), "two.dxf").ToString());
            plots["notTraced"] = new JArray("A + T");
            Assert.Contains(((JArray)capped["rows"]).Cast<JObject>(), row => (string)row["kind"] == "plotOutline" && (string)row["proposedUse"] == "plot outlines");
            JObject left = Result(new[] { plots });
            Assert.DoesNotContain(((JArray)left["rows"]).Cast<JObject>(), row => (string)row["kind"] == "plotOutline" && (string)row["proposedUse"] == "plot outlines");
            Assert.Empty((JArray)left["plots"]);
        }

        [Fact]
        public void R428_the_pair_trace_on_several_threads_writes_the_inventory_one_thread_writes()
        {
            var document = new CadDocument();
            for (int index = 0; index < 4; index++)
            {
                string layer = new[] { "A", "B", "C", "D" }[index];
                for (int line = 0; line <= 6; line++)
                {
                    document.Entities.Add(Line(document, layer, 3 * line + index, -1, 3 * line + index, 20));
                    document.Entities.Add(Line(document, layer, -1, 3 * line + index, 20, 3 * line + index));
                }
            }
            string one = CadScan.Inventory(document, "grid.dxf", "grid.dxf", 1, "00", 2, 1, 600, null).ToString();
            string three = CadScan.Inventory(document, "grid.dxf", "grid.dxf", 1, "00", 2, 3, 600, null).ToString();
            Assert.Equal(one, three);
            Assert.Equal(4 + 6, ((JArray)JObject.Parse(three)["candidates"]).Count);
        }

        [Fact]
        public void R428_the_cell_index_of_the_trace_gives_what_the_loops_over_every_item_give()
        {
            // Random drawings at site and at small coordinates: lines that cross, ends within the node tolerance of a line, arcs of small
            // and of large radius, long and short pieces together, so the index is held to the old loops byte for byte
            var random = new Random(4211);
            int faces = 0;
            foreach (double origin in new[] { 0.0, 2400000.0 })
            {
                for (int drawing = 0; drawing < 40; drawing++)
                {
                    var segs = new List<PlotShapes.Seg>();
                    for (int line = 0; line < 60; line++)
                    {
                        double x = origin + random.NextDouble() * 100, y = origin + random.NextDouble() * 100;
                        double length = line % 7 == 0 ? 400 : 1 + random.NextDouble() * 30;
                        double angle = random.Next(8) * Math.PI / 4 + (line % 5 == 0 ? random.NextDouble() * 1e-3 : 0);
                        segs.Add(new PlotShapes.Seg { X1 = x, Y1 = y, X2 = x + length * Math.Cos(angle), Y2 = y + length * Math.Sin(angle) });
                        if (line % 3 == 0)
                        {
                            // An end that stops within the node tolerance of the line before it
                            PlotShapes.Seg last = segs[segs.Count - 2 < 0 ? 0 : segs.Count - 2];
                            double mx = (last.X1 + last.X2) / 2, my = (last.Y1 + last.Y2) / 2 + 0.004;
                            segs.Add(new PlotShapes.Seg { X1 = mx, Y1 = my, X2 = mx + 5, Y2 = my + 9 });
                        }
                    }
                    for (int arc = 0; arc < 6; arc++)
                    {
                        double radius = arc % 2 == 0 ? 2 + random.NextDouble() * 10 : 500;
                        segs.Add(new PlotShapes.Seg
                        {
                            IsArc = true, Cx = origin + random.NextDouble() * 100, Cy = origin + random.NextDouble() * 100, Radius = radius,
                            A0 = random.NextDouble() * Math.PI, A1 = Math.PI + random.NextDouble() * Math.PI,
                        });
                    }
                    string indexed = Newtonsoft.Json.JsonConvert.SerializeObject(PlotShapes.Build(segs, "A", false));
                    string every = Newtonsoft.Json.JsonConvert.SerializeObject(PlotShapes.Build(segs, "A", true));
                    Assert.Equal(every, indexed);
                    faces += PlotShapes.Build(segs, "A", false).Count(shape => shape.Kind == "plot_shape");
                }
            }
            Assert.True(faces > 100, "the drawings trace " + faces + " faces, too few to test the trace");
        }

        [Fact]
        public void R421_long_lines_crossed_and_met_by_many_short_ones_trace_every_face_of_the_comb()
        {
            // Round 4j-1b. Two lines of 1000 m and 101 short ones between them, the odd ones crossing both long lines and the even ones
            // ending on them, the case where a box index of a long line left the trace as slow as a loop over every piece
            var document = new CadDocument();
            document.Entities.Add(Line(document, "A", 0, 0, 1000, 0));
            document.Entities.Add(Line(document, "A", 0, 5, 1000, 5));
            for (int index = 0; index <= 100; index++)
            {
                double x = 10 * index;
                document.Entities.Add(index % 2 == 1 ? Line(document, "A", x, -1, x, 6) : Line(document, "A", x, 0, x, 5));
            }
            JObject cad = Scan(document, "comb.dxf");
            JObject layer = ((JArray)cad["layers"]).Cast<JObject>().Single(row => (string)row["layer"] == "A");
            Assert.Equal(100, (int)layer["closedOutlines"]);
        }

        [Fact]
        public void R424_the_centroid_of_a_face_at_site_coordinates_is_its_corner_mean_within_a_micrometre()
        {
            // A parallelogram of 300 m2 turned 30 degrees, at coordinates of the size of a site grid and of no project
            var ring = new List<double[]>
            {
                new[] { 500010.0, 2400020.0 }, new[] { 500031.651, 2400032.5 }, new[] { 500025.651, 2400042.892 }, new[] { 500004.0, 2400030.392 },
            };
            double[] centre = CadScan.Centroid(ring);
            Assert.InRange(centre[0], ring.Average(p => p[0]) - 1e-6, ring.Average(p => p[0]) + 1e-6);
            Assert.InRange(centre[1], ring.Average(p => p[1]) - 1e-6, ring.Average(p => p[1]) + 1e-6);
        }

        [Fact]
        public void R420_the_inventory_counts_each_layer_after_the_explode_of_a_nested_insert_with_no_layer_dropped()
        {
            var document = new CadDocument();
            var inner = new BlockRecord("IN");
            document.BlockRecords.Add(inner);
            foreach (Entity entity in Square(document, "A", "A", 0, 0))
            {
                inner.Entities.Add(entity);
            }
            inner.Entities.Add(Text(document, "B", "7", 5, 5));
            var outer = new BlockRecord("OUT");
            document.BlockRecords.Add(outer);
            outer.Entities.Add(new Insert(inner) { InsertPoint = new XYZ(10, 0, 0), Layer = Layer(document, "C") });
            document.Entities.Add(new Insert(outer) { InsertPoint = new XYZ(100, 100, 0), Layer = Layer(document, "C") });
            document.Entities.Add(new LwPolyline(new[]
            {
                new LwPolyline.Vertex(new XY(0, 0)), new LwPolyline.Vertex(new XY(4, 0)) { Bulge = 1 }, new LwPolyline.Vertex(new XY(4, 4)), new LwPolyline.Vertex(new XY(0, 4)),
            }) { IsClosed = true, Layer = Layer(document, "D") });

            JObject cad = Scan(document, "f.dxf");
            var layers = ((JArray)cad["layers"]).ToDictionary(row => (string)row["layer"], row => string.Join(" ", new[] { "closedOutlines", "texts", "blocks", "segments" }.Select(key => (int)row[key])));
            Assert.Equal(new[] { "0", "A", "B", "C", "D" }, layers.Keys.ToArray());
            Assert.Equal("1 0 0 4", layers["A"]);
            Assert.Equal("0 1 0 0", layers["B"]);
            Assert.Equal("0 0 2 0", layers["C"]);
            Assert.Equal("1 0 0 4", layers["D"]);
            Assert.Equal("0 0 0 0", layers["0"]);
            // Both transforms reach the pieces of the inner block
            Assert.All(((JArray)cad["lines"]["A"]).Select(line => (double)line[0]), x => Assert.InRange(x, 110, 120));
            JToken text = ((JArray)cad["texts"]).Single();
            Assert.Equal("B 7 115 105", (string)text["layer"] + " " + (string)text["text"] + " " + (double)text["x"] + " " + (double)text["y"]);
            Assert.Equal(new[] { "C OUT", "C IN" }, ((JArray)cad["inserts"]).Select(insert => (string)insert["layer"] + " " + (string)insert["block"]).ToArray());
            // The side of bulge 1 is one arc, a half circle on the side from 4 0 to 4 4, so the outline is a face with it
            JArray arc = (JArray)((JArray)cad["arcs"]["D"]).Single();
            Assert.Equal(new[] { 4.0, 2.0, 2.0, -90.0, 90.0 }, arc.Select(value => (double)value).ToArray());
            Assert.Equal(3, ((JArray)cad["lines"]["D"]).Count);
            Assert.Equal(new[] { "A", "A + D", "D" }, ((JArray)cad["candidates"]).Select(candidate => string.Join(" + ", candidate["layers"].Select(layer => (string)layer))).ToArray());
            JToken face = ((JArray)cad["candidates"])[0]["faces"].Single();
            Assert.Equal(100.0, (double)face["area"], 6);
            Assert.Equal(new[] { 115.0, 105.0 }, face["centroid"].Select(value => (double)value).ToArray());
            Assert.Equal(4, ((JArray)face["ring"]).Count);
        }

        [Fact]
        public void R421_a_plot_outline_made_only_by_the_pair_of_two_layers_wins_over_each_layer_alone()
        {
            var document = new CadDocument();
            Add(document, Square(document, "A", "A", 0, 0));
            Add(document, Square(document, "A", "B", 20, 0));
            Add(document, Square(document, "A", "B", 40, 0));
            document.Entities.Add(Text(document, "T", "1", 5, 5));
            document.Entities.Add(Text(document, "T", "2", 25, 5));
            document.Entities.Add(Text(document, "T", "3", 45, 5));

            JObject result = Result(new[] { Scan(document, "f.dxf") });
            Assert.Equal("1 faces check 1", Short(RowOf(result, "plotOutline", "f.dxf|A")));
            Assert.Equal("3 faces plot outlines 3", Short(RowOf(result, "plotOutline", "f.dxf|A + B")));
            Assert.DoesNotContain(((JArray)result["rows"]).Cast<JObject>(), row => (string)row["source"] == "f.dxf|B" && (string)row["kind"] == "plotOutline");
            Assert.Equal("3 labels in plots plot numbers 3", Short(RowOf(result, "plotNumber", "f.dxf|T")));
            Assert.Equal(new[] { "1", "2", "3" }, result["plots"].Select(plot => (string)plot["plot"]).ToArray());
            Assert.Empty(Checks(result, "tie"));
        }

        private static string Short(JObject row)
        {
            return (string)row["found"] + " " + (string)row["proposedUse"] + " " + (int)row["score"];
        }

        [Fact]
        public void R421_a_tie_of_two_outline_layers_proposes_none_and_writes_a_check_row()
        {
            var document = new CadDocument();
            // One plot drawn twice, on A and on C, so A alone, C alone and the pair each cover it once
            Add(document, Square(document, "A", "A", 0, 0));
            Add(document, Square(document, "C", "C", 0, 0));
            document.Entities.Add(Text(document, "T", "1", 5, 5));

            JObject result = Result(new[] { Scan(document, "f.dxf") });
            Assert.Equal("1 faces check 1", Short(RowOf(result, "plotOutline", "f.dxf|A")));
            Assert.Equal("1 faces check 1", Short(RowOf(result, "plotOutline", "f.dxf|A + C")));
            Assert.Equal("1 faces check 1", Short(RowOf(result, "plotOutline", "f.dxf|C")));
            Assert.Equal(new[] { "plotOutline||f.dxf|A or f.dxf|A + C or f.dxf|C" }, Checks(result, "tie"));
            Assert.Equal(new[] { "||" }, Checks(result, "noPlotDrawing"));
            Assert.Empty((JArray)result["plots"]);
        }

        [Fact]
        public void R424_two_drawings_link_by_plot_and_each_field_that_fails_is_a_conflict_row_with_both_values()
        {
            CadDocument first = Plots("1", 0, 0, "2", 20, 0, "3", 40, 0, "4", 60, 0, "5", 80, 0);
            CadDocument second = Plots("1", 0, 0, "2", 200, 0, "Q 3", 40, 0, "9", 60, 0);

            JObject result = Result(new[] { Scan(first, "f.dxf"), Scan(second, "g.dxf") });
            List<string> conflicts = result["conflicts"].Select(item => string.Join("|", new[] { "plot", "field", "valueA", "sourceA", "valueB", "sourceB" }.Select(key => (string)item[key]))).ToList();
            Assert.Equal(new[]
            {
                "2|location|25.000, 5.000|f.dxf|A|205.000, 5.000|g.dxf|A",
                "3|label|3|f.dxf|A|Q 3|g.dxf|A",
                "4|number|4|f.dxf|A|9|g.dxf|A",
            }, conflicts);
            Assert.Equal(3, (int)RowOf(result, "plotOutline", "f.dxf|A")["conflicts"]);
            Assert.Equal("plot outlines", (string)RowOf(result, "plotOutline", "g.dxf|A")["proposedUse"]);
            Assert.Equal(new[] { "1", "2", "3", "4", "5" }, result["plots"].Select(plot => (string)plot["plot"]).ToArray());
        }

        [Fact]
        public void R421_a_number_two_plots_of_the_plot_drawing_hold_is_a_check_row_and_joins_nothing()
        {
            JObject result = Result(new[] { Scan(Plots("1", 0, 0, "1", 20, 0, "2", 40, 0), "f.dxf") });
            Assert.Equal(new[] { "f.dxf|A|1|2" }, Checks(result, "numberTwice"));
            Assert.Equal(new[] { "2" }, result["plots"].Select(plot => (string)plot["plot"]).ToArray());
        }

        [Fact]
        public void R425_a_value_with_two_codes_is_a_check_and_gives_no_villa_type_and_a_code_matches_whole_or_by_token_with_case()
        {
            CadDocument document = Plots("1", 0, 0, "2", 20, 0, "3", 40, 0);
            document.Entities.Add(Text(document, "N", "X1/X2", 3, 3));
            document.Entities.Add(Text(document, "N", "X1", 7, 3));
            document.Entities.Add(Text(document, "N", "type X2", 23, 3));
            document.Entities.Add(Text(document, "N", "x1", 43, 3));

            JObject result = Result(new[] { Scan(document, "f.dxf") });
            Assert.Equal(new[] { "f.dxf|text|N|1|X1/X2" }, Checks(result, "twoCodes"));
            Assert.Equal("X2 villa type 1", Short(RowOf(result, "villaType", "f.dxf|text|N")));
            Assert.Equal("{}", result["plots"][0]["villaType"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("{\"f.dxf|text|N\":\"X2\"}", result["plots"][1]["villaType"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(new[] { "X2" }, ScanLogic.ValueCodes(" X2 ", Codes(), Settings.TokenSplit));
            Assert.Equal(new[] { "X1", "X3", "X4" }, ScanLogic.ValueCodes("X4 X3-X1 X1", Codes(), Settings.TokenSplit));
            Assert.Empty(ScanLogic.ValueCodes("x2", Codes(), Settings.TokenSplit));
        }

        [Fact]
        public void R425_a_code_that_holds_a_split_character_is_matched_whole_the_longest_first_and_a_run_is_not_used_again()
        {
            Assert.Equal(new[] { "X3-0 (Q)" }, ScanLogic.ValueCodes("type X3-0 (Q) left", Codes(), Settings.TokenSplit));
            Assert.Equal(new[] { "X3" }, ScanLogic.ValueCodes("X3-0", Codes(), Settings.TokenSplit));
            Assert.Equal(new[] { "X1", "X3-0 (Q)" }, ScanLogic.ValueCodes("X3-0 (Q)/X1", Codes(), Settings.TokenSplit));
            Assert.Empty(ScanLogic.ValueCodes("AX3 X3B", Codes(), Settings.TokenSplit));
        }

        [Fact]
        public void R426_a_block_source_of_a_lower_score_leads_over_a_text_source_and_a_share_under_the_strong_share_proposes_none()
        {
            CadDocument document = Plots("1", 0, 0, "2", 20, 0, "3", 40, 0, "4", 60, 0);
            document.Entities.Add(Insert(document, "V", "X1", 2, 2));
            foreach (double x in new[] { 3.0, 23.0, 43.0, 63.0 })
            {
                document.Entities.Add(Text(document, "N", "X2", x, 3));
            }
            JObject cad = Scan(document, "f.dxf");

            JObject weak = Result(new[] { cad });
            Assert.Equal("X1 check 1", Short(RowOf(weak, "villaType", "f.dxf|block|V")));
            Assert.Equal("X2 check 4", Short(RowOf(weak, "villaType", "f.dxf|text|N")));
            Assert.Equal(new[] { "villaType||f.dxf|block|V 0.250 | f.dxf|text|N 1.000" }, Checks(weak, "noStrongSource"));
            JObject conflict = (JObject)weak["conflicts"].Single();
            Assert.Equal("1|villa type|X1|f.dxf|block|V|X2|f.dxf|text|N", string.Join("|", new[] { "plot", "field", "valueA", "sourceA", "valueB", "sourceB" }.Select(key => (string)conflict[key])));
            Assert.Equal(1, (int)RowOf(weak, "villaType", "f.dxf|block|V")["conflicts"]);
            Assert.Equal(0, (int)RowOf(weak, "plotOutline", "f.dxf|A")["conflicts"]);

            JObject strong = Result(new[] { cad }, share: 0.25);
            Assert.Equal("X1 villa type 1", Short(RowOf(strong, "villaType", "f.dxf|block|V")));
            Assert.Empty(Checks(strong, "noStrongSource"));
        }

        [Fact]
        public void R426_a_source_of_profile_codes_only_scores_0_and_gets_no_row_but_gives_its_codes_and_its_conflicts_with_the_lead()
        {
            CadDocument document = Plots("1", 0, 0, "2", 20, 0);
            document.Entities.Add(Insert(document, "V", "X1", 2, 2));
            document.Entities.Add(Insert(document, "V", "X1", 22, 2));
            document.Entities.Add(Text(document, "N", "X3", 3, 3));
            document.Entities.Add(Text(document, "N", "X3", 23, 3));

            JObject result = Result(new[] { Scan(document, "f.dxf") }, prototypes: new[] { "X1" });
            Assert.Equal("X1 villa type 2", Short(RowOf(result, "villaType", "f.dxf|block|V")));
            Assert.DoesNotContain(((JArray)result["rows"]).Cast<JObject>(), row => (string)row["source"] == "f.dxf|text|N");
            Assert.Equal("{\"f.dxf|block|V\":\"X1\",\"f.dxf|text|N\":\"X3\"}", result["plots"][0]["villaType"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(new[] { "1|f.dxf|block|V|f.dxf|text|N", "2|f.dxf|block|V|f.dxf|text|N" }, result["conflicts"].Select(item => (string)item["plot"] + "|" + (string)item["sourceA"] + "|" + (string)item["sourceB"]).ToArray());
            Assert.Equal(2, (int)RowOf(result, "villaType", "f.dxf|block|V")["conflicts"]);
        }

        [Fact]
        public void R421_a_face_of_two_labels_a_label_outside_the_outline_and_what_the_explode_did_not_read_are_check_rows()
        {
            CadDocument document = Plots("1", 0, 0, "2", 20, 0, "3", 40, 0);
            document.Entities.Add(Text(document, "T", "4", 3, 3));
            document.Entities.Add(Text(document, "T", "8", 500, 500));
            // A chain of blocks deeper than the depth limit of the explode
            BlockRecord deepest = null;
            for (int depth = Exploder.DepthLimit + 1; depth >= 0; depth--)
            {
                var block = new BlockRecord("D" + depth);
                document.BlockRecords.Add(block);
                if (deepest != null)
                {
                    block.Entities.Add(new Insert(deepest));
                }
                deepest = block;
            }
            document.Entities.Add(new Insert(deepest) { Layer = Layer(document, "C") });
            var xref = new BlockRecord("OUTSIDE");
            document.BlockRecords.Add(xref);
            xref.Flags = ACadSharp.Blocks.BlockTypeFlags.XRef;
            xref.BlockEntity.XRefPath = ".\\outside.dwg";
            document.Entities.Add(new Insert(xref) { Layer = Layer(document, "C") });

            JObject cad = Scan(document, "f.dxf");
            Assert.Equal(new[] { "f.dxf|xref OUTSIDE external reference", "f.dxf|depth limit 1" }, cad["notRead"].Select(item => (string)item["source"] + " " + (string)item["value"]).ToArray());
            JObject result = Result(new[] { cad });
            Assert.Equal(new[] { "f.dxf|A||1, 4" }, Checks(result, "manyLabels"));
            Assert.Equal(new[] { "f.dxf|T|8|8" }, Checks(result, "labelOutside"));
            Assert.Equal(new[] { "f.dxf|depth limit||1", "f.dxf|xref OUTSIDE||external reference" }, Checks(result, "notRead"));
            Assert.Equal(new[] { "2", "3" }, result["plots"].Select(plot => (string)plot["plot"]).ToArray());
        }

        [Fact]
        public void R426_the_old_and_new_code_of_one_row_is_a_check_not_a_conflict_and_another_code_is_a_conflict()
        {
            CadDocument document = Plots("1", 0, 0, "2", 20, 0, "3", 40, 0);
            foreach (double x in new[] { 2.0, 22.0, 42.0 })
            {
                document.Entities.Add(Insert(document, "V", "X3", x, 2));
            }
            document.Entities.Add(Text(document, "N", "X4", 3, 3));
            document.Entities.Add(Text(document, "N", "X2", 23, 3));

            JObject result = Result(new[] { Scan(document, "f.dxf") });
            Assert.Equal("X3 villa type 3", Short(RowOf(result, "villaType", "f.dxf|block|V")));
            Assert.Equal("X2, X4 check 2", Short(RowOf(result, "villaType", "f.dxf|text|N")));
            Assert.Equal(new[] { "f.dxf|text|N|1|X3, X4" }, Checks(result, "oldNewPair"));
            JObject conflict = (JObject)result["conflicts"].Single();
            Assert.Equal("2|villa type|X3|f.dxf|block|V|X2|f.dxf|text|N", string.Join("|", new[] { "plot", "field", "valueA", "sourceA", "valueB", "sourceB" }.Select(key => (string)conflict[key])));
            Assert.Equal("3 inserts villa blocks 3", Short(RowOf(result, "villaBlock", "f.dxf|V")));
            Assert.Equal(1, (int)RowOf(result, "villaType", "f.dxf|text|N")["conflicts"]);
        }

        // A workbook of one sheet: a title row, then the header row with an empty cell, then the rows, numbers as number cells
        private string Workbook()
        {
            string path = Path.Combine(folder, "book.xlsx");
            using (SpreadsheetDocument book = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart part = book.AddWorkbookPart();
                part.Workbook = new Workbook();
                var sheetPart = part.AddNewPart<WorksheetPart>();
                var data = new SheetData();
                Func<int, object[], Row> row = (index, values) =>
                {
                    var made = new Row { RowIndex = (uint)index };
                    for (int column = 0; column < values.Length; column++)
                    {
                        if (values[column] == null)
                        {
                            continue;
                        }
                        string reference = ScanLogic.ColumnLetter(column) + index;
                        // A double is written with its point, so the cell is read as a float as openpyxl reads it
                        string number = values[column] is double real ? real.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(values[column], System.Globalization.CultureInfo.InvariantCulture);
                        made.Append(values[column] is string text
                            ? new Cell { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(text)) }
                            : new Cell { CellReference = reference, CellValue = new CellValue(number) });
                    }
                    return made;
                };
                data.Append(row(2, new object[] { "name", null, "type" }));
                data.Append(row(3, new object[] { "a", 11, "X1" }));
                data.Append(row(4, new object[] { "b", 12.0, "X2" }));
                data.Append(row(5, new object[] { "c", 2.5, "X1" }));
                sheetPart.Worksheet = new Worksheet(new SheetDimension { Reference = "A1:C5" }, data);
                part.Workbook.AppendChild(new Sheets(new DocumentFormat.OpenXml.Spreadsheet.Sheet { Id = part.GetIdOfPart(sheetPart), SheetId = 1, Name = "S" }));
            }
            return path;
        }

        [Fact]
        public void R426_an_excel_sheet_joins_its_rows_by_the_plot_column_and_shift_that_match_the_most_plots()
        {
            ScanWorkbook book = ScanSources.Workbook(Workbook());
            ScanSheet sheet = book.Sheets.Single();
            Assert.Equal(new[] { "name", "B", "type" }, sheet.Headers);
            Assert.Equal(new[] { "a|11|X1", "b|12|X2", "c|2.5|X1" }, sheet.Rows.Select(cells => string.Join("|", cells)).ToArray());

            JObject result = Result(new[] { Scan(Plots("1", 0, 0, "2", 20, 0), "f.dxf") }, new[] { book });
            Assert.Equal("X1, X2 villa type 2", Short(RowOf(result, "villaType", "book.xlsx|S|type|shift 10")));
            Assert.Equal("{\"book.xlsx|S|type|shift 10\":\"X1\"}", result["plots"][0]["villaType"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.DoesNotContain(((JArray)result["rows"]).Cast<JObject>(), row => ((string)row["source"]).Contains("shift 0"));
        }

        [Fact]
        public void R426_a_tie_of_two_plot_columns_joins_no_row_of_the_sheet()
        {
            ScanSheet sheet = ScanSheet.Of("S", new List<IList<string>> { new[] { "p", "q", "type" }, new[] { "1", "1", "X1" } });
            JObject result = Result(new[] { Scan(Plots("1", 0, 0), "f.dxf") }, new[] { new ScanWorkbook { Name = "b.xlsx", Sheets = new List<ScanSheet> { sheet } } });
            Assert.Equal(new[] { "excelJoin||b.xlsx|S|p|shift 0 or b.xlsx|S|q|shift 0" }, Checks(result, "tie"));
            Assert.DoesNotContain(((JArray)result["rows"]).Cast<JObject>(), row => (string)row["kind"] == "villaType");
        }

        private static PageChar Char(string text, double x0, double x1, double top)
        {
            return new PageChar { Text = text, X0 = x0, X1 = x1, Top = top, Bottom = top + 1, Upright = true };
        }

        [Fact]
        public void R426_pdf_characters_join_into_lines_by_the_gap_and_the_tolerance_of_the_top_as_the_location_plan_and_turned_ones_join_none()
        {
            var chars = new List<PageChar>
            {
                Char("7", 10, 11, 50),
                Char("X", 20, 21, 20.04), Char("1", 21.2, 22, 20),
                Char("1", 10, 11, 20), Char("2", 11.2, 12, 20),
            };
            PageChar turned = Char("9", 30, 31, 20);
            turned.Upright = false;
            chars.Add(turned);
            Assert.Equal(new[] { "12", "X1", "7" }, ScanLogic.PdfLines(chars, 0.3, 0.05).Select(line => line.Text).ToArray());
        }

        [Fact]
        public void R426_a_pdf_code_line_pairs_with_the_number_line_under_it_and_text_on_one_line_or_two_numbers_join_nothing()
        {
            string path = Path.Combine(folder, "plan.pdf");
            var builder = new PdfDocumentBuilder();
            PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
            PdfPageBuilder first = builder.AddPage(400, 300);
            first.AddText("X1", 1, new PdfPoint(50, 200), font);
            first.AddText("11", 1, new PdfPoint(50, 198.5), font);
            PdfPageBuilder second = builder.AddPage(400, 300);
            second.AddText("12 X2", 1, new PdfPoint(50, 200), font);
            second.AddText("X2", 1, new PdfPoint(100, 100), font);
            second.AddText("12 13", 1, new PdfPoint(100, 98.5), font);
            File.WriteAllBytes(path, builder.Build());

            ScanPdf pdf = ScanSources.Pdf(path, Settings.PdfJoinGapPt, Settings.PdfLineTolerancePt);
            Assert.Equal(new[] { "X1", "11" }, pdf.Pages[0].Select(line => line.Text).ToArray());
            JObject result = Result(new[] { Scan(Plots("1", 0, 0, "2", 20, 0), "f.dxf") }, null, new[] { pdf });
            Assert.Equal("X1 villa type 1", Short(RowOf(result, "villaType", "plan.pdf|page 1|shift 10")));
            Assert.DoesNotContain(((JArray)result["rows"]).Cast<JObject>(), row => ((string)row["source"]).Contains("page 2"));
            Assert.Equal("{\"plan.pdf|page 1|shift 10\":\"X1\"}", result["plots"][0]["villaType"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("{}", result["plots"][1]["villaType"].ToString(Newtonsoft.Json.Formatting.None));
        }

        [Fact]
        public void R427_the_scan_result_writes_its_lists_and_fields_in_the_order_and_texts_of_the_design()
        {
            CadDocument document = Plots("2", 20, 0, "1", 0, 0);
            document.Entities.Add(Insert(document, "V", "X1", 2, 2));
            JObject result = Result(new[] { Scan(document, "f.dxf") });
            Assert.Equal(new[] { "inputs", "rows", "conflicts", "checks", "plots" }, result.Properties().Select(item => item.Name).ToArray());
            Assert.Equal(new[] { "kind", "source", "found", "proposedUse", "score", "conflicts" }, ((JObject)result["rows"][0]).Properties().Select(item => item.Name).ToArray());
            Assert.Equal(new[]
            {
                "cadLayer|f.dxf|0|0 closed outlines, 0 texts, 0 blocks||0",
                "cadLayer|f.dxf|A|2 closed outlines, 0 texts, 0 blocks||0",
                "cadLayer|f.dxf|T|0 closed outlines, 2 texts, 0 blocks||0",
                "cadLayer|f.dxf|V|0 closed outlines, 0 texts, 1 blocks||0",
                "plotOutline|f.dxf|A|2 faces|plot outlines|2",
                "plotNumber|f.dxf|T|2 labels in plots|plot numbers|2",
                "villaBlock|f.dxf|V|1 inserts|villa blocks|1",
                "villaType|f.dxf|block|V|X1|villa type|1",
            }, result["rows"].Select(row => string.Join("|", (string)row["kind"], (string)row["source"], (string)row["found"], (string)row["proposedUse"], (string)row["score"])).ToArray());
            Assert.Equal(new[] { "plot", "label", "centroid", "villaType" }, ((JObject)result["plots"][0]).Properties().Select(item => item.Name).ToArray());
            Assert.Equal("1|1|5.000, 5.000", string.Join("|", (string)result["plots"][0]["plot"], (string)result["plots"][0]["label"], (string)result["plots"][0]["centroid"]));
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        [Fact]
        public void R427_the_worker_command_scan_writes_scan_cad_and_scan_json_into_the_output_folder()
        {
            string drawing = Path.Combine(folder, "f.dxf");
            CadDocument document = Plots("1", 0, 0, "2", 20, 0);
            document.Entities.Add(Insert(document, "V", "KX", 2, 2));
            using (var writer = new DxfWriter(drawing, document, false))
            {
                writer.Write();
            }
            string profile = Path.Combine(folder, "profile.json");
            File.WriteAllText(profile, Profile().ToString());
            string prototype = Path.Combine(folder, "P-ARC-KX-S-1.rvt");
            File.WriteAllText(prototype, "a dummy prototype, read by its name only");
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, new JObject
            {
                ["profile"] = profile,
                ["prototypeNames"] = new JObject { ["prefix"] = "P-", ["discipline"] = "ARC", ["suffix"] = "-S-" },
                ["siteBuild"] = new JObject { ["minFreeGB"] = 2.0 },
                ["majority"] = new JObject { ["strongShare"] = 0.9 },
                ["addin"] = new JObject { ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "Road.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) }, ["workerTimeLimitS"] = new JObject { ["scan"] = 60 }, ["workerTimeLimitMinS"] = 10 },
            }.ToString());
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "scan1", Command = "scan", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { Input("config", config), Input("scanCad:1", drawing), Input(BuildSteps.PrototypeRole + Path.GetFileName(prototype), prototype) },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = Path.Combine(folder, "out"), TimeLimitS = 60,
            };
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "scan1"));
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            Assert.Equal(new[] { "scan_cad_1.json", "scan.json" }, answer.Outputs.Select(output => Path.GetFileName(output.Path)).ToArray());
            JObject cad = JObject.Parse(File.ReadAllText(Path.Combine(folder, "out", "scan_cad_1.json")));
            Assert.Equal("f.dxf", (string)cad["source"]);
            Assert.Equal(FileHash.Sha256(drawing), (string)cad["sha256"]);
            JObject scan = JObject.Parse(File.ReadAllText(Path.Combine(folder, "out", "scan.json")));
            Assert.Equal(new[] { "config", "scanCad:1", "prototype:P-ARC-KX-S-1.rvt" }, scan["inputs"].Select(input => (string)input["role"]).ToArray());
            // The code KX is known only by the prototype file name
            Assert.Equal("KX villa type 1", Short(RowOf(scan, "villaType", "f.dxf|block|V")));
            Assert.Equal("scan", WorkerRunner.StepCommand[WorkerRunner.ScanStep]);
        }

        [Fact]
        public void R420_rename_layers_writes_a_dxf_whose_layers_carry_the_names_of_the_map()
        {
            string input = Path.Combine(folder, "in.dxf");
            CadDocument document = Plots("1", 0, 0);
            using (var writer = new DxfWriter(input, document, false))
            {
                writer.Write();
            }
            string map = Path.Combine(folder, "map.json");
            File.WriteAllText(map, "{\"A\": \"T\", \"T\": \"Z\"}");
            string output = Path.Combine(folder, "out.dxf");
            string before = FileHash.Sha256(input);
            var info = new StringWriter();
            var error = new StringWriter();
            Assert.True(LayerRename.Run(input, output, map, info, error) == 0, error.ToString());
            Assert.Equal(before, FileHash.Sha256(input));
            CadDocument renamed = CadExtractor.Open(output, new List<string>());
            JObject cad = Scan(renamed, "out.dxf");
            var layers = ((JArray)cad["layers"]).ToDictionary(row => (string)row["layer"], row => (int)row["segments"] + " " + (int)row["texts"]);
            Assert.Equal("4 0", layers["T"]);
            Assert.Equal("0 1", layers["Z"]);
            Assert.False(layers.ContainsKey("A"));
            Assert.Equal(1, LayerRename.Run(input, input, map, info, new StringWriter()));
            File.WriteAllText(map, "{\"Q\": \"R\"}");
            Assert.Equal(4, LayerRename.Run(input, Path.Combine(folder, "other.dxf"), map, info, new StringWriter()));
        }
    }
}
