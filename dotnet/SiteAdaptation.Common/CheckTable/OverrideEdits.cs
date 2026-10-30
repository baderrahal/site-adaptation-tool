using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.CheckTable
{
    // R376, R386, R330. Save overrides of the check table. Only a row whose Use differs from its opening value is a change, and each change
    // carries "byHand": true. Rule on a row of an override marks its entry "use": "rule", Past on one the build left marks it "use":
    // "past", Past on a row of no override adds the entry of the answer key that holds the plot, its source the group of majority.groups
    // that holds the key, and Skip adds {"plot", "reason", "byHand": true} to the list skip. The list is the text of
    // majority.overrides_text, one entry per line, so every line no change touches is kept byte for byte. The text is checked as a
    // build checks it before it is written by a temporary name and a rename
    public static class OverrideEdits
    {
        private const string OverridesHead = "  \"overrides\": [";
        private const string CannotHead = "  \"cannot\": [";
        private const string SkipHead = "  \"skip\": [";

        // The number of changes written, 0 writes nothing
        public static int Save(string path, IReadOnlyList<CheckRow> rows, IReadOnlyList<CheckChoice> uses, JObject config, string runId)
        {
            var byIndex = rows.ToDictionary(row => row.Index);
            var changes = new List<KeyValuePair<CheckRow, string>>();
            foreach (CheckChoice choice in uses)
            {
                if (!byIndex.TryGetValue(choice.Index, out CheckRow row))
                {
                    throw new InvalidOperationException("the check table has no row " + choice.Index);
                }
                if (!CheckUse.IsValid(choice.Use))
                {
                    throw new InvalidOperationException("the Use " + choice.Use + " of plot " + row.Plot + " is not Rule, Past or Skip");
                }
                if (choice.Use == row.OpenUse)
                {
                    continue;
                }
                if (choice.Use == CheckUse.Past && !row.PastOffered)
                {
                    throw new InvalidOperationException("Past is not offered on plot " + row.Plot + " " + row.Check + ": " + (row.PastWhy ?? "no reason was given"));
                }
                changes.Add(new KeyValuePair<CheckRow, string>(row, choice.Use));
            }
            if (changes.Count == 0)
            {
                return 0;
            }
            string text = WorkFiles.ReadText(path);
            Parts parts = Parts.Of(text, path);
            JObject groups = config["majority"]?["groups"] as JObject ?? throw new InvalidDataException("config holds no majority.groups, the source of an entry is the group of its key (R330)");
            List<string> defaults = (config["majority"]?["overrideDefaultSources"] as JArray ?? new JArray()).Select(item => (string)item).ToList();
            foreach (KeyValuePair<CheckRow, string> change in changes)
            {
                CheckRow row = change.Key;
                if (change.Value == CheckUse.Skip)
                {
                    if (!parts.Skip.Any(line => (string)JObject.Parse(line)["plot"] == row.Plot))
                    {
                        parts.Skip.Add(Line(new JObject { ["plot"] = row.Plot, ["reason"] = "Skip chosen in 4 Check on the row " + row.Check + (row.Field == null ? "" : " " + row.Field), ["byHand"] = true }));
                    }
                    continue;
                }
                int at = row.From == "override" ? Find(parts.Overrides, row, defaults) : -1;
                if (at >= 0)
                {
                    parts.Overrides[at] = Line(Entry(JObject.Parse(parts.Overrides[at]), row, change.Value));
                }
                else if (change.Value == CheckUse.Past)
                {
                    parts.Overrides.Add(Line(New(row, groups)));
                }
            }
            string written = parts.Text();
            JObject check = JObject.Parse(written);
            PlanOverrides.CheckEntries(check["overrides"] as JArray ?? new JArray(), groups);
            PlanOverrides.CheckSkip(check["skip"], path + ": ");
            WorkFiles.WriteText(path, runId, written);
            return changes.Count;
        }

        // The entry of an override with the Use of its row, a copy, the entry given is not changed
        public static JObject Entry(JObject entry, CheckRow row, string use)
        {
            var changed = (JObject)entry.DeepClone();
            changed["use"] = use == CheckUse.Rule ? "rule" : "past";
            changed["byHand"] = true;
            return changed;
        }

        // R330. A new entry of Past on a row of no override, the field as the list names it
        private static JObject New(CheckRow row, JObject groups)
        {
            if (row.Key == null || row.Model == null || row.PastValue == null)
            {
                throw new InvalidOperationException("Past on plot " + row.Plot + " " + row.Check + " has no answer key, model or value to write");
            }
            string source = groups.Properties().Where(group => (group.Value as JArray ?? new JArray()).Any(key => (string)key == row.Key)).Select(group => group.Name).FirstOrDefault()
                ?? throw new InvalidOperationException("the key " + row.Key + " of plot " + row.Plot + " is in no group of majority.groups, the list takes no source for it");
            return new JObject
            {
                ["plot"] = row.Plot, ["field"] = ListField(row.Field), ["value"] = row.PastValue.DeepClone(), ["source"] = source, ["model"] = row.Model, ["key"] = row.Key, ["byHand"] = true,
            };
        }

        // A key of the missing list by the field of the override list that holds it, check_rows.MISSING of Python
        public static string ListField(string field)
        {
            const string parameters = "parameters.";
            if (field != null && field.StartsWith(parameters, StringComparison.Ordinal))
            {
                return "parameter " + field.Substring(parameters.Length);
            }
            return field == "floors.ramp" ? "rampType" : field;
        }

        private static int Find(List<string> lines, CheckRow row, List<string> defaults)
        {
            var hits = new List<int>();
            for (int at = 0; at < lines.Count; at++)
            {
                JObject item = JObject.Parse(lines[at]);
                if (Text(item["plot"]) == row.Plot && (string)item["field"] == row.Field && (row.Key == null ? defaults.Contains((string)item["source"]) : (string)item["key"] == row.Key))
                {
                    hits.Add(at);
                }
            }
            if (hits.Count > 1)
            {
                throw new InvalidOperationException("the override list holds " + hits.Count + " entries of plot " + row.Plot + " field " + row.Field + ", a build refuses two (R280)");
            }
            return hits.Count == 1 ? hits[0] : -1;
        }

        private static string Text(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
        }

        // json.dumps of Python with its default separators and ensure_ascii off, as majority.overrides_text writes a line
        public static string Line(JToken value)
        {
            var written = new StringBuilder();
            Dump(value, written);
            return written.ToString();
        }

        private static void Dump(JToken value, StringBuilder written)
        {
            switch (value.Type)
            {
                case JTokenType.Object:
                    written.Append('{');
                    bool first = true;
                    foreach (JProperty item in ((JObject)value).Properties())
                    {
                        written.Append(first ? "" : ", ").Append(JsonConvert.ToString(item.Name)).Append(": ");
                        Dump(item.Value, written);
                        first = false;
                    }
                    written.Append('}');
                    return;
                case JTokenType.Array:
                    written.Append('[');
                    for (int at = 0; at < ((JArray)value).Count; at++)
                    {
                        written.Append(at == 0 ? "" : ", ");
                        Dump(((JArray)value)[at], written);
                    }
                    written.Append(']');
                    return;
                case JTokenType.String:
                    written.Append(JsonConvert.ToString((string)value));
                    return;
                default:
                    written.Append(PyJson.Dumps(value));
                    return;
            }
        }

        // The list in the parts majority.overrides_text writes: the head with the note, the overrides, the cannot list kept as it is and the
        // skip list, each entry on its own line with no comma
        private sealed class Parts
        {
            public List<string> Head { get; } = new List<string>();
            public List<string> Overrides { get; } = new List<string>();
            public List<string> Cannot { get; private set; }
            public List<string> Skip { get; } = new List<string>();
            public bool SkipGiven { get; private set; }

            public static Parts Of(string text, string path)
            {
                var parts = new Parts();
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                int at = 0;
                while (at < lines.Length && lines[at] != OverridesHead)
                {
                    parts.Head.Add(lines[at]);
                    at++;
                }
                List<string> block = null;
                for (; at < lines.Length; at++)
                {
                    string line = lines[at];
                    if (line == OverridesHead || line == CannotHead || line == SkipHead)
                    {
                        parts.SkipGiven |= line == SkipHead;
                        block = line == OverridesHead ? parts.Overrides : line == SkipHead ? parts.Skip : (parts.Cannot = new List<string>());
                    }
                    else if (line == "  ]" || line == "  ],")
                    {
                        block = null;
                    }
                    else if (block != null && line.StartsWith("    {", StringComparison.Ordinal))
                    {
                        string entry = line.Substring(4).TrimEnd(',');
                        try
                        {
                            JObject.Parse(entry);
                        }
                        catch (JsonException)
                        {
                            throw Refused(path, line);
                        }
                        block.Add(entry);
                    }
                    else if (line != "}" && line.Length > 0)
                    {
                        throw Refused(path, line);
                    }
                }
                if (parts.Head.Count == lines.Length)
                {
                    throw Refused(path, "no line " + OverridesHead.Trim());
                }
                return parts;
            }

            private static InvalidDataException Refused(string path, string line)
            {
                return new InvalidDataException(path + " is not one entry per line as majority.overrides_text writes it, at " + line.Trim() + ". Save overrides keeps every other entry cell for cell only on that form (R376)");
            }

            public string Text()
            {
                var written = new StringBuilder();
                written.Append(string.Join("\n", Head)).Append('\n').Append(OverridesHead).Append('\n').Append(Block(Overrides)).Append("  ]");
                if (Cannot != null)
                {
                    written.Append(",\n").Append(CannotHead).Append('\n').Append(Block(Cannot)).Append("  ]");
                }
                if (SkipGiven || Skip.Count > 0)
                {
                    written.Append(",\n").Append(SkipHead).Append('\n').Append(Block(Skip)).Append("  ]");
                }
                return written.Append("\n}\n").ToString();
            }

            private static string Block(List<string> items)
            {
                return string.Join(",\n", items.Select(item => "    " + item)) + (items.Count > 0 ? "\n" : "");
            }
        }
    }
}
