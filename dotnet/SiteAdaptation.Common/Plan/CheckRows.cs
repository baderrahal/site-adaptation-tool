using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R363. What the check rows take of a plot: its unit and NBH of plots.csv, its skip reason, the overrides the build applied and left with
    // why and the keys of the missing list, check_rows.plot_infos of Python
    public sealed class CheckPlot
    {
        public string Unit { get; set; }

        public string Nbh { get; set; }

        public string Skip { get; set; }

        public List<string> Applied { get; set; } = new List<string>();

        public JObject NotApplied { get; set; } = new JObject();

        public List<string> Lacks { get; set; } = new List<string>();
    }

    // Q108. The status of one value of the plan, on the plots it is a value of
    public sealed class StatusValue
    {
        public string Field { get; set; }

        public List<string> Plots { get; set; }

        public string Status { get; set; }

        public string StatusFrom { get; set; }

        public string Source { get; set; }
    }

    // R363. The check rows of button 3, python/check_rows.py: one row per plot and check of an override of the list, a plot and field an open
    // question of majority.decidedByQuestion decides, an entry of the cannot list of R272 and a key of the missing list, with its check type,
    // field, the Use it opens on and what Past writes in the form of the list, all from the block checkRows of the settings, and the chip
    // Assumed of Q108, one row per rule and status with its plots. The step checkRows of the parity harness, step 14
    public static class CheckRows
    {
        public const string StepName = "checkRows";

        public static readonly string[] StepFields = { "unit", "nbh", "rows" };

        // check_rows.FROM_ORDER, the sources of a row in the order a plot lists them within one check and field
        private static readonly string[] FromOrder = { "override", "question", "cannot", "missing" };
        private const string Rule = "Rule";
        private const string Past = "Past";
        private const string Skip = "Skip";
        public const string NoBlock = "the settings hold no block checkRows, the check rows of R363 take their check types and the form of Past from it";

        private static string Text(JToken value)
        {
            return value == null || value.Type == JTokenType.Null ? null : value.Type == JTokenType.String ? (string)value : value.ToString(Formatting.None);
        }

        private static bool Truthy(JToken value)
        {
            return value != null && value.Type != JTokenType.Null && !(value.Type == JTokenType.String && ((string)value).Length == 0);
        }

        // check_rows.list_entries: the overrides and the cannot entries of the list whose source is one of sources, with models only those
        // of the past model files named
        public static Tuple<List<JObject>, List<JObject>> ListEntries(JObject data, ICollection<string> sources, ICollection<string> models = null)
        {
            Func<string, List<JObject>> chosen = name => (data?[name] as JArray ?? new JArray()).OfType<JObject>()
                .Where(item => sources.Contains(Text(item["source"])) && (models == null || models.Count == 0 || models.Contains(Text(item["model"])))).ToList();
            return Tuple.Create(chosen("overrides"), chosen("cannot"));
        }

        // check_rows.entry_of: the entry of a field or missing key, its own, else that of its head, parameter * or levels.*, refused when none
        public static JObject EntryOf(JObject block, string part, string name)
        {
            JObject table = block[part] as JObject ?? new JObject();
            if (table[name] is JObject own)
            {
                return own;
            }
            foreach (string mark in new[] { ".", " " })
            {
                int at = name.IndexOf(mark, StringComparison.Ordinal);
                if (at >= 0 && table[name.Substring(0, at) + mark + "*"] is JObject head)
                {
                    return head;
                }
            }
            throw new InvalidDataException("checkRows." + part + " names no check for " + name + " (R363)");
        }

        // check_rows.plot_infos from the plots of the build after the overrides, the unit and NBH from the rows of plots.csv
        public static Dictionary<string, CheckPlot> PlotsOf(IEnumerable<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans,
            IReadOnlyDictionary<string, Dictionary<string, string>> rows)
        {
            var found = new Dictionary<string, CheckPlot>();
            foreach (string number in numbers)
            {
                rows.TryGetValue(number, out Dictionary<string, string> row);
                string unit = null, nbh = null;
                row?.TryGetValue("typologyRev9", out unit);
                row?.TryGetValue("nbhId", out nbh);
                plans.TryGetValue(number, out PlotPlan plan);
                Dictionary<string, object> overrides = plan?.Overrides ?? new Dictionary<string, object>();
                overrides.TryGetValue("skip", out object skip);
                overrides.TryGetValue("applied", out object applied);
                overrides.TryGetValue("notApplied", out object left);
                found[number] = new CheckPlot
                {
                    Unit = string.IsNullOrEmpty(unit) ? null : unit, Nbh = string.IsNullOrEmpty(nbh) ? null : nbh, Skip = skip as string,
                    Applied = (applied as List<string> ?? new List<string>()).ToList(), NotApplied = (left as JObject)?.DeepClone() as JObject ?? new JObject(),
                    Lacks = (plan?.Lacks ?? new List<string>()).Distinct().ToList(),
                };
            }
            return found;
        }

        private static JObject PastOf(JObject item, JToken value = null)
        {
            JToken writes = item["past"];
            return new JObject
            {
                ["writes"] = writes?.DeepClone() ?? JValue.CreateNull(), ["value"] = value?.DeepClone() ?? JValue.CreateNull(),
                ["why"] = writes != null && writes.Type != JTokenType.Null ? JValue.CreateNull() : item["why"]?.DeepClone() ?? JValue.CreateNull(),
            };
        }

        private static JObject Made(string check, string field, string came, string use, JToken rule, JToken tool, JObject past, JToken why)
        {
            return new JObject
            {
                ["check"] = check, ["field"] = field, ["from"] = came, ["use"] = use, ["rule"] = rule?.DeepClone() ?? JValue.CreateNull(),
                ["toolValue"] = tool?.DeepClone() ?? JValue.CreateNull(), ["past"] = past, ["why"] = why?.DeepClone() ?? JValue.CreateNull(),
            };
        }

        // check_rows.plot_rows: the rows of each plot of the scope, ordered by the check types of the mock, then any other type by name, then
        // field and source
        public static Dictionary<string, List<JObject>> Rows(IReadOnlyList<string> numbers, IReadOnlyDictionary<string, CheckPlot> plots, IList<JObject> overrides,
            IList<JObject> cannot, JObject decided, JObject block)
        {
            var byPair = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject item in overrides)
            {
                byPair[Text(item["plot"]) + "\n" + Text(item["field"])] = item;
            }
            var questions = new Dictionary<string, List<Tuple<string, string, JToken>>>(StringComparer.Ordinal);
            foreach (JProperty question in (decided ?? new JObject()).Properties())
            {
                if (!(question.Value is JObject rule))
                {
                    continue;
                }
                foreach (JProperty byKey in (rule["plots"] as JObject ?? new JObject()).Properties())
                {
                    foreach (JToken plot in byKey.Value as JArray ?? new JArray())
                    {
                        foreach (JToken field in rule["fields"] as JArray ?? new JArray())
                        {
                            string key = Text(plot);
                            if (!questions.ContainsKey(key))
                            {
                                questions[key] = new List<Tuple<string, string, JToken>>();
                            }
                            questions[key].Add(Tuple.Create((string)field, question.Name, rule["why"]));
                        }
                    }
                }
            }
            var misses = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            foreach (JObject item in cannot)
            {
                string key = Text(item["plot"]);
                if (!misses.ContainsKey(key))
                {
                    misses[key] = new List<JObject>();
                }
                misses[key].Add(item);
            }
            List<string> types = (block["types"] as JArray ?? new JArray()).Select(item => (string)item).ToList();
            var found = new Dictionary<string, List<JObject>>();
            foreach (string number in numbers)
            {
                CheckPlot plot = plots[number];
                bool skip = plot.Skip != null;
                var made = new List<JObject>();
                List<string> applied = plot.Applied ?? new List<string>();
                JObject left = plot.NotApplied ?? new JObject();
                foreach (string field in applied.Concat(left.Properties().Select(item => item.Name).Where(name => !applied.Contains(name))))
                {
                    JObject item = EntryOf(block, "fields", field);
                    byPair.TryGetValue(number + "\n" + field, out JObject entry);
                    made.Add(Made((string)item["check"], field, "override", skip ? Skip : applied.Contains(field) ? Past : Rule, entry?["decision"], entry?["rule"],
                        PastOf(item, entry?["value"]), left[field]));
                }
                if (questions.TryGetValue(number, out List<Tuple<string, string, JToken>> asked))
                {
                    foreach (Tuple<string, string, JToken> question in asked)
                    {
                        JObject item = EntryOf(block, "fields", question.Item1);
                        made.Add(Made((string)item["check"], question.Item1, "question", skip ? Skip : Rule, question.Item2, null, PastOf(item), question.Item3));
                    }
                }
                if (misses.TryGetValue(number, out List<JObject> missed))
                {
                    foreach (JObject entry in missed)
                    {
                        JObject item = EntryOf(block, "fields", Text(entry["field"]));
                        var past = new JObject { ["writes"] = JValue.CreateNull(), ["value"] = JValue.CreateNull(), ["why"] = block["cannotWhy"]?.DeepClone() ?? JValue.CreateNull() };
                        made.Add(Made((string)item["check"], Text(entry["field"]), "cannot", skip ? Skip : Rule, entry["decision"], null, past, entry["reason"]));
                    }
                }
                foreach (string key in plot.Lacks ?? new List<string>())
                {
                    JObject item = EntryOf(block, "missing", key);
                    made.Add(Made((string)item["check"], key, "missing", skip ? Skip : Rule, null, null, PastOf(item), null));
                }
                found[number] = made.OrderBy(item => types.Contains((string)item["check"]) ? types.IndexOf((string)item["check"]) : types.Count)
                    .ThenBy(item => (string)item["check"], StringComparer.Ordinal).ThenBy(item => (string)item["field"], StringComparer.Ordinal)
                    .ThenBy(item => Array.IndexOf(FromOrder, (string)item["from"])).ToList();
            }
            return found;
        }

        // check_rows.type_order: the check types of the mock in its order, then any other type the rows hold by name
        public static List<string> TypeOrder(IReadOnlyDictionary<string, List<JObject>> rows, JObject block)
        {
            List<string> types = (block["types"] as JArray ?? new JArray()).Select(item => (string)item).ToList();
            return types.Concat(rows.Values.SelectMany(made => made).Select(item => (string)item["check"]).Distinct().Where(name => !types.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)).ToList();
        }

        // check_rows.chips: All, the count per check type in the order of the mock, Assumed and the plots of the scope with no row
        public static JObject Chips(IReadOnlyList<string> numbers, IReadOnlyDictionary<string, List<JObject>> rows, int assumed, JObject block)
        {
            var found = new JObject { ["All"] = numbers.Sum(number => rows[number].Count) };
            foreach (string name in TypeOrder(rows, block))
            {
                found[name] = numbers.Sum(number => rows[number].Count(item => (string)item["check"] == name));
            }
            found["Assumed"] = assumed;
            found["followEveryRule"] = numbers.Count(number => rows[number].Count == 0);
            return found;
        }

        private static string Label(JObject entry)
        {
            string status = Text(entry["status"]);
            string strength = Text(entry["strength"]);
            return status + (status == "MEASURED" && !string.IsNullOrEmpty(strength) ? " " + strength : "");
        }

        // check_rows.rule_of: the entry of the detail value_status chose, the worst by worstFirst, else the first, named by its block entry,
        // else its question, document or why
        public static string RuleOf(JObject detail, IList<string> worst)
        {
            List<JObject> entries = (detail?["entries"] as JArray)?.OfType<JObject>().ToList();
            if (entries == null || entries.Count == 0)
            {
                entries = new List<JObject> { new JObject() };
            }
            JObject chosen = entries[0];
            int best = int.MaxValue;
            for (int index = 0; index < entries.Count; index++)
            {
                int rank = entries[index]["status"] == null ? -1 : worst.IndexOf(Label(entries[index]));
                if (rank >= 0 && rank < best)
                {
                    best = rank;
                    chosen = entries[index];
                }
            }
            return new[] { "entry", "question", "document", "why" }.Select(key => chosen[key]).Where(Truthy).Select(Text).FirstOrDefault();
        }

        // check_rows.assumed_rows: per rule and status the plots that hold a value of it, the count of the values and their fields, a status
        // out of assumed and a value an override sets, of the source override or moved by one, not shown
        public static JObject AssumedRows(IEnumerable<StatusValue> values, JObject details, IList<string> assumed, IList<string> worst)
        {
            var grouped = new Dictionary<string, Tuple<string, string, HashSet<string>, int[], HashSet<string>>>(StringComparer.Ordinal);
            foreach (StatusValue value in values)
            {
                JObject detail = details?[value.StatusFrom] as JObject ?? new JObject();
                string strength = Text(detail["strength"]);
                string label = value.Status + (value.Status == "MEASURED" && !string.IsNullOrEmpty(strength) ? " " + strength : "");
                if (!assumed.Contains(label) || value.Source == "override" || Truthy(detail["override"]))
                {
                    continue;
                }
                string rule = RuleOf(detail, worst);
                string key = (rule ?? "None") + " | " + label;
                if (!grouped.TryGetValue(key, out Tuple<string, string, HashSet<string>, int[], HashSet<string>> item))
                {
                    item = grouped[key] = Tuple.Create(rule, label, new HashSet<string>(StringComparer.Ordinal), new int[1], new HashSet<string>(StringComparer.Ordinal));
                }
                item.Item3.UnionWith(value.Plots);
                item.Item4[0]++;
                item.Item5.Add(value.Field);
            }
            var found = new JObject();
            foreach (KeyValuePair<string, Tuple<string, string, HashSet<string>, int[], HashSet<string>>> item in grouped.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                found[item.Key] = new JObject
                {
                    ["rule"] = item.Value.Item1, ["status"] = item.Value.Item2, ["plotCount"] = item.Value.Item3.Count,
                    ["plots"] = new JArray(item.Value.Item3.OrderBy(plot => plot, PyMath.SortKey)), ["values"] = item.Value.Item4[0],
                    ["fields"] = new JArray(item.Value.Item5.OrderBy(field => field, StringComparer.Ordinal)),
                };
            }
            return found;
        }

        // check_rows.plan_values: each sourced value of a written plot of the scope by the step status, and each value of a wall on the written
        // plots of the scope it stands on (R362)
        public static List<StatusValue> PlanValues(IReadOnlyList<string> numbers, IReadOnlyDictionary<string, Dictionary<string, object>> status, IEnumerable<PlanWall> walls)
        {
            var written = new HashSet<string>(StringComparer.Ordinal);
            var found = new List<StatusValue>();
            foreach (string number in numbers)
            {
                if (status.TryGetValue(number, out Dictionary<string, object> plot) && plot["fields"] is JObject fields)
                {
                    written.Add(number);
                    found.AddRange(fields.Properties().Select(field => new StatusValue
                    {
                        Field = field.Name, Plots = new List<string> { number }, Status = (string)field.Value["status"], StatusFrom = (string)field.Value["statusFrom"],
                        Source = (string)field.Value["source"],
                    }));
                }
            }
            foreach (PlanWall wall in walls)
            {
                List<string> plots = wall.Plots.Where(written.Contains).ToList();
                if (plots.Count > 0)
                {
                    found.AddRange(Walls.Fields.Select(field => new StatusValue
                    {
                        Field = "walls." + field, Plots = plots, Status = wall.Status[field][1], StatusFrom = wall.Status[field][2], Source = wall.Status[field][0],
                    }));
                }
            }
            return found;
        }

        // check_rows.step: per plot of the scope its unit, NBH and rows, and the tables assumed and chips, from the plots of the build, the
        // status of its values, its walls and the override list as the build took it
        public static Tuple<Dictionary<string, Dictionary<string, object>>, JObject> Step(IReadOnlyList<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans,
            IReadOnlyDictionary<string, Dictionary<string, string>> rows, JObject data, ICollection<string> sources, JObject config, IReadOnlyDictionary<string, Dictionary<string, object>> status,
            IEnumerable<PlanWall> walls, JObject details)
        {
            JObject block = config["checkRows"] as JObject ?? throw new InvalidDataException(NoBlock);
            Tuple<List<JObject>, List<JObject>> entries = ListEntries(data, sources);
            Dictionary<string, CheckPlot> plots = PlotsOf(numbers, plans, rows);
            Dictionary<string, List<JObject>> made = Rows(numbers, plots, entries.Item1, entries.Item2, config["majority"]?["decidedByQuestion"] as JObject, block);
            List<string> assumed = (config["valueStatus"]?["assumed"] as JArray ?? new JArray()).Select(item => (string)item).ToList();
            List<string> worst = (config["valueStatus"]?["worstFirst"] as JArray ?? new JArray()).Select(item => (string)item).ToList();
            JObject groups = AssumedRows(PlanValues(numbers, status, walls), details, assumed, worst);
            var found = numbers.ToDictionary(number => number, number => new Dictionary<string, object>
            {
                ["unit"] = plots[number].Unit, ["nbh"] = plots[number].Nbh, ["rows"] = new JArray(made[number]),
            });
            return Tuple.Create(found, new JObject { ["assumed"] = groups, ["chips"] = Chips(numbers, made, groups.Count, block) });
        }
    }
}
