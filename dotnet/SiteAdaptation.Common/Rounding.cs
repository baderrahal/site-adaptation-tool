using System;
using System.Globalization;

namespace SiteAdaptation.Common
{
    // R385. The one rounding rule of C# and Python: the value written as its text of 15 significant digits, read as a decimal,
    // rounded half away from zero and turned back into a double. Math.Round on the double itself rounds its binary value, so
    // 0.5005, which a double holds a little under the tie, gave 0.5 where the decimal gives 0.501, as python/common.py does
    public static class Rounding
    {
        // The significant digits of the text a value is rounded from, those of the G15 text of a double below, no project value (Q103)
        public const int Significant = 15;

        // A value that is not finite stays as it is, the JSON converters write it as null. A value whose digit after the asked
        // decimals, the one that decides a tie, lies past the 15 digits of its text throws an OverflowException and is never
        // written: the text itself would round there, and G15 of net48 breaks an exact tie of 16 digits half up where net8.0 and
        // Python go half to even, so 1234567890123.125 would give 1234567890123.13 and 1234567890123.12
        public static double Fixed(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }
            // NumberStyles.Float reads the exponent the text of a value under 1E-04 holds, such as 5.55111512312578E-17
            string g15 = value.ToString("G15", CultureInfo.InvariantCulture);
            decimal text = decimal.Parse(g15, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (text != 0 && Exponent(text) + decimals + 2 > Significant)
            {
                throw new OverflowException(g15 + " needs " + (Exponent(text) + decimals + 2) + " significant digits to round at " + decimals + " decimals, more than the " + Significant + " of its text (R385)");
            }
            double rounded = (double)Math.Round(text, decimals, MidpointRounding.AwayFromZero);
            return rounded == 0 ? 0 : rounded;
        }

        // The power of ten of the first digit, as Decimal.adjusted() of Python gives it, exact on the decimal
        private static int Exponent(decimal value)
        {
            value = Math.Abs(value);
            int exponent = 0;
            while (value >= 10)
            {
                value /= 10;
                exponent++;
            }
            while (value < 1)
            {
                value *= 10;
                exponent--;
            }
            return exponent;
        }
    }
}
