using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

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
        public bool IsGood { get { return Mark == DataCheckText.Marks[0] || Mark == DataCheckText.Marks[1]; } }
    }

    // R620, R606. A file the check did not read, with the exact reason the worker wrote
    public sealed class DataCheckFileRow
    {
        public string Name { get; set; }
        public string Reason { get; set; }
    }

    // R620. What the window shows of one data check: the rows, the files not read, the note of a check the worker stopped, the files
    // it read and the plots it found
    public sealed class DataCheckResult
    {
        public IReadOnlyList<DataCheckRow> Rows { get; set; } = new DataCheckRow[0];
        public IReadOnlyList<DataCheckFileRow> Unread { get; set; } = new DataCheckFileRow[0];
        public string Stopped { get; set; }
        public int FilesRead { get; set; }
        public int PlotsFound { get; set; }
    }

    // R620. data_check.json of the worker command dataCheck (R600, R605, R606) and plots.json beside it turned into what the window shows,
    // in the order the worker wrote them. No Revit type, so a test reads it with no window
    public static class DataCheckRows
    {
        public const string FileName = "data_check.json";
        public const string PlotsFileName = "plots.json";
        public const string NoNeeds = "the data check result holds no needs";

        // The ids of the needs are the ones of the worker, each at the place of its plain word in DataCheckText
        public static IReadOnlyList<string> NeedIds { get { return DataCheckLogic.Needs; } }
        public static readonly string[] MarkIds = { "strong", "usable", "weak", "missing" };

        private static string Word(IReadOnlyList<string> ids, string[] words, string id)
        {
            int at = ids.ToList().IndexOf(id);
            return at < 0 ? id : words[at];
        }

        // plotsJson may be null, the plots found are then 0
        public static DataCheckResult ReadResult(string dataCheckJson, string plotsJson)
        {
            JObject root = JObject.Parse(dataCheckJson);
            JArray needs = root["needs"] as JArray;
            if (needs == null || needs.Count == 0)
            {
                throw new FormatException(NoNeeds);
            }
            List<JObject> files = (root["files"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
            return new DataCheckResult
            {
                Rows = needs.OfType<JObject>().Select(row =>
                {
                    string need = (string)row["need"] ?? "";
                    string mark = (string)row["mark"] ?? "";
                    return new DataCheckRow
                    {
                        Need = need,
                        Name = Word(NeedIds, DataCheckText.Needs, need),
                        Mark = Word(MarkIds, DataCheckText.Marks, mark),
                        Line = (string)row["line"] ?? "",
                    };
                }).ToList(),
                Unread = files.Where(file => (string)file["status"] != "read").Select(file => new DataCheckFileRow { Name = (string)file["name"] ?? "", Reason = (string)file["reason"] ?? "" }).ToList(),
                Stopped = string.IsNullOrWhiteSpace((string)root["stopped"]) ? null : (string)root["stopped"],
                FilesRead = files.Count(file => (string)file["status"] == "read"),
                PlotsFound = plotsJson == null ? 0 : (JObject.Parse(plotsJson)["plots"] as JArray)?.Count ?? 0,
            };
        }
    }
}
