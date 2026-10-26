using System;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;

namespace SiteAdaptation.Common
{
    // R350. A number text read as the nearest double and a double written as a text that reads back as the same double, on net48 as on
    // net8.0 and in Python. .NET Framework 4.8 reads 2264.253152 one unit of the last bit off the nearest double and writes the double
    // of 2264.2531520000002 with the format R as 2264.253152, so a cell of the sales sheet reached Python as another number (RAN, the
    // parity of 4c, 6 cells of 2,591 rows on net48 and none on net8.0). Python float() and repr() are exact, this is the C# of both
    public static class ExactDouble
    {
        private static readonly double[] Tens = { 1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22 };

        // The nearest double to a decimal text of an optional sign, digits with an optional point and an optional exponent, ties to
        // even. A text of another form throws FormatException
        public static double Parse(string text)
        {
            string body = (text ?? "").Trim();
            bool negative = false;
            int at = 0;
            if (at < body.Length && (body[at] == '+' || body[at] == '-'))
            {
                negative = body[at] == '-';
                at++;
            }
            var digits = new System.Text.StringBuilder();
            int fraction = 0;
            bool point = false, any = false;
            for (; at < body.Length; at++)
            {
                char letter = body[at];
                if (letter >= '0' && letter <= '9')
                {
                    any = true;
                    if (digits.Length > 0 || letter != '0')
                    {
                        digits.Append(letter);
                    }
                    if (point)
                    {
                        fraction++;
                    }
                }
                else if (letter == '.' && !point)
                {
                    point = true;
                }
                else
                {
                    break;
                }
            }
            if (!any)
            {
                throw new FormatException("no digits in the number text " + text);
            }
            int exponent = 0;
            if (at < body.Length)
            {
                if ((body[at] != 'e' && body[at] != 'E') || !int.TryParse(body.Substring(at + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
                {
                    throw new FormatException("the number text " + text + " is not of digits, a point and an exponent");
                }
            }
            double value = Nearest(digits.ToString(), exponent - fraction);
            return negative ? -value : value;
        }

        // The nearest double to the whole number of digits times ten to the power ten
        private static double Nearest(string digits, int ten)
        {
            if (digits.Length == 0)
            {
                return 0;
            }
            // Fifteen digits or less and a power of ten a double holds exactly: one division or product, rounded once by IEEE
            if (digits.Length <= 15 && Math.Abs(ten) <= 22)
            {
                double whole = long.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
                return ten >= 0 ? whole * Tens[ten] : whole / Tens[-ten];
            }
            BigInteger numerator = BigInteger.Parse(digits, CultureInfo.InvariantCulture), denominator = BigInteger.One;
            if (ten >= 0)
            {
                numerator *= BigInteger.Pow(10, ten);
            }
            else
            {
                denominator = BigInteger.Pow(10, -ten);
            }
            return Quotient(numerator, denominator);
        }

        private static long BitLength(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();
            int top = bytes.Length - 1;
            while (top > 0 && bytes[top] == 0)
            {
                top--;
            }
            int bits = 0;
            for (int b = bytes[top]; b > 0; b >>= 1)
            {
                bits++;
            }
            return top * 8L + bits;
        }

        // The nearest double to a positive fraction: 53 bits of the quotient, ties to even, subnormal and overflow at the ends
        private static double Quotient(BigInteger numerator, BigInteger denominator)
        {
            long shift = 53 - (BitLength(numerator) - BitLength(denominator));
            BigInteger quotient, remainder, divisor;
            while (true)
            {
                BigInteger top = shift >= 0 ? numerator << (int)shift : numerator;
                divisor = shift >= 0 ? denominator : denominator << (int)-shift;
                quotient = BigInteger.DivRem(top, divisor, out remainder);
                if (quotient >= (BigInteger.One << 53))
                {
                    shift--;
                }
                else if (quotient < (BigInteger.One << 52))
                {
                    shift++;
                }
                else
                {
                    break;
                }
            }
            long biased = 1075 - shift;
            if (biased <= 0)
            {
                // A subnormal: the multiple of 2 to the minus 1074 nearest the fraction
                BigInteger scaled = BigInteger.DivRem(numerator << 1074, denominator, out remainder);
                int tie = (remainder * 2).CompareTo(denominator);
                if (tie > 0 || (tie == 0 && !scaled.IsEven))
                {
                    scaled += 1;
                }
                return BitConverter.Int64BitsToDouble((long)scaled);
            }
            int half = (remainder * 2).CompareTo(divisor);
            if (half > 0 || (half == 0 && !quotient.IsEven))
            {
                quotient += 1;
                if (quotient == (BigInteger.One << 53))
                {
                    quotient >>= 1;
                    biased++;
                }
            }
            if (biased >= 2047)
            {
                return double.PositiveInfinity;
            }
            return BitConverter.Int64BitsToDouble((biased << 52) | ((long)quotient - (1L << 52)));
        }

        // A double as a text that reads back as the same double on every runtime: G17, with a point when it holds none, so Python
        // reads a float and not an integer
        public static string Text(double value)
        {
            string text = value.ToString("G17", CultureInfo.InvariantCulture);
            return text.IndexOfAny(new[] { '.', 'E', 'e', 'N', 'I' }) < 0 ? text + ".0" : text;
        }

        // Writes every double of a JSON tree by Text, a value that is not finite as null
        public sealed class Converter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(double) || objectType == typeof(double?);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    writer.WriteNull();
                    return;
                }
                writer.WriteRawValue(Text(number));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}
