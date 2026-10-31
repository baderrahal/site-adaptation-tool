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

        private static List<SiteCluster> MadeClusters()
        {
            Dictionary<string, Dictionary<string, string>> rows = Rows();
            Dictionary<string, BuildFace> faces = Faces();
            List<string> units = rows.Where(item => !string.IsNullOrEmpty(item.Value["typologyRev9"]) && faces.ContainsKey(item.Key)).Select(item => item.Key).ToList();
            return SiteClusters.Site(units, rows, faces, SiteClusters.BlocksByName(Blocks()));
        }

        [Fact]
        public void R354_the_clusters_of_a_build_are_the_footprint_clusters_alone_since_q089()
        {
            List<SiteCluster> clusters = MadeClusters();
            Assert.Equal(new[] { "Duplex_DP2 1,2 footprint", "Duplex_DP2 3,4 footprint" },
                clusters.Select(cluster => cluster.Code + " " + string.Join(",", cluster.Plots) + " " + cluster.Source).ToArray());
            Dictionary<string, Dictionary<string, object>> step = SiteClusters.Step(Enumerable.Range(1, 8).Select(number => number.ToString()), clusters);
            Assert.Equal(new[] { "1|2", "3|4" }, step.Keys.ToArray());
            Assert.Equal(new[] { "3", "4" }, (List<string>)step["3|4"]["plots"]);
            Assert.Equal("Duplex_DP2", step["3|4"]["code"]);
            Assert.Equal("footprint", step["3|4"]["source"]);
            Assert.Equal(new[] { "1|2" }, SiteClusters.Step(new[] { "2" }, clusters).Keys.ToArray());
            Assert.Empty(SiteClusters.Step(new[] { "5", "6", "7", "8" }, clusters));
            // Q089. The analysis join is gone, so SiteClusters holds no member of it
            Assert.DoesNotContain(typeof(SiteClusters).GetMembers(), member => new[] { "AnalysisOf", "HoldAnalysis", "Joined", "Numbered", "AnalysisSource", "AnalysisTurn" }.Contains(member.Name));
        }

        [Fact]
        public void R354_two_blocks_of_one_name_that_tie_on_a_face_give_it_the_first_and_a_block_on_its_edge_gives_none()
        {
            List<SiteCluster> clusters = MadeClusters();
            Assert.DoesNotContain(clusters, cluster => cluster.Plots.Contains("7") || cluster.Plots.Contains("8"));
            // The block that touches 6 along an edge shares no area with it, and since Q089 no analysis gives 6 a cluster
            Assert.DoesNotContain(clusters, cluster => cluster.Plots.Contains("6"));
        }

        [Fact]
        public void R354_a_plot_of_two_plan_numbers_or_a_cluster_left_with_one_plot_is_no_cluster()
        {
            Dictionary<string, Dictionary<string, string>> rows = Rows();
            rows["2|9"] = new Dictionary<string, string>(rows["2"]) { ["locationPlanNumber"] = "2|9" };
            Dictionary<string, BuildFace> faces = Faces();
            faces["2|9"] = faces["2"];
            // 1 and 2|9 share the first duplex block, 2|9 is no plot of its own, so 1 is left alone and no cluster
            Assert.Equal(new[] { "3,4" }, SiteClusters.Site(new[] { "1", "2|9", "3", "4" }, rows, faces, SiteClusters.BlocksByName(Blocks()))
                .Select(cluster => string.Join(",", cluster.Plots)).ToArray());
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
            string config = WorkerRunTests.RepoConfig();
            return new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile>
                {
                    Input("config", config), Input("rules", RepoRules()), Input("plotsCsv", data), Input("labels", labels), Input("baseplanRead", baseplan),
                    Input("villaBlocksRead", footprint),
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
            Assert.Equal(new[] { "1|2", "3|4" }, ((JObject)file["plots"]).Properties().Select(item => item.Name).ToArray());
            Assert.Equal("footprint", (string)file["plots"]["3|4"]["written"]["source"]);
            Assert.Equal(2, answer.Counts["clusters items"]);
            Assert.False(answer.Counts.ContainsKey("clusters of the build from the analysis"));

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

            // Q089. A request that names an analysis is refused with the line of plan_from_book.py, for the faces as for the build steps
            foreach (string[] steps in new[] { new[] { "clusters" }, new[] { "faces" } })
            {
                WorkerRequest named = BuildRequest("c5", steps);
                string analysis = Path.Combine(folder, "analysis.json");
                File.WriteAllText(analysis, new JObject { ["source"] = "ROSHN Makkah Baseplan.dwg", ["clusters"] = new JArray() }.ToString());
                named.Inputs.Add(Input("analysis", analysis));
                Assert.Equal(2, WorkerRunner.Run(named.Write(folder), WorkerCommands.Default(), () => 9000.0));
                Assert.Equal(BuildSteps.AnalysisRefused, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c5")).Message);
                Assert.Equal("the analysis is no input of a build since Q089, a build takes the footprint clusters of R236 alone (R354)", BuildSteps.AnalysisRefused);
                Assert.False(File.Exists(Path.Combine(folder, "steps", "step_" + steps[0] + "_csharp.json")), "a run refused leaves no step file");
            }

            // P2. The config of a build is read as project_profile.read_object reads it, a duplicate key refused by the name of the file
            WorkerRequest twice = BuildRequest("c7", new[] { "clusters" });
            string copy = Path.Combine(folder, "project.json");
            File.WriteAllText(copy, "{\"twice\": 1, \"twice\": 2, " + File.ReadAllText(WorkerRunTests.RepoConfig()).Trim().Substring(1));
            twice.Inputs[0] = Input("config", copy);
            Assert.Equal(2, WorkerRunner.Run(twice.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("project.json has a duplicate key twice", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c7")).Message);

            // F3. A setting due as a number given as a text is refused before any step
            WorkerRequest text = BuildRequest("c9", new[] { "clusters" });
            JObject textConfig = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            textConfig["siteBuild"]["corner"]["roadEdgeM"] = "1.0";
            string textCopy = Path.Combine(folder, "project.json");
            File.WriteAllText(textCopy, textConfig.ToString());
            text.Inputs[0] = Input("config", textCopy);
            Assert.Equal(2, WorkerRunner.Run(text.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("config siteBuild.corner.roadEdgeM holds a text, a build takes it as a number (R355)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c9")).Message);

            // G1. A labels.csv with no row is refused, where it gave a build of no plot reported done
            WorkerRequest empty = BuildRequest("c10", new[] { "clusters" });
            File.WriteAllText(empty.Inputs[3].Path, "kind,number,nbh\n");
            empty.Inputs[3] = Input("labels", empty.Inputs[3].Path);
            Assert.Equal(2, WorkerRunner.Run(empty.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("labels.csv holds no row under its header (R345)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "c10")).Message);
        }

        // P3 of the review of PR 55, test_build_steps.SHAPES: a keyRules value of the wrong shape, which Need let through as a list and the C#
        // then read as empty or as no hand, refused with the line of Python
        [Fact]
        public void R354_a_key_rule_of_the_wrong_shape_is_refused_with_the_line_of_python()
        {
            JObject key = (JObject)JObject.Parse(File.ReadAllText(RepoRules()))["levelProfiles"]["parsons2024Rev6"]["keyRules"];
            BuildRules.CheckKeyRules(key);
            var cases = new[]
            {
                Tuple.Create("rampSlope", "table", (JToken)new JArray("C10"), "keyRules rampSlope holds table as a list, a build takes it as an object (R337)"),
                Tuple.Create("linkOrigin", "byCode", (JToken)new JArray(), "keyRules linkOrigin holds byCode as a list, a build takes it as an object (R337)"),
                Tuple.Create("yardOutline", "groups", (JToken)new JObject(), "keyRules yardOutline holds groups as an object, a build takes it as a list (R337)"),
                Tuple.Create("parameter CASE", "table", (JToken)"x", "keyRules parameter CASE holds table as a text, a build takes it as an object (R337)"),
                Tuple.Create("hand villa", "byFile", (JToken)new JObject { ["made-up.rvt"] = new JArray(true) },
                    "keyRules hand villa holds byFile made-up.rvt as a list, a build takes it as an object of factor values (R337)"),
                Tuple.Create("hand cluster", "byFile", (JToken)new JObject { ["made-up.rvt"] = null },
                    "keyRules hand cluster holds byFile made-up.rvt as null, a build takes it as an object of factor values (R337)"),
            };
            foreach (Tuple<string, string, JToken, string> item in cases)
            {
                var changed = (JObject)key.DeepClone();
                changed[item.Item1][item.Item2] = item.Item3;
                Assert.Equal(item.Item4, Assert.Throws<InvalidDataException>(() => BuildRules.CheckKeyRules(changed)).Message);
            }
        }

        // F4 of the third review of PR 55, test_build_steps.KIND_LINES: a key rule of another kind than a build reads, the line of Python
        [Fact]
        public void R337_a_key_rule_of_another_kind_is_refused_with_one_line_the_same_in_python()
        {
            const string parameterRules = "plot, rev9Code, plotAndCode, plotCodeStepsAndFacade, ffl, fflDownToTheCm, all, typology, mirroredFactor, typologyMirrored, steps, typologySteps";
            var cases = new[]
            {
                Tuple.Create(new[] { "linkRotation", "rule" }, (JToken)new JArray("frontRule"), "keyRules linkRotation codes ['frontRule'], a build gives only frontRule (R337)"),
                Tuple.Create(new[] { "linkRotation", "rule" }, (JToken)new JObject { ["a"] = "it's" }, "keyRules linkRotation codes {'a': \"it's\"}, a build gives only frontRule (R337)"),
                Tuple.Create(new[] { "rampSlope", "table", "C10" }, (JToken)new JArray("0.5"),
                    "the slope of El @ Entry and the ramp class of keyRules differ for C10 (rampSlopePercent 2.0, keyRules.rampSlope ['0.5']), give both one slope (R337)"),
                Tuple.Create(new[] { "parameter CASE" }, (JToken)"x", "keyRules parameter CASE codes None, a build gives only " + parameterRules + " (R337)"),
                Tuple.Create(new[] { "entry" }, (JToken)"x", "keyRules entry codes None, a build gives only roadLevel (R337)"),
            };
            foreach (Tuple<string[], JToken, string> item in cases)
            {
                JObject rules = JObject.Parse(File.ReadAllText(RepoRules()));
                JToken place = rules["levelProfiles"]["parsons2024Rev6"]["keyRules"];
                foreach (string key in item.Item1.Take(item.Item1.Length - 1))
                {
                    place = place[key];
                }
                place[item.Item1.Last()] = item.Item2;
                Assert.Equal(item.Item3, Assert.Throws<InvalidDataException>(() => BuildRules.KeyRules(rules, null, "hands")).Message);
            }
        }

        // F3 of the third review of PR 55, test_build_steps.NUMBER_LINES: a setting a build reads as a number given as a text, true or false
        // or null, which (double)token took as a number where Python stopped on TypeError, refused with the line of Python
        [Fact]
        public void R355_a_setting_due_as_a_number_is_refused_with_the_line_of_python()
        {
            JObject real = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            BuildRules.NumberSettings(real, "project.json");
            var cases = new[]
            {
                Tuple.Create(new[] { "siteBuild", "corner", "roadEdgeM" }, (JToken)"1.0", "config siteBuild.corner.roadEdgeM holds a text, a build takes it as a number (R355)"),
                Tuple.Create(new[] { "analysis", "toleranceM" }, (JToken)true, "config analysis.toleranceM holds true or false, a build takes it as a number (R355)"),
                Tuple.Create(new[] { "analysis", "partyWallShare" }, (JToken)JValue.CreateNull(), "config analysis.partyWallShare holds null, a build takes it as a number (R355)"),
            };
            foreach (Tuple<string[], JToken, string> item in cases)
            {
                var changed = (JObject)real.DeepClone();
                JToken place = changed;
                foreach (string key in item.Item1.Take(item.Item1.Length - 1))
                {
                    place = place[key];
                }
                place[item.Item1.Last()] = item.Item2;
                Assert.Equal(item.Item3, Assert.Throws<InvalidDataException>(() => BuildRules.NumberSettings(changed, "project.json")).Message);
            }
            foreach (Tuple<JToken, string> slope in new[] { Tuple.Create((JToken)"2.0", "a text"), Tuple.Create((JToken)false, "true or false") })
            {
                JObject rules = JObject.Parse(File.ReadAllText(RepoRules()));
                rules["levelProfiles"]["parsons2024Rev6"]["rampSlopePercent"]["C10"] = slope.Item1;
                Assert.Equal("rampSlopePercent C10 of the level profile holds " + slope.Item2 + ", a build takes it as a number (R337)",
                    Assert.Throws<InvalidDataException>(() => BuildRules.KeyRules(rules, null, "fronts")).Message);
            }
        }

        // F5 of the third review of PR 55: plots.csv and labels.csv are read as utf-8-sig, as book_rows and nbh_plots read them, so a byte that
        // is no UTF-8 such as 0xE9 is refused where File.ReadAllText took it as U+FFFD
        [Fact]
        public void R345_a_csv_of_the_data_book_with_a_byte_that_is_no_utf8_is_refused()
        {
            string path = Path.Combine(folder, "plots.csv");
            File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.ASCII.GetBytes("locationPlanNumber,baseplanLabel,typologyRev9\n1,101,DP"))
                .Concat(new byte[] { 0xE9, 0x0A }).ToArray());
            Assert.Equal("plots.csv is no UTF-8 text, the byte 0xE9 at 57 is none, as Python reads it with utf-8-sig (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(path)).Message);
            File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.ASCII.GetBytes("locationPlanNumber,baseplanLabel,typologyRev9\n1,101,DP2\n")).ToArray());
            Assert.Equal("DP2", CsvRows.Read(path)[0]["typologyRev9"]);
            Assert.Equal("locationPlanNumber", CsvRows.Read(path)[0].Keys.First());
        }

        // F6 of the third review of PR 55: the words of BuildRules.ShapeName, which plan_rules.shape_name gives the same, and the bytes of an
        // input read by WorkFiles.ReadBytes, never under a temporary name
        [Fact]
        public void R337_shape_name_names_each_kind_of_a_json_value_as_python_names_it()
        {
            Assert.Equal(new[] { "an object", "a list", "a text", "true or false", "a number", "a number", "null" },
                new[] { JTokenType.Object, JTokenType.Array, JTokenType.String, JTokenType.Boolean, JTokenType.Integer, JTokenType.Float, JTokenType.Null }.Select(BuildRules.ShapeName).ToArray());
        }

        [Fact]
        public void R347_read_bytes_gives_the_bytes_of_an_input_and_never_reads_a_temporary_name()
        {
            string path = Path.Combine(folder, "input.json");
            File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D });
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D }, WorkFiles.ReadBytes(path));
            File.WriteAllText(path + WorkFiles.Marker + "r1", "{}");
            Assert.Throws<InvalidOperationException>(() => WorkFiles.ReadBytes(path + WorkFiles.Marker + "r1"));
        }

        // G3 of the fourth review of PR 55, test_build_steps.AREA_LINES: the lines AddinConfig.ParityTolerance and the faces of the build give,
        // which plan_from_book.area_tolerance now gives the same
        [Fact]
        public void R345_the_areas_tolerance_is_held_as_python_holds_it()
        {
            var cases = new[]
            {
                Tuple.Create("true", "addin.parity.kinds.areas is true, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("NaN", "addin.parity.kinds.areas is NaN, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("Infinity", "addin.parity.kinds.areas is Infinity, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("-1", "addin.parity.kinds.areas is -1, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("\"x\"", "addin.parity.kinds.areas is \"x\", a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                // H2 of the fifth review of PR 55: a text and a nested value written as json.dumps writes them, ASCII with escapes and a float by repr
                Tuple.Create("\"\u00e9\"", "addin.parity.kinds.areas is \"\\u00e9\", a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("\"Infinity\"", "addin.parity.kinds.areas is \"Infinity\", a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("[-1E+20]", "addin.parity.kinds.areas is [-1e+20], a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("{\"a\": [\"\u00e9\\n\", 1.5, null, false]}", "addin.parity.kinds.areas is {\"a\":[\"\\u00e9\\n\",1.5,null,false]}, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("-1E400", "addin.parity.kinds.areas is -Infinity, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
            };
            string path = Path.Combine(folder, "project.json");
            foreach (Tuple<string, string> item in cases)
            {
                File.WriteAllText(path, "{\"addin\": {\"parity\": {\"kinds\": {\"areas\": " + item.Item1 + "}}}}");
                Assert.Equal(item.Item2, Assert.Throws<InvalidDataException>(() => AddinConfig.Load(path).ParityTolerance("areas")).Message);
            }
            File.WriteAllText(path, "{\"addin\": {\"parity\": {\"kinds\": {}}}}");
            Assert.Equal("addin.parity.kinds names no kind areas (R345)", Assert.Throws<InvalidDataException>(() => AddinConfig.Load(path).ParityTolerance("areas")).Message);
        }

        // G2 of the fourth review of PR 55, test_build_steps.CSV_KEPT and CSV_REFUSED: the CSV files read as csv of Python reads them with
        // newline="" and strict
        [Fact]
        public void R345_a_csv_of_the_data_book_is_read_as_python_reads_it_with_newline_and_strict()
        {
            const string head = "locationPlanNumber,baseplanLabel,typologyRev9";
            string path = Path.Combine(folder, "plots.csv");
            foreach (Tuple<string, string> item in new[] { Tuple.Create(head + "\r\n1,101,\"D\r\nP\"\r\n2,102,DP2\r\n", "D\r\nP"), Tuple.Create(head + "\n1,101,\"D\rP\"\n2,102,DP2\n", "D\rP") })
            {
                File.WriteAllText(path, item.Item1);
                List<Dictionary<string, string>> rows = CsvRows.Read(path);
                Assert.Equal(item.Item2, rows[0]["typologyRev9"]);
                Assert.Equal("DP2", rows[1]["typologyRev9"]);
            }
            File.WriteAllText(path, head + "\n1,101," + new string('D', 131072) + "\n");
            Assert.Equal(131072, CsvRows.Read(path)[0]["typologyRev9"].Length);
            var refused = new[]
            {
                Tuple.Create(head + "\n1,101,\"DP2\n2,102,DP2\n3,103,DP2\n", "unexpected end of data"),
                Tuple.Create(head + "\n1,101," + new string('D', 131073) + "\n", "field larger than field limit (131072)"),
                Tuple.Create(head + "\n1,101,\"DP\"2\n", "',' expected after '\"'"),
            };
            foreach (Tuple<string, string> item in refused)
            {
                File.WriteAllText(path, item.Item1);
                Assert.Equal("plots.csv is no CSV as Python reads it: " + item.Item2 + " (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(path)).Message);
            }
        }

        // G1 of the fourth review of PR 55: a plots.csv or labels.csv with no row, or a header that lacks a column the build reads, refused
        // with the line of Python, where an empty labels.csv gave a build of no plot reported done
        [Fact]
        public void R345_a_csv_of_the_data_book_with_no_row_or_a_header_that_lacks_a_column_is_refused()
        {
            string labels = Path.Combine(folder, "labels.csv"), plots = Path.Combine(folder, "plots.csv");
            foreach (byte[] content in new[] { new byte[0], new byte[] { 0xEF, 0xBB, 0xBF } })
            {
                File.WriteAllBytes(labels, content);
                Assert.Equal("labels.csv has no column kind, number in its header (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(labels, CsvRows.LabelsColumns)).Message);
            }
            File.WriteAllText(labels, "kind,number,nbh\n");
            Assert.Equal("labels.csv holds no row under its header (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(labels, CsvRows.LabelsColumns)).Message);
            File.WriteAllBytes(plots, new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.ASCII.GetBytes("locationPlanNumber,baseplanLabel\n1,101\n")).ToArray());
            Assert.Equal("plots.csv has no column locationPlanNumber in its header (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(plots, CsvRows.PlotsColumns)).Message);
            Assert.Single(CsvRows.Read(plots));
        }

        // G4 of the fourth review of PR 55: the helpers that name their rule ids now, each with a test that names the id
        [Fact]
        public void R354_py_text_and_py_repr_write_a_value_as_python_str_and_repr_write_it()
        {
            Assert.Equal("None", SiteClusters.PyText(null));
            Assert.Equal("a b", SiteClusters.PyText("a b"));
            Assert.Equal("['a', 1, 1.5, True, None, {'k': \"it's\"}]", SiteClusters.PyText(new JArray("a", 1, 1.5, true, null, new JObject { ["k"] = "it's" })));
            Assert.Equal("'a\\\\b\\n\\x00'", SiteClusters.TextRepr("a\\b\n\u0000"));
            Assert.Equal("'1e-07'", SiteClusters.TextRepr("1e-07"));
            Assert.Equal("1e-07", SiteClusters.PyRepr(1e-07));
        }

        // L6 of the fifth review of PR 55, test_build_steps.LINK_FIT_LINES: PyText of R337 in the lines of the link fits a build by keyRules
        // refuses, the value written as str() of Python writes it for %s
        [Fact]
        public void R337_py_text_writes_the_value_of_a_link_fit_line_as_python_str_writes_it()
        {
            var cases = new[]
            {
                Tuple.Create<JToken, JToken, string>("middle", new JArray(1.0, 2.0), "keyRules linkOrigin byCode DP1 holds anchor middle, a build takes start or end (R337)"),
                Tuple.Create<JToken, JToken, string>(null, new JArray(1.0, 2.0), "keyRules linkOrigin byCode DP1 holds anchor None, a build takes start or end (R337)"),
                Tuple.Create<JToken, JToken, string>("start", new JArray(1.5, "x"), "keyRules linkOrigin byCode DP1 holds offset [1.5, 'x'], a build takes a list of two numbers (R337)"),
                Tuple.Create<JToken, JToken, string>("end", new JObject { ["a"] = true }, "keyRules linkOrigin byCode DP1 holds offset {'a': True}, a build takes a list of two numbers (R337)"),
            };
            foreach (Tuple<JToken, JToken, string> item in cases)
            {
                var fit = new JObject { ["offset"] = item.Item2 };
                if (item.Item1 != null)
                {
                    fit["anchor"] = item.Item1;
                }
                var rules = new JObject { ["linkOrigin"] = new JObject { ["byCode"] = new JObject { ["DP1"] = fit } } };
                Assert.Equal(item.Item3, Assert.Throws<InvalidDataException>(() => BuildRules.CheckLinkFits(rules)).Message);
            }
        }

        [Fact]
        public void R345_first_invalid_names_the_first_byte_of_a_broken_utf8_sequence()
        {
            string path = Path.Combine(folder, "labels.csv");
            File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes("kind,number\nclientCode,1").Concat(new byte[] { 0xE2, 0x82, 0x0A, 0xC0, 0x80 }).ToArray());
            Assert.Equal("labels.csv is no UTF-8 text, the byte 0xE2 at 24 is none, as Python reads it with utf-8-sig (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(path)).Message);
            File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes("kind,number\nclientCode,1\n").Concat(new byte[] { 0xC0, 0x80 }).ToArray());
            Assert.Equal("labels.csv is no UTF-8 text, the byte 0xC0 at 25 is none, as Python reads it with utf-8-sig (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(path)).Message);
        }

        [Fact]
        public void R355_boxes_counts_the_boxes_of_the_tree_of_the_road_lines()
        {
            Func<int, RoadLines> lines = count => new RoadLines(new[] { (IReadOnlyList<double[]>)Enumerable.Range(0, count + 1).Select(index => new[] { index * 1.0, 0.0 }).ToList() });
            Assert.Equal(1, lines(8).Boxes);
            Assert.Equal(3, lines(9).Boxes);
            Assert.True(lines(200).Boxes < 2 * lines(200).Count);
            Assert.Equal(0, new RoadLines(new IReadOnlyList<double[]>[0]).Boxes);
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

        // R355 the fronts of test_build_steps.FRONT_FACES: a 12 m face 3 m off a road along y = -3, a corner face with a second road along
        // x = 45, and a face with no unit
        private static Dictionary<string, FrontItem> MadeFronts(string orientation, params string[] numbers)
        {
            JObject rules = JObject.Parse(File.ReadAllText(RepoRules()));
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            var faces = new Dictionary<string, BuildFace>
            {
                ["11"] = BuildFace.Of("11", Ring(0.0, 0.0, 12.0, 25.0)), ["12"] = BuildFace.Of("12", Ring(30.0, 0.0, 42.0, 25.0)), ["13"] = BuildFace.Of("13", Ring(60.0, 0.0, 72.0, 25.0)),
            };
            var rows = new Dictionary<string, Dictionary<string, string>>
            {
                ["11"] = new Dictionary<string, string> { ["typologyRev9"] = "DP2" }, ["12"] = new Dictionary<string, string> { ["typologyRev9"] = "C10" },
                ["13"] = new Dictionary<string, string> { ["typologyRev9"] = "" },
            };
            var roads = new RoadLines(new[] { new[] { new[] { -50.0, -3.0 }, new[] { 100.0, -3.0 } }, new[] { new[] { 45.0, -10.0 }, new[] { 45.0, 40.0 } } });
            var sales = new JObject { ["12"] = new JObject { ["Orientation"] = orientation == null ? JValue.CreateNull() : (JToken)orientation } };
            Func<double, double, KeyValuePair<double?, string>> level = (x, y) => x < 40.0 ? new KeyValuePair<double?, string>(10.0 + x / 100.0, "Top Surface-02") : new KeyValuePair<double?, string>(null, null);
            return Fronts.Of(numbers.Length > 0 ? numbers : new[] { "11", "12", "13", "14" }, rows, faces, roads, Fronts.Limits(rules, config), level, rules,
                (double)config["siteBuild"]["corner"]["roadEdgeM"], sales);
        }

        private static readonly string[] Tried = { "Top Surface-02" };

        [Fact]
        public void R355_the_front_of_a_plot_is_its_edge_nearest_a_curb_within_the_limits_with_its_frame_ramp_corners_and_road_levels()
        {
            Dictionary<string, Dictionary<string, object>> step = Fronts.Step(new[] { "11", "12", "13", "14" }, MadeFronts("WS"), Tried, 0.005);
            Dictionary<string, object> front = step["11"];
            Assert.Equal("nearestCurb", front["frontRule"]);
            Assert.Equal("DP2", front["unit"]);
            Assert.Equal(new[] { 0.0, 0.0 }, (double[])front["left"]);
            Assert.Equal(new[] { 12.0, 0.0 }, (double[])front["right"]);
            Assert.Equal(new[] { 0.0, 1.0 }, (double[])front["normal"]);
            Assert.Equal(new[] { 1.0, 0.0 }, (double[])front["rightHand"]);
            Assert.Equal(12.0, front["length"]);
            Assert.Equal(3.0, front["distance"]);
            Assert.Equal(false, front["tied"]);
            Assert.Equal(5.52, front["depth"]);
            Assert.Equal("0,0 12,0 12,5.52 0,5.52", Text((double[][])front["corners"]));
            Assert.Equal(10.0, front["roadLevelLeft"]);
            Assert.Equal(10.12, front["roadLevelRight"]);
            Assert.Equal("Top Surface-02", front["surfaceLeft"]);
            Assert.Equal("Top Surface-02", front["surfaceRight"]);
            Assert.Null(front["noRoadLevel"]);
            Assert.Equal(new[] { 60.0, 0.0 }, (double[])step["13"]["left"]);
            Assert.Null(step["13"]["corners"]);
            Assert.Null(step["13"]["noRoadLevel"]);
            Assert.Null(step["13"]["unit"]);
            Assert.Equal("nearestCurb", step["14"]["frontRule"]);
            Assert.All(step["14"].Where(item => item.Key != "frontRule"), item => Assert.Null(item.Value));
            Assert.Equal(16, step["14"].Count);
        }

        [Fact]
        public void R355_a_corner_plot_takes_the_road_edge_its_orientation_names_its_r122_frame_on_that_edge_and_no_front_with_no_orientation()
        {
            Dictionary<string, FrontItem> fronts = MadeFronts("WS");
            Assert.Equal("orientation", fronts["12"].FrontRule);
            Assert.Equal(new[] { 30.0, 0.0 }, fronts["12"].Frame.Left);
            Assert.Equal(new[] { 42.0, 0.0 }, fronts["12"].Frame.Right);
            Dictionary<string, object> step = Fronts.Step(new[] { "12" }, fronts, Tried, 0.005)["12"];
            Assert.Equal(5.5, step["depth"]);
            Assert.Null(step["roadLevelLeft"]);
            Assert.Equal("Top Surface-02", step["surfaceLeft"]);
            Assert.Null(step["surfaceRight"]);
            var missing = (JObject)step["noRoadLevel"];
            Assert.Equal("12", (string)missing["plot"]);
            Assert.Equal(new[] { "right" }, missing["corners"].ToObject<string[]>());
            Assert.Equal(new[] { "Top Surface-02" }, missing["tried"].ToObject<string[]>());
            Assert.Equal("no road surface covers the corner within snapM 0.005 m, so no road level is given (R344)", (string)missing["reason"]);

            FrontFrame frame = MadeFronts("SE")["12"].Frame;
            Assert.Equal(new[] { 42.0, 0.0 }, frame.Left);
            Assert.Equal(new[] { 42.0, 25.0 }, frame.Right);
            Assert.Equal(new[] { -1.0, 0.0 }, frame.N);
            Assert.Equal(new[] { 0.0, 1.0 }, frame.R);
            Assert.Equal(25.0, frame.Length);
            Assert.Equal(3.0, frame.Distance);
            Assert.False(frame.Tied);

            Dictionary<string, FrontItem> none = MadeFronts(null);
            Assert.Null(none["12"].Frame);
            Assert.Equal("a corner plot with no Orientation in the sales sheet", none["12"].FrontRule);
            var why = (JObject)Fronts.Step(new[] { "12" }, none, Tried, 0.005)["12"]["noRoadLevel"];
            Assert.Empty((JArray)why["corners"]);
            Assert.Empty((JArray)why["tried"]);
            Assert.Equal("no front frame, so no front corners to take a road level at", (string)why["reason"]);
        }

        [Fact]
        public void R355_the_config_names_the_fields_of_the_step_fronts_by_their_kind()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("frontRule:noGeometry: unit:noGeometry: left:points:3 right:points:3 normal:points:6 rightHand:points:6 length:points:3 distance:points:3 tied:noGeometry: depth:points:3 "
                + "corners:points:3 roadLevelLeft:levels:4 roadLevelRight:levels:4 surfaceLeft:noGeometry: surfaceRight:noGeometry: noRoadLevel:noGeometry:",
                string.Join(" ", config.StepFields(Fronts.StepName).Select(field => field.Name + ":" + field.Kind + ":" + field.Decimals)));
        }

        // B4 of the review of PR 55. ReadJson took every number through decimal, so 1.2345678901234567e-15, 3e-29 and -0.0 came out other
        // doubles than Python reads and a number past the decimal range was refused. The bits are those json.loads of Python 3.12.10 gives
        // for the same text, test_build_steps.JSON_TEXT
        internal const string JsonText = "{\"a\": 1.2345678901234567e-15, \"b\": 3e-29, \"c\": -0.0, \"d\": 1e30, \"e\": 8e28, \"f\": [0.1, 2264.253152, 1E+400, -1e-400, "
            + "4.9406564584124654e-324, 2.4703282292062328e-324, 1.7976931348623158e+308], \"g\": 12345678901234567890123, \"h\": -0, \"i\": 7.9228162514264337593543950336e28, "
            + "\"j\": \"x\\u00e9\\ud83d\\ude00\", \"a\": 1.2345678901234567e-15}";

        // P2 of the review of PR 55, B4 second attempt: each JSON input read as its Python reader reads it. Config, the rules profile and the
        // footprint blocks go through project_profile.read_object, utf-8-sig with a duplicate key refused, the other reads through json.loads,
        // a byte order mark refused and a key met again keeping the last value. An integer of more than 4300 digits is refused as int() of
        // Python refuses it, and float() of an integer past the long range is the nearest double, where (double)BigInteger cut it
        [Fact]
        public void R354_each_json_input_is_read_as_its_python_reader_reads_it_a_duplicate_key_a_byte_order_mark_and_long_integers()
        {
            string path = Path.Combine(folder, "project.json");
            File.WriteAllText(path, "{\"a\": {\"b\": 1, \"b\": 2}}");
            Assert.Equal("project.json has a duplicate key b", Assert.Throws<InvalidDataException>(() => PyMath.ReadJson(path, "config", true)).Message);
            Assert.Equal(2L, (long)PyMath.ReadJson(path, "analysis")["a"]["b"]);
            File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("{\"a\": 1}")).ToArray());
            Assert.Equal(1L, (long)PyMath.ReadJson(path, "config", true)["a"]);
            Assert.StartsWith("the input analysis is no JSON as Python reads it: Unexpected UTF-8 BOM (decode using utf-8-sig)",
                Assert.Throws<InvalidDataException>(() => PyMath.ReadJson(path, "analysis")).Message);
            File.WriteAllText(path, "[" + new string('7', 4300) + ", -" + new string('7', 4300) + "]");
            JArray wide = (JArray)PyMath.ReadJson(path, "analysis");
            Assert.Equal(4300, wide[0].ToString().Length);
            Assert.Equal(JTokenType.Integer, wide[1].Type);
            File.WriteAllText(path, "[-" + new string('7', 4301) + "]");
            Assert.Contains("Exceeds the limit (4300 digits) for integer string conversion: value has 4301 digits",
                Assert.Throws<InvalidDataException>(() => PyMath.ReadJson(path, "analysis")).Message);
            Assert.Equal(1180591620717411565568.0, BuildFace.Number(new JValue(System.Numerics.BigInteger.Pow(2, 70) + System.Numerics.BigInteger.Pow(2, 17) + 1)));
            Assert.Equal(-1180591620717411565568.0, BuildFace.Number(new JValue(-(System.Numerics.BigInteger.Pow(2, 70) + System.Numerics.BigInteger.Pow(2, 17) + 1))));
            Assert.Equal("int too large to convert to float", Assert.Throws<InvalidDataException>(() => BuildFace.Number(new JValue(System.Numerics.BigInteger.Pow(10, 400)))).Message);
        }

        [Fact]
        public void R354_every_number_of_a_json_input_is_the_double_or_integer_python_reads_from_its_text()
        {
            string path = Path.Combine(folder, "numbers.json");
            File.WriteAllText(path, JsonText);
            JObject read = (JObject)PyMath.ReadJson(path, "numbers");
            string Bits(JToken token) => BitConverter.DoubleToInt64Bits((double)token).ToString("x16");
            Assert.Equal(new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" }, read.Properties().Select(item => item.Name).ToArray());
            Assert.Equal("3cd63d70819bfe6b", Bits(read["a"]));
            Assert.Equal("3a0303c73efd0676", Bits(read["b"]));
            Assert.Equal("8000000000000000", Bits(read["c"]));
            Assert.Equal("46293e5939a08cea", Bits(read["d"]));
            Assert.Equal("45f027e72f1f1281", Bits(read["e"]));
            Assert.Equal(new[] { "3fb999999999999a", "40a1b0819d2391d5", "7ff0000000000000", "8000000000000000", "0000000000000001", "0000000000000001", "7fefffffffffffff" },
                ((JArray)read["f"]).Select(Bits).ToArray());
            Assert.Equal(JTokenType.Integer, read["g"].Type);
            Assert.Equal("12345678901234567890123", read["g"].ToString());
            Assert.Equal(JTokenType.Integer, read["h"].Type);
            Assert.Equal(0L, (long)read["h"]);
            Assert.Equal("45f0000000000000", Bits(read["i"]));
            Assert.Equal(JTokenType.Float, read["i"].Type);
            Assert.Equal("x\u00e9\ud83d\ude00", (string)read["j"]);
            // Python json.loads refuses each of these, so the build does
            foreach (string text in new[] { "{\"a\": 1,}", "{\"a\": 01}", "// a note\n{}", "{'a': 1}", "[1] 2", "{\"a\": \"x\ty\"}", "", "{\"a\": .5}", "{\"a\": 1.}", "{\"a\": +1}" })
            {
                File.WriteAllText(path, text);
                InvalidDataException refused = Assert.Throws<InvalidDataException>(() => PyMath.ReadJson(path, "numbers"));
                Assert.StartsWith("the input numbers is no JSON as Python reads it", refused.Message);
            }
            File.WriteAllText(path, "[NaN, Infinity, -Infinity, true, false, null]");
            JArray named = (JArray)PyMath.ReadJson(path, "numbers");
            Assert.True(double.IsNaN((double)named[0]));
            Assert.Equal(new object[] { double.PositiveInfinity, double.NegativeInfinity, true, false }, new object[] { (double)named[1], (double)named[2], (bool)named[3], (bool)named[4] });
            Assert.Equal(JTokenType.Null, named[5].Type);
        }

        // Python math.dist, the hypot of CPython 3.12 vector_norm, and the distance of GEOS from a point to a segment, as the values Python
        // 3.12.10 gave for these made up pairs, the last digit of each
        [Fact]
        public void R355_the_length_of_an_edge_and_the_distance_to_a_curb_are_those_python_and_shapely_give()
        {
            Assert.Equal(5.0, PyMath.Dist(new[] { 0.0, 0.0 }, new[] { 3.0, 4.0 }));
            Assert.Equal(0.0, PyMath.Dist(new[] { 7.0, 7.0 }, new[] { 7.0, 7.0 }));
            Assert.Equal(360.0, PyMath.Mod(-1e-20, 360.0));
            Assert.Equal(0.0, PyMath.Mod(-0.0, 360.0));
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(PyMath.Mod(-0.0, 360.0)));
            Assert.Equal(270.0, PyMath.Mod(-90.0, 360.0));
            Assert.Equal(180.0, PyMath.Bearing(new[] { 0.0, -1.0 }));
            Assert.Equal(90.0, PyMath.Bearing(new[] { 1.0, 0.0 }));
            Assert.Equal(3.0, PyMath.SegmentDistance(new[] { 6.0, 0.0 }, new[] { -50.0, -3.0 }, new[] { 100.0, -3.0 }));
            Assert.Equal(5.0, PyMath.SegmentDistance(new[] { 3.0, 4.0 }, new[] { 0.0, 0.0 }, new[] { 0.0, 0.0 }));
        }

        // A made up LandXML of one surface of two triangles over the square from (x0, y0) to (x1, y1) at the level z, in meter
        private string Surface(string name, double x0, double y0, double x1, double y1, double z)
        {
            Func<double, double, string> point = (x, y) => y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + z.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string path = Path.Combine(folder, name + ".xml");
            File.WriteAllText(path, "<?xml version=\"1.0\"?><LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\"><Units><Metric linearUnit=\"meter\" areaUnit=\"squareMeter\"/></Units>"
                + "<Surfaces><Surface name=\"" + name + "\"><Definition surfType=\"TIN\"><Pnts><P id=\"1\">" + point(x0, y0) + "</P><P id=\"2\">" + point(x1, y0) + "</P><P id=\"3\">" + point(x1, y1)
                + "</P><P id=\"4\">" + point(x0, y1) + "</P></Pnts><Faces><F>1 2 3</F><F>1 3 4</F></Faces></Definition></Surface></Surfaces></LandXML>");
            return path;
        }

        // The request of a worker build of the step fronts on made up reads of the faces of test_build_steps.FRONT_FACES, the two roads, the
        // sales row of the corner face and the six road surfaces config names, the first covering every corner west of x = 40
        private WorkerRequest FrontRequest(string runId, string orientation = "SE")
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            var plots = new JArray(Shape("111", Ring(0.0, 0.0, 12.0, 25.0)), Shape("112", Ring(30.0, 0.0, 42.0, 25.0)), Shape("113", Ring(60.0, 0.0, 72.0, 25.0)));
            string baseplan = ReadFile("read_baseplan.json", "baseplan", (string)config["drawings"]["baseplan"], new JObject { ["plots"] = plots });
            string layer = (string)config["analysis"]["roadEdgeLayers"][0];
            var pieces = new JArray(new JObject { ["kind"] = "polyline", ["layer"] = layer, ["pts"] = new JArray(new JArray(-50.0, -3.0), new JArray(100.0, -3.0)), ["closed"] = false },
                new JObject { ["kind"] = "line", ["layer"] = layer, ["pts"] = new JArray(new JArray(45.0, -10.0), new JArray(45.0, 40.0)) });
            string curbs = ReadFile("read_curbs.json", "curbs", (string)config["drawings"]["roadLayout"], new JObject { ["pieces"] = pieces });
            string workbookName = Path.GetFileName((string)config["dataBook"]["rev9"]["workbooks"][(string)config["dataBook"]["locationPlan"]["sales"]["workbook"]]);
            string salesFile = Path.Combine(folder, workbookName);
            File.WriteAllText(salesFile, "a made up sales workbook");
            var workbooks = new JObject
            {
                ["sales"] = new JObject { ["112"] = new JObject { ["Orientation"] = orientation } },
                [RunStamp.Key] = new JObject { ["runId"] = "r0", ["inputs"] = new JArray(new JObject { ["role"] = "sales", ["path"] = salesFile, ["sha256"] = FileHash.Sha256(salesFile) }) },
            };
            string workbooksPath = Path.Combine(folder, "read_workbooks.json");
            File.WriteAllText(workbooksPath, workbooks.ToString());
            string data = Path.Combine(folder, "plots.csv");
            File.WriteAllText(data, "locationPlanNumber,baseplanLabel,typologyRev9\n111,111,DP2\n112,112,C10\n113,113,\n");
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, "kind,number,nbh\nclientCode,111,012\nclientCode,112,012\nclientCode,113,012\nclientCode,114,012\n");
            var inputs = new List<WorkerFile>
            {
                Input("config", WorkerRunTests.RepoConfig()), Input("rules", RepoRules()), Input("plotsCsv", data), Input("labels", labels), Input("baseplanRead", baseplan),
                Input("curbsRead", curbs), Input("workbooksRead", workbooksPath),
            };
            int place = 0;
            foreach (string name in config["siteBuild"]["roadSurfaces"].Select(item => (string)item))
            {
                inputs.Add(Input(WorkerRunner.RoadSurfaceRole + name, place++ == 0 ? Surface(name, -60.0, -20.0, 40.0, 30.0, 10.0) : Surface(name, 1000.0, 1000.0, 1010.0, 1010.0, 99.0)));
            }
            return new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" }, Inputs = inputs,
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, Steps = new List<string> { "fronts" }, StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
        }

        [Fact]
        public void R355_the_worker_build_writes_the_step_fronts_from_the_reads_and_refuses_a_sales_read_of_another_workbook_or_road_surfaces_not_of_the_profile()
        {
            Assert.Equal(0, WorkerRunner.Run(FrontRequest("f1").Write(folder), WorkerCommands.Default(), () => 9000.0));
            JObject file = RunStamp.Read(Path.Combine(folder, "steps", "step_fronts_csharp.json"));
            Assert.Equal(new[] { "111", "112", "113", "114" }, ((JObject)file["plots"]).Properties().Select(item => item.Name).ToArray());
            JToken plain = file["plots"]["111"]["written"];
            Assert.Equal(10.0, (double)plain["roadLevelLeft"]);
            Assert.Equal(10.0, (double)plain["roadLevelRight"]);
            Assert.Equal("Top Surface-02", (string)plain["surfaceLeft"]);
            JToken corner = file["plots"]["112"]["written"];
            Assert.Equal("orientation", (string)corner["frontRule"]);
            Assert.Equal("42.0 0.0", string.Join(" ", corner["left"].Select(item => ((double)item).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))));
            Assert.Equal(new[] { "left", "right" }, corner["noRoadLevel"]["corners"].ToObject<string[]>());
            Assert.Equal(6, corner["noRoadLevel"]["tried"].Count());

            WorkerRequest other = FrontRequest("f2");
            File.Move(Path.Combine(folder, Path.GetFileName((string)JObject.Parse(File.ReadAllText(other.Inputs[6].Path))[RunStamp.Key]["inputs"][0]["path"])), Path.Combine(folder, "another.xlsx"));
            JObject workbooks = JObject.Parse(File.ReadAllText(other.Inputs[6].Path));
            workbooks[RunStamp.Key]["inputs"][0]["path"] = Path.Combine(folder, "another.xlsx");
            File.WriteAllText(other.Inputs[6].Path, workbooks.ToString());
            other.Inputs[6] = Input("workbooksRead", other.Inputs[6].Path);
            Assert.Equal(2, WorkerRunner.Run(other.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the input workbooksRead is the read of the sales workbook another.xlsx", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "f2")).Message);

            WorkerRequest missing = FrontRequest("f3");
            missing.Inputs.RemoveAt(missing.Inputs.Count - 1);
            Assert.Equal(2, WorkerRunner.Run(missing.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the road surfaces of the request are not those of the level profile in their order", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "f3")).Message);

            WorkerRequest none = FrontRequest("f4");
            none.Inputs[5] = Input("curbsRead", ReadFile("read_curbs.json", "curbs", "a road layout.dwg", new JObject { ["pieces"] = new JArray() }));
            Assert.Equal(2, WorkerRunner.Run(none.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the road layout read holds no line of the curb layers analysis.roadEdgeLayers names, the front of a plot is the edge nearest a curb (R355)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "f4")).Message);
        }

        // R357 the hands of test_build_steps.hand_build: a row of six 12 m faces along a road at y = -3, plot 1 a VD2 villa with a side
        // road at x = -3, plots 2 and 3 a DP2 pair of the footprint, plots 4 to 6 DP2 units in no cluster, the hand tables made up
        private const string Dp2File = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt";
        private const string Vd2File = "RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt";

        private static JObject HandRules()
        {
            JObject rules = JObject.Parse(File.ReadAllText(RepoRules()));
            JObject key = (JObject)rules["levelProfiles"]["parsons2024Rev6"]["keyRules"];
            key["hand villa"] = new JObject { ["rule"] = "blockMirrorRoadSide", ["byFile"] = new JObject { [Vd2File] = new JObject { ["no left"] = true, ["no none"] = false } } };
            key["hand cluster"] = new JObject { ["rule"] = "clusterSide", ["byFile"] = new JObject { [Dp2File] = new JObject { ["left"] = false, ["right"] = true } } };
            key["hand alone"] = new JObject { ["rule"] = "rowPairSideOrBlockMirrorOpenEnd", ["byFile"] = new JObject { [Dp2File] = new JObject { ["pair left"] = false, ["pair right"] = true } } };
            return rules;
        }

        private static Dictionary<string, Dictionary<string, object>> HandStep()
        {
            return HandSteps().Item1;
        }

        // The steps hands and linkFrame of the made up row, the second with the hand of the first
        private static Tuple<Dictionary<string, Dictionary<string, object>>, Dictionary<string, Dictionary<string, object>>> HandSteps(Dictionary<string, string> codes = null,
            Func<Dictionary<string, Dictionary<string, object>>, Dictionary<string, Dictionary<string, object>>> handsOf = null, Dictionary<string, string> filesOf = null,
            Action<JObject> contextConfig = null, Action<JObject> stepConfig = null, Action<JObject> linkKey = null, Action<JObject> handKey = null,
            Action<HandContext> seeContext = null)
        {
            JObject rules = HandRules();
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            contextConfig?.Invoke(config);
            var numbers = new List<string> { "1", "2", "3", "4", "5", "6" };
            var faces = numbers.ToDictionary(number => number, number => BuildFace.Of(number, Ring((int.Parse(number) - 1) * 12.0, 0.0, int.Parse(number) * 12.0, 25.0)));
            var rows = numbers.ToDictionary(number => number, number => new Dictionary<string, string>
            {
                ["locationPlanNumber"] = number, ["typologyRev9"] = number == "1" ? "VD2" : "DP2", ["footprintMirrored"] = "no", ["zone"] = "Zone 2",
            });
            var roads = new RoadLines(new[] { new[] { new[] { -50.0, -3.0 }, new[] { 100.0, -3.0 } }, new[] { new[] { -3.0, -10.0 }, new[] { -3.0, 40.0 } } });
            var sales = new JObject { ["1"] = new JObject { ["Orientation"] = "WS" } };
            var clusters = new List<SiteCluster> { new SiteCluster { Code = "Duplex_DP2", Plots = new List<string> { "2", "3" }, Source = "footprint" } };
            Dictionary<string, FrontItem> fronts = Fronts.Of(numbers, rows, faces, roads, Fronts.Limits(rules, config), (x, y) => new KeyValuePair<double?, string>(null, null), rules,
                (double)config["siteBuild"]["corner"]["roadEdgeM"], sales);
            var files = new Dictionary<string, string> { ["DP2"] = Dp2File, ["VD2"] = Vd2File };
            JObject key = BuildRules.KeyRules(rules, null, "hands");
            handKey?.Invoke(key);
            HandContext context = Hands.Context(numbers, rows, faces, clusters, fronts.ToDictionary(item => item.Key, item => item.Value.Frame), roads, rules, config, key, files);
            seeContext?.Invoke(context);
            stepConfig?.Invoke(config);
            Dictionary<string, Dictionary<string, object>> hands = Hands.Step(numbers.Concat(new[] { "7" }), rows, context, key, files, rules, config);
            hands = handsOf == null ? hands : handsOf(hands);
            Dictionary<string, Dictionary<string, string>> linkRows = codes == null ? rows : codes.ToDictionary(item => item.Key, item => new Dictionary<string, string> { ["typologyRev9"] = item.Value });
            if (codes != null)
            {
                fronts["6"] = new FrontItem { Face = null, Frame = null, Unit = "DP2", FrontRule = "nearestCurb" };
            }
            linkKey?.Invoke(key);
            return Tuple.Create(hands, LinkFrames.Step(numbers.Concat(new[] { "7" }), linkRows, fronts, context.Roles, hands, key, filesOf ?? files, rules));
        }

        // B3 of the review of PR 55, MAJORITY_LINES of test_build_steps.py: a missing majority.pairKinds, kindByPrefix or sideShare, or a
        // keyRules.linkOrigin with no byCode, was read as empty here where Python raised KeyError, so both refuse it with one line
        [Fact]
        public void R357_a_missing_majority_list_or_link_fit_is_refused_with_the_line_of_python_and_never_read_as_empty()
        {
            var lines = new[]
            {
                Tuple.Create("pairKinds", "config holds no majority.pairKinds list of texts, the unit kinds that pair in a row of units (R357)"),
                Tuple.Create("kindByPrefix", "config holds no majority.kindByPrefix object of texts, the kind of a unit code the rules profile gives none (R357)"),
                Tuple.Create("sideShare", "config holds no majority.sideShare number, the share of the depth a side neighbour shares (R357)"),
            };
            foreach (Tuple<string, string> line in lines)
            {
                Assert.Equal(line.Item2, Assert.Throws<InvalidDataException>(() => HandSteps(contextConfig: config => ((JObject)config["majority"]).Remove(line.Item1))).Message);
                Assert.Equal(line.Item2, Assert.Throws<InvalidDataException>(() => HandSteps(stepConfig: config => ((JObject)config["majority"]).Remove(line.Item1))).Message);
            }
            Assert.Equal(lines[0].Item2, Assert.Throws<InvalidDataException>(() => HandSteps(contextConfig: config => config["majority"]["pairKinds"] = new JArray(1))).Message);
            Assert.Equal(lines[1].Item2, Assert.Throws<InvalidDataException>(() => HandSteps(contextConfig: config => config["majority"]["kindByPrefix"] = new JObject { ["DP"] = 2 })).Message);
            Assert.Equal(lines[2].Item2, Assert.Throws<InvalidDataException>(() => HandSteps(contextConfig: config => config["majority"]["sideShare"] = true)).Message);
            Assert.Equal("config holds no majority block, the settings of the majority rule a build by keyRules reads (R357)",
                Assert.Throws<InvalidDataException>(() => HandSteps(contextConfig: config => config.Remove("majority"))).Message);
            Assert.Equal("keyRules linkOrigin codes plotFrameRefit, a build gives only plotFrameRefit with byCode (R337)",
                Assert.Throws<InvalidDataException>(() => HandSteps(linkKey: key => ((JObject)key["linkOrigin"]).Remove("byCode"))).Message);
        }

        // F1 of the third review of PR 55, test_build_steps.FIT_LINES: a fit with no anchor put the link at the end corner here, a list or null
        // left the plot not placed in silence, a bad fit of a code out of the scope passed and an offset of a text was taken as a number, where
        // Python stops. Every fit of the table is held, by CheckKeyRules and by LinkFrames.Step, with the line of Python
        [Fact]
        public void R356_a_link_fit_of_the_wrong_shape_or_kind_is_refused_with_the_line_of_python()
        {
            JObject key = (JObject)JObject.Parse(File.ReadAllText(RepoRules()))["levelProfiles"]["parsons2024Rev6"]["keyRules"];
            var cases = new[]
            {
                Tuple.Create("DP2", (JToken)new JObject { ["offset"] = new JArray(1.0, 2.0) }, "keyRules linkOrigin byCode DP2 holds anchor None, a build takes start or end (R337)"),
                Tuple.Create("DP2", (JToken)new JArray(), "keyRules linkOrigin holds byCode DP2 as a list, a build takes it as an object of anchor and offset (R337)"),
                Tuple.Create("DP2", (JToken)JValue.CreateNull(), "keyRules linkOrigin holds byCode DP2 as null, a build takes it as an object of anchor and offset (R337)"),
                Tuple.Create("ZZ9", (JToken)new JObject { ["anchor"] = "middle", ["offset"] = new JArray(1.0, 2.0) }, "keyRules linkOrigin byCode ZZ9 holds anchor middle, a build takes start or end (R337)"),
                Tuple.Create("DP2", (JToken)new JObject { ["anchor"] = "start", ["offset"] = new JArray("1.0", 2.0) }, "keyRules linkOrigin byCode DP2 holds offset ['1.0', 2.0], a build takes a list of two numbers (R337)"),
                Tuple.Create("DP2", (JToken)new JObject { ["anchor"] = "start", ["offset"] = new JArray(1.0) }, "keyRules linkOrigin byCode DP2 holds offset [1.0], a build takes a list of two numbers (R337)"),
                Tuple.Create("DP2", (JToken)new JObject { ["anchor"] = "end", ["offset"] = new JArray(true, 1) }, "keyRules linkOrigin byCode DP2 holds offset [True, 1], a build takes a list of two numbers (R337)"),
            };
            foreach (Tuple<string, JToken, string> item in cases)
            {
                var changed = (JObject)key.DeepClone();
                changed["linkOrigin"]["byCode"][item.Item1] = item.Item2;
                Assert.Equal(item.Item3, Assert.Throws<InvalidDataException>(() => BuildRules.CheckKeyRules(changed)).Message);
                Assert.Equal(item.Item3, Assert.Throws<InvalidDataException>(() => HandSteps(linkKey: linked => linked["linkOrigin"]["byCode"][item.Item1] = item.Item2.DeepClone())).Message);
            }
        }

        // F2 of the third review of PR 55, test_build_steps.LEAF_LINES: a hand of a byFile table that is no true or false, taken as truthy,
        // so the text false mirrored the link, refused with the line of Python
        [Fact]
        public void R357_a_hand_of_a_byfile_table_that_is_no_true_or_false_is_refused_with_the_line_of_python()
        {
            JObject key = (JObject)JObject.Parse(File.ReadAllText(RepoRules()))["levelProfiles"]["parsons2024Rev6"]["keyRules"];
            string name = ((JObject)key["hand villa"]["byFile"]).Properties().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal).First();
            string factor = ((JObject)key["hand villa"]["byFile"][name]).Properties().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal).First();
            var cases = new[]
            {
                Tuple.Create((JToken)"false", "a text"), Tuple.Create((JToken)1, "a number"), Tuple.Create((JToken)JValue.CreateNull(), "null"), Tuple.Create((JToken)new JArray(true), "a list"),
            };
            foreach (Tuple<JToken, string> item in cases)
            {
                var changed = (JObject)key.DeepClone();
                changed["hand villa"]["byFile"][name][factor] = item.Item1;
                Assert.Equal("keyRules hand villa holds byFile " + name + " " + factor + " as " + item.Item2 + ", a build takes true or false (R337)",
                    Assert.Throws<InvalidDataException>(() => BuildRules.CheckKeyRules(changed)).Message);
            }
        }

        [Fact]
        public void R356_the_link_of_a_plot_is_the_fit_of_its_code_from_the_anchor_corner_of_its_front_in_the_link_axes_of_its_hand()
        {
            Dictionary<string, Dictionary<string, object>> step = HandSteps().Item2;
            JObject fit = (JObject)JObject.Parse(File.ReadAllText(RepoRules()))["levelProfiles"]["parsons2024Rev6"]["keyRules"]["linkOrigin"]["byCode"]["DP2"];
            double along = (double)fit["offset"][0], into = (double)fit["offset"][1];
            Assert.Equal(Dp2File + "|DP2|" + (string)fit["anchor"], step["4"]["prototypeFile"] + "|" + step["4"]["fitUnit"] + "|" + step["4"]["anchor"]);
            Assert.Equal(new[] { along, into }, (double[])step["4"]["offset"]);
            Assert.True((bool)(JToken)step["4"]["mirrored"]);
            Assert.Equal(new[] { 36.0 - along, into }, (double[])step["4"]["insert"]);
            Assert.Equal(180.0, ((StepValue)step["4"]["rotationDeg"]).Raw);
            Assert.False((bool)(JToken)step["5"]["mirrored"]);
            Assert.Equal(new[] { 60.0 + along, into }, (double[])step["5"]["insert"]);
            Assert.Equal(0.0, ((StepValue)step["5"]["rotationDeg"]).Raw);
            Assert.Empty((JArray)step["4"]["lacks"]);
            Assert.All(step["7"].Where(item => item.Key != "lacks"), item => Assert.Null(item.Value));
            Assert.Empty((JArray)step["7"]["lacks"]);
            Assert.Equal(8, step["7"].Count);
        }

        [Fact]
        public void R356_a_plot_with_no_hand_no_fit_no_face_or_no_file_lacks_its_link_with_why()
        {
            var codes = new Dictionary<string, string> { ["4"] = "DP2", ["5"] = "XX9", ["6"] = "DP2" };
            Dictionary<string, Dictionary<string, object>> step = HandSteps(codes, hands =>
            {
                hands["4"]["mirrored"] = null;
                return hands;
            }, new Dictionary<string, string> { ["VD2"] = Vd2File }).Item2;
            Func<string, string> lacks = number => ((JArray)step[number]["lacks"]).ToString(Newtonsoft.Json.Formatting.None);
            Assert.Equal("[{'key':'prototypeFile','why':'no single prototype file in the folder carries the unit DP2'},{'key':'mirrored','why':'no rule gives the mirror flag of DP2 in no cluster'}]".Replace('\'', '"'), lacks("4"));
            Assert.EndsWith("{'key':'insert','why':'plotFrame holds no anchor and offset for XX9'}]".Replace('\'', '"'), lacks("5"));
            Assert.Null(step["5"]["fitUnit"]);
            Assert.EndsWith("{'key':'transform','why':'no baseplan face carries the label of the plot'}]".Replace('\'', '"'), lacks("6"));
            Assert.Null(step["6"]["insert"]);
        }

        [Fact]
        public void R356_the_rotation_of_a_link_is_written_as_the_plan_writes_it_and_one_that_rounds_to_360_as_0()
        {
            Assert.Equal(0.0, LinkFrames.PlanRotation(359.9996));
            Assert.Equal(359.999, LinkFrames.PlanRotation(359.9994));
            Assert.Equal(0.0, LinkFrames.PlanRotation(-0.0));
            var fields = new List<ParityField> { new ParityField { Name = "rotationDeg", Kind = "angles", Decimals = 3 }, new ParityField { Name = "insert", Kind = "points", Decimals = 3 } };
            var plots = new Dictionary<string, Dictionary<string, object>>
            {
                ["1"] = new Dictionary<string, object> { ["rotationDeg"] = new StepValue { Raw = 359.9996, Written = LinkFrames.PlanRotation(359.9996) }, ["insert"] = new[] { 1.00049, 2.0 } },
            };
            JObject file = StepFile.Build("linkFrame", "csharp", "project", plots, fields);
            Assert.Equal(359.9996, (double)file["plots"]["1"]["raw"]["rotationDeg"]);
            Assert.Equal(0.0, (double)file["plots"]["1"]["written"]["rotationDeg"]);
            Assert.Equal(1.0, (double)file["plots"]["1"]["written"]["insert"][0]);
        }

        [Fact]
        public void R356_the_config_names_the_fields_of_the_step_link_frame_by_their_kind()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("prototypeFile:noGeometry: fitUnit:noGeometry: anchor:noGeometry: offset:points:3 mirrored:noGeometry: insert:points:3 rotationDeg:angles:3 lacks:noGeometry:",
                string.Join(" ", config.StepFields(LinkFrames.StepName).Select(field => field.Name + ":" + field.Kind + ":" + field.Decimals)));
        }

        [Fact]
        public void R357_the_hand_of_a_plot_is_the_table_of_its_file_read_by_the_factor_of_its_unit_class()
        {
            Dictionary<string, Dictionary<string, object>> step = HandStep();
            Dictionary<string, object> villa = step["1"];
            Assert.Equal("VD2|villa|villa|villa", villa["unit"] + "|" + villa["kind"] + "|" + villa["pairKind"] + "|" + villa["unitClass"]);
            Assert.Null(villa["cluster"]);
            Assert.Empty((List<string>)villa["left"]);
            Assert.Equal(new[] { "2" }, (List<string>)villa["right"]);
            Assert.Equal("left|left|no|no left|no left|alone no left|blockMirrorRoadSide|no left|" + Vd2File,
                string.Join("|", new[] { "openEnd", "roadSide", "blockMirror", "blockMirrorRoadSide", "blockMirrorOpenEnd", "rowPairSideOrBlockMirrorOpenEnd", "handRule", "factor", "linkFile" }.Select(name => villa[name])));
            Assert.Null(villa["rowPair"]);
            Assert.True((bool)(JToken)villa["mirrored"]);
            Assert.Equal(new[] { "2", "3" }, (List<string>)step["2"]["cluster"]);
            Assert.Equal("cluster|left|pair|1|2|Duplex_DP2", string.Join("|", new[] { "unitClass", "clusterSide", "clusterRole", "clusterIndex", "clusterUnits", "clusterCode" }.Select(name => step["2"][name])));
            Assert.False((bool)(JToken)step["2"]["mirrored"]);
            Assert.Equal("right|2|right", step["3"]["clusterSide"] + "|" + step["3"]["clusterIndex"] + "|" + step["3"]["factor"]);
            Assert.True((bool)(JToken)step["3"]["mirrored"]);
            Assert.Equal("0 2 3 right|1 1 3 left|2 0 3 right", string.Join("|", new[] { "4", "5", "6" }.Select(number => (JObject)step[number]["rowPair"])
                .Select(pair => pair["left"] + " " + pair["right"] + " " + pair["length"] + " " + pair["side"])));
            Assert.Equal(new[] { true, false, true }, new[] { "4", "5", "6" }.Select(number => (bool)(JToken)step[number]["mirrored"]).ToArray());
            Assert.Equal("right", step["6"]["openEnd"]);
            Assert.Equal("none", step["4"]["roadSide"]);
            Assert.Equal(new[] { "3" }, (List<string>)step["4"]["left"]);
            Assert.Null(step["7"]["unit"]);
            Assert.Null(step["7"]["mirrored"]);
            Assert.Null(step["7"]["handRule"]);
            Assert.Equal("alone", step["7"]["unitClass"]);
            Assert.Null(step["7"]["handTable"]);
            Assert.Equal("villa cluster alone", villa["handTable"] + " " + step["2"]["handTable"] + " " + step["4"]["handTable"]);
            Assert.Equal(27, step["7"].Count);
        }

        // test_build_steps.no_cluster_table, Q098: the file of DP2 holds no table of hand cluster, as no footprint cluster of the 7 keys holds a
        // DP2 unit, and its table of hand alone holds the factor plots 2 and 3 of the made up row take
        private static void NoClusterTable(JObject key)
        {
            ((JObject)key["hand cluster"]["byFile"]).Remove(Dp2File);
            key["hand alone"]["byFile"][Dp2File]["alone no none"] = true;
        }

        [Fact]
        public void R357_Q098_a_cluster_unit_whose_file_has_no_table_of_hand_cluster_takes_the_table_of_hand_alone()
        {
            Dictionary<string, Dictionary<string, object>> step = HandSteps(handKey: NoClusterTable).Item1;
            // Since Q105 their factor is the pair of the row walked as if no cluster held them
            foreach (Tuple<string, string, bool> item in new[] { Tuple.Create("2", "pair right", true), Tuple.Create("3", "pair left", false) })
            {
                Assert.Equal("cluster alone rowPairSideOrBlockMirrorOpenEnd " + item.Item2, string.Join(" ", new[] { "unitClass", "handTable", "handRule", "factor" }.Select(name => step[item.Item1][name])));
                Assert.Equal(item.Item3, (bool)(JToken)step[item.Item1]["mirrored"]);
            }
            Assert.Equal("alone villa", step["4"]["handTable"] + " " + step["1"]["handTable"]);
        }

        // test_r357_q105_a_cluster_unit_of_the_hand_alone_table_takes_the_row_pair_of_no_cluster_for_its_hand_only: plots 2 and 3 take the table
        // of hand alone (Q098) and walk their row pair as if no cluster held them, the row 2 to 6 from its right end, 2 pair right and 3 pair
        // left, while the row pairs of 4 to 6 and the row pairs the row partners of the levels are made of stay those of the plain walk
        [Fact]
        public void R357_Q105_a_cluster_unit_of_the_hand_alone_table_takes_the_row_pair_of_no_cluster_for_its_hand_only()
        {
            HandContext context = null;
            Dictionary<string, Dictionary<string, object>> step = HandSteps(handKey: NoClusterTable, seeContext: found => context = found).Item1;
            Assert.Equal("{\"left\":0,\"right\":4,\"length\":5,\"side\":\"right\"}", ((JObject)step["2"]["rowPair"]).ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("{\"left\":1,\"right\":3,\"length\":5,\"side\":\"left\"}", ((JObject)step["3"]["rowPair"]).ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("pair right True pair left False", step["2"]["factor"] + " " + (bool)(JToken)step["2"]["mirrored"] + " " + step["3"]["factor"] + " " + (bool)(JToken)step["3"]["mirrored"]);
            Assert.Equal("right left right", string.Join(" ", new[] { "4", "5", "6" }.Select(number => (string)((JObject)step[number]["rowPair"])["side"])));
            HandContext plain = null;
            Dictionary<string, Dictionary<string, object>> before = HandSteps(seeContext: found => plain = found).Item1;
            Assert.Null(before["2"]["rowPair"]);
            Assert.Equal(((JObject)before["4"]["rowPair"]).ToString(), ((JObject)step["4"]["rowPair"]).ToString());
            // The row partners of the levels of R359 from the plain walk, with or without the fall back of Q098
            Assert.False(context.RowPairs.ContainsKey("2") || context.RowPairs.ContainsKey("3"));
            Assert.Equal(string.Join(" ", plain.RowPairs.OrderBy(item => item.Key).Select(item => item.Key + item.Value.Side)),
                string.Join(" ", context.RowPairs.OrderBy(item => item.Key).Select(item => item.Key + item.Value.Side)));
        }

        [Fact]
        public void R357_Q098_the_table_of_a_unit_is_that_of_its_class_and_a_cluster_unit_falls_back_only_to_a_table_of_hand_alone_of_its_file()
        {
            JObject key = JObject.Parse("{\"hand villa\": {\"rule\": \"blockMirrorRoadSide\", \"byFile\": {\"" + Vd2File + "\": {}}}, \"hand cluster\": {\"rule\": \"clusterSide\", "
                + "\"byFile\": {\"" + Dp2File + "\": {}}}, \"hand alone\": {\"rule\": \"rowPairSideOrBlockMirrorOpenEnd\", \"byFile\": {\"" + Dp2File + "\": {}, \"alone.rvt\": {}}}}");
            Assert.Equal("cluster", Hands.HandTable(key, "cluster", Dp2File));
            Assert.Equal("alone", Hands.HandTable(key, "cluster", "alone.rvt"));
            Assert.Equal("cluster cluster", Hands.HandTable(key, "cluster", "other.rvt") + " " + Hands.HandTable(key, "cluster", null));
            Assert.Equal("villa alone", Hands.HandTable(key, "villa", "alone.rvt") + " " + Hands.HandTable(key, "alone", "other.rvt"));
        }

        // The length of the boundary of a face within the buffer of another at 0.05 m, the buffer as GEOS builds it with 16 segments a
        // quarter, against what shapely gave for these made up faces: side by side, a back neighbour, a neighbour of 12.45 m and a fan
        [Fact]
        public void R357_the_band_length_of_a_face_is_the_one_shapely_gives_with_its_buffer_of_16_segments_a_quarter()
        {
            BuildFace a = BuildFace.Of("1", Ring(0, 0, 12, 25));
            Assert.Equal(25.1, Hands.BandLength(a, BuildFace.Of("2", Ring(12, 0, 24, 25)), 0.05), 9);
            Assert.Equal(12.100000000000001, Hands.BandLength(a, BuildFace.Of("3", Ring(0, 25, 12, 37.4)), 0.05), 9);
            Assert.Equal(12.55, Hands.BandLength(a, BuildFace.Of("4", Ring(12, 0, 24, 12.45)), 0.05), 9);
            Assert.Equal(1.059800209322752, Hands.BandLength(BuildFace.Of("5", Ring(0, 0, 12, 40)), BuildFace.Of("6", Ring(12.04, 30, 14, 31)), 0.05), 9);
        }

        [Fact]
        public void R357_a_side_neighbour_shares_side_share_of_the_depth_by_its_band_and_takes_its_side_by_the_centroids_along_the_front()
        {
            var faces = new Dictionary<string, BuildFace>
            {
                ["1"] = BuildFace.Of("1", Ring(0, 0, 12, 25)), ["2"] = BuildFace.Of("2", Ring(12, 0, 24, 12.45)), ["3"] = BuildFace.Of("3", Ring(-12, 0, 0, 12.39)),
            };
            var frames = new Dictionary<string, FrontFrame> { ["1"] = new FrontFrame { N = new[] { 0.0, 1.0 }, R = new[] { 1.0, 0.0 }, Left = new[] { 0.0, 0.0 }, Right = new[] { 12.0, 0.0 } } };
            Dictionary<string, Sides> found = Hands.SideNeighbours(new[] { "1", "2", "3" }, faces, frames, 0.5, 25.0);
            Assert.Equal(new[] { "2" }, found["1"].Right);
            Assert.Empty(found["1"].Left);
            Assert.False(found["2"].Framed);
            Assert.Empty(found["2"].Left);
        }

        [Fact]
        public void R357_the_prototype_file_of_a_code_is_the_one_of_the_folder_else_that_of_its_keo_file_and_a_code_of_two_files_gets_none()
        {
            JObject rules = JObject.Parse(File.ReadAllText(RepoRules()));
            Dictionary<string, string> files = Hands.PrototypeFiles(new[] { "RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt", "RRE-WP-NSP-KEO-arc-dp2-ZZ-MOD-2200.rvt", "notes.txt",
                "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt", "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3101.rvt" }, rules);
            Assert.Equal("RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt", files["DP1"]);
            Assert.Equal("RRE-WP-NSP-KEO-arc-dp2-ZZ-MOD-2200.rvt", files["DP2"]);
            // VD1 links the file of its keo_file DP1, TH1 holds two files so neither it nor DT1 of keo_file TH1 gets one
            Assert.Equal("RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt", files["VD1"]);
            Assert.False(files.ContainsKey("TH1"));
            Assert.False(files.ContainsKey("DT1"));
        }

        [Fact]
        public void R357_the_config_names_the_fields_of_the_step_hands_all_exact()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit kind pairKind unitClass cluster clusterCode clusterSide clusterRole clusterIndex clusterUnits left right openEnd roadSide rowPair blockMirror "
                + "blockMirrorRoadSide blockMirrorOpenEnd rowPairSideOrBlockMirrorOpenEnd handTable handRule factor linkFile mirrored proposedCluster proposedMirror proposalReason", string.Join(" ", config.StepFields(Hands.StepName).Select(field => field.Name)));
            Assert.All(config.StepFields(Hands.StepName), field => Assert.Equal("noGeometry", field.Kind));
        }

        // The request of a worker build of the step hands on made up reads of the row of HandStep, a footprint block on 2 and 3, a rules file
        // with the made up hand tables and two made up prototype files
        private WorkerRequest HandRequest(string runId)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            var numbers = new[] { "1", "2", "3", "4", "5", "6" };
            var plots = new JArray(numbers.Select(number => Shape(number, Ring((int.Parse(number) - 1) * 12.0, 0.0, int.Parse(number) * 12.0, 25.0))));
            string baseplan = ReadFile("read_baseplan.json", "baseplan", (string)config["drawings"]["baseplan"], new JObject { ["plots"] = plots });
            var blocks = new JArray(new JObject { ["name"] = "Duplex_DP2", ["outline"] = Points(Ring(14.0, 5.0, 34.0, 20.0)) });
            string footprint = ReadFile("read_villaBlocks.json", "villaBlocks", (string)config["drawings"]["villaBlocks"], new JObject { ["blocks"] = blocks });
            string layer = (string)config["analysis"]["roadEdgeLayers"][0];
            var pieces = new JArray(new JObject { ["kind"] = "line", ["layer"] = layer, ["pts"] = new JArray(new JArray(-50.0, -3.0), new JArray(100.0, -3.0)) },
                new JObject { ["kind"] = "line", ["layer"] = layer, ["pts"] = new JArray(new JArray(-3.0, -10.0), new JArray(-3.0, 40.0)) });
            string curbs = ReadFile("read_curbs.json", "curbs", (string)config["drawings"]["roadLayout"], new JObject { ["pieces"] = pieces });
            string salesFile = Path.Combine(folder, Path.GetFileName((string)config["dataBook"]["rev9"]["workbooks"][(string)config["dataBook"]["locationPlan"]["sales"]["workbook"]]));
            File.WriteAllText(salesFile, "a made up sales workbook");
            string workbooks = Path.Combine(folder, "read_workbooks.json");
            File.WriteAllText(workbooks, new JObject
            {
                ["sales"] = new JObject { ["1"] = new JObject { ["Orientation"] = "WS" } },
                [RunStamp.Key] = new JObject { ["runId"] = "r0", ["inputs"] = new JArray(new JObject { ["role"] = "sales", ["path"] = salesFile, ["sha256"] = FileHash.Sha256(salesFile) }) },
            }.ToString());
            string data = Path.Combine(folder, "plots.csv");
            File.WriteAllText(data, "locationPlanNumber,baseplanLabel,typologyRev9,footprintBlock,footprintMirrored\n" + string.Join("\n", numbers.Select(number => number + "," + number + ","
                + (number == "1" ? "VD2," : "DP2,") + (number == "2" || number == "3" ? "Duplex_DP2" : "") + ",no")) + "\n");
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, "kind,number,nbh\n" + string.Join("\n", numbers.Select(number => "clientCode," + number + ",012")) + "\n");
            string rules = Path.Combine(folder, "rules.json");
            File.WriteAllText(rules, HandRules().ToString());
            string prototypes = Path.Combine(folder, "prototypes");
            Directory.CreateDirectory(prototypes);
            // B2. The prototype folder of pastJob, the one Python lists, here the made up folder
            config["inputRoot"] = folder;
            config["pastJob"]["upgradedPrototypeFolder"] = "prototypes";
            string settings = Path.Combine(folder, "project.json");
            File.WriteAllText(settings, config.ToString());
            var inputs = new List<WorkerFile>
            {
                Input("config", settings), Input("rules", rules), Input("plotsCsv", data), Input("labels", labels), Input("baseplanRead", baseplan),
                Input("villaBlocksRead", footprint), Input("curbsRead", curbs), Input("workbooksRead", workbooks),
            };
            foreach (string name in new[] { Dp2File, Vd2File })
            {
                File.WriteAllText(Path.Combine(prototypes, name), "a made up prototype " + name);
                inputs.Add(Input(BuildSteps.PrototypeRole + name, Path.Combine(prototypes, name)));
            }
            return new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" }, Inputs = inputs,
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, Steps = new List<string> { "clusters", "hands", "linkFrame" }, StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
        }

        [Fact]
        public void R357_the_worker_build_writes_the_step_hands_and_refuses_a_prototype_named_otherwise_or_prototypes_of_two_folders()
        {
            Assert.Equal(0, WorkerRunner.Run(HandRequest("h1").Write(folder), WorkerCommands.Default(), () => 9000.0));
            JObject file = RunStamp.Read(Path.Combine(folder, "steps", "step_hands_csharp.json"));
            JToken plots = file["plots"];
            Assert.Equal("Duplex_DP2", (string)plots["2"]["written"]["clusterCode"]);
            Assert.Equal(new[] { false, true, true, false, true }, new[] { "2", "3", "4", "5", "6" }.Select(number => (bool)plots[number]["written"]["mirrored"]).ToArray());
            Assert.True((bool)plots["1"]["written"]["mirrored"]);
            Assert.Equal(Vd2File, (string)plots["1"]["written"]["linkFile"]);
            Assert.Equal(new[] { "2|3" }, ((JObject)RunStamp.Read(Path.Combine(folder, "steps", "step_clusters_csharp.json"))["plots"]).Properties().Select(item => item.Name).ToArray());
            JObject link = RunStamp.Read(Path.Combine(folder, "steps", "step_linkFrame_csharp.json"));
            Assert.Equal(180.0, (double)link["plots"]["4"]["written"]["rotationDeg"]);
            Assert.Equal(Dp2File, (string)link["plots"]["4"]["written"]["prototypeFile"]);

            WorkerRequest renamed = HandRequest("h2");
            renamed.Inputs[8] = new WorkerFile { Role = BuildSteps.PrototypeRole + "other.rvt", Path = renamed.Inputs[8].Path, Sha256 = renamed.Inputs[8].Sha256 };
            Assert.Equal(2, WorkerRunner.Run(renamed.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the input prototype:other.rvt is the file " + Dp2File + ", a prototype file is named by its own file name (R357)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "h2")).Message);

            WorkerRequest apart = HandRequest("h3");
            string elsewhere = Path.Combine(folder, "elsewhere");
            Directory.CreateDirectory(elsewhere);
            File.Copy(apart.Inputs[9].Path, Path.Combine(elsewhere, Vd2File), true);
            apart.Inputs[9] = Input(BuildSteps.PrototypeRole + Vd2File, Path.Combine(elsewhere, Vd2File));
            Assert.Equal(2, WorkerRunner.Run(apart.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the prototype files of the request lie in 2 folders", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "h3")).Message);
        }

        // B2 of the review of PR 55: the C# took the files the request named where Python takes the whole prototype folder of pastJob, so with two
        // DP2 files in the folder and one named the C# gave DP2 a file and Python none. The request names every file of that folder or is refused
        [Fact]
        public void R357_the_worker_takes_the_whole_prototype_folder_of_past_job_and_refuses_a_request_that_names_less_or_another_folder()
        {
            WorkerRequest named = HandRequest("h4");
            string prototypes = Path.Combine(folder, "prototypes");
            const string other = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2201.rvt";
            File.WriteAllText(Path.Combine(prototypes, other), "a second made up DP2 file");
            Assert.Equal(2, WorkerRunner.Run(named.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the prototype folder " + prototypes + " holds files the request does not name: " + other + ", and the request names files it does not hold: none, "
                + "a build takes the whole folder as plan_from_book.folder_files lists it (R357)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "h4")).Message);

            WorkerRequest both = HandRequest("h5");
            both.Inputs.Add(Input(BuildSteps.PrototypeRole + other, Path.Combine(prototypes, other)));
            Assert.Equal(0, WorkerRunner.Run(both.Write(folder), WorkerCommands.Default(), () => 9000.0));
            JObject hands = RunStamp.Read(Path.Combine(folder, "steps", "step_hands_csharp.json"));
            Assert.Equal(JTokenType.Null, hands["plots"]["2"]["written"]["linkFile"].Type);
            Assert.Equal(Vd2File, (string)hands["plots"]["1"]["written"]["linkFile"]);
            File.Delete(Path.Combine(prototypes, other));

            WorkerRequest moved = HandRequest("h6");
            string elsewhere = Path.Combine(folder, "elsewhere");
            Directory.CreateDirectory(elsewhere);
            foreach (string name in new[] { Dp2File, Vd2File })
            {
                File.Copy(Path.Combine(prototypes, name), Path.Combine(elsewhere, name), true);
            }
            moved.Inputs[8] = Input(BuildSteps.PrototypeRole + Dp2File, Path.Combine(elsewhere, Dp2File));
            moved.Inputs[9] = Input(BuildSteps.PrototypeRole + Vd2File, Path.Combine(elsewhere, Vd2File));
            Assert.Equal(2, WorkerRunner.Run(moved.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the prototype files of the request lie in " + elsewhere + ", pastJob names the prototype folder " + prototypes + " (R357)",
                WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "h6")).Message);

            WorkerRequest gone = HandRequest("h7");
            Directory.Delete(prototypes, true);
            gone.Inputs[8] = moved.Inputs[8];
            gone.Inputs[9] = moved.Inputs[9];
            Assert.Equal(2, WorkerRunner.Run(gone.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the prototype folder " + prototypes + " of pastJob is not on disk (R357)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "h7")).Message);
        }

        // Q214 (Bader, 2026-10-31). The Revit run after 5c listed the pane folder in 4 Check and refused the build on the folder of pastJob
        [Fact]
        public void R357_Q214_the_build_takes_the_prototype_folder_the_request_names_else_that_of_past_job()
        {
            string prototypes = Path.Combine(folder, "prototypes");
            string pane = Path.Combine(folder, "pane");
            WorkerRequest named = HandRequest("q1");
            Directory.CreateDirectory(pane);
            foreach (string name in new[] { Dp2File, Vd2File })
            {
                File.Copy(Path.Combine(prototypes, name), Path.Combine(pane, name), true);
            }
            named.Inputs[8] = Input(BuildSteps.PrototypeRole + Dp2File, Path.Combine(pane, Dp2File));
            named.Inputs[9] = Input(BuildSteps.PrototypeRole + Vd2File, Path.Combine(pane, Vd2File));
            named.PrototypeFolder = pane;
            Directory.Delete(prototypes, true);
            Assert.Equal(0, WorkerRunner.Run(named.Write(folder), WorkerCommands.Default(), () => 9000.0));
            JObject hands = RunStamp.Read(Path.Combine(folder, "steps", "step_hands_csharp.json"));
            Assert.Equal(Vd2File, (string)hands["plots"]["1"]["written"]["linkFile"]);

            WorkerRequest config = HandRequest("q2");
            config.PrototypeFolder = pane;
            Assert.Equal(2, WorkerRunner.Run(config.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the prototype files of the request lie in " + prototypes + ", the request names the prototype folder " + pane + " (R357)",
                WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "q2")).Message);

            WorkerRequest none = HandRequest("q3");
            Assert.Null(none.PrototypeFolder);
            Assert.Equal(0, WorkerRunner.Run(none.Write(folder), WorkerCommands.Default(), () => 9000.0));

            WorkerRequest gone = HandRequest("q4");
            gone.PrototypeFolder = Path.Combine(folder, "gone");
            Assert.Equal(2, WorkerRunner.Run(gone.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the prototype folder " + gone.PrototypeFolder + " of the request is not on disk (R357)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "q4")).Message);

            WorkerRequest relative = HandRequest("q5");
            relative.PrototypeFolder = "pane";
            Assert.Equal(2, WorkerRunner.Run(relative.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the request names the prototype folder pane, no full path (R357)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "q5")).Message);
        }

        // Q214. The plan of a build names the folder its prototypes came from, the one button 7 loads them from
        [Fact]
        public void R357_Q214_the_prototype_folder_of_a_build_is_that_of_the_request_else_upgraded_else_past_of_past_job()
        {
            var settings = new JObject { ["inputRoot"] = folder, ["pastJob"] = new JObject { ["prototypeFolder"] = "p2021", ["upgradedPrototypeFolder"] = "p2024" } };
            string pane = Path.Combine(folder, "pane");
            Assert.Equal(pane, BuildSteps.PrototypeFolder(settings, pane));
            Assert.Equal(Path.Combine(folder, "p2024"), BuildSteps.PrototypeFolder(settings, null));
            Assert.Equal(Path.Combine(folder, "p2024"), BuildSteps.PrototypeFolder(settings, ""));
            ((JObject)settings["pastJob"]).Remove("upgradedPrototypeFolder");
            Assert.Equal(Path.Combine(folder, "p2021"), BuildSteps.PrototypeFolder(settings, null));
        }

        private static string Text(IEnumerable<double[]> ring)
        {
            return string.Join(" ", ring.Select(point => point[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + point[1].ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
}
