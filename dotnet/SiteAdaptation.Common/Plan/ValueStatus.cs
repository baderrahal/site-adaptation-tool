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
    // the worst status of the list by worstFirst, else that of the first, listing each (Q107). An override of the list marks every field of its
    // overrideSets, named by the override applied last whose set holds the field (Q107). A pair the map does not hold, or an entry the block
    // lacks, is UNKNOWN with why, never a guess
    public sealed class ValueStatusTable
    {
        private const string Override = "override";
        private const string Parameters = "parameters.";
        private const string AnyParameter = "parameters.*";
        private const string Measured = "MEASURED";
        private const string SetsLine = "valueStatus.overrideSets maps each override of the list to the list of the fields it moves, the settings hold {0} (R401)";
        private static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        // value_status.LABELS, the labels a status of a value takes, the six of rule_status.py with MEASURED by its strength
        public static readonly string[] Labels = { "UNKNOWN", "CHOSEN by Code", "CHOSEN by Bader", "RECOMMENDED", "MEASURED weak", "MEASURED strong", "MEASURED", "DOCUMENT" };
        private static readonly string OrderLine = "valueStatus.worstFirst lists the statuses a value of several block entries takes first, the worst first, each of "
            + string.Join(", ", Labels.Take(Labels.Length - 1)) + " or " + Labels.Last() + ", the settings hold {0} (R401)";

        private readonly JObject sources, fields, block;
        private readonly Dictionary<string, List<string>> sets = new Dictionary<string, List<string>>();
        private readonly List<string> order = new List<string>();
        // Q107, R2 and R3 of the review of PR 75: per override the fields it moves that the code still computes, with what it reads, and the
        // field whose value tells whether an override moved a field
        private readonly Dictionary<string, Dictionary<string, List<JObject>>> reads = new Dictionary<string, Dictionary<string, List<JObject>>>();
        private readonly Dictionary<string, string> movedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        private const string ReadsLine = "valueStatus.overrideReads maps an override of overrideSets to the fields of its set the code still computes from other entries, "
            + "each to the list of them, the settings hold {0} (R401)";
        private const string MovedLine = "valueStatus.movedBy maps a field to the field whose value tells whether an override moved it, the settings hold {0} (R401)";
        // The status of each key given, so a value of a key given before takes it with no new detail
        private readonly Dictionary<string, string> statusOf = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> files;
        private readonly string levelProfile;

        // value_status.Table.used: the detail of each key the build gave a value, the table statusFrom
        public Dictionary<string, JObject> Used { get; } = new Dictionary<string, JObject>(StringComparer.Ordinal);

        // The decimals of addin.parity a field of the step overrides is written at, by its key
        public Dictionary<string, int> Decimals { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        public ValueStatusTable(JObject map, JObject block, string profileFile, string levelProfile, IDictionary<string, int> decimals = null)
        {
            foreach (KeyValuePair<string, int> item in decimals ?? new Dictionary<string, int>())
            {
                Decimals[item.Key] = item.Value;
            }
            sources = map?["sources"] as JObject ?? new JObject();
            fields = map?["fields"] as JObject ?? new JObject();
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
                // value_status.worst_first: a list of labels of Labels, else refused with one line
                JToken worst = map["worstFirst"];
                if (!(worst is JArray ranked) || ranked.Count == 0 || ranked.Any(name => name.Type != JTokenType.String || !Labels.Contains((string)name)))
                {
                    throw new InvalidDataException(string.Format(OrderLine, worst == null || worst.Type == JTokenType.Null ? "none" : PlanOverrides.Dumps(worst)));
                }
                order = ranked.Select(name => (string)name).ToList();
                // value_status.override_reads: per override of the sets the fields of its set with the list of entries they still read
                if (!(map["overrideReads"] is JObject read))
                {
                    throw new InvalidDataException(string.Format(ReadsLine, "none"));
                }
                foreach (JProperty item in read.Properties())
                {
                    if (!sets.ContainsKey(item.Name) || !(item.Value is JObject byField) || !byField.HasValues || byField.Properties().Any(field =>
                        !sets[item.Name].Contains(field.Name) || !(field.Value is JArray refs) || refs.Count == 0 || refs.Any(reference => reference.Type != JTokenType.Object)))
                    {
                        throw new InvalidDataException(string.Format(ReadsLine, item.Name + ": " + PlanOverrides.Dumps(item.Value)));
                    }
                    reads[item.Name] = byField.Properties().ToDictionary(field => field.Name, field => ((JArray)field.Value).OfType<JObject>().ToList());
                }
                // value_status.moved_by: a field to a field, else refused with one line
                JToken moved = map["movedBy"];
                if (!(moved is JObject byName) || byName.Properties().Any(item => item.Value.Type != JTokenType.String))
                {
                    throw new InvalidDataException(string.Format(MovedLine, moved == null || moved.Type == JTokenType.Null ? "none" : PlanOverrides.Dumps(moved)));
                }
                foreach (JProperty item in byName.Properties())
                {
                    movedBy[item.Name] = (string)item.Value;
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
        // movedBy of the map: the field whose value tells whether an override moved field, field itself when it names none
        public string MovedBy(string field)
        {
            return movedBy.TryGetValue(field, out string name) ? name : field;
        }

        // applied in the order the build applied them, so the last whose set holds the field names the override that moved the value, with the
        // entries the code still reads for it by overrideReads, and unmoved, the value equal to the one with no override, leaves the value its own
        // list (Q107)
        public Tuple<string, JObject, string> Of(string field, string source, string hand = null, IEnumerable<string> applied = null, bool unmoved = false)
        {
            string moved = source == Override || unmoved ? null : (applied ?? Enumerable.Empty<string>()).LastOrDefault(name => sets.TryGetValue(name, out List<string> moves) && moves.Contains(field));
            if (moved != null)
            {
                source = Override;
            }
            List<JObject> refs = Refs(field, source);
            if (moved != null && refs != null && reads.TryGetValue(moved, out Dictionary<string, List<JObject>> byField) && byField.TryGetValue(field, out List<JObject> more))
            {
                refs = refs.Concat(more).ToList();
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
            // The key names the field, source, hand and override the detail is made of, so a key given before keeps its detail, one copy a build
            if (statusOf.TryGetValue(key, out string known))
            {
                return Tuple.Create(known, Used[key], key);
            }
            List<JObject> entries = refs == null
                ? new List<JObject> { new JObject { ["status"] = "UNKNOWN", ["why"] = "valueStatus holds no status for " + field + " from " + source + " (R401)" } }
                : refs.Select(reference => Resolve(field, source, reference, hand)).ToList();
            // Q107. The worst status of the list by worstFirst, else the first entry
            JObject chosen = entries.Where(entry => order.Contains(EntryLabel(entry))).OrderBy(entry => order.IndexOf(EntryLabel(entry))).FirstOrDefault() ?? entries[0];
            var detail = new JObject { ["entries"] = new JArray(entries) };
            if ((string)chosen["status"] == Measured && chosen["strength"]?.Type == JTokenType.String && ((string)chosen["strength"]).Length > 0)
            {
                detail["strength"] = chosen["strength"].DeepClone();
            }
            if (moved != null)
            {
                detail["override"] = moved;
            }
            Used[key] = detail;
            statusOf[key] = (string)chosen["status"];
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
            // value_status.table_of: the decimals of the fields of the step overrides, which a value is held at against the value with no override
            var decimals = new Dictionary<string, int>();
            foreach (JProperty field in ((config["addin"]?["parity"]?["steps"]?["overrides"]) as JObject ?? new JObject()).Properties())
            {
                if (field.Value["decimals"] != null && field.Value["decimals"].Type == JTokenType.Integer)
                {
                    decimals[field.Name] = (int)field.Value["decimals"];
                }
            }
            return new ValueStatusTable(config["valueStatus"] as JObject, rules["status"] as JObject, config["profile"]?.Type == JTokenType.String ? (string)config["profile"] : null,
                levelProfile, decimals);
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
            IReadOnlyDictionary<string, Dictionary<string, object>> hands, ValueStatusTable table, IReadOnlyDictionary<string, PlotPlan> rulePlans = null)
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
                    // Q107. In the order the build applied them, so the status names the override that moved a value
                    List<string> applied = plan.AppliedOrder ?? plan.Overrides["applied"] as List<string> ?? new List<string>();
                    PlotPlan rule = null;
                    rulePlans?.TryGetValue(number, out rule);
                    rule = rule != null && (bool)rule.Overrides["written"] ? rule : null;
                    fields = new JObject();
                    foreach (Tuple<string, string> item in Sources(plan))
                    {
                        // R3 of the review of PR 75: a field left at its value with no override was moved by none
                        bool unmoved = applied.Count > 0 && rule != null && Unmoved(table.MovedBy(item.Item1), plan, rule, table.Decimals);
                        Tuple<string, JObject, string> status = table.Of(item.Item1, item.Item2, handTable as string, applied, unmoved);
                        // Q107. The key of the detail, the detail once in the table statusFrom of the step file (Tables)
                        fields[item.Item1] = new JObject { ["source"] = item.Item2, ["status"] = status.Item1, ["statusFrom"] = status.Item3 };
                    }
                }
                found[number] = new Dictionary<string, object> { ["unit"] = plan.Overrides["unit"], ["written"] = written, ["fields"] = fields };
            }
            return found;
        }

        // The keys of PlotPlan of a field of the plan, value_status.fields_of of Python
        private static readonly Dictionary<string, string> OverrideKeys = new Dictionary<string, string>
        {
            ["mirrored"] = "mirrored", ["insert"] = "insert", ["rotationDeg"] = "rotationDeg", ["levels.entry"] = "entry", ["levels.ffl"] = "ffl", ["levels.ssl"] = "ssl",
            ["levels.height"] = "height", ["levels.steps"] = "steps", ["floors.ramp.corners"] = "rampZ", ["floors.ramp.type"] = "rampType", ["floors.yard.z"] = "yardZ",
            ["floors.yard.outline"] = "yardOutline",
        };

        // value_status.unmoved: the value of field in the plan the same at the decimals the plan writes it as in the plan of the plot with no
        // override, false for a field the plan holds no value of. The insert of Python holds the FFL as its Z, which the C# keeps apart
        public static bool Unmoved(string field, PlotPlan plan, PlotPlan rule, IReadOnlyDictionary<string, int> decimals)
        {
            // movedBy may add lowest: the lowest Z of the points of that field, which the heightOffset of a ramp is taken from, the Z of each
            // corner here
            string[] parts = field.Split(new[] { ' ' }, 2);
            if (parts.Length == 2 && parts[1] == "lowest" && OverrideKeys.TryGetValue(parts[0], out string lowestKey))
            {
                int? places = decimals.TryGetValue(lowestKey, out int found) ? found : (int?)null;
                Func<object, double> lowest = value => ((System.Collections.IEnumerable)value).Cast<object>()
                    .Select(item => Convert.ToDouble(item is JValue token ? token.Value : item, System.Globalization.CultureInfo.InvariantCulture))
                    .Select(z => places.HasValue ? Rounding.Fixed(z, places.Value) : z).Min();
                return plan.Overrides[lowestKey] != null && rule.Overrides[lowestKey] != null && lowest(plan.Overrides[lowestKey]) == lowest(rule.Overrides[lowestKey]);
            }
            if (OverrideKeys.TryGetValue(field, out string key))
            {
                return Held(key, plan, rule, decimals) && (key != "insert" || Held("ffl", plan, rule, decimals));
            }
            if (field == "linkName")
            {
                return plan.Parameters["linkName"] != null && rule.Parameters["linkName"] != null && Same(plan.Parameters["linkName"], rule.Parameters["linkName"], null);
            }
            if (field.StartsWith("parameters.", StringComparison.Ordinal))
            {
                JToken mine = (plan.Parameters["parameters"] as JObject)?[field.Substring("parameters.".Length)];
                JToken other = (rule.Parameters["parameters"] as JObject)?[field.Substring("parameters.".Length)];
                return mine != null && other != null && JToken.DeepEquals(mine, other);
            }
            return false;
        }

        private static bool Held(string key, PlotPlan plan, PlotPlan rule, IReadOnlyDictionary<string, int> decimals)
        {
            return plan.Overrides[key] != null && rule.Overrides[key] != null && Same(plan.Overrides[key], rule.Overrides[key], decimals.TryGetValue(key, out int places) ? places : (int?)null);
        }

        // Python == of two values at the decimals of value_status.written: numbers by value, a written value by what the plan writes, lists
        // item by item
        private static bool Same(object a, object b, int? decimals)
        {
            a = a is StepValue first ? first.Written : a;
            b = b is StepValue second ? second.Written : b;
            a = a is JValue one ? one.Value : a;
            b = b is JValue two ? two.Value : b;
            if (a == null || b == null)
            {
                return a == null && b == null;
            }
            if (a is JObject || b is JObject)
            {
                return JToken.DeepEquals(a as JToken ?? JToken.FromObject(a), b as JToken ?? JToken.FromObject(b));
            }
            if (a is string || b is string || a is bool || b is bool)
            {
                return a.Equals(b);
            }
            if (a is System.Collections.IEnumerable items && b is System.Collections.IEnumerable others)
            {
                List<object> left = items.Cast<object>().ToList(), right = others.Cast<object>().ToList();
                return left.Count == right.Count && left.Zip(right, (one, other) => Same(one, other, decimals)).All(same => same);
            }
            double x = Convert.ToDouble(a, System.Globalization.CultureInfo.InvariantCulture), y = Convert.ToDouble(b, System.Globalization.CultureInfo.InvariantCulture);
            return decimals.HasValue ? Rounding.Fixed(x, decimals.Value) == Rounding.Fixed(y, decimals.Value) : x == y;
        }

        private static IEnumerable<string> Keys(IReadOnlyDictionary<string, Dictionary<string, object>> step)
        {
            return step.Values.Select(plot => plot["fields"]).OfType<JObject>().SelectMany(fields => fields.Properties().Select(field => (string)field.Value["statusFrom"]));
        }

        // plan_from_book.status_step, Q107: the tables of the step file, statusFrom the detail of each key its fields name, once, in the order of the keys
        public static JObject Tables(IReadOnlyDictionary<string, Dictionary<string, object>> step, ValueStatusTable table)
        {
            var details = new JObject();
            foreach (string key in Keys(step).Distinct().OrderBy(key => key, StringComparer.Ordinal))
            {
                details[key] = table.Used[key];
            }
            return new JObject { ["statusFrom"] = details };
        }

        // value_status.summary statusByField: the plots by field and status, in the order of Python, field then status, the strength of a
        // MEASURED value from the detail of its key
        public static List<KeyValuePair<Tuple<string, string>, int>> Counts(IReadOnlyDictionary<string, Dictionary<string, object>> step, ValueStatusTable table)
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
                    table.Used.TryGetValue((string)field.Value["statusFrom"], out JObject detail);
                    var key = Tuple.Create(field.Name, Label((string)field.Value["status"], detail));
                    counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
                }
            }
            return counts.OrderBy(item => item.Key.Item1, StringComparer.Ordinal).ThenBy(item => item.Key.Item2, StringComparer.Ordinal).ToList();
        }
    }
}
