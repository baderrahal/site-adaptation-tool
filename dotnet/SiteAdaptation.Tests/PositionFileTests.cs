using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CadReader;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R353. The project position file of button 1 and its check points on made up key reads built in code (R10), the C# side of
    // python/tests/test_position_file.py on the same made up reads, on net48 and net8.0
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

        private static readonly JObject SiteRules = JObject.Parse("{\"angleToTrueNorthDeg\": 0, \"levels\": [\"Level 1\", \"Level 2\"], \"levelStepsM\": {\"Level 2\": 4.0}, \"levelBase\": \"Survey Point\"}");

        // test_position_file.made_read: the internal origin at shared (1000, 2000, 5) turned by angle, the survey point and the project base
        // point on the internal origin, two levels 4 m apart on one Elevation Base
        private static JObject MadeRead(double angle = 0.0, string levelBase = "Survey Point", double surveyOff = 0.0)
        {
            double turn = angle * Math.PI / 180.0;
            Func<double, double, double, JArray> shared = (x, y, z) => new JArray(Math.Cos(turn) * x - Math.Sin(turn) * y + 1000.0, Math.Sin(turn) * x + Math.Cos(turn) * y + 2000.0, z + 5.0);
            JArray survey = shared(0, 0, 0);
            survey[0] = (double)survey[0] + surveyOff;
            return new JObject
            {
                ["tool"] = "RevitPrototypeReader", ["lengthUnit"] = "m", ["angleUnit"] = "deg",
                ["documents"] = new JArray(new JObject
                {
                    ["file"] = "key_TEST-RC", ["kind"] = "project",
                    ["surveyPoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = survey },
                    ["basePoint"] = new JObject { ["position"] = new JArray(0.0, 0.0, 0.0), ["sharedPosition"] = shared(0, 0, 0) },
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

        [Fact]
        public void R353_the_position_file_is_the_site_of_a_key_read_and_holds_the_site_rules_of_the_profile()
        {
            ProjectPositionFile position = ProjectPositionFile.From(ProjectPositionFile.DocumentOf(MadeRead()), "imc");
            Assert.Equal("imc", position.Key);
            Assert.Equal("key_TEST-RC", position.Model);
            Assert.Equal(new double?[] { 1000.0, 2000.0, 5.0, 0.0 }, new[] { position.Position.EastM, position.Position.NorthM, position.Position.ElevationM, position.Position.AngleDeg });
            Assert.Equal(new[] { 1000.0, 2000.0, 5.0 }, position.SurveyShared);
            Assert.Equal(new[] { "Level 1", "Level 2" }, position.Levels.Select(level => level.Name));
            Assert.Equal("Survey Point", position.LevelBase);
            Assert.Empty(ProjectPositionFile.AgainstRules(position, SiteRules));
            JObject other = (JObject)SiteRules.DeepClone();
            other["levelStepsM"]["Level 2"] = 4.001;
            other["levelBase"] = "Project Base Point";
            Assert.Equal(2, ProjectPositionFile.AgainstRules(position, other).Count);
            Assert.Equal(1000.0, (double)position.Written()["projectPosition"]["eastM"]);
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

        private static readonly List<double[]> Controls = new List<double[]> { new[] { 1010.0, 2020.0 }, new[] { 990.0, 1985.0 }, new[] { 1500.0, 2500.0 } };
        private static readonly List<double[]> Centroids = new List<double[]> { new[] { 1005.0, 2003.0 }, new[] { 1040.0, 1990.0 }, new[] { 1020.0, 2050.0 } };

        [Fact]
        public void R353_the_check_points_go_to_their_internal_points_by_the_file_and_by_the_survey_pair_of_the_key_read()
        {
            foreach (double angle in new[] { 0.0, 30.0 })
            {
                JObject document = ProjectPositionFile.DocumentOf(MadeRead(angle));
                ProjectPositionFile position = ProjectPositionFile.From(document, "imc");
                Dictionary<string, Dictionary<string, object>> rows = ProjectPositionFile.CheckPoints(position, Controls, Centroids, document, 1.0, 2);
                Assert.Equal(new[] { "control 1", "control 2", "control 3", "plots min", "plots max", "survey point", "project base point" }, rows.Keys);
                Assert.Equal(new[] { 1005.0, 1990.0, 0.0 }, (double[])rows["plots min"]["sharedM"]);
                Assert.Equal(new[] { 1040.0, 2050.0, 0.0 }, (double[])rows["plots max"]["sharedM"]);
                double turn = angle * Math.PI / 180.0;
                double[] internalPoint = (double[])rows["control 1"]["internalM"];
                Assert.Equal(Math.Cos(turn) * 10.0 + Math.Sin(turn) * 20.0, internalPoint[0], 9);
                Assert.Equal(-Math.Sin(turn) * 10.0 + Math.Cos(turn) * 20.0, internalPoint[1], 9);
                Assert.Equal(-5.0, internalPoint[2], 9);
                Assert.All(rows.Values, row => Assert.True((double)row["gapMm"] < 1e-6 && (bool)row["pass"]));
            }
            // The survey point of the read 0.6 mm and 1.2 mm off the project position: the file and the read part by that much on every point
            foreach (Tuple<double, bool> off in new[] { Tuple.Create(0.0006, true), Tuple.Create(0.0012, false) })
            {
                JObject document = ProjectPositionFile.DocumentOf(MadeRead(surveyOff: off.Item1));
                Dictionary<string, object> row = ProjectPositionFile.CheckPoints(ProjectPositionFile.From(document, "imc"), Controls, Centroids, document, 1.0, 2)["control 3"];
                Assert.Equal(off.Item1 * 1000.0, (double)row["gapMm"], 6);
                Assert.Equal(off.Item2, (bool)row["pass"]);
            }
            // The gap is judged as written at the decimals of config, so 1.004 mm shows 1.0 and passes at 1 mm
            JObject near = ProjectPositionFile.DocumentOf(MadeRead(surveyOff: 0.001004));
            Assert.True((bool)ProjectPositionFile.CheckPoints(ProjectPositionFile.From(near, "imc"), Controls, Centroids, near, 1.0, 2)["control 1"]["pass"]);
            Dictionary<string, object> alone = ProjectPositionFile.CheckPoints(ProjectPositionFile.From(near, "imc"), Controls, Centroids, null, 1.0, 2)["control 1"];
            Assert.Null(alone["keyInternalM"]);
            Assert.Null(alone["pass"]);
            Assert.Contains("no face", Assert.Throws<InvalidDataException>(() => ProjectPositionFile.CheckPoints(ProjectPositionFile.From(near, "imc"), Controls, new List<double[]>(), null, 1.0, 2)).Message);
        }

        [Fact]
        public void R353_config_names_the_position_file_its_limit_and_the_key_of_the_read()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            PositionSettings settings = PositionSettings.From(config);
            Assert.Equal(1.0, settings.CheckPointLimitMm);
            Assert.Equal(2, settings.GapDecimals);
            Assert.EndsWith(".json", settings.File);
            Assert.Equal("imc", settings.Key);
            config["addin"]["position"]["gapDecimals"] = 7;
            Assert.Throws<InvalidDataException>(() => PositionSettings.From(config));
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        private WorkerAnswer RunPosition(string id, JObject read, List<string> steps, out int code, params WorkerFile[] more)
        {
            string config = Path.Combine(folder, "project.json");
            if (!File.Exists(config))
            {
                File.Copy(WorkerRunTests.RepoConfig(), config);
            }
            string rules = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(WorkerRunTests.RepoConfig())), "rules", "roshn-makkah.json");
            string readPath = Path.Combine(folder, "read_" + id + ".json");
            File.WriteAllText(readPath, read.ToString());
            var inputs = new List<WorkerFile> { Input("config", config), Input("rules", rules), Input(ProjectPositionFile.KeyReadRole, readPath) };
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
        public void R353_the_command_position_writes_the_file_and_the_step_and_refuses_a_read_off_the_site_rules()
        {
            int code;
            WorkerAnswer answer = RunPosition("p1", MadeRead(), new List<string> { ProjectPositionFile.PositionStep }, out code);
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            string name = Path.GetFileName(PositionSettings.From(JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))).File);
            JObject written = JObject.Parse(File.ReadAllText(Path.Combine(folder, "out", name)));
            Assert.Equal("Survey Point", (string)written["levelBase"]);
            Assert.Equal("imc", (string)written["key"]);
            JObject step = JObject.Parse(File.ReadAllText(Path.Combine(folder, "steps", StepFile.FileName(ProjectPositionFile.PositionStep, "csharp"))));
            Assert.Equal(new[] { "Level 1", "Level 2" }, step["plots"]["position"]["raw"]["levelNames"].Select(item => (string)item));
            Assert.Equal(new[] { "keyRead", "rules" }, WorkerRunner.Steps[ProjectPositionFile.PositionStep]);
            Assert.Equal("position", WorkerRunner.StepCommand[ProjectPositionFile.PositionStep]);
            Assert.Equal("position", WorkerRunner.StepCommand[ProjectPositionFile.CheckPointsStep]);
            JObject off = MadeRead();
            off["documents"][0]["site"]["levels"][0]["elevation"] = 9.5;
            answer = RunPosition("p2", off, new List<string>(), out code);
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("levelStepsM", answer.Message);
            string pdf = Path.Combine(folder, "plan.pdf");
            File.WriteAllText(pdf, "not read");
            answer = RunPosition("p3", MadeRead(), new List<string>(), out code, Input(LocationPlan.PlotNumbersRole, pdf));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("the check points need the baseplan and the location plan", answer.Message);
        }
    }
}
