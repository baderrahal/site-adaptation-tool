using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R482. An override frontEdge of a plot names the edge of its baseplan ring that is its front, on the notched plot of python/tests/test_r482_front_edge.py, made up in code (R10)
    public sealed class R482_FrontEdgeTests
    {
        private const string Dp2File = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt";
        private const string Vd2File = "RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt";
        private const string Th1File = "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-2200.rvt";
        private const string NoFace = "the plot has no face, so no ring holds the edge (R482)";

        // Drawn clockwise from the east end of the notch, so the ring is walked as drawn: edge 0 the notch of 3 m, edge 1 its side of 5 m, edge 2 the front of 9 m, 3 west, 4 back, 5 east
        private static readonly double[][] Notched =
        {
            new[] { 72.0, 5.0 }, new[] { 69.0, 5.0 }, new[] { 69.0, 0.0 }, new[] { 60.0, 0.0 }, new[] { 60.0, 25.0 }, new[] { 72.0, 25.0 },
        };

        private static readonly string[] Numbers = { "1", "2", "3", "4", "5", "6" };

        private static RoadLines Roads()
        {
            return new RoadLines(new[] { new[] { new[] { -50.0, -3.0 }, new[] { 100.0, -3.0 } }, new[] { new[] { -3.0, -10.0 }, new[] { -3.0, 40.0 } } });
        }

        // Both the front and the notch look south, the first of the ring wins the tie of the Orientation
        private static JObject Sales()
        {
            return new JObject { ["1"] = new JObject { ["Orientation"] = "WS" }, ["6"] = new JObject { ["Orientation"] = "WS" } };
        }

        private static Dictionary<string, BuildFace> Faces()
        {
            Dictionary<string, BuildFace> faces = Numbers.Take(5).ToDictionary(number => number,
                number => BuildFace.Of(number, LevelFloorStepTests.Ring((int.Parse(number) - 1) * 12.0, 0.0, int.Parse(number) * 12.0, 25.0)));
            faces["6"] = BuildFace.Of("6", Notched);
            return faces;
        }

        private static Dictionary<string, Dictionary<string, string>> Rows(IEnumerable<string> numbers)
        {
            return numbers.ToDictionary(number => number, number => new Dictionary<string, string>
            {
                ["locationPlanNumber"] = number, ["typologyRev9"] = number == "1" ? "VD2" : "DP2", ["footprintMirrored"] = "no", ["zone"] = "Zone 2",
            });
        }

        private static KeyValuePair<double?, string> Level(double x, double y)
        {
            // The notch stands 5 m in, so no level at y = 5 leaves every front on y = 0 its level
            return y > 4.0 && y < 6.0 ? new KeyValuePair<double?, string>(null, null) : new KeyValuePair<double?, string>(10.0 + x / 100.0, "road");
        }

        private static Dictionary<string, FrontItem> Front(IReadOnlyDictionary<string, JToken> edges, IEnumerable<string> numbers = null)
        {
            JObject rules = LevelFloorStepTests.MadeRules();
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            return Fronts.Of(numbers ?? new[] { "6" }, Rows(Numbers), Faces(), Roads(), Fronts.Limits(rules, config), Level, rules, (double)config["siteBuild"]["corner"]["roadEdgeM"], Sales(),
                "roshn-makkah.json", edges);
        }

        private static IReadOnlyDictionary<string, JToken> Edge(string plot, JToken value)
        {
            return new Dictionary<string, JToken> { [plot] = value };
        }

        private static JToken Named(int number)
        {
            return new JObject { ["edge"] = number };
        }

        private sealed class Built
        {
            public Dictionary<string, PlotPlan> Plans;
            public Dictionary<string, FrontItem> Fronts;
            public Dictionary<string, Dictionary<string, object>> Hands;
            public JObject Rules;
            public JObject Config;
        }

        private static OverrideList List(string plot, int number)
        {
            return new OverrideList(new Dictionary<string, JObject> { [plot] = new JObject { ["frontEdge"] = Named(number) } });
        }

        // python/tests/test_r482_front_edge.build: the row with plot 6 notched and the plots of faceless with no face, built through the list
        private static Built Build(OverrideList list, string[] faceless = null)
        {
            List<string> numbers = Numbers.Concat(faceless ?? new string[0]).ToList();
            var rows = Rows(numbers);
            Dictionary<string, BuildFace> faces = Faces();
            JObject rules = LevelFloorStepTests.MadeRules();
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            RoadLines roads = Roads();
            JObject sales = Sales();
            var clusters = new List<SiteCluster> { new SiteCluster { Code = "Duplex_DP2", Plots = new List<string> { "2", "3" }, Source = "footprint" } };
            double[] limits = Fronts.Limits(rules, config);
            double roadEdgeM = (double)config["siteBuild"]["corner"]["roadEdgeM"];
            Dictionary<string, JToken> edges = list.FrontEdges();
            Dictionary<string, FrontItem> fronts = Fronts.Of(numbers, rows, faces, roads, limits, (x, y) => new KeyValuePair<double?, string>(null, null), rules, roadEdgeM, sales, "roshn-makkah.json", edges);
            HandContext context = Hands.Context(numbers, rows, faces, clusters, fronts.ToDictionary(item => item.Key, item => item.Value.Frame), roads, rules, config);
            JObject key = BuildRules.KeyRules(rules, null, "overrides");
            JObject profile = BuildRules.LevelProfile(rules, null);
            var files = new Dictionary<string, string> { ["DP2"] = Dp2File, ["VD2"] = Vd2File, ["TH1-IM"] = Th1File };
            Dictionary<string, Dictionary<string, object>> hands = Hands.Step(numbers, rows, context, key, files, rules, config);
            Func<IEnumerable<string>, Dictionary<string, FrontItem>> leveled = members => Fronts.Of(members, rows, faces, roads, limits, Level, rules, roadEdgeM, sales, "roshn-makkah.json", edges);
            LevelBuild build = KeyLevels.Of(numbers, rows, clusters, context, leveled, rules, key, profile, "parsons2024Rev6");
            return new Built
            {
                Plans = PlanOverrides.Of(numbers, rows, build, fronts, context, hands, key, profile, rules, config, files, list, "parsons2024Rev6", 0.5, "the rules profile", Level),
                Fronts = leveled(numbers),
                Hands = hands,
                Rules = rules,
                Config = config,
            };
        }

        private static string Text(JToken token)
        {
            return token.ToString(Newtonsoft.Json.Formatting.None);
        }

        [Fact]
        public void R482_the_rule_takes_the_notch_of_a_ring_walked_from_its_first_vertex_clockwise()
        {
            Assert.Equal(Notched.Select(point => string.Join(" ", point)), Faces()["6"].Ring.Select(point => string.Join(" ", point)));
            FrontItem item = Front(null)["6"];
            Assert.Equal("orientation", item.FrontRule);
            Assert.Equal(new[] { "69 5", "72 5" }, new[] { item.Frame.Left, item.Frame.Right }.Select(point => string.Join(" ", point)).OrderBy(text => text));
        }

        [Fact]
        public void R482_the_frame_of_the_edge_is_built_as_the_rule_builds_a_frame_rule_named_front_edge()
        {
            FrontItem item = Front(Edge("6", Named(2)))["6"];
            FrontFrame frame = item.Frame;
            Assert.Equal("frontEdge", item.FrontRule);
            // Seen from the road south of it the plot looks north, so the left end is the west end
            Assert.Equal(new[] { 60.0, 0.0 }, frame.Left);
            Assert.Equal(new[] { 69.0, 0.0 }, frame.Right);
            Assert.Equal(new[] { 0.0, 1.0 }, frame.N.Select(value => value + 0.0));
            Assert.Equal(new[] { 1.0, 0.0 }, frame.R.Select(value => value + 0.0));
            Assert.Equal(9.0, frame.Length);
            Assert.Equal(3.0, frame.Distance);
            Assert.False(frame.Tied);
            Assert.Equal(new[] { 60.0, 0.0 }, item.Corners[0]);
            Assert.Equal(new[] { 69.0, 0.0 }, item.Corners[1]);
            Assert.True(item.Depth > 0);
            Assert.Equal(new[] { 10.6, 10.69 }, item.Front);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void R482_each_number_of_the_ring_gives_the_frame_of_that_edge(int number)
        {
            FrontFrame frame = Front(Edge("6", Named(number)))["6"].Frame;
            Assert.Equal(new[] { Notched[number], Notched[(number + 1) % 6] }.Select(point => string.Join(" ", point)).OrderBy(text => text),
                new[] { frame.Left, frame.Right }.Select(point => string.Join(" ", point)).OrderBy(text => text));
            double[] probe = { (frame.Left[0] + frame.Right[0]) / 2 + frame.N[0] * 0.01, (frame.Left[1] + frame.Right[1]) / 2 + frame.N[1] * 0.01 };
            Assert.True(LocationPlan.Inside(Notched, probe[0], probe[1]));
        }

        [Theory]
        [InlineData("{\"edge\": 6}", "override frontEdge of plot 6 names edge 6, the ring of its face holds edges 0 to 5 (R482)")]
        [InlineData("{\"edge\": -1}", "override frontEdge of plot 6 names edge -1, the ring of its face holds edges 0 to 5 (R482)")]
        [InlineData("{\"edge\": 1.5}", "override frontEdge of plot 6 holds {\"edge\": 1.5}, its value is {\"edge\": n}, n a whole number (R482)")]
        [InlineData("{\"edge\": true}", "override frontEdge of plot 6 holds {\"edge\": true}, its value is {\"edge\": n}, n a whole number (R482)")]
        [InlineData("{\"edge\": \"2\"}", "override frontEdge of plot 6 holds {\"edge\": \"2\"}, its value is {\"edge\": n}, n a whole number (R482)")]
        [InlineData("{\"edge\": 2, \"more\": 1}", "override frontEdge of plot 6 holds {\"edge\": 2, \"more\": 1}, its value is {\"edge\": n}, n a whole number (R482)")]
        [InlineData("2", "override frontEdge of plot 6 holds 2, its value is {\"edge\": n}, n a whole number (R482)")]
        public void R482_a_number_no_ring_holds_or_another_value_is_refused_by_name(string value, string line)
        {
            Assert.Equal(line, Assert.Throws<InvalidDataException>(() => Front(Edge("6", JToken.Parse(value)))).Message);
        }

        [Fact]
        public void R482_a_number_past_a_long_is_refused_by_name()
        {
            Assert.Equal("override frontEdge of plot 6 names edge 99999999999999999999, the ring of its face holds edges 0 to 5 (R482)",
                Assert.Throws<InvalidDataException>(() => Front(Edge("6", JToken.Parse("{\"edge\": 99999999999999999999}")))).Message);
        }

        [Fact]
        public void R482_no_override_gives_the_fronts_as_before()
        {
            Func<Dictionary<string, FrontItem>, string> text = fronts => string.Join("\n", fronts.Select(item => item.Key + " " + item.Value.FrontRule + " " + item.Value.Unit + " "
                + Text(JToken.FromObject(new object[] { item.Value.Frame, item.Value.Corners, item.Value.Depth, item.Value.Front, item.Value.Surfaces }))));
            string before = text(Front(null, Numbers));
            Assert.Equal(before, text(Front(new Dictionary<string, JToken>(), Numbers)));
            Assert.Equal(before, text(Front(Edge("99", Named(0)), Numbers)));
        }

        [Fact]
        public void R482_a_plot_with_no_face_takes_no_frame_and_the_build_says_why()
        {
            FrontItem item = Front(Edge("8", Named(0)), new[] { "8" })["8"];
            Assert.Null(item.Frame);
            Assert.Equal("nearestCurb", item.FrontRule);
            Dictionary<string, PlotPlan> plans = Build(List("8", 0), new[] { "8" }).Plans;
            Assert.False((bool)plans["8"].Overrides["written"]);
            Assert.Equal(NoFace, (string)((JObject)plans["8"].Overrides["notApplied"])["frontEdge"]);
        }

        [Fact]
        public void R482_a_notched_plot_is_written_with_the_override_and_not_without()
        {
            Built without = Build(new OverrideList(new Dictionary<string, JObject>()));
            Assert.False((bool)without.Plans["6"].Overrides["written"]);
            Assert.Null(without.Fronts["6"].Front);
            Built built = Build(List("6", 2));
            PlotPlan plot = built.Plans["6"];
            Assert.True((bool)plot.Overrides["written"]);
            Assert.Equal(new[] { "frontEdge" }, (List<string>)plot.Overrides["applied"]);
            Assert.Equal("{}", Text((JObject)plot.Overrides["notApplied"]));
            Assert.Equal(new[] { 10.6, 10.69 }, built.Fronts["6"].Front);
            Assert.NotNull(plot.Overrides["ffl"]);
            // Plot 5, its row partner, shares its FFL by keyRules.ffl as any pair does, the other plots of the row keep every value
            Assert.Equal(Convert.ToString(plot.Overrides["ffl"]), Convert.ToString(built.Plans["5"].Overrides["ffl"]));
            foreach (string number in Numbers.Take(4))
            {
                Assert.Equal(Text(JToken.FromObject(without.Plans[number].Overrides.Where(item => !(item.Value is StepValue)).ToDictionary(item => item.Key, item => item.Value))),
                    Text(JToken.FromObject(built.Plans[number].Overrides.Where(item => !(item.Value is StepValue)).ToDictionary(item => item.Key, item => item.Value))));
            }
        }

        [Fact]
        public void R482_a_row_marked_use_rule_keeps_the_rule_front_and_says_why()
        {
            var list = new OverrideList(new Dictionary<string, JObject>(), null, new Dictionary<string, Dictionary<string, string>> { ["6"] = new Dictionary<string, string> { ["frontEdge"] = "use rule" } });
            Assert.Empty(list.FrontEdges());
            Built built = Build(list);
            Assert.Equal("orientation", built.Fronts["6"].FrontRule);
            Assert.Equal("use rule", (string)((JObject)built.Plans["6"].Overrides["notApplied"])["frontEdge"]);
        }

        [Fact]
        public void R482_the_list_gives_the_edges_of_its_plots()
        {
            var list = new OverrideList(new Dictionary<string, JObject>
            {
                ["6"] = new JObject { ["frontEdge"] = Named(2), ["heightM"] = new JObject { ["gapToRuleM"] = 0.1 } },
                ["5"] = new JObject { ["heightM"] = new JObject { ["gapToRuleM"] = 0.1 } },
            });
            Assert.Equal(new[] { "6" }, list.FrontEdges().Keys);
            Assert.Equal("{\"edge\":2}", Text(list.FrontEdges()["6"]));
        }

        private const string TextOfEdge3 = "edge 3 of the plot outline";

        [Fact]
        public void R482_the_check_table_shows_the_rule_and_the_past_in_words()
        {
            // Q376. The rule column names the front set by hand, the past is the edge the list writes
            Assert.Equal(CheckShownText.FrontEdge, CheckShown.RuleName("parsons frontEdge"));
            Assert.Equal(TextOfEdge3, CheckShown.Past(Named(3)));
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal("Position", (string)config["checkRows"]["fields"]["frontEdge"]["check"]);
        }

        // R401, Q299. What a frontEdge line moves, as MOVED of python/tests/test_r482_front_edge.py holds it
        private static readonly string[] Moved =
        {
            "insert", "rotationDeg", "levels.entry", "levels.ffl", "levels.ssl", "levels.height", "levels.steps", "levels.roadAtFrontM", "linkName",
            "parameters.RRE_ZZ_SP_Prototype-FFL", "parameters.RRE_ZZ_SP_Number-of-Steps", "parameters.RRE_ZZ_SP_RVT-Name",
            "floors.generic.z", "floors.generic.heightOffset", "floors.yard.outline", "floors.yard.type", "floors.yard.z", "floors.yard.heightOffset",
            "floors.ramp.outline", "floors.ramp.corners", "floors.ramp.heightOffset", "walls.ends", "walls.base", "walls.top",
        };

        // A value that stands on the elevation of its level or on where a wall ends reads UNKNOWN by what it holds with no override too
        private static readonly string[] StillUnknown = { "floors.generic.heightOffset", "floors.yard.heightOffset", "floors.ramp.heightOffset", "walls.ends" };

        // test_r482_front_edge.strong_table: the map of the settings on a block that holds every entry a frontEdge value reads as a strong measure, so a value read only by the rules would read MEASURED
        private static ValueStatusTable StrongTable()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            string profileFile = (string)config["profile"];
            var profile = new JObject();
            var settings = new JObject();
            foreach (JProperty field in ((JObject)config["valueStatus"]["overrideReads"]["frontEdge"]).Properties())
            {
                string parameter = field.Name.StartsWith("parameters.", StringComparison.Ordinal) ? field.Name.Substring("parameters.".Length) : "";
                foreach (JObject reference in ((JArray)field.Value).OfType<JObject>())
                {
                    foreach (Tuple<string, JObject> kind in new[] { Tuple.Create("profile", profile), Tuple.Create("config", settings) })
                    {
                        if (reference[kind.Item1] != null)
                        {
                            string path = ((string)reference[kind.Item1]).Replace("{levelProfile}", "parsons2024Rev6").Replace("{hand}", "alone").Replace("{unit}", "DP2").Replace("{parameter}", parameter);
                            kind.Item2[path] = new JObject { ["status"] = "MEASURED", ["strength"] = "strong", ["share"] = 0.95, ["measuredBy"] = "made" };
                        }
                    }
                }
            }
            return new ValueStatusTable((JObject)config["valueStatus"], new JObject { [profileFile] = profile, [ValueStatus.SettingsFile] = settings }, profileFile, "parsons2024Rev6");
        }

        private static string Sorted(IEnumerable<string> names)
        {
            return string.Join(" ", names.OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void R482_R401_the_override_sets_hold_what_a_front_edge_line_moves()
        {
            JObject status = (JObject)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["valueStatus"];
            Assert.Equal(Sorted(Moved), Sorted(((JArray)status["overrideSets"]["frontEdge"]).Select(name => (string)name)));
            // Every field still reads the rules that stand on the front, so the hand pick drops only the rule of the front
            Assert.Equal(Sorted(Moved), Sorted(((JObject)status["overrideReads"]["frontEdge"]).Properties().Select(field => field.Name)));
        }

        [Fact]
        public void R482_R401_a_value_on_a_hand_picked_front_names_the_line_and_never_reads_as_a_rule_measure()
        {
            foreach (string field in Moved)
            {
                Tuple<string, JObject, string> found = StrongTable().Of(field, "computed", "alone", new[] { "frontEdge" }, "DP2", "6");
                Assert.EndsWith(" by frontEdge", found.Item3);
                Assert.Equal("[\"frontEdge\"]", Text(found.Item2["overrides"]));
                Assert.Equal(StillUnknown.Contains(field) ? "UNKNOWN" : "RECOMMENDED", found.Item1);
                Assert.Equal("Q053", (string)found.Item2["entries"][0]["question"]);
                Assert.True(((JArray)found.Item2["entries"]).Count > 1, field);
            }
        }

        [Fact]
        public void R482_R401_the_front_rule_is_what_the_hand_pick_leaves_and_the_road_stays_read()
        {
            ValueStatusTable table = StrongTable();
            Func<string, string> read = field => string.Join(" ", ((JArray)table.Of(field, "estimate", "alone", new[] { "frontEdge" }, "DP2", "6").Item2["entries"]).Select(entry => (string)entry["entry"] ?? ""));
            Assert.Contains("frontRoad.surface", read("levels.roadAtFrontM"));
            Assert.DoesNotContain("frontCorner.rule", read("levels.roadAtFrontM"));
            Assert.Contains("keyRules.linkRotation.rule", read("rotationDeg"));
            Assert.Contains("keyRules.hand alone.rule", read("rotationDeg"));
            Assert.Contains("keyRules.entry.rule", read("levels.entry"));
            Assert.Contains("keyRules.rampOutline.rule", read("floors.ramp.outline"));
        }

        [Fact]
        public void R482_R401_a_front_edge_with_a_height_gap_reads_what_both_leave()
        {
            ValueStatusTable table = StrongTable();
            Tuple<string, JObject, string> height = table.Of("levels.height", "estimate", "alone", new[] { "frontEdge", "heightM" }, "DP2", "6");
            Assert.Equal("levels.height from override by frontEdge heightM", height.Item3);
            Assert.Equal("RECOMMENDED", height.Item1);
            Assert.Equal(2, ((JArray)height.Item2["entries"]).Count);
            Assert.EndsWith("keyRules.entry.rule", (string)height.Item2["entries"][1]["entry"]);
            // The gap sets the FFL outright, so it reads no rule of the FFL whatever the front left
            Assert.Single((JArray)table.Of("levels.ffl", "estimate", "alone", new[] { "frontEdge", "heightM" }, "DP2", "6").Item2["entries"]);
        }

        [Fact]
        public void R482_R401_the_values_of_a_plot_built_on_the_line_name_it_and_the_others_stay()
        {
            Built built = Build(List("6", 2));
            ValueStatusTable table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            Dictionary<string, Dictionary<string, object>> step = ValueStatus.Step(built.Plans.Keys.ToList(), built.Plans, built.Hands, table);
            JObject fields = (JObject)step["6"]["fields"];
            List<string> named = fields.Properties().Where(field => ((string)field.Value["statusFrom"]).Contains("by frontEdge")).Select(field => field.Name).ToList();
            Assert.Equal(Sorted(Moved.Where(field => fields[field] != null && (string)fields[field]["source"] != "override")), Sorted(named));
            foreach (string field in new[] { "insert", "rotationDeg", "levels.entry", "levels.ffl", "floors.ramp.corners" })
            {
                Assert.Contains(field, named);
            }
            Assert.DoesNotContain(named, field => (string)fields[field]["status"] == "MEASURED");
            // A plot of the row that holds no line reads as before
            Assert.DoesNotContain(((JObject)step["4"]["fields"]).Properties(), field => ((string)field.Value["statusFrom"]).Contains("frontEdge"));
        }

        private static OverrideList Lines(params string[] heightPlots)
        {
            Dictionary<string, JObject> found = heightPlots.ToDictionary(plot => plot, plot => new JObject { ["heightM"] = new JObject { ["gapToRuleM"] = -0.004 } });
            if (!found.ContainsKey("6"))
            {
                found["6"] = new JObject();
            }
            found["6"]["frontEdge"] = Named(2);
            return new OverrideList(found);
        }

        [Fact]
        public void R482_row_partners_hold_one_ffl_only_when_both_take_the_gap()
        {
            double rule = Convert.ToDouble(Build(Lines()).Plans["6"].Overrides["ffl"]);
            Dictionary<string, PlotPlan> both = Build(Lines("5", "6")).Plans;
            Assert.Equal(Convert.ToDouble(both["5"].Overrides["ffl"]), Convert.ToDouble(both["6"].Overrides["ffl"]), 9);
            // The step holds the FFL unrounded with no gap, a gap moves the FFL of the rule at its 3 decimals
            Assert.Equal(Math.Round(rule, 3) - 0.004, Convert.ToDouble(both["6"].Overrides["ffl"]), 9);
            Dictionary<string, PlotPlan> alone = Build(Lines("5")).Plans;
            Assert.NotEqual(Convert.ToDouble(alone["5"].Overrides["ffl"]), Convert.ToDouble(alone["6"].Overrides["ffl"]), 9);
        }
    }
}
