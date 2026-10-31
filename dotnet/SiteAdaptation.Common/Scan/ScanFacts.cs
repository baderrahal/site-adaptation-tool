using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Scan
{
    // One column of a sheet the scan read villa types from, Raw the villaType source a pick names
    public sealed class ScanColumn
    {
        public string Column { get; set; }

        public string Raw { get; set; }
    }

    // One sheet of an Excel file the scan read villa types from, shown as the file and the sheet
    public sealed class ScanSheetChoice
    {
        public string Shown { get; set; }

        public List<ScanColumn> Columns { get; } = new List<ScanColumn>();
    }

    public sealed class ScanFactsData
    {
        public int Layers { get; set; }
        public int Plots { get; set; }
        public int VillaTypes { get; set; }
        public string PlotLayer { get; set; }
        public List<ScanSource> LayerChoices { get; } = new List<ScanSource>();
        public string LayerProposed { get; set; }
        public List<ScanSheetChoice> Sheets { get; } = new List<ScanSheetChoice>();
        public string VillaTypeProposed { get; set; }
    }

    // R440 Drawing facts from the scan. Input: the text of scan.json (R427). Output: the facts the block "What the tool read from the
    // drawing" of New project shows: the layers of the plot drawing (its cadLayer rows), the plots and the distinct villa types of
    // plots, the layer of the proposed plot outline, the plot outline sources for the layer dropdown, and the Excel villa type sources
    // by sheet and column for the sheet and column dropdowns. The scan reads no survey point, so none is given
    public static class ScanFacts
    {
        public static ScanFactsData Of(string scanJsonText)
        {
            JObject scan = JObject.Parse(scanJsonText);
            ScanViewData view = ScanView.Read(scanJsonText, new ProjectSettings());
            var facts = new ScanFactsData { Plots = view.PlotsFound };
            ScanValueRow outline = view.Values.FirstOrDefault(value => value.Kind == ScanView.PlotOutlineKind);
            if (outline != null && outline.Sources.Count > 0)
            {
                facts.LayerChoices.AddRange(outline.Sources);
                facts.LayerProposed = outline.Proposed;
                string raw = outline.Proposed ?? outline.Sources.OrderByDescending(source => source.Score).First().Raw;
                int bar = raw.IndexOf('|');
                facts.PlotLayer = bar < 0 ? raw : raw.Substring(bar + 1);
                string file = bar < 0 ? raw : raw.Substring(0, bar);
                facts.Layers = Rows(scan).Count(row => (string)row["kind"] == "cadLayer" && ((string)row["source"] ?? "").StartsWith(file + "|", StringComparison.Ordinal));
            }
            ScanValueRow villa = view.Values.FirstOrDefault(value => value.Kind == ScanView.VillaTypeKind);
            // Review of SCAN-UI: villaType of a plot is an object of source to code. The codes of the proposed source count, else
            // those of every source
            facts.VillaTypes = (scan["plots"] as JArray ?? new JArray()).OfType<JObject>()
                .SelectMany(plot => (plot["villaType"] as JObject ?? new JObject()).Properties()
                    .Where(source => villa?.Proposed == null || source.Name == villa.Proposed)
                    .Select(source => source.Value.Type == JTokenType.String ? ((string)source.Value).Trim() : ""))
                .Where(code => code.Length > 0).Distinct(StringComparer.Ordinal).Count();
            if (villa != null)
            {
                facts.VillaTypeProposed = villa.Proposed;
                foreach (ScanSource source in villa.Sources)
                {
                    if (!Excel(source.Raw, out string file, out string sheet, out string column))
                    {
                        continue;
                    }
                    string shown = file + ", " + sheet;
                    ScanSheetChoice choice = facts.Sheets.FirstOrDefault(item => item.Shown == shown);
                    if (choice == null)
                    {
                        choice = new ScanSheetChoice { Shown = shown };
                        facts.Sheets.Add(choice);
                    }
                    choice.Columns.Add(new ScanColumn { Column = column, Raw = source.Raw });
                }
            }
            return facts;
        }

        // "<file>|<sheet>|<column>|shift <s>" of docs/design/17-scanner.md, a sheet name may hold a bar so the column and shift are
        // taken from the end
        public static bool Excel(string raw, out string file, out string sheet, out string column)
        {
            file = sheet = column = null;
            int bar = raw.IndexOf('|');
            int shift = raw.LastIndexOf("|shift ", StringComparison.Ordinal);
            if (bar < 0 || shift <= bar)
            {
                return false;
            }
            string place = raw.Substring(bar + 1, shift - bar - 1);
            int last = place.LastIndexOf('|');
            if (last <= 0 || place.StartsWith("page ", StringComparison.Ordinal) || place.StartsWith("block|", StringComparison.Ordinal) || place.StartsWith("text|", StringComparison.Ordinal))
            {
                return false;
            }
            file = raw.Substring(0, bar);
            sheet = place.Substring(0, last);
            column = place.Substring(last + 1);
            return true;
        }

        private static IEnumerable<JObject> Rows(JObject scan)
        {
            return (scan["rows"] as JArray ?? new JArray()).OfType<JObject>();
        }
    }
}
