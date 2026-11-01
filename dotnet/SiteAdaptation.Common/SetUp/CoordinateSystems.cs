using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // R454. One input and the coordinate system it states, by the GeoData of a drawing or the epsgCode of a LandXML. Name null when it
    // states none
    public sealed class StatedSystem
    {
        public string File { get; set; }
        public string Name { get; set; }

        // The problems CadExtractor met reading the drawing, so a drawing read in part shows it in its line
        public int Problems { get; set; }
    }

    public enum SystemAgreement
    {
        Agree,
        Conflict,
        NoneStated,
    }

    // R454. What the inputs state together. EpsgForMath is the code the site position takes, the one stated or coordinates.expectedEpsg
    public sealed class SystemsCheck
    {
        public SystemAgreement Outcome { get; set; }
        public int? Epsg { get; set; }
        public int EpsgForMath { get; set; }

        // One plain line per input for the Set up site window, listed and never a stop
        public List<string> Lines { get; set; } = new List<string>();

        // What stops button 1, a conflict, a code unlike the expected one or a name no code is known for
        public List<string> Stops { get; set; } = new List<string>();
    }

    // R454 to R456. The block coordinates of config/project.json, each key refused by its name when missing
    public sealed class CoordinateSettings
    {
        public const string FromCadSource = "cad";
        public const string FromPositionFile = "positionFile";

        public string Source { get; set; }
        public Dictionary<string, int> Systems { get; set; }
        public int ExpectedEpsg { get; set; }
        public double OriginRoundM { get; set; }
        public double AngleToTrueNorthDeg { get; set; }
        public double OriginElevationM { get; set; }
        public string PositionName { get; set; }

        // Null leaves the place name of the document as it is
        public string PlaceName { get; set; }
        public int ControlPointCount { get; set; }
        public double ControlPointLimitMm { get; set; }

        // The least and the most easting of a UTM zone, and the northing north and south of the equator, a plot box outside them is
        // in no UTM zone, likely local drawing coordinates
        public double[] UtmEastM { get; set; }
        public double[] UtmNorthM { get; set; }
        public double[] UtmSouthNorthM { get; set; }

        public bool FromCad
        {
            get { return Source == FromCadSource; }
        }

        public static CoordinateSettings From(JObject config)
        {
            JObject block = config["coordinates"] as JObject ?? throw new InvalidDataException("config holds no block coordinates, the coordinate system and site position of button 1 (R454)");
            string source = Text(block, "source");
            if (source != FromCadSource && source != FromPositionFile)
            {
                throw new InvalidDataException("coordinates.source is " + source + ", it is " + FromCadSource + " or " + FromPositionFile + " (R455)");
            }
            JObject systems = block["systems"] as JObject ?? throw new InvalidDataException("config holds no coordinates.systems, the EPSG code of each system name (R454)");
            var codes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JProperty item in systems.Properties())
            {
                codes[item.Name] = item.Value.Type == JTokenType.Integer ? item.Value.Value<int>() : throw new InvalidDataException("coordinates.systems." + item.Name + " is no whole EPSG code (R454)");
            }
            JToken place = block["placeName"];
            double count = Number(block, "controlPointCount");
            if (count < 1 || count != Math.Floor(count))
            {
                throw new InvalidDataException("coordinates.controlPointCount is no whole count above 0 (R456)");
            }
            var settings = new CoordinateSettings
            {
                Source = source, Systems = codes, ExpectedEpsg = (int)Number(block, "expectedEpsg"), OriginRoundM = Number(block, "originRoundM"),
                AngleToTrueNorthDeg = Number(block, "angleToTrueNorthDeg"), OriginElevationM = Number(block, "originElevationM"), PositionName = Text(block, "positionName"),
                PlaceName = place == null || place.Type == JTokenType.Null ? null : Text(block, "placeName"), ControlPointCount = (int)count, ControlPointLimitMm = Number(block, "controlPointLimitMm"),
                UtmEastM = Range(block, "utmEastM"), UtmNorthM = Range(block, "utmNorthM"), UtmSouthNorthM = Range(block, "utmSouthNorthM"),
            };
            if (settings.OriginRoundM <= 0 || settings.ControlPointLimitMm <= 0)
            {
                throw new InvalidDataException("coordinates.originRoundM and coordinates.controlPointLimitMm are lengths above 0 (R455, R456)");
            }
            return settings;
        }

        private static string Text(JObject block, string name)
        {
            JToken value = block[name];
            return value != null && value.Type == JTokenType.String && ((string)value).Length > 0 ? (string)value : throw new InvalidDataException("config holds no text coordinates." + name + " (R454)");
        }

        private static double Number(JObject block, string name)
        {
            JToken value = block[name];
            double number = value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) ? value.Value<double>() : double.NaN;
            return !double.IsNaN(number) && !double.IsInfinity(number) ? number : throw new InvalidDataException("config holds no number coordinates." + name + " (R454)");
        }

        private static double[] Range(JObject block, string name)
        {
            JArray pair = block[name] as JArray;
            double[] range = pair != null && pair.Count == 2 && pair.All(value => value.Type == JTokenType.Integer || value.Type == JTokenType.Float) ? pair.Select(value => value.Value<double>()).ToArray() : null;
            return range != null && range[0] < range[1] ? range : throw new InvalidDataException("coordinates." + name + " is no pair of the least and the most metres (R455)");
        }
    }

    // R454. The coordinate systems the inputs state, held against each other and against coordinates.expectedEpsg
    public static class CoordinateSystems
    {
        private const string EpsgPrefix = "EPSG:";

        public static SystemsCheck Agree(IReadOnlyList<StatedSystem> files, CoordinateSettings settings)
        {
            var check = new SystemsCheck();
            var stated = new List<KeyValuePair<string, int>>();
            foreach (StatedSystem file in files)
            {
                string problems = file.Problems > 0 ? ", " + Code(file.Problems) + " read problems" : "";
                if (string.IsNullOrWhiteSpace(file.Name))
                {
                    check.Lines.Add(file.File + " states no coordinate system" + problems);
                    continue;
                }
                int? code = EpsgOf(file.Name, settings);
                if (code == null)
                {
                    check.Lines.Add(file.File + " states " + file.Name + problems);
                    check.Stops.Add(file.File + " states the coordinate system " + file.Name + ", which coordinates.systems maps to no EPSG code");
                    continue;
                }
                check.Lines.Add(file.File + " states " + file.Name + ", EPSG " + Code(code.Value) + problems);
                stated.Add(new KeyValuePair<string, int>(file.File, code.Value));
            }
            List<int> codes = stated.Select(item => item.Value).Distinct().ToList();
            if (codes.Count > 1)
            {
                check.Outcome = SystemAgreement.Conflict;
                check.Stops.Add("The inputs state different coordinate systems: " + string.Join(", ", stated.Select(item => item.Key + " EPSG " + Code(item.Value))));
            }
            else if (codes.Count == 1)
            {
                check.Outcome = SystemAgreement.Agree;
                check.Epsg = codes[0];
                if (codes[0] != settings.ExpectedEpsg)
                {
                    check.Stops.Add("The inputs state EPSG " + Code(codes[0]) + ", not EPSG " + Code(settings.ExpectedEpsg) + " of coordinates.expectedEpsg");
                }
            }
            else
            {
                check.Outcome = SystemAgreement.NoneStated;
            }
            check.EpsgForMath = check.Epsg ?? settings.ExpectedEpsg;
            return check;
        }

        // R455. The baseplan states no system and takes the one of the Road Layout or of config, so its plot box is held to the range a
        // UTM zone of that code is valid in. Null when it lies inside, else the plain line that stops button 1
        public static string OutsideZone(IReadOnlyList<CadPlot> plots, CoordinateSettings settings, int epsg)
        {
            List<double[]> vertices = plots.Where(plot => plot.Vertices != null).SelectMany(plot => plot.Vertices).ToList();
            if (vertices.Count == 0)
            {
                return null;
            }
            UtmZone zone = UtmMath.ZoneOf(epsg);
            double[] east = { vertices.Min(vertex => vertex[0]), vertices.Max(vertex => vertex[0]) };
            double[] north = { vertices.Min(vertex => vertex[1]), vertices.Max(vertex => vertex[1]) };
            double[] northRange = zone.North ? settings.UtmNorthM : settings.UtmSouthNorthM;
            if (east[0] >= settings.UtmEastM[0] && east[1] <= settings.UtmEastM[1] && north[0] >= northRange[0] && north[1] <= northRange[1])
            {
                return null;
            }
            return "The plot outlines of the baseplan run " + Metres(east) + " m east and " + Metres(north) + " m north, outside the range of UTM zone "
                + Code(zone.Number) + (zone.North ? " north" : " south") + " of EPSG " + Code(epsg) + ", " + Metres(settings.UtmEastM) + " m east and " + Metres(northRange)
                + " m north. The baseplan is likely in drawing coordinates or another system, so button 1 takes no site position from it";
        }

        private static string Metres(double[] range)
        {
            return range[0].ToString("0", CultureInfo.InvariantCulture) + " to " + range[1].ToString("0", CultureInfo.InvariantCulture);
        }

        // A name of coordinates.systems, a bare code or EPSG:code, null for a name of no known code
        public static int? EpsgOf(string name, CoordinateSettings settings)
        {
            string trimmed = name.Trim();
            if (settings.Systems.TryGetValue(trimmed, out int mapped))
            {
                return mapped;
            }
            string digits = trimmed.StartsWith(EpsgPrefix, StringComparison.OrdinalIgnoreCase) ? trimmed.Substring(EpsgPrefix.Length) : trimmed;
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int code) ? code : (int?)null;
        }

        private static string Code(int code)
        {
            return code.ToString(CultureInfo.InvariantCulture);
        }
    }
}
