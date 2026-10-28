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
        public static readonly string[] Sheets = { LevelsSheet, ClustersSheet, OverridesSheet, SettingsSheet };

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

        public const string TopLine = "NBH <nbh>, <zones>, road surface <surfaces> of <n> front corners, formulas: El @ Entry = the road at the two front corners + the slope "
            + "of the unit x the ramp depth (R278, R339), FFL = <ffl> (R340), SSL = FFL <sign> <ssl> m (levels.ground_ssl), Height = FFL - El @ Entry, "
            + "Steps = one for the base and one per riser over it, half a riser no step (R340)";

        public const string SurfaceCount = "<name> on <n>";
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
        public const string UnitNotWritten = "not written";
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

    public sealed class LevelBook
    {
        public string FileName { get; set; }
        public List<LevelSheet> Sheets { get; } = new List<LevelSheet>();
        public List<string> Issues { get; } = new List<string>();
        public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
    }

    // What the level Excel is made from: the plan as written, its rules profile, config, the override list, the step clusters and the rows of
    // plots.csv of the build, and the run that writes it
    public sealed class LevelInputs
    {
        public JObject Plan { get; set; }
        public string PlanSha256 { get; set; }
        public JObject Rules { get; set; }
        public string RulesName { get; set; }
        public string RulesSha256 { get; set; }
        public JObject Config { get; set; }
        public JObject Overrides { get; set; }
        public string OverridesSha256 { get; set; }
        public JObject Clusters { get; set; }
        public List<Dictionary<string, string>> PlotRows { get; set; }
        public string RunId { get; set; }
        public string Profile { get; set; }
        public DateTime Now { get; set; }
    }

    // R364. The sheets of the level Excel from the plan as written, no level recomputed: each value of the levels of a plot is the value the
    // plan holds, the cluster of a plot is the cluster of the step clusters of the build that holds it, a piece of the sheet Clusters is a run
    // of plots of one cluster that share one FFL as the plan writes it, and the reason of an override is the one of its entry in the list
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

        // Null when the plan, the rules and the request agree on the level profile and the plan names its NBH, else why the request is refused
        public static string Refusal(JObject plan, JObject rules, string profile)
        {
            string built = (string)(plan?["summary"]?["rules"] as JObject)?["levelProfile"];
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
            string nbh = plan["neighbourhood"]?.Type == JTokenType.String ? (string)plan["neighbourhood"] : null;
            if (nbh == null || !NamePart.IsMatch(nbh) || !NamePart.IsMatch(profile))
            {
                return "the neighbourhood " + (nbh ?? "null") + " of the plan or the profile " + profile + " names no file (R364)";
            }
            return null;
        }

        public static LevelBook Build(LevelInputs inputs)
        {
            JObject plan = inputs.Plan;
            JObject profile = (JObject)inputs.Rules["levelProfiles"][inputs.Profile];
            JObject keyRules = profile["keyRules"] as JObject;
            string nbh = (string)plan["neighbourhood"];
            var book = new LevelBook { FileName = Fill(LevelExcelText.FileName, "nbh", nbh, "profile", inputs.Profile) };

            List<JObject> written = (plan["plots"] as JArray ?? new JArray()).OfType<JObject>().ToList();
            List<JObject> notWritten = (plan["missing"] as JArray ?? new JArray()).OfType<JObject>().Where(item => item["written"]?.Type == JTokenType.Boolean && !(bool)item["written"]).ToList();
            Dictionary<string, List<string>> clusterOf = ClustersOf(inputs.Clusters);
            Dictionary<string, List<JObject>> entries = Entries(inputs.Overrides, (JObject)plan["summary"]["rules"]);

            var levels = new LevelSheet { Name = LevelExcelText.LevelsSheet, TopLine = TopLine(inputs, plan, profile, keyRules, written, notWritten), Header = LevelExcelText.LevelColumns, Formats = LevelExcelText.LevelFormats };
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
                        book.Issues.Add("plot " + number + " is written with no levels." + key + " (R364)");
                        return null;
                    }
                    return value.Type == JTokenType.Integer ? (object)(long)value : (double)value;
                };
                levels.Rows.Add(new[]
                {
                    number, (object)(string)plot["unit"]?["value"], ClusterText(clusterOf, number), level("roadAtFrontM"), level("entry"), level("ffl"), level("ssl"), level("height"), level("steps"),
                    SourceText(book, used, entries, plot, number),
                });
            }
            foreach (JObject item in notWritten)
            {
                string number = (string)item["plot"];
                IEnumerable<string> why = (item["missing"] as JArray ?? new JArray()).Select(found => Fill(LevelExcelText.MissingReason, "key", (string)found["key"], "why", (string)found["why"]));
                levels.Rows.Add(new object[] { number, null, ClusterText(clusterOf, number), null, null, null, null, null, null, LevelExcelText.NotWritten + string.Join(LevelExcelText.ListJoin, why) });
            }

            book.Sheets.Add(levels);
            book.Sheets.Add(ClusterSheet(written, notWritten, clusterOf, keyRules));
            book.Sheets.Add(used);
            book.Sheets.Add(SettingsSheet(inputs, profile, plan));
            book.Counts["level rows written"] = written.Count;
            book.Counts["level rows not written"] = notWritten.Count;
            book.Counts["cluster pieces"] = book.Sheets[1].Rows.Count;
            book.Counts["overrides used"] = used.Rows.Count;
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

        private static string ClusterText(Dictionary<string, List<string>> clusterOf, string number)
        {
            List<string> cluster;
            return clusterOf.TryGetValue(number, out cluster) ? string.Join(LevelExcelText.ClusterJoin, cluster) : LevelExcelText.NoCluster;
        }

        // The entries of the level fields of the override list a build of the plan takes, its sources and its models, by plot and field (R280)
        private static Dictionary<string, List<JObject>> Entries(JObject list, JObject rules)
        {
            var sources = new HashSet<string>((rules["overrideSource"] as JArray ?? new JArray()).Select(item => (string)item));
            var models = new HashSet<string>((rules["overrideModel"] as JArray ?? new JArray()).Select(item => (string)item));
            var found = new Dictionary<string, List<JObject>>();
            foreach (JObject entry in (list?["overrides"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string field = (string)entry["field"];
                if (!LevelFields.Contains(field) || !sources.Contains((string)entry["source"]) || models.Count > 0 && !models.Contains((string)entry["model"]))
                {
                    continue;
                }
                string key = (string)entry["plot"] + "|" + field;
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
        private static string SourceText(LevelBook book, LevelSheet used, Dictionary<string, List<JObject>> entries, JObject plot, string number)
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
                    book.Issues.Add("plot " + number + " field " + field + ": the plan applied it, the override list holds no entry of its sources (R364)");
                    parts.Add(Fill(LevelExcelText.OverrideReason, "field", field, "reason", LevelExcelText.NoEntry));
                    continue;
                }
                if (same.Count > 1)
                {
                    book.Issues.Add("plot " + number + " field " + field + ": the override list holds " + same.Count + " entries of its sources, the first is listed (R280, R364)");
                }
                JObject entry = same[0];
                parts.Add(Fill(LevelExcelText.OverrideReason, "field", field, "reason", (string)entry["reason"] ?? ""));
                used.Rows.Add(new[] { number, field, GapOf(entry["value"]), (object)(string)entry["reason"], (string)entry["source"], (string)entry["model"] });
            }
            return LevelExcelText.Override + string.Join(LevelExcelText.ListJoin, parts);
        }

        // The gap of a level override to its rule, a step count as the number it is, any other value as json.dumps writes it
        private static object GapOf(JToken value)
        {
            JToken gap = value is JObject item ? item["gapToRuleM"] : value;
            if (gap != null && (gap.Type == JTokenType.Float || gap.Type == JTokenType.Integer))
            {
                return gap.Type == JTokenType.Integer ? (object)(long)gap : (double)gap;
            }
            return value == null ? null : PlanOverrides.Dumps(value);
        }

        private static string TopLine(LevelInputs inputs, JObject plan, JObject profile, JObject keyRules, List<JObject> written, List<JObject> notWritten)
        {
            var zoneOf = new Dictionary<string, string>();
            foreach (Dictionary<string, string> row in inputs.PlotRows)
            {
                string number, zone;
                if (row.TryGetValue("locationPlanNumber", out number) && row.TryGetValue("zone", out zone) && !string.IsNullOrEmpty(number))
                {
                    zoneOf[number] = zone;
                }
            }
            List<string> zones = written.Select(plot => (string)plot["plot"]).Concat(notWritten.Select(item => (string)item["plot"]))
                .Select(number => { string zone; return zoneOf.TryGetValue(number, out zone) ? zone : null; }).Where(zone => !string.IsNullOrEmpty(zone)).Distinct().OrderBy(zone => zone, StringComparer.Ordinal).ToList();
            JObject surfaces = plan["summary"]?["roadSurfacesOfFrontCorners"] as JObject ?? new JObject();
            long corners = surfaces.Properties().Sum(item => (long)item.Value);
            JToken height = keyRules?["ffl"]?["rule"] ?? profile["height"];
            string rule = height?.Type == JTokenType.String ? (string)height : null;
            string ffl;
            if (rule == null || !LevelExcelText.FflFormulas.TryGetValue(rule, out ffl))
            {
                throw new InvalidDataException("the level profile " + inputs.Profile + " names the height rule " + (rule ?? "null") + ", the level Excel names the FFL of " + string.Join(", ", LevelExcelText.FflFormulas.Keys) + " (R364)");
            }
            double ground = Fronts.Metres(inputs.Rules, inputs.RulesName, inputs.Rules["levels"]?["ground_ssl"], "levels.ground_ssl");
            return Fill(LevelExcelText.TopLine, "nbh", (string)plan["neighbourhood"], "zones", zones.Count == 0 ? LevelExcelText.NoZone : string.Join(LevelExcelText.ZonesJoin, zones),
                "surfaces", string.Join(LevelExcelText.ListJoin, surfaces.Properties().Select(item => Fill(LevelExcelText.SurfaceCount, "name", item.Name, "n", ((long)item.Value).ToString(CultureInfo.InvariantCulture)))),
                "n", corners.ToString(CultureInfo.InvariantCulture), "ffl", ffl, "sign", ground <= 0 ? "-" : "+", "ssl", Number(Math.Abs(ground)));
        }

        // One row per piece of a cluster that holds a written plot, in the order the plan writes the plots: the written plots of the cluster in
        // its order, a piece each run of them with one FFL as written, its unit sequence and the split keyRules.levelSplit gives that sequence
        private static LevelSheet ClusterSheet(List<JObject> written, List<JObject> notWritten, Dictionary<string, List<string>> clusterOf, JObject keyRules)
        {
            var sheet = new LevelSheet { Name = LevelExcelText.ClustersSheet, Header = LevelExcelText.ClusterColumns, Formats = LevelExcelText.ClusterFormats };
            Dictionary<string, JObject> byNumber = written.ToDictionary(plot => (string)plot["plot"], plot => plot);
            JObject split = keyRules?["levelSplit"] as JObject;
            JObject table = Text(keyRules?["ffl"]?["rule"]) == "oneFflPerPieceAtTheHighestEntry" && Text(split?["rule"]) == "units" ? split["table"] as JObject : null;
            var listed = new HashSet<string>();
            foreach (string number in written.Select(plot => (string)plot["plot"]))
            {
                List<string> cluster;
                if (!clusterOf.TryGetValue(number, out cluster) || !listed.Add(string.Join("|", cluster)))
                {
                    continue;
                }
                string name = string.Join(LevelExcelText.ClusterJoin, cluster);
                string sequence = string.Join(" ", cluster.Select(plot => byNumber.ContainsKey(plot) ? (string)byNumber[plot]["unit"]?["value"] : LevelExcelText.UnitNotWritten));
                string splitText = Text(table?[sequence]) ?? LevelExcelText.NoSplit;
                var piece = new List<string>();
                double? top = null;
                foreach (string plot in cluster.Where(byNumber.ContainsKey))
                {
                    JToken value = byNumber[plot]["levels"]?["ffl"]?["value"];
                    double? ffl = value == null || value.Type == JTokenType.Null ? (double?)null : (double)value;
                    if (piece.Count > 0 && !Nullable.Equals(ffl, top))
                    {
                        sheet.Rows.Add(new object[] { name, string.Join(LevelExcelText.ClusterJoin, piece), sequence, splitText, top });
                        piece.Clear();
                    }
                    piece.Add(plot);
                    top = ffl;
                }
                if (piece.Count > 0)
                {
                    sheet.Rows.Add(new object[] { name, string.Join(LevelExcelText.ClusterJoin, piece), sequence, splitText, top });
                }
            }
            return sheet;
        }

        private static LevelSheet SettingsSheet(LevelInputs inputs, JObject profile, JObject plan)
        {
            var sheet = new LevelSheet { Name = LevelExcelText.SettingsSheet, Header = LevelExcelText.SettingColumns, Formats = new string[2] };
            string surface = Text(profile["frontRoad"]?["surface"]);
            IEnumerable<string> order = surface == "roadLevel" ? (inputs.Config["siteBuild"]?["roadSurfaces"] as JArray ?? new JArray()).Select(item => (string)item) : new[] { surface };
            JToken pass = inputs.Config["placerCheck"]?["level"]?["passMm"];
            string build = plan["run"]?["runId"]?.Type == JTokenType.String ? (string)plan["run"]["runId"] : inputs.RunId;
            var rows = new[]
            {
                new[] { LevelExcelText.RunId, inputs.RunId },
                new[] { LevelExcelText.Profile, inputs.Profile },
                new[] { LevelExcelText.RulesSha, inputs.RulesSha256 },
                new[] { LevelExcelText.OverridesSha, inputs.OverridesSha256 },
                new[] { LevelExcelText.RoadOrder, string.Join(LevelExcelText.ListJoin, order) },
                new[] { LevelExcelText.PassLimit, pass == null || pass.Type == JTokenType.Null ? LevelExcelText.NoPassLimit : Fill(LevelExcelText.PassLimitMm, "n", Number((double)pass)) },
                new[] { LevelExcelText.RunTime, inputs.Now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) },
                new[] { LevelExcelText.EditableColumns, string.Join(LevelExcelText.ListJoin, LevelExcelText.LevelColumns.Where(LevelExcelText.Editable.Contains)) },
                new[] { LevelExcelText.PlanBuildId, build },
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
