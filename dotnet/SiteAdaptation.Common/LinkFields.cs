using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SiteAdaptation.Common
{
    // Turns the Name and the parameters of a link instance into the named fields of a link entry.
    // Nothing is guessed. A value the source does not hold is null and the gap is added to the error lines
    public static class LinkFields
    {
        public const string NameSeparator = " | ";
        public const string PlotNumber = "RRE_ZZ_SP_Plot-Number";
        public const string Prototype = "RRE_ZZ_SP_Prototype";
        public const string Ffl = "RRE_ZZ_SP_Prototype-FFL";
        public const string Steps = "RRE_ZZ_SP_Number-of-Steps";
        public const string Case = "CASE";
        public const string RvtName = "RRE_ZZ_SP_RVT-Name";
        private const string LocationSuffix = @"\s*:\s*location\b.*$";
        private const string FilePrefix = " : ";

        private static readonly string[] PartNames = { "plot", "prototype", "steps", "facadeColor", "designOption" };

        public static void Apply(LinkEntry entry, string name, Dictionary<string, object> parameters, IList<string> errors)
        {
            entry.RawName = name;
            entry.Parameters = parameters;
            entry.PlotNumber = Pick(parameters, PlotNumber, errors);
            entry.Prototype = Pick(parameters, Prototype, errors);
            entry.Ffl = Pick(parameters, Ffl, errors);
            entry.Steps = Pick(parameters, Steps, errors);
            entry.Case = Pick(parameters, Case, errors);
            entry.NameParts = Split(CleanName(name, parameters), errors);
        }

        // The shared parameter RRE_ZZ_SP_RVT-Name holds the name without the file name and the location text, so it wins when it is there.
        // Without it the file name in front of the first part and the location suffix on the last part are cut from the raw name
        public static string CleanName(string rawName, Dictionary<string, object> parameters)
        {
            object shared;
            var fromParameter = parameters != null && parameters.TryGetValue(RvtName, out shared) ? shared as string : null;
            if (!string.IsNullOrWhiteSpace(fromParameter))
            {
                return fromParameter.Trim();
            }
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return rawName;
            }
            string clean = Regex.Replace(rawName, LocationSuffix, "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            int firstPart = clean.IndexOf(NameSeparator, System.StringComparison.Ordinal);
            string head = firstPart < 0 ? clean : clean.Substring(0, firstPart);
            int prefix = head.IndexOf(FilePrefix, System.StringComparison.Ordinal);
            return prefix < 0 ? clean : clean.Substring(prefix + FilePrefix.Length);
        }

        public static NameParts Split(string name, IList<string> errors)
        {
            var parts = new NameParts();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Link name is empty");
                return parts;
            }
            string[] pieces = name.Split(new[] { NameSeparator }, System.StringSplitOptions.None);
            for (int index = 0; index < pieces.Length && index < PartNames.Length; index++)
            {
                string piece = pieces[index].Trim();
                if (piece.Length == 0)
                {
                    errors.Add("Link name part " + PartNames[index] + " is empty");
                    continue;
                }
                Set(parts, index, piece);
            }
            if (pieces.Length < PartNames.Length)
            {
                errors.Add("Link name has " + pieces.Length + " of " + PartNames.Length + " parts");
            }
            else if (pieces.Length > PartNames.Length)
            {
                errors.Add("Link name has " + pieces.Length + " parts and only the first " + PartNames.Length + " are read");
            }
            return parts;
        }

        private static void Set(NameParts parts, int index, string value)
        {
            switch (index)
            {
                case 0:
                    parts.Plot = value;
                    break;
                case 1:
                    parts.Prototype = value;
                    break;
                case 2:
                    parts.Steps = value;
                    break;
                case 3:
                    parts.FacadeColor = value;
                    break;
                default:
                    parts.DesignOption = value;
                    break;
            }
        }

        private static object Pick(Dictionary<string, object> parameters, string key, IList<string> errors)
        {
            object value;
            if (parameters == null || !parameters.TryGetValue(key, out value) || value == null)
            {
                errors.Add("Parameter " + key + " is missing");
                return null;
            }
            var text = value as string;
            if (text != null && text.Trim().Length == 0)
            {
                errors.Add("Parameter " + key + " is empty");
                return null;
            }
            return value;
        }
    }
}
