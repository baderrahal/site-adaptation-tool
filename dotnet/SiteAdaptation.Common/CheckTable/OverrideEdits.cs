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
    // that holds the key, and Skip adds {"plot", "reason", "byHand": true} to the list skip. On a row that opened on Skip the use its entry
    // holds under the skip takes only the plot off the list skip and leaves the entry byte for byte, as no hand edit of it was made, so an
    // applied override stays applied and an entry of a majority run is not frozen by a byHand it never had (R330). The list is the text of
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
            string text = WorkFiles.ReadText(path);
            Edited edit = Apply(text, path, rows, uses, config);
            if (edit.Count > 0)
            {
                WorkFiles.WriteText(path, runId, edit.Text);
            }
            return edit.Count;
        }

        // The list text with the changes of the uses on the text given, which is kept as it is when no change writes anything. A change
        // counts only where it alters the text: Skip on a plot the list skips already writes nothing
        public static Edited Apply(string text, string path, IReadOnlyList<CheckRow> rows, IReadOnlyList<CheckChoice> uses, JObject config)
        {
            var byIndex = rows.ToDictionary(row => row.Index);
            var final = rows.ToDictionary(row => row.Index, row => row.OpenUse);
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
                final[row.Index] = choice.Use;
            }
            if (changes.Count == 0)
            {
                return new Edited(text, 0);
            }
            Parts parts = Parts.Of(text, path);
            JObject groups = config["majority"]?["groups"] as JObject ?? throw new InvalidDataException("config holds no majority.groups, the source of an entry is the group of its key (R330)");
            List<string> defaults = (config["majority"]?["overrideDefaultSources"] as JArray ?? new JArray()).Select(item => (string)item).ToList();
            var unskipped = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (KeyValuePair<CheckRow, string> change in changes)
            {
                CheckRow row = change.Key;
                if (change.Value == CheckUse.Skip)
                {
                    if (!parts.Skip.Any(line => SkipOf(line) == row.Plot))
                    {
                        parts.Skip.Add(Line(new JObject { ["plot"] = row.Plot, ["reason"] = "Skip chosen in 4 Check on the row " + row.Check + (row.Field == null ? "" : " " + row.Field), ["byHand"] = true }));
                        count++;
                    }
                    continue;
                }
                bool written = false;
                if (row.OpenUse == CheckUse.Skip)
                {
                    // The skip list holds a plot, not a row, so it keeps the plot while any row of it is left on Skip
                    CheckRow kept = rows.FirstOrDefault(other => other.Plot == row.Plot && final[other.Index] == CheckUse.Skip);
                    if (kept != null)
                    {
                        throw new InvalidOperationException("plot " + row.Plot + " stays on the skip list while its row " + kept.Check + (kept.Field == null ? "" : " " + kept.Field)
                            + " keeps Skip, set every row of plot " + row.Plot + " off Skip to take it off (R376)");
                    }
                    if (parts.Skip.RemoveAll(line => SkipOf(line) == row.Plot) > 0 || unskipped.Contains(row.Plot))
                    {
                        unskipped.Add(row.Plot);
                        written = true;
                    }
                }
                if (row.From == "override")
                {
                    int at = Find(parts.Overrides, row, defaults);
                    if (at < 0)
                    {
                        throw new InvalidOperationException("the override list holds no entry of plot " + row.Plot + " field " + row.Field + " that the build applied or left, it changed since the build. Run 4 Check again (R376)");
                    }
                    JObject entry = JObject.Parse(parts.Overrides[at]);
                    string line = row.OpenUse == CheckUse.Skip && change.Value == UseOf(entry) ? parts.Overrides[at] : Line(Entry(entry, row, change.Value));
                    if (line != parts.Overrides[at])
                    {
                        parts.Overrides[at] = line;
                        written = true;
                    }
                }
                else if (change.Value == CheckUse.Past)
                {
                    parts.Overrides.Add(Line(New(row, groups)));
                    written = true;
                }
                count += written ? 1 : 0;
            }
            if (count == 0)
            {
                return new Edited(text, 0);
            }
            string edited = parts.Text();
            JObject check = JObject.Parse(edited);
            PlanOverrides.CheckEntries(check["overrides"] as JArray ?? new JArray(), groups);
            PlanOverrides.CheckSkip(check["skip"], path + ": ");
            return new Edited(edited, count);
        }

        public sealed class Edited
        {
            public Edited(string text, int count)
            {
                Text = text;
                Count = count;
            }

            public string Text { get; }

            public int Count { get; }
        }

        private static string SkipOf(string line)
        {
            return Text(JObject.Parse(line)["plot"]);
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

        // R330, R386. The Use an entry holds for its row, the one a plot on the skip list keeps under the skip: Rule for an entry marked
        // "use": "rule", Past for any other, since a build applies an entry with no use
        public static string UseOf(JObject entry)
        {
            return PlanOverrides.UseOf(entry) == "rule" ? CheckUse.Rule : CheckUse.Past;
        }

        // The entry of the list a row of an override names, by plot, field and the key of the row, else the default sources of a build
        public static bool Matches(JObject item, CheckRow row, ICollection<string> defaults)
        {
            return Text(item["plot"]) == row.Plot && Text(item["field"]) == row.Field && (row.Key == null ? defaults.Contains(Text(item["source"])) : Text(item["key"]) == row.Key);
        }

        private static int Find(List<string> lines, CheckRow row, List<string> defaults)
        {
            var hits = new List<int>();
            for (int at = 0; at < lines.Count; at++)
            {
                JObject item = JObject.Parse(lines[at]);
                if (Matches(item, row, defaults))
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
