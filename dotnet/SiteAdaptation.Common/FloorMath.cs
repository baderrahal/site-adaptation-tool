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
            List<double[]> ends;
            return TryOuter(loops, out vertices, out arcs, out ends);
        }

        // ends holds the start of each edge of the outer loop, the true corners, without the points cut out of a spline
        public static bool TryOuter(IEnumerable<IList<BoundaryEdge>> loops, out List<double[]> vertices, out List<FloorArc> arcs, out List<double[]> ends)
        {
            vertices = null;
            arcs = null;
            ends = null;
            double best = -1;
            foreach (IList<BoundaryEdge> loop in loops)
            {
                var points = new List<double[]>();
                var found = new List<FloorArc>();
                var starts = new List<double[]>();
                foreach (BoundaryEdge edge in loop)
                {
                    if (edge.Radius.HasValue)
                    {
                        found.Add(new FloorArc { Edge = points.Count, Center = edge.Center, Radius = edge.Radius.Value, Ccw = edge.Ccw });
                    }
                    starts.Add(edge.Start);
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
                    ends = starts;
                }
            }
            return vertices != null;
        }

        // How many floors use shape editing, report a slope or use neither. A floor with both counts under shape editing and under slope
        public static FloorTally Tally(IEnumerable<FloorRow> floors)
        {
            var tally = new FloorTally();
            foreach (FloorRow floor in floors)
            {
                bool shape = floor.CornersSource == FromShapeEditor;
                bool slope = floor.Slope != null && (floor.Slope.Parameter != null || floor.Slope.Arrow == true);
                tally.Floors++;
                tally.ShapeEditing += shape ? 1 : 0;
                tally.Slope += slope ? 1 : 0;
                tally.Neither += !shape && !slope ? 1 : 0;
            }
            return tally;
        }

        public const string FromShapeEditor = "shapeEditor";
        public const string FromBoundary = "boundary";

        // Real corner levels come from the shape editing vertices when shape editing is on and holds at least 3 corners.
        // Otherwise the corners are the outer loop end points at the top elevation of the floor, which is flat.
        // Returns false when neither source gives a corner
        public static bool TryCorners(ShapeEditing shape, IList<double[]> boundaryEnds, double? topElevation, out List<double[]> corners, out string source)
        {
            if (shape != null && shape.Enabled && shape.Corners != null && shape.Corners.Count >= 3)
            {
                corners = shape.Corners;
                source = FromShapeEditor;
                return true;
            }
            if (boundaryEnds != null && boundaryEnds.Count >= 3 && topElevation.HasValue)
            {
                corners = boundaryEnds.Select(point => new[] { point[0], point[1], topElevation.Value }).ToList();
                source = FromBoundary;
                return true;
            }
            corners = null;
            source = null;
            return false;
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

    // Counts of floors by what they use for their levels, see FloorMath.Tally
    public class FloorTally
    {
        public int Floors { get; set; }
        public int ShapeEditing { get; set; }
        public int Slope { get; set; }
        public int Neither { get; set; }
    }
}
