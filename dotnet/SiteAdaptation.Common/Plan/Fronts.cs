using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R355. The road edge lines of the curb layers, analysis.roadEdgeLayers, as cad_analysis.read_road_edges gives them, with the distance
    // of a point to the nearest of them as shapely STRtree.query_nearest gives it, the least distance of GEOS to a segment of any line. A tree
    // of the boxes of the segments, halved at the middle of their centres along the wider side down to Leaf segments, finds them, and the
    // distance itself is the least over every segment the boxes could not rule out. A box is ruled out only when it lies past the least found
    // by more than the slack, so the least is the one over every segment. The tree holds each segment once, so a stray vertex far off costs
    // no more than any segment, where the grid of 32 m cells before filled every cell of the box of each segment, 130,354,888 bytes on net8.0
    // and 140,498,368 on net48 for one vertex at 36000, 36000 (B6 of the review of PR 55, round_addin logs p4efix1_b6_first_csharp.txt)
    public sealed class RoadLines
    {
        private const int Leaf = 8;
        private readonly List<double[][]> segments = new List<double[][]>();
        private readonly Node root;

        private sealed class Node
        {
            public double MinX, MinY, MaxX, MaxY;
            public Node Low, High;
            public int[] Items;
        }

        public RoadLines(IEnumerable<IReadOnlyList<double[]>> lines)
        {
            foreach (IReadOnlyList<double[]> line in lines)
            {
                for (int index = 0; index + 1 < line.Count; index++)
                {
                    segments.Add(new[] { line[index], line[index + 1] });
                }
            }
            Count = segments.Count;
            if (Count > 0)
            {
                root = Build(Enumerable.Range(0, Count).ToArray(), 0, Count);
            }
        }

        // The number of segments, 0 for no line
        public int Count { get; }

        private Node Build(int[] order, int start, int count)
        {
            var node = new Node { MinX = double.PositiveInfinity, MinY = double.PositiveInfinity, MaxX = double.NegativeInfinity, MaxY = double.NegativeInfinity };
            for (int index = start; index < start + count; index++)
            {
                double[][] segment = segments[order[index]];
                node.MinX = Math.Min(node.MinX, Math.Min(segment[0][0], segment[1][0]));
                node.MaxX = Math.Max(node.MaxX, Math.Max(segment[0][0], segment[1][0]));
                node.MinY = Math.Min(node.MinY, Math.Min(segment[0][1], segment[1][1]));
                node.MaxY = Math.Max(node.MaxY, Math.Max(segment[0][1], segment[1][1]));
            }
            if (count <= Leaf)
            {
                node.Items = new int[count];
                Array.Copy(order, start, node.Items, 0, count);
                return node;
            }
            bool alongX = node.MaxX - node.MinX >= node.MaxY - node.MinY;
            var keys = new double[count];
            for (int index = 0; index < count; index++)
            {
                double[][] segment = segments[order[start + index]];
                keys[index] = alongX ? segment[0][0] + segment[1][0] : segment[0][1] + segment[1][1];
            }
            var slice = new int[count];
            Array.Copy(order, start, slice, 0, count);
            // The order among equal keys may differ by runtime, which moves no distance: the least over the segments is the same set's
            Array.Sort(keys, slice);
            Array.Copy(slice, 0, order, start, count);
            int half = count / 2;
            node.Low = Build(order, start, half);
            node.High = Build(order, start + half, count - half);
            return node;
        }

        private static double BoxDistance(Node node, double x, double y)
        {
            double dx = Math.Max(Math.Max(node.MinX - x, 0.0), x - node.MaxX);
            double dy = Math.Max(Math.Max(node.MinY - y, 0.0), y - node.MaxY);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // The least distance from the point to a segment of any line
        public double Distance(double x, double y)
        {
            double best = double.PositiveInfinity;
            if (root != null)
            {
                Search(root, new[] { x, y }, ref best);
            }
            return best;
        }

        private void Search(Node node, double[] point, ref double best)
        {
            // The slack, a micrometre and a part in a billion of the least, lies far over the rounding of the box and the segment distance
            if (BoxDistance(node, point[0], point[1]) > best + 1e-6 + 1e-9 * best)
            {
                return;
            }
            if (node.Items != null)
            {
                foreach (int index in node.Items)
                {
                    double distance = PyMath.SegmentDistance(point, segments[index][0], segments[index][1]);
                    if (distance < best)
                    {
                        best = distance;
                    }
                }
                return;
            }
            if (BoxDistance(node.Low, point[0], point[1]) <= BoxDistance(node.High, point[0], point[1]))
            {
                Search(node.Low, point, ref best);
                Search(node.High, point, ref best);
            }
            else
            {
                Search(node.High, point, ref best);
                Search(node.Low, point, ref best);
            }
        }
    }

    // R355. The front frame of a plot, cad_links.front_frame and corner_front.frame_for: n the unit normal into the plot, r the right hand
    // of someone on the road facing the plot, left and right the ends of the front edge seen from the road, its length, the distance of
    // its middle to the nearest curb and whether another candidate edge lies within FRONT_TIE of it
    public sealed class FrontFrame
    {
        public double[] N { get; set; }

        public double[] R { get; set; }

        public double[] Left { get; set; }

        public double[] Right { get; set; }

        public double Length { get; set; }

        public double Distance { get; set; }

        public bool Tied { get; set; }
    }

    // R355. A road edge of a face, corner_front.road_edges: its ends in the order of the ring, length, distance and outward normal
    public sealed class RoadEdge
    {
        public double[] First { get; set; }

        public double[] Second { get; set; }

        public double Length { get; set; }

        public double Distance { get; set; }

        public double[] Out { get; set; }
    }

    // R355. The front of a plot as plan_from_book.fronts keeps it
    public sealed class FrontItem
    {
        public BuildFace Face { get; set; }

        public FrontFrame Frame { get; set; }

        public double[][] Corners { get; set; }

        public double? Depth { get; set; }

        // The road levels of the two front corners when both have one, else null
        public double[] Front { get; set; }

        public string[] Surfaces { get; set; } = new string[0];

        public string Unit { get; set; }

        public string FrontRule { get; set; }
    }

    // R355, R122, R225, R226, R127, R344. The front of a plot the build takes, as plan_from_book.fronts gives it: on a plot with two or
    // more road edges the edge the Orientation of the sales sheet names (corner_front.corner_frame), none on such a plot with no
    // Orientation, else the front of R122, the ramp outline of R127 on it with the front setback of the unit, and the road level of R344
    // at its two front corners, the first surface of the order that covers each within snapM
    public static class Fronts
    {
        public const string StepName = "fronts";

        // entry_levels.FRONT_TIE, how near another candidate edge makes a tie, and the probe of corner_front.outward, in metres
        private const double FrontTie = 1.0;
        private const double Probe = 0.01;

        private static readonly Dictionary<string, double[]> Quadrants = new Dictionary<string, double[]>
        {
            ["EN"] = new[] { 0.0, 90.0 }, ["SE"] = new[] { 90.0, 180.0 }, ["WS"] = new[] { 180.0, 270.0 }, ["NW"] = new[] { 270.0, 360.0 },
        };

        // project_profile.metres: a length of the rules profile in mm as metres, refused when the profile does not say its lengths are mm
        public static double Metres(JObject rules, string rulesName, JToken value, string dotted)
        {
            JToken mm = rules["units_mm"];
            if (mm == null || mm.Type != JTokenType.Boolean || !(bool)mm)
            {
                throw new InvalidDataException(rulesName + " does not say its lengths are in mm");
            }
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
            {
                throw new InvalidDataException(rulesName + " value " + dotted + " is not a number");
            }
            return BuildFace.Number(value) / 1000;
        }

        // cad_links.front_limits: from the narrowest plot_width_m of the typologies less analysis.toleranceM up to below
        // analysis.partyWallShare of plot.long_side
        public static double[] Limits(JObject rules, JObject config, string rulesName = "roshn-makkah.json")
        {
            List<double> widths = ((rules["typologies"] as JObject) ?? new JObject()).Properties().Select(item => (item.Value as JObject)?["plot_width_m"])
                .Where(value => value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)).Select(BuildFace.Number).ToList();
            if (widths.Count == 0)
            {
                throw new InvalidDataException(rulesName + " typologies hold no plot_width_m");
            }
            JObject analysis = config["analysis"] as JObject ?? new JObject();
            return new[] { widths.Min() - BuildFace.Number(analysis["toleranceM"]), BuildFace.Number(analysis["partyWallShare"]) * Metres(rules, rulesName, rules["plot"]?["long_side"], "plot.long_side") };
        }

        // ramp_outline.setback: ramp.front_setback of the unit, else its default, in metres
        public static double Setback(JObject rules, string unit, string rulesName = "roshn-makkah.json")
        {
            JObject table = rules["ramp"]?["front_setback"] as JObject ?? new JObject();
            string key = unit != null && table[unit] != null ? unit : "default";
            return Metres(rules, rulesName, table[key], "ramp.front_setback." + key);
        }

        // ramp_outline.outline: front left, front right, back right, back left
        public static double[][] Outline(FrontFrame frame, double depth)
        {
            double[] left = frame.Left, right = frame.Right, normal = frame.N;
            return new[] { left, right, new[] { right[0] + depth * normal[0], right[1] + depth * normal[1] }, new[] { left[0] + depth * normal[0], left[1] + depth * normal[1] } };
        }

        private static double[] Middle(double[] first, double[] second)
        {
            return new[] { (first[0] + second[0]) / 2, (first[1] + second[1]) / 2 };
        }

        private static IEnumerable<double[][]> Edges(BuildFace face)
        {
            double[][] ring = face.Ring;
            for (int index = 0; index < ring.Length; index++)
            {
                yield return new[] { ring[index], ring[(index + 1) % ring.Length] };
            }
        }

        // cad_links.front_frame
        public static FrontFrame FrontFrame(BuildFace face, RoadLines roads, double[] limits)
        {
            if (face == null || roads.Count == 0)
            {
                return null;
            }
            var candidates = new List<Tuple<double, double[], double[]>>();
            foreach (double[][] edge in Edges(face))
            {
                double length = PyMath.Dist(edge[0], edge[1]);
                if (length < limits[0] || length >= limits[1])
                {
                    continue;
                }
                double[] middle = Middle(edge[0], edge[1]);
                candidates.Add(Tuple.Create(roads.Distance(middle[0], middle[1]), edge[0], edge[1]));
            }
            if (candidates.Count == 0)
            {
                return null;
            }
            candidates = candidates.OrderBy(item => item.Item1).ToList();
            double distance = candidates[0].Item1;
            double[] first = candidates[0].Item2, second = candidates[0].Item3;
            double full = PyMath.Dist(first, second);
            double[] along = { (second[0] - first[0]) / full, (second[1] - first[1]) / full };
            double[] normal = { -along[1], along[0] };
            if ((face.CentroidX - first[0]) * normal[0] + (face.CentroidY - first[1]) * normal[1] < 0)
            {
                normal = new[] { -normal[0], -normal[1] };
            }
            double[] right = { normal[1], -normal[0] };
            bool firstLeft = first[0] * right[0] + first[1] * right[1] < second[0] * right[0] + second[1] * right[1];
            return new FrontFrame
            {
                N = normal, R = right, Left = firstLeft ? first : second, Right = firstLeft ? second : first, Length = full, Distance = distance,
                Tied = candidates.Count > 1 && candidates[1].Item1 - distance <= FrontTie,
            };
        }

        // corner_front.outward: the unit normal of an edge out of the face, by a probe 0.01 m off its middle
        public static double[] Outward(BuildFace face, double[] first, double[] second)
        {
            double length = PyMath.Dist(first, second);
            double[] normal = { -(second[1] - first[1]) / length, (second[0] - first[0]) / length };
            double[] middle = Middle(first, second);
            return LocationPlan.Inside(face.Ring, middle[0] + normal[0] * Probe, middle[1] + normal[1] * Probe) ? new[] { -normal[0], -normal[1] } : normal;
        }

        // corner_front.road_edges: the edges whose middle lies within limit of a curb line, in the order of the ring
        public static List<RoadEdge> RoadEdges(BuildFace face, RoadLines roads, double limit)
        {
            var found = new List<RoadEdge>();
            foreach (double[][] edge in Edges(face))
            {
                double[] middle = Middle(edge[0], edge[1]);
                double distance = roads.Distance(middle[0], middle[1]);
                if (distance <= limit)
                {
                    found.Add(new RoadEdge { First = edge[0], Second = edge[1], Length = PyMath.Dist(edge[0], edge[1]), Distance = distance, Out = Outward(face, edge[0], edge[1]) });
                }
            }
            return found;
        }

        // corner_front.same_edge: the R122 frame stands on the edge, its two ends the ends of the edge as sets of points
        public static bool SameEdge(FrontFrame frame, RoadEdge edge)
        {
            if (frame == null)
            {
                return false;
            }
            Func<double[], double[], bool> same = (a, b) => a[0] == b[0] && a[1] == b[1];
            Func<double[][], double[][], bool> holds = (one, other) => one.All(point => other.Any(item => same(point, item)));
            double[][] ends = { frame.Left, frame.Right }, edgeEnds = { edge.First, edge.Second };
            return holds(ends, edgeEnds) && holds(edgeEnds, ends);
        }

        // corner_front.frame_for
        public static FrontFrame FrameFor(RoadEdge edge)
        {
            double[] normal = { -edge.Out[0], -edge.Out[1] };
            double[] right = { normal[1], -normal[0] };
            bool firstLeft = edge.First[0] * right[0] + edge.First[1] * right[1] < edge.Second[0] * right[0] + edge.Second[1] * right[1];
            return new FrontFrame { N = normal, R = right, Left = firstLeft ? edge.First : edge.Second, Right = firstLeft ? edge.Second : edge.First, Length = edge.Length, Distance = edge.Distance, Tied = false };
        }

        // corner_front.pick of the rule orientation: the road edge whose outward normal bears into the quadrant of the Orientation of the
        // sales row, the nearest the middle of the quadrant, of every road edge when none bears into it, the first of the ring on a tie,
        // null when the row holds no Orientation of the four
        public static RoadEdge Orientation(List<RoadEdge> edges, JToken salesRow)
        {
            JToken orientation = (salesRow as JObject)?["Orientation"];
            double[] quadrant;
            if (orientation == null || orientation.Type != JTokenType.String || !Quadrants.TryGetValue((string)orientation, out quadrant))
            {
                return null;
            }
            double centre = (quadrant[0] + quadrant[1]) / 2.0;
            List<RoadEdge> inside = edges.Where(edge => quadrant[0] <= PyMath.Bearing(edge.Out) && PyMath.Bearing(edge.Out) < quadrant[1]).ToList();
            List<RoadEdge> pool = inside.Count > 0 ? inside : edges;
            RoadEdge best = null;
            double least = double.PositiveInfinity;
            foreach (RoadEdge edge in pool)
            {
                double gap = PyMath.AngleGap(PyMath.Bearing(edge.Out), centre);
                if (best == null || gap < least)
                {
                    best = edge;
                    least = gap;
                }
            }
            return best;
        }

        // corner_front.corner_frame: the frame and the rule that gave it
        public static KeyValuePair<FrontFrame, string> CornerFrame(BuildFace face, RoadLines roads, double[] limits, double roadEdgeM, JToken salesRow)
        {
            FrontFrame frame = FrontFrame(face, roads, limits);
            List<RoadEdge> edges = RoadEdges(face, roads, roadEdgeM);
            if (edges.Count < 2)
            {
                return new KeyValuePair<FrontFrame, string>(frame, "nearestCurb");
            }
            RoadEdge chosen = Orientation(edges, salesRow);
            if (chosen == null)
            {
                return new KeyValuePair<FrontFrame, string>(null, "a corner plot with no Orientation in the sales sheet");
            }
            return new KeyValuePair<FrontFrame, string>(SameEdge(frame, chosen) ? frame : FrameFor(chosen), "orientation");
        }

        // plan_from_book.fronts: per plot its frame, by the corner rule when roadEdgeM and the sales rows are given, the ramp outline and the
        // road levels of its two front corners by roadLevel, the level and the surface at a point, or no level and no surface
        public static Dictionary<string, FrontItem> Of(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, IReadOnlyDictionary<string, BuildFace> faces,
            RoadLines roads, double[] limits, Func<double, double, KeyValuePair<double?, string>> roadLevel, JObject rules, double? roadEdgeM, JObject sales, string rulesName = "roshn-makkah.json")
        {
            var found = new Dictionary<string, FrontItem>();
            foreach (string number in numbers)
            {
                faces.TryGetValue(number, out BuildFace face);
                string rule = "nearestCurb";
                FrontFrame frame;
                if (face == null)
                {
                    frame = null;
                }
                else if (roadEdgeM.HasValue && sales != null)
                {
                    KeyValuePair<FrontFrame, string> corner = CornerFrame(face, roads, limits, roadEdgeM.Value, sales[number]);
                    frame = corner.Key;
                    rule = corner.Value;
                }
                else
                {
                    frame = FrontFrame(face, roads, limits);
                }
                string unit = rows.TryGetValue(number, out Dictionary<string, string> row) && row.TryGetValue("typologyRev9", out string code) && !string.IsNullOrEmpty(code) ? code : null;
                var item = new FrontItem { Face = face, Frame = frame, Unit = unit, FrontRule = rule };
                if (frame != null && unit != null)
                {
                    item.Depth = Setback(rules, unit, rulesName);
                    item.Corners = Outline(frame, item.Depth.Value);
                    KeyValuePair<double?, string>[] levels = item.Corners.Take(2).Select(corner => roadLevel(corner[0], corner[1])).ToArray();
                    item.Surfaces = levels.Select(level => level.Value).ToArray();
                    if (levels.All(level => level.Key.HasValue))
                    {
                        item.Front = levels.Select(level => level.Key.Value).ToArray();
                    }
                }
                found[number] = item;
            }
            return found;
        }

        // plan_from_book.no_road_level: why a plot has no road level at a front corner
        public static JObject NoRoadLevel(string number, FrontItem item, IReadOnlyList<string> tried, double snap)
        {
            if (item.Corners == null)
            {
                return new JObject { ["plot"] = number, ["corners"] = new JArray(), ["tried"] = new JArray(), ["reason"] = "no front frame, so no front corners to take a road level at" };
            }
            var missing = new JArray(new[] { "left", "right" }.Where((side, index) => index < item.Surfaces.Length && item.Surfaces[index] == null));
            return new JObject
            {
                ["plot"] = number, ["corners"] = missing, ["tried"] = new JArray(tried.Cast<object>().ToArray()),
                ["reason"] = "no road surface covers the corner within snapM " + SheetRows.PyRepr(snap) + " m, so no road level is given (R344)",
            };
        }

        // plan_from_book.front_step: per plot of the scope every field of the front the build takes, null where it has none
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, FrontItem> fronts, IReadOnlyList<string> tried, double snap)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                FrontItem item = fronts[number];
                FrontFrame frame = item.Frame;
                double[] levels = item.Front;
                found[number] = new Dictionary<string, object>
                {
                    ["frontRule"] = item.FrontRule, ["unit"] = item.Unit, ["left"] = frame?.Left, ["right"] = frame?.Right, ["normal"] = frame?.N, ["rightHand"] = frame?.R,
                    ["length"] = frame == null ? null : (object)frame.Length, ["distance"] = frame == null ? null : (object)frame.Distance, ["tied"] = frame == null ? null : (object)frame.Tied,
                    ["depth"] = item.Depth, ["corners"] = item.Corners, ["roadLevelLeft"] = levels == null ? null : (object)levels[0], ["roadLevelRight"] = levels == null ? null : (object)levels[1],
                    ["surfaceLeft"] = item.Surfaces.Length > 0 ? item.Surfaces[0] : null, ["surfaceRight"] = item.Surfaces.Length > 1 ? item.Surfaces[1] : null,
                    ["noRoadLevel"] = item.Unit != null && item.Front == null ? NoRoadLevel(number, item, tried, snap) : null,
                };
            }
            return found;
        }
    }
}
