using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    public static class OutlineLoops
    {
        // Two points closer than this, in metres, are one point
        public const double Tolerance = 0.001;

        // The loops that surround the faces whose edges are given. A floor with shape editing has several top faces,
        // and the edge between two of them is on both, so an edge found twice is inside the outline and is dropped.
        // What is left is chained end to end into loops. Each loop lists its points in order and is closed by going from the last back to the first.
        // A chain that does not come back to its start is counted in openChains and is not written
        public static List<List<double[]>> Build(IEnumerable<BoundaryEdge> edges, out int openChains)
        {
            List<BoundaryEdge> pending = edges.Where(edge => edge != null && edge.Start != null && edge.End != null).ToList();
            var left = new List<BoundaryEdge>();
            var used = new bool[pending.Count];
            for (int index = 0; index < pending.Count; index++)
            {
                if (used[index])
                {
                    continue;
                }
                int twin = -1;
                for (int other = index + 1; other < pending.Count && twin < 0; other++)
                {
                    if (!used[other] && SameEdge(pending[index], pending[other]))
                    {
                        twin = other;
                    }
                }
                used[index] = true;
                if (twin >= 0)
                {
                    used[twin] = true;
                }
                else
                {
                    left.Add(pending[index]);
                }
            }

            var loops = new List<List<double[]>>();
            openChains = 0;
            while (left.Count > 0)
            {
                BoundaryEdge first = left[0];
                left.RemoveAt(0);
                var points = new List<double[]>();
                AddEdge(points, first, false);
                double[] head = first.Start;
                double[] tail = first.End;
                while (!Near(tail, head))
                {
                    double[] here = tail;
                    int next = left.FindIndex(candidate => Near(candidate.Start, here) || Near(candidate.End, here));
                    if (next < 0)
                    {
                        break;
                    }
                    BoundaryEdge edge = left[next];
                    left.RemoveAt(next);
                    bool reversed = !Near(edge.Start, here);
                    AddEdge(points, edge, reversed);
                    tail = reversed ? edge.Start : edge.End;
                }
                if (Near(tail, head) && points.Count >= 3)
                {
                    loops.Add(points);
                }
                else
                {
                    openChains++;
                }
            }
            return loops;
        }

        // Start, then the points between, and not the end because the next edge starts there
        private static void AddEdge(List<double[]> points, BoundaryEdge edge, bool reversed)
        {
            AddPoint(points, reversed ? edge.End : edge.Start);
            if (edge.Via != null)
            {
                foreach (double[] via in reversed ? Enumerable.Reverse(edge.Via) : edge.Via)
                {
                    AddPoint(points, via);
                }
            }
        }

        private static void AddPoint(List<double[]> points, double[] point)
        {
            if (points.Count == 0 || !Near(points[points.Count - 1], point))
            {
                points.Add(new[] { point[0], point[1], point[2] });
            }
        }

        private static bool SameEdge(BoundaryEdge a, BoundaryEdge b)
        {
            return (Near(a.Start, b.Start) && Near(a.End, b.End)) || (Near(a.Start, b.End) && Near(a.End, b.Start));
        }

        private static bool Near(double[] a, double[] b)
        {
            double dx = a[0] - b[0];
            double dy = a[1] - b[1];
            double dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz) <= Tolerance;
        }
    }
}
