using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.AddIn.ScanResults
{
    // R620. One row of the section Data check: the need by its plain name, its mark and the plain line the worker gave
    public sealed class DataCheckRow
    {
        public string Need { get; set; }
        public string Name { get; set; }
        public string Mark { get; set; }
        public string Line { get; set; }

        // Strong and usable read green, weak and missing amber, as the status chips of the rest of the window
        public bool IsGood { get { return Mark == "Strong" || Mark == "Usable"; } }
    }

    // R620. data_check.json of the worker command dataCheck (R600, R605) turned into rows, in the order the worker wrote them. No Revit
    // type, so a test reads it with no window
    public static class DataCheckRows
    {
        public const string FileName = "data_check.json";

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plotOutlines"] = "Plot outlines", ["plotNumbers"] = "Plot numbers", ["groupLevels"] = "Group levels", ["villaType"] = "Villa types",
            ["villaBlocks"] = "Villa blocks", ["roadEdges"] = "Road edges", ["roadLevels"] = "Road levels", ["coordinates"] = "Coordinates",
            ["prototypes"] = "Prototypes", ["rules"] = "Rule sets", ["sharedParameters"] = "Shared parameters",
        };

        public static IReadOnlyList<DataCheckRow> Read(string dataCheckJson)
        {
            JArray needs = JObject.Parse(dataCheckJson)["needs"] as JArray;
            if (needs == null)
            {
                throw new FormatException("data_check.json holds no needs");
            }
            return needs.OfType<JObject>().Select(row =>
            {
                string need = (string)row["need"] ?? "";
                string mark = (string)row["mark"] ?? "";
                return new DataCheckRow
                {
                    Need = need,
                    Name = Names.TryGetValue(need, out string name) ? name : need,
                    Mark = mark.Length == 0 ? mark : char.ToUpperInvariant(mark[0]) + mark.Substring(1),
                    Line = (string)row["line"] ?? "",
                };
            }).ToList();
        }
    }
}
