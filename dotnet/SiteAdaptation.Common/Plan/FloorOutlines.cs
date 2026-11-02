using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Clipper2Lib;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R358. The floors of a build by the rules of keyRules before an override of the list moves them (R360): the Generic floor on the whole
    // face, the ramp of R127 with the Z of its corners by the chain of Step 8 and its type, and the yard on the outline keyRules fits its
    // unit, mirror flag and plot width, placed by the link of R356, as plan_from_book.generic_floor, ramp_floor and floor_step and
    // plan_from_cad.flat_floor give them
    public static class FloorOutlines
    {
        public const string StepName = "floors";

        // The line of plan_from_book.YARD_GROUPS
        public const string YardGroups = "the step floors of the parity harness takes the yard outlines of keyRules.yardOutline groups, keyRules of {0} codes fittedOutline, whose outlines "
            + "lie in config/floor_outlines.json, which the C# does not read (R358)";

        // plan_from_book.NO_BAND, the lack of a ramp whose slope no band of the ramp types holds, under rev6
        public const string NoBand = "no ramp type for a slope of {0} percent in the bands of rampTypes (R408)";

        // R475. floor_fit.YARD_CUT_EMPTY, the line of a plot whose fitted yard outline the cut leaves empty
        public const string YardCutEmpty = "the cut of R475 leaves the fitted yard outline empty";

        // floor_fit.EPSILON, the width of a fitted outline a plot takes
        private const double Epsilon = 1e-9;

        // plan_from_book.ramp_heights: the front pair at the road, the back pair El @ Entry plus its front corner less the mean of the two
        public static double[] RampHeights(double[] front, double entry)
        {
            double middle = (front[0] + front[1]) / 2.0;
            return new[] { front[0], front[1], entry + front[1] - middle, entry + front[0] - middle };
        }

        // R470. plan_from_book.joint_near: floors.rampJoint.nearM of config, a length above 0, null while floors.rampJoint.join is false (Q340)
        public static double? JointNear(JObject config, string configName)
        {
            JToken join = config["floors"]?["rampJoint"]?["join"];
            if (join == null || join.Type != JTokenType.Boolean)
            {
                throw new InvalidDataException(configName + " floors.rampJoint.join must be true or false (R470)");
            }
            JToken near = config["floors"]?["rampJoint"]?["nearM"];
            if (near == null || (near.Type != JTokenType.Integer && near.Type != JTokenType.Float) || BuildFace.Number(near) <= 0)
            {
                throw new InvalidDataException(configName + " floors.rampJoint.nearM must be a length above 0 (R470)");
            }
            return (bool)join ? BuildFace.Number(near) : (double?)null;
        }

        // R470. plan_from_book.joint_levels: ramps by plot, the plan corners, their raw levels and the road levels of the front pair before any
        // rampFrontM gap. Front corners of two or more plots within near in plan, chained, are one joint and each takes the road level of the
        // first corner of the joint in the order of the plots and corners given. The back pair keeps El @ Entry (R358) and joins nothing. The
        // new levels of each plot a joint changed
        public static Dictionary<string, double[]> JointLevels(IEnumerable<Tuple<string, double[][], double[], double[]>> ramps, double near)
        {
            var heightsOf = new Dictionary<string, double[]>();
            var corners = new List<Tuple<string, int, double, double, double>>();
            foreach (Tuple<string, double[][], double[], double[]> ramp in ramps)
            {
                heightsOf[ramp.Item1] = ramp.Item3;
                for (int index = 0; index < Math.Min(2, Math.Min(ramp.Item2.Length, ramp.Item4.Length)); index++)
                {
                    corners.Add(Tuple.Create(ramp.Item1, index, ramp.Item2[index][0], ramp.Item2[index][1], ramp.Item4[index]));
                }
            }
            int[] parent = Enumerable.Range(0, corners.Count).ToArray();
            int Root(int at)
            {
                while (parent[at] != at)
                {
                    parent[at] = parent[parent[at]];
                    at = parent[at];
                }
                return at;
            }
            // A grid of cells near wide finds the corners within near among the 3 by 3 cells around each, the joints the same as every pair gives
            var cells = new Dictionary<Tuple<long, long>, List<int>>();
            for (int at = 0; at < corners.Count; at++)
            {
                var cell = Tuple.Create((long)Math.Floor(corners[at].Item3 / near), (long)Math.Floor(corners[at].Item4 / near));
                if (!cells.TryGetValue(cell, out List<int> inCell))
                {
                    cells[cell] = inCell = new List<int>();
                }
                inCell.Add(at);
            }
            for (int at = 0; at < corners.Count; at++)
            {
                long column = (long)Math.Floor(corners[at].Item3 / near), row = (long)Math.Floor(corners[at].Item4 / near);
                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        if (!cells.TryGetValue(Tuple.Create(column + dx, row + dy), out List<int> inCell))
                        {
                            continue;
                        }
                        foreach (int other in inCell)
                        {
                            double x = corners[other].Item3 - corners[at].Item3, y = corners[other].Item4 - corners[at].Item4;
                            if (other > at && corners[other].Item1 != corners[at].Item1 && x * x + y * y <= near * near)
                            {
                                int a = Root(at), b = Root(other);
                                parent[Math.Max(a, b)] = Math.Min(a, b);
                            }
                        }
                    }
                }
            }
            var groups = new Dictionary<int, List<int>>();
            for (int at = 0; at < corners.Count; at++)
            {
                int root = Root(at);
                if (!groups.TryGetValue(root, out List<int> members))
                {
                    groups[root] = members = new List<int>();
                }
                members.Add(at);
            }
            var found = new Dictionary<string, double[]>();
            foreach (List<int> members in groups.Values)
            {
                if (members.Select(at => corners[at].Item1).Distinct().Count() < 2)
                {
                    continue;
                }
                double level = corners[members[0]].Item5;
                foreach (int at in members)
                {
                    string plot = corners[at].Item1;
                    if (heightsOf[plot][corners[at].Item2] == level)
                    {
                        continue;
                    }
                    if (!found.TryGetValue(plot, out double[] heights))
                    {
                        found[plot] = heights = (double[])heightsOf[plot].Clone();
                    }
                    heights[corners[at].Item2] = level;
                }
            }
            return found;
        }

        // floor_fit.width_key: the plot width in whole steps by round() of Python, half to even, then to 3 decimals by round() again, a lookup
        // that is never written as a level or a length (Phase 4b)
        public static double WidthKey(double width, double step)
        {
            return PyMath.Round(Math.Round(width / step, MidpointRounding.ToEven) * step, 3);
        }

        // floor_fit.width_step: cadLinks.floorWidthStepM, a number above 0
        public static double WidthStep(JObject config, string configName)
        {
            JToken step = config["cadLinks"]?["floorWidthStepM"];
            if (step == null || (step.Type != JTokenType.Integer && step.Type != JTokenType.Float) || BuildFace.Number(step) <= 0)
            {
                throw new InvalidDataException(configName + " cadLinks.floorWidthStepM must be a length above 0");
            }
            return BuildFace.Number(step);
        }

        // floor_fit.outline_for: the first group of the unit, mirror flag and width within EPSILON, else null
        public static JObject OutlineFor(JArray groups, string unit, bool mirrored, double width)
        {
            foreach (JObject group in groups.OfType<JObject>())
            {
                JToken flag = group["mirrored"];
                if ((string)group["unit"] == unit && flag != null && flag.Type == JTokenType.Boolean && (bool)flag == mirrored && Math.Abs(BuildFace.Number(group["width"]) - width) < Epsilon)
                {
                    return group;
                }
            }
            return null;
        }

        // floor_fit.to_world: a loop of the link frame in plan, the link Y axis turned by plus 90 degrees from X, by minus 90 when mirrored
        public static List<double[]> ToWorld(JArray loop, double[] origin, double rotation, bool mirrored)
        {
            double turn = PyMath.Radians(rotation);
            double[] x = { Math.Cos(turn), Math.Sin(turn) };
            double[] y = mirrored ? new[] { x[1], -x[0] } : new[] { -x[1], x[0] };
            return loop.Select(point => new[]
            {
                origin[0] + BuildFace.Number(point[0]) * x[0] + BuildFace.Number(point[1]) * y[0], origin[1] + BuildFace.Number(point[0]) * x[1] + BuildFace.Number(point[1]) * y[1],
            }).ToList();
        }

        // R475. floor_fit.grid_rings: rings in whole mm, on the 3 decimals the plan writes by R385, a ring of under 3 points left out
        private static Paths64 GridRings(IEnumerable<IList<double[]>> loops)
        {
            var rings = new Paths64();
            foreach (IList<double[]> loop in loops.Where(loop => loop.Count >= 3))
            {
                var ring = new Path64();
                foreach (double[] point in loop)
                {
                    ring.Add(new Point64((long)Math.Round(Rounding.Fixed(point[0], 3) * 1000, MidpointRounding.ToEven), (long)Math.Round(Rounding.Fixed(point[1], 3) * 1000, MidpointRounding.ToEven)));
                }
                rings.Add(ring);
            }
            return rings;
        }

        // R475. floor_fit.clip_rings: one run of the clipper by even-odd fill on rings in whole mm, a collinear vertex left out
        private static Paths64 ClipRings(ClipType kind, Paths64 subject, Paths64 other)
        {
            var found = new Paths64();
            if (subject.Count == 0)
            {
                return found;
            }
            var clipper = new Clipper64 { PreserveCollinear = false };
            clipper.AddSubject(subject);
            if (other != null && other.Count > 0)
            {
                clipper.AddClip(other);
            }
            clipper.Execute(kind, FillRule.EvenOdd, found);
            return found;
        }

        // R475. floor_fit.doubled_area: twice the area of rings in whole mm, a hole of the clipper counted negative. Each ring from its own first
        // vertex, since a product of two coordinates in mm can pass a long and the size of a plot cannot
        private static long DoubledArea(IEnumerable<Path64> rings)
        {
            long sum = 0;
            foreach (Path64 ring in rings)
            {
                for (int at = 1; at < ring.Count - 1; at++)
                {
                    sum += (ring[at].X - ring[0].X) * (ring[at + 1].Y - ring[0].Y) - (ring[at + 1].X - ring[0].X) * (ring[at].Y - ring[0].Y);
                }
            }
            return sum;
        }

        // R475. The compare of two rings as Python compares two lists, vertex by vertex, x then y, a ring that ends first before the longer one
        private static int RingOrder(Path64 first, Path64 second)
        {
            for (int at = 0; at < Math.Min(first.Count, second.Count); at++)
            {
                int order = first[at].X != second[at].X ? first[at].X.CompareTo(second[at].X) : first[at].Y.CompareTo(second[at].Y);
                if (order != 0)
                {
                    return order;
                }
            }
            return first.Count.CompareTo(second.Count);
        }

        // R475. floor_fit.fixed_rings: the one order of the two languages, each ring from its smallest (x, y), the winding of the clipper kept, outer
        // rings anticlockwise and holes clockwise, outer rings first, each kind sorted
        private static List<Path64> FixedRings(Paths64 rings)
        {
            var outer = new List<Path64>();
            var holes = new List<Path64>();
            foreach (Path64 ring in rings)
            {
                int start = 0;
                for (int at = 1; at < ring.Count; at++)
                {
                    if (ring[at].X < ring[start].X || (ring[at].X == ring[start].X && ring[at].Y < ring[start].Y))
                    {
                        start = at;
                    }
                }
                var placed = new Path64();
                placed.AddRange(ring.Skip(start));
                placed.AddRange(ring.Take(start));
                (DoubledArea(new[] { ring }) > 0 ? outer : holes).Add(placed);
            }
            outer.Sort(RingOrder);
            holes.Sort(RingOrder);
            return outer.Concat(holes).ToList();
        }

        // R475. plan_from_book.yard_in_plot: the even-odd yard loops cut to the face and by the ramp on the grid of 1 mm, each by what the plot has, in
        // metres as whole mm / 1000.0 in the order of FixedRings. No loops when the cut leaves nothing. A yard the cut leaves its own area on the grid is
        // the given loops themselves
        public static List<List<double[]>> YardInPlot(List<List<double[]>> loops, double[][] face, double[][] ramp)
        {
            Paths64 whole = ClipRings(ClipType.Union, GridRings(loops), null);
            Paths64 cut = whole;
            if (face != null && face.Length > 0)
            {
                cut = ClipRings(ClipType.Intersection, cut, GridRings(new IList<double[]>[] { face }));
            }
            if (ramp != null && ramp.Length > 0)
            {
                cut = ClipRings(ClipType.Difference, cut, GridRings(new IList<double[]>[] { ramp }));
            }
            if (cut.Count == 0)
            {
                return new List<List<double[]>>();
            }
            if (DoubledArea(cut) == DoubledArea(whole))
            {
                return loops;
            }
            return FixedRings(cut).Select(ring => ring.Select(point => new[] { point.X / 1000.0, point.Y / 1000.0 }).ToList()).ToList();
        }

        // plan_rules.key_yard: the depth under the FFL keyRules.yard gives a zone, null for no yard
        public static double? YardDepth(JObject keyRules, string zone)
        {
            JToken value = keyRules["yard"]["table"][zone];
            if (value == null)
            {
                throw new InvalidDataException("keyRules.yard holds no yard for " + zone + " (R337)");
            }
            if (value.Type == JTokenType.String && (string)value == "noYard")
            {
                return null;
            }
            return value.Type == JTokenType.String ? double.Parse(((string)value).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture) : BuildFace.Number(value);
        }

        // plan_from_book.build: a unit plot of the scope whose zone keyRules.yard holds no yard is refused, never given another value
        public static void CheckZones(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, JObject keyRules)
        {
            JObject table = (JObject)keyRules["yard"]["table"];
            var missing = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string number in numbers)
            {
                if (!rows.TryGetValue(number, out Dictionary<string, string> row) || !row.TryGetValue("typologyRev9", out string unit) || string.IsNullOrEmpty(unit))
                {
                    continue;
                }
                row.TryGetValue("zone", out string zone);
                if (table[zone ?? ""] == null)
                {
                    string named = string.IsNullOrEmpty(zone) ? "no zone" : zone;
                    if (!missing.ContainsKey(named))
                    {
                        missing[named] = new List<string>();
                    }
                    missing[named].Add(number);
                }
            }
            if (missing.Count > 0)
            {
                throw new InvalidDataException("keyRules.yard holds no yard for " + string.Join(", ", missing.Select(item => item.Key + " (" + item.Value.Count + " plots, " + string.Join(" ", item.Value.Take(5)) + ")"))
                    + ", an area missing from the table is refused and never given another value (R337)");
            }
        }

        private static JObject Lack(string key, string why)
        {
            return new JObject { ["key"] = key, ["why"] = why };
        }

        // plan_from_book.floor_step: per unit plot of the scope the Generic outline, the ramp outline with the Z of its corners, class and type,
        // and the yard with its depth, unit, width, type and outline on the link by the rule, with what the floors lack
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, LevelBuild build,
            IReadOnlyDictionary<string, Dictionary<string, object>> links, JObject keyRules, JObject levelProfile, JObject rules, double step, string profileName)
        {
            if ((string)keyRules["yardOutline"]["rule"] != "fittedOutlineRefit")
            {
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, YardGroups, profileName));
            }
            JArray groups = keyRules["yardOutline"]["groups"] as JArray ?? new JArray();
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                rows.TryGetValue(number, out Dictionary<string, string> row);
                string unit = null, zone = null;
                row?.TryGetValue("typologyRev9", out unit);
                row?.TryGetValue("zone", out zone);
                unit = string.IsNullOrEmpty(unit) ? null : unit;
                var made = new Dictionary<string, object>
                {
                    ["unit"] = unit, ["generic"] = null, ["rampOutline"] = null, ["rampZ"] = null, ["rampClass"] = null, ["rampType"] = null, ["yardDepth"] = null, ["yardUnit"] = null,
                    ["yardWidth"] = null, ["yardType"] = null, ["yardOutline"] = null, ["lacks"] = new JArray(), ["yardGrade"] = null,
                };
                found[number] = made;
                if (unit == null)
                {
                    continue;
                }
                var lacks = new JArray();
                build.Fronts.TryGetValue(number, out FrontItem item);
                build.Entries.TryGetValue(number, out EntryLevel entry);
                links.TryGetValue(number, out Dictionary<string, object> link);
                if (item?.Face != null)
                {
                    made["generic"] = new List<double[][]> { item.Face.Ring };
                }
                if (item?.Corners != null)
                {
                    made["rampOutline"] = item.Corners;
                }
                if (item?.Front != null && entry != null)
                {
                    made["rampZ"] = RampHeights(item.Front, entry.Rule);
                }
                if (build.Rev6)
                {
                    // R410. The grade of the back yard by the manual and no yard floor. R408: the ramp type of the band of the slope, the result
                    made["yardGrade"] = DesignBasis.YardGrade(unit, rules);
                    JToken kind = entry == null ? null : DesignBasis.RampType(entry.Slope, build.Bands);
                    made["rampType"] = kind;
                    if (entry != null && kind == null)
                    {
                        lacks.Add(Lack("floors.ramp", string.Format(CultureInfo.InvariantCulture, NoBand, SheetRows.PyRepr(entry.Slope))));
                    }
                    made["lacks"] = lacks;
                    continue;
                }
                double? below = YardDepth(keyRules, zone ?? "");
                made["yardDepth"] = below;
                double[] insert = link?["insert"] as double[];
                if (below.HasValue && insert != null)
                {
                    bool mirrored = (bool)(JToken)link["mirrored"];
                    string keo = (rules["typologies"]?[unit] as JObject)?["keo_file"]?.Type == JTokenType.String ? (string)rules["typologies"][unit]["keo_file"] : null;
                    string yardUnit = groups.OfType<JObject>().Any(group => (string)group["unit"] == unit) ? unit : string.IsNullOrEmpty(keo) ? unit : keo;
                    double width = WidthKey(item.Frame.Length, step);
                    JObject group = OutlineFor(groups, yardUnit, mirrored, width);
                    made["yardUnit"] = yardUnit;
                    made["yardWidth"] = width;
                    if (group == null)
                    {
                        lacks.Add(Lack("floors.yard", "no fitted outline for " + yardUnit + (mirrored ? " mirrored" : " not mirrored") + " on a plot " + SheetRows.PyRepr(width) + " m wide"));
                    }
                    else
                    {
                        double[] origin = insert.Select(value => Rounding.Fixed(value, 3)).ToArray();
                        double rotation = (double)((StepValue)link["rotationDeg"]).Written;
                        // R475. The yard in its plot, by the ramp of the rule, as no override has moved it yet
                        List<List<double[]>> cut = YardInPlot(((JArray)group["loops"]).Select(loop => ToWorld((JArray)loop, origin, rotation, mirrored)).ToList(), item.Face?.Ring, item.Corners);
                        if (cut.Count > 0)
                        {
                            made["yardType"] = group["type"]?.DeepClone();
                            made["yardOutline"] = cut;
                        }
                        else
                        {
                            lacks.Add(Lack("floors.yard", YardCutEmpty));
                        }
                    }
                }
                JToken rampClass = keyRules["rampSlope"]["table"][unit];
                JToken rampType = rampClass != null && rampClass.Type == JTokenType.String ? levelProfile["rampTypes"][(string)rampClass] : null;
                made["rampClass"] = rampClass?.DeepClone();
                made["rampType"] = rampType?.DeepClone();
                if (rampType == null || rampType.Type == JTokenType.Null)
                {
                    lacks.Add(Lack("floors.ramp", "keyRules.rampSlope and rampTypes give no ramp type for " + unit));
                }
                made["lacks"] = lacks;
            }
            return found;
        }
    }
}
