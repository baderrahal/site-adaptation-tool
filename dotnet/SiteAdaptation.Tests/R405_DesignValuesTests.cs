using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.SetUp;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R405, Q103. The design values of a build, the placer and button 1 come from designValues of config/project.json, each the literal
    // the code held before, so the move changes no plan by one unit, and a value missing is refused by its name
    public sealed class R405_DesignValuesTests
    {
        private static JObject RepoConfig()
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
        }

        [Fact]
        public void R405_each_value_of_the_config_equals_the_literal_it_replaced()
        {
            DesignValues values = DesignValues.From(RepoConfig());
            Assert.Equal(new[] { 0.001, 0.001, 0.001, 0.001, 0.01 }, new[] { values.PlacerCoordinateToleranceM, values.PlacerAngleToleranceDeg, values.PlacerZToleranceM, values.PlacerReadBackOriginM, values.PlacerReadBackAngleDeg });
            Assert.Equal(new[] { 0.001, 0.001, 0.002, 0.001 }, new[] { values.FloorTopToleranceM, values.FloorLevelToleranceM, values.FloorCornerOnVertexM, values.FloorShortestEdgeM });
            Assert.Equal(new[] { 1.0, 0.01, 0.05, 1.0, 0.5, 1e-6 }, new[] { values.FrontTieM, values.FrontOutwardProbeM, values.HandSideBandM, values.SiteToleranceMm, values.SameMm, values.SameDeg });
            Assert.Equal(new long[] { 16, 10, 3, 6 }, new long[] { values.HandBufferSegmentsPerQuarter, values.CmGrainMm, values.Decimals, values.AngleDecimals });
            Assert.Equal(new[] { "EN", "SE", "WS", "NW" }, values.Quadrants.Keys);
            Assert.Equal(new[] { 0.0, 90.0, 90.0, 180.0, 180.0, 270.0, 270.0, 360.0 }, new[] { values.Quadrants["EN"], values.Quadrants["SE"], values.Quadrants["WS"], values.Quadrants["NW"] }.SelectMany(span => span));
            Assert.Equal(Math.PI / 2 / 16, Math.PI / 2 / values.HandBufferSegmentsPerQuarter);
        }

        [Fact]
        public void R405_the_code_takes_the_values_of_the_run()
        {
            DesignValues values = DesignValues.From(RepoConfig());
            Assert.Equal(values.PlacerCoordinateToleranceM, Placer.CoordinateTolerance);
            Assert.Equal(values.PlacerReadBackAngleDeg, Placer.ReadBackAngle);
            Assert.Equal(values.FloorCornerOnVertexM, FloorPlacer.CornerOnVertex);
            Assert.Equal(values.SiteToleranceMm, SiteCoordinates.ToleranceMm);
            Assert.Equal(values.SameMm, SetUpPlanner.SameMm);
            Assert.Equal(values.AngleDecimals, SiteFacts.AngleDecimals);
            Assert.Equal(values.Decimals, JsonOut.Decimals);
        }

        // A literal put back in place of the read of the config fails here
        [Theory]
        [InlineData("SiteAdaptation.Common/Placer.cs", "CoordinateTolerance|AngleTolerance|ZTolerance|ReadBackOrigin|ReadBackAngle")]
        [InlineData("SiteAdaptation.Common/FloorPlacer.cs", "TopTolerance|LevelTolerance|CornerOnVertex|ShortestEdge")]
        [InlineData("SiteAdaptation.Common/Plan/Fronts.cs", "FrontTie|Probe")]
        [InlineData("SiteAdaptation.Common/Plan/Hands.cs", "Band|Quantum")]
        [InlineData("SiteAdaptation.Common/SiteSpec.cs", "ToleranceMm")]
        [InlineData("SiteAdaptation.Common/Read/ProjectPositionFile.cs", "SameMm|SameDeg")]
        [InlineData("SiteAdaptation.Common/SetUp/SetUpPlanner.cs", "SameMm|SameDeg")]
        [InlineData("SiteAdaptation.Common/SiteFacts.cs", "AngleDecimals")]
        [InlineData("SiteAdaptation.Common/JsonOut.cs", "Decimals")]
        [InlineData("SiteAdaptation.Common/Plan/BuildPlan.cs", "Decimals")]
        public void R405_no_design_value_is_a_literal_of_the_code(string file, string names)
        {
            string text = File.ReadAllText(Path.Combine(new[] { ThemeGateTests.RepoRoot(), "dotnet" }.Concat(file.Split('/')).ToArray()));
            Assert.DoesNotMatch(new Regex(@"const\s+(double|int|long)\s+(" + names + @")\s*="), text);
            Assert.DoesNotContain("FloorDivide(millimetres, 10) / 100.0", text);
            Assert.DoesNotContain("[\"EN\"] = new[] { 0.0, 90.0 }", text);
        }

        [Fact]
        public void R405_fflDownToTheCm_gives_the_text_of_the_old_floor_division()
        {
            foreach (double ffl in new[] { 150.389, 150.38, 150.0, 0.009, -0.001, -1.237, 2.5, 99.999, 100.0005 })
            {
                long mm = (long)Rounding.Fixed(ffl * 1000, 0);
                long whole = mm / 10;
                long down = (mm % 10 != 0 && mm < 0) ? whole - 1 : whole;
                Assert.Equal(PyMath.FixedText(down / 100.0, 3), KeyLevels.PrototypeFfl("fflDownToTheCm", ffl));
            }
        }

        [Fact]
        public void R405_a_missing_value_is_refused_by_its_name()
        {
            JObject config = RepoConfig();
            ((JObject)config["designValues"]["fronts"]).Remove("frontTieM");
            Assert.Contains("designValues.fronts.frontTieM", Assert.Throws<InvalidDataException>(() => DesignValues.From(config)).Message);
            config["designValues"]["fronts"]["frontTieM"] = 1.0;
            config["designValues"]["levels"]["cmGrainMm"] = 10.5;
            Assert.Contains("designValues.levels.cmGrainMm", Assert.Throws<InvalidDataException>(() => DesignValues.From(config)).Message);
            Assert.Contains("no designValues block", Assert.Throws<InvalidDataException>(() => DesignValues.From(new JObject())).Message);
        }

        // Q127, J5. A path of config under the work root mark takes the root of the machine, a path with no mark stays as it is
        [Fact]
        public void Q127_J5_a_path_under_the_work_root_mark_takes_the_root_and_config_holds_no_path_of_a_user()
        {
            string root = Path.Combine(Path.GetTempPath(), "SiteAdaptation");
            Assert.Equal(Path.Combine(root, "round_addin", "position", "p.json"), WorkRoot.Resolve(WorkRoot.Mark + "\\round_addin/position\\p.json", () => root));
            Assert.Equal(root, WorkRoot.Resolve(WorkRoot.Mark, () => root));
            Assert.Equal("relative/p.json", WorkRoot.Resolve("relative/p.json", () => throw new InvalidOperationException("no root asked for a path with no mark")));
            foreach (string name in new[] { "project.json", "road_surfaces.json" })
            {
                string text = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", name)).Replace("\\\\", "/").ToLowerInvariant();
                Assert.DoesNotContain("c:/users/", text);
            }
        }

        internal static string RepoConfigPath()
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json");
        }

        // A run folder holding a copy of the config of the repo, so the values the test sets are those every other test reads
        internal static string RunFolderWithConfig()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sa_r405_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "config"));
            File.Copy(RepoConfigPath(), Path.Combine(folder, "config", "project.json"));
            return folder;
        }

        // The plain addinRun of the Revit run after 5c failed to write its result: the test host takes the config copy of its run
        // folder the run script writes, else that of the repo above the test host
        [Fact]
        public void R405_a_plain_addinRun_takes_the_config_copy_of_its_run_folder_else_that_of_the_repo()
        {
            string folder = RunFolderWithConfig();
            try
            {
                string copy = Path.Combine(folder, "config", "project.json");
                Assert.Equal(copy, AddinRunFiles.Config(folder, AppContext.BaseDirectory));
                Assert.Null(DesignValues.UseConfigFile(AddinRunFiles.Config(folder, AppContext.BaseDirectory)));
                Assert.Equal(copy, DesignValues.Current.Source);
                File.Delete(copy);
                Assert.Equal(Path.GetFullPath(RepoConfigPath()), Path.GetFullPath(AddinRunFiles.Config(folder, AppContext.BaseDirectory)));
            }
            finally
            {
                DesignValues.Use(DesignValues.Load(RepoConfigPath()));
                Directory.Delete(folder, true);
            }
            string root = ThemeGateTests.RepoRoot();
            string script = File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            int branch = script.IndexOf("        'addinRun' {", StringComparison.Ordinal);
            int plain = script.IndexOf("            if (-not $round.setUp -and -not $round.buttons) {", branch, StringComparison.Ordinal);
            int copied = script.IndexOf("Copy-Item -LiteralPath (Join-Path $repo 'config\\project.json') -Destination (Join-Path $plainConfig 'project.json')", plain, StringComparison.Ordinal);
            Assert.True(branch > 0 && plain > branch && copied > plain);
            string session = File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.TestHost", "AddinRunSession.cs"));
            int use = session.IndexOf("DesignValues.UseConfigFile(AddinRunFiles.Config(folder,", StringComparison.Ordinal);
            Assert.True(use > 0 && use < session.IndexOf("record.HomeEvent.Raise(", StringComparison.Ordinal));
        }

        // R466. The read KC2 of D2 opened the key copy and its log kept no step: inside Revit no config lies above the test host, so
        // JsonOut threw on the first number of a step. Every job names the config of the repo and the session takes it before any step
        [Fact]
        public void R466_every_test_host_job_names_the_config_of_the_repo_and_the_session_takes_it_before_the_first_step()
        {
            var job = Newtonsoft.Json.JsonConvert.DeserializeObject<TestHostJob>(new JObject { ["kind"] = "read", ["designConfig"] = RepoConfigPath() }.ToString());
            Assert.Equal(RepoConfigPath(), job.DesignConfig);
            try
            {
                Assert.Null(DesignValues.UseConfigFile(job.DesignConfig));
                Assert.Equal(RepoConfigPath(), DesignValues.Current.Source);
                string written = null;
                var runner = new TestHostRunner(new TestHostLog { Code = "KC2" }, value => written = JsonOut.ToJson(value), () => new DateTime(2026, 11, 1, 17, 15, 0, DateTimeKind.Utc));
                Assert.True(runner.Run(new[] { new JobStep { Name = "read", LimitMinutes = 15, Action = () => "read" } }));
                Assert.Contains("\"name\": \"read\"", written);
                Assert.Contains("\"seconds\": 0.0", written);
                string missing = Path.Combine(Path.GetTempPath(), "r466-" + Guid.NewGuid().ToString("N"), "project.json");
                Assert.StartsWith("the design values were not read from the config " + missing, DesignValues.UseConfigFile(missing));
            }
            finally
            {
                DesignValues.Use(DesignValues.Load(RepoConfigPath()));
            }
            string root = ThemeGateTests.RepoRoot();
            string script = File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            int named = script.IndexOf("$job.designConfig = Join-Path $repo 'config\\project.json'", StringComparison.Ordinal);
            Assert.True(named > 0 && named < script.IndexOf("$job | ConvertTo-Json -Depth 5 | Set-Content -Path $jobPath", StringComparison.Ordinal));
            string session = File.ReadAllText(Path.Combine(root, "dotnet", "SiteAdaptation.TestHost", "JobSession.cs"));
            int use = session.IndexOf("DesignValues.UseConfigFile(job.DesignConfig)", StringComparison.Ordinal);
            Assert.True(use > session.IndexOf("private void Subscribe()", StringComparison.Ordinal) && use < session.IndexOf("application.Idling += OnFirstIdling", StringComparison.Ordinal));
        }

        [Fact]
        public void R405_a_config_of_a_test_with_no_block_leaves_the_values_of_the_run()
        {
            double before = Placer.CoordinateTolerance;
            DesignValues.UseConfigOf(new JObject { ["profile"] = "x" });
            Assert.Equal(before, Placer.CoordinateTolerance);
        }
    }
}
