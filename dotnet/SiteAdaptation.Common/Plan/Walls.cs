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

        private const string Source = "keyRules";
        private const string PlotsSource = "locationPlan";
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
            };
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
        }

        private sealed class Span
        {
            public string Plot;
            public double Low;
            public double High;
            public bool Front;
        }

        // plan_walls.walls: the walls of the plots of order the plan writes, on the rings of every face of the site, the front frames of the
        // written plots as left and right and their FFL at 3 decimals, in the order of the plot, its edge and its part
        public static List<PlanWall> Of(IList<string> order, ISet<string> written, IReadOnlyDictionary<string, double[][]> rings, IReadOnlyDictionary<string, double[][]> frames,
            IReadOnlyDictionary<string, double> ffls, WallRules rules)
        {
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
                    double[][] ring = rings[item.Key];
                    for (int index = 0; index < ring.Length; index++)
                    {
                        double[] p = ring[index], q = ring[(index + 1) % ring.Length];
                        others.Add(new Other { Plot = item.Key, P = p, Q = q, Front = SameEdge(p, q, frame) });
                    }
                }
                frames.TryGetValue(plot, out double[][] own);
                double[][] edges = rings[plot];
                for (int edge = 0; edge < edges.Length; edge++)
                {
                    double[] a = edges[edge], b = edges[(edge + 1) % edges.Length];
                    if (SameEdge(a, b, own))
                    {
                        continue;
                    }
                    found.AddRange(EdgeWalls(plot, edge, a, b, others, written, ffls, rules));
                }
            }
            return found;
        }

        // plan_walls.edge_walls: the edges of the other faces along the edge within near, the parts between their ends, a part on the front of a
        // written plot open and a part another written plot comes first on left to it
        private static IEnumerable<PlanWall> EdgeWalls(string plot, int edge, double[] a, double[] b, List<Other> others, ISet<string> written, IReadOnlyDictionary<string, double> ffls,
            WallRules rules)
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
                    spans.Add(new Span { Plot = other.Plot, Low = low, High = high, Front = other.Front });
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
            var parts = new List<Tuple<double, double, List<string>, bool>>();
            for (int index = 0; index + 1 < kept.Count; index++)
            {
                double start = kept[index], end = kept[index + 1], middle = (start + end) / 2.0;
                List<Span> on = spans.Where(span => span.Low <= middle && middle <= span.High).ToList();
                List<string> owners = new[] { plot }.Concat(on.Select(span => span.Plot)).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
                bool shut = on.Any(span => span.Front && written.Contains(span.Plot));
                Tuple<double, double, List<string>, bool> last = parts.Count == 0 ? null : parts[parts.Count - 1];
                if (last != null && last.Item3.SequenceEqual(owners) && last.Item4 == shut)
                {
                    parts[parts.Count - 1] = Tuple.Create(last.Item1, end, owners, shut);
                }
                else
                {
                    parts.Add(Tuple.Create(start, end, owners, shut));
                }
            }
            foreach (Tuple<double, double, List<string>, bool> part in parts)
            {
                List<string> writers = part.Item3.Where(name => written.Contains(name) && ffls.ContainsKey(name)).ToList();
                if (part.Item4 || writers[0] != plot)
                {
                    continue;
                }
                double start = part.Item1, end = part.Item2;
                double[] first = start == 0.0 ? new[] { a[0], a[1] } : new[] { a[0] + ux * start, a[1] + uy * start };
                double[] last = end == length ? new[] { b[0], b[1] } : new[] { a[0] + ux * end, a[1] + uy * end };
                string basePlot = Pick(writers, ffls, rules.HigherBase), topPlot = Pick(writers, ffls, rules.HigherTop);
                yield return new PlanWall
                {
                    Plot = plot, Edge = edge, Plots = part.Item3, Ends = new[] { first, last }, Base = ffls[basePlot] - rules.Depth, Top = ffls[topPlot] + rules.Rise,
                    BasePlot = basePlot, TopPlot = topPlot,
                };
            }
        }

        private static double? Ffl(PlotPlan plan)
        {
            object value = plan?.Overrides != null && plan.Overrides.TryGetValue("ffl", out object held) ? held : null;
            value = value is StepValue step ? step.Raw : value;
            value = value is JValue token ? token.Value : value;
            return value == null ? (double?)null : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool Written(PlotPlan plan)
        {
            return plan?.Overrides != null && plan.Overrides.TryGetValue("written", out object written) && written is bool yes && yes;
        }

        // The walls of the plots of order a build writes, on the faces of the whole site and the fronts of the build, the FFL after the
        // overrides at the 3 decimals of the plan, as plan_from_book.build takes them
        public static List<PlanWall> OfPlans(IList<string> order, IReadOnlyDictionary<string, PlotPlan> plans, IReadOnlyDictionary<string, BuildFace> faces,
            IReadOnlyDictionary<string, FrontItem> fronts, WallRules rules)
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
            var frames = new Dictionary<string, double[][]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, FrontItem> item in fronts)
            {
                if (plans.TryGetValue(item.Key, out PlotPlan plan) && Written(plan) && item.Value?.Frame != null)
                {
                    frames[item.Key] = new[] { item.Value.Frame.Left, item.Value.Frame.Right };
                }
            }
            return Of(order, new HashSet<string>(ffls.Keys, StringComparer.Ordinal), faces.Where(item => item.Value != null).ToDictionary(item => item.Key, item => item.Value.Ring), frames, ffls, rules);
        }

        // plan_walls.mark: the status of each value of a wall, the base and the top as moved by an override of their plot that moved its FFL, in
        // the order the build applied them, rulePlans the plots built with no override (Q107)
        public static void Mark(PlanWall wall, ValueStatusTable table, IReadOnlyDictionary<string, PlotPlan> plans, IReadOnlyDictionary<string, PlotPlan> rulePlans)
        {
            foreach (string field in Fields)
            {
                string level = field == "base" ? wall.BasePlot : field == "top" ? wall.TopPlot : null;
                string source = field == "plots" ? PlotsSource : Source;
                List<string> applied = new List<string>();
                bool unmoved = false;
                if (level != null && plans.TryGetValue(level, out PlotPlan plan))
                {
                    applied = plan.AppliedOrder ?? plan.Overrides["applied"] as List<string> ?? new List<string>();
                    PlotPlan rule = null;
                    rulePlans?.TryGetValue(level, out rule);
                    rule = rule != null && Written(rule) ? rule : null;
                    unmoved = applied.Count > 0 && rule != null && ValueStatus.Unmoved(table.MovedBy("walls." + field), plan, rule, table.Decimals);
                }
                Tuple<string, JObject, string> status = table.Of("walls." + field, source, null, applied, unmoved);
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
