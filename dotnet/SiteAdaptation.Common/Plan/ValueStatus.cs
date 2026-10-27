using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R401, the plan part, as value_status.Table of Python gives it. The map of valueStatus of the settings and the block status of the rules
    // profile, read once per build for the level profile the build follows: the status of a value of a field from a source, its detail and the
    // key the plan writes the detail by. The map names one block entry or a list of them, by the code that computes the value, and a value takes
    // the status of the first assumed one, else of the first, listing each. An override of the list marks every field of its overrideSets. A
    // pair the map does not hold, or an entry the block lacks, is UNKNOWN with why, never a guess
    public sealed class ValueStatusTable
    {
        private const string Override = "override";
        private const string Parameters = "parameters.";
        private const string AnyParameter = "parameters.*";
        private const string Measured = "MEASURED";
        private const string SetsLine = "valueStatus.overrideSets maps each override of the list to the list of the fields it moves, the settings hold {0} (R401)";
        private static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        private readonly JObject sources, fields, block;
        private readonly Dictionary<string, List<string>> sets = new Dictionary<string, List<string>>();
        private readonly HashSet<string> assumed;
        private readonly Dictionary<string, string> files;
        private readonly string levelProfile;

        public ValueStatusTable(JObject map, JObject block, string profileFile, string levelProfile)
        {
            sources = map?["sources"] as JObject ?? new JObject();
            fields = map?["fields"] as JObject ?? new JObject();
            assumed = new HashSet<string>(((map?["assumed"] as JArray) ?? new JArray()).Select(item => (string)item));
            if (map != null && map.HasValues)
            {
                // value_status.override_sets: each override to a list of field names, else refused with one line
                if (!(map["overrideSets"] is JObject given) || !given.HasValues)
                {
                    throw new InvalidDataException(string.Format(SetsLine, "none"));
                }
                foreach (JProperty item in given.Properties())
                {
                    if (!(item.Value is JArray names) || names.Count == 0 || names.Any(name => name.Type != JTokenType.String))
                    {
                        throw new InvalidDataException(string.Format(SetsLine, item.Name + ": " + PlanOverrides.Dumps(item.Value)));
                    }
                    sets[item.Name] = names.Select(name => (string)name).ToList();
                }
            }
            this.block = block ?? new JObject();
            files = new Dictionary<string, string> { ["profile"] = profileFile, ["config"] = ValueStatus.SettingsFile };
            this.levelProfile = levelProfile;
        }

        private static List<JObject> AsList(JToken found)
        {
            return found is JArray list ? list.OfType<JObject>().ToList() : found is JObject one ? new List<JObject> { one } : null;
        }

        private List<JObject> Refs(string field, string source)
        {
            foreach (string name in field.StartsWith(Parameters, StringComparison.Ordinal) ? new[] { field, AnyParameter } : new[] { field })
            {
                List<JObject> found = AsList((fields[name] as JObject)?[source]);
                if (found != null)
                {
                    return found;
                }
            }
            return AsList(sources[source]);
        }

        private static string EntryLabel(JObject entry)
        {
            return ValueStatus.Label((string)entry["status"], entry);
        }

        // value_status.Table.of: the status, the detail and its key
        public Tuple<string, JObject, string> Of(string field, string source, string hand = null, IEnumerable<string> applied = null)
        {
            string moved = source == Override ? null : (applied ?? Enumerable.Empty<string>()).FirstOrDefault(name => sets.TryGetValue(name, out List<string> moves) && moves.Contains(field));
            if (moved != null)
            {
                source = Override;
            }
            List<JObject> refs = Refs(field, source);
            List<JObject> entries = refs == null
                ? new List<JObject> { new JObject { ["status"] = "UNKNOWN", ["why"] = "valueStatus holds no status for " + field + " from " + source + " (R401)" } }
                : refs.Select(reference => Resolve(field, source, reference, hand)).ToList();
            JObject chosen = entries.FirstOrDefault(entry => assumed.Contains(EntryLabel(entry))) ?? entries[0];
            var detail = new JObject { ["entries"] = new JArray(entries) };
            if ((string)chosen["status"] == Measured && chosen["strength"]?.Type == JTokenType.String && ((string)chosen["strength"]).Length > 0)
            {
                detail["strength"] = chosen["strength"].DeepClone();
            }
            if (moved != null)
            {
                detail["override"] = moved;
            }
            string key = field + " from " + source;
            if ((refs ?? new List<JObject>()).Any(reference => new[] { "profile", "config" }.Any(kind => reference[kind]?.Type == JTokenType.String && ((string)reference[kind]).Contains("{hand}"))))
            {
                key += " hand " + (hand ?? "None");
            }
            if (moved != null)
            {
                key += " by " + moved;
            }
            return Tuple.Create((string)chosen["status"], detail, key);
        }

        private JObject Resolve(string field, string source, JObject reference, string hand)
        {
            if (reference.Property("status") != null)
            {
                return (JObject)reference.DeepClone();
            }
            string kind = reference.Property("profile") != null ? "profile" : reference.Property("config") != null ? "config" : null;
            if (kind == null)
            {
                return new JObject { ["status"] = "UNKNOWN", ["why"] = "valueStatus names no block entry and no status for " + field + " from " + source + " (R401)" };
            }
            string template = (string)reference[kind];
            var names = new Dictionary<string, string>
            {
                ["levelProfile"] = levelProfile, ["parameter"] = field.StartsWith(Parameters, StringComparison.Ordinal) ? field.Substring(Parameters.Length) : null, ["hand"] = hand,
            };
            string lacking = Placeholder.Matches(template).Cast<Match>().Select(match => match.Groups[1].Value)
                .FirstOrDefault(word => !names.TryGetValue(word, out string value) || value == null);
            if (lacking != null)
            {
                return new JObject { ["entry"] = template, ["status"] = "UNKNOWN", ["why"] = "the plot holds no " + lacking + " (R401)" };
            }
            string path = Placeholder.Replace(template, match => names[match.Groups[1].Value]);
            JObject entry = (block[files[kind]] as JObject)?[path] as JObject;
            if (entry == null || entry.Property("status") == null)
            {
                return new JObject { ["entry"] = files[kind] + " " + path, ["status"] = "UNKNOWN", ["why"] = "the block status holds no entry (R401)" };
            }
            var found = new JObject { ["entry"] = files[kind] + " " + path, ["status"] = entry["status"].DeepClone() };
            foreach (JProperty item in entry.Properties().Where(item => item.Name != "status"))
            {
                found[item.Name] = item.Value.DeepClone();
            }
            return found;
        }
    }

    // R401. The step status of the parity harness: per plot of the scope each sourced value of the plot the plan writes by its field, with its
    // source, status and detail, as plan_from_book.status_step gives it from the plan. The worker writes no plan, so the fields of a written
    // plot are those plan_from_book.build writes, by the outcomes of the overrides step (PlanOverrides) and the hand table of the hands step
    public static class ValueStatus
    {
        public const string StepName = "status";
        // rule_status.SETTINGS_FILE, the key of the block entries of the settings
        public const string SettingsFile = "config/project.json";

        private const string Computed = "computed";
        private const string Estimate = "estimate";
        private const string LocationPlan = "locationPlan";
        private const string OverrideSource = "override";
        private static readonly string[] FloorFields = { "type", "level", "levelElevation", "outline", "plotValue" };

        public static ValueStatusTable Table(JObject config, JObject rules, string levelProfile)
        {
            return new ValueStatusTable(config["valueStatus"] as JObject, rules["status"] as JObject, config["profile"]?.Type == JTokenType.String ? (string)config["profile"] : null,
                levelProfile);
        }

        // value_status.label: the status a count names, MEASURED with its strength
        public static string Label(string status, JObject detail)
        {
            string strength = detail?["strength"]?.Type == JTokenType.String ? (string)detail["strength"] : null;
            return status + (status == "MEASURED" && !string.IsNullOrEmpty(strength) ? " " + strength : "");
        }

        // The field and source of each sourced value plan_from_book.build writes for a plot it writes
        private static List<Tuple<string, string>> Sources(PlotPlan plan)
        {
            var applied = new HashSet<string>(plan.Overrides["applied"] as List<string> ?? new List<string>());
            var found = new List<Tuple<string, string>> { Tuple.Create("unit", LocationPlan), Tuple.Create("prototypeFile", Computed) };
            foreach (string field in new[] { "insert", "rotationDeg", "mirrored" })
            {
                found.Add(Tuple.Create(field, applied.Contains(field) ? OverrideSource : Computed));
            }
            found.AddRange(new[] { "ffl", "ssl", "entry", "height", "steps" }.Select(name => Tuple.Create("levels." + name, Estimate)));
            JObject sources = plan.Parameters["sources"] as JObject ?? new JObject();
            found.AddRange(sources.Properties().Select(item => Tuple.Create("parameters." + item.Name, (string)item.Value)));
            if (sources[LinkFields.RvtName] != null)
            {
                found.Add(Tuple.Create("linkName", (string)sources[LinkFields.RvtName]));
            }
            var kinds = new List<Tuple<string, string[]>> { Tuple.Create("generic", new[] { "z", "heightOffset" }) };
            if (plan.Overrides["yardOutline"] != null)
            {
                kinds.Add(Tuple.Create("yard", new[] { "z", "heightOffset" }));
            }
            if (plan.Overrides["rampType"] != null)
            {
                kinds.Add(Tuple.Create("ramp", new[] { "corners", "heightOffset", "slopePercent" }));
            }
            foreach (Tuple<string, string[]> kind in kinds)
            {
                found.AddRange(FloorFields.Select(name => Tuple.Create("floors." + kind.Item1 + "." + name, Computed)));
                found.AddRange(kind.Item2.Select(name => Tuple.Create("floors." + kind.Item1 + "." + name, Estimate)));
            }
            return found;
        }

        // plan_from_book.status_step: no field for a plot the plan does not write
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans,
            IReadOnlyDictionary<string, Dictionary<string, object>> hands, ValueStatusTable table)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                PlotPlan plan = plans[number];
                bool written = (bool)plan.Overrides["written"];
                JObject fields = null;
                if (written)
                {
                    hands.TryGetValue(number, out Dictionary<string, object> hand);
                    object handTable = null;
                    hand?.TryGetValue("handTable", out handTable);
                    var applied = plan.Overrides["applied"] as List<string> ?? new List<string>();
                    fields = new JObject();
                    foreach (Tuple<string, string> item in Sources(plan))
                    {
                        Tuple<string, JObject, string> status = table.Of(item.Item1, item.Item2, handTable as string, applied);
                        fields[item.Item1] = new JObject { ["source"] = item.Item2, ["status"] = status.Item1, ["statusFrom"] = status.Item2 };
                    }
                }
                found[number] = new Dictionary<string, object> { ["unit"] = plan.Overrides["unit"], ["written"] = written, ["fields"] = fields };
            }
            return found;
        }

        // value_status.summary statusByField: the plots by field and status, in the order of Python, field then status
        public static List<KeyValuePair<Tuple<string, string>, int>> Counts(IReadOnlyDictionary<string, Dictionary<string, object>> step)
        {
            var counts = new Dictionary<Tuple<string, string>, int>();
            foreach (Dictionary<string, object> plot in step.Values)
            {
                if (!(plot["fields"] is JObject fields))
                {
                    continue;
                }
                foreach (JProperty field in fields.Properties())
                {
                    var key = Tuple.Create(field.Name, Label((string)field.Value["status"], field.Value["statusFrom"] as JObject));
                    counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
                }
            }
            return counts.OrderBy(item => item.Key.Item1, StringComparer.Ordinal).ThenBy(item => item.Key.Item2, StringComparer.Ordinal).ToList();
        }
    }
}
