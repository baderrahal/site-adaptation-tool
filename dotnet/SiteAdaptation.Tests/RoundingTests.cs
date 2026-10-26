using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R385. One rounding rule in Python and C#: the 15 significant digit text of a value read as a decimal, half away from zero.
    // The values are those of python/tests/test_rounding.py, so both languages are held to the same results
    public class RoundingTests
    {
        private class Sample
        {
            public double Value { get; set; }

            [JsonConverter(typeof(JsonOut.DecimalsConverter), 4)]
            public double Wide { get; set; }

            public double[] Point { get; set; }
        }

        // The sign bit, since minus zero equals zero
        private static bool IsMinus(double value)
        {
            return BitConverter.DoubleToInt64Bits(value) < 0;
        }

        [Theory]
        [InlineData(12.3455, 12.346)]
        [InlineData(15.1235, 15.124)]
        [InlineData(1.0005, 1.001)]
        [InlineData(2.0465, 2.047)]
        [InlineData(7.0625, 7.063)]
        [InlineData(0.5005, 0.501)]
        public void R385_a_tie_rounds_half_away_from_zero(double value, double rounded)
        {
            Assert.Equal(rounded, Rounding.Fixed(value, 3));
            Assert.Equal(-rounded, Rounding.Fixed(-value, 3));
            Assert.Equal(rounded, JsonOut.Round3(value));
        }

        [Fact]
        public void R385_float_noise_does_not_move_a_value()
        {
            Assert.Equal("5.55111512312578E-17", (0.1 + 0.2 - 0.3).ToString("G15", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(0.0, Rounding.Fixed(0.1 + 0.2 - 0.3, 3));
            Assert.Equal(0.0, Rounding.Fixed(4E-06, 4));
            Assert.Equal(0.0, Rounding.Fixed(1.234E-05, 4));
            Assert.Equal(0.00001, Rounding.Fixed(1.234E-05, 5));
            Assert.Equal(0.38, Rounding.Fixed(150.38 - 150.0, 4));
            Assert.Equal(0.3, Rounding.Fixed(0.30000000000000004, 3));
            // 0.7 + 0.0005 is 0.7004999999999999 as a double, its 15 digit text is the tie 0.7005
            Assert.Equal(0.701, Rounding.Fixed(0.7 + 0.0005, 3));
            Assert.Equal(0.0001, Rounding.Fixed(0.00005, 4));
            Assert.Equal(3.0, Rounding.Fixed(2.5, 0));
            Assert.Equal(-3.0, Rounding.Fixed(-2.5, 0));
        }

        [Fact]
        public void R385_minus_zero_is_written_as_0_and_a_value_that_is_not_finite_is_left_as_it_is()
        {
            double zero = Rounding.Fixed(-0.0004, 3);
            Assert.Equal(0.0, zero);
            Assert.False(IsMinus(zero));
            Assert.False(IsMinus(Rounding.Fixed(-0.0, 3)));
            Assert.True(double.IsNaN(Rounding.Fixed(double.NaN, 3)));
            Assert.True(double.IsPositiveInfinity(Rounding.Fixed(double.PositiveInfinity, 3)));
            string json = JsonOut.ToJson(new Sample { Value = double.NaN, Wide = -0.00004, Point = new[] { -0.0004 } });
            Assert.Contains("\"value\": null", json);
            Assert.Contains("\"wide\": 0", json);
            Assert.DoesNotContain("-0", json);
        }

        [Fact]
        public void R385_a_value_the_decimal_cannot_hold_throws_and_is_never_written()
        {
            Assert.Throws<OverflowException>(() => Rounding.Fixed(1E+300, 3));
            Assert.Throws<OverflowException>(() => Rounding.Fixed(-1E+300, 3));
            Assert.Throws<OverflowException>(() => Rounding.Fixed(7.93E+28, 3));
            Assert.Equal(7.9E+27, Rounding.Fixed(7.9E+27, 3));
            // 2 to the 96 as a double has the 15 digit text 7.92281625142643E+28, under the largest decimal, as in Python
            Assert.Equal(7.92281625142643E+28, Rounding.Fixed(Math.Pow(2, 96), 3));
            Assert.Throws<OverflowException>(() => JsonOut.ToJson(new Sample { Value = 1E+300 }));
        }

        [Fact]
        public void R385_json_writes_ties_and_named_decimals_by_the_rule()
        {
            string json = JsonOut.ToJson(new Sample { Value = 0.7 + 0.0005, Wide = 1.00005, Point = new[] { 12.3455, 0.5005 } });
            Assert.Contains("\"value\": 0.701", json);
            Assert.Contains("\"wide\": 1.0001", json);
            Assert.Contains("[12.346, 0.501]", json);
            var boxed = JsonOut.ToJson(new Dictionary<string, object> { { "ffl", 7.0625 } });
            Assert.Contains("\"ffl\": 7.063", boxed);
        }
    }
}
