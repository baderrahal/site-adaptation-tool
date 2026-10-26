using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R352. The area of the overlap of two simple polygons by vertical slabs, the slabs cut at every vertex and at every crossing of an edge
    // of one with an edge of the other, so the overlap of the cross sections changes linearly within a slab and its width at the middle of
    // the slab gives the area of the slab exactly. shapely gives the same area to the rounding of the coordinates
    public static class PolygonOverlap
    {
        public static double Area(IReadOnlyList<double[]> a, IReadOnlyList<double[]> b)
        {
            var xs = new List<double>();
            xs.AddRange(a.Select(point => point[0]));
            xs.AddRange(b.Select(point => point[0]));
            for (int i = 0; i < a.Count; i++)
            {
                double[] p = a[i], q = a[(i + 1) % a.Count];
                for (int k = 0; k < b.Count; k++)
                {
                    double[] r = b[k], s = b[(k + 1) % b.Count];
                    double x;
                    if (Crossing(p, q, r, s, out x))
                    {
                        xs.Add(x);
                    }
                }
            }
            List<double> cuts = xs.Distinct().OrderBy(x => x).ToList();
            double area = 0;
            for (int index = 0; index + 1 < cuts.Count; index++)
            {
                double width = cuts[index + 1] - cuts[index];
                if (width <= 0)
                {
                    continue;
                }
                double middle = (cuts[index] + cuts[index + 1]) / 2;
                area += width * Overlap(Section(a, middle), Section(b, middle));
            }
            return area;
        }

        // The x of the crossing of two segments, false when they do not cross or run parallel
        private static bool Crossing(double[] p, double[] q, double[] r, double[] s, out double x)
        {
            x = 0;
            double dx1 = q[0] - p[0], dy1 = q[1] - p[1], dx2 = s[0] - r[0], dy2 = s[1] - r[1];
            double denominator = dx1 * dy2 - dy1 * dx2;
            if (denominator == 0)
            {
                return false;
            }
            double t = ((r[0] - p[0]) * dy2 - (r[1] - p[1]) * dx2) / denominator;
            double u = ((r[0] - p[0]) * dy1 - (r[1] - p[1]) * dx1) / denominator;
            if (t < 0 || t > 1 || u < 0 || u > 1)
            {
                return false;
            }
            x = p[0] + t * dx1;
            return true;
        }

        // The intervals of y a polygon holds on the vertical line at x, from the crossings of its edges taken in pairs
        private static List<double[]> Section(IReadOnlyList<double[]> polygon, double x)
        {
            var ys = new List<double>();
            for (int index = 0; index < polygon.Count; index++)
            {
                double[] p = polygon[index], q = polygon[(index + 1) % polygon.Count];
                if ((p[0] < x) != (q[0] < x))
                {
                    ys.Add(p[1] + (x - p[0]) * (q[1] - p[1]) / (q[0] - p[0]));
                }
            }
            ys.Sort();
            var found = new List<double[]>();
            for (int index = 0; index + 1 < ys.Count; index += 2)
            {
                found.Add(new[] { ys[index], ys[index + 1] });
            }
            return found;
        }

        private static double Overlap(List<double[]> first, List<double[]> second)
        {
            double length = 0;
            foreach (double[] one in first)
            {
                foreach (double[] two in second)
                {
                    length += Math.Max(0, Math.Min(one[1], two[1]) - Math.Max(one[0], two[0]));
                }
            }
            return length;
        }

        // shapely contains of a polygon in another: every vertex inside or on the outline of the other and no edge of one crossing an
        // edge of the other between their ends
        public static bool Contains(IReadOnlyList<double[]> outer, IReadOnlyList<double[]> inner)
        {
            foreach (double[] point in inner)
            {
                if (!LocationPlan.Inside(outer.ToArray(), point[0], point[1]) && !OnOutline(outer, point))
                {
                    return false;
                }
            }
            for (int i = 0; i < inner.Count; i++)
            {
                double[] p = inner[i], q = inner[(i + 1) % inner.Count];
                for (int k = 0; k < outer.Count; k++)
                {
                    if (Proper(p, q, outer[k], outer[(k + 1) % outer.Count]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool OnOutline(IReadOnlyList<double[]> polygon, double[] point)
        {
            for (int index = 0; index < polygon.Count; index++)
            {
                double[] a = polygon[index], b = polygon[(index + 1) % polygon.Count];
                double cross = (b[0] - a[0]) * (point[1] - a[1]) - (b[1] - a[1]) * (point[0] - a[0]);
                if (cross == 0 && Math.Min(a[0], b[0]) <= point[0] && point[0] <= Math.Max(a[0], b[0]) && Math.Min(a[1], b[1]) <= point[1] && point[1] <= Math.Max(a[1], b[1]))
                {
                    return true;
                }
            }
            return false;
        }

        private static double Side(double[] a, double[] b, double[] c)
        {
            return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
        }

        private static bool Proper(double[] p, double[] q, double[] r, double[] s)
        {
            double d1 = Side(r, s, p), d2 = Side(r, s, q), d3 = Side(p, q, r), d4 = Side(p, q, s);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }
    }

    // R352. The plot table of button 2, python/plot_table.py step for step: per baseplan face inside the Phase 1 border its label, type and
    // flags, the official number of the neighbourhood schedule that holds its label plus the shift (R28), the location plan labels whose
    // site point as written it holds, joined in the order of their numbers, the footprint blocks that hold at least minShare of their
    // outline in it, largest overlap first, overlaps within the areas tolerance in the order of the read (everyShare), and the area key of
    // R256 by the location plan numbers of the face
    public static class PlotTable
    {
        public const string StepName = "plotTable";
        public const string BorderRole = "border";

        public static readonly string[] Roles = { LocationPlan.PlotNumbersRole, "baseplan", "villaBlocks", LocationPlan.ScheduleRole + "*", "dlus", "sales", BorderRole };

        // phase1_border.csv of the data book, its x and y columns in their order
        public static List<double[]> Border(IEnumerable<Dictionary<string, string>> rows)
        {
            var found = new List<double[]>();
            foreach (Dictionary<string, string> row in rows)
            {
                string x, y;
                if (!row.TryGetValue("x", out x) || !row.TryGetValue("y", out y))
                {
                    throw new InvalidDataException("the border holds no column x and y (R352)");
                }
                found.Add(new[] { ExactDouble.Parse(x), ExactDouble.Parse(y) });
            }
            if (found.Count < 3)
            {
                throw new InvalidDataException("the border holds " + found.Count + " points, a border needs three (R352)");
            }
            return found;
        }

        private sealed class Unit
        {
            public string Name;
            public double[][] Outline;
            public double Area;
            public double[] Low;
            public double[] High;
            public JToken Rotation;
            public JToken Mirrored;
        }

        // The rotation of a block as the read holds it, null when it holds none
        private static object Rotation(JToken value)
        {
            return value == null || value.Type == JTokenType.Null ? null : (object)value.Value<double>();
        }

        private static string Joined(IEnumerable<string> values)
        {
            string text = string.Join("|", values.Select(value => value ?? "")).Trim('|');
            return text.Length == 0 ? null : text;
        }

        private static double[] Low(IEnumerable<double[]> points)
        {
            return new[] { points.Min(point => point[0]), points.Min(point => point[1]) };
        }

        private static double[] High(IEnumerable<double[]> points)
        {
            return new[] { points.Max(point => point[0]), points.Max(point => point[1]) };
        }

        public static Dictionary<string, Dictionary<string, object>> Build(JArray plots, IReadOnlyDictionary<string, PlanFace> faces, List<double[]> border,
            IReadOnlyDictionary<string, HashSet<string>> officials, IReadOnlyList<KeyValuePair<string, int>> shifts, IReadOnlyList<LocationPlan.Row> labels,
            JArray blocks, double minShare, SheetRows.AreaKey dlus, SheetRows.AreaKey sales, double areaTolerance)
        {
            var count = new Dictionary<string, int>();
            var types = new Dictionary<string, JToken>();
            var many = new HashSet<string>();
            foreach (JToken entry in plots)
            {
                JToken plot = entry["plot"];
                string label = (string)entry["kind"] == "plot_shape" ? FaceStep.WholeLabel(plot == null || plot.Type == JTokenType.Null ? null : plot.ToString()) : null;
                if (label != null)
                {
                    count[label] = count.TryGetValue(label, out int seen) ? seen + 1 : 1;
                    types[label] = entry["type"] ?? JValue.CreateNull();
                }
                if ((string)entry["reason"] == "many_labels")
                {
                    foreach (JToken item in entry["labels"] as JArray ?? new JArray())
                    {
                        many.Add(item.ToString());
                    }
                }
            }
            List<PlanFace> faceList = faces.Values.OrderBy(face => long.Parse(face.Label, CultureInfo.InvariantCulture)).ToList();
            var bounds = faceList.ToDictionary(face => face.Label, face => new[] { Low(face.Rings.SelectMany(ring => ring)), High(face.Rings.SelectMany(ring => ring)) });
            // The location plan labels by the faces that hold their site point as labels.csv writes it, at 3 decimals
            var onPlan = new Dictionary<string, List<LocationPlan.Row>>();
            foreach (LocationPlan.Row row in labels)
            {
                double east = Rounding.Fixed(row.East, 3), north = Rounding.Fixed(row.North, 3);
                foreach (PlanFace face in faceList.Where(face => face.Rings.Any(ring => LocationPlan.Inside(ring, east, north))))
                {
                    if (!onPlan.ContainsKey(face.Label))
                    {
                        onPlan[face.Label] = new List<LocationPlan.Row>();
                    }
                    onPlan[face.Label].Add(row);
                }
            }
            // The footprint blocks with an outline of three points or more, every face that holds minShare of one or more
            var units = new List<Unit>();
            foreach (JToken block in blocks)
            {
                double[][] outline = (block["outline"] as JArray ?? new JArray()).Select(point => new[] { (double)point[0], (double)point[1] }).ToArray();
                if (outline.Length < 3)
                {
                    continue;
                }
                string name = ((string)block["name"] ?? "").Split('$').Last();
                units.Add(new Unit { Name = name, Outline = outline, Area = FaceStep.Ring(outline).Area, Low = Low(outline), High = High(outline), Rotation = block["rotationDeg"], Mirrored = block["mirrored"] });
            }
            var perBlock = new List<List<string>>();
            var overlaps = new Dictionary<string, Dictionary<int, double>>();
            for (int item = 0; item < units.Count; item++)
            {
                Unit unit = units[item];
                var shares = new List<Tuple<double, string>>();
                if (unit.Area > 0)
                {
                    foreach (PlanFace face in faceList)
                    {
                        double[][] box = bounds[face.Label];
                        if (box[0][0] > unit.High[0] || box[1][0] < unit.Low[0] || box[0][1] > unit.High[1] || box[1][1] < unit.Low[1])
                        {
                            continue;
                        }
                        double overlap = face.Rings.Sum(ring => PolygonOverlap.Area(ring, unit.Outline));
                        if (overlap / unit.Area > 0)
                        {
                            shares.Add(Tuple.Create(overlap / unit.Area, face.Label));
                            if (!overlaps.ContainsKey(face.Label))
                            {
                                overlaps[face.Label] = new Dictionary<int, double>();
                            }
                            overlaps[face.Label][item] = overlap;
                        }
                    }
                }
                perBlock.Add(shares.OrderBy(share => -share.Item1).ThenBy(share => long.Parse(share.Item2, CultureInfo.InvariantCulture)).Where(share => share.Item1 >= minShare).Select(share => share.Item2).ToList());
            }
            var found = new Dictionary<string, List<int>>();
            for (int item = 0; item < perBlock.Count; item++)
            {
                foreach (string hit in perBlock[item])
                {
                    if (!found.ContainsKey(hit))
                    {
                        found[hit] = new List<int>();
                    }
                    found[hit].Add(item);
                }
            }
            var table = new Dictionary<string, Dictionary<string, object>>();
            double[][] borderRing = border.ToArray();
            foreach (PlanFace face in faceList)
            {
                if (!LocationPlan.Inside(borderRing, face.CentroidX, face.CentroidY))
                {
                    continue;
                }
                long label = long.Parse(face.Label, CultureInfo.InvariantCulture);
                List<string> homes = shifts.Where(shift => officials.TryGetValue(shift.Key, out HashSet<string> held) && held.Contains((label + shift.Value).ToString(CultureInfo.InvariantCulture)))
                    .Select(shift => shift.Key).ToList();
                string home = homes.Count == 1 ? homes[0] : null;
                List<LocationPlan.Row> plan = (onPlan.TryGetValue(face.Label, out List<LocationPlan.Row> held2) ? held2 : new List<LocationPlan.Row>())
                    .OrderBy(row => long.Parse(row.Number, CultureInfo.InvariantCulture)).ToList();
                // Overlaps within the areas tolerance keep the order of the read, so the parking bays of a face are not ordered by binary noise
                List<int> on = found.TryGetValue(face.Label, out List<int> blocksOn)
                    ? blocksOn.OrderBy(item => -Math.Round(overlaps[face.Label][item] / areaTolerance)).ThenBy(item => item).ToList() : new List<int>();
                List<string> numbers = plan.Select(row => row.Number).ToList();
                var values = new HashSet<Tuple<string, string>>(numbers.Where(dlus.Values.ContainsKey).Select(number => Tuple.Create(dlus.Values[number].Key, dlus.Values[number].Value)));
                Tuple<string, string> key = values.Count == 1 ? values.First() : null;
                string why = numbers.Count == 0 ? "noNumber" : values.Count == 0 ? "noRow" : values.Count > 1 ? "twoValues" : null;
                string check = null;
                if (key != null)
                {
                    var against = new HashSet<Tuple<string, string>>(numbers.Where(sales.Values.ContainsKey).Select(number => Tuple.Create(sales.Values[number].Key, sales.Values[number].Value)));
                    check = against.Count == 0 ? "noSalesRow" : against.Count == 1 && against.Contains(key) ? "same" : "differs";
                }
                double area = face.Rings.Sum(ring => FaceStep.Ring(ring).Area);
                table[face.Label] = new Dictionary<string, object>
                {
                    ["baseplanLabel"] = face.Label,
                    ["clientType"] = types.TryGetValue(face.Label, out JToken type) ? type : null,
                    ["centroid"] = new[] { face.CentroidX, face.CentroidY },
                    ["faceAreaM2"] = area,
                    ["crossesBorder"] = !face.Rings.All(ring => PolygonOverlap.Contains(border, ring)),
                    ["duplicateLabel"] = count.TryGetValue(face.Label, out int labelCount) && labelCount > 1,
                    ["inManyLabelFace"] = many.Contains(face.Label),
                    ["officialNumber"] = home == null ? null : (label + shifts.First(shift => shift.Key == home).Value).ToString(CultureInfo.InvariantCulture),
                    ["neighbourhood"] = home,
                    ["twoNeighbourhoods"] = homes.Count > 1,
                    ["locationPlanNumber"] = numbers.Count == 0 ? null : string.Join("|", numbers),
                    ["locationPlanCode"] = plan.Count == 0 ? null : string.Join("|", plan.Select(row => row.Code)),
                    ["locationPlanNewCode"] = Joined(plan.Select(row => row.NewCode)),
                    ["typologyRev9"] = Joined(plan.Select(row => row.Rev9)),
                    ["footprintBlock"] = on.Count == 0 ? null : string.Join("|", on.Select(item => units[item].Name)),
                    ["footprintRotationDeg"] = on.Count == 0 ? null : Rotation(units[on[0]].Rotation),
                    ["footprintMirrored"] = on.Count == 0 ? null : units[on[0]].Mirrored,
                    ["footprintShared"] = on.Count == 0 ? (object)null : perBlock[on[0]].Count > 1,
                    ["zone"] = key?.Item1,
                    ["nbhId"] = key?.Item2,
                    ["areaKeyMethod"] = key != null ? "locationPlanNumber" : why,
                    ["areaKeyCheck"] = check,
                };
            }
            return table;
        }
    }
}
