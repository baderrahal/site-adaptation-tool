using System.Collections.Generic;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    public class JsonOutTests
    {
        private class Sample
        {
            public double Value { get; set; }
            public double? Missing { get; set; }
            public double[] Point { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
        }

        [Fact]
        public void R2_numbers_round_to_three_decimals()
        {
            string json = JsonOut.ToJson(new Sample { Value = 1.23456, Point = new[] { 10.0004, -2.5006, 3.0 } });
            Assert.Contains("\"value\": 1.235", json);
            Assert.Contains("[10, -2.501, 3]", json);
        }

        [Fact]
        public void R2_negative_zero_is_written_as_zero()
        {
            string json = JsonOut.ToJson(new Sample { Value = -0.0001, Point = new[] { -0.0004 } });
            Assert.Contains("\"value\": 0", json);
            Assert.DoesNotContain("-0", json);
        }

        [Fact]
        public void R2_boxed_parameter_values_are_rounded_too()
        {
            var sample = new Sample { Parameters = new Dictionary<string, object> { { "Width", 2.34567 }, { "Mark", "P-1" }, { "Count", 4 } } };
            string json = JsonOut.ToJson(sample);
            Assert.Contains("\"Width\": 2.346", json);
            Assert.Contains("\"Mark\": \"P-1\"", json);
            Assert.Contains("\"Count\": 4", json);
        }

        [Fact]
        public void R3_null_fields_are_left_out_and_dictionary_keys_keep_their_case()
        {
            string json = JsonOut.ToJson(new Sample { Parameters = new Dictionary<string, object> { { "Type Mark", "A" } } });
            Assert.DoesNotContain("missing", json);
            Assert.Contains("\"Type Mark\"", json);
        }

        [Fact]
        public void R2_not_a_number_becomes_null()
        {
            string json = JsonOut.ToJson(new Sample { Value = double.NaN });
            Assert.Contains("\"value\": null", json);
        }
    }
}
