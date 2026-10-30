using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.Common.CheckTable
{
    // R376, docs/design/03b-buttons-2-to-8.md 4 Check. The rows of the check table: those of the build step checkRows of R363, the plots
    // marked Wrong in 3 Review data, and with the past job of Q117 the Plot cut rows of key_join.json and the Unit code rows of a key that
    // links the file of another unit than Typology_Rev9. The check types and what Past writes come from the block checkRows of config
    public static class CheckTableBuild
    {
        // CHOSEN by Code in Lane B 5c, the check type of a plot the team marked Wrong
        public const string TeamCheck = "Team check";
        public const string PlotCut = "Plot cut";
        public const string UnitCode = "Unit code";
        public const string NoPastJob = "No past job was given, so Plot cut shows no rows and no plot is counted as following every rule on Plot cut.";
        public const string PlotCutWhy = "a build always takes the whole baseplan face (R326)";
        private const string Joined = "joined";

        public static string NoKey(string plot)
        {
            return "no answer key holds plot " + plot;
        }

        public static string NoPastValue(string plot)
        {
            return "the past job gives no value for plot " + plot;
        }

        // nbh null is the whole project, one table with NBH all. manualRule gives the manual rule id of a check type and field, null
        // until the main session wires it to the manualRules block of the rules profile
        public static CheckTableView Compose(string nbh, JObject step, JObject block, IReadOnlyList<ReviewCheck> wrong, PastJob past,
            Func<string, string, string> manualRule, bool saveAllowed)
        {
            JObject plots = step["plots"] as JObject ?? new JObject();
            List<string> types = (block["types"] as JArray ?? new JArray()).Select(item => (string)item).ToList();
            // A plot of the build that plots.csv gives no nbhId stands in the NBH the build was asked for, the scope of the run (CHOSEN, R376)
            var numbers = plots.Properties().Where(item => nbh == null || (string)item.Value["nbh"] == nbh || NoNbh(item.Value["nbh"])).Select(item => item.Name).ToList();
            var held = new HashSet<string>(numbers, StringComparer.Ordinal);
            var rows = new List<CheckRow>();
            var stepRows = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string number in numbers)
            {
                JObject plot = (JObject)plots[number];
                List<JObject> made = (plot["rows"] as JArray ?? new JArray()).OfType<JObject>().ToList();
                stepRows[number] = made.Count;
                foreach (JObject item in made)
                {
                    rows.Add(FromStep(number, plot, item, past, nbh));
                }
            }
            foreach (ReviewCheck check in wrong ?? new ReviewCheck[0])
            {
                if (held.Contains(check.Plot))
                {
                    rows.Add(new CheckRow
                    {
                        Plot = check.Plot, Unit = (string)plots[check.Plot]["unit"], Nbh = NbhOf(plots[check.Plot], nbh), Check = TeamCheck, From = "team", OpenUse = CheckUse.Rule,
                        PastWhy = "the team marked the plot Wrong in 3 Review data: " + check.Note, Why = check.Note,
                    });
                }
            }
            if (past?.KeyJoin != null)
            {
                rows.AddRange(CutRows(past.KeyJoin, plots, held));
            }
            if (past?.Links != null)
            {
                rows.AddRange(UnitRows(past.Links, plots, held, block));
            }
            List<CheckRow> ordered = rows.OrderBy(row => row.Plot, Numbers.Instance).ThenBy(row => Rank(types, row.Check)).ThenBy(row => row.Check, StringComparer.Ordinal)
                .ThenBy(row => row.Field ?? "", StringComparer.Ordinal).ThenBy(row => row.From ?? "", StringComparer.Ordinal).ToList();
            for (int at = 0; at < ordered.Count; at++)
            {
                ordered[at].Index = at;
                ordered[at].Nbh = ordered[at].Nbh ?? nbh;
                ordered[at].ManualRuleId = manualRule?.Invoke(ordered[at].Check, ordered[at].Field);
            }
            JObject tables = step["tables"] as JObject ?? new JObject();
            JObject assumed = tables["assumed"] as JObject ?? new JObject();
            var counts = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("All", ordered.Count) };
            counts.AddRange(types.Select(type => new KeyValuePair<string, int>(type, ordered.Count(row => row.Check == type))));
            counts.AddRange(ordered.Select(row => row.Check).Where(type => !types.Contains(type)).Distinct().OrderBy(type => type, StringComparer.Ordinal)
                .Select(type => new KeyValuePair<string, int>(type, ordered.Count(row => row.Check == type))));
            counts.Add(new KeyValuePair<string, int>("Assumed", assumed.Count));
            // R363 counts a plot with no row and no value an override set or moved. A plot of no build row that got a row here no longer
            // follows every rule. The build gives the count only, so a plot it left out as moved is taken off again (CHOSEN, a lower bound)
            int follow = nbh == null && tables["chips"]?["followEveryRule"] != null ? (int)tables["chips"]["followEveryRule"] : numbers.Count(number => stepRows[number] == 0);
            follow -= ordered.Where(row => stepRows.TryGetValue(row.Plot, out int count) && count == 0).Select(row => row.Plot).Distinct().Count();
            follow = Math.Max(0, follow);
            return new CheckTableView
            {
                Nbh = nbh ?? "all",
                Title = "4 Check, NBH " + (nbh ?? "all") + ", " + numbers.Count + " plots",
                Rows = ordered,
                Counts = counts,
                FollowEveryRule = follow,
                FollowLine = follow + " plots follow every rule",
                NoPastJobLine = past != null && past.KeyJoin != null ? null : NoPastJob,
                Assumed = assumed,
                SaveAllowed = saveAllowed,
            };
        }

        private static bool NoNbh(JToken value)
        {
            return value == null || value.Type == JTokenType.Null || (value.Type == JTokenType.String && ((string)value).Length == 0);
        }

        private static string NbhOf(JToken plot, string nbh)
        {
            return NoNbh(plot["nbh"]) ? nbh : (string)plot["nbh"];
        }

        private static CheckRow FromStep(string number, JObject plot, JObject item, PastJob past, string nbh)
        {
            JObject offered = item["past"] as JObject ?? new JObject();
            string form = offered["writes"]?.Type == JTokenType.String ? (string)offered["writes"] : null;
            JToken value = offered["value"] == null || offered["value"].Type == JTokenType.Null ? null : offered["value"];
            string from = (string)item["from"];
            var row = new CheckRow
            {
                Plot = number, Unit = (string)plot["unit"], Nbh = NbhOf(plot, nbh), Check = (string)item["check"], Field = (string)item["field"], From = from,
                OpenUse = (string)item["use"] == CheckUse.Past || (string)item["use"] == CheckUse.Skip ? (string)item["use"] : CheckUse.Rule, Rule = item["rule"], ToolValue = item["toolValue"], PastForm = form, PastValue = value,
                Why = item["why"],
            };
            KeyLink link = null;
            past?.Links?.TryGetValue(number, out link);
            row.Key = link?.Code;
            row.Model = link?.Model;
            if (form == null)
            {
                row.PastWhy = (string)offered["why"] ?? (string)item["why"];
                return row;
            }
            if (from == "override")
            {
                row.PastOffered = true;
                return row;
            }
            if (link == null)
            {
                row.PastWhy = NoKey(number);
                return row;
            }
            row.PastValue = row.PastValue ?? KeyValue(link, row.Field, form);
            row.PastOffered = row.PastValue != null;
            row.PastWhy = row.PastOffered ? null : NoPastValue(number);
            return row;
        }

        // Q117. The value the link of a key gives in the form of the list. Only the hand and the text of a parameter are read from a
        // link, a gap to the rule needs the past value of the key read, which the link does not hold (CHOSEN, the rest stays empty)
        private static JToken KeyValue(KeyLink link, string field, string form)
        {
            if (form == "trueOrFalse" && field == "mirrored" && link.Mirrored != null)
            {
                return link.Mirrored.Value;
            }
            const string parameters = "parameters.", parameter = "parameter ";
            string name = field == null ? null : field.StartsWith(parameters, StringComparison.Ordinal) ? field.Substring(parameters.Length)
                : field.StartsWith(parameter, StringComparison.Ordinal) ? field.Substring(parameter.Length) : null;
            if (form == "text" && name != null && link.Parameters?[name] != null && link.Parameters[name].Type != JTokenType.Null)
            {
                return link.Parameters[name].ToString();
            }
            return null;
        }

        // R326, Q117. A plot of a key whose generic floor joins no one face, cut, no generic floor or any other status of key_join.py
        private static IEnumerable<CheckRow> CutRows(JObject keyJoin, JObject plots, HashSet<string> held)
        {
            foreach (JProperty key in (keyJoin["keys"] as JObject ?? new JObject()).Properties())
            {
                foreach (JProperty plot in (key.Value["join"]?["plots"] as JObject ?? new JObject()).Properties())
                {
                    string status = (string)plot.Value["status"];
                    if (status == null || status == Joined || !held.Contains(plot.Name))
                    {
                        continue;
                    }
                    yield return new CheckRow
                    {
                        Plot = plot.Name, Unit = (string)plots[plot.Name]["unit"], Nbh = (string)plots[plot.Name]["nbh"], Check = PlotCut, From = "join", OpenUse = CheckUse.Rule,
                        Key = key.Name, PastWhy = PlotCutWhy, Why = "the key " + key.Name + " joins the plot as " + status + " (R326)",
                    };
                }
            }
        }

        // Q017, Q056, Q117. A key that links the file of another unit than Typology_Rev9 of the plot. Past is never offered, the code is
        // Typology_Rev9 and never a key's
        private static IEnumerable<CheckRow> UnitRows(IReadOnlyDictionary<string, KeyLink> links, JObject plots, HashSet<string> held, JObject block)
        {
            string why = (string)block["fields"]?["prototypeFile"]?["why"];
            foreach (KeyValuePair<string, KeyLink> link in links)
            {
                if (!held.Contains(link.Key))
                {
                    continue;
                }
                string unit = (string)plots[link.Key]["unit"];
                string linked = Hands.UnitOfFile(link.Value.File);
                if (unit == null || linked == null || string.Equals(unit, linked, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                yield return new CheckRow
                {
                    Plot = link.Key, Unit = unit, Nbh = (string)plots[link.Key]["nbh"], Check = UnitCode, Field = "prototypeFile", From = "key", OpenUse = CheckUse.Rule,
                    ToolValue = unit, Key = link.Value.Code, Model = link.Value.Model, PastWhy = why,
                    Why = "the key " + link.Value.Code + " links " + link.Value.File + " of unit " + linked + ", Typology_Rev9 gives " + unit,
                };
            }
        }

        private static int Rank(List<string> types, string check)
        {
            int at = types.IndexOf(check);
            return at < 0 ? types.Count : at;
        }

        // Plot numbers by their whole number, then as text
        private sealed class Numbers : IComparer<string>
        {
            public static readonly Numbers Instance = new Numbers();

            public int Compare(string x, string y)
            {
                bool a = long.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out long one), b = long.TryParse(y, NumberStyles.None, CultureInfo.InvariantCulture, out long two);
                if (a && b && one != two)
                {
                    return one.CompareTo(two);
                }
                return a != b ? (a ? -1 : 1) : string.CompareOrdinal(x, y);
            }
        }
    }
}
