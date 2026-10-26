using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R354 to R357. The build steps of the worker on made up faces, blocks, curbs and clusters built in code (R10), the C# side of
    // python/tests/test_build_steps.py on the same made up data, on net48 and net8.0
    public sealed class BuildStepTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r354_" + Guid.NewGuid().ToString("N"));

        public BuildStepTests()
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

        private static double[][] Ring(double x0, double y0, double x1, double y1)
        {
            return new[] { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };
        }

        private static JArray Points(double[][] ring)
        {
            return new JArray(ring.Select(point => new JArray(point[0], point[1])));
        }

        private static JObject Shape(string label, double[][] ring)
        {
            return new JObject { ["kind"] = "plot_shape", ["plot"] = label, ["vertices"] = Points(ring) };
        }

        // The faces of test_build_steps.ROW and APART: a row of six 12 m faces along x numbered 1 to 6, faces 7 and 8 apart
        private static Dictionary<string, double[][]> Rings()
        {
            var found = new Dictionary<string, double[][]>();
            for (int index = 0; index < 6; index++)
            {
                found[(index + 1).ToString()] = Ring(index * 12.0, 0.0, index * 12.0 + 12.0, 25.0);
            }
            found["7"] = Ring(100.0, 0.0, 112.0, 25.0);
            found["8"] = Ring(112.0, 0.0, 124.0, 25.0);
            return found;
        }

        private static Dictionary<string, BuildFace> Faces()
        {
            return Rings().ToDictionary(item => item.Key, item => BuildFace.Of((100 + int.Parse(item.Key)).ToString(), item.Value));
        }

        private static readonly string[][] Wired =
        {
            new[] { "1", "Duplex_DP2" }, new[] { "2", "Duplex_DP2" }, new[] { "3", "Duplex_DP2" }, new[] { "4", "Duplex_DP2" }, new[] { "5", "Villa_VD2" },
            new[] { "6", "Duplex_DP2" }, new[] { "7", "Pair_N" }, new[] { "8", "Pair_N" },
        };

        private static Dictionary<string, Dictionary<string, string>> Rows()
        {
            return Wired.ToDictionary(item => item[0], item => new Dictionary<string, string>
            {
                ["locationPlanNumber"] = item[0], ["baseplanLabel"] = (100 + int.Parse(item[0])).ToString(), ["typologyRev9"] = "DP2", ["footprintBlock"] = item[1],
            });
        }

        // test_build_steps.BLOCKS: two duplex blocks of one name, a villa block on 5 alone, two blocks of one name that tie at 150 m2 on 7,
        // the second also on 8, and a block that touches 6 along an edge only
        private static JArray Blocks()
        {
            return new JArray(
                new JObject { ["name"] = "Duplex_DP2", ["outline"] = Points(Ring(2.0, 5.0, 22.0, 20.0)) },
                new JObject { ["name"] = "Duplex_DP2", ["outline"] = Points(Ring(26.0, 5.0, 46.0, 20.0)) },
                new JObject { ["name"] = "Villa_VD2", ["outline"] = Points(Ring(50.0, 5.0, 58.0, 20.0)) },
                new JObject { ["name"] = "Pair_N", ["outline"] = Points(Ring(100.0, 0.0, 106.0, 25.0)) },
                new JObject { ["name"] = "Pair_N", ["outline"] = Points(Ring(106.0, 0.0, 118.0, 25.0)) },
                new JObject { ["name"] = "Duplex_DP2", ["outline"] = Points(Ring(72.0, 5.0, 80.0, 20.0)) });
        }

        private static JObject Analysis()
        {
            return new JObject { ["source"] = "ROSHN Makkah Baseplan.dwg", ["clusters"] = new JArray(new JObject { ["code"] = "M02", ["plots"] = new JArray("104", "105", "106") }) };
        }

        private static List<SiteCluster> MadeClusters(JObject analysis = null, Dictionary<string, Dictionary<string, string>> rows = null)
        {
            rows = rows ?? Rows();
            Dictionary<string, BuildFace> faces = Faces();
            List<string> units = rows.Where(item => !string.IsNullOrEmpty(item.Value["typologyRev9"]) && faces.ContainsKey(item.Key)).Select(item => item.Key).ToList();
            Dictionary<string, string> numberOf = FaceStep.NumberOfLabel(rows.Values);
            return SiteClusters.Site(units, rows, faces, SiteClusters.BlocksByName(Blocks()), SiteClusters.AnalysisOf(analysis ?? Analysis(), "analysis.json", "ROSHN Makkah Baseplan.dwg"), numberOf);
        }

        [Fact]
        public void R354_the_clusters_of_a_build_are_the_footprint_clusters_then_the_rest_of_the_analysis_each_with_its_source()
        {
            List<SiteCluster> clusters = MadeClusters();
            Assert.Equal(new[] { "Duplex_DP2 1,2 footprint", "Duplex_DP2 3,4 footprint", "M02 5,6 analysis" },
                clusters.Select(cluster => cluster.Code + " " + string.Join(",", cluster.Plots) + " " + cluster.Source).ToArray());
            Dictionary<string, Dictionary<string, object>> step = SiteClusters.Step(Enumerable.Range(1, 8).Select(number => number.ToString()), clusters);
            Assert.Equal(new[] { "1|2", "3|4", "5|6" }, step.Keys.ToArray());
            Assert.Equal(new[] { "5", "6" }, (List<string>)step["5|6"]["plots"]);
            Assert.Equal("M02", step["5|6"]["code"]);
            Assert.Equal("analysis", step["5|6"]["source"]);
            Assert.Equal(new[] { "1|2" }, SiteClusters.Step(new[] { "2" }, clusters).Keys.ToArray());
            Assert.Empty(SiteClusters.Step(new[] { "7", "8" }, clusters));
        }

        [Fact]
        public void R354_two_blocks_of_one_name_that_tie_on_a_face_give_it_the_first_and_a_block_on_its_edge_gives_none()
        {
            List<SiteCluster> clusters = MadeClusters();
            Assert.DoesNotContain(clusters, cluster => cluster.Plots.Contains("7") || cluster.Plots.Contains("8"));
            Assert.All(clusters.Where(cluster => cluster.Plots.Contains("6")), cluster => Assert.Equal("analysis", cluster.Source));
        }

        [Fact]
        public void R354_a_plot_of_two_plan_numbers_or_a_cluster_left_with_one_plot_is_no_cluster()
        {
            Dictionary<string, Dictionary<string, string>> rows = Rows();
            rows["7|8"] = new Dictionary<string, string>(rows["7"]) { ["locationPlanNumber"] = "7|8", ["baseplanLabel"] = "107" };
            var analysis = new JObject
            {
                ["source"] = "ROSHN Makkah Baseplan.dwg",
                ["clusters"] = new JArray(new JObject { ["code"] = "M04", ["plots"] = new JArray("107", "108") }, new JObject { ["code"] = "M02", ["plots"] = new JArray("101", "105") }),
            };
            Assert.Equal(new[] { "1,2", "3,4" }, MadeClusters(analysis, rows).Select(cluster => string.Join(",", cluster.Plots)).ToArray());
        }

        [Fact]
        public void R354_a_label_of_two_rings_is_refused_by_the_build_steps()
        {
            Dictionary<string, double[][]> rings = Rings();
            var plots = new JArray(Shape("101", rings["1"]), Shape("102", rings["2"]), Shape("0102", Ring(24.0, 0.0, 30.0, 10.0)), Shape("x", rings["3"]));
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => BuildFace.OneRing(plots));
            Assert.Equal("1 baseplan labels hold two rings or more: 102, a build takes a face of one ring, whose edges it walks in the order shapely gives them, "
                + "so plan_from_book.py and the C# refuse such a label (R354)", refused.Message);
            BuildFace.OneRing(new JArray(Shape("101", rings["1"]), Shape("102", Ring(0.0, 0.0, 1.0, 0.0))));
        }

        [Fact]
        public void R354_a_footprint_block_that_is_not_a_simple_ring_is_refused_by_the_build()
        {
            JArray blocks = Blocks();
            blocks.Add(new JObject { ["name"] = "Duplex_DP2", ["insertPoint"] = new JArray(1.0, 2.0, 0.0), ["outline"] = new JArray(new JArray(0.0, 0.0), new JArray(10.0, 10.0), new JArray(10.0, 0.0), new JArray(0.0, 10.0)) });
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => SiteClusters.BlocksByName(blocks));
            Assert.Equal("the footprint block Duplex_DP2 at [1.0, 2.0, 0.0] is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# would not, "
                + "so the build refuses it (R354)", refused.Message);
            Assert.Equal(new[] { "Duplex_DP2", "Villa_VD2", "Pair_N" }, SiteClusters.BlocksByName(Blocks()).Keys.ToArray());
            Assert.Equal(3, SiteClusters.BlocksByName(Blocks())["Duplex_DP2"].Count);
        }

        [Fact]
        public void R354_an_analysis_of_another_drawing_or_with_no_clusters_is_refused()
        {
            Assert.Single(SiteClusters.AnalysisOf(Analysis(), "analysis.json", "ROSHN Makkah Baseplan.dwg"));
            var cases = new[]
            {
                Tuple.Create(new JObject { ["source"] = "Makkah Building Footprint.dwg", ["clusters"] = new JArray() }, "is of Makkah Building Footprint.dwg"),
                Tuple.Create(new JObject { ["clusters"] = new JArray() }, "is of None"),
                Tuple.Create(new JObject { ["source"] = "ROSHN Makkah Baseplan.dwg" }, "holds no clusters list"),
                Tuple.Create(new JObject { ["source"] = "rosHN Makkah Baseplan.dwg", ["clusters"] = new JArray() }, "is of rosHN Makkah Baseplan.dwg"),
            };
            foreach (Tuple<JObject, string> item in cases)
            {
                InvalidDataException refused = Assert.Throws<InvalidDataException>(() => SiteClusters.AnalysisOf(item.Item1, "analysis.json", "ROSHN Makkah Baseplan.dwg"));
                Assert.Contains(item.Item2, refused.Message);
                Assert.EndsWith("(R354)", refused.Message);
            }
        }

        [Fact]
        public void R354_the_config_names_the_fields_of_the_step_clusters_all_exact()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal(new[] { "plots", "code", "source" }, config.StepFields(SiteClusters.StepName).Select(field => field.Name).ToArray());
            Assert.All(config.StepFields(SiteClusters.StepName), field => Assert.Equal("noGeometry", field.Kind));
        }

        // The ring of a face as shapely gives Polygon(vertices).buffer(0): a repeated vertex and the closing one dropped, an anticlockwise
        // ring turned clockwise from its first vertex, a clockwise ring as it is (measured on the 2,962 faces of the baseplan, round_addin
        // logs p4e_buffer_order.txt)
        [Fact]
        public void R354_the_ring_of_a_face_is_the_one_shapely_gives_by_buffer_zero()
        {
            double[][] square = Ring(0, 0, 10, 0 + 10);
            Assert.Equal("0,0 0,10 10,10 10,0", Text(BuildFace.Of("1", square).Ring));
            Assert.Equal("0,10 10,10 10,0 0,0", Text(BuildFace.Of("1", square.Reverse().ToArray()).Ring));
            Assert.Equal("0,0 0,10 10,10 10,0", Text(BuildFace.Of("1", new[] { new double[] { 0, 0 }, new double[] { 10, 0 }, new double[] { 10, 0 }, new double[] { 10, 10 }, new double[] { 0, 10 }, new double[] { 0, 0 } }).Ring));
            Assert.Equal("0,0 0,10 10,10 10,0 5,0", Text(BuildFace.Of("1", new[] { new double[] { 0, 0 }, new double[] { 5, 0 }, new double[] { 10, 0 }, new double[] { 10, 10 }, new double[] { 0, 10 } }).Ring));
            Assert.Equal(100.0, BuildFace.Of("1", square).Area);
        }

        // A made up read of the worker: its role, source, units and a run stamp of one made up drawing beside it
        private string ReadFile(string name, string role, string source, JObject content, string units = "Meters")
        {
            string drawing = Path.Combine(folder, role + ".dwg");
            if (!File.Exists(drawing))
            {
                File.WriteAllText(drawing, "a made up drawing of " + role);
            }
            content["role"] = role;
            content["source"] = source;
            content["units"] = units;
            content[RunStamp.Key] = new JObject { ["runId"] = "r0", ["inputs"] = new JArray(new JObject { ["role"] = role, ["path"] = drawing, ["sha256"] = FileHash.Sha256(drawing) }) };
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content.ToString());
            return path;
        }

        internal static string RepoRules()
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(WorkerRunTests.RepoConfig())), "rules", "roshn-makkah.json");
        }

        private WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        // The request of a worker build of the build steps on the made up reads of the faces and blocks of test_build_steps.py
        private WorkerRequest BuildRequest(string runId, IEnumerable<string> steps)
        {
            Dictionary<string, double[][]> rings = Rings();
            var plots = new JArray(rings.Select(item => Shape((100 + int.Parse(item.Key)).ToString(), item.Value)));
            string baseplan = ReadFile("read_baseplan.json", "baseplan", "ROSHN Makkah Baseplan.dwg", new JObject { ["plots"] = plots });
            string footprint = ReadFile("read_villaBlocks.json", "villaBlocks", "Makkah Building Footprint.dwg", new JObject { ["blocks"] = Blocks() });
            string data = Path.Combine(folder, "plots.csv");
            File.WriteAllText(data, "locationPlanNumber,baseplanLabel,typologyRev9,footprintBlock\n" + string.Join("\n", Wired.Select(item => item[0] + "," + (100 + int.Parse(item[0])) + ",DP2," + item[1])) + "\n");
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, "kind,number,nbh\n" + string.Join("\n", Wired.Select(item => "clientCode," + item[0] + ",012")) + "\n");
            string analysis = Path.Combine(folder, "analysis.json");
            File.WriteAllText(analysis, Analysis().ToString());
            string config = WorkerRunTests.RepoConfig();
            return new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile>
                {
                    Input("config", config), Input("rules", RepoRules()), Input("plotsCsv", data), Input("labels", labels), Input("baseplanRead", baseplan),
                    Input("villaBlocksRead", footprint), Input("analysis", analysis),
                },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, Steps = steps.ToList(), StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
        }

        [Fact]
        public void R354_the_worker_build_writes_the_step_clusters_from_the_reads_and_refuses_a_read_of_another_role_a_stale_stamp_or_another_unit()
        {
            Assert.Equal(0, WorkerRunner.Run(BuildRequest("c1", new[] { "clusters" }).Write(folder), WorkerCommands.Default(), () => 9000.0));
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c1"));
            JObject file = RunStamp.Read(Path.Combine(folder, "steps", "step_clusters_csharp.json"));
            Assert.Equal(new[] { "1|2", "3|4", "5|6" }, ((JObject)file["plots"]).Properties().Select(item => item.Name).ToArray());
            Assert.Equal("analysis", (string)file["plots"]["5|6"]["written"]["source"]);
            Assert.Equal(3, answer.Counts["clusters items"]);
            Assert.Equal(1, answer.Counts["clusters of the build from the analysis"]);

            WorkerRequest other = BuildRequest("c2", new[] { "clusters" });
            other.Inputs[5] = Input("villaBlocksRead", other.Inputs[4].Path);
            Assert.Equal(2, WorkerRunner.Run(other.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the input villaBlocksRead is the read of baseplan, the build takes the read of villaBlocks (R354)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c2")).Message);

            WorkerRequest stale = BuildRequest("c3", new[] { "clusters" });
            File.AppendAllText(Path.Combine(folder, "villaBlocks.dwg"), " changed");
            Assert.Equal(2, WorkerRunner.Run(stale.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("changed since the output was written", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c3")).Message);
            Assert.False(File.Exists(Path.Combine(folder, "steps", "step_clusters_csharp.json")), "a run refused leaves no step file");

            File.Delete(Path.Combine(folder, "villaBlocks.dwg"));
            WorkerRequest millimetres = BuildRequest("c4", new[] { "clusters" });
            millimetres.Inputs[4] = Input("baseplanRead", ReadFile("read_baseplan.json", "baseplan", "ROSHN Makkah Baseplan.dwg", new JObject { ["plots"] = new JArray() }, "Millimeters"));
            Assert.Equal(2, WorkerRunner.Run(millimetres.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("a drawing is read only in Meters", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c4")).Message);

            WorkerRequest lacking = BuildRequest("c5", new[] { "clusters" });
            lacking.Inputs.RemoveAt(6);
            Assert.Equal(2, WorkerRunner.Run(lacking.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the step clusters needs the input analysis", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c5")).Message);
        }

        [Fact]
        public void R354_a_level_profile_with_no_key_rules_or_a_key_rule_a_build_cannot_give_is_refused_with_the_lines_of_python()
        {
            JObject rules = JObject.Parse(File.ReadAllText(RepoRules()));
            Assert.NotNull(BuildRules.KeyRules(rules, null, "clusters"));
            InvalidDataException none = Assert.Throws<InvalidDataException>(() => BuildRules.KeyRules(rules, "native2022", "clusters"));
            Assert.Equal("the build steps clusters of the parity harness are those of a build by keyRules, the level profile native2022 holds none, the C# builds by keyRules alone (R354)", none.Message);
            Assert.Equal("the rules profile has no level profile other", Assert.Throws<InvalidDataException>(() => BuildRules.KeyRules(rules, "other", "clusters")).Message);
            JObject bad = (JObject)rules.DeepClone();
            bad["levelProfiles"]["parsons2024Rev6"]["keyRules"]["hand villa"]["rule"] = "clusterIndexParity";
            Assert.Equal("keyRules hand villa codes clusterIndexParity, a build gives only all, blockMirror, clusterSide, openEnd, roadSide, blockMirrorOpenEnd, blockMirrorRoadSide, rowPairSide, rowPairSideOrBlockMirrorOpenEnd with byFile (R337)",
                Assert.Throws<InvalidDataException>(() => BuildRules.KeyRules(bad, null, "clusters")).Message);
            bad = (JObject)rules.DeepClone();
            bad["levelProfiles"]["parsons2024Rev6"]["keyRules"]["linkRotation"]["rule"] = "nearestCurb";
            Assert.Equal("keyRules linkRotation codes nearestCurb, a build gives only frontRule (R337)", Assert.Throws<InvalidDataException>(() => BuildRules.KeyRules(bad, null, "clusters")).Message);
            bad = (JObject)rules.DeepClone();
            bad["levelProfiles"]["parsons2024Rev6"]["keyRules"]["rampSlope"]["table"]["C10"] = "0.5";
            Assert.Equal("the slope of El @ Entry and the ramp class of keyRules differ for C10 (rampSlopePercent 2.0, keyRules.rampSlope 0.5), give both one slope (R337)",
                Assert.Throws<InvalidDataException>(() => BuildRules.KeyRules(bad, null, "clusters")).Message);
        }

        private static string Text(IEnumerable<double[]> ring)
        {
            return string.Join(" ", ring.Select(point => point[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + point[1].ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
}
