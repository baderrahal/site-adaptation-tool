using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354 to R357. What the build steps take from Python as Python gives it, so the C# of plan_from_book.py decides the same: the order of
    // link_summary.sort_key, str.isdigit and int of a plot number, and a JSON input read with every number the nearest double of its text
    public static class PyMath
    {
        // link_summary.sort_key: a text re.fullmatch(r"-?\d+(\.\d+)?") takes, \d any decimal digit, before every other text, by its float
        // then by the text, the other texts by the text
        private static readonly Regex NumberText = new Regex(@"^-?\d+(\.\d+)?\z");

        public static readonly IComparer<string> SortKey = Comparer<string>.Create(CompareSortKey);

        public static int CompareSortKey(string one, string other)
        {
            bool first = IsNumberText(one), second = IsNumberText(other);
            if (first != second)
            {
                return first ? -1 : 1;
            }
            if (first)
            {
                int byValue = Float(one).CompareTo(Float(other));
                if (byValue != 0)
                {
                    return byValue;
                }
            }
            return string.CompareOrdinal(one, other);
        }

        private static bool IsNumberText(string text)
        {
            return text != null && NumberText.IsMatch(text);
        }

        // Python float of a text of decimal digits, a sign and a point, each decimal digit of any script read as its value, as float() reads it
        private static double Float(string text)
        {
            var ascii = new StringBuilder();
            foreach (char letter in text)
            {
                int digit = CharUnicodeInfo.GetDecimalDigitValue(letter);
                ascii.Append(digit >= 0 ? (char)('0' + digit) : letter);
            }
            return ExactDouble.Parse(ascii.ToString());
        }

        // Python str.isdigit: one character or more, each with a digit value, a superscript digit among them
        public static bool IsDigit(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            for (int index = 0; index < text.Length; index += char.IsSurrogatePair(text, index) ? 2 : 1)
            {
                if (CharUnicodeInfo.GetDigitValue(text, index) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        // R355. Python math.dist of two points of the plane, the vector_norm of CPython 3.12: the differences scaled by a power of two,
        // squared and summed without loss, the square root corrected once, so the length of an edge is the double Python gives
        public static double Dist(double[] p, double[] q)
        {
            double a = Math.Abs(p[0] - q[0]), b = Math.Abs(p[1] - q[1]);
            bool nan = double.IsNaN(a) || double.IsNaN(b);
            double max = 0.0;
            if (a > max)
            {
                max = a;
            }
            if (b > max)
            {
                max = b;
            }
            return VectorNorm(new[] { a, b }, max, nan);
        }

        private const double DoubleMin = 2.2250738585072014E-308;

        private static double VectorNorm(double[] vec, double max, bool nan)
        {
            if (double.IsInfinity(max))
            {
                return max;
            }
            if (nan)
            {
                return double.NaN;
            }
            if (max == 0.0 || vec.Length <= 1)
            {
                return max;
            }
            int exponent = Exponent(max);
            if (exponent < -1023)
            {
                for (int index = 0; index < vec.Length; index++)
                {
                    vec[index] /= DoubleMin;
                }
                return DoubleMin * VectorNorm(vec, max / DoubleMin, nan);
            }
            double scale = PowerOfTwo(-exponent);
            double csum = 1.0, frac1 = 0.0, frac2 = 0.0;
            foreach (double value in vec)
            {
                double x = value * scale;
                double hi, lo, sumHi, sumLo;
                Product(x, x, out hi, out lo);
                FastSum(csum, hi, out sumHi, out sumLo);
                csum = sumHi;
                frac1 += lo;
                frac2 += sumLo;
            }
            double h = Math.Sqrt(csum - 1.0 + (frac1 + frac2));
            double productHi, productLo, totalHi, totalLo;
            Product(-h, h, out productHi, out productLo);
            FastSum(csum, productHi, out totalHi, out totalLo);
            csum = totalHi;
            frac1 += productLo;
            frac2 += totalLo;
            double rest = csum - 1.0 + (frac1 + frac2);
            h += rest / (2.0 * h);
            return h / scale;
        }

        // frexp: the exponent e of a positive finite value with value = m times 2 to the e and m from 0.5 up to below 1
        private static int Exponent(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            int biased = (int)((bits >> 52) & 0x7FF);
            if (biased != 0)
            {
                return biased - 1022;
            }
            long mantissa = bits & 0xFFFFFFFFFFFFFL;
            int shift = 0;
            while ((mantissa & (1L << 52)) == 0)
            {
                mantissa <<= 1;
                shift++;
            }
            return -1022 - shift + 1;
        }

        private static double PowerOfTwo(int power)
        {
            return power >= -1022 ? BitConverter.Int64BitsToDouble((long)(power + 1023) << 52) : BitConverter.Int64BitsToDouble(1L << (power + 1074));
        }

        // The product of two doubles as its rounded value and the exact rest, by the splits of Dekker, as fma gives it
        private static void Product(double x, double y, out double hi, out double lo)
        {
            hi = x * y;
            double xh, xl, yh, yl;
            Split(x, out xh, out xl);
            Split(y, out yh, out yl);
            lo = ((xh * yh - hi) + xh * yl + xl * yh) + xl * yl;
        }

        private static void Split(double value, out double hi, out double lo)
        {
            double part = 134217729.0 * value;
            hi = part - (part - value);
            lo = value - hi;
        }

        private static void FastSum(double a, double b, out double hi, out double lo)
        {
            hi = a + b;
            lo = b - (hi - a);
        }

        // Python float a % b: the rest with the sign of b, a rest of zero a zero of the sign of b, so -1E-20 % 360 is 360 and -0.0 % 360 is 0.0
        public static double Mod(double a, double b)
        {
            double rest = a % b;
            if (rest != 0)
            {
                if ((b < 0) != (rest < 0))
                {
                    rest += b;
                }
            }
            else
            {
                rest = b < 0 || (b == 0 && 1.0 / b < 0) ? -0.0 : 0.0;
            }
            return rest;
        }

        // math.degrees, x times the double of 180 over pi
        public static double Degrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        // corner_front.bearing: degrees clockwise from north of a plan vector, from 0 up to below 360 by Python %
        public static double Bearing(double[] vector)
        {
            return Mod(Degrees(Math.Atan2(vector[0], vector[1])), 360.0);
        }

        // cad_links.angle_of: degrees anticlockwise from east of a plan vector, by Python %
        public static double AngleOf(double[] vector)
        {
            return Mod(Degrees(Math.Atan2(vector[1], vector[0])), 360.0);
        }

        // cad_links.angle_gap: the turn between two angles in degrees, 0 to 180
        public static double AngleGap(double first, double second)
        {
            double gap = Mod(Math.Abs(first - second), 360.0);
            return Math.Min(gap, 360.0 - gap);
        }

        // The distance of GEOS from a point to a segment, Distance::pointToSegment, a segment of one point its distance from that point
        public static double SegmentDistance(double[] p, double[] a, double[] b)
        {
            if (a[0] == b[0] && a[1] == b[1])
            {
                return PointDistance(p, a);
            }
            double length2 = (b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]);
            double r = ((p[0] - a[0]) * (b[0] - a[0]) + (p[1] - a[1]) * (b[1] - a[1])) / length2;
            if (r <= 0.0)
            {
                return PointDistance(p, a);
            }
            if (r >= 1.0)
            {
                return PointDistance(p, b);
            }
            double s = ((a[1] - p[1]) * (b[0] - a[0]) - (a[0] - p[0]) * (b[1] - a[1])) / length2;
            return Math.Abs(s) * Math.Sqrt(length2);
        }

        private static double PointDistance(double[] p, double[] q)
        {
            double dx = p[0] - q[0], dy = p[1] - q[1];
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // A JSON input as Python json.loads reads it: every number with a point or an exponent the nearest double of its text by ExactDouble,
        // where double.Parse of net48 can take one unit of the last bit off, texts kept as texts and never taken for dates. A number past the
        // decimal type is refused with the role named
        public static JToken ReadJson(string path, string role)
        {
            JToken found;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(WorkFiles.ReadText(path))) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal })
                {
                    found = JToken.Load(reader);
                }
            }
            catch (JsonReaderException failure) when (failure.InnerException is OverflowException)
            {
                throw new InvalidDataException("the input " + role + " holds a number past 7.9E+28, which R385 never writes, " + failure.Message + " (R354)");
            }
            IEnumerable<JToken> tokens = found is JContainer container ? container.DescendantsAndSelf() : new[] { found };
            foreach (JValue value in tokens.OfType<JValue>().Where(item => item.Type == JTokenType.Float).ToList())
            {
                value.Value = ExactDouble.Parse(((decimal)value.Value).ToString(CultureInfo.InvariantCulture));
            }
            return found;
        }
    }
}
