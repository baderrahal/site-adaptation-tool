using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R407 to R410, round 4i. The design basis of a build, designBasis of the config read once per run, python/design_basis.py: rev6 the Site
    // Adaptation Manual Rev 06 of the client, the other mode of designBasis.modes the past job. Under the past job nothing but the mode is read,
    // so a build is the build of before round 4i. Under rev6 El @ Entry is the input and the ramp slope the result (R408), the road level comes
    // from the one surface designBasis.roadLevel names (R409) and the back yard is a grade with no floor (R410)
    public sealed class DesignBasis
    {
        public const string Rev6 = "rev6";
        public const string CivilFile = "civilFile";
        public const string RoadPlusSlope = "roadPlusSlope";

        // The attachment of a townhouse in typologies of the rules profile, whose back yard 2.3.C grades toward the back
        private const string BothSides = "both_sides";

        // designBasis.modes holds rev6 and the one mode of the past job, whose name lives in config alone
        private const int ModeCount = 2;

        // design_basis.SLOPE_DECIMALS, the decimals the plan writes slopePercent of a ramp at (R2)
        public const int SlopeDecimals = 3;

        // design_basis.NUMBER, a number of the civil file as float() of Python and double.Parse both read it
        private static readonly Regex NumberText = new Regex(@"^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$", RegexOptions.CultureInvariant);

        public const string NoFloor = "designBasis.backYard.floor is true, the manual gives no level for a yard floor under rev6, so none is placed until Q225 is answered (R410)";

        public string Mode { get; private set; }

        public bool IsRev6 => Mode == Rev6;

        public string DoorSource { get; private set; }

        // The path of the civil file the levels were read from, its name in the reason of a plot it does not hold
        public string CivilPath { get; private set; }

        public Dictionary<string, double> Levels { get; private set; }

        public JObject Slopes { get; private set; }

        public string Surface { get; private set; }

        // R411. designBasis.clusters.propose under rev6, whether the build proposes a cluster and a mirror flag per unit plot
        public bool Propose { get; private set; }

        public bool Proposes => IsRev6 && Propose;

        private static InvalidDataException Refuse(string text)
        {
            return new InvalidDataException(text);
        }

        private static string Text(JToken value)
        {
            return value == null || value.Type == JTokenType.Null ? "none" : value.Type == JTokenType.String ? (string)value : value.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static bool Finite(JToken value)
        {
            return value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) && !double.IsNaN((double)value) && !double.IsInfinity((double)value);
        }

        private static JObject Block(JObject settings)
        {
            return settings["designBasis"] as JObject ?? throw Refuse("designBasis of the config is no object (R407)");
        }

        // design_basis.mode_of: designBasis.mode, one of designBasis.modes, any other value refused by its name
        public static string ModeOf(JObject settings)
        {
            JObject block = Block(settings);
            JArray modes = block["modes"] as JArray;
            JToken mode = block["mode"];
            string listed = modes == null ? "none" : string.Join(", ", modes.Select(Text));
            if (modes == null || modes.Any(item => item.Type != JTokenType.String) || modes.Select(item => (string)item).Distinct(StringComparer.Ordinal).Count() != ModeCount
                || !modes.Any(item => (string)item == Rev6))
            {
                throw Refuse("designBasis.modes must hold rev6 and the mode of the past job, it holds " + listed + " (R407)");
            }
            if (mode == null || mode.Type != JTokenType.String || !modes.Any(item => (string)item == (string)mode))
            {
                throw Refuse("designBasis.mode " + Text(mode) + " is no mode of designBasis.modes " + listed + " (R407)");
            }
            return (string)mode;
        }

        // design_basis.civil_levels: El @ Entry of each plot of the civil file by the text of plotColumn and the number of levelColumn. A file that
        // is missing, a column the header lacks, a level that is no finite number and a plot twice are refused
        public static Dictionary<string, double> CivilLevels(string path, JToken plotColumn, JToken levelColumn)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw Refuse("designBasis.doorLevel.civilFile.path " + (string.IsNullOrEmpty(path) ? "none" : path) + " is no file, the civil file of the door levels is missing (R408)");
            }
            string name = Path.GetFileName(path);
            List<Dictionary<string, string>> rows = CsvRows.Read(path, out List<string> header);
            foreach (KeyValuePair<JToken, string> column in new[] { new KeyValuePair<JToken, string>(plotColumn, "plotColumn"), new KeyValuePair<JToken, string>(levelColumn, "levelColumn") })
            {
                if (column.Key == null || column.Key.Type != JTokenType.String || !header.Contains((string)column.Key))
                {
                    throw Refuse("the civil file " + name + " holds no column " + Text(column.Key) + ", designBasis.doorLevel.civilFile." + column.Value + " (R408)");
                }
            }
            var found = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (Dictionary<string, string> row in rows)
            {
                string plot = (row.TryGetValue((string)plotColumn, out string cell) ? cell ?? "" : "").Trim();
                string text = (row.TryGetValue((string)levelColumn, out string level) ? level ?? "" : "").Trim();
                double value = double.NaN;
                if (NumberText.IsMatch(text))
                {
                    try
                    {
                        value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                    }
                    catch (OverflowException)
                    {
                        // net48 throws on a number past the range of a double, which Python reads as inf
                        value = double.PositiveInfinity;
                    }
                }
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw Refuse("the civil file " + name + " gives plot " + plot + " the level " + SiteClusters.TextRepr(text) + ", which is no finite number (R408)");
                }
                if (found.ContainsKey(plot))
                {
                    throw Refuse("the civil file " + name + " holds plot " + plot + " twice (R408)");
                }
                found[plot] = value;
            }
            return found;
        }

        // design_basis.basis_of: under the past job the mode alone, under rev6 also the back yard floor held false (R410), the door level source
        // with what it reads (R408) and the one road surface (R409). civilPath gives the path of the input of the civil file, asked only for civilFile
        public static DesignBasis Of(JObject settings, Func<string> civilPath)
        {
            JObject block = Block(settings);
            var basis = new DesignBasis { Mode = ModeOf(settings) };
            if (!basis.IsRev6)
            {
                return basis;
            }
            JToken floor = block["backYard"]?["floor"];
            if (floor != null && floor.Type == JTokenType.Boolean && (bool)floor)
            {
                throw Refuse(NoFloor);
            }
            if (floor == null || floor.Type != JTokenType.Boolean)
            {
                throw Refuse("designBasis.backYard.floor must be true or false, it holds " + PyRepr(floor) + " (R410)");
            }
            JObject door = block["doorLevel"] as JObject ?? throw Refuse("designBasis.doorLevel of the config is no object (R408)");
            JArray sources = door["sources"] as JArray;
            JToken source = door["source"];
            string named = source?.Type == JTokenType.String ? (string)source : null;
            if (sources == null || named == null || !sources.Any(item => item.Type == JTokenType.String && (string)item == named) || (named != CivilFile && named != RoadPlusSlope))
            {
                throw Refuse("designBasis.doorLevel.source " + Text(source) + " is no source of designBasis.doorLevel.sources, El @ Entry comes from civilFile or roadPlusSlope (R408)");
            }
            basis.DoorSource = named;
            if (named == CivilFile)
            {
                JObject civil = door["civilFile"] as JObject ?? new JObject();
                JToken configured = civil["path"];
                if (configured == null || configured.Type != JTokenType.String || ((string)configured).Length == 0)
                {
                    throw Refuse("designBasis.doorLevel.civilFile.path none is no file, the civil file of the door levels is missing (R408)");
                }
                basis.CivilPath = civilPath();
                basis.Levels = CivilLevels(basis.CivilPath, civil["plotColumn"], civil["levelColumn"]);
            }
            else
            {
                JObject table = door["slopePercent"] as JObject;
                if (table == null || !Finite(table["default"]) || table.Properties().Any(item => !Finite(item.Value)))
                {
                    throw Refuse("designBasis.doorLevel.slopePercent must hold a finite number for default and for each unit code it names (R408)");
                }
                basis.Slopes = table;
            }
            JToken surface = block["roadLevel"]?["surface"];
            if (surface == null || surface.Type != JTokenType.String || ((string)surface).Length == 0)
            {
                throw Refuse("designBasis.roadLevel.surface names no road surface, a build under rev6 takes the road level from the one surface it names (R409)");
            }
            basis.Surface = (string)surface;
            JToken propose = block["clusters"]?["propose"];
            if (propose == null || propose.Type != JTokenType.Boolean)
            {
                throw Refuse("designBasis.clusters.propose must be true or false, it holds " + PyRepr(propose) + " (R411)");
            }
            basis.Propose = (bool)propose;
            return basis;
        }

        // repr() of Python of a value of the config, for the line of a refusal
        private static string PyRepr(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return "None";
            }
            return value.Type == JTokenType.String ? SiteClusters.TextRepr((string)value) : value.Type == JTokenType.Boolean ? ((bool)value ? "True" : "False") : value.ToString(Newtonsoft.Json.Formatting.None);
        }

        // design_basis.door_entry: El @ Entry of a plot under rev6, unrounded, and why it has none: the level of the civil file, or the road at front
        // plus slopePercent of the unit, else its default, over the ramp depth
        public Tuple<double?, string> DoorEntry(string number, string unit, double[] front, double depth)
        {
            if (DoorSource == CivilFile)
            {
                return Levels.TryGetValue(number, out double level) ? Tuple.Create((double?)level, (string)null)
                    : Tuple.Create((double?)null, "the civil file " + Path.GetFileName(CivilPath) + " holds no row of plot " + number + ", so no El @ Entry (R408)");
            }
            double slope = (double)(unit != null && Slopes[unit] != null ? Slopes[unit] : Slopes["default"]);
            return Tuple.Create((double?)((front[0] + front[1]) / 2.0 + slope / 100.0 * depth), (string)null);
        }

        // design_basis.slope_of: the ramp slope in percent, the result, El @ Entry less the road at front over the ramp depth
        public static double SlopeOf(double entry, double[] front, double depth)
        {
            return (entry - (front[0] + front[1]) / 2.0) / depth * 100.0;
        }

        // design_basis.outside_manual: a slope outside slopes_percent front_min to front_max of the rules profile, manual 2.3.A, as the plan writes it
        public static bool OutsideManual(double slope, JObject slopes)
        {
            double written = Rounding.Fixed(slope, SlopeDecimals);
            return written < BuildFace.Number(slopes["front_min"]) || written > BuildFace.Number(slopes["front_max"]);
        }

        // floor_fit.type_of: the ramp type of a slope from the bands, the first whose upper limit lies above it, null for none
        public static JToken RampType(double slope, JArray bands)
        {
            foreach (JObject band in bands.OfType<JObject>())
            {
                JToken below = band["belowPercent"];
                if (below == null || below.Type == JTokenType.Null || slope < BuildFace.Number(below))
                {
                    return band["type"]?.DeepClone();
                }
            }
            return null;
        }

        // design_basis.yard_grade: a unit of attachment both_sides toward the back at townhouse_back_rev6 (2.3.C), any other toward the front within
        // back_rev6_min to back_rev6_max (2.3.B), both in percent
        public static JObject YardGrade(string unit, JObject rules)
        {
            JObject slopes = (JObject)rules["slopes_percent"];
            JToken attachment = (rules["typologies"]?[unit] as JObject)?["attachment"];
            if (attachment != null && attachment.Type == JTokenType.String && (string)attachment == BothSides)
            {
                return new JObject { ["toward"] = "back", ["minPercent"] = slopes["townhouse_back_rev6"].DeepClone(), ["maxPercent"] = slopes["townhouse_back_rev6"].DeepClone() };
            }
            return new JObject { ["toward"] = "front", ["minPercent"] = slopes["back_rev6_min"].DeepClone(), ["maxPercent"] = slopes["back_rev6_max"].DeepClone() };
        }

        // The ramp bands of rampTypes of the floor file the input floorOutlines names, under rev6
        public static JArray Bands(JObject floors)
        {
            return floors?["rampTypes"]?["bands"] as JArray ?? throw Refuse("the floor file of the input floorOutlines holds no rampTypes.bands, the ramp type of a slope under rev6 (R408)");
        }
    }
}
