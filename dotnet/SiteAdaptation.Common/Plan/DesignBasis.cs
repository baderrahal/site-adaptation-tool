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
    // from the first of the surfaces designBasis.roadLevel names in order that covers a corner (R409, R490, R491), a zone mostly off them stops
    // the build (R493), the back yard is a grade with no floor (R410) and designBasis.split.test turns on the split of a cluster by slope and steps (R414)
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

        public const string BothSurfaces = "designBasis.roadLevel holds both surface and surfaces, a build under rev6 takes its road surfaces from surfaces alone (R490)";
        public const string NoSurfaces = "designBasis.roadLevel names no road surface, a build under rev6 takes the road level from designBasis.roadLevel.surfaces in their order (R490)";
        public const string BadSurfaces = "designBasis.roadLevel.surfaces must be a list in order, not empty, of non empty unique texts (R490)";
        public const string NotRoadShape = "designBasis.roadLevel.notRoad must be an object of file stem to why (R485)";
        public const string BadPick = "the road surfaces of the request must be a list in order, not empty, of non empty unique texts (R492)";

        // The config the basis was read from, for the reasons of siteBuild.roadFolder.notTaken a pick is held to
        private JObject read;

        // R493. The name of a plot whose zone column is blank, the sentence of a zone that stops a build and the end of the message, by which 4 Check knows it
        private const string NoZone = "no zone";
        public const string NoRoadEnd = "(R493)";

        public string Mode { get; private set; }

        public bool IsRev6 => Mode == Rev6;

        public string DoorSource { get; private set; }

        // The path of the civil file the levels were read from, its name in the reason of a plot it does not hold
        public string CivilPath { get; private set; }

        public Dictionary<string, double> Levels { get; private set; }

        public JObject Slopes { get; private set; }

        // R490. The road surfaces of a build under rev6 in order, those of designBasis.roadLevel or the pick of the request (R492)
        public List<string> Surfaces { get; private set; }

        // R493. designBasis.roadLevel.noRoadStopShare under rev6, 0 for a config with none, which stops at any plot with no road level (CHOSEN)
        public double NoRoadStopShare { get; private set; }

        // R411. designBasis.clusters.propose under rev6, whether the build proposes a cluster and a mirror flag per unit plot
        public bool Propose { get; private set; }

        public bool Proposes => IsRev6 && Propose;

        // R414. designBasis.split under rev6: test, the size of a cluster it tests and the walls it tries in order
        public bool SplitTest { get; private set; }

        public int SplitUnits { get; private set; }

        public List<int> SplitWalls { get; private set; }

        public bool Splits => IsRev6 && SplitTest;

        public const string SplitWallsLine = "designBasis.split.units must be a whole number of 2 or more and designBasis.split.walls a list of the walls it tries in order, each a wall after a unit inside the cluster (R414)";

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
        // with what it reads (R408), the road surfaces in order (R490) and the limit of plots with no road level (R493). civilPath gives the path of the
        // input of the civil file, asked only for civilFile. R795: picked is the pick of Set up the request carries, a new project names no road surface
        // in config, so under rev6 with a pick an empty designBasis.roadLevel.surfaces is no refusal, TakePick then gives the surfaces
        public static DesignBasis Of(JObject settings, Func<string> civilPath, IReadOnlyList<string> picked = null)
        {
            JObject block = Block(settings);
            var basis = new DesignBasis { Mode = ModeOf(settings), read = settings };
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
            JObject road = block["roadLevel"] as JObject ?? new JObject();
            basis.Surfaces = picked != null && road["surfaces"] is JArray none && none.Count == 0 && road.Property("surface") == null ? new List<string>() : SurfacesOf(road);
            JToken notRoad = road["notRoad"];
            if (notRoad != null && !(notRoad is JObject listed && listed.Properties().All(item => item.Name.Length > 0 && item.Value.Type == JTokenType.String && ((string)item.Value).Length > 0)))
            {
                throw Refuse(NotRoadShape);
            }
            foreach (string name in basis.Surfaces)
            {
                string why = Settings.RoadSurfaceChoices.NotTakenWhy(settings, name);
                if (why != null)
                {
                    throw Refuse("designBasis.roadLevel.surfaces names " + name + ", which is no road surface: " + why + " (R490)");
                }
                why = Settings.RoadSurfaceChoices.NotRoadWhy(settings, name);
                if (why != null)
                {
                    throw Refuse(Settings.RoadSurfaceChoices.NotRoadLine(name, why));
                }
            }
            basis.NoRoadStopShare = StopShareOf(road);
            JToken propose = block["clusters"]?["propose"];
            if (propose == null || propose.Type != JTokenType.Boolean)
            {
                throw Refuse("designBasis.clusters.propose must be true or false, it holds " + PyRepr(propose) + " (R411)");
            }
            basis.Propose = (bool)propose;
            ReadSplit(block["split"] as JObject ?? new JObject(), basis);
            return basis;
        }

        // design_basis.surfaces_of: the road surfaces in order, non empty and unique texts, the one surface of an old config a list of one, both keys
        // together, no value or a bad value refused by name
        private static List<string> SurfacesOf(JObject road)
        {
            if (road.Property("surface") != null && road.Property("surfaces") != null)
            {
                throw Refuse(BothSurfaces);
            }
            if (road.Property("surfaces") == null)
            {
                JToken surface = road["surface"];
                if (surface == null || surface.Type != JTokenType.String || ((string)surface).Length == 0)
                {
                    throw Refuse(NoSurfaces);
                }
                return new List<string> { (string)surface };
            }
            JArray surfaces = road["surfaces"] as JArray;
            if (surfaces == null || surfaces.Any(name => name.Type != JTokenType.String) || !GoodList(surfaces.Select(name => (string)name).ToList()))
            {
                throw Refuse(BadSurfaces);
            }
            return surfaces.Select(name => (string)name).ToList();
        }

        private static bool GoodList(IReadOnlyList<string> names)
        {
            return names.Count > 0 && names.All(name => !string.IsNullOrEmpty(name)) && names.Distinct(StringComparer.Ordinal).Count() == names.Count;
        }

        // design_basis.stop_share_of: a number from 0 to 1 and no bool, refused by name, a config with none reads 0
        private static double StopShareOf(JObject road)
        {
            if (road.Property("noRoadStopShare") == null)
            {
                return 0;
            }
            JToken share = road["noRoadStopShare"];
            if (!Finite(share) || (double)share < 0 || (double)share > 1)
            {
                throw Refuse("designBasis.roadLevel.noRoadStopShare must be a number from 0 to 1, it holds " + PyRepr(share) + " (R493)");
            }
            return (double)share;
        }

        // R492. The pick of Set up in the request, under rev6 in place of the surfaces of config, ignored under the past job
        public void TakePick(IReadOnlyList<string> picked)
        {
            if (!IsRev6 || picked == null)
            {
                return;
            }
            if (!GoodList(picked))
            {
                throw Refuse(BadPick);
            }
            foreach (string name in picked)
            {
                string why = Settings.RoadSurfaceChoices.NotTakenWhy(read, name);
                if (why != null)
                {
                    throw Refuse("the road surfaces of the request name " + name + ", which is no road surface: " + why + " (R492)");
                }
                why = Settings.RoadSurfaceChoices.NotRoadWhy(read, name);
                if (why != null)
                {
                    throw Refuse(Settings.RoadSurfaceChoices.NotRoadLine(name, why));
                }
            }
            Surfaces = picked.ToList();
        }

        // design_basis.no_road_stop: under rev6 the message that stops a build, one sentence per zone in zone order whose share of its reached unit plots
        // that have a front and lack a road level at a corner, the gaps, lies above NoRoadStopShare, else null. Exactly at the share does not stop, a few
        // plots stay listed in the summary, and so does a plot with no front, which no road surface can mend
        public string NoRoadStop(IEnumerable<string> reached, IEnumerable<string> gaps, IReadOnlyDictionary<string, Dictionary<string, string>> rows)
        {
            if (!IsRev6)
            {
                return null;
            }
            string ZoneOf(string number)
            {
                string zone = null;
                if (rows.TryGetValue(number, out Dictionary<string, string> row))
                {
                    row.TryGetValue("zone", out zone);
                }
                zone = (zone ?? "").Trim();
                return zone.Length == 0 ? NoZone : zone;
            }
            var plots = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string zone in reached.Select(ZoneOf))
            {
                plots.TryGetValue(zone, out int count);
                plots[zone] = count + 1;
            }
            var lacking = gaps.GroupBy(ZoneOf, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            string names = string.Join(", ", Surfaces.Select(ReadSettings.RoadName));
            List<string> lines = plots.Where(zone => lacking.ContainsKey(zone.Key) && (double)lacking[zone.Key] / zone.Value > NoRoadStopShare)
                .Select(zone => zone.Key + ": " + lacking[zone.Key].ToString(CultureInfo.InvariantCulture) + " of " + zone.Value.ToString(CultureInfo.InvariantCulture)
                    + " plots have no road level on the road surfaces named (" + names + "). What to do: add the road surface of this zone in Set up.").ToList();
            return lines.Count == 0 ? null : string.Join(" ", lines) + " " + NoRoadEnd;
        }

        // R493. A stop or a line of this rule, whose text goes to the user as it stands
        public static bool IsNoRoad(string message)
        {
            return message != null && message.EndsWith(NoRoadEnd, StringComparison.Ordinal);
        }

        // R493. The one issue of a run under rev6 below the limit with the plots it leaves with no road level, null for none
        public static string NoRoadLine(IReadOnlyCollection<string> noLevel)
        {
            return noLevel.Count == 0 ? null : noLevel.Count.ToString(CultureInfo.InvariantCulture) + " plots have no road level and are listed in the plan: " + string.Join(", ", noLevel) + " " + NoRoadEnd;
        }

        // design_basis.split_of: test true or false, units a whole number of 2 or more and walls a list of walls after a unit inside the cluster
        private static void ReadSplit(JObject split, DesignBasis basis)
        {
            JToken test = split["test"];
            if (test == null || test.Type != JTokenType.Boolean)
            {
                throw Refuse("designBasis.split.test must be true or false, it holds " + PyRepr(test) + " (R414)");
            }
            JToken units = split["units"];
            JArray walls = split["walls"] as JArray;
            if (units == null || units.Type != JTokenType.Integer || (long)units < 2 || walls == null || walls.Count == 0
                || walls.Any(wall => wall.Type != JTokenType.Integer || (long)wall <= 0 || (long)wall >= (long)units))
            {
                throw Refuse(SplitWallsLine);
            }
            basis.SplitTest = (bool)test;
            basis.SplitUnits = (int)units;
            basis.SplitWalls = walls.Select(wall => (int)wall).ToList();
        }

        // repr() of Python of a value of the config, for the line of a refusal
        private static string PyRepr(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return "None";
            }
            if (value.Type == JTokenType.Float && (double.IsNaN((double)value) || double.IsInfinity((double)value)))
            {
                return double.IsNaN((double)value) ? "nan" : (double)value > 0 ? "inf" : "-inf";
            }
            return value.Type == JTokenType.String ? SiteClusters.TextRepr((string)value) : value.Type == JTokenType.Boolean ? ((bool)value ? "True" : "False") : value.ToString(Newtonsoft.Json.Formatting.None);
        }

        // design_basis.road_plus_slope: the road at front plus a slope in percent over the ramp depth, unrounded
        public static double RoadPlusSlopeOf(double[] front, double slope, double depth)
        {
            return (front[0] + front[1]) / 2.0 + slope / 100.0 * depth;
        }

        // design_basis.door_entry: El @ Entry of a plot under rev6, unrounded, and why it has none: the level of the civil file, or the road at front
        // plus slopePercent of the unit, else its default, over the ramp depth, or plus joined, the slope of a shared back corner (R515)
        public Tuple<double?, string> DoorEntry(string number, string unit, double[] front, double depth, double? joined = null)
        {
            if (DoorSource == CivilFile)
            {
                return Levels.TryGetValue(number, out double level) ? Tuple.Create((double?)level, (string)null)
                    : Tuple.Create((double?)null, "the civil file " + Path.GetFileName(CivilPath) + " holds no row of plot " + number + ", so no El @ Entry (R408)");
            }
            double slope = joined ?? (double)(unit != null && Slopes[unit] != null ? Slopes[unit] : Slopes["default"]);
            return Tuple.Create((double?)RoadPlusSlopeOf(front, slope, depth), (string)null);
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

        // plan_from_book.build: the plots of numbers with a slope whose slope lies outside the manual, each with that slope at 3 decimals
        public static JArray OutsideList(IEnumerable<string> numbers, IReadOnlyDictionary<string, double> slopes, JObject limits)
        {
            return new JArray(numbers.Where(number => slopes.ContainsKey(number) && OutsideManual(slopes[number], limits))
                .Select(number => new JObject { ["plot"] = number, ["slopePercent"] = Rounding.Fixed(slopes[number], SlopeDecimals) }));
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
