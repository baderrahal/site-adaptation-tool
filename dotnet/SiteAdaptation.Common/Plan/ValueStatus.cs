using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R401, the plan part, as value_status.Table of Python gives it. The map of valueStatus of the settings and the block status of the rules
    // profile, read once per build for the level profile the build follows: the status of a value of a field from a source and the detail that
    // gives it. A pair the map does not hold, or an entry the block lacks, is UNKNOWN with why, never a guess
    public sealed class ValueStatusTable
    {
        private const string Override = "override";
        private const string Parameters = "parameters.";
        private const string AnyParameter = "parameters.*";
        private static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        private readonly JObject sources, fields, sets, block;
        private readonly Dictionary<string, string> files;
        private readonly string levelProfile;

        public ValueStatusTable(JObject map, JObject block, string profileFile, string levelProfile)
        {
            sources = map?["sources"] as JObject ?? new JObject();
            fields = map?["fields"] as JObject ?? new JObject();
            sets = map?["overrideSets"] as JObject ?? new JObject();
            this.block = block ?? new JObject();
            files = new Dictionary<string, string> { ["profile"] = profileFile, ["config"] = ValueStatus.SettingsFile };
            this.levelProfile = levelProfile;
        }

        // An applied override of overrideSets that sets the field gives it the status of the source override, as its source stays
        public Tuple<string, JObject> Of(string field, string source, string hand = null, IEnumerable<string> applied = null)
        {
            string moved = (applied ?? Enumerable.Empty<string>()).FirstOrDefault(name => sets[name]?.Type == JTokenType.String && (string)sets[name] == field);
            if (moved != null)
            {
                source = Override;
            }
            JObject reference = null;
            foreach (string name in field.StartsWith(Parameters, StringComparison.Ordinal) ? new[] { field, AnyParameter } : new[] { field })
            {
                reference = (fields[name] as JObject)?[source] as JObject;
                if (reference != null)
                {
                    break;
                }
            }
            reference = reference ?? sources[source] as JObject;
            Tuple<string, JObject> found = Resolve(field, source, reference, hand);
            if (moved != null)
            {
                found.Item2["override"] = moved;
            }
            return found;
        }

        private static Tuple<string, JObject> Unknown(string why)
        {
            return Tuple.Create("UNKNOWN", new JObject { ["why"] = why + " (R401)" });
        }

        private static JObject Rest(JObject entry)
        {
            var rest = new JObject();
            foreach (JProperty item in entry.Properties().Where(item => item.Name != "status"))
            {
                rest[item.Name] = item.Value.DeepClone();
            }
            return rest;
        }

        private Tuple<string, JObject> Resolve(string field, string source, JObject reference, string hand)
        {
            if (reference == null)
            {
                return Unknown("valueStatus holds no status for " + field + " from " + source);
            }
            if (reference.Property("status") != null)
            {
                return Tuple.Create((string)reference["status"], Rest(reference));
            }
            string kind = reference.Property("profile") != null ? "profile" : reference.Property("config") != null ? "config" : null;
            if (kind == null)
            {
                return Unknown("valueStatus names no block entry and no status for " + field + " from " + source);
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
                return Unknown("the plot holds no " + lacking + " for " + template);
            }
            string path = Placeholder.Replace(template, match => names[match.Groups[1].Value]);
            JObject entry = (block[files[kind]] as JObject)?[path] as JObject;
            if (entry == null || entry.Property("status") == null)
            {
                return Unknown("the block status holds no entry " + files[kind] + " " + path);
            }
            JObject detail = new JObject { ["entry"] = files[kind] + " " + path };
            foreach (JProperty item in Rest(entry).Properties())
            {
                detail[item.Name] = item.Value;
            }
            return Tuple.Create((string)entry["status"], detail);
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
                        Tuple<string, JObject> status = table.Of(item.Item1, item.Item2, handTable as string, applied);
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
