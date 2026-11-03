using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common
{
    // R381, Q124. The counts the add-in reads from the model at report time and gives the worker as the input revitCounts of reportExcel: per
    // plot the tagged links, the tagged floors by kind and whether the last run of button 6 that committed set its levels and read them back.
    // Made here from the tags of R368 with no Revit type, so the reader in the add-in only collects the tags and writes this file
    public static class RevitCounts
    {
        public const string Format = "siteAdaptationRevitCounts";
        public const string NotCounts = "the input revitCounts is no file of the counts the add-in read from the model, format " + Format
            + " with plots by number, each with links, floors by kind and level (R381)";

        public static JObject FromTags(IEnumerable<PlacedTag> tags, ICollection<string> levelPlots, string levelRun)
        {
            var plots = new SortedDictionary<string, JObject>(StringComparer.Ordinal);
            Func<string, JObject> of = number =>
            {
                if (!plots.TryGetValue(number, out JObject plot))
                {
                    var floors = new JObject();
                    foreach (string kind in SiteCoordinates.FloorKinds)
                    {
                        floors[kind] = 0;
                    }
                    plots[number] = plot = new JObject { ["links"] = 0, ["floors"] = floors, ["level"] = false };
                }
                return plot;
            };
            foreach (PlacedTag tag in tags)
            {
                if (tag.Key.Kind == PlacedKinds.Link)
                {
                    JObject plot = of(tag.Key.Plot);
                    plot["links"] = (int)plot["links"] + 1;
                }
                else if (SiteCoordinates.FloorKinds.Contains(tag.Key.Kind))
                {
                    JObject floors = (JObject)of(tag.Key.Plot)["floors"];
                    floors[tag.Key.Kind] = (int)floors[tag.Key.Kind] + 1;
                }
            }
            foreach (string number in levelPlots ?? new string[0])
            {
                of(number)["level"] = true;
            }
            var made = new JObject();
            foreach (KeyValuePair<string, JObject> plot in plots)
            {
                made[plot.Key] = plot.Value;
            }
            return new JObject { ["format"] = Format, ["formatVersion"] = 1, ["levelRun"] = levelRun, ["plots"] = made };
        }

        // R483. The counts with the element id of each link and floor by plot and kind, so a row of the sheet Placed can be traced to its element.
        // Read from the model at report time, so an id always names what stands now
        public static JObject FromElements(IReadOnlyCollection<KeyValuePair<long, PlacedTag>> elements, ICollection<string> levelPlots, string levelRun)
        {
            JObject counts = FromTags(elements.Select(element => element.Value), levelPlots, levelRun);
            foreach (var kind in elements.Where(element => element.Value.Key.Kind == PlacedKinds.Link || SiteCoordinates.FloorKinds.Contains(element.Value.Key.Kind))
                .GroupBy(element => new { element.Value.Key.Plot, element.Value.Key.Kind }))
            {
                var plot = (JObject)counts["plots"][kind.Key.Plot];
                if (!(plot["ids"] is JObject ids))
                {
                    plot["ids"] = ids = new JObject();
                }
                ids[kind.Key.Kind] = new JArray(kind.Select(element => element.Key).Distinct().OrderBy(id => id));
            }
            // Q307. The walls by "<plot>|<part>", apart from plots, as a wall counts in no plot row of the box and may stand on a plot with no villa
            var walls = new JObject();
            foreach (var wall in elements.Where(element => element.Value.Key.Kind == PlacedKinds.BoundaryWall).GroupBy(element => element.Value.Key.Plot + "|" + element.Value.Key.Part))
            {
                walls[wall.Key] = new JArray(wall.Select(element => element.Key).Distinct().OrderBy(id => id));
            }
            counts["walls"] = walls;
            return counts;
        }

        // The ids the model holds for a wall of a plot, none when the counts hold no walls
        public static List<long> WallIds(JObject counts, string plot, string part)
        {
            return (counts?["walls"]?[plot + "|" + part] as JArray)?.Select(id => (long)id).ToList() ?? new List<long>();
        }

        // R483. The cell Element id of a plot and kind: the id, the ids joined where a person copied an element, null where the model holds none
        // or the counts hold no ids
        public static object ElementId(JObject counts, string plot, string kind)
        {
            List<long> ids = (counts?["plots"]?[plot]?["ids"]?[kind] as JArray)?.Select(id => (long)id).ToList();
            if (ids == null || ids.Count == 0)
            {
                return null;
            }
            return ids.Count == 1 ? (object)ids[0] : string.Join(ReportExcelText.ListJoin, ids);
        }

        // Null when the file is a counts file of this form, else why the request is refused
        public static string Refusal(JObject counts)
        {
            if ((string)counts?["format"] != Format || !(counts["plots"] is JObject plots))
            {
                return NotCounts;
            }
            foreach (JProperty plot in plots.Properties())
            {
                if (!(plot.Value is JObject item) || item["links"]?.Type != JTokenType.Integer || item["level"]?.Type != JTokenType.Boolean
                    || !(item["floors"] is JObject floors) || floors.Properties().Any(kind => kind.Value.Type != JTokenType.Integer))
                {
                    return NotCounts;
                }
            }
            return null;
        }

        // The links, the floors and the plots with their levels read back of the plots of one NBH, the Revit column of the box Counts agree
        public static long[] Totals(JObject counts, Func<string, bool> inNbh)
        {
            var totals = new long[3];
            foreach (JProperty plot in ((JObject)counts["plots"]).Properties().Where(item => inNbh(item.Name)))
            {
                totals[0] += (long)plot.Value["links"];
                totals[1] += ((JObject)plot.Value["floors"]).Properties().Sum(kind => (long)kind.Value);
                totals[2] += (bool)plot.Value["level"] ? 1 : 0;
            }
            return totals;
        }
    }
}
