using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Scan
{
    // R435. One source of a value as scan.json holds it: Raw is the source string of the row and the key of a pick
    public sealed class ScanSource
    {
        public string Raw { get; set; }
        public string Shown { get; set; }
        public int Score { get; set; }

        public override string ToString()
        {
            return Shown;
        }
    }

    // R435. One line of "Where each value comes from". Selected is null until a proposal or a pick names a source
    public sealed class ScanValueRow
    {
        public string Kind { get; set; }
        public string Label { get; set; }
        public List<ScanSource> Sources { get; } = new List<ScanSource>();
        public string Proposed { get; set; }
        public string Selected { get; set; }
        public int Plots { get; set; }

        public bool IsPick { get { return Selected == null; } }
        public string Status { get { return IsPick ? ScanViewText.Pick : ScanViewText.Clear; } }
        public string Covers { get { return CoversOf(Selected); } }
        public string Checks { get { return ChecksOf(Selected); } }
        public int Best { get { return Sources.Count == 0 ? 0 : Sources.Max(source => source.Score); } }

        // The source counted when none is selected is the best of the kind, the first of the highest score
        private ScanSource Counted(string raw)
        {
            return Sources.FirstOrDefault(source => source.Raw == raw) ?? Sources.OrderByDescending(source => source.Score).FirstOrDefault() ?? new ScanSource();
        }

        public string CoversOf(string raw)
        {
            int score = Counted(raw).Score;
            return Kind == ScanView.VillaTypeKind
                ? ScanView.Fill(ScanViewText.CoversOf, ScanView.Count(score), ScanView.Count(Plots))
                : ScanView.Fill(ScanViewText.CoversPlots, ScanView.Count(score));
        }

        public string ChecksOf(string raw)
        {
            return ScanView.Fill(ScanViewText.ChecksFiles, ScanView.Count(Math.Max(Sources.Count - 1, 0)));
        }
    }

    // R435. What the window says of a value that is Pick
    public sealed class ScanUnclear
    {
        public string Kind { get; set; }
        public string Title { get; set; }
        public string Line { get; set; }
        public string ToDo { get; set; }
    }

    public sealed class ScanFileRow
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Found { get; set; }
        public string Status { get; set; }
        public bool IsRead { get { return Status == ScanViewText.Read; } }
    }

    public sealed class ScanConflictRow
    {
        public string Plot { get; set; }
        public string Field { get; set; }
        public string Value { get; set; }
        public string SourceA { get; set; }
        public string SourceB { get; set; }
        public string ChoiceA { get; set; }
        public string ChoiceB { get; set; }
        public string Selected { get; set; }

        // True once the user, or the settings, picked a side. A conflict not picked is Open
        public bool Picked { get; set; }
        public bool Open { get { return !Picked; } }
        public string Key { get { return ScanView.ConflictKey(Plot, Field, SourceB); } }
        public string SelectedChoice { get { return Selected == SourceB ? ChoiceB : ChoiceA; } }
    }

    public sealed class ScanViewData
    {
        public List<ScanValueRow> Values { get; } = new List<ScanValueRow>();
        public List<ScanFileRow> Files { get; } = new List<ScanFileRow>();
        public List<ScanConflictRow> Conflicts { get; } = new List<ScanConflictRow>();
        public int FilesRead { get; set; }
        public int PlotsFound { get; set; }
        public int ConflictCount { get; set; }

        public IEnumerable<ScanUnclear> Unclear { get { return Values.Where(value => value.IsPick).Select(ScanView.UnclearOf); } }

        // Whether the window has to open: a value is Pick or a conflict is Open
        public bool NeedsWindow { get { return Values.Any(value => value.IsPick) || Conflicts.Any(conflict => conflict.Open); } }
    }

    // R435, docs/design/17-scanner.md scan.json. The rows of scan.json as the window What the tool read shows them
    public static class ScanView
    {
        public const string VillaTypeKind = "villaType";
        public const string PlotOutlineKind = "plotOutline";
        public const string ConfigRole = "config";

        private static readonly string[] ValueKinds = { PlotOutlineKind, "plotNumber", "villaBlock", VillaTypeKind };
        private static readonly string[] ValueLabels = { ScanViewText.PlotOutline, ScanViewText.PlotNumber, ScanViewText.VillaBlock, ScanViewText.VillaType };
        private static readonly Regex Slot = new Regex(@"<[a-z]+>");
        private static readonly Regex CadFound = new Regex(@"^(\d+) closed outlines, (\d+) texts, (\d+) blocks");

        public static ScanViewData Read(string scanJsonText, ProjectSettings settings)
        {
            JObject root = JObject.Parse(scanJsonText);
            settings = settings ?? new ProjectSettings();
            var data = new ScanViewData();
            List<JObject> rows = Objects(root["rows"]);
            List<JObject> inputs = Objects(root["inputs"]);
            List<JObject> checks = Objects(root["checks"]);
            List<JObject> conflicts = Objects(root["conflicts"]);
            int plots = (root["plots"] as JArray)?.Count ?? 0;

            for (int at = 0; at < ValueKinds.Length; at++)
            {
                List<JObject> ofKind = rows.Where(row => Text(row["kind"]) == ValueKinds[at]).ToList();
                // Breaker of SCAN-UI: a plot outline or villa type the scan found no source for is a value to pick, so the gate opens
                if (ofKind.Count == 0 && ValueKinds[at] != PlotOutlineKind && ValueKinds[at] != VillaTypeKind)
                {
                    continue;
                }
                var value = new ScanValueRow { Kind = ValueKinds[at], Label = ValueLabels[at], Plots = plots };
                foreach (JObject row in ofKind)
                {
                    string raw = Text(row["source"]);
                    value.Sources.Add(new ScanSource { Raw = raw, Shown = Show(ValueKinds[at], raw), Score = (int?)row["score"] ?? 0 });
                }
                JObject proposed = ofKind.FirstOrDefault(row => Text(row["proposedUse"]) != "" && Text(row["proposedUse"]) != "check");
                value.Proposed = proposed == null ? null : Text(proposed["source"]);
                string picked = null;
                settings.ScanPicks?.TryGetValue(ValueKinds[at], out picked);
                // Breaker of SCAN-UI: a saved pick that names no source of this scan, a file renamed by a new revision, asks again
                value.Selected = picked == null ? value.Proposed : value.Sources.Any(source => source.Raw == picked) ? picked : null;
                data.Values.Add(value);
            }

            // Review of SCAN-UI: the settings file is an input of the scan as role config, no file the user gave, so it has no row
            List<JObject> given = inputs.Where(input => Text(input["role"]) != ConfigRole).ToList();
            foreach (JObject input in given)
            {
                string name = Text(input["name"]);
                string role = Text(input["role"]);
                string kind = role.StartsWith("scanCad", StringComparison.Ordinal) ? ScanViewText.Cad
                    : role.StartsWith("scanExcel", StringComparison.Ordinal) ? ScanViewText.Excel
                    : role.StartsWith("scanPdf", StringComparison.Ordinal) ? ScanViewText.Pdf
                    : role.StartsWith("prototype", StringComparison.Ordinal) ? ScanViewText.Prototype : role;
                bool missed = checks.Any(check => Text(check["what"]) == "notRead" && FileOf(Text(check["source"])) == name);
                var file = new ScanFileRow { Name = name, Kind = kind, Status = missed ? ScanViewText.NotRead : ScanViewText.Read };
                if (kind == ScanViewText.Cad)
                {
                    List<JObject> layers = rows.Where(row => Text(row["kind"]) == "cadLayer" && FileOf(Text(row["source"])) == name).ToList();
                    int outlines = 0, texts = 0, blocks = 0;
                    foreach (JObject layer in layers)
                    {
                        Match found = CadFound.Match(Text(layer["found"]));
                        if (found.Success)
                        {
                            outlines += int.Parse(found.Groups[1].Value, CultureInfo.InvariantCulture);
                            texts += int.Parse(found.Groups[2].Value, CultureInfo.InvariantCulture);
                            blocks += int.Parse(found.Groups[3].Value, CultureInfo.InvariantCulture);
                        }
                    }
                    file.Found = Fill(ScanViewText.FoundCad, Count(layers.Count), Count(outlines), Count(texts), Count(blocks));
                }
                else
                {
                    int sources = rows.Count(row => Text(row["kind"]) == VillaTypeKind && FileOf(Text(row["source"])) == name);
                    file.Found = Fill(ScanViewText.FoundSources, Count(sources));
                }
                data.Files.Add(file);
            }

            foreach (JObject item in conflicts)
            {
                string field = Text(item["field"]);
                string a = Text(item["sourceA"]);
                string b = Text(item["sourceB"]);
                var conflict = new ScanConflictRow
                {
                    Plot = Text(item["plot"]),
                    Field = field,
                    Value = FieldLabel(field),
                    SourceA = a,
                    SourceB = b,
                    ChoiceA = Fill(ScanViewText.ChoiceFrom, Text(item["valueA"]), FileOf(a)),
                    ChoiceB = Fill(ScanViewText.ChoiceFrom, Text(item["valueB"]), FileOf(b)),
                };
                string saved = null;
                settings.ConflictPicks?.TryGetValue(conflict.Key, out saved);
                conflict.Picked = saved != null && (saved == a || saved == b);
                conflict.Selected = conflict.Picked ? saved : a;
                data.Conflicts.Add(conflict);
            }

            data.FilesRead = given.Count;
            data.PlotsFound = plots;
            data.ConflictCount = conflicts.Count;
            return data;
        }

        public static ScanUnclear UnclearOf(ScanValueRow value)
        {
            bool villa = value.Kind == VillaTypeKind;
            return new ScanUnclear
            {
                Kind = value.Kind,
                Title = Fill(ScanViewText.NotClear, value.Label),
                Line = Fill(villa ? ScanViewText.NoPrototypeMatch : ScanViewText.NoSourceCovers, Count(value.Best), Count(value.Plots)),
                ToDo = villa ? ScanViewText.DoVillaType : Fill(ScanViewText.DoPick, value.Label),
            };
        }

        // Breaker of SCAN-UI: the scanner writes one conflict per other source of a plot and field, so the other source is in the key
        public static string ConflictKey(string plot, string field, string sourceB)
        {
            return plot + "|" + field + "|" + sourceB;
        }

        public static string FileOf(string source)
        {
            int bar = source.IndexOf('|');
            return bar < 0 ? source : source.Substring(0, bar);
        }

        // "<file>|<layer>" and the villa type shapes of docs/design/17-scanner.md. A layer may hold a bar itself, so only the first bar parts
        // the file and the shift and column are taken from the end
        public static string Show(string kind, string raw)
        {
            int bar = raw.IndexOf('|');
            if (bar < 0)
            {
                return raw;
            }
            string file = raw.Substring(0, bar);
            string rest = raw.Substring(bar + 1);
            if (kind != VillaTypeKind)
            {
                return Fill(ScanViewText.OnLayer, file, rest);
            }
            if (rest.StartsWith("block|", StringComparison.Ordinal))
            {
                return Fill(ScanViewText.BlocksOn, file, rest.Substring(6));
            }
            if (rest.StartsWith("text|", StringComparison.Ordinal))
            {
                return Fill(ScanViewText.TextsOn, file, rest.Substring(5));
            }
            int shift = rest.LastIndexOf("|shift ", StringComparison.Ordinal);
            if (shift < 0)
            {
                return raw;
            }
            string place = rest.Substring(0, shift);
            if (place.StartsWith("page ", StringComparison.Ordinal))
            {
                return Fill(ScanViewText.OnPage, file, place.Substring(5));
            }
            int column = place.LastIndexOf('|');
            return column < 0 ? raw : Fill(ScanViewText.OnSheet, file, place.Substring(0, column), place.Substring(column + 1));
        }

        public static string FieldLabel(string field)
        {
            switch (field)
            {
                case "number": return ScanViewText.PlotNumber;
                case "location": return ScanViewText.Location;
                case "label": return ScanViewText.Label;
                case "villa type": return ScanViewText.VillaType;
                default: return field;
            }
        }

        public static string Count(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // The slots of a text of the label list filled in their order
        public static string Fill(string template, params string[] values)
        {
            int next = 0;
            return Slot.Replace(template, match => next < values.Length ? values[next++] : match.Value);
        }

        private static List<JObject> Objects(JToken token)
        {
            return (token as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
        }

        private static string Text(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.Type == JTokenType.String ? (string)token : token.ToString();
        }
    }
}
