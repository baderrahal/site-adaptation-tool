using System;
using System.Collections.Generic;

namespace SiteAdaptation.C3DProbe
{
    // The edge of a TIN is every triangle side that one visible triangle owns and no other (R152).
    // Vertices are matched on a 0.1 mm grid, so one point read through two triangles lands on one key
    public sealed class TinBorder
    {
        private const double KeyStep = 1e-4;

        private readonly Dictionary<(long, long), int> vertexIndex = new Dictionary<(long, long), int>();
        private readonly List<double[]> vertices = new List<double[]>();
        private readonly Dictionary<long, int> edgeCount = new Dictionary<long, int>();

        public int Triangles { get; private set; }

        public int Vertices => vertices.Count;

        public void Add(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            int a = Vertex(x1, y1);
            int b = Vertex(x2, y2);
            int c = Vertex(x3, y3);
            // A sliver with two corners on one key has no area and no side worth counting
            if (a == b || b == c || a == c)
            {
                return;
            }
            Triangles++;
            Count(a, b);
            Count(b, c);
            Count(c, a);
        }

        public List<(int, int)> BorderEdges()
        {
            var edges = new List<(int, int)>();
            foreach (var pair in edgeCount)
            {
                if (pair.Value == 1)
                {
                    edges.Add(((int)(pair.Key >> 32), (int)(pair.Key & 0xffffffffL)));
                }
            }
            return edges;
        }

        public double[] VertexAt(int index)
        {
            return vertices[index];
        }

        // Chains the border edges into rings. Where more than two border edges meet at one vertex, as at a pinch
        // between two strips, the walk takes the next free edge, so every edge lands in exactly one ring
        public List<List<double[]>> Rings()
        {
            var edges = BorderEdges();
            var byVertex = new Dictionary<int, List<int>>();
            for (int index = 0; index < edges.Count; index++)
            {
                Link(byVertex, edges[index].Item1, index);
                Link(byVertex, edges[index].Item2, index);
            }
            var used = new bool[edges.Count];
            var rings = new List<List<double[]>>();
            for (int first = 0; first < edges.Count; first++)
            {
                if (used[first])
                {
                    continue;
                }
                used[first] = true;
                int start = edges[first].Item1;
                int at = edges[first].Item2;
                var ring = new List<double[]> { vertices[start], vertices[at] };
                while (at != start)
                {
                    int next = -1;
                    foreach (int candidate in byVertex[at])
                    {
                        if (!used[candidate])
                        {
                            next = candidate;
                            break;
                        }
                    }
                    if (next < 0)
                    {
                        break;
                    }
                    used[next] = true;
                    at = edges[next].Item1 == at ? edges[next].Item2 : edges[next].Item1;
                    ring.Add(vertices[at]);
                }
                rings.Add(ring);
            }
            return rings;
        }

        private static void Link(Dictionary<int, List<int>> byVertex, int vertex, int edge)
        {
            if (!byVertex.TryGetValue(vertex, out var list))
            {
                list = new List<int>();
                byVertex[vertex] = list;
            }
            list.Add(edge);
        }

        private int Vertex(double x, double y)
        {
            var key = ((long)Math.Round(x / KeyStep), (long)Math.Round(y / KeyStep));
            if (!vertexIndex.TryGetValue(key, out int index))
            {
                index = vertices.Count;
                vertexIndex[key] = index;
                vertices.Add(new[] { x, y });
            }
            return index;
        }

        private void Count(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            edgeCount.TryGetValue(key, out int seen);
            edgeCount[key] = seen + 1;
        }
    }
}
