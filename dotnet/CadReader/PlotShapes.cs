using System;
using System.Collections.Generic;
using System.Linq;
using ACadSharp.Entities;

namespace CadReader
{
    // Builds plot geometry from the LINE and ARC entities of the plot line layer.
    // Plot walls are often drawn once and shared, so a line can end in the middle of another line.
    // Lines are split at those T junctions, dangling pieces are dropped, and every bounded face left is one closed shape.
    // Pieces that bound no closed shape are still written, as open pieces, so nothing on the layer is lost.
    public static class PlotShapes
    {

        // Drawing units, metres on the real baseplan
        public const double Tolerance = 0.01;
        private const double MinArea = 0.001;

        private sealed class Edge
        {
            public int A;
            public int B;
            public bool IsArc;
            public double Cx;
            public double Cy;
            public double Radius;
            public bool Used;
            public bool Removed;
        }

        private sealed class Graph
        {
            public readonly List<double[]> Nodes = new List<double[]>();
            public readonly List<Edge> Pieces = new List<Edge>();
            public string Layer;
            private readonly Dictionary<Tuple<long, long>, List<int>> grid = new Dictionary<Tuple<long, long>, List<int>>();

            public int Node(double x, double y)
            {
                long cellX = (long)Math.Floor(x / Tolerance);
                long cellY = (long)Math.Floor(y / Tolerance);
                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        List<int> bucket;
                        if (!grid.TryGetValue(Tuple.Create(cellX + dx, cellY + dy), out bucket))
                        {
                            continue;
                        }
                        foreach (int index in bucket)
                        {
                            if (Math.Abs(Nodes[index][0] - x) <= Tolerance && Math.Abs(Nodes[index][1] - y) <= Tolerance)
                            {
                                return index;
                            }
                        }
                    }
                }
                Nodes.Add(new[] { x, y });
                var key = Tuple.Create(cellX, cellY);
                if (!grid.ContainsKey(key))
                {
                    grid[key] = new List<int>();
                }
                grid[key].Add(Nodes.Count - 1);
                return Nodes.Count - 1;
            }
        }

        // A straight piece, or an arc that runs counter clockwise from angle A0 to angle A1
        public sealed class Seg
        {
            public bool IsArc;
            public double X1;
            public double Y1;
            public double X2;
            public double Y2;
            public double Cx;
            public double Cy;
            public double Radius;
            public double A0;
            public double A1;

            public static Seg FromPiece(CadReader.Piece piece)
            {
                if (piece.Kind == "line")
                {
                    return new Seg { X1 = piece.Pts[0][0], Y1 = piece.Pts[0][1], X2 = piece.Pts[1][0], Y2 = piece.Pts[1][1] };
                }
                double[] from = piece.Ccw == false ? piece.End : piece.Start;
                double[] to = piece.Ccw == false ? piece.Start : piece.End;
                return new Seg
                {
                    IsArc = true,
                    Cx = piece.Center[0],
                    Cy = piece.Center[1],
                    Radius = piece.Radius.Value,
                    A0 = Math.Atan2(from[1] - piece.Center[1], from[0] - piece.Center[0]),
                    A1 = Math.Atan2(to[1] - piece.Center[1], to[0] - piece.Center[0]),
                };
            }
        }

        public static List<PlotEntry> Build(IEnumerable<Seg> segs, string layer)
        {
            var graph = new Graph { Layer = layer };
            foreach (Seg seg in segs)
            {
                if (seg.IsArc)
                {
                    AddArc(graph, seg);
                }
                else
                {
                    AddLine(graph, seg.X1, seg.Y1, seg.X2, seg.Y2);
                }
            }
            SplitAtJunctions(graph);
            RemoveDuplicates(graph);

            var result = new List<PlotEntry>();
            result.AddRange(TraceShapes(graph));
            result.AddRange(OpenPieces(graph));
            return result;
        }

        private static void AddLine(Graph graph, double x1, double y1, double x2, double y2)
        {
            int a = graph.Node(x1, y1);
            int b = graph.Node(x2, y2);
            if (a != b)
            {
                graph.Pieces.Add(new Edge { A = a, B = b });
            }
        }

        private static void AddArc(Graph graph, Seg arc)
        {
            int a = graph.Node(arc.Cx + arc.Radius * Math.Cos(arc.A0), arc.Cy + arc.Radius * Math.Sin(arc.A0));
            int b = graph.Node(arc.Cx + arc.Radius * Math.Cos(arc.A1), arc.Cy + arc.Radius * Math.Sin(arc.A1));
            if (a != b)
            {
                graph.Pieces.Add(new Edge { A = a, B = b, IsArc = true, Cx = arc.Cx, Cy = arc.Cy, Radius = arc.Radius });
            }
        }

        // Counter clockwise sweep from the first node to the second node of an arc piece, in (0, 2 pi)
        private static double Sweep(Graph graph, Edge piece)
        {
            double[] a = graph.Nodes[piece.A];
            double[] b = graph.Nodes[piece.B];
            double sweep = Math.Atan2(b[1] - piece.Cy, b[0] - piece.Cx) - Math.Atan2(a[1] - piece.Cy, a[0] - piece.Cx);
            while (sweep <= 0)
            {
                sweep += 2 * Math.PI;
            }
            while (sweep > 2 * Math.PI)
            {
                sweep -= 2 * Math.PI;
            }
            return sweep;
        }

        // Two straight pieces that cross without sharing an end get a node at the crossing, the split below then cuts both
        private static void AddCrossingNodes(Graph graph)
        {
            var lines = graph.Pieces.Where(piece => !piece.IsArc).ToList();
            for (int first = 0; first < lines.Count; first++)
            {
                double[] a = graph.Nodes[lines[first].A];
                double[] b = graph.Nodes[lines[first].B];
                for (int second = first + 1; second < lines.Count; second++)
                {
                    double[] c = graph.Nodes[lines[second].A];
                    double[] d = graph.Nodes[lines[second].B];
                    if (Math.Max(a[0], b[0]) < Math.Min(c[0], d[0]) || Math.Max(c[0], d[0]) < Math.Min(a[0], b[0])
                        || Math.Max(a[1], b[1]) < Math.Min(c[1], d[1]) || Math.Max(c[1], d[1]) < Math.Min(a[1], b[1]))
                    {
                        continue;
                    }
                    double rx = b[0] - a[0];
                    double ry = b[1] - a[1];
                    double sx = d[0] - c[0];
                    double sy = d[1] - c[1];
                    double denominator = rx * sy - ry * sx;
                    if (Math.Abs(denominator) < 1e-12)
                    {
                        continue;
                    }
                    double along = ((c[0] - a[0]) * sy - (c[1] - a[1]) * sx) / denominator;
                    double other = ((c[0] - a[0]) * ry - (c[1] - a[1]) * rx) / denominator;
                    if (along > 0 && along < 1 && other > 0 && other < 1)
                    {
                        graph.Node(a[0] + along * rx, a[1] + along * ry);
                    }
                }
            }
        }

        private static void SplitAtJunctions(Graph graph)
        {
            AddCrossingNodes(graph);
            int count = graph.Pieces.Count;
            for (int index = 0; index < count; index++)
            {
                Edge piece = graph.Pieces[index];
                var hits = new List<KeyValuePair<double, int>>();
                for (int node = 0; node < graph.Nodes.Count; node++)
                {
                    if (node == piece.A || node == piece.B)
                    {
                        continue;
                    }
                    double along;
                    if (piece.IsArc ? OnArc(graph, piece, node, out along) : OnLine(graph, piece, node, out along))
                    {
                        hits.Add(new KeyValuePair<double, int>(along, node));
                    }
                }
                if (hits.Count == 0)
                {
                    continue;
                }
                hits.Sort((first, second) => first.Key.CompareTo(second.Key));
                int last = piece.B;
                piece.B = hits[0].Value;
                int previous = hits[0].Value;
                for (int hit = 1; hit < hits.Count; hit++)
                {
                    graph.Pieces.Add(new Edge { A = previous, B = hits[hit].Value, IsArc = piece.IsArc, Cx = piece.Cx, Cy = piece.Cy, Radius = piece.Radius });
                    previous = hits[hit].Value;
                }
                graph.Pieces.Add(new Edge { A = previous, B = last, IsArc = piece.IsArc, Cx = piece.Cx, Cy = piece.Cy, Radius = piece.Radius });
            }
        }

        private static bool OnLine(Graph graph, Edge piece, int node, out double along)
        {
            double[] a = graph.Nodes[piece.A];
            double[] b = graph.Nodes[piece.B];
            double[] p = graph.Nodes[node];
            double dx = b[0] - a[0];
            double dy = b[1] - a[1];
            double lengthSquared = dx * dx + dy * dy;
            along = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / lengthSquared;
            if (along <= 0 || along >= 1)
            {
                return false;
            }
            double distance = Math.Abs((p[0] - a[0]) * dy - (p[1] - a[1]) * dx) / Math.Sqrt(lengthSquared);
            return distance <= Tolerance;
        }

        private static bool OnArc(Graph graph, Edge piece, int node, out double along)
        {
            along = 0;
            double[] p = graph.Nodes[node];
            if (Math.Abs(Math.Sqrt((p[0] - piece.Cx) * (p[0] - piece.Cx) + (p[1] - piece.Cy) * (p[1] - piece.Cy)) - piece.Radius) > Tolerance)
            {
                return false;
            }
            double[] a = graph.Nodes[piece.A];
            double offset = Math.Atan2(p[1] - piece.Cy, p[0] - piece.Cx) - Math.Atan2(a[1] - piece.Cy, a[0] - piece.Cx);
            while (offset < 0)
            {
                offset += 2 * Math.PI;
            }
            along = offset;
            return offset > 0 && offset < Sweep(graph, piece);
        }

        private static void RemoveDuplicates(Graph graph)
        {
            var seen = new HashSet<string>();
            foreach (Edge piece in graph.Pieces)
            {
                int low = Math.Min(piece.A, piece.B);
                int high = Math.Max(piece.A, piece.B);
                string key = low + ":" + high + (piece.IsArc ? ":" + Math.Round(piece.Cx / Tolerance) + ":" + Math.Round(piece.Cy / Tolerance) : "");
                if (!seen.Add(key))
                {
                    piece.Removed = true;
                }
            }
            graph.Pieces.RemoveAll(piece => piece.Removed);
        }

        // Half edge 2 * i runs A to B of piece i and half edge 2 * i + 1 runs B to A
        private static int From(Edge piece, int half)
        {
            return (half & 1) == 0 ? piece.A : piece.B;
        }

        private static int To(Edge piece, int half)
        {
            return (half & 1) == 0 ? piece.B : piece.A;
        }

        // Signed bulge of a half edge, positive when the travel bends to the left
        private static double Bulge(Graph graph, Edge piece, int half)
        {
            if (!piece.IsArc)
            {
                return 0;
            }
            double bulge = Math.Tan(Sweep(graph, piece) / 4);
            return (half & 1) == 0 ? bulge : -bulge;
        }

        private static double LeavingAngle(Graph graph, Edge piece, int half)
        {
            double[] from = graph.Nodes[From(piece, half)];
            double[] to = graph.Nodes[To(piece, half)];
            double chord = Math.Atan2(to[1] - from[1], to[0] - from[0]);
            return chord - 2 * Math.Atan(Bulge(graph, piece, half));
        }

        private static List<PlotEntry> TraceShapes(Graph graph)
        {
            var shapes = new List<PlotEntry>();
            Prune(graph);

            var outgoing = new List<int>[graph.Nodes.Count];
            for (int index = 0; index < graph.Pieces.Count; index++)
            {
                if (graph.Pieces[index].Removed)
                {
                    continue;
                }
                for (int half = 2 * index; half <= 2 * index + 1; half++)
                {
                    int from = From(graph.Pieces[index], half);
                    if (outgoing[from] == null)
                    {
                        outgoing[from] = new List<int>();
                    }
                    outgoing[from].Add(half);
                }
            }
            foreach (List<int> list in outgoing.Where(list => list != null))
            {
                list.Sort((first, second) => LeavingAngle(graph, graph.Pieces[first / 2], first).CompareTo(LeavingAngle(graph, graph.Pieces[second / 2], second)));
            }

            var visited = new bool[graph.Pieces.Count * 2];
            for (int start = 0; start < visited.Length; start++)
            {
                if (visited[start] || graph.Pieces[start / 2].Removed)
                {
                    continue;
                }
                var cycle = new List<int>();
                int half = start;
                while (!visited[half])
                {
                    visited[half] = true;
                    cycle.Add(half);
                    List<int> around = outgoing[To(graph.Pieces[half / 2], half)];
                    int twinAt = around.IndexOf(half ^ 1);
                    half = around[(twinAt - 1 + around.Count) % around.Count];
                }
                double area = SignedArea(graph, cycle);
                if (area > MinArea)
                {
                    shapes.Add(Shape(graph, cycle, area));
                    foreach (int used in cycle)
                    {
                        graph.Pieces[used / 2].Used = true;
                    }
                }
            }
            return shapes;
        }

        // Pieces that end at a node with nothing else cannot bound a shape
        private static void Prune(Graph graph)
        {
            var degree = new int[graph.Nodes.Count];
            foreach (Edge piece in graph.Pieces)
            {
                degree[piece.A]++;
                degree[piece.B]++;
            }
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (Edge piece in graph.Pieces)
                {
                    if (!piece.Removed && (degree[piece.A] == 1 || degree[piece.B] == 1))
                    {
                        piece.Removed = true;
                        degree[piece.A]--;
                        degree[piece.B]--;
                        changed = true;
                    }
                }
            }
        }

        private static double SignedArea(Graph graph, List<int> cycle)
        {
            double twice = 0;
            double segments = 0;
            foreach (int half in cycle)
            {
                Edge piece = graph.Pieces[half / 2];
                double[] a = graph.Nodes[From(piece, half)];
                double[] b = graph.Nodes[To(piece, half)];
                twice += a[0] * b[1] - b[0] * a[1];
                if (piece.IsArc)
                {
                    double theta = 4 * Math.Atan(Bulge(graph, piece, half));
                    segments += piece.Radius * piece.Radius / 2 * (theta - Math.Sin(theta));
                }
            }
            return twice / 2 + segments;
        }

        private static PlotEntry Shape(Graph graph, List<int> cycle, double area)
        {
            // Straight neighbours that run on the same line are one side, the split points are not corners
            var sides = new List<int>();
            foreach (int half in cycle)
            {
                if (sides.Count > 0 && Joinable(graph, sides[sides.Count - 1], half))
                {
                    continue;
                }
                sides.Add(half);
            }
            if (sides.Count > 1 && Joinable(graph, sides[sides.Count - 1], sides[0]))
            {
                sides.RemoveAt(0);
            }

            var entry = new PlotEntry
            {
                Kind = "plot_shape",
                Layer = graph.Layer,
                Closed = true,
                Area = area,
                Vertices = new List<double[]>(),
            };
            for (int index = 0; index < sides.Count; index++)
            {
                int half = sides[index];
                Edge piece = graph.Pieces[half / 2];
                double[] point = graph.Nodes[From(piece, half)];
                entry.Vertices.Add(new[] { point[0], point[1] });
                if (piece.IsArc)
                {
                    if (entry.Arcs == null)
                    {
                        entry.Arcs = new List<ArcEntry>();
                    }
                    entry.Arcs.Add(new ArcEntry { Segment = index, Center = new[] { piece.Cx, piece.Cy }, Radius = piece.Radius, Ccw = (half & 1) == 0 });
                }
            }
            return entry;
        }

        // The two half edges meet at a node, are both straight and keep the same direction
        private static bool Joinable(Graph graph, int first, int second)
        {
            Edge one = graph.Pieces[first / 2];
            Edge two = graph.Pieces[second / 2];
            if (one.IsArc || two.IsArc || To(one, first) != From(two, second))
            {
                return false;
            }
            double[] a = graph.Nodes[From(one, first)];
            double[] b = graph.Nodes[To(one, first)];
            double[] c = graph.Nodes[To(two, second)];
            double ux = b[0] - a[0];
            double uy = b[1] - a[1];
            double vx = c[0] - b[0];
            double vy = c[1] - b[1];
            double cross = ux * vy - uy * vx;
            double dot = ux * vx + uy * vy;
            return dot > 0 && Math.Abs(cross) / Math.Sqrt(ux * ux + uy * uy) <= Tolerance;
        }

        private static List<PlotEntry> OpenPieces(Graph graph)
        {
            var open = graph.Pieces.Where(piece => !piece.Used).ToList();
            var parent = new Dictionary<int, int>();
            Func<int, int> find = null;
            find = node =>
            {
                int root;
                if (!parent.TryGetValue(node, out root))
                {
                    parent[node] = node;
                    return node;
                }
                if (root == node)
                {
                    return node;
                }
                parent[node] = find(root);
                return parent[node];
            };
            foreach (Edge piece in open)
            {
                parent[find(piece.A)] = find(piece.B);
            }
            var groups = new List<PlotEntry>();
            var byRoot = new Dictionary<int, PlotEntry>();
            foreach (Edge piece in open)
            {
                int root = find(piece.A);
                PlotEntry entry;
                if (!byRoot.TryGetValue(root, out entry))
                {
                    entry = new PlotEntry { Kind = "plot_open", Layer = graph.Layer, Closed = false, Segments = new List<double[]>() };
                    byRoot[root] = entry;
                    groups.Add(entry);
                }
                double[] a = graph.Nodes[piece.A];
                double[] b = graph.Nodes[piece.B];
                entry.Segments.Add(piece.IsArc
                    ? new[] { a[0], a[1], b[0], b[1], piece.Cx, piece.Cy, piece.Radius }
                    : new[] { a[0], a[1], b[0], b[1] });
            }
            return groups;
        }
    }
}
