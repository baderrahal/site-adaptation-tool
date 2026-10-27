using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354, R337, R277. The level profile and keyRules a build by keyRules takes from the rules file, refused as plan_rules.py refuses them:
    // the profile named or levelProfiles.default (profile_name), one the rules file holds with frontRoad, rampSlopePercent, rampTypes and
    // height (level_profile), and keyRules held by check_key_rules and check_ramp_slopes, every rule a build takes a candidate it can give,
    // with the same lines. The C# builds by keyRules alone, so a profile with none is refused for every build step
    public static class BuildRules
    {
        private static readonly string[] NotProfiles = { "note", "default" };

        public static readonly string[] HandFactors = { "all", "blockMirror", "clusterSide", "openEnd", "roadSide", "blockMirrorOpenEnd", "blockMirrorRoadSide", "rowPairSide", "rowPairSideOrBlockMirrorOpenEnd" };

        private static readonly string[] TableFactors = { "all", "typology", "mirroredFactor", "typologyMirrored", "steps", "typologySteps" };
        private static readonly string[] Formulas = { "plot", "rev9Code", "plotAndCode", "plotCodeStepsAndFacade" };
        private static readonly string[] LevelRules = { "ffl", "fflDownToTheCm" };
        public const string Parameter = "parameter ";

        private static readonly KeyValuePair<string, string[]>[] LevelWords =
        {
            new KeyValuePair<string, string[]>("entry", new[] { "roadLevel" }), new KeyValuePair<string, string[]>("rampFront", new[] { "roadLevel" }),
            new KeyValuePair<string, string[]>("levelSplit", new[] { "none", "units" }), new KeyValuePair<string, string[]>("baseHeight", new[] { "profile", "typology" }),
            new KeyValuePair<string, string[]>("ffl", new[] { "oneFflPerPieceAtTheHighestEntry", "oneFflPerClusterAtTheHighestEntry", "ownEntryPlusBase" }),
            new KeyValuePair<string, string[]>("steps", new[] { "stepsFromHeight", "stepBands" }),
        };

        private static readonly string[] LevelNeeded = { "entry", "steps", "baseHeight" };

        // plan_rules.profile_name
        public static string ProfileName(JObject rules, string name)
        {
            JObject profiles = rules["levelProfiles"] as JObject ?? new JObject();
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            JToken chosen = profiles["default"];
            string text = chosen == null || chosen.Type != JTokenType.String ? null : (string)chosen;
            if (string.IsNullOrEmpty(text) || NotProfiles.Contains(text) || !(profiles[text] is JObject))
            {
                List<string> names = profiles.Properties().Select(item => item.Name).Where(key => !NotProfiles.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToList();
                throw new InvalidDataException("levelProfiles.default of the rules profile names no level profile, name one of " + string.Join(", ", names));
            }
            return text;
        }

        // plan_rules.level_profile
        public static JObject LevelProfile(JObject rules, string name)
        {
            JObject profiles = rules["levelProfiles"] as JObject ?? new JObject();
            name = ProfileName(rules, name);
            if (profiles[name] == null || NotProfiles.Contains(name))
            {
                throw new InvalidDataException("the rules profile has no level profile " + name);
            }
            JObject found = profiles[name] as JObject ?? throw new InvalidDataException("the rules profile has no level profile " + name);
            foreach (string key in new[] { "frontRoad", "rampSlopePercent", "rampTypes", "height" })
            {
                if (found[key] == null)
                {
                    throw new InvalidDataException("level profile " + name + " has no " + key);
                }
            }
            return found;
        }

        // keyRules of the level profile, held by plan_rules.key_rules_of, refused when the profile holds none, since the C# builds by keyRules
        // alone (plan_from_book.py refuses the build steps of such a profile with the same line)
        public static JObject KeyRules(JObject rules, string name, string steps)
        {
            JObject profile = LevelProfile(rules, name);
            if (!(profile["keyRules"] is JObject key))
            {
                throw new InvalidDataException("the build steps " + steps + " of the parity harness are those of a build by keyRules, the level profile " + ProfileName(rules, name)
                    + " holds none, the C# builds by keyRules alone (R354)");
            }
            CheckKeyRules(key);
            CheckRampSlopes(key, profile);
            return key;
        }

        private static JObject Need(JObject rules, string word, string[] allowed, params string[] fields)
        {
            JObject entry = rules[word] as JObject;
            JToken rule = entry?["rule"];
            if (entry == null || rule == null || rule.Type != JTokenType.String || !allowed.Contains((string)rule) || fields.Any(field => entry[field] == null))
            {
                string coded = entry == null ? "None" : SiteClusters.PyText(rule);
                throw new InvalidDataException("keyRules " + word + " codes " + coded + ", a build gives only " + string.Join(", ", allowed)
                    + (fields.Length == 0 ? "" : " with " + string.Join(" and ", fields)) + " (R337)");
            }
            // P3 of the review of PR 55: each table in the shape a build reads it, a list where an object is due refused, as Python refuses it
            foreach (string field in fields)
            {
                JTokenType wanted = field == "groups" ? JTokenType.Array : JTokenType.Object;
                if (entry[field].Type != wanted)
                {
                    throw new InvalidDataException("keyRules " + word + " holds " + field + " as " + ShapeName(entry[field].Type) + ", a build takes it as " + ShapeName(wanted) + " (R337)");
                }
                if (field == "byFile")
                {
                    foreach (JProperty table in ((JObject)entry[field]).Properties())
                    {
                        if (table.Value.Type != JTokenType.Object)
                        {
                            throw new InvalidDataException("keyRules " + word + " holds byFile " + table.Name + " as " + ShapeName(table.Value.Type) + ", a build takes it as an object of factor values (R337)");
                        }
                        // F2 of the third review of PR 55: a hand is true or false, never a text a build would take as true
                        foreach (JProperty hand in ((JObject)table.Value).Properties().Where(hand => hand.Value.Type != JTokenType.Boolean))
                        {
                            throw new InvalidDataException("keyRules " + word + " holds byFile " + table.Name + " " + hand.Name + " as " + ShapeName(hand.Value.Type) + ", a build takes true or false (R337)");
                        }
                    }
                }
            }
            return entry;
        }

        // R356, F1 of the third review of PR 55. plan_rules.check_link_fits: every fit of keyRules.linkOrigin.byCode, of a code of the scope or
        // not, an object with an anchor of start or end and an offset of two numbers, else refused with the line of Python, where a fit with no
        // anchor put the link at the end corner, a list or null left the plot not placed in silence and an offset of a text was a number
        public static void CheckLinkFits(JObject rules)
        {
            foreach (JProperty item in ((JObject)rules["linkOrigin"]["byCode"]).Properties())
            {
                if (!(item.Value is JObject fit))
                {
                    throw new InvalidDataException("keyRules linkOrigin holds byCode " + item.Name + " as " + ShapeName(item.Value.Type) + ", a build takes it as an object of anchor and offset (R337)");
                }
                JToken anchor = fit["anchor"];
                if (anchor == null || anchor.Type != JTokenType.String || ((string)anchor != "start" && (string)anchor != "end"))
                {
                    throw new InvalidDataException("keyRules linkOrigin byCode " + item.Name + " holds anchor " + SiteClusters.PyText(anchor) + ", a build takes start or end (R337)");
                }
                JToken offset = fit["offset"];
                if (!(offset is JArray pair) || pair.Count != 2 || pair.Any(value => value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
                {
                    throw new InvalidDataException("keyRules linkOrigin byCode " + item.Name + " holds offset " + SiteClusters.PyText(offset) + ", a build takes a list of two numbers (R337)");
                }
            }
        }

        // R355, F3 of the third review of PR 55. plan_from_book.number_settings: each setting of config a build reads as a number a number,
        // else refused with the line of Python, where (double)token took a text as a number and Python stopped on TypeError
        public static readonly string[] Numbers = { "siteBuild.corner.roadEdgeM", "analysis.toleranceM", "analysis.partyWallShare" };

        public static void NumberSettings(JObject settings, string configName)
        {
            foreach (string dotted in Numbers)
            {
                JToken value = settings;
                foreach (string part in dotted.Split('.'))
                {
                    value = value is JObject node && node[part] != null ? node[part] : throw new InvalidDataException(configName + " has no value " + dotted);
                }
                if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
                {
                    throw new InvalidDataException("config " + dotted + " holds " + ShapeName(value.Type) + ", a build takes it as a number (R355)");
                }
            }
        }

        // R337. plan_rules.shape_name: the kind of a JSON value as a message of CheckKeyRules names it
        public static string ShapeName(JTokenType type)
        {
            switch (type)
            {
                case JTokenType.Object:
                    return "an object";
                case JTokenType.Array:
                    return "a list";
                case JTokenType.String:
                    return "a text";
                case JTokenType.Boolean:
                    return "true or false";
                case JTokenType.Integer:
                case JTokenType.Float:
                    return "a number";
                default:
                    return "null";
            }
        }

        // plan_rules.check_key_rules, every rule in the order Python holds them
        public static void CheckKeyRules(JObject rules)
        {
            Need(rules, "linkRotation", new[] { "frontRule" });
            Need(rules, "linkOrigin", new[] { "plotFrameRefit" }, "byCode");
            CheckLinkFits(rules);
            foreach (string unitClass in new[] { "villa", "cluster", "alone" })
            {
                Need(rules, "hand " + unitClass, HandFactors, "byFile");
            }
            Need(rules, "rampSlope", new[] { "typology" }, "table");
            Need(rules, "rampOutline", new[] { "outlineR127" });
            JToken generic = Need(rules, "generic", new[] { "all" }, "table")["table"];
            if (!JToken.DeepEquals(generic, new JObject { ["all"] = "wholeFace" }))
            {
                throw new InvalidDataException("keyRules generic gives no whole face, a build gives only the whole face (R337)");
            }
            Need(rules, "yard", new[] { "zone" }, "table");
            if ((string)Need(rules, "yardOutline", new[] { "fittedOutlineRefit", "fittedOutline" })["rule"] == "fittedOutlineRefit")
            {
                Need(rules, "yardOutline", new[] { "fittedOutlineRefit" }, "groups");
            }
            foreach (JProperty item in rules.Properties().Where(item => item.Name.StartsWith(Parameter, StringComparison.Ordinal)))
            {
                string coded = (item.Value as JObject)?["rule"]?.Type == JTokenType.String ? (string)item.Value["rule"] : null;
                Need(rules, item.Name, Formulas.Concat(LevelRules).Concat(TableFactors).ToArray(), TableFactors.Contains(coded) ? new[] { "table" } : new string[0]);
            }
            foreach (KeyValuePair<string, string[]> word in LevelWords)
            {
                if (rules[word.Key] != null)
                {
                    string coded = (rules[word.Key] as JObject)?["rule"]?.Type == JTokenType.String ? (string)rules[word.Key]["rule"] : null;
                    Need(rules, word.Key, word.Value, coded == "units" || coded == "typology" ? new[] { "table" } : new string[0]);
                }
            }
            if (rules["ffl"] != null)
            {
                string coded = (string)rules["ffl"]["rule"];
                IEnumerable<string> needed = LevelNeeded.Concat(coded == "oneFflPerPieceAtTheHighestEntry" ? new[] { "levelSplit" } : new string[0]);
                foreach (string word in needed)
                {
                    if (rules[word] == null)
                    {
                        throw new InvalidDataException("keyRules codes ffl " + coded + " and no " + word + ", a build of the levels by keyRules needs " + string.Join(", ", needed) + " (R340)");
                    }
                }
            }
        }

        // plan_rules.slope_class: flat for 0, else the slope as Python "%g" writes it
        public static string SlopeClass(double slope)
        {
            return slope == 0 ? "flat" : slope.ToString("G6", CultureInfo.InvariantCulture).Replace("E", "e");
        }

        // plan_rules.check_ramp_slopes: the slope class of El @ Entry of each code of keyRules.rampSlope equal to the class it codes
        public static void CheckRampSlopes(JObject rules, JObject profile)
        {
            // An object since CheckKeyRules held it so, never a list read as empty (P3 of the review of PR 55)
            JObject table = (JObject)rules["rampSlope"]["table"];
            JObject slopes = profile["rampSlopePercent"] as JObject ?? new JObject();
            // F3 of the third review of PR 55: a slope of a text or true or false refused by its line, never taken as a number
            foreach (JProperty code in table.Properties())
            {
                string named = slopes[code.Name] != null ? code.Name : "default";
                JToken slope = slopes[named];
                if (slope == null || (slope.Type != JTokenType.Integer && slope.Type != JTokenType.Float))
                {
                    throw new InvalidDataException("rampSlopePercent " + named + " of the level profile holds " + ShapeName(slope?.Type ?? JTokenType.Null) + ", a build takes it as a number (R337)");
                }
            }
            var wrong = new List<string>();
            foreach (JProperty code in table.Properties())
            {
                JToken slope = slopes[code.Name] ?? slopes["default"];
                string found = SlopeClass(BuildFace.Number(slope));
                if (code.Value.Type != JTokenType.String || found != (string)code.Value)
                {
                    wrong.Add(code.Name + " (rampSlopePercent " + SiteClusters.PyText(slope) + ", keyRules.rampSlope " + SiteClusters.PyText(code.Value) + ")");
                }
            }
            if (wrong.Count > 0)
            {
                throw new InvalidDataException("the slope of El @ Entry and the ramp class of keyRules differ for " + string.Join(", ", wrong.OrderBy(item => item, StringComparer.Ordinal)) + ", give both one slope (R337)");
            }
        }
    }
}
