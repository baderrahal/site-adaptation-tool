using System;
using System.Collections.Generic;
using System.Globalization;
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
    // R353. The project position file of button 1 and its check points on made up key reads, key points, baseplan faces and location plan
    // numbers built in code (R10), the C# side of python/tests/test_position_file.py on the same made up inputs, on net48 and net8.0
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

        // test_position_file.COPIES: the copy of each made up answer key, its reads named <copy stem>_TEST-<job>
        private static readonly Dictionary<string, string> Copies = Group.ToDictionary(key => key, key => "copy_" + key);

        private static string Model(string key, string job = null)
        {
            return Copies[key] + "_TEST-" + (job ?? "R" + key.Substring(2).ToUpperInvariant());
        }

        private static Func<double, double, double, double[]> Shared(double angle)
        {
            double turn = angle * Math.PI / 180.0;
            return (x, y, z) => new[] { Math.Cos(turn) * x - Math.Sin(turn) * y + 1000.0, Math.Sin(turn) * x + Math.Cos(turn) * y + 2000.0, z + 5.0 };
        }

        // test_position_file.made_read: the internal origin at shared (1000, 2000, 5) turned by angle, the survey point and the project base
        // point on the internal origin, two levels 4 m apart on one Elevation Base
        private static JObject MadeRead(double angle = 0.0, string levelBase = "Survey Point", string key = "imc")
        {
            double[] origin = Shared(angle)(0, 0, 0);
            return new JObject
            {
                ["tool"] = "RevitPrototypeReader", ["lengthUnit"] = "m", ["angleUnit"] = "deg",
                ["documents"] = new JArray(new JObject
                {
                    ["file"] = Model(key), ["kind"] = "project",
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

        // test_position_file.documents: the made up reads of the 7 keys as documents, change applied to each
        private static Dictionary<string, JObject> Documents(Action<string, JObject> change = null)
        {
            var found = new Dictionary<string, JObject>();
            foreach (string key in Group)
            {
                JObject document = ProjectPositionFile.DocumentOf(MadeRead(key: key));
                change?.Invoke(key, document);
                found[key] = document;
            }
            return found;
        }

        // test_position_file.FLOORS: the Generic floors of a made up key, internal corners of squares of 10 m, 2001 at the internal origin,
        // 2002 20 m east, 2003 20 m north, 2004 a yard floor, 2005 two Generic floors, and a Generic floor of no plot 80 m east
        private static readonly object[][] Floors =
        {
            new object[] { "2001", "Generic 150mm", 0.0, 0.0 }, new object[] { "2002", "Generic 150mm", 20.0, 0.0 }, new object[] { "2003", "Generic 150mm", 0.0, 20.0 },
            new object[] { "2004", "yard", 40.0, 40.0 }, new object[] { "2005", "Generic 150mm", -30.0, -30.0 }, new object[] { "2005", "Generic 150mm", -30.0, -30.0 },
            new object[] { null, "Generic 150mm", 80.0, 0.0 },
        };

        // test_position_file.NUMBERS: the location plan numbers of the made up faces
        private static readonly Dictionary<string, string> Numbers = new Dictionary<string, string> { ["2001"] = "101", ["2002"] = "102", ["2003"] = "103" };

        // test_position_file.made_points: testhost_<code>_points.json of a made up key at full precision
        private static JObject MadePoints(double angle = 0.0, string key = "imc", JArray floors = null)
        {
            Func<double, double, double, double[]> shared = Shared(angle);
            if (floors == null)
            {
                floors = new JArray();
                for (int index = 0; index < Floors.Length; index++)
                {
                    double x0 = (double)Floors[index][2], y0 = (double)Floors[index][3];
                    double[][] corners = { new[] { x0, y0, 0.15 }, new[] { x0 + 10.0, y0, 0.15 }, new[] { x0 + 10.0, y0 + 10.0, 0.15 }, new[] { x0, y0 + 10.0, 0.15 } };
                    floors.Add(new JObject
                    {
                        ["elementId"] = 100 + index, ["typeName"] = (string)Floors[index][1], ["plot"] = Floors[index][0] == null ? JValue.CreateNull() : (JToken)(string)Floors[index][0],
                        ["corners"] = new JArray(corners.Select(corner => new JArray(corner))), ["sharedCorners"] = new JArray(corners.Select(corner => new JArray(shared(corner[0], corner[1], corner[2])))),
                    });
                }
            }
            double[] origin = shared(0, 0, 0);
            return new JObject
            {
                ["format"] = KeyPointsFile.FormatText, ["formatVersion"] = 1, ["model"] = Model(key, "K" + key.Substring(2).ToUpperInvariant()) + ".rvt", ["lengthUnit"] = "m", ["angleUnit"] = "deg",
                ["projectPosition"] = new JObject { ["eastWest"] = 1000.0, ["northSouth"] = 2000.0, ["elevation"] = 5.0, ["angleDeg"] = angle },
                ["surveyPoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = new JArray(origin) },
                ["basePoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = new JArray(origin) },
                ["floors"] = floors,
            };
        }

        // test_position_file.made_plots: the baseplan faces of the made up floors in site metres, labels 101 to 103, scaled and moved east,
        // with the merge of each vertex of CadReader --exact, 0 unless merges names those of a label
        private static JArray MadePlots(double scale = 1.0, double east = 0.0, double angle = 0.0, Dictionary<string, double[]> merges = null)
        {
            double turn = angle * Math.PI / 180.0;
            var plots = new JArray();
            for (int index = 0; index < 3; index++)
            {
                double x0 = (double)Floors[index][2], y0 = (double)Floors[index][3];
                double[][] ring = { new[] { x0, y0 }, new[] { x0 + 10.0, y0 }, new[] { x0 + 10.0, y0 + 10.0 }, new[] { x0, y0 + 10.0 } };
                plots.Add(new JObject
                {
                    ["kind"] = "plot_shape", ["plot"] = (101 + index).ToString(CultureInfo.InvariantCulture), ["type"] = "DP1",
                    ["vertices"] = new JArray(ring.Select(point => new JArray((Math.Cos(turn) * point[0] - Math.Sin(turn) * point[1] + 1000.0) * scale + east,
                        (Math.Sin(turn) * point[0] + Math.Cos(turn) * point[1] + 2000.0) * scale))),
                    ["vertexMergeM"] = new JArray((merges != null && merges.TryGetValue((101 + index).ToString(CultureInfo.InvariantCulture), out double[] own) ? own : new double[ring.Length]).Select(value => (object)value).ToArray()),
                });
            }
            return plots;
        }

        // test_position_file.numbered: a numbering of the build by a table of the faces of each number
        private static Func<string, string, IReadOnlyList<string>> Numbered(IDictionary<string, string[]> numbers)
        {
            return (key, plot) => numbers.TryGetValue(plot, out string[] faces) ? faces : new string[0];
        }

        private static Dictionary<string, string[]> Single(IDictionary<string, string> numbers)
        {
            return numbers.ToDictionary(item => item.Key, item => new[] { item.Value });
        }

        private static Dictionary<string, Dictionary<string, object>> MadeCheck(ProjectPositionFile position, JArray plots, JObject points = null, Func<string, string, IReadOnlyList<string>> numbering = null,
            IEnumerable<string> keys = null)
        {
            Dictionary<string, JObject> byKey = (keys ?? new[] { "imc" }).ToDictionary(key => key, key => points ?? MadePoints(key: key));
            return ProjectPositionFile.CheckPoints(position, byKey, Group, LocationPlan.Faces(plots, 0.000001), Group.ToDictionary(key => key, key => new List<string> { "Generic 150mm" }),
                numbering ?? Numbered(Single(Numbers)), 1.0, 2, ProjectPositionFile.MergesOf(plots));
        }

        [Fact]
        public void R353_the_position_file_is_the_site_of_a_key_read_and_holds_every_field_of_key_rules_site()
        {
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            Assert.Equal("imc", position.Key);
            Assert.Equal(Model("imc"), position.Model);
            Assert.Equal(new double?[] { 1000.0, 2000.0, 5.0, 0.0 }, new[] { position.Position.EastM, position.Position.NorthM, position.Position.ElevationM, position.Position.AngleDeg });
            Assert.Equal(new[] { 1000.0, 2000.0, 5.0 }, position.SurveyShared);
            Assert.Equal(new[] { "Level 1", "Level 2" }, position.Levels.Select(level => level.Name));
            Assert.Equal("Survey Point", position.LevelBase);
            Assert.Equal("Internal|Internal|x|0.01", position.ActiveSite + "|" + string.Join(",", position.Sites.Select(item => (string)item)) + "|" + position.PlaceName + "|" + position.LengthAccuracy.Value.ToString("R", CultureInfo.InvariantCulture));
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

        [Fact]
        public void R353_the_reference_key_is_held_against_every_other_key_of_its_group_and_a_set_that_disagrees_is_refused()
        {
            Assert.Empty(ProjectPositionFile.SetDiffers(Documents(), "imc", Group, Copies));
            // The Native set: the read of IMB with its survey point and project position 2432 km off the Parsons one in shared coordinates
            List<string> found = ProjectPositionFile.SetDiffers(Documents((key, document) =>
            {
                if (key == "imb")
                {
                    document["surveyPoint"]["sharedPosition"][0] = (double)document["surveyPoint"]["sharedPosition"][0] + 2432000.0;
                    document["basePoint"]["sharedPosition"][0] = (double)document["basePoint"]["sharedPosition"][0] + 2432000.0;
                    document["site"]["projectPosition"]["eastWest"] = (double)document["site"]["projectPosition"]["eastWest"] + 2432000.0;
                }
            }), "imc", Group, Copies);
            Assert.NotEmpty(found);
            Assert.All(found, line => Assert.StartsWith("imb ", line));
            Assert.Contains(found, line => line.Contains("surveyPoint.sharedPosition 2432000000"));
            Assert.Contains(found, line => line.Contains("projectPosition"));
            // A level 1 mm off and another site name are each named, a key of the group not given too
            Assert.Equal(new[] { "imd activeSite Site 2", "imd levels.Level 2 1.0 mm" }, ProjectPositionFile.SetDiffers(Documents((key, document) =>
            {
                if (key == "imd")
                {
                    document["site"]["levels"][0]["elevation"] = 9.001;
                    document["site"]["activeSite"] = "Site 2";
                }
            }), "imc", Group, Copies).OrderBy(line => line, StringComparer.Ordinal));
            Dictionary<string, JObject> missing = Documents();
            missing.Remove("img");
            Assert.Equal(new[] { "img no read, every key of the group is held against the reference" }, ProjectPositionFile.SetDiffers(missing, "imc", Group, Copies));
        }

        [Fact]
        public void R353_every_value_of_the_set_missing_or_not_read_in_a_key_read_is_refused_naming_the_key_and_the_field()
        {
            // The inputs of test_position_file, the same lines: a read of no site, no survey point, no base point and no angle, a read whose
            // project position is not read, one with no eastWest, one with no levels, no place and no units, and the IMC read given for IMA
            Assert.Equal(new[]
            {
                "ima surveyPoint.position missing", "ima surveyPoint.sharedPosition missing", "ima basePoint.position missing", "ima basePoint.sharedPosition missing",
                "ima angleToTrueNorthDeg missing", "ima site missing",
            }, ProjectPositionFile.SetDiffers(Documents((key, document) =>
            {
                if (key == "ima")
                {
                    foreach (string name in new[] { "site", "surveyPoint", "basePoint", "angleToTrueNorthDeg" })
                    {
                        document.Remove(name);
                    }
                }
            }), "imc", Group, Copies));
            Assert.Equal(new[]
            {
                "ima site.projectPosition not read", "imb site.projectPosition.eastWest missing", "imd site.levels missing", "imd site.placeName missing",
                "imd site.units.system missing", "imd site.units.length missing", "imd site.units.lengthSystem missing", "imd site.units.lengthAccuracy missing",
            }, ProjectPositionFile.SetDiffers(Documents((key, document) =>
            {
                if (key == "ima")
                {
                    document["site"]["projectPosition"] = JValue.CreateNull();
                    document["site"]["notRead"] = new JObject { ["projectPosition"] = "an error" };
                }
                if (key == "imb")
                {
                    ((JObject)document["site"]["projectPosition"]).Remove("eastWest");
                }
                if (key == "imd")
                {
                    document["site"]["levels"] = new JArray();
                    document["site"]["units"] = new JObject();
                    ((JObject)document["site"]).Remove("placeName");
                }
            }), "imc", Group, Copies));
            Assert.Equal(new[] { "ima model " + Model("imc") + " is not a work copy copy_ima_TEST-<job> of its answer key" }, ProjectPositionFile.SetDiffers(Documents((key, document) =>
            {
                if (key == "ima")
                {
                    document["file"] = Model("imc");
                }
            }), "imc", Group, Copies));
        }

        [Fact]
        public void R353_the_check_points_hold_the_baseplan_faces_against_the_floor_corners_of_the_keys_through_the_position()
        {
            foreach (double angle in new[] { 0.0, 30.0 })
            {
                ProjectPositionFile turned = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead(angle)), "imc");
                Dictionary<string, Dictionary<string, object>> rows = MadeCheck(turned, MadePlots(angle: angle), MadePoints(angle));
                // The extreme plots by the mean of their corners, the floor of no plot never taken, and the 6 other keys listed apart
                var taken = rows.Where(row => row.Value["plot"] != null).ToDictionary(row => row.Key, row => row.Value);
                Assert.Equal(new[] { "imc 2001 101 101", "imc 2002 102 102", "imc 2003 103 103" },
                    taken.Values.Select(row => row["key"] + " " + row["plot"] + " " + row["face"] + " " + row["expectedFace"]).Distinct().OrderBy(text => text));
                Assert.Equal(new[] { "imc/2001/0", "imc/2001/1", "imc/2001/2", "imc/2001/3" }, rows.Keys.Take(4));
                Assert.Equal(12, taken.Count);
                Assert.All(taken.Values, row => Assert.True((double)row["gapMm"] < 1e-6 && (double)row["revitGapMm"] < 1e-6 && (bool)row["pass"] && row["why"] == null));
                Assert.Equal(Group.Skip(1).Select(key => key + " no key points False"), rows.Where(row => row.Value["plot"] == null).Select(row => row.Key + " " + row.Value["why"] + " " + row.Value["pass"]));
                Assert.Equal(new[] { 30.0, 0.0 }, (double[])taken["imc/2002/1"]["keyInternalM"]);
                Assert.Equal(30.0, ((double[])taken["imc/2002/1"]["internalM"])[0], 9);
            }
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            foreach (Tuple<double, string> off in new[] { Tuple.Create(0.0006, (string)null), Tuple.Create(0.0012, "gap over the limit") })
            {
                Assert.All(MadeCheck(position, MadePlots(east: off.Item1), keys: Group).Values, row =>
                {
                    Assert.Equal(off.Item1 * 1000.0, (double)row["gapMm"], 6);
                    Assert.Equal(off.Item2, (string)row["why"]);
                    Assert.Equal(off.Item2 == null, (bool)row["pass"]);
                });
            }
            // A baseplan drawn in feet, one 1000 km off and one in mm with a header in metres: no face holds a floor, every point fails
            foreach (JArray plots in new[] { MadePlots(scale: 1 / 0.3048), MadePlots(east: 1000000.0), MadePlots(scale: 1000.0) })
            {
                Dictionary<string, Dictionary<string, object>> rows = MadeCheck(position, plots, keys: Group);
                Assert.Equal(84, rows.Count);
                Assert.All(rows.Values, row => Assert.True(row["face"] == null && row["gapMm"] == null && (string)row["why"] == "no face" && false.Equals(row["pass"])));
            }
            JObject empty = MadePoints();
            empty["floors"] = new JArray();
            Assert.Contains("no Generic floor", Assert.Throws<InvalidDataException>(() => MadeCheck(position, MadePlots(), empty)).Message);
        }

        [Fact]
        public void R353_a_point_on_the_face_of_another_plot_fails_whatever_its_gap()
        {
            // test_position_file: a row of identical plots and a baseplan whose faces and labels lie one plot east of the keys
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            var floors = new JArray();
            double[] starts = { 0.0, 10.0, 20.0, 30.0 };
            for (int index = 0; index < starts.Length; index++)
            {
                double x0 = starts[index];
                double[][] corners = { new[] { x0, 0.0, 0.15 }, new[] { x0 + 10.0, 0.0, 0.15 }, new[] { x0 + 10.0, 10.0, 0.15 }, new[] { x0, 10.0, 0.15 } };
                floors.Add(new JObject
                {
                    ["elementId"] = index, ["typeName"] = "Generic 150mm", ["plot"] = (2001 + index).ToString(CultureInfo.InvariantCulture), ["corners"] = new JArray(corners.Select(corner => new JArray(corner))),
                    ["sharedCorners"] = new JArray(corners.Select(corner => new JArray(corner[0] + 1000.0, corner[1] + 2000.0, corner[2] + 5.0))),
                });
            }
            var plots = new JArray();
            double[] faces = { -10.0, 0.0, 10.0, 20.0, 30.0, 40.0 };
            for (int index = 0; index < faces.Length; index++)
            {
                double x0 = faces[index];
                plots.Add(new JObject
                {
                    ["kind"] = "plot_shape", ["plot"] = (100 + index).ToString(CultureInfo.InvariantCulture),
                    ["vertices"] = new JArray(new JArray(x0 + 1010.0, 2000.0), new JArray(x0 + 1020.0, 2000.0), new JArray(x0 + 1020.0, 2010.0), new JArray(x0 + 1010.0, 2010.0)),
                });
            }
            var numbers = new Dictionary<string, string> { ["2001"] = "101", ["2002"] = "102", ["2003"] = "103", ["2004"] = "104" };
            Dictionary<string, Dictionary<string, object>> rows = MadeCheck(position, plots, MadePoints(floors: floors), Numbered(Single(numbers)));
            List<Dictionary<string, object>> taken = rows.Values.Where(row => row["plot"] != null).ToList();
            Assert.Equal(8, taken.Count);
            Assert.All(taken, row => Assert.True((double)row["gapMm"] < 1e-6 && (string)row["why"] == "face of another plot" && false.Equals(row["pass"])));
            Assert.Equal(new[] { "2001 100 101", "2004 103 104" }, taken.Select(row => row["plot"] + " " + row["face"] + " " + row["expectedFace"]).Distinct().OrderBy(text => text));
            Dictionary<string, object> summary = ProjectPositionFile.Summary(rows, 1.0);
            Assert.Equal("14 0 8 6 True", string.Join(" ", summary["points"], summary["pass"], summary["otherFace"], summary["noPoints"], summary["failed"]));
            Assert.Equal(new[] { "no face numbered" }, MadeCheck(position, MadePlots(), numbering: (key, plot) => new string[0]).Values.Where(row => row["plot"] != null).Select(row => (string)row["why"]).Distinct());
            Func<string, string, IReadOnlyList<string>> shifts = ProjectPositionFile.Numbering(new List<Dictionary<string, string>>(), new Dictionary<string, int> { ["IMC"] = 10 });
            Assert.Equal(new[] { "101" }, shifts("imc", "111"));
            Assert.Empty(shifts("ima", "111"));
            Assert.Equal(10, ProjectPositionFile.Shifts(JObject.Parse("{\"neighbourhoods\": {\"IMC\": {\"shift\": 10}}}"))["IMC"]);
            Assert.Contains("IMC a shift that is no whole number", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.Shifts(JObject.Parse("{\"neighbourhoods\": {\"IMC\": {\"shift\": 10.0}}}"))).Message);
        }

        private static Dictionary<string, string> Label(string number, string kind, string face)
        {
            return new Dictionary<string, string> { ["number"] = number, ["kind"] = kind, ["face"] = face };
        }

        [Fact]
        public void R353_only_a_plot_label_numbers_a_face_and_a_number_on_two_faces_fails_its_point_never_the_shift()
        {
            // test_position_file: rows of the client code kind and of the other kinds, a number on two faces, an empty face and the shift of IMC
            var labels = new List<Dictionary<string, string>>
            {
                Label("2001", "clientCode", "101"), Label("2001", "clientCode", "101"), Label("2002", "openSpace", "102"), Label("2004", "landUse", "104"),
                Label("2005", "substation", "105"), Label("2006", "clientCode", ""), Label("2003", "clientCode", "7"), Label("2003", "clientCode", "8"),
            };
            Func<string, string, IReadOnlyList<string>> faceOf = ProjectPositionFile.Numbering(labels, new Dictionary<string, int> { ["IMC"] = 10 });
            Assert.Equal(new[] { "101", "", "", "", "", "7|8" }, new[] { "2001", "2002", "2004", "2005", "2006", "2003" }.Select(number => string.Join("|", faceOf("ima", number))));
            Assert.Equal(new[] { "1992", "1996", "7|8" }, new[] { "2002", "2006", "2003" }.Select(number => string.Join("|", faceOf("imc", number))));
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            var numbers = new Dictionary<string, string[]> { ["2001"] = new[] { "101", "102" }, ["2002"] = new[] { "102" }, ["2003"] = new[] { "103" } };
            Dictionary<string, Dictionary<string, object>> rows = MadeCheck(position, MadePlots(), numbering: Numbered(numbers));
            List<Dictionary<string, object>> two = rows.Values.Where(row => (string)row["plot"] == "2001").ToList();
            Assert.Equal(4, two.Count);
            Assert.All(two, row => Assert.True((string)row["why"] == "number on two faces" && string.Join("|", (string[])row["numberFaces"]) == "101|102" && row["expectedFace"] == null && false.Equals(row["pass"])));
            Assert.All(rows.Values.Where(row => (string)row["plot"] != "2001"), row => Assert.Null(row["numberFaces"]));
            Dictionary<string, object> summary = ProjectPositionFile.Summary(rows, 1.0);
            Assert.Equal("4 8 True", string.Join(" ", summary["twoFaces"], summary["pass"], summary["failed"]));
            Assert.Contains("imc/2001/0 number on two faces 101, 102, gap 0.0 mm", ProjectPositionFile.Issues(rows, 2));
        }

        [Fact]
        public void R353_a_check_point_on_a_vertex_merged_more_than_the_limit_fails_with_its_merge()
        {
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            var merges = new Dictionary<string, double[]> { ["101"] = new[] { 0.0, 0.0012, 0.0004, 0.0 } };
            Dictionary<string, Dictionary<string, object>> rows = MadeCheck(position, MadePlots(merges: merges));
            Assert.Equal(new[] { 0.0, 1.2, 0.4, 0.0 }, Enumerable.Range(0, 4).Select(k => Math.Round((double)rows["imc/2001/" + k]["mergeMm"], 9)));
            Assert.Equal(new[] { null, "vertex merged over the limit", null, null }, Enumerable.Range(0, 4).Select(k => (string)rows["imc/2001/" + k]["why"]));
            Assert.True((double)rows["imc/2001/1"]["gapMm"] < 1e-6);
            Assert.Equal(1, ProjectPositionFile.Summary(rows, 1.0)["merged"]);
            Assert.All(rows.Values.Where(row => row["plot"] == null), row => Assert.Null(row["mergeMm"]));
            Assert.Equal(new[] { 0.0, 0.0012, 0.0004, 0.0 }, ProjectPositionFile.MergesOf(MadePlots(merges: merges))["101"][0]);
        }

        [Fact]
        public void R353_a_not_read_that_is_no_object_is_refused()
        {
            foreach (Tuple<JToken, string> value in new[] { Tuple.Create((JToken)new JArray("projectPosition"), "array"), Tuple.Create((JToken)"levels could not be read", "string"), Tuple.Create((JToken)3, "number") })
            {
                JObject read = MadeRead();
                read["documents"][0]["site"]["notRead"] = value.Item1;
                Assert.Contains("notRead as a JSON " + value.Item2, Assert.Throws<InvalidDataException>(() => ProjectPositionFile.From(ProjectPositionFile.DocumentOf(read), "imc")).Message);
            }
            Assert.Equal(new[] { "ima site.notRead is no object" }, ProjectPositionFile.SetDiffers(Documents((key, document) =>
            {
                if (key == "ima")
                {
                    document["site"]["notRead"] = new JArray("levels");
                }
            }), "imc", Group, Copies));
            JObject none = MadeRead();
            none["documents"][0]["site"]["notRead"] = JValue.CreateNull();
            Assert.Equal("imc", ProjectPositionFile.From(ProjectPositionFile.DocumentOf(none), "imc").Key);
        }

        [Fact]
        public void R353_the_key_points_are_held_to_their_key_and_to_the_position_file()
        {
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            Assert.Empty(ProjectPositionFile.PointsFaults(MadePoints(), "imc", Copies, position));
            JObject other = MadePoints();
            other["model"] = Model("img", "KG") + ".rvt";
            Assert.Equal(new[] { "imc keyPoints model " + Model("img", "KG") + ".rvt is not a work copy copy_imc_TEST-<job> of its answer key" }, ProjectPositionFile.PointsFaults(other, "imc", Copies, position));
            JObject far = MadePoints();
            far["projectPosition"]["eastWest"] = 6000.0;
            Assert.Equal(new[] { "imc keyPoints projectPosition 5000000.0 mm off the position file" }, ProjectPositionFile.PointsFaults(far, "imc", Copies, position));
            JObject turned = MadePoints();
            turned["projectPosition"]["angleDeg"] = 0.5;
            Assert.Equal(new[] { "imc keyPoints projectPosition angle 0.5 deg off the position file" }, ProjectPositionFile.PointsFaults(turned, "imc", Copies, position));
            Assert.Equal("a", ProjectPositionFile.AnswerCopies(JObject.Parse("{\"keys\": [{\"code\": \"IMC\", \"copy\": \"a.rvt\"}]}"), JObject.Parse("{\"copies\": [{\"code\": \"IMC\", \"copy\": \"a.rvt\"}]}"))["imc"]);
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => ProjectPositionFile.AnswerCopies(JObject.Parse("{\"keys\": [{\"code\": \"IMC\", \"copy\": \"a.rvt\"}]}"), JObject.Parse("{\"copies\": [{\"code\": \"IMC\", \"copy\": \"b.rvt\"}]}")));
            Assert.Contains("answerKeys names for IMC the copy a.rvt and key_copies.json b.rvt", refused.Message);
            Assert.Contains("no code or no copy", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.AnswerCopies(JObject.Parse("{\"keys\": [{\"code\": \"IMC\"}]}"), JObject.Parse("{\"copies\": [{\"code\": \"IMC\"}]}"))).Message);
            // test_position_file: two codes that name one copy, in either case of its name
            foreach (string second in new[] { "x.rvt", "X.RVT" })
            {
                JObject both = JObject.Parse("{\"keys\": [{\"code\": \"IMA\", \"copy\": \"x.rvt\"}, {\"code\": \"IMB\", \"copy\": \"" + second + "\"}]}");
                Assert.Matches("IMA and IMB.*one copy", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.AnswerCopies(both, new JObject { ["copies"] = both["keys"].DeepClone() })).Message);
            }
            JObject real = (JObject)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["answerKeys"];
            Assert.Equal(7, ProjectPositionFile.AnswerCopies(real, new JObject { ["copies"] = real["keys"].DeepClone() }).Count);
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

        // R353. CadReader --exact: the plot shapes of a drawing at full precision, a vertex at 1000.0004 read as it is, where plots.json
        // writes 1000
        [Fact]
        public void R353_the_exact_plots_of_a_drawing_keep_every_digit_of_its_vertices()
        {
            string path = Baseplan(0.0004);
            var problems = new List<string>();
            CadExtract extract = CadExtractor.Extract(CadExtractor.Open(path, problems), "baseplan.dwg", LayerConfig.Load(Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json")), null, path, problems);
            JObject exact = CadExtractor.ExactPlots(extract, "Meters");
            JToken face = exact["plots"].First(item => (string)item["kind"] == "plot_shape" && (string)item["plot"] == "101");
            Assert.Contains(face["vertices"], vertex => (double)vertex[0] == 1000.0004);
            string text = CadExtractor.ExactText(exact);
            Assert.Contains("1000.0004", text);
            Assert.Equal("Meters", (string)JObject.Parse(text)["units"]);
            // Each vertex with the merge PlotShapes made of it, 0 where the line ends meet exactly
            Assert.Equal(face["vertices"].Count(), face["vertexMergeM"].Count());
            Assert.All(face["vertexMergeM"], merge => Assert.Equal(0.0, (double)merge));
            // A line end 3 mm east and 4 mm north of the next line's start is one node of the face, the vertex 5 mm from the end merged into it
            path = Baseplan(0.0, 0.003, 0.004);
            extract = CadExtractor.Extract(CadExtractor.Open(path, problems), "baseplan.dwg", LayerConfig.Load(Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json")), null, path, problems);
            face = CadExtractor.ExactPlots(extract, "Meters")["plots"].First(item => (string)item["kind"] == "plot_shape" && (string)item["plot"] == "101");
            Assert.Equal(new[] { 0.0, 0.0, 0.0, 0.005 }, face["vertexMergeM"].Select(merge => Math.Round((double)merge, 9)).OrderBy(merge => merge));
        }

        // R353. A read into a folder without --exact removes a plots_exact.json an earlier read left there, so it never stands beside a newer
        // plots.json, and a read with --exact writes it
        [Fact]
        public void R353_a_read_without_exact_removes_the_plots_exact_of_an_earlier_read()
        {
            string path = Baseplan(0.0004);
            var problems = new List<string>();
            CadExtract extract = CadExtractor.Extract(CadExtractor.Open(path, problems), "baseplan.dwg", LayerConfig.Load(Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json")), null, path, problems);
            string outDir = Path.Combine(folder, "read");
            Directory.CreateDirectory(outDir);
            string exact = Path.Combine(outDir, "plots_exact.json");
            Assert.True(CadExtractor.WriteExact(extract, outDir, true));
            Assert.True(File.Exists(exact));
            Assert.False(CadExtractor.WriteExact(extract, outDir, false));
            Assert.False(File.Exists(exact));
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

        // test_position_file.made_plots as a baseplan drawing in meters, its faces moved east by east: the three faces on the plot line layer,
        // each with its label inside
        private string Baseplan(double east = 0.0, double gapEast = 0.0, double gapNorth = 0.0)
        {
            var document = new CadDocument();
            document.Header.InsUnits = UnitsType.Meters;
            var line = new Layer("MP_PLOT_LINE");
            var text = new Layer("MP_PLOT_TEXT");
            document.Layers.Add(line);
            document.Layers.Add(text);
            foreach (JToken plot in MadePlots(east: east))
            {
                List<double[]> ring = plot["vertices"].Select(point => new[] { (double)point[0], (double)point[1] }).ToList();
                for (int corner = 0; corner < ring.Count; corner++)
                {
                    double[] from = ring[corner], to = ring[(corner + 1) % ring.Count];
                    // The first line of face 101 drawn with its end off the next line's start, as a drawing whose lines do not quite meet
                    double offEast = (string)plot["plot"] == "101" && corner == 0 ? gapEast : 0.0, offNorth = (string)plot["plot"] == "101" && corner == 0 ? gapNorth : 0.0;
                    document.Entities.Add(new Line { StartPoint = new XYZ(from[0], from[1], 0), EndPoint = new XYZ(to[0] + offEast, to[1] + offNorth, 0), Layer = line });
                }
                document.Entities.Add(new MText { Value = "DP1\\P" + (string)plot["plot"], InsertPoint = new XYZ(ring[0][0] + 5, ring[0][1] + 5, 0), Layer = text });
            }
            string path = Path.Combine(folder, "baseplan_" + east.ToString("R", CultureInfo.InvariantCulture) + "_" + gapEast.ToString("R", CultureInfo.InvariantCulture) + ".dwg");
            using (var writer = new DwgWriter(path, document))
            {
                writer.Write();
            }
            return path;
        }

        // The answer keys of config and a key_copies.json that lists the same copies, the reads named as their work copies
        private Dictionary<string, string> CliCopies(out string manifest)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            var keys = (JArray)config["answerKeys"]["keys"];
            manifest = Write("key_copies.json", new JObject { ["copies"] = new JArray(keys.Select(entry => new JObject { ["code"] = entry["code"], ["copy"] = entry["copy"] })) });
            return keys.ToDictionary(entry => ((string)entry["code"]).ToLowerInvariant(), entry => ((string)entry["copy"]).Substring(0, ((string)entry["copy"]).Length - 4));
        }

        private WorkerAnswer RunPosition(string id, JObject read, List<string> steps, out int code, IEnumerable<string> others, Dictionary<string, string> copies, string manifest, params WorkerFile[] more)
        {
            string config = Path.Combine(folder, "project.json");
            if (!File.Exists(config))
            {
                File.Copy(WorkerRunTests.RepoConfig(), config);
            }
            string rules = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(WorkerRunTests.RepoConfig())), "rules", "roshn-makkah.json");
            read["documents"][0]["file"] = copies["imc"] + "_TEST-RC";
            var inputs = new List<WorkerFile> { Input("config", config), Input("rules", rules), Input(ProjectPositionFile.KeyCopiesRole, manifest), Input(ProjectPositionFile.KeyReadRole, Write("read_" + id + ".json", read)) };
            foreach (string key in others)
            {
                JObject other = MadeRead(key: key);
                other["documents"][0]["file"] = copies[key] + "_TEST-R" + key.Substring(2).ToUpperInvariant();
                inputs.Add(Input(ProjectPositionFile.KeyReadRole + ":" + key, Write("read_" + id + "_" + key + ".json", other)));
            }
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
        public void R353_the_command_position_writes_the_file_its_steps_and_the_check_points_and_a_failed_check_is_an_issue_of_the_run()
        {
            int code;
            string manifest;
            Dictionary<string, string> copies = CliCopies(out manifest);
            string layers = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json");
            JObject points = MadePoints();
            points["model"] = copies["imc"] + "_TEST-KC.rvt";
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, "number,kind,face\n2001,clientCode,101\n2002,clientCode,102\n2003,clientCode,103\n");
            string shifts = Write("plot_numbers.json", JObject.Parse("{\"neighbourhoods\": {\"IMC\": {\"shift\": 10}}}"));
            // The baseplan 0.0004 m east of the keys at full precision: every corner 0.4 mm off, where the 3 decimals of plots.json gave 0
            WorkerFile[] check =
            {
                Input("baseplan", Baseplan(0.0004)), Input("cadLayers", layers), Input(ProjectPositionFile.KeyPointsRole + ":imc", Write("points_imc.json", points)),
                Input(ProjectPositionFile.LabelsRole, labels), Input(ProjectPositionFile.PlotShiftsRole, shifts),
            };
            WorkerAnswer answer = RunPosition("p1", MadeRead(), new List<string> { ProjectPositionFile.PositionStep, ProjectPositionFile.CheckPointsStep }, out code, Group.Skip(1), copies, manifest, check);
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            Assert.Equal("done", answer.Status);
            string name = Path.GetFileName(PositionSettings.From(JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))).File);
            JObject written = JObject.Parse(File.ReadAllText(Path.Combine(folder, "out", name)));
            Assert.Equal("Survey Point", (string)written["levelBase"]);
            Assert.Equal("Internal", (string)written["activeSite"]);
            // The check of the 6 keys with no points failed: the file records it, every failed point is an issue, the run ends 0 and done
            Assert.Equal("{\"limitMm\":1.0,\"points\":18,\"pass\":12,\"overLimit\":0,\"noFace\":0,\"otherFace\":0,\"noNumber\":0,\"twoFaces\":0,\"merged\":0,\"noPoints\":6,\"failed\":true}",
                written["checkPoints"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.StartsWith("the check points failed: 12 of 18 pass at 1.0 mm", answer.Issues[0]);
            Assert.Contains("ima no key points", answer.Issues);
            JObject step = JObject.Parse(File.ReadAllText(Path.Combine(folder, "steps", StepFile.FileName(ProjectPositionFile.CheckPointsStep, "csharp"))));
            Assert.Equal(0.4, (double)step["plots"]["imc/2001/0"]["raw"]["gapMm"], 6);
            Assert.Equal(new[] { "keyRead", "rules", ProjectPositionFile.KeyCopiesRole, "keyRead:*" }, WorkerRunner.Steps[ProjectPositionFile.PositionStep]);
            Assert.Equal(new[] { "keyRead", "rules", ProjectPositionFile.KeyCopiesRole, "baseplan", ProjectPositionFile.LabelsRole, ProjectPositionFile.PlotShiftsRole, "keyPoints:*" },
                WorkerRunner.Steps[ProjectPositionFile.CheckPointsStep]);
            JObject off = MadeRead();
            off["documents"][0]["site"]["levels"][0]["elevation"] = 9.5;
            answer = RunPosition("p2", off, new List<string>(), out code, Group.Skip(1), copies, manifest);
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("levelStepsM", answer.Message);
            // A set with a key left out, the IMC read given for IMA, and key points of another position, each refused before anything is written
            answer = RunPosition("p3", MadeRead(), new List<string>(), out code, Group.Skip(1).Take(5), copies, manifest);
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("img no read", answer.Message);
            JObject imcForIma = MadeRead();
            imcForIma["documents"][0]["file"] = copies["imc"] + "_TEST-RC";
            answer = RunPosition("p4", MadeRead(), new List<string>(), out code, Group.Skip(2), copies, manifest, Input(ProjectPositionFile.KeyReadRole + ":ima", Write("ima_is_imc.json", imcForIma)));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("ima model " + copies["imc"] + "_TEST-RC is not a work copy", answer.Message);
            JObject far = (JObject)points.DeepClone();
            far["projectPosition"]["eastWest"] = 6000.0;
            check[2] = Input(ProjectPositionFile.KeyPointsRole + ":imc", Write("points_far.json", far));
            answer = RunPosition("p5", MadeRead(), new List<string>(), out code, Group.Skip(1), copies, manifest, check);
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("imc keyPoints projectPosition 5000000.0 mm off the position file", answer.Message);
            // A request that names neither the baseplan nor key points writes the file with an issue that the check points were not made,
            // as R387 lists a check that cannot be made, and one that names the baseplan with no key points is refused
            answer = RunPosition("p6", MadeRead(), new List<string>(), out code, Group.Skip(1), copies, manifest);
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            Assert.Equal(new[] { ProjectPositionFile.NotMade }, answer.Issues);
            answer = RunPosition("p7", MadeRead(), new List<string>(), out code, Group.Skip(1), copies, manifest, check[0], check[1], check[3], check[4]);
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("the request names one of them", answer.Message);
        }
    }
}
