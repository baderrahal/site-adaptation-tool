using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CadReader;
using CSMath;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    public class CadExplodeTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sat-" + Guid.NewGuid().ToString("N"));

        public CadExplodeTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static Layer Layer(CadDocument document, string name)
        {
            Layer layer;
            if (!document.Layers.TryGetValue(name, out layer))
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

        private static BlockRecord Block(CadDocument document, string name, params Entity[] children)
        {
            var block = new BlockRecord(name);
            document.BlockRecords.Add(block);
            foreach (Entity child in children)
            {
                block.Entities.Add(child);
            }
            return block;
        }

        private static CadExtract Run(CadDocument document, LayerConfig config = null, string source = null)
        {
            return CadExtractor.Extract(document, "dummy", config, null, source, new List<string>());
        }

        private static Piece[] OfKind(CadExtract extract, string kind)
        {
            return extract.Pieces.Pieces.Where(piece => piece.Kind == kind).ToArray();
        }

        [Fact]
        public void R17_nested_inserts_are_exploded_with_every_transform_applied()
        {
            var document = new CadDocument();
            var inner = Block(document, "INNER");
            inner.Entities.Add(Line(document, "A", 0, 0, 1, 0));
            var outer = Block(document, "OUTER");
            outer.Entities.Add(new Insert(inner) { InsertPoint = new XYZ(10, 0, 0), XScale = 2, YScale = 2, ZScale = 2, Layer = Layer(document, "A") });
            document.Entities.Add(new Insert(outer) { InsertPoint = new XYZ(100, 100, 0), Rotation = Math.PI / 2, Layer = Layer(document, "A") });

            var line = OfKind(Run(document), "line").Single();
            Assert.Equal(100, line.Pts[0][0], 6);
            Assert.Equal(110, line.Pts[0][1], 6);
            Assert.Equal(100, line.Pts[1][0], 6);
            Assert.Equal(112, line.Pts[1][1], 6);
            Assert.Equal(2, line.Path.Split('/').Length);
            Assert.False(string.IsNullOrEmpty(line.Handle));
        }

        [Fact]
        public void R17_layer_zero_inside_a_block_takes_the_layer_of_its_insert()
        {
            var document = new CadDocument();
            var block = Block(document, "B", Line(document, "0", 0, 0, 5, 0));
            document.Entities.Add(new Insert(block) { Layer = Layer(document, "ROADS") });

            var extract = Run(document);
            Assert.Equal("ROADS", OfKind(extract, "line").Single().Layer);
            var roads = extract.Layers.Layers.Single(layer => layer.Name == "ROADS");
            Assert.Equal(1, roads.Entities);
            Assert.Equal(1, roads.After);
            Assert.Equal(1, roads.AfterByType["LINE"]);
        }

        [Fact]
        public void R17_layers_file_counts_entities_before_and_after_exploding()
        {
            var document = new CadDocument();
            var block = Block(document, "B", Line(document, "A", 0, 0, 1, 0), Line(document, "A", 0, 0, 2, 0), Line(document, "B", 0, 0, 3, 0));
            document.Entities.Add(new Insert(block) { Layer = Layer(document, "A") });
            document.Entities.Add(Line(document, "A", 5, 5, 6, 6));

            var totals = Run(document).Layers.Totals;
            Assert.Equal(2, totals.Before);
            Assert.Equal(4, totals.After);
            Assert.Equal(1, totals.Inserts);
            Assert.Equal(1, totals.MaxDepth);
        }

        [Fact]
        public void R17_mirrored_insert_flips_arc_direction_and_polyline_bulge()
        {
            var document = new CadDocument();
            var arc = new Arc { Center = new XYZ(0, 0, 0), Radius = 1, StartAngle = 0, EndAngle = Math.PI / 2, Layer = Layer(document, "A") };
            var poly = new LwPolyline(new[] { new LwPolyline.Vertex(new XY(0, 0)) { Bulge = 0.5 }, new LwPolyline.Vertex(new XY(2, 0)) }) { Layer = Layer(document, "A") };
            var block = Block(document, "B", arc, poly);
            document.Entities.Add(new Insert(block) { XScale = -1, Layer = Layer(document, "A") });

            var extract = Run(document);
            var world = OfKind(extract, "arc").Single();
            Assert.Equal(-1, world.Start[0], 6);
            Assert.Equal(0, world.End[0], 6);
            Assert.Equal(1, world.End[1], 6);
            Assert.False(world.Ccw.Value);
            Assert.Equal(-0.5, OfKind(extract, "polyline").Single().Pts[0][2], 6);
        }

        [Fact]
        public void R17_arc_drawn_on_a_flipped_plane_is_read_in_world_coordinates()
        {
            var document = new CadDocument();
            document.Entities.Add(new Arc { Center = new XYZ(3, 0, 0), Radius = 1, StartAngle = 0, EndAngle = Math.PI / 2, Normal = new XYZ(0, 0, -1), Layer = Layer(document, "A") });

            var world = OfKind(Run(document), "arc").Single();
            Assert.Equal(-3, world.Center[0], 6);
            Assert.Equal(-4, world.Start[0], 6);
            Assert.False(world.Ccw.Value);
        }

        [Fact]
        public void R17_blocks_nested_deeper_than_the_limit_stop_and_are_counted()
        {
            var document = new CadDocument();
            BlockRecord below = Block(document, "LEVEL0");
            below.Entities.Add(Line(document, "A", 0, 0, 1, 1));
            for (int level = 1; level <= Exploder.DepthLimit + 3; level++)
            {
                var block = Block(document, "LEVEL" + level);
                block.Entities.Add(new Insert(below));
                below = block;
            }
            document.Entities.Add(new Insert(below));

            var extract = Run(document);
            Assert.Equal(1, extract.Layers.Totals.DepthLimitHits);
            Assert.Equal(Exploder.DepthLimit, extract.Layers.Totals.MaxDepth);
            Assert.Empty(OfKind(extract, "line"));
        }

        [Fact]
        public void R17_insert_attributes_and_extended_data_are_read()
        {
            var document = new CadDocument();
            var block = Block(document, "TAG");
            var insert = new Insert(block) { InsertPoint = new XYZ(4, 5, 0), Layer = Layer(document, "A") };
            insert.Attributes.Add(new AttributeEntity { Tag = "PLOT", Value = "1204", InsertPoint = new XYZ(4, 5, 0) });
            document.Entities.Add(insert);
            var line = Line(document, "A", 0, 0, 1, 1);
            var data = new ACadSharp.XData.ExtendedData();
            data.Records.Add(new ACadSharp.XData.ExtendedDataString("hello"));
            line.ExtendedData.Add(new AppId("TESTAPP"), data);
            document.Entities.Add(line);

            var extract = Run(document);
            var read = OfKind(extract, "insert").Single();
            Assert.Equal("1204", read.Attrs["PLOT"]);
            Assert.Equal("TAG", read.Name);
            var attribute = extract.Texts.Texts.Single(text => text.Kind == "attrib");
            Assert.Equal("1204", attribute.Text);
            Assert.Equal("PLOT", attribute.Tag);
            var entry = OfKind(extract, "line").Single().Xdata.Single();
            Assert.Equal("TESTAPP", entry.App);
            Assert.Equal("hello", entry.Values.Single());
        }

        [Fact]
        public void R17_hatch_boundary_loops_are_read_in_world_coordinates()
        {
            var document = new CadDocument();
            var hatch = new Hatch { Layer = Layer(document, "A") };
            var path = new Hatch.BoundaryPath();
            path.Edges.Add(new Hatch.BoundaryPath.Line { Start = new XY(0, 0), End = new XY(4, 0) });
            path.Edges.Add(new Hatch.BoundaryPath.Line { Start = new XY(4, 0), End = new XY(4, 3) });
            hatch.Paths.Add(path);
            var block = Block(document, "H", hatch);
            document.Entities.Add(new Insert(block) { InsertPoint = new XYZ(10, 20, 0), Layer = Layer(document, "A") });

            var read = OfKind(Run(document), "hatch").Single();
            Assert.Equal(1, read.Loops.Count);
            Assert.Equal(10, read.Loops[0][0][0], 6);
            Assert.Equal(20, read.Loops[0][0][1], 6);
            Assert.Equal(14, read.Loops[0][3][0], 6);
        }

        [Fact]
        public void R17_extents_cover_the_kept_pieces()
        {
            var document = new CadDocument();
            document.Entities.Add(Line(document, "A", -2, 1, 7, 9));

            var extents = Run(document).Layers.Extents;
            Assert.Equal(new[] { -2.0, 1.0, 7.0, 9.0 }, extents.Kept);
            Assert.Equal(1, extents.Points);
        }

        [Fact]
        public void R17_pieces_are_streamed_one_per_line_into_valid_json()
        {
            string path = Path.Combine(folder, "pieces.json");
            var document = new CadDocument();
            document.Entities.Add(Line(document, "A", 0, 0, 1.23456, 1));
            document.Entities.Add(Line(document, "A", 0, 0, 2, 2));
            using (var writer = new JsonOut.ArrayWriter(path, "dummy.dwg", "pieces"))
            {
                CadExtractor.Extract(document, "dummy.dwg", null, writer.Add, null, null);
                Assert.Equal(2, writer.Count);
            }
            var json = JObject.Parse(File.ReadAllText(path));
            Assert.Equal("dummy.dwg", (string)json["source"]);
            Assert.Equal(2, json["pieces"].Count());
            Assert.Contains("1.235", File.ReadAllText(path));
        }

        [Fact]
        public void R18_drop_wins_over_keep_and_unlisted_layers_are_kept_and_flagged()
        {
            var config = new LayerConfig { KeepLayers = new List<string> { "T*", "road" }, DropLayers = new List<string> { "title" } };
            Assert.Equal("drop", config.Status("TITLE"));
            Assert.Equal("keep", config.Status("Tree"));
            Assert.Equal("keep", config.Status("ROAD"));
            Assert.Equal("unlisted", config.Status("PLOTS"));
        }

        [Fact]
        public void R18_dropped_layers_are_counted_and_not_written()
        {
            var document = new CadDocument();
            document.Entities.Add(Line(document, "TITLE_BLOCK", 0, 0, 1, 1));
            document.Entities.Add(Line(document, "ROAD", 0, 0, 2, 2));
            document.Entities.Add(Line(document, "MISC", 0, 0, 3, 3));
            var config = new LayerConfig { KeepLayers = new List<string> { "ROAD" }, DropLayers = new List<string> { "TITLE*" } };

            var extract = Run(document, config);
            Assert.Equal(new[] { "MISC", "ROAD" }, extract.Pieces.Pieces.Select(piece => piece.Layer).OrderBy(name => name).ToArray());
            var layers = extract.Layers.Layers.ToDictionary(layer => layer.Name);
            Assert.Equal("drop", layers["TITLE_BLOCK"].Status);
            Assert.Equal(1, layers["TITLE_BLOCK"].After);
            Assert.Equal("keep", layers["ROAD"].Status);
            Assert.Equal("unlisted", layers["MISC"].Status);
            Assert.Equal(1, extract.Layers.Totals.Dropped);
        }

        [Fact]
        public void R18_children_of_an_insert_on_a_dropped_layer_are_dropped_with_it()
        {
            var document = new CadDocument();
            var block = Block(document, "LOGO", Line(document, "0", 0, 0, 1, 1), Line(document, "ART", 0, 0, 2, 2));
            document.Entities.Add(new Insert(block) { Layer = Layer(document, "TITLE") });
            var config = new LayerConfig { DropLayers = new List<string> { "TITLE" } };

            var extract = Run(document, config);
            Assert.Empty(extract.Pieces.Pieces);
            Assert.Equal(2, extract.Layers.Totals.After);
        }

        [Fact]
        public void R18_config_file_is_read_with_keep_drop_and_plot_layers()
        {
            string path = Path.Combine(folder, "layers.json");
            File.WriteAllText(path, "{\"plotLineLayers\": [\"P1\", \"P2\"], \"plotLabelLayers\": [\"T1\"], \"keep\": [\"K*\"], \"drop\": [\"D?\"]}");

            var config = LayerConfig.Load(path);
            Assert.True(config.IsPlotLine("p2"));
            Assert.False(config.IsPlotLine("P3"));
            Assert.True(config.IsPlotLabel("t1"));
            Assert.Equal("keep", config.Status("KEEP_ME"));
            Assert.Equal("drop", config.Status("D1"));
            Assert.Equal("P1+P2", config.PlotLineName);
        }

        [Fact]
        public void R19_text_in_blocks_and_multileader_text_are_read()
        {
            var document = new CadDocument();
            var note = new MText { Value = "{\\fArial;DT3}", InsertPoint = new XYZ(1, 2, 0), Layer = Layer(document, "A") };
            var block = Block(document, "N", note);
            document.Entities.Add(new Insert(block) { InsertPoint = new XYZ(10, 10, 0), Layer = Layer(document, "A") });
            var leader = new MultiLeader { Layer = Layer(document, "A") };
            leader.ContextData.TextLabel = "N 120";
            leader.ContextData.TextLocation = new XYZ(7, 8, 0);
            document.Entities.Add(leader);

            var texts = Run(document).Texts.Texts;
            var inBlock = texts.Single(text => text.Kind == "mtext");
            Assert.Equal("DT3", inBlock.Text);
            Assert.Equal(11, inBlock.X, 6);
            Assert.Equal(12, inBlock.Y, 6);
            Assert.NotNull(inBlock.Path);
            var fromLeader = texts.Single(text => text.Kind == "mleader");
            Assert.Equal("N 120", fromLeader.Text);
            Assert.Equal(7, fromLeader.X, 6);
        }

        [Fact]
        public void R19_xref_is_recorded_with_name_path_and_found_and_the_run_carries_on()
        {
            var document = new CadDocument();
            var missing = Block(document, "GONE");
            missing.Flags = BlockTypeFlags.XRef;
            missing.BlockEntity.XRefPath = ".\\gone.dwg";
            var present = Block(document, "HERE");
            present.Flags = BlockTypeFlags.XRef;
            present.BlockEntity.XRefPath = ".\\here.dwg";
            document.Entities.Add(new Insert(missing));
            document.Entities.Add(new Insert(present));
            document.Entities.Add(Line(document, "A", 0, 0, 1, 1));
            string source = Path.Combine(folder, "plan.dwg");
            File.WriteAllText(Path.Combine(folder, "here.dwg"), "x");

            var xrefs = Run(document, null, source).Layers.Xrefs.ToDictionary(xref => xref.Name);
            Assert.False(xrefs["GONE"].Found);
            Assert.Equal(".\\gone.dwg", xrefs["GONE"].Path);
            Assert.Equal(1, xrefs["GONE"].Inserts);
            Assert.True(xrefs["HERE"].Found);
        }

        [Fact]
        public void R19_reader_notes_are_grouped_with_counts()
        {
            var document = new CadDocument();
            var problems = new List<string> { "Error: entry A already exists", "Error: entry A already exists", "NotImplemented: Proxy object" };

            var notes = CadExtractor.Extract(document, "dummy", null, null, null, problems).Layers.Notes;
            Assert.Equal(2, notes.Count);
            Assert.Equal(2, notes[0].Count);
            Assert.Equal("Error: entry A already exists", notes[0].Text);
        }

        private static MText Label(CadDocument document, string layer, string value, double x, double y)
        {
            return new MText { Value = value, InsertPoint = new XYZ(x, y, 0), Layer = Layer(document, layer) };
        }

        private static void Square(CadDocument document, string layer, double x, double y, double size)
        {
            document.Entities.Add(Line(document, layer, x, y, x + size, y));
            document.Entities.Add(Line(document, layer, x + size, y, x + size, y + size));
            document.Entities.Add(Line(document, layer, x + size, y + size, x, y + size));
            document.Entities.Add(Line(document, layer, x, y + size, x, y));
        }

        [Fact]
        public void R20_face_with_one_plot_label_becomes_a_plot_with_number_type_and_centre()
        {
            var document = new CadDocument();
            Square(document, "MP_PLOT_LINE", 0, 0, 10);
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "DT3\\P1001", 2, 3));

            var extract = Run(document);
            var plot = extract.Plots.Plots.Single(entry => entry.Kind == "plot_shape");
            Assert.Equal("1001", plot.Plot);
            Assert.Equal("DT3", plot.Type);
            Assert.Equal(5, plot.Center[0], 6);
            Assert.Equal(5, plot.Center[1], 6);
            Assert.Empty(extract.OtherFaces.Faces);
            Assert.Equal(1, extract.Plots.Summary.Plots);
        }

        [Fact]
        public void R20_face_with_no_label_or_many_labels_goes_to_other_faces_with_the_reason()
        {
            var document = new CadDocument();
            Square(document, "MP_PLOT_LINE", 0, 0, 10);
            Square(document, "MP_PLOT_LINE", 20, 0, 10);
            Square(document, "MP_PLOT_LINE", 40, 0, 10);
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "DT3\\P1001", 22, 3));
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "DT3\\P1002", 42, 3));
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "VD2\\P1003", 48, 7));

            var extract = Run(document);
            Assert.Equal("1001", extract.Plots.Plots.Single(entry => entry.Kind == "plot_shape").Plot);
            var faces = extract.OtherFaces.Faces.ToDictionary(face => face.Reason);
            Assert.Equal(2, faces.Count);
            Assert.Equal(new[] { "1002", "1003" }, faces["many_labels"].Labels.OrderBy(number => number).ToArray());
            Assert.Equal("other_face", faces["no_label"].Kind);
            Assert.Equal(1, extract.Plots.Summary.NoLabel);
            Assert.Equal(1, extract.Plots.Summary.ManyLabels);
        }

        [Fact]
        public void R20_label_outside_every_face_and_text_without_a_number_are_counted_not_joined()
        {
            var document = new CadDocument();
            Square(document, "MP_PLOT_LINE", 0, 0, 10);
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "1001", 5, 5));
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "1002", 50, 50));
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "Local Mosque", 6, 6));
            document.Entities.Add(Label(document, "OTHER_TEXT", "1003", 7, 7));

            var summary = Run(document).Plots.Summary;
            Assert.Equal(1, summary.Plots);
            Assert.Equal(2, summary.Labels);
            Assert.Equal(1, summary.LabelsWithoutNumber);
            Assert.Equal(1, summary.LabelsOutsideShapes);
        }

        [Fact]
        public void R20_subdivision_layer_lines_split_a_block_into_plots_and_duplicate_numbers_are_listed()
        {
            var document = new CadDocument();
            Square(document, "MP_PLOT_LINE", 0, 0, 10);
            document.Entities.Add(Line(document, "MP_PLOT_SUBD", 5, 0, 5, 10));
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "1001", 2, 5));
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "1001", 8, 5));
            var config = new LayerConfig { PlotLineLayers = new List<string> { "MP_PLOT_LINE", "MP_PLOT_SUBD" } };

            var summary = Run(document, config).Plots.Summary;
            Assert.Equal(2, summary.Plots);
            Assert.Equal(new[] { "1001" }, summary.DuplicateNumbers.ToArray());
        }

        [Fact]
        public void R20_face_bounded_by_an_arc_holds_its_label()
        {
            var document = new CadDocument();
            document.Entities.Add(Line(document, "MP_PLOT_LINE", -5, 0, 5, 0));
            document.Entities.Add(new Arc { Center = new XYZ(0, 0, 0), Radius = 5, StartAngle = 0, EndAngle = Math.PI, Layer = Layer(document, "MP_PLOT_LINE") });
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "1004", 0, 4));
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "1005", 0, -2));

            var extract = Run(document);
            Assert.Equal("1004", extract.Plots.Plots.Single(entry => entry.Kind == "plot_shape").Plot);
            Assert.Equal(1, extract.Plots.Summary.LabelsOutsideShapes);
        }

        [Fact]
        public void R20_plots_and_other_faces_are_written_to_separate_files()
        {
            var document = new CadDocument();
            Square(document, "MP_PLOT_LINE", 0, 0, 10);
            Square(document, "MP_PLOT_LINE", 20, 0, 10);
            document.Entities.Add(Label(document, "MP_PLOT_TEXT", "1001", 5, 5));
            string outDir = Path.Combine(folder, "out");

            CadExtractor.WriteFiles(Run(document), outDir);
            var plots = JObject.Parse(File.ReadAllText(Path.Combine(outDir, "plots.json")));
            var other = JObject.Parse(File.ReadAllText(Path.Combine(outDir, "other_faces.json")));
            Assert.Equal(1, plots["plots"].Count(entry => (string)entry["kind"] == "plot_shape"));
            Assert.Equal("no_label", (string)other["faces"][0]["reason"]);
        }
    }
}
