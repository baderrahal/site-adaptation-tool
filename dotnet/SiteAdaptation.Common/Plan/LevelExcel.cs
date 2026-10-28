using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R364, docs/design/07-mock.md 7.5 and item 5 of docs/addin-mock.md. Every label of the level Excel in one place, word for word from
    // the mock where it gives the words, the rest CHOSEN in Round ADDIN 4g3 PR 4. A value is written <name> and filled by LevelSheets
    public static class LevelExcelText
    {
        public const string FileName = "<nbh>_Levels_<profile>.xlsx";

        public const string LevelsSheet = "Levels";
        public const string ClustersSheet = "Clusters";
        public const string OverridesSheet = "Overrides used";
        public const string SettingsSheet = "Settings";

        public const string PlotNo = "Plot No";
        public const string Unit = "Unit";
        public const string Cluster = "Cluster";
        public const string RoadAtFront = "Road at front";
        public const string Entry = "El @ Entry";
        public const string Ffl = "FFL";
        public const string Ssl = "SSL";
        public const string Height = "Height";
        public const string Steps = "Steps";
        public const string Source = "Source";
        public static readonly string[] LevelColumns = { PlotNo, Unit, Cluster, RoadAtFront, Entry, Ffl, Ssl, Height, Steps, Source };

        // 7.5: Road at front, El @ Entry and Height with 4 decimals as R2 keeps them, FFL and SSL with 3, the steps a whole number, null a text
        public const string FourDecimals = "0.0000";
        public const string ThreeDecimals = "0.000";
        public const string Whole = "0";
        public static readonly string[] LevelFormats = { null, null, null, FourDecimals, FourDecimals, ThreeDecimals, ThreeDecimals, FourDecimals, Whole, null };

        // Bader, 2026-10-28, for the button Update from report of Phase 5f: the columns the team may edit, marked by one light fill on their
        // header and listed in Settings, every other column read only and unmarked. The colour is ARGB, CHOSEN light blue
        public static readonly string[] Editable = { Entry, Ffl, Steps };
        public const string EditableFill = "FFDDEBF7";

        public const string Pieces = "Plots";
        public const string UnitSequence = "Unit sequence";
        public const string Split = "Split";
        public static readonly string[] ClusterColumns = { Cluster, Pieces, UnitSequence, Split, Ffl };
        public static readonly string[] ClusterFormats = { null, null, null, null, ThreeDecimals };

        public const string Field = "Field";
        // Review 1 of PR 91, CHOSEN: the gap of every level field as a number, the step count of a steps override less the steps of the rule
        public const string Gap = "Gap";
        public const string Reason = "Reason";
        public const string Model = "Model";
        public static readonly string[] OverrideColumns = { PlotNo, Field, Gap, Reason, Source, Model };
        public static readonly string[] OverrideFormats = { null, null, null, null, null, null };

        public const string Setting = "Setting";
        public const string Value = "Value";
        public static readonly string[] SettingColumns = { Setting, Value };
        public const string RunId = "Run id";
        public const string Profile = "Profile";
        public const string RulesSha = "Rules SHA-256";
        public const string OverridesSha = "Override list SHA-256";
        public const string RoadOrder = "Road surface order";
        public const string PassLimit = "Pass limit";
        public const string RunTime = "Run time";
        public const string EditableColumns = "Editable columns";
        public const string PlanBuildId = "Plan build id";
        public const string PlanSha = "Plan SHA-256";

        public const string TopLine = "NBH <nbh>, <zones>, road surface <surfaces> of <n> front corners<none>, formulas: El @ Entry = the road at the two front corners + the slope "
            + "of the unit x the ramp depth (R278, R339), FFL = <ffl> (R340), SSL = FFL <sign> <ssl> m (levels.ground_ssl), Height = FFL - El @ Entry, "
            + "Steps = one for the base and one per riser over it, half a riser no step (R340)";

        public const string SurfaceCount = "<name> on <n>";
        // Review 1 of PR 91: a front corner no road surface covers is counted apart, never named as a surface
        public const string NoSurface = ", front corners with no road surface <n>";
        public const string ZonesJoin = " and ";
        public const string NoZone = "no zone";

        // The FFL of each height rule a level profile names, keyRules.ffl.rule or the height of a profile with no keyRules (R278, R340)
        public static readonly Dictionary<string, string> FflFormulas = new Dictionary<string, string>
        {
            ["oneFflPerPieceAtTheHighestEntry"] = "the highest El @ Entry of the piece + the base of its unit, to the mm",
            ["oneFflPerClusterAtTheHighestEntry"] = "the highest El @ Entry of the cluster + the base of its unit, to the mm",
            ["ownEntryPlusBase"] = "El @ Entry + the base of its unit, to the mm",
            ["medianPerUnit"] = "El @ Entry + the median height of its unit, to the mm",
        };

        public const string Rule = "Rule";
        public const string Override = "Override ";
        public const string OverrideReason = "<field> (<reason>)";
        public const string NoEntry = "no entry in the override list";
        public const string NotWritten = "Not written: ";
        public const string MissingReason = "<key> (<why>)";
        public const string NoCluster = "none";
        public const string NoSplit = "none";
        public const string MiddleSplit = "middle";
        public const string WallsSplit = "walls <n>";
        public const string UnitNotWritten = "not written";
        // Review 1 of PR 91: a plot of a cluster the plan neither writes nor lists as missing lies outside the scope of the build
        public const string OutsideScope = "outside the scope";
        // Review 1 of PR 91: the build id is the run id of the run stamp of the plan only, a Python build writes none
        public const string NoBuildId = "none, the plan holds no run stamp";
        public const string ClusterJoin = "-";
        public const string ListJoin = ", ";
        public const string PassLimitMm = "<n> mm";
        public const string NoPassLimit = "none";
    }

    // R364. One sheet of the level Excel: its name, the top line over its header or null, the header, the number format of each column,
    // null for a text, and its rows, each cell a string, a double, a long or null for an empty cell
    public sealed class LevelSheet
    {
        public string Name { get; set; }
        public string TopLine { get; set; }
        public string[] Header { get; set; }
        public string[] Formats { get; set; }
        public List<object[]> Rows { get; } = new List<object[]>();
    }

    // One workbook, of one NBH of the plan
    public sealed class LevelBook
    {
        public string Nbh { get; set; }
        public string FileName { get; set; }
        public List<LevelSheet> Sheets { get; } = new List<LevelSheet>();
    }

    public sealed class LevelResult
    {
        public List<LevelBook> Books { get; } = new List<LevelBook>();
        public List<string> Issues { get; } = new List<string>();
        public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
    }

    // What the level Excel is made from: the plan as written, its rules profile, config with the road surface order of the level profile,
    // the override list, the step clusters, levels and hands of the build, the rows of plots.csv, and the run that writes it
    public sealed class LevelInputs
    {
        public JObject Plan { get; set; }
        public string PlanSha256 { get; set; }
        public JObject Rules { get; set; }
        public string RulesName { get; set; }
        public string RulesSha256 { get; set; }
        public JObject Config { get; set; }
        public List<string> RoadOrder { get; set; }
        public JObject Overrides { get; set; }
        public string OverridesSha256 { get; set; }
        public JObject Clusters { get; set; }
        public JObject Levels { get; set; }
        public JObject Hands { get; set; }
        public List<Dictionary<string, string>> PlotRows { get; set; }
        public string RunId { get; set; }
        public string Profile { get; set; }
        public DateTime Now { get; set; }
    }

    // R364. The sheets of the level Excel from the plan as written, no level recomputed: each value of the levels of a plot is the value the
    // plan holds, the cluster of a plot the cluster of the step clusters of the build that holds it, a piece of the sheet Clusters the piece
    // the step levels of the build give each plot with the FFL of its rule (levelRule.ffl), and the reason of an override the one of its entry
    // in the list. One workbook per NBH of the plan by nbhId of plots.csv (7.5, review 1 of PR 91)
    public static class LevelSheets
    {
        // The fields of the override list that move a level, El @ Entry, the FFL, the steps or the ramp front (7.5)
        public static readonly string[] LevelFields = { "entryM", "heightM", "steps", "rampFrontM" };

        private static readonly Regex NamePart = new Regex("^[A-Za-z0-9_-]{1,64}$");

        private static string Fill(string template, params string[] pairs)
        {
            for (int index = 0; index < pairs.Length; index += 2)
            {
                template = template.Replace("<" + pairs[index] + ">", pairs[index + 1]);
            }
            return template;
        }

        private static string Text(JToken token)
        {
            return token?.Type == JTokenType.String ? (string)token : null;
        }

        private static string Number(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string Count(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // Null when the plan, the rules and the request agree on the level profile, else why the request is refused
        public static string Refusal(JObject plan, JObject rules, string profile)
        {
            string built = Text((plan?["summary"]?["rules"] as JObject)?["levelProfile"]);
            if (built == null)
            {
                return "the plan names no level profile in summary.rules, a level Excel is written of a build by its level profile (R364)";
            }
            if (built != profile)
            {
                return "the plan was built with the level profile " + built + ", the request names " + profile + " (R364)";
            }
            if (!(rules?["levelProfiles"]?[profile] is JObject))
            {
                return "the rules profile holds no level profile " + profile + " (R364)";
            }
            if (!NamePart.IsMatch(profile))
            {
                return "the profile " + profile + " names no file (R364)";
            }
            return null;
        }

        private sealed class Plan
        {
            public List<JObject> Written;
            public List<JObject> NotWritten;
            public Dictionary<string, JObject> ByNumber;
            public HashSet<string> Missing;
            public Dictionary<string, List<string>> ClusterOf;
            public Dictionary<string, List<string>> PieceOf;
            public Dictionary<string, List<string>> RowOf;
            public Dictionary<string, List<string>> Ordered;
            public HashSet<string> Unordered;
            public Dictionary<string, List<JObject>> Entries;
            public Dictionary<string, Dictionary<string, string>> Rows;
        }

        public static LevelResult Build(LevelInputs inputs)
        {
            JObject plan = inputs.Plan;
            JObject profile = (JObject)inputs.Rules["levelProfiles"][inputs.Profile];
            JObject keyRules = profile["keyRules"] as JObject;
            JObject rules = (JObject)plan["summary"]["rules"];
            var made = new Plan
            {
                Written = (plan["plots"] as JArray ?? new JArray()).OfType<JObject>().ToList(),
                NotWritten = (plan["missing"] as JArray ?? new JArray()).OfType<JObject>().Where(item => item["written"]?.Type == JTokenType.Boolean && !(bool)item["written"]).ToList(),
                ClusterOf = ClustersOf(inputs.Clusters),
                PieceOf = PiecesOf(inputs.Levels),
                RowOf = RowsOf(inputs.Hands),
                Entries = Entries(inputs.Overrides, rules),
                Rows = new Dictionary<string, Dictionary<string, string>>(),
            };
            made.ByNumber = made.Written.ToDictionary(item => (string)item["plot"], item => item);
            made.Missing = new HashSet<string>(made.NotWritten.Select(item => (string)item["plot"]));
            made.Rows = RowsByNumber(inputs.PlotRows);
            OrderClusters(made);
            var result = new LevelResult();
            Func<string, string> nbhOf = number =>
            {
                Dictionary<string, string> row;
                string nbh;
                return made.Rows.TryGetValue(number, out row) && row.TryGetValue("nbhId", out nbh) && !string.IsNullOrEmpty(nbh) ? nbh : null;
            };
            List<string> numbers = made.Written.Select(item => (string)item["plot"]).Concat(made.NotWritten.Select(item => (string)item["plot"])).ToList();
            List<string> lacking = numbers.Where(number => nbhOf(number) == null).ToList();
            if (lacking.Count > 0)
            {
                result.Issues.Add(lacking.Count + " plots of the plan have no nbhId in plots.csv, so no level Excel lists them: " + string.Join(", ", lacking) + " (R364)");
            }
            foreach (string nbh in numbers.Select(nbhOf).Where(item => item != null).Distinct().OrderBy(item => item, StringComparer.Ordinal))
            {
                if (!NamePart.IsMatch(nbh))
                {
                    throw new InvalidDataException("the nbhId " + nbh + " of plots.csv names no file (R364)");
                }
                var mine = new HashSet<string>(numbers.Where(number => nbhOf(number) == nbh));
                result.Books.Add(Book(inputs, made, profile, keyRules, nbh, mine, result));
            }
            result.Counts["level workbooks"] = result.Books.Count;
            result.Counts["level rows written"] = result.Books.Sum(book => book.Sheets[0].Rows.Count(row => !((string)row[9]).StartsWith(LevelExcelText.NotWritten, StringComparison.Ordinal)));
            result.Counts["level rows not written"] = result.Books.Sum(book => book.Sheets[0].Rows.Count(row => ((string)row[9]).StartsWith(LevelExcelText.NotWritten, StringComparison.Ordinal)));
            result.Counts["level rows with no NBH"] = lacking.Count;
            result.Counts["cluster pieces"] = result.Books.Sum(book => book.Sheets[1].Rows.Count);
            result.Counts["overrides used"] = result.Books.Sum(book => book.Sheets[2].Rows.Count);
            return result;
        }

        private static LevelBook Book(LevelInputs inputs, Plan made, JObject profile, JObject keyRules, string nbh, HashSet<string> mine, LevelResult result)
        {
            var book = new LevelBook { Nbh = nbh, FileName = Fill(LevelExcelText.FileName, "nbh", nbh, "profile", inputs.Profile) };
            List<JObject> written = made.Written.Where(item => mine.Contains((string)item["plot"])).ToList();
            List<JObject> notWritten = made.NotWritten.Where(item => mine.Contains((string)item["plot"])).ToList();
            var levels = new LevelSheet { Name = LevelExcelText.LevelsSheet, TopLine = TopLine(inputs, made, profile, keyRules, nbh, mine, written), Header = LevelExcelText.LevelColumns, Formats = LevelExcelText.LevelFormats };
            var used = new LevelSheet { Name = LevelExcelText.OverridesSheet, Header = LevelExcelText.OverrideColumns, Formats = LevelExcelText.OverrideFormats };
            foreach (JObject plot in written)
            {
                string number = (string)plot["plot"];
                JObject values = plot["levels"] as JObject ?? new JObject();
                Func<string, object> level = key =>
                {
                    JToken value = values[key]?["value"];
                    if (value == null || value.Type == JTokenType.Null)
                    {
                        result.Issues.Add("plot " + number + " is written with no levels." + key + " (R364)");
                        return null;
                    }
                    return value.Type == JTokenType.Integer ? (object)(long)value : (double)value;
                };
                levels.Rows.Add(new[]
                {
                    number, (object)Text(plot["unit"]?["value"]), ClusterText(made, number), level("roadAtFrontM"), level("entry"), level("ffl"), level("ssl"), level("height"), level("steps"),
                    SourceText(result, used, made.Entries, plot, number),
                });
            }
            foreach (JObject item in notWritten)
            {
                string number = (string)item["plot"];
                IEnumerable<string> why = (item["missing"] as JArray ?? new JArray()).Select(found => Fill(LevelExcelText.MissingReason, "key", Text(found["key"]), "why", Text(found["why"])));
                levels.Rows.Add(new object[] { number, null, ClusterText(made, number), null, null, null, null, null, null, LevelExcelText.NotWritten + string.Join(LevelExcelText.ListJoin, why) });
            }
            book.Sheets.Add(levels);
            book.Sheets.Add(ClusterSheet(made, written, result));
            book.Sheets.Add(used);
            book.Sheets.Add(SettingsSheet(inputs));
            return book;
        }

        // The cluster of each plot, the step clusters of the build with two plots or more, by the plots the step writes
        private static Dictionary<string, List<string>> ClustersOf(JObject step)
        {
            var found = new Dictionary<string, List<string>>();
            foreach (JProperty item in (step?["plots"] as JObject ?? new JObject()).Properties())
            {
                List<string> plots = ((item.Value["written"] ?? item.Value["raw"])?["plots"] as JArray ?? new JArray()).Select(plot => (string)plot).ToList();
                if (plots.Count < 2)
                {
                    continue;
                }
                foreach (string plot in plots)
                {
                    found[plot] = plots;
                }
            }
            return found;
        }

        // The piece of each plot the step levels of the build write, the plots that share its FFL by the rule (R339, R340), one source
        private static Dictionary<string, List<string>> PiecesOf(JObject step)
        {
            var found = new Dictionary<string, List<string>>();
            foreach (JProperty item in (step?["plots"] as JObject ?? new JObject()).Properties())
            {
                JArray piece = (item.Value["written"] ?? item.Value["raw"])?["piece"] as JArray;
                if (piece != null)
                {
                    found[item.Name] = piece.Select(plot => (string)plot).ToList();
                }
            }
            return found;
        }

        // The row order of the cluster of each plot, the cluster of the step hands of the build
        private static Dictionary<string, List<string>> RowsOf(JObject step)
        {
            var found = new Dictionary<string, List<string>>();
            foreach (JProperty item in (step?["plots"] as JObject ?? new JObject()).Properties())
            {
                JArray row = (item.Value["written"] ?? item.Value["raw"])?["cluster"] as JArray;
                if (row != null)
                {
                    found[item.Name] = row.Select(plot => (string)plot).ToList();
                }
            }
            return found;
        }

        // Review 2 of PR 91: the row of plots.csv of each number, and for a number with no row of its own the zone and nbhId of the joined rows
        // whose parts hold it, none when they disagree, as area_key.key_of of Python takes one value of the numbers of a plot
        public static Dictionary<string, Dictionary<string, string>> RowsByNumber(IEnumerable<Dictionary<string, string>> rows)
        {
            var found = new Dictionary<string, Dictionary<string, string>>();
            var joined = new Dictionary<string, List<Dictionary<string, string>>>();
            foreach (Dictionary<string, string> row in rows)
            {
                string number;
                if (!row.TryGetValue("locationPlanNumber", out number) || string.IsNullOrEmpty(number))
                {
                    continue;
                }
                found[number] = row;
                if (number.Contains("|"))
                {
                    foreach (string part in number.Split('|').Where(part => part.Length > 0))
                    {
                        List<Dictionary<string, string>> same;
                        if (!joined.TryGetValue(part, out same))
                        {
                            joined[part] = same = new List<Dictionary<string, string>>();
                        }
                        same.Add(row);
                    }
                }
            }
            Func<Dictionary<string, string>, string, string> value = (row, key) => { string text; return row.TryGetValue(key, out text) ? text ?? "" : ""; };
            foreach (KeyValuePair<string, List<Dictionary<string, string>>> part in joined.Where(item => !found.ContainsKey(item.Key)).ToList())
            {
                var pairs = part.Value.Select(row => Tuple.Create(value(row, "zone"), value(row, "nbhId"))).Distinct().ToList();
                if (pairs.Count == 1)
                {
                    found[part.Key] = new Dictionary<string, string> { ["locationPlanNumber"] = part.Key, ["zone"] = pairs[0].Item1, ["nbhId"] = pairs[0].Item2 };
                }
            }
            return found;
        }

        // Review 2 of PR 91: each cluster in the row order the step hands give it (R123), on both sheets, the sorted order of the step clusters
        // when the hands give none
        private static void OrderClusters(Plan made)
        {
            made.Ordered = new Dictionary<string, List<string>>();
            made.Unordered = new HashSet<string>();
            foreach (List<string> cluster in made.ClusterOf.Values.Distinct())
            {
                List<string> row = cluster.Select(plot => { List<string> found; return made.RowOf.TryGetValue(plot, out found) ? found : null; })
                    .FirstOrDefault(found => found != null && found.Count == cluster.Count && !found.Except(cluster).Any());
                if (row == null)
                {
                    made.Unordered.Add(string.Join("|", cluster));
                    row = cluster;
                }
                foreach (string plot in cluster)
                {
                    made.Ordered[plot] = row;
                }
            }
        }

        private static string ClusterText(Plan made, string number)
        {
            List<string> row;
            return made.Ordered.TryGetValue(number, out row) ? string.Join(LevelExcelText.ClusterJoin, row) : LevelExcelText.NoCluster;
        }

        // The entries of the level fields of the override list a build of the plan takes, by its sources and models as the check rows take
        // them (CheckRows.ListEntries), by plot and field (R280)
        private static Dictionary<string, List<JObject>> Entries(JObject list, JObject rules)
        {
            var sources = new HashSet<string>((rules["overrideSource"] as JArray ?? new JArray()).Select(item => (string)item));
            var models = new HashSet<string>((rules["overrideModel"] as JArray ?? new JArray()).Select(item => (string)item));
            var found = new Dictionary<string, List<JObject>>();
            foreach (JObject entry in CheckRows.ListEntries(list, sources, models).Item1.Where(item => LevelFields.Contains(Text(item["field"]))))
            {
                string key = Text(entry["plot"]) + "|" + Text(entry["field"]);
                List<JObject> same;
                if (!found.TryGetValue(key, out same))
                {
                    found[key] = same = new List<JObject>();
                }
                same.Add(entry);
            }
            return found;
        }

        // Rule, or Override with each level field the plan applied and the reason of its entry, the order of the applied list of the plan
        private static string SourceText(LevelResult result, LevelSheet used, Dictionary<string, List<JObject>> entries, JObject plot, string number)
        {
            List<string> applied = (plot["overrides"]?["applied"] as JArray ?? new JArray()).Select(item => (string)item).Where(LevelFields.Contains).ToList();
            if (applied.Count == 0)
            {
                return LevelExcelText.Rule;
            }
            var parts = new List<string>();
            foreach (string field in applied)
            {
                List<JObject> same;
                if (!entries.TryGetValue(number + "|" + field, out same))
                {
                    result.Issues.Add("plot " + number + " field " + field + ": the plan applied it, the override list holds no entry of its sources (R364)");
                    parts.Add(Fill(LevelExcelText.OverrideReason, "field", field, "reason", LevelExcelText.NoEntry));
                    continue;
                }
                if (same.Count > 1)
                {
                    result.Issues.Add("plot " + number + " field " + field + ": the override list holds " + same.Count + " entries of its sources, the first is listed (R280, R364)");
                }
                JObject entry = same[0];
                parts.Add(Fill(LevelExcelText.OverrideReason, "field", field, "reason", Text(entry["reason"]) ?? ""));
                used.Rows.Add(new[] { number, field, GapOf(result, plot, number, field, entry["value"]), (object)Text(entry["reason"]), Text(entry["source"]), Text(entry["model"]) });
            }
            return LevelExcelText.Override + string.Join(LevelExcelText.ListJoin, parts);
        }

        // The gap of a level override to its rule as a number: gapToRuleM of a level in metres, the step count less the steps of the rule of
        // the plan (levelRule.steps) for steps, null with an issue when the entry or the plan gives none
        private static object GapOf(LevelResult result, JObject plot, string number, string field, JToken value)
        {
            if (field == "steps")
            {
                long? count = PlanOverrides.WholeNumber(value);
                long? rule = PlanOverrides.WholeNumber(plot["levelRule"]?["steps"]);
                if (count.HasValue && rule.HasValue)
                {
                    return count.Value - rule.Value;
                }
            }
            else
            {
                JToken gap = PlanOverrides.GapOf(value);
                if (gap != null && (gap.Type == JTokenType.Float || gap.Type == JTokenType.Integer))
                {
                    return (double)gap;
                }
            }
            result.Issues.Add("plot " + number + " field " + field + ": the entry or the rule of the plan gives no gap (R364)");
            return null;
        }

        private static string TopLine(LevelInputs inputs, Plan made, JObject profile, JObject keyRules, string nbh, HashSet<string> mine, List<JObject> written)
        {
            List<string> zones = mine.Select(number => { Dictionary<string, string> row; string zone; return made.Rows.TryGetValue(number, out row) && row.TryGetValue("zone", out zone) ? zone : null; })
                .Where(zone => !string.IsNullOrEmpty(zone)).Distinct().OrderBy(zone => zone, StringComparer.Ordinal).ToList();
            // Review 2 of PR 91: every front corner of the written plots of the NBH, one basis, by the surface each takes its level from, most
            // first, a tie in the order met, a corner with no surface counted apart, so the named and the apart add up to all
            var surfaces = new List<KeyValuePair<string, int>>();
            long none = 0, corners = 0;
            foreach (string name in written.SelectMany(plot => (plot["roadSurfaces"] as JArray ?? new JArray()).Select(Text)))
            {
                corners++;
                if (name == null || name == LevelExcelText.NoCluster)
                {
                    none++;
                    continue;
                }
                int at = surfaces.FindIndex(item => item.Key == name);
                if (at < 0)
                {
                    surfaces.Add(new KeyValuePair<string, int>(name, 1));
                }
                else
                {
                    surfaces[at] = new KeyValuePair<string, int>(name, surfaces[at].Value + 1);
                }
            }
            List<KeyValuePair<string, int>> ordered = surfaces.Select((item, index) => new { item, index }).OrderByDescending(pair => pair.item.Value).ThenBy(pair => pair.index).Select(pair => pair.item).ToList();
            JToken height = keyRules?["ffl"]?["rule"] ?? profile["height"];
            string rule = Text(height);
            string ffl;
            if (rule == null || !LevelExcelText.FflFormulas.TryGetValue(rule, out ffl))
            {
                throw new InvalidDataException("the level profile " + inputs.Profile + " names the height rule " + (rule ?? "null") + ", the level Excel names the FFL of " + string.Join(", ", LevelExcelText.FflFormulas.Keys) + " (R364)");
            }
            double ground = Fronts.Metres(inputs.Rules, inputs.RulesName, inputs.Rules["levels"]?["ground_ssl"], "levels.ground_ssl");
            string named = string.Join(LevelExcelText.ListJoin, ordered.Select(item => Fill(LevelExcelText.SurfaceCount, "name", item.Key, "n", Count(item.Value))));
            return Fill(LevelExcelText.TopLine, "nbh", nbh, "zones", zones.Count == 0 ? LevelExcelText.NoZone : string.Join(LevelExcelText.ZonesJoin, zones),
                "surfaces", named, "n", Count(corners), "none", none > 0 ? Fill(LevelExcelText.NoSurface, "n", Count(none)) : "",
                "ffl", ffl, "sign", ground <= 0 ? "-" : "+", "ssl", Number(Math.Abs(ground)));
        }

        // One row per piece of the build of a cluster that holds a written plot of the NBH, in the order the plan writes the plots: the pieces the
        // step levels give the plots of the cluster in its order, the FFL of the rule of the piece, its unit sequence and the split its pieces make
        private static LevelSheet ClusterSheet(Plan made, List<JObject> written, LevelResult result)
        {
            var sheet = new LevelSheet { Name = LevelExcelText.ClustersSheet, Header = LevelExcelText.ClusterColumns, Formats = LevelExcelText.ClusterFormats };
            var listed = new HashSet<string>();
            foreach (string number in written.Select(plot => (string)plot["plot"]))
            {
                List<string> cluster;
                if (!made.ClusterOf.TryGetValue(number, out cluster) || !listed.Add(string.Join("|", cluster)))
                {
                    continue;
                }
                // The row order of the cluster, which the name, the split and the pieces run along
                List<string> row = made.Ordered[number];
                string name = string.Join(LevelExcelText.ClusterJoin, row);
                if (made.Unordered.Contains(string.Join("|", cluster)))
                {
                    result.Issues.Add("cluster " + name + ": the step hands give no row order, the sorted order is taken (R364)");
                }
                string sequence = string.Join(" ", row.Select(plot => made.ByNumber.ContainsKey(plot) ? Text(made.ByNumber[plot]["unit"]?["value"])
                    : made.Missing.Contains(plot) ? LevelExcelText.UnitNotWritten : LevelExcelText.OutsideScope));
                var pieces = new List<List<string>>();
                foreach (string plot in row)
                {
                    List<string> piece;
                    if (made.PieceOf.TryGetValue(plot, out piece) && !pieces.Any(other => other.SequenceEqual(piece)))
                    {
                        pieces.Add(piece);
                    }
                }
                if (made.ByNumber.Keys.Any(plot => cluster.Contains(plot) && !made.PieceOf.ContainsKey(plot)))
                {
                    result.Issues.Add("cluster " + name + ": a written plot has no piece in the step levels (R364)");
                }
                // A cut before each piece and after it, so a piece of plots outside the scope, which the step does not hold, still cuts
                List<int> cuts = pieces.SelectMany(piece => new[] { row.IndexOf(piece[0]), row.IndexOf(piece[piece.Count - 1]) + 1 }).Where(at => at > 0 && at < row.Count).Distinct().OrderBy(at => at).ToList();
                string split = cuts.Count == 0 ? LevelExcelText.NoSplit : cluster.Count % 2 == 0 && cuts.SequenceEqual(new[] { cluster.Count / 2 }) ? LevelExcelText.MiddleSplit
                    : Fill(LevelExcelText.WallsSplit, "n", string.Join(",", cuts));
                foreach (List<string> piece in pieces)
                {
                    List<double> rules = piece.Where(made.ByNumber.ContainsKey).Select(plot => made.ByNumber[plot]["levelRule"]?["ffl"]).Where(value => value != null && value.Type != JTokenType.Null).Select(value => (double)value).Distinct().ToList();
                    // A piece of no written plot has no FFL to list, so it gives no row
                    if (rules.Count == 0)
                    {
                        continue;
                    }
                    if (rules.Count > 1)
                    {
                        result.Issues.Add("piece " + string.Join(LevelExcelText.ClusterJoin, piece) + ": its plots hold " + rules.Count + " FFLs of the rule, the first is listed (R364)");
                    }
                    sheet.Rows.Add(new object[] { name, string.Join(LevelExcelText.ClusterJoin, piece), sequence, split, rules[0] });
                }
            }
            return sheet;
        }

        private static LevelSheet SettingsSheet(LevelInputs inputs)
        {
            var sheet = new LevelSheet { Name = LevelExcelText.SettingsSheet, Header = LevelExcelText.SettingColumns, Formats = new string[2] };
            JToken pass = inputs.Config["placerCheck"]?["level"]?["passMm"];
            var rows = new[]
            {
                new[] { LevelExcelText.RunId, inputs.RunId },
                new[] { LevelExcelText.Profile, inputs.Profile },
                new[] { LevelExcelText.RulesSha, inputs.RulesSha256 },
                new[] { LevelExcelText.OverridesSha, inputs.OverridesSha256 },
                new[] { LevelExcelText.RoadOrder, string.Join(LevelExcelText.ListJoin, inputs.RoadOrder) },
                new[] { LevelExcelText.PassLimit, pass == null || pass.Type == JTokenType.Null ? LevelExcelText.NoPassLimit : Fill(LevelExcelText.PassLimitMm, "n", Number((double)pass)) },
                new[] { LevelExcelText.RunTime, inputs.Now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) },
                new[] { LevelExcelText.EditableColumns, string.Join(LevelExcelText.ListJoin, LevelExcelText.LevelColumns.Where(LevelExcelText.Editable.Contains)) },
                new[] { LevelExcelText.PlanBuildId, Text((inputs.Plan["run"] as JObject)?["runId"]) ?? LevelExcelText.NoBuildId },
                new[] { LevelExcelText.PlanSha, inputs.PlanSha256 },
            };
            foreach (string[] row in rows)
            {
                sheet.Rows.Add(row.Cast<object>().ToArray());
            }
            return sheet;
        }
    }
}
