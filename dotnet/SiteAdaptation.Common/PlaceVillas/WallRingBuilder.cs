using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R500. One wall of a prototype as the Revit side reads it, metres in the internal plan of the prototype. Centre is null for a line,
    // an arc runs from Start to End about Centre, counter clockwise when CounterClockwise. Exterior is Wall.Orientation, Flipped in it
    public sealed class RingWall
    {
        public string Id { get; set; }
        public string Function { get; set; }
        public double[] Start { get; set; }
        public double[] End { get; set; }
        public double[] Centre { get; set; }
        public bool CounterClockwise { get; set; }
        public double Width { get; set; }
        public double[] Exterior { get; set; }

        // True when the wall stands on or crosses the plane of the top of the ground slab
        public bool SpansGround { get; set; }

        // From that plane up to the top of the wall
        public double HeightM { get; set; }
    }

    // R503. One edge of the ring in walking order, a line when Centre is null, else an arc from Start to End about Centre
    public sealed class RingEdge
    {
        public RingEdge(double[] start, double[] end, double[] centre = null, bool counterClockwise = true)
        {
            Start = start;
            End = end;
            Centre = centre;
            CounterClockwise = counterClockwise;
        }

        public double[] Start { get; }
        public double[] End { get; }
        public double[] Centre { get; }
        public bool CounterClockwise { get; }
        public bool IsArc => Centre != null;
    }

    // R500 to R503. The limits of addin.lightMode, each with its why text in config
    public sealed class RingLimits
    {
        public double OutsideM { get; set; }
        public double InsideM { get; set; }
        public double MinHeightM { get; set; }
        public double ShortestEdgeM { get; set; }
        public double CornerGapM { get; set; }
        public double SlotM { get; set; }
        public double ArcStepM { get; set; }
    }

    public sealed class WallLeftOut
    {
        public WallLeftOut(string id, string why)
        {
            Id = id;
            Why = why;
        }

        public string Id { get; }
        public string Why { get; }
    }

    // R500 to R503. What the builder gives: the edges in walking order, or none and the reason
    public sealed class WallRingBuild
    {
        public IReadOnlyList<RingEdge> Edges { get; set; } = new List<RingEdge>();
        public string Reason { get; set; }
        public IReadOnlyList<string> Used { get; set; } = new List<string>();

        // The ring walls whose Orientation faces the house, so their other face is the outer one (R501)
        public IReadOnlyList<string> BackFace { get; set; } = new List<string>();
        public IReadOnlyList<WallLeftOut> LeftOut { get; set; } = new List<WallLeftOut>();
    }

    // R503. The ring as points for a reader that takes points: the start of each edge, an arc cut every arcStepM along its length
    public static class RingEdges
    {
        public static List<double[]> Points(IEnumerable<RingEdge> edges, double arcStepM)
        {
            var points = new List<double[]>();
            foreach (RingEdge edge in edges)
            {
                points.Add(edge.Start);
                if (edge.IsArc)
                {
                    double radius = RingGeometry.Distance(edge.Start, edge.Centre), first = RingGeometry.Angle(edge.Centre, edge.Start), sweep = Sweep(edge);
                    int cuts = (int)Math.Ceiling(sweep * radius / arcStepM);
                    for (int i = 1; i < cuts; i++)
                    {
                        double angle = first + (edge.CounterClockwise ? 1 : -1) * sweep * i / cuts;
                        points.Add(new[] { edge.Centre[0] + radius * Math.Cos(angle), edge.Centre[1] + radius * Math.Sin(angle) });
                    }
                }
            }
            return points;
        }

        // The middle of an arc, the third point a Revit arc is made through
        public static double[] Middle(RingEdge edge)
        {
            double radius = RingGeometry.Distance(edge.Start, edge.Centre);
            double angle = RingGeometry.Angle(edge.Centre, edge.Start) + (edge.CounterClockwise ? 1 : -1) * Sweep(edge) / 2;
            return new[] { edge.Centre[0] + radius * Math.Cos(angle), edge.Centre[1] + radius * Math.Sin(angle) };
        }

        // The ring counter clockwise, the order a solid is extruded from. A slab loop may be drawn either way, an arc counts by its middle
        public static List<RingEdge> CounterClockwise(IReadOnlyList<RingEdge> edges)
        {
            List<double[]> shape = edges.SelectMany(edge => edge.IsArc ? new[] { edge.Start, Middle(edge) } : new[] { edge.Start }).ToList();
            if (RingMath.Area(shape) >= 0)
            {
                return edges.ToList();
            }
            return edges.Reverse().Select(edge => new RingEdge(edge.End, edge.Start, edge.Centre, !edge.CounterClockwise)).ToList();
        }

        // The angle the arc turns through, above 0 and at most a full turn
        public static double Sweep(RingEdge edge)
        {
            double from = RingGeometry.Angle(edge.Centre, edge.Start), to = RingGeometry.Angle(edge.Centre, edge.End);
            double sweep = RingGeometry.Turn(edge.CounterClockwise ? to - from : from - to);
            return sweep == 0 ? 2 * Math.PI : sweep;
        }
    }

    internal static class RingGeometry
    {
        public static double Distance(double[] a, double[] b)
        {
            return Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
        }

        public static double Angle(double[] centre, double[] point)
        {
            return Math.Atan2(point[1] - centre[1], point[0] - centre[0]);
        }

        // Brought into 0 up to a full turn
        public static double Turn(double angle)
        {
            double full = 2 * Math.PI, turn = angle % full;
            return turn < 0 ? turn + full : turn;
        }

        public static double SegmentDistance(double[] point, double[] a, double[] b)
        {
            double dx = b[0] - a[0], dy = b[1] - a[1], square = dx * dx + dy * dy;
            double t = square == 0 ? 0 : Math.Max(0, Math.Min(1, ((point[0] - a[0]) * dx + (point[1] - a[1]) * dy) / square));
            return Distance(point, new[] { a[0] + t * dx, a[1] + t * dy });
        }

        // Plus outside the ring, minus inside, by the nearest edge
        public static double SignedDistance(double[] point, IReadOnlyList<double[]> ring)
        {
            double nearest = double.MaxValue;
            for (int i = 0; i < ring.Count; i++)
            {
                nearest = Math.Min(nearest, SegmentDistance(point, ring[i], ring[(i + 1) % ring.Count]));
            }
            return RingMath.Holds(ring, point) ? -nearest : nearest;
        }
    }

    // R500 to R503, Q321. The outline of a prototype from the exterior faces of its walls. The joins of the files close on none of the 13
    // prototypes, so the ring comes from geometry only: which faces see the outside gives the edges, the edge whose start lies nearest the
    // end of the last gives the order, and each corner is where two face lines cross, so a wall that stops short or runs past never shows
    public static class WallRingBuilder
    {
        // Two lines closer than this lie on one line. The files hold face lines to 0.02 mm, the smallest real step is 20 mm
        private const double OnLineM = 0.001;

        // Under this sine two lines are parallel. The walls of the 13 prototypes meet at 90 and 180 degrees only
        private const double ParallelSine = 0.001;

        // The length of one piece of a face tried for sight of the outside
        private const double SampleM = 0.05;

        public static WallRingBuild Build(IEnumerable<RingWall> walls, IReadOnlyList<double[]> slab, RingLimits limits)
        {
            var leftOut = new List<WallLeftOut>();
            var deep = new List<RingWall>();
            List<RingWall> kept = Filter(walls, slab, limits, leftOut, deep);
            List<double[][]> plans = kept.Concat(deep).Select(wall => Plan(wall, limits)).ToList();
            var back = new HashSet<string>(StringComparer.Ordinal);
            List<Face> faces = Merged(kept.SelectMany(wall => Runs(wall, plans, back, limits)).ToList());
            var used = new HashSet<string>(StringComparer.Ordinal);
            List<Face> edges = faces.Where(face => face.Length >= limits.ShortestEdgeM).ToList();
            if (edges.Count < 3)
            {
                return Refused("the exterior walls show " + edges.Count + " faces to the outside, a ring needs 3", kept, leftOut);
            }
            string reason = null;
            List<Join> ring = Walk(edges, limits, ref reason);
            if (ring == null)
            {
                return Refused(reason, kept, leftOut);
            }
            List<Face> carriers = Carriers(ring);
            List<RingEdge> closed = Closed(carriers, limits, ref reason);
            if (closed == null)
            {
                return Refused(reason, kept, leftOut);
            }
            foreach (Face face in carriers)
            {
                used.UnionWith(face.Walls);
            }
            leftOut.AddRange(kept.Where(wall => !used.Contains(wall.Id)).Select(wall => new WallLeftOut(wall.Id, "no face of it on the ring, an outer wall stands before it")));
            return new WallRingBuild
            {
                Edges = closed, Used = used.OrderBy(id => id, StringComparer.Ordinal).ToList(), BackFace = back.Where(used.Contains).OrderBy(id => id, StringComparer.Ordinal).ToList(), LeftOut = leftOut,
            };
        }

        private static WallRingBuild Refused(string reason, List<RingWall> kept, List<WallLeftOut> leftOut)
        {
            leftOut.AddRange(kept.Select(wall => new WallLeftOut(wall.Id, "the walls gave no ring")));
            return new WallRingBuild { Reason = reason, LeftOut = leftOut };
        }

        // R500. Function first, then the ground plane, then height, then the distance of the exterior face from the slab. A wall is too far
        // out when any of its face is over the outside limit, too far in when none of its face comes within the inside limit, so a wall of
        // a notch the slab does not follow stays by the end that reaches the slab edge. A wall too far in still stops a ray in deep
        private static List<RingWall> Filter(IEnumerable<RingWall> walls, IReadOnlyList<double[]> slab, RingLimits limits, List<WallLeftOut> leftOut, List<RingWall> deep)
        {
            var kept = new List<RingWall>();
            foreach (RingWall wall in walls ?? Enumerable.Empty<RingWall>())
            {
                string why = null;
                if (wall.Function != WallFunctions.Exterior)
                {
                    why = "function " + wall.Function;
                }
                else if (wall.Start == null || wall.End == null || wall.Exterior == null || !(wall.Width > 0))
                {
                    why = "its curve is no line or arc";
                }
                else if (!wall.SpansGround)
                {
                    why = "it does not stand on the ground plane";
                }
                else if (wall.HeightM < limits.MinHeightM)
                {
                    why = "its height " + Metres(wall.HeightM) + " is under wallRingMinHeightM";
                }
                else
                {
                    Face face = Face.Of(wall, false);
                    int pieces = Pieces(face.Length, 0.25);
                    List<double> distances = Enumerable.Range(0, pieces + 1).Select(i => RingGeometry.SignedDistance(face.At(face.Length * i / pieces), slab)).ToList();
                    if (distances.Max() > limits.OutsideM)
                    {
                        why = "its face stands " + Metres(distances.Max()) + " outside the slab, over wallRingOutsideM";
                    }
                    else if (-distances.Max() > limits.InsideM)
                    {
                        why = "all its face stands " + Metres(-distances.Max()) + " or more inside the slab, over wallRingInsideM";
                        deep.Add(wall);
                    }
                }
                if (why == null)
                {
                    kept.Add(wall);
                }
                else
                {
                    leftOut.Add(new WallLeftOut(wall.Id, why));
                }
            }
            return kept;
        }

        private static int Pieces(double length, double piece)
        {
            return Math.Max(1, (int)Math.Ceiling(length / piece));
        }

        private static string Metres(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + " m";
        }

        // The plan of one wall, both its faces, so a ray that meets it stops
        private static double[][] Plan(RingWall wall, RingLimits limits)
        {
            Face front = Face.Of(wall, false), back = Face.Of(wall, true);
            int cuts = wall.Centre == null ? 1 : Pieces(front.Length, limits.ArcStepM);
            var points = new List<double[]>();
            points.AddRange(Enumerable.Range(0, cuts + 1).Select(i => front.At(front.Length * i / cuts)));
            points.AddRange(Enumerable.Range(0, cuts + 1).Select(i => back.At(back.Length * i / cuts)));
            return points.ToArray();
        }

        // R501. The stretches of a wall's faces that see the outside: its exterior face, and where that is shut in, its other face, since
        // a one layer wall modelled with its exterior to the house still has the outer face of the house on its far side
        private static IEnumerable<Face> Runs(RingWall wall, List<double[][]> plans, HashSet<string> back, RingLimits limits)
        {
            Face front = Face.Of(wall, false), other = Face.Of(wall, true);
            int pieces = Pieces(front.Length, SampleM);
            var frontSees = new bool[pieces];
            for (int i = 0; i < pieces; i++)
            {
                frontSees[i] = Sees(front, front.Length * (i + 0.5) / pieces, plans, limits);
            }
            foreach (Face run in Stretches(front, frontSees))
            {
                yield return run;
            }
            var backSees = new bool[pieces];
            for (int i = 0; i < pieces; i++)
            {
                // The back face walks the other way, so piece i of it lies across from the piece counted from the end of the front
                backSees[i] = !frontSees[pieces - 1 - i] && Sees(other, other.Length * (i + 0.5) / pieces, plans, limits);
            }
            foreach (Face run in Stretches(other, backSees))
            {
                back.Add(wall.Id);
                yield return run;
            }
        }

        private static IEnumerable<Face> Stretches(Face face, bool[] sees)
        {
            double piece = face.Length / sees.Length;
            for (int i = 0; i < sees.Length; i++)
            {
                if (!sees[i])
                {
                    continue;
                }
                int first = i;
                while (i + 1 < sees.Length && sees[i + 1])
                {
                    i++;
                }
                yield return face.Part(first * piece, (i + 1) * piece);
            }
        }

        // True when a ray from just off the face straight out meets no wall, or a ray along the face either way meets none and the wall
        // before the face stands further off than a slot. Along the face so a recess or a porch, whose side walls face each other, still
        // sees the outside through its open side, and not through the narrow slot between a wall and a pier beside it
        private static bool Sees(Face face, double along, List<double[][]> plans, RingLimits limits)
        {
            double[] point = face.At(along), direction = face.Along(along), outward = new[] { direction[1], -direction[0] };
            double[] from = { point[0] + outward[0] * OnLineM, point[1] + outward[1] * OnLineM };
            double before = Nearest(from, outward, plans);
            if (double.IsPositiveInfinity(before))
            {
                return true;
            }
            return before > limits.SlotM && (double.IsPositiveInfinity(Nearest(from, direction, plans)) || double.IsPositiveInfinity(Nearest(from, new[] { -direction[0], -direction[1] }, plans)));
        }

        // How far along the ray the first wall stands, infinity when it meets none
        private static double Nearest(double[] from, double[] direction, List<double[][]> plans)
        {
            double nearest = double.PositiveInfinity;
            foreach (double[][] plan in plans)
            {
                for (int i = 0; i < plan.Length; i++)
                {
                    double[] a = plan[i], b = plan[(i + 1) % plan.Length];
                    double ex = b[0] - a[0], ey = b[1] - a[1];
                    double cross = direction[0] * ey - direction[1] * ex;
                    if (cross == 0)
                    {
                        continue;
                    }
                    double wx = a[0] - from[0], wy = a[1] - from[1];
                    double t = (wx * ey - wy * ex) / cross, u = (wx * direction[1] - wy * direction[0]) / cross;
                    if (t > 0 && u >= 0 && u <= 1 && t < nearest)
                    {
                        nearest = t;
                    }
                }
            }
            return nearest;
        }

        // R502, the 80 mm pieces of the files. Stretches of one line that touch or overlap are one edge, whatever wall they came from
        private static List<Face> Merged(List<Face> runs)
        {
            var merged = new List<Face>();
            foreach (IGrouping<int, Face> line in Lines(runs))
            {
                List<Face> ordered = line.OrderBy(face => face.IsArc ? 0 : Dot(face.At(0), face.Along(0))).ToList();
                Face open = ordered[0];
                foreach (Face next in ordered.Skip(1))
                {
                    if (!next.IsArc && !open.IsArc && Dot(Minus(next.At(0), open.At(open.Length)), open.Along(0)) <= 2 * SampleM)
                    {
                        open = open.Upto(next);
                    }
                    else
                    {
                        merged.Add(open);
                        open = next;
                    }
                }
                merged.Add(open);
            }
            return merged;
        }

        // Lines that run the same way on one line share a number, every arc has its own
        private static IEnumerable<IGrouping<int, Face>> Lines(List<Face> runs)
        {
            var carriers = new List<Face>();
            var numbers = new Dictionary<Face, int>();
            foreach (Face run in runs)
            {
                int found = run.IsArc ? -1 : carriers.FindIndex(known => !known.IsArc && OnOneLine(known, run));
                if (found < 0)
                {
                    carriers.Add(run);
                    found = carriers.Count - 1;
                }
                numbers[run] = found;
            }
            return runs.GroupBy(run => numbers[run]);
        }

        private static bool OnOneLine(Face a, Face b)
        {
            double[] along = a.Along(0);
            return Dot(along, b.Along(0)) > 0 && Math.Abs(Cross(along, b.Along(0))) < ParallelSine && Math.Abs(Cross(along, Minus(b.At(0), a.At(0)))) < OnLineM;
        }

        // How one edge goes on to the next
        private sealed class Join
        {
            public Face From;
            public Face To;

            // The two ends of the connecting edge at a step, else null
            public double[] StepFrom;
            public double[] StepTo;
            public double Cost;
        }

        // R502. From the longest edge on: of the edges left, the one whose start lies nearest this end, until the walk is back at the first.
        // An edge no walk reaches is left out, a walk with no next within the corner gap is no ring and says where it stopped
        private static List<Join> Walk(List<Face> edges, RingLimits limits, ref string reason)
        {
            Face first = edges.OrderByDescending(edge => edge.Length).First();
            var left = new List<Face>(edges);
            left.Remove(first);
            var ring = new List<Join>();
            Face now = first;
            while (true)
            {
                Join best = null;
                foreach (Face next in ring.Count >= 2 ? left.Concat(new[] { first }) : left)
                {
                    Join join = Joined(now, next, limits);
                    if (join != null && (best == null || join.Cost < best.Cost))
                    {
                        best = join;
                    }
                }
                if (best == null)
                {
                    double[] end = now.At(now.Length);
                    reason = "the ring stops at the end of wall " + string.Join("+", now.Walls.OrderBy(id => id, StringComparer.Ordinal)) + ", no face starts within wallRingCornerGapM of it after "
                        + ring.Count + " edges, the walls do not close there";
                    return null;
                }
                ring.Add(best);
                if (best.To == first)
                {
                    return ring;
                }
                left.Remove(best.To);
                now = best.To;
            }
        }

        // R502. Two lines that cross: the corner where they cross, whatever each end does. Two parallel lines: the outer one keeps its
        // whole length and a connecting edge takes the step. Null when an end stands further than the corner gap from where it should reach
        private static Join Joined(Face from, Face to, RingLimits limits)
        {
            double[] head = from.At(from.Length), tail = to.At(0);
            if (!from.IsArc && !to.IsArc && Math.Abs(Cross(from.Along(0), to.Along(0))) < ParallelSine)
            {
                double[] along = from.Along(0);
                double[] gap = Minus(tail, head);
                double ahead = Dot(gap, along), outward = gap[0] * along[1] - gap[1] * along[0];
                if (Dot(along, to.Along(0)) < 0)
                {
                    // The end of a pier: the walk turns back on the line to its left, the edge across stands at the end that reaches further
                    if (outward >= 0 || -outward > limits.CornerGapM)
                    {
                        return null;
                    }
                    double[] end = ahead > 0 ? new[] { head[0] + along[0] * ahead, head[1] + along[1] * ahead } : head;
                    double[] across = ahead > 0 ? tail : new[] { tail[0] - along[0] * ahead, tail[1] - along[1] * ahead };
                    return new Join { From = from, To = to, StepFrom = end, StepTo = across, Cost = -outward + Math.Abs(ahead) };
                }
                if (Math.Max(0, ahead) > limits.CornerGapM)
                {
                    return null;
                }
                if (Math.Abs(outward) < OnLineM)
                {
                    return new Join { From = from, To = to, Cost = Math.Abs(ahead) };
                }
                if (Math.Abs(outward) > limits.CornerGapM)
                {
                    return null;
                }
                double[] start = outward > 0 ? new[] { tail[0] - along[1] * outward, tail[1] + along[0] * outward } : head;
                double[] stop = outward > 0 ? tail : new[] { head[0] + along[1] * outward, head[1] - along[0] * outward };
                if (Dot(Minus(start, from.At(0)), along) <= 0 || Dot(Minus(to.At(to.Length), stop), along) <= 0)
                {
                    return null;
                }
                return new Join { From = from, To = to, StepFrom = start, StepTo = stop, Cost = Math.Abs(outward) + Math.Max(0, ahead) };
            }
            double[] corner = Meet(from, to, head, tail);
            if (corner == null)
            {
                double apart = RingGeometry.Distance(head, tail);
                return apart > limits.CornerGapM ? null : new Join { From = from, To = to, StepFrom = head, StepTo = tail, Cost = apart };
            }
            double onFrom = from.ToHead(corner), onTo = to.FromTail(corner);
            if (from.Length + onFrom <= 0 || onTo >= to.Length || Math.Max(onFrom, -onTo) > limits.CornerGapM)
            {
                return null;
            }
            return new Join { From = from, To = to, Cost = Math.Abs(onFrom) + Math.Abs(onTo) };
        }

        // Where the lines or circles of two faces cross, the crossing nearest the two ends. Null when parallel or when they never cross
        private static double[] Meet(Face a, Face b, double[] near, double[] other)
        {
            List<double[]> roots = Crossings(a, b);
            return roots.Count == 0 ? null : roots.OrderBy(root => RingGeometry.Distance(root, near) + RingGeometry.Distance(root, other)).First();
        }

        private static List<double[]> Crossings(Face a, Face b)
        {
            var roots = new List<double[]>();
            if (!a.IsArc && !b.IsArc)
            {
                double[] da = a.Along(0), db = b.Along(0);
                double cross = Cross(da, db);
                if (Math.Abs(cross) < ParallelSine)
                {
                    return roots;
                }
                double t = Cross(Minus(b.At(0), a.At(0)), db) / cross;
                roots.Add(new[] { a.At(0)[0] + da[0] * t, a.At(0)[1] + da[1] * t });
                return roots;
            }
            if (a.IsArc && b.IsArc)
            {
                double apart = RingGeometry.Distance(a.Centre, b.Centre);
                if (apart < OnLineM || apart > a.Radius + b.Radius + OnLineM || apart < Math.Abs(a.Radius - b.Radius) - OnLineM)
                {
                    return roots;
                }
                double reach = (apart * apart + a.Radius * a.Radius - b.Radius * b.Radius) / (2 * apart);
                double high = Math.Sqrt(Math.Max(0, a.Radius * a.Radius - reach * reach));
                double ux = (b.Centre[0] - a.Centre[0]) / apart, uy = (b.Centre[1] - a.Centre[1]) / apart;
                roots.Add(new[] { a.Centre[0] + ux * reach - uy * high, a.Centre[1] + uy * reach + ux * high });
                roots.Add(new[] { a.Centre[0] + ux * reach + uy * high, a.Centre[1] + uy * reach - ux * high });
                return roots;
            }
            Face line = a.IsArc ? b : a, arc = a.IsArc ? a : b;
            double[] direction = line.Along(0), to = Minus(arc.Centre, line.At(0));
            double foot = Dot(to, direction), off = Cross(direction, to);
            if (Math.Abs(off) > arc.Radius + OnLineM)
            {
                return roots;
            }
            double half = Math.Sqrt(Math.Max(0, arc.Radius * arc.Radius - off * off));
            double[] origin = line.At(0);
            roots.Add(new[] { origin[0] + direction[0] * (foot - half), origin[1] + direction[1] * (foot - half) });
            roots.Add(new[] { origin[0] + direction[0] * (foot + half), origin[1] + direction[1] * (foot + half) });
            return roots;
        }

        // The ring as faces whose every neighbours cross: one line through a join on one line, a connecting edge at a step
        private static List<Face> Carriers(List<Join> ring)
        {
            var carriers = new List<Face>();
            foreach (Join join in ring)
            {
                Face last = carriers.Count > 0 && carriers[carriers.Count - 1].OnSameCarrier(join.From) ? carriers[carriers.Count - 1] : null;
                if (last == null)
                {
                    carriers.Add(join.From);
                }
                else
                {
                    last.Walls.UnionWith(join.From.Walls);
                }
                if (join.StepFrom != null)
                {
                    carriers.Add(Face.Connecting(join.StepFrom, join.StepTo));
                }
            }
            if (carriers.Count > 1 && carriers[0].OnSameCarrier(carriers[carriers.Count - 1]))
            {
                carriers[0].Walls.UnionWith(carriers[carriers.Count - 1].Walls);
                carriers.RemoveAt(carriers.Count - 1);
            }
            return carriers;
        }

        // R502, R503. The corners of the ring by crossing each carrier with the next. An edge under the shortest edge, or one the crossings
        // turn back, goes: its neighbours then cross, or when they are parallel the shorter neighbour goes with it into the longer
        private static List<RingEdge> Closed(List<Face> carriers, RingLimits limits, ref string reason)
        {
            while (carriers.Count >= 3)
            {
                List<double[]> corners = Corners(carriers);
                if (corners == null)
                {
                    reason = "two neighbouring faces of the ring never cross";
                    return null;
                }
                double[] lengths = Lengths(carriers, corners);
                int shortest = Enumerable.Range(0, carriers.Count).OrderBy(i => lengths[i]).First();
                if (lengths[shortest] >= limits.ShortestEdgeM)
                {
                    return Enumerable.Range(0, carriers.Count).Select(i => carriers[i].Edge(corners[(i + carriers.Count - 1) % carriers.Count], corners[i])).ToList();
                }
                int before = (shortest + carriers.Count - 1) % carriers.Count, after = (shortest + 1) % carriers.Count;
                Face gone = carriers[shortest];
                if (Crossings(carriers[before], carriers[after]).Count > 0)
                {
                    carriers[before].Walls.UnionWith(gone.Walls);
                    carriers.RemoveAt(shortest);
                    continue;
                }
                int shorter = lengths[before] < lengths[after] ? before : after;
                int keep = shorter == before ? after : before;
                carriers[keep].Walls.UnionWith(gone.Walls);
                carriers[keep].Walls.UnionWith(carriers[shorter].Walls);
                Face leaving = carriers[shorter];
                carriers.Remove(gone);
                carriers.Remove(leaving);
            }
            reason = "the ring holds under 3 edges once the edges under wallRingShortestEdgeM are merged";
            return null;
        }

        private static List<double[]> Corners(List<Face> carriers)
        {
            var corners = new List<double[]>();
            for (int i = 0; i < carriers.Count; i++)
            {
                Face a = carriers[i], b = carriers[(i + 1) % carriers.Count];
                double[] corner = Meet(a, b, a.At(a.Length), b.At(0));
                if (corner == null)
                {
                    return null;
                }
                corners.Add(corner);
            }
            return corners;
        }

        // The length of each edge between its two corners, under 0 where the corners turn it back
        private static double[] Lengths(List<Face> carriers, List<double[]> corners)
        {
            var lengths = new double[carriers.Count];
            for (int i = 0; i < carriers.Count; i++)
            {
                lengths[i] = carriers[i].Between(corners[(i + carriers.Count - 1) % carriers.Count], corners[i]);
            }
            return lengths;
        }

        private static double Dot(double[] a, double[] b)
        {
            return a[0] * b[0] + a[1] * b[1];
        }

        private static double Cross(double[] a, double[] b)
        {
            return a[0] * b[1] - a[1] * b[0];
        }

        private static double[] Minus(double[] a, double[] b)
        {
            return new[] { a[0] - b[0], a[1] - b[1] };
        }

        // One stretch of a face in walking order, the outside on its right, so the ring walks counter clockwise
        private sealed class Face
        {
            private double[] origin;
            private double[] direction;
            private double startAngle;
            private bool counterClockwise;

            public double Length { get; private set; }
            public double[] Centre { get; private set; }
            public double Radius { get; private set; }
            public HashSet<string> Walls { get; private set; } = new HashSet<string>(StringComparer.Ordinal);
            public bool IsArc => Centre != null;

            public static Face Of(RingWall wall, bool back)
            {
                var face = new Face();
                face.Walls.Add(wall.Id);
                double half = wall.Width / 2;
                double length = Math.Sqrt(wall.Exterior[0] * wall.Exterior[0] + wall.Exterior[1] * wall.Exterior[1]);
                double[] outward = { wall.Exterior[0] / length, wall.Exterior[1] / length };
                if (wall.Centre == null)
                {
                    if (back)
                    {
                        outward = new[] { -outward[0], -outward[1] };
                    }
                    face.direction = new[] { -outward[1], outward[0] };
                    double from = Dot(wall.Start, face.direction), to = Dot(wall.End, face.direction);
                    double[] tail = from <= to ? wall.Start : wall.End;
                    face.origin = new[] { tail[0] + outward[0] * half, tail[1] + outward[1] * half };
                    face.Length = Math.Abs(to - from);
                    return face;
                }
                double radius = RingGeometry.Distance(wall.Start, wall.Centre);
                double low = wall.CounterClockwise ? RingGeometry.Angle(wall.Centre, wall.Start) : RingGeometry.Angle(wall.Centre, wall.End);
                double high = wall.CounterClockwise ? RingGeometry.Angle(wall.Centre, wall.End) : RingGeometry.Angle(wall.Centre, wall.Start);
                double sweep = RingGeometry.Turn(high - low);
                sweep = sweep == 0 ? 2 * Math.PI : sweep;
                double middle = low + sweep / 2;
                bool away = outward[0] * Math.Cos(middle) + outward[1] * Math.Sin(middle) >= 0;
                away = back ? !away : away;
                face.Centre = wall.Centre;
                face.Radius = radius + (away ? half : -half);
                face.counterClockwise = away;
                face.startAngle = away ? low : low + sweep;
                face.Length = face.Radius * sweep;
                return face;
            }

            public static Face Connecting(double[] from, double[] to)
            {
                double length = RingGeometry.Distance(from, to);
                return new Face { origin = from, direction = new[] { (to[0] - from[0]) / length, (to[1] - from[1]) / length }, Length = length };
            }

            public double[] At(double along)
            {
                if (!IsArc)
                {
                    return new[] { origin[0] + direction[0] * along, origin[1] + direction[1] * along };
                }
                double angle = startAngle + (counterClockwise ? 1 : -1) * along / Radius;
                return new[] { Centre[0] + Radius * Math.Cos(angle), Centre[1] + Radius * Math.Sin(angle) };
            }

            public double[] Along(double along)
            {
                if (!IsArc)
                {
                    return direction;
                }
                double angle = startAngle + (counterClockwise ? 1 : -1) * along / Radius;
                return counterClockwise ? new[] { -Math.Sin(angle), Math.Cos(angle) } : new[] { Math.Sin(angle), -Math.Cos(angle) };
            }

            public Face Part(double from, double to)
            {
                var part = (Face)MemberwiseClone();
                part.Walls = new HashSet<string>(Walls, StringComparer.Ordinal);
                part.Length = to - from;
                if (IsArc)
                {
                    part.startAngle = startAngle + (counterClockwise ? 1 : -1) * from / Radius;
                }
                else
                {
                    part.origin = At(from);
                }
                return part;
            }

            // This stretch carried on to the end of the other, both on one line, keeping both their walls
            public Face Upto(Face other)
            {
                var joined = (Face)MemberwiseClone();
                joined.Walls = new HashSet<string>(Walls.Concat(other.Walls), StringComparer.Ordinal);
                joined.Length = Math.Max(Length, Dot(Minus(other.At(other.Length), origin), direction));
                return joined;
            }

            // How far past the end of the stretch the point lies along it, under 0 when before the end
            public double ToHead(double[] point)
            {
                return IsArc ? Signed(At(Length), point) * Radius : Dot(Minus(point, origin), direction) - Length;
            }

            // How far past the start of the stretch the point lies along it, under 0 when before the start
            public double FromTail(double[] point)
            {
                return IsArc ? Signed(At(0), point) * Radius : Dot(Minus(point, origin), direction);
            }

            // The length of this carrier from one point on it to the next, under 0 when they lie the wrong way
            public double Between(double[] from, double[] to)
            {
                return IsArc ? Signed(from, to) * Radius : Dot(Minus(to, from), direction);
            }

            public bool OnSameCarrier(Face other)
            {
                if (IsArc || other.IsArc)
                {
                    return IsArc && other.IsArc && RingGeometry.Distance(Centre, other.Centre) < OnLineM && Math.Abs(Radius - other.Radius) < OnLineM && counterClockwise == other.counterClockwise;
                }
                return OnOneLine(this, other);
            }

            public RingEdge Edge(double[] from, double[] to)
            {
                return IsArc ? new RingEdge(from, to, Centre, counterClockwise) : new RingEdge(from, to);
            }

            // The angle from one point on the circle to another in walking order, within half a turn either way
            private double Signed(double[] from, double[] to)
            {
                double turn = RingGeometry.Turn((counterClockwise ? 1 : -1) * (RingGeometry.Angle(Centre, to) - RingGeometry.Angle(Centre, from)));
                return turn > Math.PI ? turn - 2 * Math.PI : turn;
            }
        }
    }

    public static class WallFunctions
    {
        public const string Exterior = "Exterior";
    }
}
