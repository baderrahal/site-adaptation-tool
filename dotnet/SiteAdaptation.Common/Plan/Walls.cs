using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R362, R335. The wall rules of a build: the base and the top on the higher plot or the lower, the yard depth, the top over the FFL and
    // keyPlans.wallEdgeM, plan_walls.wall_rules of Python
    public sealed class WallRules
    {
        public bool HigherBase { get; set; }

        public bool HigherTop { get; set; }

        public double Depth { get; set; }

        public double Rise { get; set; }

        public double Near { get; set; }

        // Q115. The span keyRules.wallOpen leaves open by unit pair, start and end from the front end of the edge, null for no wallOpen
        public IReadOnlyDictionary<string, double[]> Open { get; set; }
    }

    // R362. A boundary wall of a build: the plot and edge it runs along, the plots it stands on, its two ends, base and top, the plots of the
    // base and the top, and per value its source, status and the key of its detail (R401)
    public sealed class PlanWall
    {
        public string Plot { get; set; }

        public int Edge { get; set; }

        public List<string> Plots { get; set; }

        public double[][] Ends { get; set; }

        public double Base { get; set; }

        public double Top { get; set; }

        public string BasePlot { get; set; }

        public string TopPlot { get; set; }

        // Q120. The plots whose front of R355 before their turn the wall lies on, so it stands as their turn opened another edge
        public List<string> Turned { get; set; } = new List<string>();

        public Dictionary<string, string[]> Status { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    // R362. Step 11 of the plan builder, the boundary walls of a build by the rules R335 measured on the answer keys, as python/plan_walls.py gives
    // them: a wall along each edge of the face of a written plot but its front edge (keyRules.frontOpen all), its centre on the edge
    // (keyRules.wallLine centreOnTheEdge), an edge another face lies along within keyPlans.wallEdgeM parted where the edges of those faces end, each
    // part placed once from the first of its written plots in ordinal order, the base at the yard level and the top at the FFL plus the Rev 6
    // height of the plot of keyRules.wallBase and wallTop. Boundary walls only, the garden walls have no measured rule (conflict 8 of
    // docs/design/10-rule-ids.md)
    public static class Walls
    {
        public const string StepName = "walls";

        public static readonly string[] Fields = { "plots", "ends", "base", "top" };

        public static readonly string[] StepFields = { "unit", "written", "edges", "plots", "ends", "base", "top", "levelPlots", "status" };

        internal const string Source = "keyRules";
        internal const string PlotsSource = "locationPlan";

        // The override of the list that turns a link, plan_walls.ROTATION
        private const string Rotation = "rotationDeg";
        private static readonly string[][] Known =
        {
            new[] { "wallBase", "yardLevelOfTheHigherPlot", "yardLevelOfTheLowerPlot" }, new[] { "wallTop", "higherFflPlusRev6", "lowerFflPlusRev6" },
            new[] { "wallLine", "centreOnTheEdge" }, new[] { "frontOpen", "all" },
        };

        private static bool IsNumber(JToken value)
        {
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
            {
                return false;
            }
            double number = BuildFace.Number(value);
            return !double.IsNaN(number) && !double.IsInfinity(number) && number >= 0;
        }

        // plan_walls.wall_rules: null when keyRules holds none of the four wall rules, a rule no build places walls by or a length that is no number
        // refused with the line of Python
        public static WallRules Rules(JObject key, JObject levelProfile, string name, JObject rules, string rulesName, JToken near)
        {
            if (!Known.Any(entry => key?[entry[0]] != null))
            {
                return null;
            }
            foreach (string[] entry in Known)
            {
                JToken rule = (key[entry[0]] as JObject)?["rule"];
                if (rule == null || rule.Type != JTokenType.String || !entry.Skip(1).Contains((string)rule))
                {
                    throw new InvalidDataException("keyRules." + entry[0] + " of " + name + " holds no rule a build places boundary walls by, it takes " + string.Join(" or ", entry.Skip(1)) + " (R362)");
                }
            }
            JToken depth = levelProfile?["yardBelowFflM"];
            JToken rise = (rules["rev6"] as JObject)?["boundary_wall_top_above_ffl"];
            foreach (Tuple<string, JToken> value in new[] { Tuple.Create("levelProfiles." + name + ".yardBelowFflM", depth), Tuple.Create("rev6.boundary_wall_top_above_ffl", rise),
                Tuple.Create("keyPlans.wallEdgeM", near) })
            {
                if (!IsNumber(value.Item2))
                {
                    throw new InvalidDataException(value.Item1 + " is no number of 0 or more, a build places no boundary wall without it (R362)");
                }
            }
            return new WallRules
            {
                HigherBase = (string)key["wallBase"]["rule"] == "yardLevelOfTheHigherPlot", HigherTop = (string)key["wallTop"]["rule"] == "higherFflPlusRev6",
                Depth = BuildFace.Number(depth), Rise = Fronts.Metres(rules, rulesName, rise, "rev6.boundary_wall_top_above_ffl"), Near = BuildFace.Number(near),
                Open = OpenSpans(key["wallOpen"], name),
            };
        }

        // plan_walls.open_spans, Q115: the spans of keyRules.wallOpen by unit pair, null when the profile holds no wallOpen
        private static Dictionary<string, double[]> OpenSpans(JToken entry, string name)
        {
            if (entry == null)
            {
                return null;
            }
            var spans = (entry as JObject)?["spans"] as JObject;
            JToken rule = (entry as JObject)?["rule"];
            if (spans == null || rule == null || rule.Type != JTokenType.String || (string)rule != "clusterPairSpan"
                || spans.Properties().Any(span => !(span.Value is JArray pair) || pair.Count != 2 || !pair.All(IsNumber) || !(BuildFace.Number(pair[0]) < BuildFace.Number(pair[1]))))
            {
                throw new InvalidDataException("keyRules.wallOpen of " + name + " holds no span table a build leaves an edge open by, it takes clusterPairSpan with spans of two numbers, the first under the second (R362, Q115)");
            }
            return spans.Properties().ToDictionary(span => span.Name, span => new[] { BuildFace.Number(span.Value[0]), BuildFace.Number(span.Value[1]) }, StringComparer.Ordinal);
        }

        // Q115. The footprint cluster and the unit of each plot with a face, as plan_from_book.build gives kin to plan_walls.walls: the index of
        // the cluster or null, then Typology_Rev9 or null
        public static Dictionary<string, string[]> Kin(IReadOnlyDictionary<string, BuildFace> faces, IReadOnlyList<SiteCluster> clusters, IReadOnlyDictionary<string, Dictionary<string, string>> rows)
        {
            Func<string, string> unit = plot => rows.TryGetValue(plot, out Dictionary<string, string> row) && row.TryGetValue("typologyRev9", out string held) && !string.IsNullOrEmpty(held) ? held : null;
            var kin = faces.Where(item => item.Value != null).ToDictionary(item => item.Key, item => new[] { null, unit(item.Key) }, StringComparer.Ordinal);
            for (int index = 0; index < (clusters?.Count ?? 0); index++)
            {
                foreach (string plot in clusters[index].Plots)
                {
                    kin[plot] = new[] { index.ToString(System.Globalization.CultureInfo.InvariantCulture), unit(plot) };
                }
            }
            return kin;
        }

        private static bool SameEdge(double[] a, double[] b, double[][] frame)
        {
            if (frame == null)
            {
                return false;
            }
            double[] left = frame[0], right = frame[1];
            return (a[0] == left[0] && a[1] == left[1] && b[0] == right[0] && b[1] == right[1]) || (a[0] == right[0] && a[1] == right[1] && b[0] == left[0] && b[1] == left[1]);
        }

        // plan_walls.open_edge, breaker F2 of PR 79: the front of R355 given as frame, or on a plot a rotationDeg override turned, the edge of its
        // ring whose outward normal runs most against the link Y axis that rotation and mirrored give after the overrides, the inward normal of
        // the front as LinkFrames.Axes makes it, the first such edge in ring order
        public static double[][] OpenEdge(double[][] ring, double[][] frame, double rotation, bool mirrored, bool turned)
        {
            if (!turned)
            {
                return frame;
            }
            double turn = rotation * (Math.PI / 180.0);
            double[] axis = { Math.Cos(turn), Math.Sin(turn) };
            double[] right = mirrored ? new[] { -axis[0], -axis[1] } : axis;
            double[] inward = { -right[1], right[0] };
            double twice = 0.0;
            for (int index = 0; index < ring.Length; index++)
            {
                double[] a = ring[index], b = ring[(index + 1) % ring.Length];
                twice += a[0] * b[1] - b[0] * a[1];
            }
            double? best = null;
            double[][] found = frame;
            for (int index = 0; index < ring.Length; index++)
            {
                double[] a = ring[index], b = ring[(index + 1) % ring.Length];
                double dx = b[0] - a[0], dy = b[1] - a[1];
                double size = Math.Sqrt(dx * dx + dy * dy);
                if (size == 0)
                {
                    continue;
                }
                // The outward normal lies right of the edge on an anticlockwise ring and left of it on a clockwise one
                double[] outward = twice > 0 ? new[] { dy / size, -dx / size } : new[] { -dy / size, dx / size };
                double against = -(outward[0] * inward[0] + outward[1] * inward[1]);
                if (best == null || against > best.Value)
                {
                    best = against;
                    found = new[] { a, b };
                }
            }
            return found;
        }

        private static double[] Bounds(double[][] ring)
        {
            return new[] { ring.Min(point => point[0]), ring.Min(point => point[1]), ring.Max(point => point[0]), ring.Max(point => point[1]) };
        }

        private static string Pick(List<string> writers, IReadOnlyDictionary<string, double> ffls, bool higher)
        {
            string best = writers[0];
            foreach (string plot in writers.Skip(1))
            {
                if (higher ? ffls[plot] > ffls[best] : ffls[plot] < ffls[best])
                {
                    best = plot;
                }
            }
            return best;
        }

        private sealed class Other
        {
            public string Plot;
            public double[] P;
            public double[] Q;
            public bool Front;
            public bool Before;
        }

        private sealed class Span
        {
            public string Plot;
            public double Low;
            public double High;
            public bool Front;
            public bool Before;
        }

        private sealed class Part
        {
            public double Start;
            public double End;
            public List<string> Owners;
            public bool Shut;
            public HashSet<string> Moved;
        }

        // plan_walls.walls: the walls of the plots of order the plan writes, on the rings of every face of the site, the open edges of the
        // written plots as left and right and their FFL at 3 decimals, in the order of the plot, its edge and its part. fronts holds the front of
        // R355 of each written plot before a turn, so Turned lists the plots whose front a wall lies on (Q120)
        public static List<PlanWall> Of(IList<string> order, ISet<string> written, IReadOnlyDictionary<string, double[][]> rings, IReadOnlyDictionary<string, double[][]> frames,
            IReadOnlyDictionary<string, double> ffls, WallRules rules, IReadOnlyDictionary<string, double[][]> fronts = null, IReadOnlyDictionary<string, string[]> kin = null)
        {
            fronts = fronts ?? new Dictionary<string, double[][]>();
            double near = rules.Near;
            Dictionary<string, double[]> bounds = rings.ToDictionary(item => item.Key, item => Bounds(item.Value));
            var found = new List<PlanWall>();
            foreach (string plot in order)
            {
                if (!written.Contains(plot) || !rings.ContainsKey(plot))
                {
                    continue;
                }
                double[] mine = bounds[plot];
                var others = new List<Other>();
                foreach (KeyValuePair<string, double[]> item in bounds)
                {
                    double[] box = item.Value;
                    if (item.Key == plot || box[0] > mine[2] + near || box[2] < mine[0] - near || box[1] > mine[3] + near || box[3] < mine[1] - near)
                    {
                        continue;
                    }
                    double[][] frame = written.Contains(item.Key) && frames.TryGetValue(item.Key, out double[][] held) ? held : null;
                    double[][] front = written.Contains(item.Key) && fronts.TryGetValue(item.Key, out double[][] first) ? first : null;
                    double[][] ring = rings[item.Key];
                    for (int index = 0; index < ring.Length; index++)
                    {
                        double[] p = ring[index], q = ring[(index + 1) % ring.Length];
                        others.Add(new Other { Plot = item.Key, P = p, Q = q, Front = SameEdge(p, q, frame), Before = SameEdge(p, q, front) });
                    }
                }
                frames.TryGetValue(plot, out double[][] own);
                fronts.TryGetValue(plot, out double[][] ownFront);
                double[][] edges = rings[plot];
                for (int edge = 0; edge < edges.Length; edge++)
                {
                    double[] a = edges[edge], b = edges[(edge + 1) % edges.Length];
                    if (SameEdge(a, b, own))
                    {
                        continue;
                    }
                    found.AddRange(EdgeWalls(plot, edge, a, b, others, written, ffls, rules, SameEdge(a, b, ownFront), kin, ownFront));
                }
            }
            return found;
        }

        // plan_walls.edge_walls: the edges of the other faces along the edge within near, the parts between their ends, a part on the front of a
        // written plot open and a part another written plot comes first on left to it. turned is the edge on the front of plot before its turn,
        // and a part on the front of another written plot before its turn names that plot in Turned (Q120)
        private static IEnumerable<PlanWall> EdgeWalls(string plot, int edge, double[] a, double[] b, List<Other> others, ISet<string> written, IReadOnlyDictionary<string, double> ffls,
            WallRules rules, bool turned, IReadOnlyDictionary<string, string[]> kin, double[][] ownFront)
        {
            double near = rules.Near;
            double dx = b[0] - a[0], dy = b[1] - a[1];
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length == 0)
            {
                yield break;
            }
            double ux = dx / length, uy = dy / length;
            var spans = new List<Span>();
            foreach (Other other in others)
            {
                double[] p = other.P, q = other.Q;
                if (Math.Abs((p[0] - a[0]) * uy - (p[1] - a[1]) * ux) > near || Math.Abs((q[0] - a[0]) * uy - (q[1] - a[1]) * ux) > near)
                {
                    continue;
                }
                double tp = (p[0] - a[0]) * ux + (p[1] - a[1]) * uy, tq = (q[0] - a[0]) * ux + (q[1] - a[1]) * uy;
                double low = Math.Max(0.0, Math.Min(tp, tq)), high = Math.Min(length, Math.Max(tp, tq));
                if (high > low)
                {
                    spans.Add(new Span { Plot = other.Plot, Low = low, High = high, Front = other.Front, Before = other.Before });
                }
            }
            // An end of another edge within near of a kept cut or of the end of the edge parts nothing
            var kept = new List<double> { 0.0 };
            foreach (double cut in spans.SelectMany(span => new[] { span.Low, span.High }).OrderBy(value => value))
            {
                if (cut - kept[kept.Count - 1] > near && length - cut > near)
                {
                    kept.Add(cut);
                }
            }
            kept.Add(length);
            var parts = new List<Part>();
            for (int index = 0; index + 1 < kept.Count; index++)
            {
                double start = kept[index], end = kept[index + 1], middle = (start + end) / 2.0;
                List<Span> on = spans.Where(span => span.Low <= middle && middle <= span.High).ToList();
                List<string> owners = new[] { plot }.Concat(on.Select(span => span.Plot)).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
                bool shut = on.Any(span => span.Front && written.Contains(span.Plot));
                // A part that stands on the front a plot had before its turn stands as the turn opened another edge
                var moved = new HashSet<string>(on.Where(span => span.Before && written.Contains(span.Plot)).Select(span => span.Plot), StringComparer.Ordinal);
                if (turned)
                {
                    moved.Add(plot);
                }
                Part last = parts.Count == 0 ? null : parts[parts.Count - 1];
                if (last != null && last.Owners.SequenceEqual(owners) && last.Shut == shut)
                {
                    last.End = end;
                    last.Moved.UnionWith(moved);
                }
                else
                {
                    parts.Add(new Part { Start = start, End = end, Owners = owners, Shut = shut, Moved = moved });
                }
            }
            foreach (Part part in parts)
            {
                List<string> writers = part.Owners.Where(name => written.Contains(name) && ffls.ContainsKey(name)).ToList();
                if (part.Shut || writers[0] != plot)
                {
                    continue;
                }
                string basePlot = Pick(writers, ffls, rules.HigherBase), topPlot = Pick(writers, ffls, rules.HigherTop);
                foreach (double[] piece in Pieces(part.Start, part.End, Opening(part.Owners, written, a, b, length, rules, kin, ownFront), near))
                {
                    double start = piece[0], end = piece[1];
                    double[] first = start == 0.0 ? new[] { a[0], a[1] } : new[] { a[0] + ux * start, a[1] + uy * start };
                    double[] last = end == length ? new[] { b[0], b[1] } : new[] { a[0] + ux * end, a[1] + uy * end };
                    yield return new PlanWall
                    {
                        Plot = plot, Edge = edge, Plots = part.Owners, Ends = new[] { first, last }, Base = ffls[basePlot] - rules.Depth, Top = ffls[topPlot] + rules.Rise,
                        BasePlot = basePlot, TopPlot = topPlot, Turned = part.Owners.Where(part.Moved.Contains).ToList(),
                    };
                }
            }
        }

        // plan_walls.opening, Q115: the part of the edge keyRules.wallOpen leaves open for two written units of one footprint cluster whose pair
        // the table holds, start and end along the edge from a, measured from the end nearer the middle of the front of R355, else null
        private static double[] Opening(List<string> owners, ISet<string> written, double[] a, double[] b, double length, WallRules rules, IReadOnlyDictionary<string, string[]> kin, double[][] front)
        {
            if (rules.Open == null || rules.Open.Count == 0 || kin == null || front == null || owners.Count != 2 || !owners.All(other => written.Contains(other) && kin.ContainsKey(other)))
            {
                return null;
            }
            string[] first = kin[owners[0]], second = kin[owners[1]];
            if (first[0] == null || first[0] != second[0] || string.IsNullOrEmpty(first[1]) || string.IsNullOrEmpty(second[1]))
            {
                return null;
            }
            if (!rules.Open.TryGetValue(string.Join("|", new[] { first[1], second[1] }.OrderBy(unit => unit, StringComparer.Ordinal)), out double[] span))
            {
                return null;
            }
            double[] middle = { (front[0][0] + front[1][0]) / 2.0, (front[0][1] + front[1][1]) / 2.0 };
            double toA = (a[0] - middle[0]) * (a[0] - middle[0]) + (a[1] - middle[1]) * (a[1] - middle[1]);
            double toB = (b[0] - middle[0]) * (b[0] - middle[0]) + (b[1] - middle[1]) * (b[1] - middle[1]);
            return toA <= toB ? span : new[] { length - span[1], length - span[0] };
        }

        // plan_walls.pieces, Q115: the part start to end less the open gap, each piece left longer than near
        private static List<double[]> Pieces(double start, double end, double[] gap, double near)
        {
            if (gap == null || gap[1] <= start || gap[0] >= end)
            {
                return new List<double[]> { new[] { start, end } };
            }
            var found = new List<double[]>();
            if (gap[0] - start > near)
            {
                found.Add(new[] { start, gap[0] });
            }
            if (end - gap[1] > near)
            {
                found.Add(new[] { gap[1], end });
            }
            return found;
        }

        private static double? Ffl(PlotPlan plan)
        {
            object value = plan?.Overrides != null && plan.Overrides.TryGetValue("ffl", out object held) ? held : null;
            value = value is StepValue step ? step.Raw : value;
            value = value is JValue token ? token.Value : value;
            return value == null ? (double?)null : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        // R365. Shared with PlanSummary, a plan whose written is missing, null or false is not written
        internal static bool Written(PlotPlan plan)
        {
            return plan?.Overrides != null && plan.Overrides.TryGetValue("written", out object written) && written is bool yes && yes;
        }

        // The walls of the plots of order a build writes, on the faces of the whole site and the fronts of the build, the FFL after the
        // overrides at the 3 decimals of the plan, as plan_from_book.build takes them
        public static List<PlanWall> OfPlans(IList<string> order, IReadOnlyDictionary<string, PlotPlan> plans, IReadOnlyDictionary<string, BuildFace> faces,
            IReadOnlyDictionary<string, FrontItem> fronts, WallRules rules, IReadOnlyDictionary<string, string[]> kin = null)
        {
            var ffls = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, PlotPlan> item in plans)
            {
                double? ffl = Ffl(item.Value);
                if (Written(item.Value) && ffl.HasValue)
                {
                    ffls[item.Key] = Rounding.Fixed(ffl.Value, 3);
                }
            }
            Dictionary<string, double[][]> rings = faces.Where(item => item.Value != null).ToDictionary(item => item.Key, item => item.Value.Ring);
            var frames = new Dictionary<string, double[][]>(StringComparer.Ordinal);
            var faced = new Dictionary<string, double[][]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, FrontItem> item in fronts)
            {
                if (plans.TryGetValue(item.Key, out PlotPlan plan) && Written(plan) && item.Value?.Frame != null && rings.ContainsKey(item.Key))
                {
                    // Breaker F2 of PR 79. The open edge after the overrides, the front of R355 or the edge a rotation override turned the link to face
                    var rotation = (StepValue)plan.Overrides["rotationDeg"];
                    bool turned = AppliedOf(plan).Contains(Rotation);
                    faced[item.Key] = new[] { item.Value.Frame.Left, item.Value.Frame.Right };
                    frames[item.Key] = OpenEdge(rings[item.Key], faced[item.Key],
                        Convert.ToDouble(rotation.Written, System.Globalization.CultureInfo.InvariantCulture), (bool)(JToken)plan.Overrides["mirrored"], turned);
                }
            }
            // Q120. The fronts before a turn, so a wall names the rotation of a plot only where the turn decides whether it stands
            return Of(order, new HashSet<string>(ffls.Keys, StringComparer.Ordinal), rings, frames, ffls, rules, faced, kin);
        }

        internal static List<string> AppliedOf(PlotPlan plan)
        {
            return plan?.AppliedOrder ?? plan?.Overrides["applied"] as List<string> ?? new List<string>();
        }

        // plan_walls.mark: the status of each value of a wall. The base and the top take the FFL of the highest or lowest of the written plots of
        // the wall, so every override of the plots of the wall, in their order, whose set holds the field touched it and is named, heightM the
        // base and the top (Q112, Q113, breaker F3 of PR 79, R2 of the review of PR 85). A turn decides only whether a wall stands, so a rotation
        // of a plot is named only on a wall of Turned, and the plots of a wall come from the faces alone (Q120). The base and the top read what
        // the overrides of the plot of their level still read (Q119)
        public static void Mark(PlanWall wall, ValueStatusTable table, IReadOnlyDictionary<string, PlotPlan> plans)
        {
            Func<string, List<string>> kept = name => (plans.TryGetValue(name, out PlotPlan plan) ? AppliedOf(plan) : new List<string>())
                .Where(applied => applied != Rotation || (wall.Turned ?? new List<string>()).Contains(name)).ToList();
            List<string> done = wall.Plots.SelectMany(kept).ToList();
            var reading = new Dictionary<string, List<string>> { ["base"] = kept(wall.BasePlot), ["top"] = kept(wall.TopPlot) };
            foreach (string field in Fields)
            {
                string source = field == "plots" ? PlotsSource : Source;
                Tuple<string, JObject, string> status = table.Of("walls." + field, source, null, done, null, null, reading.TryGetValue(field, out List<string> setters) ? setters : null);
                // Q107. The source stays that of the value, an override that moved it named in the key alone
                wall.Status[field] = new[] { source, status.Item1, status.Item3 };
            }
        }

        // plan_walls.step: per plot of the scope the walls the plan places from it
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans, IReadOnlyList<PlanWall> walls)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                List<PlanWall> mine = walls.Where(wall => wall.Plot == number).ToList();
                plans.TryGetValue(number, out PlotPlan plan);
                object unit = plan?.Overrides != null && plan.Overrides.TryGetValue("unit", out object held) ? held : null;
                found[number] = new Dictionary<string, object>
                {
                    ["unit"] = unit, ["written"] = Written(plan), ["edges"] = mine.Select(wall => wall.Edge).ToList(), ["plots"] = mine.Select(wall => wall.Plots).ToList(),
                    ["ends"] = mine.Select(wall => wall.Ends).ToList(), ["base"] = mine.Select(wall => wall.Base).ToList(), ["top"] = mine.Select(wall => wall.Top).ToList(),
                    ["levelPlots"] = mine.Select(wall => new List<string> { wall.BasePlot, wall.TopPlot }).ToList(),
                    ["status"] = new JArray(mine.Select(wall => new JObject(Fields.Select(field => new JProperty(field, new JObject { ["status"] = wall.Status[field][1], ["statusFrom"] = wall.Status[field][2] }))))),
                };
            }
            return found;
        }

        // plan_walls.step: the table statusFrom of the step file, the detail of each key the walls name, once, in the order of the keys
        public static JObject Tables(IEnumerable<PlanWall> walls, ValueStatusTable table)
        {
            var details = new JObject();
            foreach (string key in walls.SelectMany(wall => Fields.Select(field => wall.Status[field][2])).Distinct().OrderBy(key => key, StringComparer.Ordinal))
            {
                details[key] = table.Used[key];
            }
            return new JObject { ["statusFrom"] = details };
        }
    }
}
