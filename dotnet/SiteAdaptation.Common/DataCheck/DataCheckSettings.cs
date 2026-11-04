using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R600 to R605. The values of the dataCheck block of the project settings. The block holds every distance, share and pattern the data
    // check uses, each CHOSEN by Code with its reason, so no value of a project and no default is in the code. A block that is missing or
    // wrong is a refusal the check says in a plain line, never a value the code supplies
    public sealed class DataCheckSettings
    {
        public const string Block = "dataCheck";

        public ScanSettings Scan { get; private set; }
        public List<double> GroupGapsM { get; private set; }
        public double WordReachM { get; private set; }
        public double WordShare { get; private set; }
        public double RoadReachM { get; private set; }
        public int RoadMinSegments { get; private set; }
        public double StrongShare { get; private set; }
        public double UsableShare { get; private set; }
        public Regex PrototypeFile { get; private set; }

        public static DataCheckSettings From(JObject config)
        {
            JObject block = config?[Block] as JObject ?? throw new InvalidDataException("the project settings hold no dataCheck block (R600)");
            JObject scan = block["scan"] as JObject ?? throw new InvalidDataException("dataCheck holds no scan block (R600)");
            JArray gaps = block["groupGapsM"] as JArray;
            if (gaps == null || gaps.Count == 0 || gaps.Any(item => item.Type != JTokenType.Float && item.Type != JTokenType.Integer || (double)item <= 0))
            {
                throw new InvalidDataException("dataCheck.groupGapsM is no list of distances above 0 in metres (R601)");
            }
            List<double> list = gaps.Select(item => (double)item).ToList();
            for (int index = 1; index < list.Count; index++)
            {
                if (list[index] >= list[index - 1])
                {
                    throw new InvalidDataException("dataCheck.groupGapsM must fall from the largest group to the smallest (R601)");
                }
            }
            var settings = new DataCheckSettings
            {
                Scan = ScanSettings.From(new JObject { ["scan"] = scan }),
                GroupGapsM = list,
                WordReachM = Positive(block, "wordReachM", "R603"),
                WordShare = Share(block, "wordShare", "R603"),
                RoadReachM = Positive(block, "roadReachM", "R604"),
                RoadMinSegments = block["roadMinSegments"]?.Type == JTokenType.Integer && (int)block["roadMinSegments"] >= 1 ? (int)block["roadMinSegments"]
                    : throw new InvalidDataException("dataCheck.roadMinSegments is no whole number of 1 or more (R604)"),
                StrongShare = Share(block, "strongShare", "R605"),
                UsableShare = Share(block, "usableShare", "R605"),
            };
            if (settings.UsableShare > settings.StrongShare)
            {
                throw new InvalidDataException("dataCheck.usableShare must not be above strongShare (R605)");
            }
            string pattern = block["prototypeFilePattern"]?.Type == JTokenType.String ? (string)block["prototypeFilePattern"] : "";
            try
            {
                settings.PrototypeFile = new Regex(pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                throw new InvalidDataException("dataCheck.prototypeFilePattern is no pattern (R602)");
            }
            if (pattern.Length == 0 || !(settings.PrototypeFile.GetGroupNumbers().Length > 1))
            {
                throw new InvalidDataException("dataCheck.prototypeFilePattern names no group for the code of a prototype file (R602)");
            }
            return settings;
        }

        private static double Positive(JObject block, string key, string rule)
        {
            JToken value = block[key];
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer) || (double)value <= 0)
            {
                throw new InvalidDataException("dataCheck." + key + " is no number above 0 (" + rule + ")");
            }
            return (double)value;
        }

        private static double Share(JObject block, string key, string rule)
        {
            double value = Positive(block, key, rule);
            if (value > 1)
            {
                throw new InvalidDataException("dataCheck." + key + " is above 1 (" + rule + ")");
            }
            return value;
        }

        // R602. The code of a prototype file name, its first group of prototypeFilePattern, else null
        public string PrototypeCode(string fileName)
        {
            Match match = PrototypeFile.Match(Path.GetFileNameWithoutExtension(fileName ?? ""));
            return match.Success && match.Groups[1].Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : null;
        }

        // The metres one drawing unit is, from the units the drawing states. Unitless is taken as metres, the unit the plans of this tool are
        // drawn in, and a unit this table does not hold is null, so the check says the distances could not be turned into the drawing's unit
        public static double? MetersPerUnit(string units)
        {
            switch ((units ?? "").Trim())
            {
                case "":
                case "Unitless":
                case "Meters":
                    return 1.0;
                case "Millimeters":
                    return 0.001;
                case "Centimeters":
                    return 0.01;
                case "Decimeters":
                    return 0.1;
                case "Feet":
                    return 0.3048;
                case "Inches":
                    return 0.0254;
                default:
                    return null;
            }
        }

        public static string Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
