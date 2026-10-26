using System;
using System.Globalization;

namespace SiteAdaptation.Common
{
    // R385. The one rounding rule of C# and Python: the value written as its text of 15 significant digits, read as a decimal,
    // rounded half away from zero and turned back into a double. Math.Round on the double itself rounds its binary value, so
    // 0.5005, which a double holds a little under the tie, gave 0.5 where the decimal gives 0.501, as python/common.py does
    public static class Rounding
    {
        // A value that is not finite stays as it is, the JSON converters write it as null. A finite value past the decimal type,
        // 7.9E+28 or more, throws an OverflowException and is never written
        public static double Fixed(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }
            // NumberStyles.Float reads the exponent the text of a value under 1E-04 holds, such as 5.55111512312578E-17
            decimal text = decimal.Parse(value.ToString("G15", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
            double rounded = (double)Math.Round(text, decimals, MidpointRounding.AwayFromZero);
            return rounded == 0 ? 0 : rounded;
        }
    }
}
