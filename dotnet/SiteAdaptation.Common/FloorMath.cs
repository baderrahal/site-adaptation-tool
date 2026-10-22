using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    public static class FloorMath
    {
        // The outer loop is the one whose vertices enclose the most area, because a hole sits inside it.
        // Returns false when there is no loop with at least 3 vertices
        public static bool TryOuter(IEnumerable<IList<BoundaryEdge>> loops, out List<double[]> vertices, out List<FloorArc> arcs)
        {
            vertices = null;
            arcs = null;
            double best = -1;
            foreach (IList<BoundaryEdge> loop in loops)
            {
                var points = new List<double[]>();
                var found = new List<FloorArc>();
                foreach (BoundaryEdge edge in loop)
                {
                    if (edge.Radius.HasValue)
                    {
                        found.Add(new FloorArc { Edge = points.Count, Center = edge.Center, Radius = edge.Radius.Value, Ccw = edge.Ccw });
                    }
                    points.Add(edge.Start);
                    if (edge.Via != null)
                    {
                        points.AddRange(edge.Via);
                    }
                }
                if (points.Count < 3)
                {
                    continue;
                }
                double area = Math.Abs(SignedArea(points));
                if (area > best)
                {
                    best = area;
                    vertices = points;
                    arcs = found;
                }
            }
            return vertices != null;
        }

        // Shoelace formula in plan, closed from the last vertex back to the first
        public static double SignedArea(IList<double[]> points)
        {
            double sum = 0;
            for (int index = 0; index < points.Count; index++)
            {
                double[] a = points[index];
                double[] b = points[(index + 1) % points.Count];
                sum += a[0] * b[1] - b[0] * a[1];
            }
            return sum / 2;
        }
    }
}
