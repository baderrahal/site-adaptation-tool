using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R80, R208, R347. plan_<NBH>.json assembled from the step files of a build as plan_from_book.py writes plan.json, on made up step files only
    public class R80_PlanFileTests
    {
        private static JObject Plot(JObject raw, JObject written = null)
        {
            return new JObject { ["raw"] = raw, ["written"] = written ?? (JObject)raw.DeepClone() };
        }

        private static JObject Step(string name, JObject plots, JObject tables = null)
        {
            var step = new JObject { ["format"] = "siteAdaptationStep", ["formatVersion"] = 2, ["step"] = name, ["side"] = "csharp", ["plots"] = plots };
            if (tables != null)
            {
                step["tables"] = tables;
            }
            return step;
        }

        // Every sourced value of plot 1 with a source the test names, so the plan is seen to take each source from the status step
        private static readonly string[] Keys =
        {
            "unit", "prototypeFile", "insert", "rotationDeg", "mirrored", "levels.ffl", "levels.ssl", "levels.entry", "levels.height", "levels.steps", "levels.roadAtFrontM",
            "parameters.P", "parameters.RVT", "linkName", "floors.generic.type", "floors.generic.level", "floors.generic.levelElevation", "floors.generic.outline",
            "floors.generic.plotValue", "floors.generic.z", "floors.generic.heightOffset", "floors.yard.type", "floors.yard.level", "floors.yard.levelElevation",
            "floors.yard.outline", "floors.yard.plotValue", "floors.yard.z", "floors.yard.heightOffset", "floors.ramp.type", "floors.ramp.level", "floors.ramp.levelElevation",
            "floors.ramp.outline", "floors.ramp.plotValue", "floors.ramp.corners", "floors.ramp.heightOffset", "floors.ramp.slopePercent",
        };

        internal static Dictionary<string, JObject> Steps(bool withStatus = true)
        {
            var fields = new JObject();
            foreach (string key in withStatus ? Keys : new string[0])
            {
                fields[key] = new JObject { ["source"] = "src " + key, ["status"] = "MEASURED", ["statusFrom"] = key + " from x" };
            }
            JObject wallStatus = new JObject();
            foreach (string field in new[] { "plots", "ends", "base", "top" })
            {
                wallStatus[field] = new JObject { ["status"] = "MEASURED", ["statusFrom"] = "walls." + field + " from keyRules" };
            }
            var noOverride = new JObject { ["unit"] = "DP1", ["skip"] = "sold", ["written"] = false, ["applied"] = new JArray(), ["notApplied"] = new JObject(), ["mirrored"] = null,
                ["insert"] = null, ["rotationDeg"] = null, ["roadAtFront"] = null, ["entry"] = null, ["ffl"] = null, ["ssl"] = null, ["height"] = null, ["steps"] = null, ["rampZ"] = null,
                ["rampType"] = null, ["yardZ"] = null, ["yardOutline"] = null };
            JObject second = (JObject)noOverride.DeepClone();
            second["unit"] = "DT1";
            second["skip"] = null;
            second["mirrored"] = false;
            second["notApplied"] = new JObject { ["heightM"] = "the plot is not written, it lacks levels.ffl" };
            var written = new JObject { ["unit"] = "DP2", ["skip"] = null, ["written"] = true, ["applied"] = new JArray("mirrored"), ["notApplied"] = new JObject(), ["mirrored"] = true,
                ["insert"] = new JArray(100.0004, 200.0006), ["rotationDeg"] = 30.5, ["roadAtFront"] = 10.1, ["entry"] = 10.2, ["ffl"] = 10.4887, ["ssl"] = 10.4137, ["height"] = 0.2887,
                ["steps"] = 1, ["rampZ"] = new JArray(10.0, 10.2, 10.3, 10.1), ["rampType"] = "Ramps 0.5%", ["yardZ"] = 10.3887,
                ["yardOutline"] = JArray.Parse("[[[1.0004,2],[3,4],[5,6]]]") };
            JObject shown = (JObject)written.DeepClone();
            shown["insert"] = new JArray(100.0, 200.001);
            shown["ffl"] = 10.489;
            shown["ssl"] = 10.414;
            shown["yardZ"] = 10.389;
            shown["yardOutline"] = JArray.Parse("[[[1.0,2],[3,4],[5,6]]]");
            return new Dictionary<string, JObject>
            {
                ["clusters"] = Step("clusters", new JObject { ["1|2"] = Plot(new JObject { ["plots"] = new JArray("1", "2"), ["code"] = "C", ["source"] = "footprint" }) }),
                ["fronts"] = Step("fronts", new JObject
                {
                    ["1"] = Plot(new JObject { ["length"] = 12.0, ["roadLevelLeft"] = 10.0004, ["roadLevelRight"] = 10.2, ["surfaceLeft"] = "S1", ["surfaceRight"] = "S1", ["noRoadLevel"] = null }),
                    ["2"] = Plot(new JObject { ["length"] = 12.0, ["roadLevelLeft"] = null, ["roadLevelRight"] = 9.9, ["surfaceLeft"] = null, ["surfaceRight"] = "S1",
                        ["noRoadLevel"] = new JObject { ["plot"] = "2", ["corners"] = new JArray("left"), ["tried"] = new JArray("S1"), ["reason"] = "none" } }),
                    ["3"] = Plot(new JObject { ["roadLevelLeft"] = null, ["roadLevelRight"] = null, ["surfaceLeft"] = null, ["surfaceRight"] = null,
                        ["noRoadLevel"] = new JObject { ["plot"] = "3" } }),
                }),
                ["hands"] = Step("hands", new JObject
                {
                    ["1"] = Plot(new JObject { ["clusterRole"] = "pair" }), ["2"] = Plot(new JObject { ["clusterRole"] = "pair" }), ["3"] = Plot(new JObject { ["clusterRole"] = null }),
                }),
                ["linkFrame"] = Step("linkFrame", new JObject
                {
                    ["1"] = Plot(new JObject { ["prototypeFile"] = "F-DP2.rvt", ["lacks"] = new JArray() }),
                    ["2"] = Plot(new JObject { ["prototypeFile"] = "F-DT1.rvt", ["lacks"] = new JArray() }),
                    ["3"] = Plot(new JObject { ["prototypeFile"] = "F-DP1.rvt", ["lacks"] = new JArray() }),
                }),
                ["levels"] = Step("levels", new JObject
                {
                    ["1"] = Plot(new JObject { ["partner"] = "2", ["entry"] = 10.2, ["ffl"] = 10.48, ["height"] = 0.28, ["steps"] = 1, ["parameters"] = new JObject { ["parameter P"] = "10.480" } }),
                    ["2"] = Plot(new JObject { ["partner"] = null, ["entry"] = null, ["ffl"] = null, ["height"] = null, ["steps"] = null, ["parameters"] = null }),
                    ["3"] = Plot(new JObject { ["partner"] = null, ["entry"] = null, ["ffl"] = null, ["height"] = null, ["steps"] = null, ["parameters"] = null }),
                }),
                ["floors"] = Step("floors", new JObject
                {
                    ["1"] = Plot(new JObject { ["generic"] = JArray.Parse("[[[0,0],[10,0],[10,10]]]"), ["rampOutline"] = JArray.Parse("[[0,0],[10,0],[10,5],[0,5]]"), ["rampClass"] = "0.5",
                        ["yardUnit"] = "DP2", ["yardType"] = "yard", ["lacks"] = new JArray() }),
                    ["2"] = Plot(new JObject { ["generic"] = null, ["rampOutline"] = null, ["rampClass"] = null, ["yardUnit"] = "DT1", ["yardType"] = null,
                        ["lacks"] = JArray.Parse("[{\"key\":\"floors.yard\",\"why\":\"no fitted outline\"}]") }),
                    ["3"] = Plot(new JObject { ["lacks"] = new JArray() }),
                }),
                ["walls"] = Step("walls", new JObject
                {
                    ["1"] = Plot(new JObject { ["edges"] = new JArray(1), ["plots"] = JArray.Parse("[[\"1\",\"2\"]]"), ["ends"] = JArray.Parse("[[[0,0],[10,0]]]"), ["base"] = new JArray(10.389),
                        ["top"] = new JArray(12.189), ["levelPlots"] = JArray.Parse("[[\"1\",\"1\"]]"), ["status"] = new JArray(wallStatus) }),
                    ["2"] = Plot(new JObject { ["edges"] = new JArray(), ["plots"] = new JArray(), ["ends"] = new JArray(), ["base"] = new JArray(), ["top"] = new JArray(), ["levelPlots"] = new JArray(), ["status"] = new JArray() }),
                    ["3"] = Plot(new JObject { ["edges"] = new JArray(), ["plots"] = new JArray(), ["ends"] = new JArray(), ["base"] = new JArray(), ["top"] = new JArray(), ["levelPlots"] = new JArray(), ["status"] = new JArray() }),
                }, new JObject { ["statusFrom"] = new JObject { ["walls.base from keyRules"] = new JObject { ["strength"] = "weak", ["share"] = 0.98251 } } }),
                ["overrides"] = Step("overrides", new JObject { ["1"] = Plot(written, shown), ["2"] = Plot(second), ["3"] = Plot(noOverride) }),
                ["parameters"] = Step("parameters", new JObject
                {
                    ["1"] = Plot(new JObject { ["parameters"] = new JObject { ["P"] = "DP2", ["RVT"] = "1 | DP2" }, ["sources"] = new JObject { ["P"] = "keyRules", ["RVT"] = "override" },
                        ["lacks"] = new JArray(), ["linkName"] = "1 | DP2" }),
                    ["2"] = Plot(new JObject { ["parameters"] = new JObject { ["P"] = "DT1" }, ["sources"] = new JObject { ["P"] = "keyRules" }, ["lacks"] = new JArray("Steps"), ["linkName"] = null }),
                    ["3"] = Plot(new JObject { ["parameters"] = null, ["sources"] = null, ["lacks"] = null, ["linkName"] = null }),
                }),
                ["status"] = Step("status", new JObject
                {
                    ["1"] = Plot(new JObject { ["unit"] = "DP2", ["written"] = true, ["fields"] = fields }),
                    ["2"] = Plot(new JObject { ["unit"] = "DT1", ["written"] = false, ["fields"] = null }),
                    ["3"] = Plot(new JObject { ["unit"] = "DP1", ["written"] = false, ["fields"] = null }),
                }, new JObject { ["statusFrom"] = new JObject { ["unit from locationPlan"] = new JObject { ["status"] = "DOCUMENT", ["share"] = 0.12345 } } }),
                ["summary"] = Step("summary", new JObject(), new JObject
                {
                    ["summary"] = new JObject
                    {
                        ["frontRule"] = new JObject { ["nearestCurb"] = 3 }, ["roadSurfacesOfFrontCorners"] = new JObject { ["S1"] = 3 }, ["floorsFound"] = new JObject { ["generic"] = 1 },
                        ["floorTypes"] = new JObject { ["generic: G"] = 1 }, ["handRules"] = new JObject { ["override"] = 1 }, ["overridesNotApplied"] = new JObject { ["heightM"] = 1 },
                        ["steps"] = new JObject { ["from"] = "keyRules.steps" }, ["statusByField"] = new JObject(), ["assumedByField"] = new JObject(), ["statusPlots"] = new JObject(),
                        ["conflicts"] = new JArray(new JObject { ["plot"] = "1" }),
                    },
                }),
            };
        }

        internal static PlanInputs Inputs()
        {
            return new PlanInputs
            {
                Nbh = "021",
                Site = JObject.Parse("{\"surveyPoint\":{\"position\":[1.00049,2,0],\"sharedPosition\":[0,0,0]},\"angleToTrueNorthDeg\":0.0004,"
                    + "\"levels\":[{\"name\":\"Level 1\",\"elevationM\":0},{\"name\":\"Level 2\",\"elevationM\":4.0}]}"),
                Settings = JObject.Parse("{\"floors\":{\"types\":{\"generic\":[\"Generic 150mm\"],\"yard\":[\"yard\"],\"ramp\":[\"Ramps 0.5%\"]},"
                    + "\"levels\":{\"generic\":\"Level 1\",\"yard\":\"Level 2\",\"ramp\":\"Level 1\"},\"plotParameter\":\"Plot\",\"yardBelowFflM\":0.1},"
                    + "\"siteBuild\":{\"roadFolder\":{\"notTaken\":{\"a\":\"b\"}}},\"majority\":{\"overrideKeep\":{\"laterFields\":{\"parsons\":[\"x\"]}}}}"),
                PrototypeFolder = Path.Combine("root", "Prototypes"),
                ProfileName = "parsons2024Rev6",
                LevelProfile = JObject.Parse("{\"job\":\"parsons\"}"),
                KeyRules = JObject.Parse("{\"yard\":{\"table\":{\"Zone 1\":\"0.1\"},\"notMeasured\":{\"Zone 1\":\"pending\"}},"
                    + "\"yardOutline\":{\"rule\":\"fittedOutlineRefit\",\"groups\":[{\"unit\":\"DP2\",\"mirrored\":true,\"width\":12.0,\"type\":\"yard\"}]}}"),
                Rules = JObject.Parse("{\"typologies\":{}}"),
                WidthStep = 0.5,
                OverrideSources = new List<string> { "parsons" },
                Rows = new Dictionary<string, Dictionary<string, string>>
                {
                    ["1"] = new Dictionary<string, string> { ["zone"] = "Zone 1", ["nbhId"] = "021", ["roadLevelLeft"] = "10.003", ["roadLevelRight"] = "10.3" },
                    ["2"] = new Dictionary<string, string> { ["zone"] = "Zone 1", ["nbhId"] = "021" },
                    ["3"] = new Dictionary<string, string> { ["zone"] = "Zone 1", ["nbhId"] = "021" },
                },
            };
        }

        private static string Text(JToken token)
        {
            return token.ToString(Newtonsoft.Json.Formatting.None);
        }

        [Fact]
        public void R80_plan_top_level_holds_format_site_and_floor_settings()
        {
            JObject plan = BuildPlan.Assemble(Steps(), Inputs());
            Assert.Equal("site-adaptation-plan", (string)plan["format"]);
            Assert.Equal(1, (int)plan["formatVersion"]);
            Assert.Equal("021", (string)plan["neighbourhood"]);
            Assert.Equal("{\"name\":\"shared\",\"lengthUnit\":\"m\",\"angleUnit\":\"deg\"}", Text(plan["coordinateSystem"]));
            Assert.Equal(1.0, (double)plan["surveyPoint"]["position"][0]);
            Assert.Equal(0.0, (double)plan["angleToTrueNorthDeg"]);
            Assert.Equal(Path.Combine("root", "Prototypes"), (string)plan["prototypeFolder"]);
            Assert.Equal("Plot", (string)plan["floorSettings"]["plotParameter"]);
            Assert.Null(plan["floorSettings"]["yardBelowFflM"]);
            Assert.Equal("the new site file", (string)plan["sources"]["survey"]);
            // R401. The detail of each status once, the values and the walls together, by key in the order of sorted, the shares as the block holds them
            Assert.Equal(new[] { "unit from locationPlan", "walls.base from keyRules" }, ((JObject)plan["statusFrom"]).Properties().Select(item => item.Name));
            Assert.Equal(0.98251, (double)plan["statusFrom"]["walls.base from keyRules"]["share"]);
            Assert.Equal("plan_021.json", BuildPlan.FileName("021"));
            Assert.Equal("plan_all.json", BuildPlan.FileName(null));
        }

        [Fact]
        public void R80_plan_plot_takes_values_after_the_overrides_and_each_source_from_the_status_step()
        {
            JObject plan = BuildPlan.Assemble(Steps(), Inputs());
            var plots = (JArray)plan["plots"];
            Assert.Single(plots);
            JObject plot = (JObject)plots[0];
            Assert.Equal("1", (string)plot["plot"]);
            Assert.Equal("[100.0,200.001,10.489]", Text(plot["insert"]["value"]));
            Assert.Equal("src insert", (string)plot["insert"]["source"]);
            Assert.Equal("insert from x", (string)plot["insert"]["statusFrom"]);
            Assert.True((bool)plot["mirrored"]["value"]);
            Assert.Equal(10.489, (double)plot["levels"]["ffl"]["value"]);
            Assert.Equal(10.414, (double)plot["levels"]["ssl"]["value"]);
            Assert.Equal(0.2887, (double)plot["levels"]["height"]["value"]);
            Assert.Equal(10.1, (double)plot["levels"]["roadAtFrontM"]["value"]);
            Assert.Equal("src parameters.RVT", (string)plot["parameters"]["RVT"]["source"]);
            Assert.Equal("1 | DP2", (string)plot["linkName"]["value"]);
            Assert.Equal("{\"entry\":10.2,\"ffl\":10.48,\"height\":0.28,\"steps\":1,\"parameter P\":\"10.480\",\"rampFrontCorners\":[10.0,10.2]}", Text(plot["levelRule"]));
            JObject floors = (JObject)plot["floors"];
            Assert.Equal(new[] { "generic", "yard", "ramp" }, floors.Properties().Select(item => item.Name));
            Assert.Equal("Generic 150mm", (string)floors["generic"]["type"]["value"]);
            Assert.Equal("{\"loops\":[{\"points\":[[0.0,0.0],[10.0,0.0],[10.0,10.0]],\"arcs\":[]}]}", Text(floors["generic"]["outline"]["value"]));
            Assert.Equal(10.489, (double)floors["generic"]["z"]["value"]);
            Assert.Equal(10.489, (double)floors["generic"]["heightOffset"]["value"]);
            Assert.Equal("DP2-1", (string)floors["yard"]["plotValue"]["value"]);
            Assert.Equal(10.389, (double)floors["yard"]["z"]["value"]);
            Assert.Equal(6.389, (double)floors["yard"]["heightOffset"]["value"]);
            Assert.Equal(4.0, (double)floors["yard"]["levelElevation"]["value"]);
            Assert.Equal("[[0.0,0.0,10.0],[10.0,0.0,10.2],[10.0,5.0,10.3],[0.0,5.0,10.1]]", Text(floors["ramp"]["corners"]["value"]));
            Assert.Equal(10.0, (double)floors["ramp"]["heightOffset"]["value"]);
            Assert.Equal(0.5, (double)floors["ramp"]["slopePercent"]["value"]);
            Assert.Equal("Ramps 0.5%", (string)floors["ramp"]["type"]["value"]);
            Assert.Equal("[\"S1\",\"S1\"]", Text(plot["roadSurfaces"]));
            Assert.Equal("{\"applied\":[\"mirrored\"],\"notApplied\":{}}", Text(plot["overrides"]));
            Assert.Equal("[{\"plot\":\"1\",\"edge\":1,\"plots\":{\"value\":[\"1\",\"2\"],\"source\":\"locationPlan\",\"status\":\"MEASURED\",\"statusFrom\":\"walls.plots from keyRules\"},"
                + "\"ends\":{\"value\":[[0,0],[10,0]],\"source\":\"keyRules\",\"status\":\"MEASURED\",\"statusFrom\":\"walls.ends from keyRules\"},"
                + "\"base\":{\"value\":10.389,\"source\":\"keyRules\",\"status\":\"MEASURED\",\"statusFrom\":\"walls.base from keyRules\"},"
                + "\"top\":{\"value\":12.189,\"source\":\"keyRules\",\"status\":\"MEASURED\",\"statusFrom\":\"walls.top from keyRules\"},\"basePlot\":\"1\",\"topPlot\":\"1\"}]", Text(plan["walls"]));
        }

        [Fact]
        public void R80_plan_lists_each_plot_not_written_in_missing_with_why_and_its_overrides()
        {
            JObject plan = BuildPlan.Assemble(Steps(), Inputs());
            Assert.Equal("[{\"plot\":\"2\",\"written\":false,\"missing\":[{\"key\":\"levels.ffl\",\"why\":\"levels.py gives no FFL\"},"
                + "{\"key\":\"parameters.Steps\",\"why\":\"the rule of keyRules gives no value for DT1\"}],"
                + "\"overrides\":{\"applied\":[],\"notApplied\":{\"heightM\":\"the plot is not written, it lacks levels.ffl\"}}},"
                + "{\"plot\":\"3\",\"written\":false,\"missing\":[{\"key\":\"skip\",\"why\":\"the skip list of the override list: sold (R386)\"}]}]", Text(plan["missing"]));
        }

        [Fact]
        public void R80_plan_summary_counts_the_plots_and_takes_the_table_of_the_summary_step()
        {
            JObject summary = (JObject)BuildPlan.Assemble(Steps(), Inputs())["summary"];
            Assert.Equal(3, (int)summary["plots"]);
            Assert.Equal(1, (int)summary["written"]);
            Assert.Equal(2, (int)summary["notWritten"]);
            Assert.Equal("{\"levels.ffl\":1,\"parameters.Steps\":1,\"skip\":1}", Text(summary["missingByKey"]));
            Assert.Equal("[\"3\"]", Text(summary["skipped"]));
            Assert.Equal(1, (int)summary["clusters"]);
            Assert.Equal(3, (int)summary["clusterPlots"]);
            Assert.Equal(2, (int)summary["inACluster"]);
            Assert.Equal("[\"2\"]", Text(summary["plotsWithNoRoadLevel"]));
            Assert.Equal("[{\"plot\":\"2\",\"corners\":[\"left\"],\"tried\":[\"S1\"],\"reason\":\"none\"}]", Text(summary["noRoadLevel"]));
            Assert.Equal("{\"a\":\"b\"}", Text(summary["roadFolderNotTaken"]));
            Assert.Equal("{\"within 5 mm\":1,\"other or empty\":1}", Text(summary["frontLevelAgainstPlotsCsv"]));
            Assert.Equal("{\"nearestCurb\":3}", Text(summary["frontRule"]));
            Assert.Equal("{\"walls\":1,\"byPlots\":{\"2\":1},\"statusByField\":{\"walls.plots\":{\"MEASURED\":1},\"walls.ends\":{\"MEASURED\":1},"
                + "\"walls.base\":{\"MEASURED weak\":1},\"walls.top\":{\"MEASURED\":1}}}", Text(summary["walls"]));
            JObject rules = (JObject)summary["rules"];
            Assert.Equal("parsons2024Rev6", (string)rules["levelProfile"]);
            Assert.Equal("[\"parsons\"]", Text(rules["overrideSource"]));
            Assert.Equal("[]", Text(rules["overrideModel"]));
            Assert.Equal("{\"mirrored\":1}", Text(rules["applied"]));
            Assert.Equal("{}", Text(rules["appliedOutsideBuild"]));
            Assert.True((bool)rules["keyRules"]);
            Assert.Equal("[\"x\"]", Text(rules["laterFields"]));
            Assert.Equal("{\"heightM\":{\"the plot is not written, it lacks levels.ffl\":{\"plots\":1,\"first\":[\"2\"]}}}", Text(rules["overridesNotAppliedWhy"]));
            Assert.Equal("{\"Zone 1\":{\"plots\":1,\"why\":\"pending\"}}", Text(rules["yardNotMeasured"]));
            Assert.Equal("{\"override\":1}", Text(rules["handRules"]));
            Assert.Equal("{\"from\":\"keyRules.steps\"}", Text(rules["steps"]));
            Assert.Equal("[{\"plot\":\"1\"}]", Text(plan_conflicts()));
        }

        private static JToken plan_conflicts()
        {
            return BuildPlan.Assemble(Steps(), Inputs())["conflicts"];
        }

        [Fact]
        public void R80_plan_yard_follows_the_hand_after_a_mirror_override()
        {
            // The rule hand found no yard outline and the override hand did, the type is the one yard type of floors.types
            Dictionary<string, JObject> steps = Steps();
            foreach (string side in new[] { "raw", "written" })
            {
                JObject floor = (JObject)steps["floors"]["plots"]["1"][side];
                floor["yardType"] = null;
                floor["yardWidth"] = 12.0;
                floor["lacks"] = JArray.Parse("[{\"key\":\"floors.yard\",\"why\":\"no fitted outline for DP2 not mirrored on a plot 12.0 m wide\"}]");
            }
            JObject plan = BuildPlan.Assemble(steps, Inputs());
            Assert.Equal("yard", (string)plan["plots"][0]["floors"]["yard"]["type"]["value"]);
            Assert.DoesNotContain(((JArray)plan["missing"]).OfType<JObject>(), item => (string)item["plot"] == "1");
            // The rule hand found a yard and the override hand finds none, the plot lacks it by the hand it is placed with
            steps = Steps();
            foreach (string side in new[] { "raw", "written" })
            {
                steps["overrides"]["plots"]["1"][side]["yardOutline"] = null;
                steps["overrides"]["plots"]["1"][side]["yardZ"] = null;
                steps["fronts"]["plots"]["1"][side]["length"] = 13.0;
            }
            plan = BuildPlan.Assemble(steps, Inputs());
            Assert.Equal("{\"plot\":\"1\",\"written\":true,\"missing\":[{\"key\":\"floors.yard\",\"why\":\"no fitted outline for DP2 mirrored on a plot 13.0 m wide\"}]}", Text(plan["missing"][0]));
        }

        [Fact]
        public void R80_plan_refuses_a_written_value_the_status_step_gives_no_source()
        {
            var failure = Assert.Throws<InvalidDataException>(() => BuildPlan.Assemble(Steps(false), Inputs()));
            Assert.Contains("plot 1", failure.Message);
            Assert.Contains("unit", failure.Message);
        }

        [Fact]
        public void R80_R347_plan_is_an_output_of_the_run_by_its_temporary_name_from_the_step_files_at_theirs()
        {
            string folder = Path.Combine(Path.GetTempPath(), "r80_plan_" + Guid.NewGuid().ToString("N"));
            string steps = Path.Combine(folder, "steps");
            Directory.CreateDirectory(steps);
            try
            {
                var request = new WorkerRequest { FormatVersion = 1, RunId = "r1", Command = "build", OutputFolder = folder, StepsFolder = steps, Inputs = new List<WorkerFile>() };
                var run = new WorkerContext(request, new WorkerAnswer { Issues = new List<string>(), Counts = new Dictionary<string, int>() }, null, () => 9000.0, () => 0.0);
                var files = new Dictionary<string, string>();
                foreach (KeyValuePair<string, JObject> step in Steps())
                {
                    files[step.Key] = run.WriteJson(steps, "step_" + step.Key + "_csharp.json", step.Value);
                }
                string plan = BuildPlan.Write(run, files, Inputs());
                Assert.Equal(Path.Combine(folder, "plan_021.json"), plan);
                Assert.False(File.Exists(plan));
                Assert.Contains(run.Written, item => item.Value == plan && item.Key == WorkFiles.TemporaryName(plan, "r1"));
                JObject written = JObject.Parse(File.ReadAllText(WorkFiles.TemporaryName(plan, "r1")));
                Assert.Equal("site-adaptation-plan", (string)written["format"]);
                Assert.Equal("r1", (string)written["run"]["runId"]);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R80_plan_names_the_nbh_of_its_scope()
        {
            PlanInputs inputs = Inputs();
            Assert.Equal("021", BuildPlan.NbhOf(new WorkerScope { Kind = "nbh", Nbh = "021" }, new[] { "1" }, inputs.Rows));
            Assert.Null(BuildPlan.NbhOf(new WorkerScope { Kind = "project" }, new[] { "1" }, inputs.Rows));
            Assert.Equal("021", BuildPlan.NbhOf(new WorkerScope { Kind = "plots", Plots = new List<string> { "1", "2" } }, new[] { "1", "2" }, inputs.Rows));
            inputs.Rows["2"]["nbhId"] = "022";
            Assert.Null(BuildPlan.NbhOf(new WorkerScope { Kind = "plots", Plots = new List<string> { "1", "2" } }, new[] { "1", "2" }, inputs.Rows));
        }
    }
}
