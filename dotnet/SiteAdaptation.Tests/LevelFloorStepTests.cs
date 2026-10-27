using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R358 and R359. The build steps floors and levels of the worker on a made up row built in code (R10), the C# side of
    // python/tests/test_level_floor_steps.py on the same made up data, on net48 and net8.0
    public sealed class LevelFloorStepTests : IDisposable
    {
        private const string Dp2File = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt";
        private const string Vd2File = "RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt";
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r358_" + Guid.NewGuid().ToString("N"));

        public LevelFloorStepTests()
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

        // The made up yard outline of DP2 not mirrored on a 12 m plot, in the link frame
        private static JObject Yard()
        {
            return new JObject
            {
                ["unit"] = "DP2", ["mirrored"] = false, ["width"] = 12.0, ["type"] = "Yard made up",
                // A JArray given one JArray takes its items, so the one loop is added to an empty list
                ["loops"] = new JArray { new JArray(new JArray(1.0, 2.0), new JArray(5.0, 2.0), new JArray(5.0, 6.0), new JArray(1.0, 6.0)) },
            };
        }

        // keyRules as the rules profile holds them, the hands of test_build_steps.py, the yard groups the made up one, no split of a cluster
        private static JObject MadeRules()
        {
            JObject rules = JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules()));
            JObject key = (JObject)rules["levelProfiles"]["parsons2024Rev6"]["keyRules"];
            key["hand villa"] = new JObject { ["rule"] = "blockMirrorRoadSide", ["byFile"] = new JObject { [Vd2File] = new JObject { ["no left"] = true, ["no none"] = false } } };
            key["hand cluster"] = new JObject { ["rule"] = "clusterSide", ["byFile"] = new JObject { [Dp2File] = new JObject { ["left"] = false, ["right"] = true } } };
            key["hand alone"] = new JObject { ["rule"] = "rowPairSideOrBlockMirrorOpenEnd", ["byFile"] = new JObject { [Dp2File] = new JObject { ["pair left"] = false, ["pair right"] = true } } };
            key["yardOutline"] = new JObject { ["rule"] = "fittedOutlineRefit", ["groups"] = new JArray(Yard()) };
            key["levelSplit"] = new JObject { ["rule"] = "units", ["table"] = new JObject() };
            return rules;
        }

        private sealed class Made
        {
            public Dictionary<string, Dictionary<string, object>> Levels;
            public Dictionary<string, Dictionary<string, object>> Floors;
            public Dictionary<string, Dictionary<string, object>> Links;
            public JObject Rules;
            public JObject Key;
            public LevelBuild Build;
        }

        // test_level_floor_steps.made_build: six 12 m faces along a road at y = -3, plot 1 a VD2 villa, 2 and 3 a DP2 pair of the footprint,
        // 4 to 6 DP2 units in no cluster, the road level 10 m plus a hundredth of x, and the steps linkFrame, levels and floors of it
        private static Made MadeBuild(Action<JObject> change = null)
        {
            JObject rules = MadeRules();
            change?.Invoke(rules);
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            var numbers = new List<string> { "1", "2", "3", "4", "5", "6" };
            var faces = numbers.ToDictionary(number => number, number => BuildFace.Of(number, Ring((int.Parse(number) - 1) * 12.0, 0.0, int.Parse(number) * 12.0, 25.0)));
            var rows = numbers.ToDictionary(number => number, number => new Dictionary<string, string>
            {
                ["locationPlanNumber"] = number, ["typologyRev9"] = number == "1" ? "VD2" : "DP2", ["footprintMirrored"] = "no", ["zone"] = "Zone 2",
            });
            var roads = new RoadLines(new[] { new[] { new[] { -50.0, -3.0 }, new[] { 100.0, -3.0 } }, new[] { new[] { -3.0, -10.0 }, new[] { -3.0, 40.0 } } });
            var sales = new JObject { ["1"] = new JObject { ["Orientation"] = "WS" } };
            var clusters = new List<SiteCluster> { new SiteCluster { Code = "Duplex_DP2", Plots = new List<string> { "2", "3" }, Source = "footprint" } };
            double[] limits = Fronts.Limits(rules, config);
            double roadEdgeM = (double)config["siteBuild"]["corner"]["roadEdgeM"];
            Dictionary<string, FrontItem> fronts = Fronts.Of(numbers, rows, faces, roads, limits, (x, y) => new KeyValuePair<double?, string>(null, null), rules, roadEdgeM, sales);
            HandContext context = Hands.Context(numbers, rows, faces, clusters, fronts.ToDictionary(item => item.Key, item => item.Value.Frame), roads, rules, config);
            JObject key = BuildRules.KeyRules(rules, null, "levels");
            JObject profile = BuildRules.LevelProfile(rules, null);
            var files = new Dictionary<string, string> { ["DP2"] = Dp2File, ["VD2"] = Vd2File };
            List<string> scope = numbers.Concat(new[] { "7" }).ToList();
            Dictionary<string, Dictionary<string, object>> hands = Hands.Step(scope, rows, context, key, files, rules, config);
            Dictionary<string, Dictionary<string, object>> links = LinkFrames.Step(scope, rows, fronts, context.Roles, hands, key, files, rules);
            LevelBuild build = KeyLevels.Of(numbers, rows, clusters, context, members => Fronts.Of(members, rows, faces, roads, limits,
                (x, y) => new KeyValuePair<double?, string>(10.0 + x / 100.0, "road"), rules, roadEdgeM, sales), rules, key, profile, "parsons2024Rev6");
            return new Made
            {
                Levels = KeyLevels.Step(scope, rows, build, key, profile, rules, KeyLevels.LaterParameters(config, profile)),
                Floors = FloorOutlines.Step(scope, rows, build, links, key, profile, rules, 0.5, "parsons2024Rev6"), Links = links, Rules = rules, Key = key, Build = build,
            };
        }

        // El @ Entry of a DP2 or VD2 unit by hand: the mean road at front plus 0.5 percent along its 5.52 m ramp, offsetM 0 of frontRoad
        private static double EntryOf(double mean)
        {
            return mean + 0.005 * 5.52;
        }

        private static string Text(object value)
        {
            return value == null ? "null" : value is IEnumerable<string> texts ? string.Join(" ", texts) : value is JToken token ? token.ToString(Newtonsoft.Json.Formatting.None) : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        [Fact]
        public void R359_parity_levels_the_road_at_front_el_at_entry_and_the_ffl_of_a_piece_by_the_level_rules_before_an_override()
        {
            Dictionary<string, Dictionary<string, object>> levels = MadeBuild().Levels;
            Assert.Equal((10.48 + 10.6) / 2.0, (double)levels["5"]["roadAtFront"]);
            Assert.Equal(EntryOf(10.54), (double)levels["5"]["entry"], 12);
            Assert.Equal(new[] { "6", "5", "null" }, new[] { "5", "6", "4" }.Select(number => Text(levels[number]["partner"])).ToArray());
            Assert.Equal(new[] { "5 6", "4", "2 3" }, new[] { "5", "4", "2" }.Select(number => Text(levels[number]["piece"])).ToArray());
            double top = Rounding.Fixed(EntryOf(10.66), 4) + 0.2;
            Assert.Equal(top, (double)levels["5"]["ffl"]);
            Assert.Equal(top, (double)levels["6"]["ffl"]);
            double ffl = Rounding.Fixed(top, 3);
            Assert.Equal(ffl - 0.075, (double)levels["5"]["ssl"]);
            Assert.Equal(ffl - Rounding.Fixed(EntryOf(10.54), 4), (double)levels["5"]["height"]);
            // By hand: El @ Entry 10.5676, the FFL 10.888, the height 0.3204, one step for the base of 0.2 m and one for the 0.708 riser over it
            Assert.Equal(new[] { 10.5676, 10.888, 0.3204 }, new[] { Rounding.Fixed(EntryOf(10.54), 4), ffl, Rounding.Fixed((double)levels["5"]["height"], 4) });
            Assert.Equal("2", Text(levels["5"]["steps"]));
            Assert.Equal("{\"parameter RRE_ZZ_SP_Prototype-FFL\":\"10.880\"}", Text(levels["5"]["parameters"]));
            Assert.All(levels["7"].Where(item => item.Key != "unit"), item => Assert.Null(item.Value));
            Assert.Equal("unit partner piece roadAtFront entry ffl ssl height steps parameters", string.Join(" ", levels["7"].Keys));
        }

        [Fact]
        public void R359_a_cluster_is_cut_into_pieces_by_the_split_of_its_units_and_a_row_partner_pairs_both_ways()
        {
            var ordered = new List<string> { "2", "3", "8", "9" };
            var roles = ordered.ToDictionary(plot => plot, plot => new ClusterRole { Plots = ordered });
            var units = new Dictionary<string, string> { ["2"] = "DP1", ["3"] = "TH1", ["8"] = "TH1", ["9"] = null };
            var partners = new Dictionary<string, string> { ["5"] = "6", ["6"] = "5", ["4"] = "5" };
            Dictionary<string, List<string>> pieces = KeyLevels.LevelPieces(new[] { "2", "8", "9", "5", "6", "4" }, roles, partners, units, new JObject { ["DP1 TH1 TH1 None"] = "walls 1,3" });
            Assert.Equal("2|3 8|9|5 6|5 6|4", string.Join("|", new[] { "2", "8", "9", "5", "6", "4" }.Select(plot => string.Join(" ", pieces[plot]))));
            Assert.Equal("2 3", string.Join(" ", KeyLevels.LevelPieces(new[] { "2" }, roles, new Dictionary<string, string>(), units, new JObject { ["DP1 TH1 TH1 None"] = "middle" })["2"]));
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => KeyLevels.LevelPieces(new[] { "2" }, roles, new Dictionary<string, string>(), units, new JObject { ["DP1 TH1 TH1 None"] = "every 2" }));
            Assert.Equal("keyRules.levelSplit names the pattern every 2, a build gives none, middle or walls n,m (R339)", refused.Message);
            var sides = new Dictionary<string, string> { ["4"] = "right", ["5"] = "left", ["6"] = "right", ["7"] = "left" };
            var neighbours = new Dictionary<string, Sides>();
            Func<string[], string[], Sides> of = (left, right) =>
            {
                var made = new Sides();
                made.Left.AddRange(left);
                made.Right.AddRange(right);
                return made;
            };
            neighbours["5"] = of(new[] { "4" }, new[] { "6" });
            neighbours["6"] = of(new[] { "5" }, new string[0]);
            neighbours["4"] = of(new string[0], new[] { "5" });
            neighbours["7"] = of(new string[0], new[] { "8", "9" });
            Assert.Equal("5:6 6:5", string.Join(" ", KeyLevels.RowPartners(sides, neighbours).OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Key + ":" + item.Value)));
        }

        [Fact]
        public void R359_the_text_of_the_prototype_ffl_is_the_ffl_down_to_the_cm_with_3_decimals_and_a_level_profile_with_no_ffl_rule_is_refused()
        {
            Assert.Equal("10.520", KeyLevels.PrototypeFfl("fflDownToTheCm", 10.528));
            Assert.Equal("10.528", KeyLevels.PrototypeFfl("ffl", 10.528));
            Assert.Equal("-0.020", KeyLevels.PrototypeFfl("fflDownToTheCm", -0.015));
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => MadeBuild(rules => ((JObject)rules["levelProfiles"]["parsons2024Rev6"]["keyRules"]).Remove("ffl")));
            Assert.Equal("the steps levels and floors of the parity harness are a build by the level rules of keyRules, keyRules of parsons2024Rev6 codes no ffl, the C# builds the "
                + "levels by keyRules alone (R359)", refused.Message);
        }

        [Fact]
        public void R358_parity_floors_the_ramp_generic_and_yard_outlines_and_the_ramp_type_by_the_rule()
        {
            Made made = MadeBuild();
            Dictionary<string, object> five = made.Floors["5"];
            Assert.Equal("48,0 48,25 60,25 60,0", string.Join(" ", ((IEnumerable<double[]>)((IEnumerable<object>)five["generic"]).Single()).Select(point => string.Join(",", point))));
            Assert.Equal("48,0 60,0 60,5.52 48,5.52", string.Join(" ", ((double[][])five["rampOutline"]).Select(point => string.Join(",", point.Select(value => value.ToString(CultureInfo.InvariantCulture))))));
            Assert.Equal("\"0.5\" \"Ramps 0.5%\"", Text(five["rampClass"]) + " " + Text(five["rampType"]));
            double entry = Rounding.Fixed(EntryOf(10.54), 4);
            Assert.Equal(new[] { 10.48, 10.6, entry + 10.6 - (10.48 + 10.6) / 2.0, entry + 10.48 - (10.48 + 10.6) / 2.0 }, (double[])five["rampZ"]);
            Assert.False((bool)(JToken)made.Links["5"]["mirrored"]);
            Assert.Equal(0.1, (double)five["yardDepth"]);
            Assert.Equal("DP2", (string)five["yardUnit"]);
            Assert.Equal(12.0, (double)five["yardWidth"]);
            Assert.Equal("\"Yard made up\"", Text(five["yardType"]));
            double[] insert = ((double[])made.Links["5"]["insert"]).Select(value => Rounding.Fixed(value, 3)).ToArray();
            var expected = new[] { new[] { 1.0, 2.0 }, new[] { 5.0, 2.0 }, new[] { 5.0, 6.0 }, new[] { 1.0, 6.0 } }.Select(point => new[] { insert[0] + point[0], insert[1] + point[1] }).ToArray();
            Assert.Equal(expected, ((IEnumerable<IEnumerable<double[]>>)five["yardOutline"]).Single().ToArray());
            Assert.True((bool)(JToken)made.Links["4"]["mirrored"]);
            Assert.Null(made.Floors["4"]["yardOutline"]);
            Assert.Equal("[{\"key\":\"floors.yard\",\"why\":\"no fitted outline for DP2 mirrored on a plot 12.0 m wide\"}]", Text(made.Floors["4"]["lacks"]));
            Assert.Equal("unit generic rampOutline rampZ rampClass rampType yardDepth yardUnit yardWidth yardType yardOutline lacks", string.Join(" ", made.Floors["7"].Keys));
            Assert.All(made.Floors["7"].Where(item => item.Key != "lacks"), item => Assert.Null(item.Value));
            Assert.Equal("[]", Text(made.Floors["7"]["lacks"]));
        }

        [Fact]
        public void R358_the_back_ramp_corners_take_el_at_entry_as_written_at_4_decimals_never_its_raw_value()
        {
            double[] heights = FloorOutlines.RampHeights(new[] { 10.0, 10.0 }, Rounding.Fixed(10.02745, 4));
            Assert.Equal(new[] { 10.0, 10.0, 10.028, 10.028 }, heights.Select(z => Rounding.Fixed(z, 3)).ToArray());
            Assert.Equal(10.027, Rounding.Fixed(10.02745, 3));
        }

        [Fact]
        public void R358_a_yard_outline_of_the_floor_file_or_a_width_step_of_another_kind_is_refused_with_the_line_of_python()
        {
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => MadeBuild(rules => rules["levelProfiles"]["parsons2024Rev6"]["keyRules"]["yardOutline"] = new JObject { ["rule"] = "fittedOutline" }));
            Assert.Equal("the step floors of the parity harness takes the yard outlines of keyRules.yardOutline groups, keyRules of parsons2024Rev6 codes fittedOutline, whose "
                + "outlines lie in config/floor_outlines.json, which the C# does not read (R358)", refused.Message);
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal(0.5, FloorOutlines.WidthStep(config, "project.json"));
            config["cadLinks"]["floorWidthStepM"] = "0.5";
            Assert.Equal("project.json cadLinks.floorWidthStepM must be a length above 0", Assert.Throws<InvalidDataException>(() => FloorOutlines.WidthStep(config, "project.json")).Message);
        }

        [Fact]
        public void R358_the_width_key_of_the_yard_lookup_rounds_half_to_even_as_python_round_does()
        {
            Assert.Equal(12.0, FloorOutlines.WidthKey(12.25, 0.5));
            Assert.Equal(13.0, FloorOutlines.WidthKey(12.75, 0.5));
            Assert.Equal(12.5, FloorOutlines.WidthKey(12.2500001, 0.5));
            // round() of Python on the exact binary value, half to even: 0.0625 a tie, 1.2355 and 0.0005 just over theirs, 2.675 just under
            Assert.Equal(new[] { 0.062, 1.236, 2.67, 0.001, 0.002 }, new[] { PyMath.Round(0.0625, 3), PyMath.Round(1.2355, 3), PyMath.Round(2.675, 2), PyMath.Round(0.0005, 3), PyMath.Round(0.0015, 3) });
            Assert.Equal("10.520 -0.020 -0.000", PyMath.FixedText(1052 / 100.0, 3) + " " + PyMath.FixedText(-2 / 100.0, 3) + " " + PyMath.FixedText(-0.0, 3));
        }

        [Fact]
        public void R358_R359_the_config_names_the_fields_of_the_steps_levels_and_floors_by_their_kind()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry partner:noGeometry piece:noGeometry roadAtFront:levels4 entry:levels4 ffl:levels3 ssl:levels3 height:levels4 steps:noGeometry parameters:noGeometry",
                string.Join(" ", config.StepFields(KeyLevels.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal("unit:noGeometry generic:points3 rampOutline:points3 rampZ:levels3 rampClass:noGeometry rampType:noGeometry yardDepth:levels3 yardUnit:noGeometry "
                + "yardWidth:points3 yardType:noGeometry yardOutline:points3 lacks:noGeometry", string.Join(" ", config.StepFields(FloorOutlines.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal("clusters fronts hands linkFrame levels floors", string.Join(" ", BuildSteps.Names));
        }

        private string ReadFile(string name, string role, string source, JObject content)
        {
            string drawing = Path.Combine(folder, role + ".dwg");
            File.WriteAllText(drawing, "a made up drawing of " + role);
            content["role"] = role;
            content["source"] = source;
            content["units"] = "Meters";
            content[RunStamp.Key] = new JObject { ["runId"] = "r0", ["inputs"] = new JArray(new JObject { ["role"] = role, ["path"] = drawing, ["sha256"] = FileHash.Sha256(drawing) }) };
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content.ToString());
            return path;
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        // A made up road surface of two triangles over a box at the level z
        private string Surface(string name, double x0, double y0, double x1, double y1, double z)
        {
            Func<double, double, string> point = (x, y) => y.ToString(CultureInfo.InvariantCulture) + " " + x.ToString(CultureInfo.InvariantCulture) + " " + z.ToString(CultureInfo.InvariantCulture);
            string path = Path.Combine(folder, name + ".xml");
            File.WriteAllText(path, "<?xml version=\"1.0\"?><LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\"><Units><Metric linearUnit=\"meter\" areaUnit=\"squareMeter\"/></Units>"
                + "<Surfaces><Surface name=\"" + name + "\"><Definition surfType=\"TIN\"><Pnts><P id=\"1\">" + point(x0, y0) + "</P><P id=\"2\">" + point(x1, y0) + "</P><P id=\"3\">" + point(x1, y1)
                + "</P><P id=\"4\">" + point(x0, y1) + "</P></Pnts><Faces><F>1 2 3</F><F>1 3 4</F></Faces></Definition></Surface></Surfaces></LandXML>");
            return path;
        }

        // The request of a worker build of the steps levels and floors on made up reads of the made up row, a footprint block on 2 and 3, the
        // made up rules, two made up prototype files and the road surfaces of config, the first flat at 10 m under the whole row
        private WorkerRequest Request(string runId, params string[] steps)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            var numbers = new[] { "1", "2", "3", "4", "5", "6" };
            var plots = new JArray(numbers.Select(number => new JObject
            {
                ["kind"] = "plot_shape", ["plot"] = number, ["vertices"] = new JArray(Ring((int.Parse(number) - 1) * 12.0, 0.0, int.Parse(number) * 12.0, 25.0).Select(point => new JArray(point[0], point[1]))),
            }));
            string baseplan = ReadFile("read_baseplan.json", "baseplan", (string)config["drawings"]["baseplan"], new JObject { ["plots"] = plots });
            var blocks = new JArray(new JObject { ["name"] = "Duplex_DP2", ["outline"] = new JArray(Ring(14.0, 5.0, 34.0, 20.0).Select(point => new JArray(point[0], point[1]))) });
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
            File.WriteAllText(data, "locationPlanNumber,baseplanLabel,typologyRev9,footprintBlock,footprintMirrored,zone\n" + string.Join("\n", numbers.Select(number => number + "," + number + ","
                + (number == "1" ? "VD2," : "DP2,") + (number == "2" || number == "3" ? "Duplex_DP2" : "") + ",no,Zone 2")) + "\n");
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, "kind,number,nbh\n" + string.Join("\n", numbers.Select(number => "clientCode," + number + ",012")) + "\n");
            string rules = Path.Combine(folder, "rules.json");
            File.WriteAllText(rules, MadeRules().ToString());
            string prototypes = Path.Combine(folder, "prototypes");
            Directory.CreateDirectory(prototypes);
            config["inputRoot"] = folder;
            config["pastJob"]["upgradedPrototypeFolder"] = "prototypes";
            string settings = Path.Combine(folder, "project.json");
            File.WriteAllText(settings, config.ToString());
            var inputs = new List<WorkerFile>
            {
                Input("config", settings), Input("rules", rules), Input("plotsCsv", data), Input("labels", labels), Input("baseplanRead", baseplan),
                Input("villaBlocksRead", footprint), Input("curbsRead", curbs), Input("workbooksRead", workbooks),
            };
            int place = 0;
            foreach (string name in config["siteBuild"]["roadSurfaces"].Select(item => (string)item))
            {
                inputs.Add(Input(WorkerRunner.RoadSurfaceRole + name, place++ == 0 ? Surface(name, -60.0, -20.0, 100.0, 30.0, 10.0) : Surface(name, 1000.0, 1000.0, 1010.0, 1010.0, 99.0)));
            }
            if (steps.Contains(FloorOutlines.StepName))
            {
                foreach (string name in new[] { Dp2File, Vd2File })
                {
                    File.WriteAllText(Path.Combine(prototypes, name), "a made up prototype " + name);
                    inputs.Add(Input(BuildSteps.PrototypeRole + name, Path.Combine(prototypes, name)));
                }
            }
            return new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" }, Inputs = inputs,
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, Steps = steps.ToList(), StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
        }

        [Fact]
        public void R359_the_worker_build_writes_the_steps_levels_and_floors_from_the_reads_and_refuses_a_request_with_no_road_surface()
        {
            Assert.Equal(0, WorkerRunner.Run(Request("l1", "levels", "floors").Write(folder), WorkerCommands.Default(), () => 9000.0));
            JObject levels = RunStamp.Read(Path.Combine(folder, "steps", "step_levels_csharp.json"));
            // A flat road at 10 m: the road at front 10, El @ Entry 10.0276, the FFL of the pair 5 and 6 10.2276 to 10.228
            JToken five = levels["plots"]["5"]["written"];
            Assert.Equal(new[] { 10.0, 10.0276, 10.228, 10.153, 0.2004 }, new[] { "roadAtFront", "entry", "ffl", "ssl", "height" }.Select(name => (double)five[name]).ToArray());
            Assert.Equal("5 6", string.Join(" ", five["piece"].Select(item => (string)item)));
            Assert.Equal(1, (int)five["steps"]);
            JObject floors = RunStamp.Read(Path.Combine(folder, "steps", "step_floors_csharp.json"));
            JToken ramp = floors["plots"]["5"]["written"];
            Assert.Equal(new[] { 10.0, 10.0, 10.028, 10.028 }, ramp["rampZ"].Select(item => (double)item).ToArray());
            Assert.Equal("Ramps 0.5%", (string)ramp["rampType"]);
            Assert.Equal(6, ((JObject)floors["plots"]).Count);

            // The step levels reads the road surfaces of the level profile, so a request with none is refused before any read
            WorkerRequest none = Request("l2", "levels");
            none.Inputs.RemoveAll(input => input.Role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal));
            Assert.Equal(2, WorkerRunner.Run(none.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains(WorkerRunner.RoadSurfaceRole, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "l2")).Message);
        }
    }
}
