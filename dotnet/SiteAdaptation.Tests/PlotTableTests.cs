using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CadReader;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R352. The plot table of the worker on made up faces, labels, schedules, blocks and area keys built in code (R10), the C# side of
    // python/tests/test_plot_table.py on the same made up data, on net48 and net8.0
    public sealed class PlotTableTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r352_" + Guid.NewGuid().ToString("N"));

        public PlotTableTests()
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

        private static JObject Plot(string label, double x0, double y0, double x1, double y1, string type = "DP1")
        {
            return new JObject { ["kind"] = "plot_shape", ["plot"] = label, ["type"] = type, ["vertices"] = new JArray(new JArray(x0, y0), new JArray(x1, y0), new JArray(x1, y1), new JArray(x0, y1)) };
        }

        private static JObject Block(string name, double x0, double y0, double x1, double y1, double? rotation = null, bool? mirrored = null)
        {
            var found = new JObject { ["name"] = name, ["outline"] = new JArray(new JArray(x0, y0), new JArray(x1, y0), new JArray(x1, y1), new JArray(x0, y1)), ["insertPoint"] = new JArray(x0, y0, 0.0) };
            if (rotation != null)
            {
                found["rotationDeg"] = rotation.Value;
            }
            if (mirrored != null)
            {
                found["mirrored"] = mirrored.Value;
            }
            return found;
        }

        private const double Bay = 14.9994505;

        // The faces of test_plot_table.PLOTS: 102 drawn as two rings of one label, 103 crossing the border and holding a label of a face with
        // many labels, 104 outside it, 105 a parking face whose first two bays lie 2E-11 m2 apart and are one overlap at 4 decimals, 107
        // touching the border from inside
        private static JArray Plots()
        {
            return new JArray
            {
                Plot("101", 0, 0, 20, 20), Plot("102", 20, 0, 30, 20, "TH1"), Plot("102", 30, 0, 40, 20, "TH1"), Plot("103", 40, 0, 60, 20, "VL3"),
                Plot("104", 60, 0, 80, 20), Plot("105", 0, 20, 20, 40, "C10"), Plot("106", 20, 20, 40, 40, "DP2"), Plot("107", 40, 20, 55, 40, "DP3"),
                new JObject { ["kind"] = "text_only", ["reason"] = "many_labels", ["labels"] = new JArray("103", "999") },
            };
        }

        private static JArray Blocks()
        {
            return new JArray
            {
                Block("A$DP1", 2, 2, 18, 18, 10.0, false), Block("DT1", 15, 2, 25, 18, 30.0, false), Block("B$TH1", 26, 2, 39, 18, 20.0, true),
                Block("PARKA", 2, 22, 3, 22 + Bay - 1e-11, 90.0, false), Block("PARKB", 5, 22, 6, 22 + Bay + 1e-11, 270.0, true),
                Block("PARKC", 8, 22, 9, 22 + Bay - 5e-5, 0.0, false), Block("SMALL", 39.5, 30, 49.5, 31),
            };
        }

        private static LocationPlan.Row Label(string number, string code, double east, double north, string newCode = null, string rev9 = null)
        {
            return new LocationPlan.Row { Number = number, Code = code, East = east, North = north, NewCode = newCode, Rev9 = rev9 };
        }

        private static SheetRows.AreaKey Key(params string[] items)
        {
            var key = new SheetRows.AreaKey();
            for (int index = 0; index < items.Length; index += 3)
            {
                key.Order.Add(items[index]);
                key.Values[items[index]] = new KeyValuePair<string, string>(items[index + 1], items[index + 2]);
            }
            return key;
        }

        private static Dictionary<string, Dictionary<string, object>> MadeTable(JArray blocks = null)
        {
            JArray plots = Plots();
            var border = new List<double[]> { new[] { -5.0, -5.0 }, new[] { 55.0, -5.0 }, new[] { 55.0, 45.0 }, new[] { -5.0, 45.0 } };
            var officials = new Dictionary<string, HashSet<string>> { ["IMC"] = new HashSet<string> { "111", "112", "113" }, ["IMD"] = new HashSet<string> { "114", "116" } };
            var shifts = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("IMC", 10), new KeyValuePair<string, int>("IMD", 11) };
            var labels = new List<LocationPlan.Row>
            {
                Label("2003", "DP1 (QP)", 25.0, 10.0, rev9: "DP1"), Label("2001", "DP1", 10.0, 10.0, "DP1", "DP1"), Label("2002", "TH1", 35.0, 10.0, "TH1"),
                Label("2004", "DP1", 100.0, 100.0), Label("2005", "VL3", 45.0, 10.0), Label("2006", "DP2", 25.0, 30.0), Label("2007", "DP2", 35.0, 30.0),
                Label("2008", "DP3", 47.0, 30.0),
            };
            SheetRows.AreaKey dlus = Key("2001", "Zone 2", "012", "2002", "Zone 2", "012", "2003", "Zone 2", "012", "2006", "Zone 2", "012", "2007", "Zone 3", "014", "2008", "Zone 2", "012");
            SheetRows.AreaKey sales = Key("2001", "Zone 2", "012", "2002", "Zone 2", "013");
            return PlotTable.Build(plots, LocationPlan.Faces(plots, 0.000001), border, officials, shifts, labels, blocks ?? Blocks(), 0.1, dlus, sales, 4);
        }

        private static string Of(Dictionary<string, object> row, string column)
        {
            object value = row[column];
            return value == null ? "null" : value is JToken token ? (token.Type == JTokenType.Null ? "null" : token.Type == JTokenType.Boolean ? ((bool)token ? "true" : "false") : token.ToString()) : value is bool flag ? (flag ? "true" : "false")
                : value is double number ? number.ToString("R", CultureInfo.InvariantCulture) : value.ToString();
        }

        private static string Cells(Dictionary<string, object> row, params string[] columns)
        {
            return string.Join(" ", columns.Select(column => Of(row, column)));
        }

        [Fact]
        public void R352_the_plot_table_of_made_up_faces_gives_every_column_as_data_book_wires_it()
        {
            Dictionary<string, Dictionary<string, object>> rows = MadeTable();
            Assert.Equal(new[] { "101", "102", "103", "105", "106", "107" }, rows.Keys);
            Assert.All(rows.Values, row => Assert.Equal(new[]
            {
                "baseplanLabel", "clientType", "centroid", "faceAreaM2", "crossesBorder", "duplicateLabel", "inManyLabelFace", "officialNumber", "neighbourhood", "twoNeighbourhoods",
                "locationPlanNumber", "locationPlanCode", "locationPlanNewCode", "typologyRev9", "footprintBlock", "footprintRotationDeg", "footprintMirrored", "footprintShared",
                "zone", "nbhId", "areaKeyMethod", "areaKeyCheck",
            }, row.Keys));
            Dictionary<string, object> first = rows["101"];
            double[] centroid = (double[])first["centroid"];
            Assert.Equal(10.0, centroid[0], 9);
            Assert.Equal(10.0, centroid[1], 9);
            Assert.Equal(400.0, (double)first["faceAreaM2"], 9);
            Assert.Equal("111 IMC false", Cells(first, "officialNumber", "neighbourhood", "twoNeighbourhoods"));
            Assert.Equal("2001 DP1 DP1 DP1", Cells(first, "locationPlanNumber", "locationPlanCode", "locationPlanNewCode", "typologyRev9"));
            Assert.Equal("DP1|DT1 10 false false", Cells(first, "footprintBlock", "footprintRotationDeg", "footprintMirrored", "footprintShared"));
            Assert.Equal("Zone 2 012 locationPlanNumber same", Cells(first, "zone", "nbhId", "areaKeyMethod", "areaKeyCheck"));
            Dictionary<string, object> two = rows["102"];
            Assert.Equal("true TH1", Cells(two, "duplicateLabel", "clientType"));
            Assert.Equal(400.0, (double)two["faceAreaM2"], 9);
            Assert.Equal("2002|2003 TH1|DP1 (QP) TH1 DP1", Cells(two, "locationPlanNumber", "locationPlanCode", "locationPlanNewCode", "typologyRev9"));
            Assert.Equal("TH1|DT1 20 true false differs", Cells(two, "footprintBlock", "footprintRotationDeg", "footprintMirrored", "footprintShared", "areaKeyCheck"));
            Assert.Equal("true true true null noRow null null null null", Cells(rows["103"], "crossesBorder", "inManyLabelFace", "twoNeighbourhoods", "officialNumber", "areaKeyMethod",
                "locationPlanNewCode", "footprintBlock", "footprintShared", "zone"));
            Assert.Equal("116 IMD noNumber", Cells(rows["105"], "officialNumber", "neighbourhood", "areaKeyMethod"));
            Assert.Equal("twoValues null", Cells(rows["106"], "areaKeyMethod", "footprintBlock"));
            Assert.Equal("false noSalesRow SMALL null null", Cells(rows["107"], "crossesBorder", "areaKeyCheck", "footprintBlock", "footprintRotationDeg", "footprintMirrored"));
        }

        private static List<double[]> Ring(params double[] values)
        {
            var found = new List<double[]>();
            for (int index = 0; index < values.Length; index += 2)
            {
                found.Add(new[] { values[index], values[index + 1] });
            }
            return found;
        }

        // test_plot_table.probe_bays: the bays of the breaker's probe of PR 54 on a mm grid at site coordinates, made from whole mm so both
        // languages hold the same doubles, A 1.001 by 0.999 m and B 1.000 by 1.000 m, 1E-6 m2 apart, on a face of 15 m
        private static IEnumerable<List<double[]>[]> ProbeBays()
        {
            for (long k = 0; k < 200; k++)
            {
                double x0 = (500000000 + (k * 7919003) % 3000000) / 1000.0, y0 = (2400000000 + (k * 6271009) % 3000000) / 1000.0;
                yield return new[]
                {
                    Ring(x0 - 5, y0 - 5, x0 + 10, y0 - 5, x0 + 10, y0 + 10, x0 - 5, y0 + 10), Ring(x0, y0, x0 + 1.001, y0, x0 + 1.001, y0 + 0.999, x0, y0 + 0.999),
                    Ring(x0 + 3, y0, x0 + 4.0, y0, x0 + 4.0, y0 + 1.0, x0 + 3, y0 + 1.0),
                };
            }
        }

        // test_plot_table.TURNED: four turned bays of the probe whose overlaps lie 1E-6 m2 apart within 2E-9
        private static IEnumerable<List<double[]>[]> Turned()
        {
            yield return new[]
            {
                Ring(500106.568, 2400080.837, 500124.46, 2400089.775, 500115.522, 2400107.667, 500097.63, 2400098.729),
                Ring(500111.568, 2400085.837, 500113.804, 2400086.954, 500111.57, 2400091.427, 500109.333, 2400090.31),
                Ring(500115.146, 2400087.625, 500117.383, 2400088.742, 500115.148, 2400093.215, 500112.912, 2400092.097),
            };
            yield return new[]
            {
                Ring(501628.12, 2400954.942, 501648.12, 2400954.945, 501648.117, 2400974.945, 501628.117, 2400974.942),
                Ring(501633.12, 2400959.942, 501635.62, 2400959.942, 501635.619, 2400964.942, 501633.119, 2400964.942),
                Ring(501637.12, 2400959.942, 501639.62, 2400959.943, 501639.619, 2400964.943, 501637.119, 2400964.942),
            };
            yield return new[]
            {
                Ring(502350.972, 2402695.553, 502370.972, 2402695.557, 502370.968, 2402715.557, 502350.968, 2402715.553),
                Ring(502355.972, 2402700.553, 502358.472, 2402700.553, 502358.471, 2402705.553, 502355.971, 2402705.553),
                Ring(502359.972, 2402700.553, 502362.472, 2402700.554, 502362.471, 2402705.554, 502359.971, 2402705.553),
            };
            yield return new[]
            {
                Ring(500507.875, 2401453.471, 500525.766, 2401462.41, 500516.828, 2401480.301, 500498.936, 2401471.363),
                Ring(500512.875, 2401458.471, 500515.111, 2401459.589, 500512.877, 2401464.061, 500510.64, 2401462.944),
                Ring(500516.453, 2401460.259, 500518.69, 2401461.376, 500516.455, 2401465.849, 500514.219, 2401464.732),
            };
        }

        [Fact]
        public void R352_blocks_of_one_overlap_as_r385_writes_it_keep_the_read_order_and_binary_noise_never_parts_them()
        {
            // PARKB lies 2E-11 m2 over PARKA, one overlap at 4 decimals, so they keep the order of the read, then PARKC 5E-5 m2 smaller
            Assert.Equal("PARKA|PARKB|PARKC 90 false", Cells(MadeTable()["105"], "footprintBlock", "footprintRotationDeg", "footprintMirrored"));
            Assert.Equal(new[] { 9, 3, 4, 7 }, PlotTable.BlockOrder(new Dictionary<int, double> { [7] = 5.0, [3] = 5.0 + 9e-7, [4] = 5.0 - 9e-7, [9] = 7.0 }, 4));
            Assert.Equal(new[] { 1, 5 }, PlotTable.BlockOrder(new Dictionary<int, double> { [1] = 2.0, [5] = 2.0 + 2e-6 }, 4));
            Assert.Equal(new[] { 5, 1 }, PlotTable.BlockOrder(new Dictionary<int, double> { [1] = 2.0, [5] = 2.0 + 2e-4 }, 4));
            Assert.Equal(new[] { 0, 1, 2, 3 }, PlotTable.BlockOrder(new Dictionary<int, double> { [0] = 1.0, [1] = 1.0 - 6e-7, [2] = 1.0 - 1.2e-6, [3] = 1.0 - 1.8e-6 }, 4));
            Assert.Empty(PlotTable.BlockOrder(new Dictionary<int, double>(), 4));
            // The bays of the probe went by size or by read order on the binary noise, the C# 2 of 8 turned bays otherwise than shapely. At
            // 4 decimals each pair is one overlap and keeps the read order, as test_plot_table holds it on shapely
            foreach (List<double[]>[] item in ProbeBays().Concat(Turned()))
            {
                var overlaps = new Dictionary<int, double> { [0] = PolygonOverlap.Area(item[0], item[1]), [1] = PolygonOverlap.Area(item[0], item[2]) };
                double gap = Math.Abs(overlaps[1] - overlaps[0]);
                Assert.True(gap > 0 && gap < 1.01e-6, "gap " + gap.ToString("R", CultureInfo.InvariantCulture));
                Assert.Equal(new[] { 0, 1 }, PlotTable.BlockOrder(overlaps, 4));
            }
        }

        [Fact]
        public void R352_the_decimals_of_the_block_order_are_addin_plot_table_and_never_a_parity_setting()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal(4, PlotTableSettings.From(config).OverlapDecimals);
            Assert.DoesNotContain("overlapDecimals", config["addin"]["parity"].ToString());
            foreach (JToken bad in new JToken[] { null, "4", 4.0, true, -1, 11 })
            {
                var changed = (JObject)config.DeepClone();
                if (bad == null)
                {
                    ((JObject)changed["addin"]).Remove("plotTable");
                }
                else
                {
                    changed["addin"]["plotTable"]["overlapDecimals"] = bad;
                }
                InvalidDataException refused = Assert.Throws<InvalidDataException>(() => PlotTableSettings.From(changed));
                Assert.Contains("addin.plotTable.overlapDecimals", refused.Message);
            }
            // A read of the plot table with no addin.plotTable is refused before any read, the areas tolerance of the parity never taken
            var without = (JObject)config.DeepClone();
            ((JObject)without["addin"]).Remove("plotTable");
            string path = Path.Combine(folder, "project_no_table.json");
            File.WriteAllText(path, without.ToString());
            string border = Path.Combine(folder, "phase1_border.csv");
            File.WriteAllText(border, "x,y\n0,0\n10,0\n10,10\n");
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "t2", Command = "read", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { Input("config", path), Input(PlotTable.BorderRole, border) }, Scope = new WorkerScope { Kind = "project" },
                OutputFolder = Path.Combine(folder, "out"), Steps = new List<string>(), StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "t2"));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("addin.plotTable.overlapDecimals", answer.Message);
        }

        [Fact]
        public void R352_a_footprint_block_that_is_not_a_simple_ring_is_refused()
        {
            JArray blocks = Blocks();
            blocks.Add(new JObject { ["name"] = "BOW", ["outline"] = new JArray(new JArray(2, 2), new JArray(8, 8), new JArray(8, 2), new JArray(2, 8)), ["insertPoint"] = new JArray(2, 2, 0), ["rotationDeg"] = 0.0, ["mirrored"] = false });
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => MadeTable(blocks));
            Assert.Contains("BOW", refused.Message);
            Assert.Contains("not a simple ring", refused.Message);
        }

        [Fact]
        public void R352_the_overlap_by_vertical_slabs_is_the_area_two_polygons_share_and_contains_allows_a_touch()
        {
            var square = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 } };
            var shifted = square.Select(point => new[] { point[0] + 4.0, point[1] + 3.0 }).ToList();
            Assert.Equal(42.0, PolygonOverlap.Area(square, shifted), 9);
            var triangle = new List<double[]> { new[] { 5.0, -5.0 }, new[] { 15.0, 5.0 }, new[] { 5.0, 15.0 } };
            // The triangle spans y from x - 10 to 20 - x, so over x from 5 to 10 it holds the whole height of the square
            Assert.Equal(50.0, PolygonOverlap.Area(square, triangle), 9);
            var hook = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 6.0, 0.0 }, new[] { 6.0, 6.0 }, new[] { 4.0, 6.0 }, new[] { 4.0, 2.0 }, new[] { 2.0, 2.0 }, new[] { 2.0, 6.0 }, new[] { 0.0, 6.0 } };
            var bar = new List<double[]> { new[] { -1.0, 3.0 }, new[] { 7.0, 3.0 }, new[] { 7.0, 4.0 }, new[] { -1.0, 4.0 } };
            Assert.Equal(4.0, PolygonOverlap.Area(hook, bar), 9);
            Assert.Equal(0.0, PolygonOverlap.Area(square, square.Select(point => new[] { point[0] + 10.0, point[1] }).ToList()), 9);
            Assert.True(PolygonOverlap.Contains(square, new List<double[]> { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 5.0 } }));
            Assert.False(PolygonOverlap.Contains(square, shifted));
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        [Fact]
        public void R352_a_read_of_the_plot_table_with_no_location_plan_footprint_or_area_key_is_refused_and_its_step_needs_them()
        {
            string config = Path.Combine(folder, "project.json");
            File.Copy(WorkerRunTests.RepoConfig(), config);
            string border = Path.Combine(folder, "phase1_border.csv");
            File.WriteAllText(border, "x,y\n0,0\n10,0\n10,10\n");
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "t1", Command = "read", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { Input("config", config), Input(PlotTable.BorderRole, border) }, Scope = new WorkerScope { Kind = "project" },
                OutputFolder = Path.Combine(folder, "out"), Steps = new List<string>(), StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "t1"));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("the plot table needs the location plan, the footprint blocks and the area key", answer.Message);
            Assert.Equal(new[] { "plotNumbers", "baseplan", "villaBlocks", "schedule:*", "dlus", "sales", "border" }, WorkerRunner.Steps[PlotTable.StepName]);
            Assert.Equal("read", WorkerRunner.StepCommand[PlotTable.StepName]);
            Assert.False(File.Exists(Path.Combine(folder, "out", "read_plotTable.json")));
        }
    }
}
