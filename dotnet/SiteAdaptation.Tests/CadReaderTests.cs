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
using Xunit;

namespace SiteAdaptation.Tests
{
    public class CadReaderTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sat-" + Guid.NewGuid().ToString("N"));

        public CadReaderTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static CadDocument BuildDummy()
        {
            var document = new CadDocument();
            document.Layers.Add(new Layer("PLOTS"));
            document.Layers.Add(new Layer("TEXT"));
            document.Layers.Add(new Layer("EMPTY"));

            var outline = new LwPolyline(new[]
            {
                new LwPolyline.Vertex(new XY(0, 0)),
                new LwPolyline.Vertex(new XY(10.12345, 0)),
                new LwPolyline.Vertex(new XY(10.12345, 5)),
            });
            outline.IsClosed = true;
            outline.Layer = document.Layers["PLOTS"];
            document.Entities.Add(outline);

            var block = new BlockRecord("PLOT_TAG");
            document.BlockRecords.Add(block);
            var insert = new Insert(block) { InsertPoint = new XYZ(100.5, 200.25, 0), Rotation = Math.PI / 2 };
            insert.Layer = document.Layers["PLOTS"];
            document.Entities.Add(insert);

            var label = new TextEntity { Value = "PLOT 12", InsertPoint = new XYZ(3, 4, 0) };
            label.Layer = document.Layers["TEXT"];
            document.Entities.Add(label);

            var note = new MText { Value = "{\\fArial|b1;\\LLOT 17\\l\\P0.5 AC.}", InsertPoint = new XYZ(7, 8, 0) };
            note.Layer = document.Layers["TEXT"];
            document.Entities.Add(note);
            return document;
        }

        private string WriteDummy(string extension)
        {
            string path = Path.Combine(folder, "dummy" + extension);
            if (extension == ".dxf")
            {
                using (var writer = new DxfWriter(path, BuildDummy(), false)) { writer.Write(); }
            }
            else
            {
                using (var writer = new DwgWriter(path, BuildDummy())) { writer.Write(); }
            }
            return path;
        }

        private CadExtract RoundTrip(string extension)
        {
            string path = WriteDummy(extension);
            return CadExtractor.Extract(CadExtractor.Open(path, new List<string>()), Path.GetFileName(path));
        }

        [Theory]
        [InlineData(".dxf")]
        [InlineData(".dwg")]
        public void R5_layers_list_every_layer_with_entity_counts(string extension)
        {
            var layers = RoundTrip(extension).Layers.Layers.ToDictionary(layer => layer.Name);
            Assert.Equal(2, layers["PLOTS"].Entities);
            Assert.Equal(2, layers["TEXT"].Entities);
            Assert.Equal(0, layers["EMPTY"].Entities);
        }

        [Fact]
        public void R189_layers_file_lists_the_dwg_classes_with_their_instance_count()
        {
            var document = BuildDummy();
            document.Classes.AddOrUpdate(new ACadSharp.Classes.DxfClass
            {
                DxfName = "AECC_TIN_SURFACE",
                CppClassName = "AeccDbSurfaceTin",
                ApplicationName = "AeccDbSurface",
                InstanceCount = 3,
                IsAnEntity = true,
            });
            var classes = CadExtractor.Extract(document, "dummy.dwg").Layers.Classes;
            var surface = Assert.Single(classes, entry => entry.DxfName == "AECC_TIN_SURFACE");
            Assert.Equal("AeccDbSurfaceTin", surface.CppClassName);
            Assert.Equal(3, surface.InstanceCount);
            Assert.True(surface.IsAnEntity);
            Assert.Equal(classes.Select(entry => entry.DxfName).OrderBy(name => name, StringComparer.Ordinal), classes.Select(entry => entry.DxfName));
        }

        [Fact]
        public void R196_layers_file_carries_the_created_and_saved_time_of_the_header()
        {
            var document = BuildDummy();
            document.Header.CreateDateTime = new DateTime(2024, 3, 25, 9, 30, 0);
            document.Header.UpdateDateTime = new DateTime(2024, 6, 25, 17, 25, 0);
            var layers = CadExtractor.Extract(document, "dummy.dwg").Layers;
            Assert.Equal("2024-03-25 09:30", layers.Created);
            Assert.Equal("2024-06-25 17:25", layers.Updated);
        }

        [Theory]
        [InlineData(".dxf")]
        [InlineData(".dwg")]
        public void R5_plots_hold_polylines_and_blocks_with_layer_and_coordinates(string extension)
        {
            var plots = RoundTrip(extension).Plots.Plots;
            var polyline = plots.Single(plot => plot.Kind == "polyline");
            Assert.Equal("PLOTS", polyline.Layer);
            Assert.True(polyline.Closed);
            Assert.Equal(3, polyline.Vertices.Count);
            Assert.Equal(10.12345, polyline.Vertices[1][0], 5);

            var block = plots.Single(plot => plot.Kind == "block");
            Assert.Equal("PLOT_TAG", block.Block);
            Assert.Equal(100.5, block.InsertPoint[0]);
            Assert.Equal(90, block.RotationDeg.Value, 3);
        }

        [Theory]
        [InlineData(".dxf")]
        [InlineData(".dwg")]
        public void R5_texts_hold_text_and_mtext_with_position(string extension)
        {
            var texts = RoundTrip(extension).Texts.Texts;
            var text = texts.Single(entry => entry.Kind == "text");
            Assert.Equal("PLOT 12", text.Text);
            Assert.Equal(3, text.X);
            var mtext = texts.Single(entry => entry.Kind == "mtext");
            Assert.Equal("LOT 17\n0.5 AC.", mtext.Text);
            Assert.Equal(8, mtext.Y);
        }

        [Fact]
        public void R2_written_files_have_at_most_three_decimals()
        {
            string outDir = Path.Combine(folder, "out");
            CadExtractor.WriteFiles(RoundTrip(".dxf"), outDir);
            string plots = File.ReadAllText(Path.Combine(outDir, "plots.json"));
            Assert.Contains("10.123", plots);
            Assert.DoesNotContain("10.1234", plots);
            Assert.True(File.Exists(Path.Combine(outDir, "layers.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "texts.json")));
        }

        [Fact]
        public void R1_input_file_is_not_modified()
        {
            string path = WriteDummy(".dxf");
            byte[] before = File.ReadAllBytes(path);
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            CadExtractor.Open(path, new List<string>());
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        }

        [Fact]
        public void Unsupported_extension_is_refused()
        {
            string path = Path.Combine(folder, "plan.txt");
            File.WriteAllText(path, "x");
            Assert.Throws<ArgumentException>(() => CadExtractor.Open(path, new List<string>()));
        }

        private CadExtract RoundTripPlotLines(string layerName, Action<CadDocument, Layer> fill)
        {
            var document = new CadDocument();
            var layer = new Layer(layerName);
            document.Layers.Add(layer);
            fill(document, layer);
            string path = Path.Combine(folder, "plotlines.dxf");
            using (var writer = new DxfWriter(path, document, false)) { writer.Write(); }
            return CadExtractor.Extract(CadExtractor.Open(path, new List<string>()), "plotlines.dxf");
        }

        private static void AddLine(CadDocument document, Layer layer, double x1, double y1, double x2, double y2)
        {
            var line = new Line { StartPoint = new XYZ(x1, y1, 0), EndPoint = new XYZ(x2, y2, 0) };
            line.Layer = layer;
            document.Entities.Add(line);
        }

        private static List<PlotEntry> Shapes(CadExtract extract)
        {
            return extract.Plots.Plots.Where(plot => plot.Kind == "plot_shape").Concat(extract.OtherFaces.Faces).ToList();
        }

        private static List<PlotEntry> OpenPieces(CadExtract extract)
        {
            return extract.Plots.Plots.Where(plot => plot.Kind == "plot_open").ToList();
        }

        [Fact]
        public void R5_plot_lines_that_meet_end_to_end_form_one_closed_shape()
        {
            var extract = RoundTripPlotLines("MP_PLOT_LINE", (document, layer) =>
            {
                AddLine(document, layer, 0, 0, 10, 0);
                AddLine(document, layer, 10, 0, 10, 5);
                AddLine(document, layer, 10, 5, 0, 5);
                AddLine(document, layer, 0, 5, 0, 0);
            });
            var shape = Shapes(extract).Single();
            Assert.True(shape.Closed);
            Assert.Equal(4, shape.Vertices.Count);
            Assert.Equal(50, shape.Area.Value, 3);
            Assert.Equal("MP_PLOT_LINE", shape.Layer);
            Assert.Empty(OpenPieces(extract));
        }

        [Fact]
        public void R5_plot_line_ending_on_the_middle_of_another_splits_the_outline_into_two_shapes()
        {
            var extract = RoundTripPlotLines("MP_PLOT_LINE", (document, layer) =>
            {
                AddLine(document, layer, 0, 0, 20, 0);
                AddLine(document, layer, 20, 0, 20, 10);
                AddLine(document, layer, 20, 10, 0, 10);
                AddLine(document, layer, 0, 10, 0, 0);
                AddLine(document, layer, 10, 0, 10, 10);
            });
            var shapes = Shapes(extract);
            Assert.Equal(2, shapes.Count);
            Assert.All(shapes, shape => Assert.Equal(100, shape.Area.Value, 3));
            Assert.All(shapes, shape => Assert.Equal(4, shape.Vertices.Count));
        }

        [Fact]
        public void R5_plot_lines_that_cross_without_sharing_an_end_still_close_a_shape()
        {
            var extract = RoundTripPlotLines("MP_PLOT_LINE", (document, layer) =>
            {
                AddLine(document, layer, -5, 3, 15, 3);
                AddLine(document, layer, -5, 6, 15, 6);
                AddLine(document, layer, 2, -5, 2, 15);
                AddLine(document, layer, 8, -5, 8, 15);
            });
            var shape = Shapes(extract).Single();
            Assert.Equal(18, shape.Area.Value, 3);
            Assert.Equal(4, OpenPieces(extract).Count);
        }

        [Fact]
        public void R5_plot_line_arc_closes_a_shape_and_is_listed_with_its_centre_and_radius()
        {
            var extract = RoundTripPlotLines("MP_PLOT_LINE", (document, layer) =>
            {
                AddLine(document, layer, -5, 0, 5, 0);
                var arc = new Arc { Center = new XYZ(0, 0, 0), Radius = 5, StartAngle = 0, EndAngle = Math.PI };
                arc.Layer = layer;
                document.Entities.Add(arc);
            });
            var shape = Shapes(extract).Single();
            Assert.Equal(Math.PI * 25 / 2, shape.Area.Value, 2);
            Assert.Equal(2, shape.Vertices.Count);
            var arcEntry = shape.Arcs.Single();
            Assert.Equal(5, arcEntry.Radius, 3);
            Assert.Equal(0, arcEntry.Center[0], 3);
            Assert.Equal(0, arcEntry.Center[1], 3);
        }

        [Fact]
        public void R5_plot_line_that_bounds_nothing_is_kept_as_an_open_piece()
        {
            var extract = RoundTripPlotLines("MP_PLOT_LINE", (document, layer) =>
            {
                AddLine(document, layer, 0, 0, 10, 0);
                AddLine(document, layer, 10, 0, 10, 5);
                AddLine(document, layer, 10, 5, 0, 5);
                AddLine(document, layer, 0, 5, 0, 0);
                AddLine(document, layer, 20, 0, 30, 0);
            });
            Assert.Single(Shapes(extract));
            var open = OpenPieces(extract).Single();
            Assert.False(open.Closed.Value);
            Assert.Equal(new[] { 20.0, 0.0, 30.0, 0.0 }, open.Segments.Single());
        }

        [Fact]
        public void R5_lines_on_other_layers_are_not_plot_shapes()
        {
            var extract = RoundTripPlotLines("OTHER", (document, layer) =>
            {
                AddLine(document, layer, 0, 0, 10, 0);
                AddLine(document, layer, 10, 0, 10, 5);
                AddLine(document, layer, 10, 5, 0, 5);
                AddLine(document, layer, 0, 5, 0, 0);
            });
            Assert.Empty(extract.Plots.Plots);
        }

        [Fact]
        public void MText_cleaner_strips_underline_toggles_fonts_and_braces()
        {
            Assert.Equal("LOT 17\n0.552 AC.\n24074 S.F.", MTextCleaner.Plain("{\\fArial|b1|i0|c0|p34;\\LLOT 17\\fArial|b0|i0|c0|p34;\\l\\P0.552 AC.\\P24074 S.F.}"));
            Assert.Equal("1/2 in", MTextCleaner.Plain("\\S1/2; in"));
            Assert.Equal("a{b}c", MTextCleaner.Plain("a\\{b\\}c"));
        }
    }
}
