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

        [Fact]
        public void MText_cleaner_strips_underline_toggles_fonts_and_braces()
        {
            Assert.Equal("LOT 17\n0.552 AC.\n24074 S.F.", MTextCleaner.Plain("{\\fArial|b1|i0|c0|p34;\\LLOT 17\\fArial|b0|i0|c0|p34;\\l\\P0.552 AC.\\P24074 S.F.}"));
            Assert.Equal("1/2 in", MTextCleaner.Plain("\\S1/2; in"));
            Assert.Equal("a{b}c", MTextCleaner.Plain("a\\{b\\}c"));
        }
    }
}
