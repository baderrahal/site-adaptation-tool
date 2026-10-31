using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R359. El @ Entry of a plot with a road level at both front corners: the unrounded value of plan_rules.entry_of and the rule, that
    // value at the 4 decimals of R2 by R385, as R339 measured it
    public sealed class EntryLevel
    {
        public string Unit { get; set; }

        public double Raw { get; set; }

        public double Rule { get; set; }

        // R408. The ramp slope in percent: under rev6 the result of El @ Entry, under the past job the slope of the level profile
        public double Slope { get; set; }
    }

    // R359. The levels of a plot by keyRules: the piece that shares its FFL, the highest El @ Entry plus base of the piece unrounded and
    // the FFL, that value at 3 decimals
    public sealed class LevelRow
    {
        public List<string> Piece { get; set; }

        public double Top { get; set; }

        public double Ffl { get; set; }
    }

    // R359. What the levels of a build take, as plan_from_book.build gives them: the row partners, the plots whose levels it works out, their
    // fronts with the road levels, El @ Entry and the levels of each
    public sealed class LevelBuild
    {
        public Dictionary<string, string> Partners { get; set; }

        public List<string> Members { get; set; }

        public Dictionary<string, FrontItem> Fronts { get; set; }

        public Dictionary<string, EntryLevel> Entries { get; set; }

        public Dictionary<string, LevelRow> Rows { get; set; }

        // R407. The design basis of the build, read once, and under rev6 the bands of the ramp types of the floor file (R408)
        public DesignBasis Basis { get; set; }

        public JArray Bands { get; set; }

        // R408. Under rev6 why each plot with a road level has no El @ Entry, the plots of the scope with none and the plots of the scope whose
        // slope lies outside slopes_percent front_min to front_max, null under the past job
        public Dictionary<string, string> NoDoorWhy { get; set; }

        public JArray NoDoor { get; set; }

        public JArray Outside { get; set; }

        public bool Rev6 => Basis != null && Basis.IsRev6;
    }

    // R359, R339, R340. The levels of a build by the level rules of keyRules, plan_rules.entry_of, row_partners, key_pieces, level_pieces,
    // key_level_rows, steps_by_rule and prototype_ffl and plan_from_book.key_levels and rule_levels, before an override of the list moves
    // them, which step 12 applies (R360)
    public static class KeyLevels
    {
        public const string StepName = "levels";

        // The line of plan_from_book.LEVELS_NO_FFL
        public const string NoFfl = "the steps levels and floors of the parity harness are a build by the level rules of keyRules, keyRules of {0} codes no ffl, the C# builds the levels by keyRules alone (R359)";

        private const string ParameterWord = "parameter ";
        private static readonly string[] LevelRules = { "ffl", "fflDownToTheCm" };

        // plan_rules.road_at_front: the mean of the road levels at the two front corners, unrounded
        public static double RoadAtFront(double[] front)
        {
            return (front[0] + front[1]) / 2.0;
        }

        // plan_rules.slope_of: rampSlopePercent of the code, else its default
        public static double SlopeOf(string code, JObject levelProfile)
        {
            JObject table = (JObject)levelProfile["rampSlopePercent"];
            return BuildFace.Number(code != null && table[code] != null ? table[code] : table["default"]);
        }

        // plan_rules.entry_of: the road at front plus offsetM of frontRoad plus the slope of the unit along the ramp depth, in this order
        public static double EntryOf(double[] front, string code, JObject levelProfile, double depth)
        {
            double slope = SlopeOf(code, levelProfile);
            return RoadAtFront(front) + BuildFace.Number(levelProfile["frontRoad"]["offsetM"]) + slope / 100.0 * depth;
        }

        // plan_rules.row_partners: a right unit pairs with the one unit beside it on the left that is a left unit, and the other way round
        public static Dictionary<string, string> RowPartners(IReadOnlyDictionary<string, string> sides, IReadOnlyDictionary<string, Sides> neighbours)
        {
            var found = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> item in sides)
            {
                if (item.Value != "left" && item.Value != "right")
                {
                    continue;
                }
                string other = item.Value == "left" ? "right" : "left";
                List<string> next = neighbours.TryGetValue(item.Key, out Sides beside) && beside != null ? (other == "left" ? beside.Left : beside.Right) : new List<string>();
                List<string> held = next.Where(plot => sides.TryGetValue(plot, out string side) && side == other).ToList();
                if (held.Count == 1)
                {
                    found[item.Key] = held[0];
                }
            }
            return found;
        }

        // int() of Python on a text: its digits with a sign and blanks around, else the ValueError line
        private static int WholeNumber(string text)
        {
            if (!int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int found))
            {
                throw new InvalidDataException("invalid literal for int() with base 10: " + SiteClusters.TextRepr(text));
            }
            return found;
        }

        // plan_rules.split_walls: the walls a split pattern names in a cluster of count units, a wall n after unit n
        public static List<int> SplitWalls(JToken pattern, int count)
        {
            if (pattern == null || pattern.Type == JTokenType.Null || (pattern.Type == JTokenType.String && (string)pattern == "none"))
            {
                return new List<int>();
            }
            string text = SiteClusters.PyText(pattern);
            if (text == "middle")
            {
                return count % 2 == 0 ? new List<int> { count / 2 } : new List<int>();
            }
            if (!text.StartsWith("walls ", StringComparison.Ordinal))
            {
                throw new InvalidDataException("keyRules.levelSplit names the pattern " + text + ", a build gives none, middle or walls n,m (R339)");
            }
            return new SortedSet<int>(text.Substring(6).Split(',').Select(WholeNumber).Where(wall => wall > 0 && wall < count)).ToList();
        }

        // plan_rules.level_pieces: the cluster of a plot in row order cut at the walls of its units, a unit in no cluster with its row partner when
        // each names the other, else the plot alone
        public static Dictionary<string, List<string>> LevelPieces(IEnumerable<string> plots, IReadOnlyDictionary<string, ClusterRole> roles, IReadOnlyDictionary<string, string> partners,
            IReadOnlyDictionary<string, string> units, JObject split)
        {
            var found = new Dictionary<string, List<string>>();
            foreach (string plot in plots)
            {
                if (roles.TryGetValue(plot, out ClusterRole role) && role != null)
                {
                    List<string> ordered = role.Plots.ToList();
                    string named = string.Join(" ", ordered.Select(other => units.TryGetValue(other, out string unit) && unit != null ? unit : "None"));
                    List<int> walls = SplitWalls(split?[named], ordered.Count);
                    walls.Add(ordered.Count);
                    int start = 0;
                    foreach (int wall in walls)
                    {
                        List<string> part = ordered.Skip(start).Take(Math.Max(0, wall - start)).ToList();
                        if (part.Contains(plot))
                        {
                            found[plot] = part;
                            break;
                        }
                        start = wall;
                    }
                }
                else if (partners.TryGetValue(plot, out string partner) && !string.IsNullOrEmpty(partner) && partners.TryGetValue(partner, out string back) && back == plot)
                {
                    found[plot] = new[] { plot, partner }.OrderBy(number => number.Length).ThenBy(number => number, StringComparer.Ordinal).ToList();
                }
                else
                {
                    found[plot] = new List<string> { plot };
                }
            }
            return found;
        }

        // plan_rules.key_pieces: the pieces of keyRules.ffl, each plot alone, the clusters whole or the pieces of the split and the row pairs
        public static Dictionary<string, List<string>> KeyPieces(JObject keyRules, IEnumerable<string> plots, IReadOnlyDictionary<string, ClusterRole> roles,
            IReadOnlyDictionary<string, string> partners, IReadOnlyDictionary<string, string> units)
        {
            string rule = (string)keyRules["ffl"]["rule"];
            if (rule == "ownEntryPlusBase")
            {
                return plots.ToDictionary(plot => plot, plot => new List<string> { plot });
            }
            if (rule == "oneFflPerClusterAtTheHighestEntry")
            {
                return LevelPieces(plots, roles, new Dictionary<string, string>(), units, new JObject());
            }
            JObject split = keyRules["levelSplit"] as JObject;
            JObject table = split != null && split["rule"]?.Type == JTokenType.String && (string)split["rule"] == "units" ? split["table"] as JObject ?? new JObject() : new JObject();
            return LevelPieces(plots, roles, partners, units, table);
        }

        // float() of Python on a number or its text
        private static double PyFloat(JToken value)
        {
            return value.Type == JTokenType.String ? double.Parse(((string)value).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture) : BuildFace.Number(value);
        }

        // plan_rules.base_by_rule: the base keyRules.baseHeight learned for the typology, else steps.base_height of the code or its default, in metres
        public static double BaseByRule(JObject rules, string code, JObject baseRule)
        {
            JObject table = baseRule?["rule"]?.Type == JTokenType.String && (string)baseRule["rule"] == "typology" ? baseRule["table"] as JObject : null;
            if (table != null && table.Count > 0 && code != null && table[code] != null)
            {
                return PyFloat(table[code]);
            }
            JObject bases = (JObject)rules["steps"]["base_height"];
            return BuildFace.Number(code != null && bases[code] != null ? bases[code] : bases["default"]) / 1000.0;
        }

        // plan_rules.piece_top: the highest El @ Entry plus the base of that unit over the members of a piece, unrounded, null for none
        public static double? PieceTop(IEnumerable<Tuple<double?, string>> members, JObject rules, JObject baseRule)
        {
            List<double> known = members.Where(member => member.Item1.HasValue && member.Item2 != null).Select(member => member.Item1.Value + BaseByRule(rules, member.Item2, baseRule)).ToList();
            return known.Count == 0 ? (double?)null : known.Max();
        }

        // plan_rules.steps_from_height: one step for the base, then one per riser over it, a part of a riser over half counting as one
        public static int StepsFromHeight(double height, double baseHeight, double riser)
        {
            return 1 + Math.Max(0, (int)Math.Ceiling(Rounding.Fixed((Rounding.Fixed(height, 6) - baseHeight) / riser, 6) - 0.5));
        }

        // plan_rules.steps_by_rule: stepsFromHeight on the base of the unit and the risers, or stepBands of the level profile
        public static JToken StepsByRule(string rule, double? height, string code, JObject rules, JObject levelProfile, JObject baseRule)
        {
            if (!height.HasValue)
            {
                return null;
            }
            if (rule == "stepsFromHeight")
            {
                return new JValue(StepsFromHeight(height.Value, BaseByRule(rules, code, baseRule), BuildFace.Number(rules["steps"]["riser_added"]) / 1000.0));
            }
            if (rule == "stepBands")
            {
                double banded = Rounding.Fixed(height.Value, 6);
                foreach (JToken band in levelProfile["stepBands"] as JArray ?? new JArray())
                {
                    if (banded < BuildFace.Number(band[0]))
                    {
                        return band[1].DeepClone();
                    }
                }
                return null;
            }
            throw new InvalidDataException("keyRules.steps codes " + rule + ", a build gives stepsFromHeight or stepBands (R339)");
        }

        // Floor division of Python
        private static long FloorDivide(long a, long b)
        {
            long whole = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? whole - 1 : whole;
        }

        // plan_rules.prototype_ffl: the text of RRE_ZZ_SP_Prototype-FFL, the FFL down to the cm, or the FFL, with 3 decimals as "%.3f" writes them
        public static string PrototypeFfl(string rule, double? ffl)
        {
            if (!ffl.HasValue)
            {
                return null;
            }
            long millimetres = (long)Rounding.Fixed(ffl.Value * 1000, 0);
            if (rule == "fflDownToTheCm")
            {
                // R405, Q103. (mm // g) * g / 1000 is the rational (mm // 10) / 100 for g 10, and a division of doubles rounds once, so the
                // text is the one of the old literal
                long grain = DesignValues.Current.CmGrainMm;
                return PyMath.FixedText(FloorDivide(millimetres, grain) * grain / 1000.0, 3);
            }
            if (rule == "ffl")
            {
                return PyMath.FixedText(millimetres / 1000.0, 3);
            }
            throw new InvalidDataException("keyRules parameter RRE_ZZ_SP_Prototype-FFL codes " + rule + ", a build gives fflDownToTheCm or ffl (R339)");
        }

        // plan_rules.level_parameters: each parameter of keyRules of a level rule and not left to a later phase, the text of its FFL
        public static JObject LevelParameters(JObject keyRules, double ffl, ICollection<string> later)
        {
            var found = new JObject();
            foreach (JProperty item in keyRules.Properties())
            {
                JToken rule = (item.Value as JObject)?["rule"];
                if (item.Name.StartsWith(ParameterWord, StringComparison.Ordinal) && !later.Contains(item.Name) && rule != null && rule.Type == JTokenType.String && LevelRules.Contains((string)rule))
                {
                    found[item.Name] = PrototypeFfl((string)rule, ffl);
                }
            }
            return found;
        }

        // The parameters of majority.overrideKeep.laterFields of the job of the level profile, left to a later phase, as rule_inputs reads them
        public static HashSet<string> LaterParameters(JObject config, JObject levelProfile)
        {
            JToken job = levelProfile["job"];
            JArray fields = job != null && job.Type == JTokenType.String ? config["majority"]?["overrideKeep"]?["laterFields"]?[(string)job] as JArray : null;
            return new HashSet<string>((fields ?? new JArray()).Where(field => field.Type == JTokenType.String && ((string)field).StartsWith(ParameterWord, StringComparison.Ordinal)).Select(field => (string)field));
        }

        // plan_from_book.build: the plots of the clusters of the scope, the scope and the row partner of each plot of the scope, by their numbers,
        // their fronts with the road levels, El @ Entry of each with a road level at both front corners and the levels by key_level_rows
        public static LevelBuild Of(IReadOnlyList<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, IEnumerable<SiteCluster> clusters, HandContext context,
            Func<IEnumerable<string>, Dictionary<string, FrontItem>> frontsOf, JObject rules, JObject keyRules, JObject levelProfile, string profileName, DesignBasis basis = null, JArray bands = null)
        {
            bool rev6 = basis != null && basis.IsRev6;
            if (keyRules["ffl"] == null)
            {
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, NoFfl, profileName));
            }
            Dictionary<string, string> partners = RowPartners(context.RowPairs.ToDictionary(item => item.Key, item => item.Value.Side), context.Neighbours);
            var wanted = new HashSet<string>(numbers);
            var members = new HashSet<string>(clusters.Where(cluster => cluster.Plots.Any(wanted.Contains)).SelectMany(cluster => cluster.Plots));
            members.UnionWith(numbers);
            members.UnionWith(numbers.Where(partners.ContainsKey).Select(number => partners[number]));
            List<string> ordered = members.OrderBy(number => number, PyMath.SortKey).ToList();
            Dictionary<string, FrontItem> fronts = frontsOf(ordered);
            var entries = new Dictionary<string, EntryLevel>();
            var noDoorWhy = new Dictionary<string, string>();
            var noDoor = new JArray();
            foreach (string number in ordered)
            {
                FrontItem item = fronts[number];
                if (item.Front != null)
                {
                    double raw, slope;
                    if (rev6)
                    {
                        // R408. El @ Entry the input, from the civil file or the road at front plus the slope of the unit, the slope the result
                        Tuple<double?, string> door = basis.DoorEntry(number, item.Unit, item.Front, item.Depth.Value);
                        if (!door.Item1.HasValue)
                        {
                            noDoorWhy[number] = door.Item2;
                            if (wanted.Contains(number))
                            {
                                noDoor.Add(new JObject { ["plot"] = number, ["reason"] = door.Item2 });
                            }
                            continue;
                        }
                        raw = door.Item1.Value;
                        slope = DesignBasis.SlopeOf(raw, item.Front, item.Depth.Value);
                    }
                    else
                    {
                        raw = EntryOf(item.Front, item.Unit, levelProfile, item.Depth.Value);
                        slope = SlopeOf(item.Unit, levelProfile);
                    }
                    entries[number] = new EntryLevel { Unit = item.Unit, Raw = raw, Rule = Rounding.Fixed(raw, 4), Slope = slope };
                }
            }
            // The code of plots.csv as it stands, an empty one kept empty, as key_levels takes it for the split of a cluster
            var units = ordered.ToDictionary(number => number, number => rows.TryGetValue(number, out Dictionary<string, string> row) && row.TryGetValue("typologyRev9", out string code) ? code : (string)null);
            Dictionary<string, List<string>> pieces = KeyPieces(keyRules, entries.Keys.ToList(), context.Roles, partners, units);
            JObject baseRule = keyRules["baseHeight"] as JObject;
            var levels = new Dictionary<string, LevelRow>();
            // plan_rules.key_level_rows: one FFL per piece at its highest El @ Entry by the rule plus the base of that unit, to the mm
            foreach (KeyValuePair<string, EntryLevel> entry in entries)
            {
                List<string> piece = pieces.TryGetValue(entry.Key, out List<string> found) && found.Count > 0 ? found : new List<string> { entry.Key };
                double? top = PieceTop(piece.Select(other => entries.TryGetValue(other, out EntryLevel held) ? Tuple.Create((double?)held.Rule, held.Unit) : Tuple.Create((double?)null, (string)null)), rules, baseRule);
                if (top.HasValue)
                {
                    levels[entry.Key] = new LevelRow { Piece = piece, Top = top.Value, Ffl = Rounding.Fixed(top.Value, 3) };
                }
            }
            var made = new LevelBuild { Partners = partners, Members = ordered, Fronts = fronts, Entries = entries, Rows = levels, Basis = basis, Bands = bands };
            if (rev6)
            {
                // R408. The plots of the scope with no El @ Entry and why, and those whose slope lies outside the manual, as plan_from_book.build lists them
                JObject limits = (JObject)rules["slopes_percent"];
                made.NoDoorWhy = noDoorWhy;
                made.NoDoor = noDoor;
                made.Outside = new JArray(numbers.Where(number => entries.ContainsKey(number) && DesignBasis.OutsideManual(entries[number].Slope, limits))
                    .Select(number => new JObject { ["plot"] = number, ["slopePercent"] = Rounding.Fixed(entries[number].Slope, DesignBasis.SlopeDecimals) }));
            }
            return made;
        }

        // plan_from_book.level_step: per plot of the scope its levels by the rule before an override, each raw the value just before its last
        // rounding, the road at front and El @ Entry from one unrounded mean, SSL and height from the FFL as written
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, LevelBuild build,
            JObject keyRules, JObject levelProfile, JObject rules, ICollection<string> later, string rulesName = "roshn-makkah.json")
        {
            double ground = Fronts.Metres(rules, rulesName, rules["levels"]?["ground_ssl"], "levels.ground_ssl");
            JObject baseRule = keyRules["baseHeight"] as JObject;
            string stepsRule = (string)keyRules["steps"]["rule"];
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                string unit = rows.TryGetValue(number, out Dictionary<string, string> row) && row.TryGetValue("typologyRev9", out string code) && !string.IsNullOrEmpty(code) ? code : null;
                build.Fronts.TryGetValue(number, out FrontItem item);
                build.Entries.TryGetValue(number, out EntryLevel entry);
                build.Rows.TryGetValue(number, out LevelRow level);
                var made = new Dictionary<string, object>
                {
                    ["unit"] = unit, ["partner"] = build.Partners.TryGetValue(number, out string partner) ? partner : null, ["piece"] = null,
                    ["roadAtFront"] = item?.Front == null ? null : (object)RoadAtFront(item.Front), ["entry"] = entry == null ? null : (object)entry.Raw,
                    ["ffl"] = null, ["ssl"] = null, ["height"] = null, ["steps"] = null, ["parameters"] = null, ["doorSource"] = null, ["slopePercent"] = null,
                };
                if (build.Rev6 && unit != null)
                {
                    // R408. The source of El @ Entry under rev6, the input, and the slope it leaves, the result
                    made["doorSource"] = build.Basis.DoorSource;
                }
                if (build.Rev6 && entry != null)
                {
                    made["slopePercent"] = entry.Slope;
                }
                if (level != null)
                {
                    double height = level.Ffl - entry.Rule;
                    made["piece"] = level.Piece.ToList();
                    made["ffl"] = level.Top;
                    made["ssl"] = level.Ffl + ground;
                    made["height"] = height;
                    made["steps"] = StepsByRule(stepsRule, Rounding.Fixed(height, 4), entry.Unit, rules, levelProfile, baseRule);
                    made["parameters"] = LevelParameters(keyRules, level.Ffl, later);
                }
                found[number] = made;
            }
            return found;
        }
    }
}
