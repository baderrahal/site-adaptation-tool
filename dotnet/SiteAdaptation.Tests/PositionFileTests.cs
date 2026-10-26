using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CadReader;
using CSMath;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R353. The project position file of button 1 and its check points on made up key reads, key points and baseplan faces built in code
    // (R10), the C# side of python/tests/test_position_file.py on the same made up reads, on net48 and net8.0
    public sealed class PositionFileTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r353_" + Guid.NewGuid().ToString("N"));

        public PositionFileTests()
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

        private static readonly JObject SiteRules = JObject.Parse("{\"rule\": \"oneSet\", \"keys\": 7, \"oneSet\": true, \"angleToTrueNorthDeg\": 0, \"activeSite\": \"Internal\", \"sites\": [\"Internal\"], "
            + "\"levels\": [\"Level 1\", \"Level 2\"], \"levelStepsM\": {\"Level 2\": 4.0}, \"units\": {\"system\": \"METRIC\", \"length\": \"meters\", \"lengthSystem\": \"METRIC\"}, "
            + "\"levelBase\": \"Survey Point\", \"lengthAccuracy\": {\"imc\": 0.01, \"imd\": 0.01}}");

        private static readonly string[] Group = { "imc", "ima", "imb", "imd", "ime", "imf", "img" };

        private static Func<double, double, double, double[]> Shared(double angle)
        {
            double turn = angle * Math.PI / 180.0;
            return (x, y, z) => new[] { Math.Cos(turn) * x - Math.Sin(turn) * y + 1000.0, Math.Sin(turn) * x + Math.Cos(turn) * y + 2000.0, z + 5.0 };
        }

        // test_position_file.made_read: the internal origin at shared (1000, 2000, 5) turned by angle, the survey point and the project base
        // point on the internal origin, two levels 4 m apart on one Elevation Base
        private static JObject MadeRead(double angle = 0.0, string levelBase = "Survey Point")
        {
            double[] origin = Shared(angle)(0, 0, 0);
            return new JObject
            {
                ["tool"] = "RevitPrototypeReader", ["lengthUnit"] = "m", ["angleUnit"] = "deg",
                ["documents"] = new JArray(new JObject
                {
                    ["file"] = "key_TEST-RC", ["kind"] = "project",
                    ["surveyPoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = new JArray(origin) },
                    ["basePoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = new JArray(origin) },
                    ["angleToTrueNorthDeg"] = angle,
                    ["site"] = new JObject
                    {
                        ["activeSite"] = "Internal", ["sites"] = new JArray("Internal"), ["placeName"] = "x",
                        ["projectPosition"] = new JObject { ["eastWest"] = 1000.0, ["northSouth"] = 2000.0, ["elevation"] = 5.0, ["angleDeg"] = angle },
                        ["levels"] = new JArray(
                            new JObject { ["name"] = "Level 2", ["elementId"] = 2, ["elevation"] = 9.0, ["projectElevation"] = 4.0, ["elevationBase"] = "Survey Point" },
                            new JObject { ["name"] = "Level 1", ["elementId"] = 1, ["elevation"] = 5.0, ["projectElevation"] = 0.0, ["elevationBase"] = levelBase }),
                        ["units"] = new JObject { ["system"] = "METRIC", ["length"] = "meters", ["lengthSystem"] = "METRIC", ["lengthAccuracy"] = 0.01 },
                    },
                }),
            };
        }

        // test_position_file.FLOORS: the Generic floors of a made up key, internal corners of squares of 10 m, 2001 at the internal origin,
        // 2002 20 m east, 2003 20 m north, 2004 a yard floor, 2005 two Generic floors
        private static readonly object[][] Floors =
        {
            new object[] { "2001", "Generic 150mm", 0.0, 0.0 }, new object[] { "2002", "Generic 150mm", 20.0, 0.0 }, new object[] { "2003", "Generic 150mm", 0.0, 20.0 },
            new object[] { "2004", "yard", 40.0, 40.0 }, new object[] { "2005", "Generic 150mm", -30.0, -30.0 }, new object[] { "2005", "Generic 150mm", -30.0, -30.0 },
        };

        // test_position_file.made_points: testhost_<code>_points.json of a made up key at full precision
        private static JObject MadePoints(double angle = 0.0)
        {
            Func<double, double, double, double[]> shared = Shared(angle);
            var floors = new JArray();
            for (int index = 0; index < Floors.Length; index++)
            {
                double x0 = (double)Floors[index][2], y0 = (double)Floors[index][3];
                double[][] corners = { new[] { x0, y0, 0.15 }, new[] { x0 + 10.0, y0, 0.15 }, new[] { x0 + 10.0, y0 + 10.0, 0.15 }, new[] { x0, y0 + 10.0, 0.15 } };
                floors.Add(new JObject
                {
                    ["elementId"] = 100 + index, ["typeName"] = (string)Floors[index][1], ["plot"] = (string)Floors[index][0],
                    ["corners"] = new JArray(corners.Select(corner => new JArray(corner))), ["sharedCorners"] = new JArray(corners.Select(corner => new JArray(shared(corner[0], corner[1], corner[2])))),
                });
            }
            double[] origin = shared(0, 0, 0);
            return new JObject
            {
                ["format"] = KeyPointsFile.FormatText, ["formatVersion"] = 1, ["model"] = "key_TEST-KC.rvt", ["lengthUnit"] = "m", ["angleUnit"] = "deg",
                ["projectPosition"] = new JObject { ["eastWest"] = 1000.0, ["northSouth"] = 2000.0, ["elevation"] = 5.0, ["angleDeg"] = angle },
                ["surveyPoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = new JArray(origin) },
                ["basePoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = new JArray(origin) },
                ["floors"] = floors,
            };
        }

        // test_position_file.made_plots: the baseplan faces of the made up floors in site metres, labels 101 to 103, scaled and moved east
        private static JArray MadePlots(double scale = 1.0, double east = 0.0, double angle = 0.0)
        {
            double turn = angle * Math.PI / 180.0;
            var plots = new JArray();
            for (int index = 0; index < 3; index++)
            {
                double x0 = (double)Floors[index][2], y0 = (double)Floors[index][3];
                double[][] ring = { new[] { x0, y0 }, new[] { x0 + 10.0, y0 }, new[] { x0 + 10.0, y0 + 10.0 }, new[] { x0, y0 + 10.0 } };
                plots.Add(new JObject
                {
                    ["kind"] = "plot_shape", ["plot"] = (101 + index).ToString(System.Globalization.CultureInfo.InvariantCulture), ["type"] = "DP1",
                    ["vertices"] = new JArray(ring.Select(point => new JArray((Math.Cos(turn) * point[0] - Math.Sin(turn) * point[1] + 1000.0) * scale + east,
                        (Math.Sin(turn) * point[0] + Math.Cos(turn) * point[1] + 2000.0) * scale))),
                });
            }
            return plots;
        }

        private static Dictionary<string, Dictionary<string, object>> MadeCheck(ProjectPositionFile position, JArray plots, JObject points = null)
        {
            return ProjectPositionFile.CheckPoints(position, new List<KeyValuePair<string, JObject>> { new KeyValuePair<string, JObject>("imc", points ?? MadePoints()) },
                LocationPlan.Faces(plots, 0.000001), new Dictionary<string, List<string>> { ["imc"] = new List<string> { "Generic 150mm" } }, 1.0, 2);
        }

        [Fact]
        public void R353_the_position_file_is_the_site_of_a_key_read_and_holds_every_field_of_key_rules_site()
        {
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            Assert.Equal("imc", position.Key);
            Assert.Equal("key_TEST-RC", position.Model);
            Assert.Equal(new double?[] { 1000.0, 2000.0, 5.0, 0.0 }, new[] { position.Position.EastM, position.Position.NorthM, position.Position.ElevationM, position.Position.AngleDeg });
            Assert.Equal(new[] { 1000.0, 2000.0, 5.0 }, position.SurveyShared);
            Assert.Equal(new[] { "Level 1", "Level 2" }, position.Levels.Select(level => level.Name));
            Assert.Equal("Survey Point", position.LevelBase);
            Assert.Equal("Internal|Internal|x|0.01", position.ActiveSite + "|" + string.Join(",", position.Sites.Select(item => (string)item)) + "|" + position.PlaceName + "|" + position.LengthAccuracy.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Empty(ProjectPositionFile.AgainstRules(position, SiteRules));
            var changes = new List<Tuple<string, JToken>>
            {
                Tuple.Create("levelStepsM", (JToken)new JObject { ["Level 2"] = 4.001 }), Tuple.Create("levelBase", (JToken)"Project Base Point"),
                Tuple.Create("activeSite", (JToken)"Site 2"), Tuple.Create("sites", (JToken)new JArray("Internal", "Site 2")),
                Tuple.Create("units", (JToken)new JObject { ["system"] = "METRIC", ["length"] = "feet", ["lengthSystem"] = "METRIC" }),
                Tuple.Create("lengthAccuracy", (JToken)new JObject { ["imc"] = 0.001 }), Tuple.Create("oneSet", (JToken)false), Tuple.Create("angleToTrueNorthDeg", (JToken)0.5),
            };
            foreach (Tuple<string, JToken> change in changes)
            {
                var rules = (JObject)SiteRules.DeepClone();
                rules[change.Item1] = change.Item2;
                List<string> found = ProjectPositionFile.AgainstRules(position, rules);
                Assert.True(found.Count == 1 && found[0].Contains(change.Item1), change.Item1 + ": " + string.Join("; ", found));
            }
            JObject written = position.Written();
            Assert.Equal(1000.0, (double)written["projectPosition"]["eastM"]);
            Assert.Equal("Internal", (string)written["activeSite"]);
            Assert.Equal("[\"Internal\"]", written["sites"].ToString(Newtonsoft.Json.Formatting.None));
        }

        [Fact]
        public void R353_a_key_read_with_no_site_a_part_not_read_or_two_level_bases_is_refused()
        {
            Assert.Contains("Elevation Bases", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead(levelBase: "Project Base Point")), "imc")).Message);
            JObject noSite = MadeRead();
            ((JObject)noSite["documents"][0]).Remove("site");
            Assert.Contains("no site facts", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.From(ProjectPositionFile.DocumentOf(noSite), "imc")).Message);
            JObject notRead = MadeRead();
            notRead["documents"][0]["site"]["levels"] = null;
            notRead["documents"][0]["site"]["notRead"] = new JObject { ["levels"] = "an error" };
            Assert.Contains("no levels", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.From(ProjectPositionFile.DocumentOf(notRead), "imc")).Message);
            JObject feet = MadeRead();
            feet["lengthUnit"] = "ft";
            Assert.Contains("m and deg", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.DocumentOf(feet)).Message);
            JObject text = MadeRead();
            text["documents"][0]["surveyPoint"]["position"][1] = "0";
            Assert.Contains("surveyPoint.position", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.From(ProjectPositionFile.DocumentOf(text), "imc")).Message);
        }

        private static Dictionary<string, JObject> Others()
        {
            return Group.Skip(1).ToDictionary(key => key, key => ProjectPositionFile.DocumentOf(MadeRead()));
        }

        [Fact]
        public void R353_the_reference_key_is_held_against_every_other_key_of_its_group_and_a_set_that_disagrees_is_refused()
        {
            JObject reference = ProjectPositionFile.DocumentOf(MadeRead());
            Assert.Empty(ProjectPositionFile.SetDiffers(reference, Others(), "imc", Group));
            // The Native set: its survey point and project position 2432 km off the Parsons one in shared coordinates
            JObject native = MadeRead();
            JToken document = native["documents"][0];
            document["surveyPoint"]["sharedPosition"][0] = (double)document["surveyPoint"]["sharedPosition"][0] + 2432000.0;
            document["basePoint"]["sharedPosition"][0] = (double)document["basePoint"]["sharedPosition"][0] + 2432000.0;
            document["site"]["projectPosition"]["eastWest"] = (double)document["site"]["projectPosition"]["eastWest"] + 2432000.0;
            Dictionary<string, JObject> others = Others();
            others["imb"] = ProjectPositionFile.DocumentOf(native);
            List<string> found = ProjectPositionFile.SetDiffers(reference, others, "imc", Group);
            Assert.NotEmpty(found);
            Assert.All(found, line => Assert.StartsWith("imb ", line));
            Assert.Contains(found, line => line.Contains("surveyPoint.sharedPosition 2432000000"));
            Assert.Contains(found, line => line.Contains("projectPosition"));
            // A level 1 mm off, another site name and a key of the group not given are each named
            JObject level = MadeRead();
            level["documents"][0]["site"]["levels"][0]["elevation"] = 9.001;
            level["documents"][0]["site"]["activeSite"] = "Site 2";
            others = Others();
            others["imd"] = ProjectPositionFile.DocumentOf(level);
            Assert.Equal(new[] { "imd activeSite Site 2", "imd levels.Level 2 1.0 mm" }, ProjectPositionFile.SetDiffers(reference, others, "imc", Group).OrderBy(line => line, StringComparer.Ordinal));
            others = Others();
            others.Remove("img");
            Assert.Equal(new[] { "img no read, every key of the group is held against the reference" }, ProjectPositionFile.SetDiffers(reference, others, "imc", Group));
        }

        [Fact]
        public void R353_the_check_points_hold_the_baseplan_faces_against_the_floor_corners_of_the_keys_through_the_position()
        {
            foreach (double angle in new[] { 0.0, 30.0 })
            {
                ProjectPositionFile turned = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead(angle)), "imc");
                Dictionary<string, Dictionary<string, object>> rows = MadeCheck(turned, MadePlots(angle: angle), MadePoints(angle));
                // The extreme plots by the mean of their corners, 2001 least east and north, 2002 most east, 2003 most north, four corners each
                Assert.Equal(new[] { "imc 2001 101", "imc 2002 102", "imc 2003 103" }, rows.Values.Select(row => row["key"] + " " + row["plot"] + " " + row["face"]).Distinct().OrderBy(text => text));
                Assert.Equal(new[] { "imc/2001/0", "imc/2001/1", "imc/2001/2", "imc/2001/3" }, rows.Keys.Take(4));
                Assert.Equal(12, rows.Count);
                Assert.All(rows.Values, row => Assert.True((double)row["gapMm"] < 1e-6 && (double)row["revitGapMm"] < 1e-6 && (bool)row["pass"]));
                Assert.Equal(new[] { 30.0, 0.0 }, (double[])rows["imc/2002/1"]["keyInternalM"]);
                double[] internalPoint = (double[])rows["imc/2002/1"]["internalM"];
                Assert.Equal(30.0, internalPoint[0], 9);
                Assert.Equal(0.0, internalPoint[1], 9);
            }
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            // A baseplan 0.6 mm and 1.2 mm east of the keys: every corner by that much, a pass and a fail at 1 mm
            foreach (Tuple<double, bool> off in new[] { Tuple.Create(0.0006, true), Tuple.Create(0.0012, false) })
            {
                Assert.All(MadeCheck(position, MadePlots(east: off.Item1)).Values, row =>
                {
                    Assert.Equal(off.Item1 * 1000.0, (double)row["gapMm"], 6);
                    Assert.Equal(off.Item2, (bool)row["pass"]);
                });
            }
            // A baseplan drawn in feet and one 1000 km off: no face holds a floor of the keys, every check point fails
            foreach (JArray plots in new[] { MadePlots(scale: 1 / 0.3048), MadePlots(east: 1000000.0) })
            {
                Dictionary<string, Dictionary<string, object>> rows = MadeCheck(position, plots);
                Assert.Equal(12, rows.Count);
                Assert.All(rows.Values, row => Assert.True(row["face"] == null && row["gapMm"] == null && false.Equals(row["pass"])));
            }
            JObject empty = MadePoints();
            empty["floors"] = new JArray();
            Assert.Contains("no Generic floor", Assert.Throws<InvalidDataException>(() => MadeCheck(position, MadePlots(), empty)).Message);
        }

        [Fact]
        public void R353_config_names_the_position_file_its_limit_the_key_of_the_read_and_its_group()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            PositionSettings settings = PositionSettings.From(config);
            Assert.Equal(1.0, settings.CheckPointLimitMm);
            Assert.Equal(2, settings.GapDecimals);
            Assert.EndsWith(".json", settings.File);
            Assert.Equal("imc", settings.Key);
            Assert.Equal(Group, settings.Group);
            Assert.Equal(new[] { "FFL" }, ProjectPositionFile.GenericTypes(config, "ime"));
            Assert.Equal(new[] { "Generic 150mm" }, ProjectPositionFile.GenericTypes(config, "imc"));
            config["addin"]["position"]["gapDecimals"] = 7;
            Assert.Throws<InvalidDataException>(() => PositionSettings.From(config));
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        private string Write(string name, JToken value)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, value.ToString());
            return path;
        }

        // test_position_file.made_plots as a baseplan drawing in meters: the three faces on the plot line layer, each with its label inside
        private string Baseplan()
        {
            var document = new CadDocument();
            document.Header.InsUnits = UnitsType.Meters;
            var line = new Layer("MP_PLOT_LINE");
            var text = new Layer("MP_PLOT_TEXT");
            document.Layers.Add(line);
            document.Layers.Add(text);
            foreach (JToken plot in MadePlots())
            {
                List<double[]> ring = plot["vertices"].Select(point => new[] { (double)point[0], (double)point[1] }).ToList();
                for (int corner = 0; corner < ring.Count; corner++)
                {
                    double[] from = ring[corner], to = ring[(corner + 1) % ring.Count];
                    document.Entities.Add(new Line { StartPoint = new XYZ(from[0], from[1], 0), EndPoint = new XYZ(to[0], to[1], 0), Layer = line });
                }
                document.Entities.Add(new MText { Value = "DP1\\P" + (string)plot["plot"], InsertPoint = new XYZ(ring[0][0] + 5, ring[0][1] + 5, 0), Layer = text });
            }
            string path = Path.Combine(folder, "baseplan.dwg");
            using (var writer = new DwgWriter(path, document))
            {
                writer.Write();
            }
            return path;
        }

        private WorkerAnswer RunPosition(string id, JObject read, List<string> steps, out int code, IEnumerable<string> others, params WorkerFile[] more)
        {
            string config = Path.Combine(folder, "project.json");
            if (!File.Exists(config))
            {
                File.Copy(WorkerRunTests.RepoConfig(), config);
            }
            string rules = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(WorkerRunTests.RepoConfig())), "rules", "roshn-makkah.json");
            var inputs = new List<WorkerFile> { Input("config", config), Input("rules", rules), Input(ProjectPositionFile.KeyReadRole, Write("read_" + id + ".json", read)) };
            inputs.AddRange(others.Select(key => Input(ProjectPositionFile.KeyReadRole + ":" + key, Write("read_" + id + "_" + key + ".json", MadeRead()))));
            inputs.AddRange(more);
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = id, Command = "position", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = inputs, Scope = new WorkerScope { Kind = "project" }, OutputFolder = Path.Combine(folder, "out"), Steps = steps,
                StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, id));
        }

        [Fact]
        public void R353_the_command_position_writes_the_file_its_steps_and_the_check_points_and_refuses_a_set_that_disagrees()
        {
            int code;
            string layers = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json");
            WorkerAnswer answer = RunPosition("p1", MadeRead(), new List<string> { ProjectPositionFile.PositionStep, ProjectPositionFile.CheckPointsStep }, out code, Group.Skip(1),
                Input("baseplan", Baseplan()), Input("cadLayers", layers), Input(ProjectPositionFile.KeyPointsRole + ":imc", Write("points_imc.json", MadePoints())));
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            string name = Path.GetFileName(PositionSettings.From(JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))).File);
            JObject written = JObject.Parse(File.ReadAllText(Path.Combine(folder, "out", name)));
            Assert.Equal("Survey Point", (string)written["levelBase"]);
            Assert.Equal("imc", (string)written["key"]);
            Assert.Equal("Internal", (string)written["activeSite"]);
            JObject step = JObject.Parse(File.ReadAllText(Path.Combine(folder, "steps", StepFile.FileName(ProjectPositionFile.PositionStep, "csharp"))));
            Assert.Equal(new[] { "Level 1", "Level 2" }, step["plots"]["position"]["raw"]["levelNames"].Select(item => (string)item));
            Assert.Equal(Group.Skip(1), step["plots"]["position"]["raw"]["heldAgainst"].Select(item => (string)item));
            JObject points = JObject.Parse(File.ReadAllText(Path.Combine(folder, "steps", StepFile.FileName(ProjectPositionFile.CheckPointsStep, "csharp"))));
            Assert.Equal(12, ((JObject)points["plots"]).Count);
            Assert.All(((JObject)points["plots"]).Properties(), item => Assert.True((bool)item.Value["raw"]["pass"]));
            Assert.Equal(12, answer.Counts["check points that pass"]);
            Assert.Equal(new[] { "keyRead", "rules", "keyRead:*" }, WorkerRunner.Steps[ProjectPositionFile.PositionStep]);
            Assert.Equal(new[] { "keyRead", "rules", "baseplan", "keyPoints:*" }, WorkerRunner.Steps[ProjectPositionFile.CheckPointsStep]);
            Assert.Equal("position", WorkerRunner.StepCommand[ProjectPositionFile.CheckPointsStep]);
            JObject off = MadeRead();
            off["documents"][0]["site"]["levels"][0]["elevation"] = 9.5;
            answer = RunPosition("p2", off, new List<string>(), out code, Group.Skip(1));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("levelStepsM", answer.Message);
            // A set with a key left out and a set with a read of the Native keys are refused before anything is written
            answer = RunPosition("p3", MadeRead(), new List<string>(), out code, Group.Skip(1).Take(5));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("img no read", answer.Message);
            JObject native = MadeRead();
            native["documents"][0]["surveyPoint"]["sharedPosition"][0] = 2433000.0;
            answer = RunPosition("p4", MadeRead(), new List<string>(), out code, Group.Skip(1).Take(5), Input(ProjectPositionFile.KeyReadRole + ":img", Write("native.json", native)));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("img surveyPoint.sharedPosition", answer.Message);
            Assert.False(File.Exists(Path.Combine(folder, "out", "checkPoints_p4.json")));
        }
    }
}
