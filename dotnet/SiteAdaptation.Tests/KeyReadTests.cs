using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Round ADDIN Phase 2a on fakes: the value a spot elevation shows, its type and its elevation base (R320)
    public class KeyReadTests
    {
        private static string RepoText(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray()));
        }

        private static double Feet(double metres)
        {
            return UnitMath.MetresToFeet(metres);
        }

        [Fact]
        public void R320_the_value_is_the_single_or_upper_value_in_metres_with_the_lower_value_type_and_bases_beside_it()
        {
            var row = new SpotRow { ElementId = 7, Origin = new[] { 1.0, 2.0, 83.25 } };
            SpotValues.Apply(row, new RawSpotValue
            {
                UpperFeet = Feet(83.25),
                LowerFeet = Feet(83.1),
                TypeName = "Target (Project)",
                ElevationBase = "Survey Point",
                RelativeBase = "Level 1",
                DisplayElevations = "Actual (Selected) Elevation",
            });

            Assert.Equal(83.25, row.Value.Value, 9);
            Assert.Equal(83.1, row.LowerValue.Value, 9);
            Assert.Equal("Target (Project)", row.TypeName);
            Assert.Equal("Survey Point", row.ElevationBase);
            Assert.Equal("Level 1", row.RelativeBase);
            Assert.Equal("Actual (Selected) Elevation", row.DisplayElevations);
        }

        [Fact]
        public void R320_a_value_that_does_not_read_stays_null_and_is_never_taken_from_the_other_value()
        {
            var upperOnly = new SpotRow { ElementId = 8 };
            SpotValues.Apply(upperOnly, new RawSpotValue { UpperFeet = Feet(80.5) });
            var lowerOnly = new SpotRow { ElementId = 9 };
            SpotValues.Apply(lowerOnly, new RawSpotValue { LowerFeet = Feet(79.5) });
            var none = new SpotRow { ElementId = 10, Value = 1.0 };
            SpotValues.Apply(none, new RawSpotValue());

            Assert.Equal(80.5, upperOnly.Value.Value, 9);
            Assert.Null(upperOnly.LowerValue);
            Assert.Null(lowerOnly.Value);
            Assert.Equal(79.5, lowerOnly.LowerValue.Value, 9);
            Assert.Null(none.Value);
            Assert.Null(none.LowerValue);
            Assert.Null(none.TypeName);
        }

        [Fact]
        public void R320_the_spot_is_written_with_its_value_type_bases_and_every_instance_parameter()
        {
            var host = new LinkWallRampTests.FakeSource();
            var spot = new SpotRow { ElementId = 90, Origin = new[] { 1.25, 2.5, 83.4 }, View = "Grading", Parameters = new Dictionary<string, object> { ["Display Elevations"] = "Actual (Selected) Elevation", ["Single/Upper Value Prefix"] = "FFL " } };
            SpotValues.Apply(spot, new RawSpotValue { UpperFeet = Feet(83.4004), TypeName = "Spot", ElevationBase = "Survey Point" });
            host.SpotList.Add(spot);
            host.SpotList.Add(new SpotRow { ElementId = 91, Origin = new[] { 3.0, 4.0, 82.9 } });
            JObject document = LinkWallRampTests.Run(host);

            JToken first = document["spotElevations"][0];
            Assert.Equal(83.4, (double)first["value"], 9);
            Assert.Equal("Spot", (string)first["typeName"]);
            Assert.Equal("Survey Point", (string)first["elevationBase"]);
            Assert.Equal("FFL ", (string)first["parameters"]["Single/Upper Value Prefix"]);
            Assert.Null(first["lowerValue"]);
            JToken second = document["spotElevations"][1];
            Assert.Null(second["value"]);
            Assert.Null(second["typeName"]);
            Assert.Null(second["parameters"]);
        }

        [Fact]
        public void R320_a_spot_with_no_value_or_no_elevation_base_is_one_error_line_per_reason_with_the_count_and_the_first_ids()
        {
            var host = new LinkWallRampTests.FakeSource();
            // Seven spots of an STR link, as the SE keys hold them: the parameter is there and holds no value
            for (long id = 40; id < 47; id++)
            {
                var spot = new SpotRow { ElementId = id, Origin = new[] { 1.0, 2.0, 3.0 } };
                SpotValues.Apply(spot, new RawSpotValue { UpperMissing = SpotValues.NoValue, TypeName = "Target (Survey)", ElevationBase = "Survey Point" });
                host.SpotList.Add(spot);
            }
            var noBase = new SpotRow { ElementId = 50 };
            SpotValues.Apply(noBase, new RawSpotValue { UpperFeet = Feet(80.0), BaseMissing = SpotValues.NoParameter });
            host.SpotList.Add(noBase);
            var neither = new SpotRow { ElementId = 51 };
            SpotValues.Apply(neither, new RawSpotValue { UpperMissing = SpotValues.NoParameter, ElevationBase = " " });
            host.SpotList.Add(neither);
            var whole = new SpotRow { ElementId = 52 };
            SpotValues.Apply(whole, new RawSpotValue { UpperFeet = Feet(80.0), ElevationBase = "Survey Point" });
            host.SpotList.Add(whole);
            JObject document = LinkWallRampTests.Run(host);
            List<string> errors = document["errors"].Select(line => (string)line).ToList();

            Assert.Contains("spotElevations value: no Single/Upper Value, the parameter holds no value on 7 elements, first 5 element ids 40, 41, 42, 43, 44", errors);
            Assert.Contains("spotElevations value: no Single/Upper Value, the element has no such parameter on 1 element, element ids 51", errors);
            Assert.Contains("spotElevations elevation base: no Elevation Base, the element has no such parameter on 1 element, element ids 50", errors);
            Assert.Contains("spotElevations elevation base: no Elevation Base, the parameter holds no value on 1 element, element ids 51", errors);
            Assert.Equal(4, errors.Count(line => line.StartsWith("spotElevations ", StringComparison.Ordinal)));
            // Every spot stays in the list, and a Lower Value that does not read gives no line, a spot of one value has none
            Assert.Equal(10, document["spotElevations"].Count());
            Assert.DoesNotContain(errors, line => line.Contains("Lower"));
            Assert.Null(document["spotElevations"][0]["value"]);
        }

        [Fact]
        public void R320_the_reader_gives_the_reason_a_value_or_base_does_not_read_and_reads_the_base_off_the_spot_type()
        {
            string text = RepoText("dotnet", "RevitPrototypeReader", "RevitModelSource.cs");
            int start = text.IndexOf("public IEnumerable<SpotRow> SpotElevations()", StringComparison.Ordinal);
            int end = text.IndexOf("private SpotHost HostOf", start, StringComparison.Ordinal);
            string body = text.Substring(start, end - start);

            Assert.Contains("UpperMissing = ", body);
            Assert.Contains("BaseMissing = ", body);
            // The Elevation Base is a parameter of the spot type, labelled so in Revit 2024, never one of the instance
            Assert.Contains("spot.DimensionType.get_Parameter(BuiltInParameter.SPOT_ELEV_BASE)", body);
            foreach (string reason in new[] { "SpotValues.NoParameter", "SpotValues.NoValue", "SpotValues.NotANumber" })
            {
                Assert.Contains(reason, body);
            }
        }

        [Fact]
        public void R320_the_reader_asks_the_spot_elevation_values_and_no_longer_the_spot_coordinate_ones()
        {
            string text = RepoText("dotnet", "RevitPrototypeReader", "RevitModelSource.cs");
            int start = text.IndexOf("public IEnumerable<SpotRow> SpotElevations()", StringComparison.Ordinal);
            int end = text.IndexOf("private SpotHost HostOf", start, StringComparison.Ordinal);
            string body = text.Substring(start, end - start);

            foreach (string parameter in new[] { "SPOT_ELEV_SINGLE_OR_UPPER_VALUE", "SPOT_ELEV_LOWER_VALUE", "SPOT_ELEV_BASE", "SPOT_ELEV_RELATIVE_BASE", "SPOT_ELEV_DISPLAY_ELEVATIONS" })
            {
                Assert.Contains("BuiltInParameter." + parameter, body);
            }
            Assert.Contains("SpotValues.Apply(row, raw)", body);
            Assert.Contains("Parameters = AllParameters(spot", body);
            // Top Coordinate and Bottom Coordinate are the values of a spot coordinate and never read on a spot elevation
            Assert.DoesNotContain("SPOT_ELEV_TOP_VALUE", text);
            Assert.DoesNotContain("SPOT_ELEV_BOT_VALUE", text);
        }
    }
}
