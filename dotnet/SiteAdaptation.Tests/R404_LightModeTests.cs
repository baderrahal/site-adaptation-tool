using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R404. Light mode of docs/design/11-light-mode.md: the Villa mode, the base shape of a prototype and its cache, the base against
    // the footprint. No Revit type, so these run on net8.0 too
    public class R404_LightModeTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "r404_" + Guid.NewGuid().ToString("N"));

        public R404_LightModeTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static JObject Config(Action<JObject> change = null)
        {
            var block = new JObject
            {
                ["note"] = "test",
                ["villaModeDefault"] = "masses",
                ["solidHeightM"] = 3.0,
                ["baseBoundary"] = "lowestFloor",
                ["baseBoundaryFloorType"] = null,
                ["footprintLimitMm"] = 100.0,
                ["footprintSampleStepMm"] = 10.0,
            };
            change?.Invoke(block);
            return new JObject { ["addin"] = new JObject { ["lightMode"] = block } };
        }

        private static readonly double[][] Square = { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 } };

        [Fact]
        public void R404_the_light_mode_keys_read_from_config()
        {
            LightModeConfig config = LightModeConfig.From(Config());
            Assert.Equal(VillaModes.Masses, config.VillaModeDefault);
            Assert.Equal(3.0, config.SolidHeightM);
            Assert.Equal(BaseBoundarySources.LowestFloor, config.BaseBoundary);
            Assert.Null(config.BaseBoundaryFloorType);
            Assert.Equal(100.0, config.FootprintLimitMm);
            Assert.Equal(10.0, config.FootprintSampleStepMm);
        }

        [Fact]
        public void R404_the_repo_config_holds_the_light_mode_block()
        {
            JObject root = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            LightModeConfig config = LightModeConfig.From(root);
            Assert.Equal(VillaModes.Masses, config.VillaModeDefault);
            Assert.Equal(BaseBoundarySources.LowestFloor, config.BaseBoundary);
            Assert.True(config.SolidHeightM > 0 && config.FootprintLimitMm > 0);
        }

        [Theory]
        [InlineData("villaModeDefault", "both")]
        [InlineData("baseBoundary", "roof")]
        [InlineData("solidHeightM", -1.0)]
        [InlineData("footprintLimitMm", 0.0)]
        [InlineData("footprintSampleStepMm", 0.0)]
        public void R404_a_light_mode_key_out_of_range_is_refused_by_its_name(string key, object value)
        {
            var error = Assert.Throws<InvalidDataException>(() => LightModeConfig.From(Config(block => block[key] = JToken.FromObject(value))));
            Assert.Contains(key, error.Message);
        }

        [Fact]
        public void R404_a_named_floor_type_needs_its_type_name()
        {
            Assert.Throws<InvalidDataException>(() => LightModeConfig.From(Config(block => block["baseBoundary"] = "floorType")));
            LightModeConfig config = LightModeConfig.From(Config(block => { block["baseBoundary"] = "floorType"; block["baseBoundaryFloorType"] = "Generic 325"; }));
            Assert.Equal("Generic 325", config.BaseBoundaryFloorType);
        }

        [Fact]
        public void R404_no_block_is_refused()
        {
            Assert.Throws<InvalidDataException>(() => LightModeConfig.From(new JObject { ["addin"] = new JObject() }));
        }

        [Fact]
        public void R404_the_villa_mode_picked_for_the_run_wins_else_the_config_default()
        {
            LightModeConfig config = LightModeConfig.From(Config());
            Assert.Equal(VillaModes.Masses, VillaModeChoice.Resolve(null, config));
            Assert.Equal(VillaModes.Links, VillaModeChoice.Resolve(VillaModes.Links, config));
            Assert.Equal(VillaModes.Links, VillaModeChoice.Resolve(null, LightModeConfig.From(Config(block => block["villaModeDefault"] = "links"))));
            Assert.Throws<ArgumentException>(() => VillaModeChoice.Resolve("solid", config));
        }

        [Fact]
        public void R404_the_settings_json_keeps_the_villa_mode_for_the_pane_row()
        {
            var settings = new ProjectSettings { VillaMode = VillaModes.Links };
            Assert.Equal(VillaModes.Links, SettingsJson.Read(SettingsJson.Write(settings)).VillaMode);
        }

        [Fact]
        public void R404_the_base_is_placed_by_insert_rotation_and_mirror()
        {
            double[][] shape = { new[] { 1.0, 2.0 } };
            Assert.Equal(new[] { 101.0, 202.0 }, ShapeMath.Place(shape, new[] { 100.0, 200.0 }, 0, false)[0].Select(value => Math.Round(value, 9)));
            Assert.Equal(new[] { 98.0, 201.0 }, ShapeMath.Place(shape, new[] { 100.0, 200.0 }, 90, false)[0].Select(value => Math.Round(value, 9)));
            // Mirrored flips the local Y before the turn, as PlacementMath.Steps mirrors across the plane whose normal is the turned Y axis
            Assert.Equal(new[] { 101.0, 198.0 }, ShapeMath.Place(shape, new[] { 100.0, 200.0 }, 0, true)[0].Select(value => Math.Round(value, 9)));
            Assert.Equal(new[] { 102.0, 201.0 }, ShapeMath.Place(shape, new[] { 100.0, 200.0 }, 90, true)[0].Select(value => Math.Round(value, 9)));
        }

        [Fact]
        public void R404_the_distance_of_two_outlines_is_taken_both_ways()
        {
            double[][] moved = Square.Select(point => new[] { point[0] + 0.05, point[1] }).ToArray();
            Assert.Equal(50.0, OutlineDistance.HausdorffMm(Square, moved, 10), 6);
            // A small square inside the big one is near it one way and far the other way, so only both ways give the far side
            double[][] inner = { new[] { 1.0, 1.0 }, new[] { 2.0, 1.0 }, new[] { 2.0, 2.0 }, new[] { 1.0, 2.0 } };
            Assert.Equal(2000.0, OutlineDistance.DirectedMm(inner, Square, 10), 6);
            Assert.Equal(Math.Sqrt(128) * 1000, OutlineDistance.HausdorffMm(Square, inner, 10), 6);
        }

        [Fact]
        public void R404_the_distance_takes_the_points_inside_an_edge_and_not_only_the_corners()
        {
            // The corners of the triangle lie on the square, the middle of its long edge does not
            double[][] notch = { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 10.0 }, new[] { 5.0, 5.0 }, new[] { 0.0, 10.0 } };
            Assert.Equal(0.0, OutlineDistance.DirectedMm(notch.Where((point, index) => index != 3).ToArray(), Square, 10), 6);
            // From the square the farthest point is the middle of its top edge, 5 / sqrt 2 m from the notch, no corner
            Assert.InRange(OutlineDistance.DirectedMm(Square, notch, 10), 3530.0, 3535.6);
            Assert.Equal(5000.0, OutlineDistance.HausdorffMm(Square, notch, 10), 3);
        }

        [Fact]
        public void R404_over_the_limit_the_plot_is_flagged_and_the_run_goes_on()
        {
            LightModeConfig config = LightModeConfig.From(Config());
            double[][] block = Square.Select(point => new[] { point[0] + 0.2, point[1] }).ToArray();
            FootprintRow over = FootprintCheck.Row("101", Square, new[] { block }, config);
            Assert.True(over.Flagged);
            Assert.Equal(200.0, over.DistanceMm.Value, 6);
            FootprintRow near = FootprintCheck.Row("102", Square, new[] { Square.Select(point => new[] { point[0] + 0.02, point[1] }).ToArray() }, config);
            Assert.False(near.Flagged);
        }

        [Fact]
        public void R404_the_footprint_of_a_plot_is_the_block_that_holds_the_middle_of_its_base()
        {
            LightModeConfig config = LightModeConfig.From(Config());
            double[][] far = Square.Select(point => new[] { point[0] + 100, point[1] }).ToArray();
            FootprintRow row = FootprintCheck.Row("101", Square, new[] { far, Square }, config);
            Assert.Equal(0.0, row.DistanceMm.Value, 6);
            FootprintRow none = FootprintCheck.Row("103", Square, new[] { far }, config);
            Assert.True(none.Flagged);
            Assert.Null(none.DistanceMm);
            Assert.Contains("no villa block", none.Why);
        }

        private sealed class CountingReader : IBaseShapeReader
        {
            public int Reads;

            public IReadOnlyList<double[]> Read(string prototypePath, LightModeConfig config)
            {
                Reads++;
                return Square;
            }
        }

        [Fact]
        public void R404_a_shape_is_read_once_per_prototype_file_and_kept_by_its_hash_and_reader_version()
        {
            var reader = new CountingReader();
            string hash = new string('a', 64);
            var cache = new ShapeCache(folder, reader, path => hash);
            LightModeConfig config = LightModeConfig.From(Config());
            Assert.Equal(4, cache.Get("VD4.rvt", config).Points.Count);
            cache.Get("VD4.rvt", config);
            Assert.Equal(1, reader.Reads);
            // A new cache on the same folder, as the next run, reads the file the first one wrote
            new ShapeCache(folder, reader, path => hash).Get("VD4.rvt", config);
            Assert.Equal(1, reader.Reads);
            string file = Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).Single();
            Assert.Contains(hash, Path.GetFileName(file));
            Assert.Contains("v" + ShapeCache.ReaderVersion, Path.GetFileName(file));
            // Another file content or another base boundary source reads the shape again
            new ShapeCache(folder, reader, path => new string('b', 64)).Get("VD4.rvt", config);
            new ShapeCache(folder, reader, path => hash).Get("VD4.rvt", LightModeConfig.From(Config(block => { block["baseBoundary"] = "floorType"; block["baseBoundaryFloorType"] = "Generic 325"; })));
            Assert.Equal(3, reader.Reads);
        }

        [Fact]
        public void R404_a_shape_that_is_no_simple_ring_of_three_corners_is_refused()
        {
            var cache = new ShapeCache(folder, new FixedReader(new[] { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 } }), path => new string('c', 64));
            Assert.Throws<InvalidDataException>(() => cache.Get("VD4.rvt", LightModeConfig.From(Config())));
            var crossed = new ShapeCache(folder, new FixedReader(new[] { new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 } }), path => new string('d', 64));
            Assert.Throws<InvalidDataException>(() => crossed.Get("VD4.rvt", LightModeConfig.From(Config())));
        }

        private sealed class FixedReader : IBaseShapeReader
        {
            private readonly double[][] points;

            public FixedReader(double[][] points)
            {
                this.points = points;
            }

            public IReadOnlyList<double[]> Read(string prototypePath, LightModeConfig config)
            {
                return points;
            }
        }
    }
}
