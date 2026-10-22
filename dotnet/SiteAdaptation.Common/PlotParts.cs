using System.Text.RegularExpressions;

namespace SiteAdaptation.Common
{
    // The Plot parameter of a floor or a wall joins the unit and the plot number with a dash, for example VD2-2441
    public static class PlotParts
    {
        public const string ParameterName = "Plot";

        private static readonly Regex Shape = new Regex(@"^\s*(?<unit>.*?\S)\s*-\s*(?<number>\d+)\s*$", RegexOptions.CultureInvariant);

        // The number is the digits after the last dash and the unit is the text before it. Anything else does not parse
        public static bool TryParse(object value, out string unit, out string number)
        {
            unit = null;
            number = null;
            var text = value == null ? null : System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            Match match = Shape.Match(text);
            if (!match.Success)
            {
                return false;
            }
            unit = match.Groups["unit"].Value;
            number = match.Groups["number"].Value;
            return true;
        }
    }
}
