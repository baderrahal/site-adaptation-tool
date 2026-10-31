using System.Linq;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.UiShots
{
    // Lane C SCAN-UI. What the tool read rendered as the other windows, with made up file names, layers and counts and no coordinate or
    // level. The villa type is Pick as on the board, 30 conflicts so that Show all stands, steps/ui-shots/README.md
    public static partial class Shots
    {
        private static BitmapSource ScanResultsPage()
        {
            return Draw(Body(LaidOutScanResults()));
        }

        public static ScanResultsWindow LaidOutScanResults()
        {
            var window = new ScanResultsWindow(SampleScanResults());
            LayOut(window);
            return window;
        }

        public static ScanResultsSession SampleScanResults()
        {
            return new ScanResultsSession(ScanView.Read(SampleScanJson().ToString(), new ProjectSettings()), new ProjectSettings(), () => null);
        }

        public static JObject SampleScanJson()
        {
            JObject Row(string kind, string source, string found, int score, string use)
            {
                return new JObject { ["kind"] = kind, ["source"] = source, ["found"] = found, ["proposedUse"] = use, ["score"] = score, ["conflicts"] = 0 };
            }
            JObject Input(string role, string name)
            {
                return new JObject { ["role"] = role, ["name"] = name, ["size"] = 1, ["sha256"] = "00" };
            }
            var rows = new JArray
            {
                Row("cadLayer", "site.dwg|OUTLINE", "300 closed outlines, 10 texts, 0 blocks", 0, ""),
                Row("cadLayer", "site.dwg|NUMBERS", "0 closed outlines, 310 texts, 0 blocks", 0, ""),
                Row("cadLayer", "footprints.dwg|HOUSES", "0 closed outlines, 0 texts, 280 blocks", 0, ""),
                Row("plotOutline", "site.dwg|OUTLINE", "300 faces", 296, "plot outlines"),
                Row("plotOutline", "footprints.dwg|HOUSES", "120 faces", 90, "check"),
                Row("plotNumber", "site.dwg|NUMBERS", "296 labels in plots", 296, "plot numbers"),
                Row("villaBlock", "footprints.dwg|HOUSES", "280 inserts", 270, "villa blocks"),
                Row("villaType", "footprints.dwg|block|HOUSES", "A1, B2", 180, "check"),
                Row("villaType", "plots.xlsx|Sheet 1|Type|shift 0", "A1, B2, C3", 150, "check"),
                Row("villaType", "numbers.pdf|page 1|shift 0", "A1", 40, "check"),
            };
            var conflicts = new JArray(Enumerable.Range(1, 30).Select(number => new JObject
            {
                ["plot"] = (100 + number).ToString(),
                ["field"] = number % 4 == 0 ? "number" : "villa type",
                ["valueA"] = number % 4 == 0 ? "101" : "A1",
                ["sourceA"] = "footprints.dwg|block|HOUSES",
                ["valueB"] = number % 4 == 0 ? "110" : "B2",
                ["sourceB"] = "plots.xlsx|Sheet 1|Type|shift 0",
            }));
            return new JObject
            {
                ["inputs"] = new JArray { Input("scanCad:1", "site.dwg"), Input("scanCad:2", "footprints.dwg"), Input("scanExcel:1", "plots.xlsx"), Input("scanPdf:1", "numbers.pdf") },
                ["rows"] = rows,
                ["conflicts"] = conflicts,
                ["checks"] = new JArray(),
                ["plots"] = new JArray(Enumerable.Range(1, 296).Select(number => new JObject { ["plot"] = number.ToString() })),
            };
        }
    }
}
