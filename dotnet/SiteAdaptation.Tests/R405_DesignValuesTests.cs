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

        [Fact]
        public void R405_a_config_of_a_test_with_no_block_leaves_the_values_of_the_run()
        {
            double before = Placer.CoordinateTolerance;
            DesignValues.UseConfigOf(new JObject { ["profile"] = "x" });
            Assert.Equal(before, Placer.CoordinateTolerance);
        }
    }
}
