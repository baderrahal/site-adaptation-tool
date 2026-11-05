using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SiteBuild
{
    // R647, Q473. The toposolid is cut to the boundary of each neighbourhood, the plot outlines of that neighbourhood joined and grown by marginM, and not to a
    // hull of the plots. The plan gives each boundary with its area and the area of the hull it replaces. Road edge points come in the optional file
    // roadEdge of the request: {"points": [{"area": "N1", "east": x, "north": y}]}. A point further than limitMm from the boundary of its neighbourhood is listed
    internal static class NeighbourhoodCut
    {
        public static JObject Build(IReadOnlyList<PlanPlot> plots, JObject roadEdge, double marginM, double limitMm, double[] origin, List<PlanIssue> issues)
        {
            var boundaries = new JArray();
            var byArea = new Dictionary<string, List<List<double[]>>>(StringComparer.Ordinal);
            var boundaryRings = new Dictionary<string, List<Rings.Boundary>>(StringComparer.Ordinal);
            foreach (PlanPlot plot in plots)
            {
                if (plot.In.Outline == null)
                {
                    continue;
                }
                string area = plot.In.Area ?? plot.In.Groups.FirstOrDefault();
                if (area == null)
                {
                    issues.Add(new PlanIssue { Plot = plot.Number, Line = PlanTexts.CutNoArea(plot.Number) });
                    continue;
                }
                List<List<double[]>> list;
                if (!byArea.TryGetValue(area, out list))
                {
                    list = new List<List<double[]>>();
                    byArea[area] = list;
                }
                list.Add(plot.In.Outline);
            }
            double cutM2 = 0, hullM2 = 0;
            foreach (KeyValuePair<string, List<List<double[]>>> area in byArea.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                List<Rings.Boundary> rings = Rings.Grown(area.Value.Cast<IList<double[]>>(), marginM, origin);
                boundaryRings[area.Key] = rings;
                double cut = rings.Sum(ring => ring.Hole ? -Rings.Area(ring.Ring) : Rings.Area(ring.Ring));
                double hull = Rings.Area(Rings.Hull(area.Value.SelectMany(ring => ring)));
                cutM2 += cut;
                hullM2 += hull;
                boundaries.Add(new JObject
                {
                    ["area"] = area.Key,
                    ["plots"] = area.Value.Count,
                    ["cutM2"] = Math.Round(cut, 1),
                    ["hullM2"] = Math.Round(hull, 1),
                    ["rings"] = new JArray(rings.Select(ring => new JObject { ["hole"] = ring.Hole, ["points"] = new JArray(ring.Ring.Select(point => new JArray(point[0], point[1]))) })),
                });
            }
            return new JObject
            {
                ["marginM"] = marginM,
                ["boundaries"] = boundaries,
                ["line"] = PlanTexts.CutLine(byArea.Count, Math.Round(cutM2, 1), Math.Round(hullM2, 1)),
                ["roadEdge"] = RoadEdge(roadEdge, boundaryRings, limitMm, issues),
            };
        }

        private static JObject RoadEdge(JObject file, Dictionary<string, List<Rings.Boundary>> boundaries, double limitMm, List<PlanIssue> issues)
        {
            var over = new JArray();
            int count = 0;
            if (file == null)
            {
                return new JObject { ["limitMm"] = limitMm, ["checked"] = 0, ["over"] = over, ["line"] = PlanTexts.NoRoadEdgePoints };
            }
            foreach (JObject point in (file["points"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string area = (string)point["area"];
                JToken east = point["east"], north = point["north"];
                List<Rings.Boundary> rings;
                if (area == null || east == null || north == null || !boundaries.TryGetValue(area, out rings))
                {
                    continue;
                }
                count++;
                double mm = rings.Min(ring => Distance(ring.Ring, (double)east, (double)north)) * 1000.0;
                if (mm > limitMm)
                {
                    over.Add(new JObject { ["area"] = area, ["east"] = (double)east, ["north"] = (double)north, ["distanceMm"] = Math.Round(mm, 1) });
                    issues.Add(new PlanIssue { Line = PlanTexts.RoadEdgePoint(area, Math.Round(mm, 1)) });
                }
            }
            return new JObject { ["limitMm"] = limitMm, ["checked"] = count, ["over"] = over, ["line"] = PlanTexts.RoadEdgeLine(count, over.Count, limitMm) };
        }

        // The least distance from a point to the edges of a ring
        private static double Distance(IList<double[]> ring, double x, double y)
        {
            double best = double.MaxValue;
            for (int at = 0; at < ring.Count; at++)
            {
                double[] a = ring[at];
                double[] b = ring[(at + 1) % ring.Count];
                double dx = b[0] - a[0], dy = b[1] - a[1];
                double length = dx * dx + dy * dy;
                double along = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / length));
                best = Math.Min(best, Math.Sqrt(Math.Pow(x - (a[0] + along * dx), 2) + Math.Pow(y - (a[1] + along * dy), 2)));
            }
            return best;
        }
    }
}
