using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common
{
    // R550. The name of a report book by ISO 19650, in the pattern of the past job schedule, the fields from addin.report.fileName of config with
    // the fields of the project settings over them. A config with no such block keeps <nbh>_Report.xlsx
    public static class ReportFile
    {
        public const string NbhToken = "<nbh>";
        public const string Revision = "revision";
        public const string Status = "status";
        public const string ProjectName = "projectName";
        public static readonly string[] NameFields = { "project", "originator", "volume", "level", "type", "role", "number" };

        // Null when config holds no block, else every field by its name
        public static SortedDictionary<string, string> Fields(JObject config, IReadOnlyDictionary<string, string> picked)
        {
            if (!(config?["addin"]?["report"]?["fileName"] is JObject block))
            {
                return null;
            }
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (JProperty field in block.Properties().Where(item => item.Value.Type == JTokenType.String && item.Name != "note"))
            {
                fields[field.Name] = (string)field.Value;
            }
            foreach (KeyValuePair<string, string> pick in picked ?? new Dictionary<string, string>())
            {
                fields[pick.Key] = pick.Value;
            }
            string[] missing = NameFields.Where(name => !fields.TryGetValue(name, out string value) || string.IsNullOrWhiteSpace(value)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException("addin.report.fileName of the settings holds no " + string.Join(", ", missing) + " (R550)");
            }
            return fields;
        }

        public static string Name(string nbh, IReadOnlyDictionary<string, string> fields)
        {
            if (fields == null)
            {
                return nbh + "_Report.xlsx";
            }
            string name = string.Join("-", NameFields.Select(field => fields[field].Replace(NbhToken, nbh))) + ".xlsx";
            if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException("the file name fields give no file name: " + name + " (R550)");
            }
            return name;
        }
    }

    // R551 to R553. The sheets Cover, Plot schedule, Floors and Walls of the report Excel, read from the plan as written and the element ids the
    // model holds at report time, nothing measured again
    public static partial class ReportSheets
    {
        // R552. The columns the past job schedule holds keep its order, the columns it lacks follow
        private static ReportTable ScheduleTable(ReportInputs inputs, Made made, List<JObject> written, List<JObject> walls)
        {
            var table = new ReportTable { Header = ReportExcelText.ScheduleColumns, Formats = new string[ReportExcelText.ScheduleColumns.Length] };
            string oldColumn = Text(inputs.Config["addin"]?["report"]?["oldPlotColumn"]);
            ILookup<string, JObject> wallsOf = walls.ToLookup(wall => (string)wall["plot"]);
            Dictionary<JObject, string> parts = WallParts(made);
            foreach (JObject plot in written)
            {
                string number = (string)plot["plot"];
                string unit = Text(plot["unit"]?["value"]);
                JObject levels = plot["levels"] as JObject ?? new JObject();
                JArray corners = plot["floors"]?["ramp"]?["corners"]?["value"] as JArray;
                string oldNumber = null;
                if (oldColumn != null && made.Rows.TryGetValue(number, out Dictionary<string, string> row))
                {
                    row.TryGetValue(oldColumn, out oldNumber);
                }
                string file = Text(plot["prototypeFile"]?["value"]);
                var cells = new List<object>
                {
                    number + "-" + unit, number, unit, Cell(levels["ffl"]?["value"]), Cell(levels["ssl"]?["value"]), Cell(levels["entry"]?["value"]), Cell(levels["height"]?["value"]),
                    Cell(plot["levelRule"]?["steps"]), Cell(levels["steps"]?["value"]), oldNumber, file == null ? null : Path.GetFileNameWithoutExtension(file), Cell(plot["mirrored"]?["value"]),
                    Cell(plot["rotationDeg"]?["value"]),
                };
                for (int index = 0; index < ReportExcelText.RampCorners; index++)
                {
                    cells.Add(corners != null && index < corners.Count ? Cell((corners[index] as JArray)?.ElementAtOrDefault(2)) : null);
                }
                cells.Add(RevitCounts.ElementId(inputs.RevitCounts, number, PlacedKinds.Link));
                cells.Add(RevitCounts.ElementId(inputs.RevitCounts, number, PlacedKinds.Generic));
                cells.Add(RevitCounts.ElementId(inputs.RevitCounts, number, PlacedKinds.Yard));
                cells.Add(RevitCounts.ElementId(inputs.RevitCounts, number, PlacedKinds.Ramp));
                List<long> wallIds = wallsOf[number].SelectMany(wall => RevitCounts.WallIds(inputs.RevitCounts, number, parts[wall])).ToList();
                cells.Add(wallIds.Count == 0 ? null : wallIds.Count == 1 ? (object)wallIds[0] : string.Join(ReportExcelText.ListJoin, wallIds));
                table.Rows.Add(cells.ToArray());
            }
            return table;
        }

        private static ReportTable FloorsTable(ReportInputs inputs, List<JObject> written)
        {
            var table = new ReportTable { Header = ReportExcelText.FloorColumns, Formats = new string[ReportExcelText.FloorColumns.Length] };
            foreach (JObject plot in written)
            {
                string number = (string)plot["plot"];
                foreach (JProperty floor in (plot["floors"] as JObject ?? new JObject()).Properties())
                {
                    table.Rows.Add(new[]
                    {
                        number, floor.Name, Cell(floor.Value["type"]?["value"]), Cell(floor.Value["level"]?["value"]), Cell(floor.Value["levelElevation"]?["value"]),
                        Cell(floor.Value["z"]?["value"]), Cell(floor.Value["heightOffset"]?["value"]), Cell(floor.Value["slopePercent"]?["value"]),
                        RevitCounts.ElementId(inputs.RevitCounts, number, floor.Name),
                    });
                }
            }
            return table;
        }

        private static ReportTable WallsTable(ReportInputs inputs, Made made, List<JObject> walls)
        {
            var table = new ReportTable { Header = ReportExcelText.WallColumns, Formats = new string[ReportExcelText.WallColumns.Length] };
            Dictionary<JObject, string> parts = WallParts(made);
            foreach (JObject wall in walls)
            {
                string number = (string)wall["plot"];
                string other = string.Join(ReportExcelText.ListJoin, (wall["plots"]?["value"] as JArray ?? new JArray()).Select(item => (string)item).Where(item => item != number));
                List<long> ids = RevitCounts.WallIds(inputs.RevitCounts, number, parts[wall]);
                table.Rows.Add(new object[]
                {
                    number, Cell(wall["edge"]), other.Length == 0 ? null : other, Cell(wall["base"]?["value"]), Cell(wall["top"]?["value"]), WallLength(wall),
                    ids.Count == 0 ? null : ids.Count == 1 ? (object)ids[0] : string.Join(ReportExcelText.ListJoin, ids),
                });
            }
            return table;
        }

        private static object WallLength(JObject wall)
        {
            if (!(wall["ends"]?["value"] is JArray ends) || ends.Count != 2)
            {
                return null;
            }
            double east = (double)ends[1][0] - (double)ends[0][0], north = (double)ends[1][1] - (double)ends[0][1];
            return Math.Sqrt(east * east + north * north);
        }

        // The part of each wall of the plan as WallPlan.Parts gives its tag, over all the walls of the plan, as a build counts them
        private static Dictionary<JObject, string> WallParts(Made made)
        {
            if (made.Parts == null)
            {
                List<string> parts = BoundaryWalls.WallPlan.Parts(made.Walls.Select(wall => Tuple.Create((string)wall["plot"],
                    (wall["plots"]?["value"] as JArray ?? new JArray()).Select(item => ((string)item).Trim()), (long?)wall["edge"] ?? 0)));
                made.Parts = made.Walls.Select((wall, index) => new { wall, index }).ToDictionary(item => item.wall, item => parts[item.index]);
            }
            return made.Parts;
        }

        // R551. One row per item, the fields as the run gave them
        private static ReportTable CoverTable(ReportInputs inputs, string nbh, string fileName)
        {
            JObject fields = inputs.RunFacts?["fields"] as JObject;
            Func<string, string> field = name => Text(fields?[name]) ?? ReportExcelText.NotSet;
            var table = new ReportTable { Header = ReportExcelText.CoverColumns, Formats = new string[ReportExcelText.CoverColumns.Length] };
            foreach (string[] row in new[]
            {
                new[] { ReportExcelText.CoverProject, field(ReportFile.ProjectName) },
                new[] { ReportExcelText.CoverNbh, nbh },
                new[] { ReportExcelText.CoverRevision, field(ReportFile.Revision) },
                new[] { ReportExcelText.CoverStatus, field(ReportFile.Status) },
                new[] { ReportExcelText.CoverDate, inputs.Now.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                new[] { ReportExcelText.CoverTool, Text(inputs.RunFacts?["toolVersion"]) ?? ReportExcelText.NotSet },
                new[] { ReportExcelText.CoverModel, Text(inputs.RunFacts?["model"]) ?? ReportExcelText.NotSet },
                new[] { ReportExcelText.CoverFile, fileName },
            })
            {
                table.Rows.Add(row.Cast<object>().ToArray());
            }
            return table;
        }
    }
}
