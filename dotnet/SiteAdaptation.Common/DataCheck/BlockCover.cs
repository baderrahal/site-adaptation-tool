using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace SiteAdaptation.Common
{
    // R731. The plots a villa block stands on, by the area its outline covers, and the place of each along the block. A duplex block covers
    // two plots and a row of houses four, its centre lies on a shared wall and gives the block to one plot or none, so the outline decides.
    // The halves of a block are opposite hands, so the hand alternates along the block from the scale sign of the insert
    public static class BlockCover
    {
        // CHOSEN by Code, no setting: a plot takes the block when the block covers at least this share of what it covers of its best plot.
        // The halves of a duplex are near even and the edge of a hull that reaches into the next plot is a sliver, so any value well under 1
        // and well over a sliver parts them the same way
        public const double ShareOfLargest = 0.5;

        // What the block covers of one plot, and its place along the block from 0
        public sealed class Covered
        {
            public int Plot;
            public double Area;
            public int Rank;
        }

        // shapes: per candidate plot its ring and then its holes, in drawing units. axisX and axisY: the world directions of the axes of the
        // block as inserted, null when the drawing gave none, then the plots are ranked along the line through the two that lie farthest apart
        public static List<Covered> Of(IReadOnlyList<KeyValuePair<int, List<List<double[]>>>> shapes, List<double[]> outline, double[] axisX, double[] axisY)
        {
            var found = new List<Covered>();
            var centres = new List<double[]>();
            if (outline == null || outline.Count < 3)
            {
                return found;
            }
            double[] origin = outline[0];
            var clip = new Paths64 { Grid(outline, origin) };
            foreach (KeyValuePair<int, List<List<double[]>>> shape in shapes)
            {
                var subject = new Paths64(shape.Value.Where(ring => ring.Count >= 3).Select(ring => Grid(ring, origin)));
                Paths64 met = Clipper.Intersect(subject, clip, FillRule.EvenOdd);
                double area = Math.Abs(met.Sum(path => Clipper.Area(path)));
                if (area > 0)
                {
                    found.Add(new Covered { Plot = shape.Key, Area = area / (Scale * Scale) });
                    centres.Add(Centre(met));
                }
            }
            if (found.Count == 0)
            {
                return found;
            }
            double largest = found.Max(item => item.Area);
            var kept = Enumerable.Range(0, found.Count).Where(at => found[at].Area >= ShareOfLargest * largest).ToList();
            List<double[]> axes = axisX != null && axisY != null ? new List<double[]> { axisX, axisY } : new List<double[]> { Farthest(kept.Select(at => centres[at]).ToList()) };
            // The houses of a block stand in a row along one of its axes, the one their centres spread along the most
            double[] along = axes.OrderByDescending(axis => Spread(kept.Select(at => Dot(centres[at], axis)).ToList())).First();
            List<int> order = kept.OrderBy(at => Dot(centres[at], along)).ThenBy(at => found[at].Plot).ToList();
            for (int rank = 0; rank < order.Count; rank++)
            {
                found[order[rank]].Rank = rank;
            }
            return order.Select(at => found[at]).ToList();
        }

        // The hand of a plot under a block: the scale sign of the insert, turned at every step along the block
        public static bool Hand(bool blockMirrored, int rank)
        {
            return blockMirrored ^ (rank % 2 == 1);
        }

        private const double Scale = 1000.0;

        // Site coordinates near 2.4e6 lose the millimetre in a long, so each ring is taken from the first point of the outline
        private static Path64 Grid(List<double[]> ring, double[] origin)
        {
            return new Path64(ring.Select(point => new Point64((long)Math.Round((point[0] - origin[0]) * Scale), (long)Math.Round((point[1] - origin[1]) * Scale))));
        }

        private static double[] Centre(Paths64 paths)
        {
            double sum = 0, x = 0, y = 0;
            foreach (Path64 path in paths)
            {
                for (int at = 0; at < path.Count; at++)
                {
                    Point64 a = path[at], b = path[(at + 1) % path.Count];
                    double cross = (double)a.X * b.Y - (double)b.X * a.Y;
                    sum += cross;
                    x += (a.X + b.X) * cross;
                    y += (a.Y + b.Y) * cross;
                }
            }
            return Math.Abs(sum) < 1e-9 ? new[] { (double)paths[0][0].X, (double)paths[0][0].Y } : new[] { x / (3 * sum), y / (3 * sum) };
        }

        private static double[] Farthest(List<double[]> points)
        {
            double best = -1;
            double[] axis = { 1, 0 };
            for (int a = 0; a < points.Count; a++)
            {
                for (int b = a + 1; b < points.Count; b++)
                {
                    double dx = points[b][0] - points[a][0], dy = points[b][1] - points[a][1], length = Math.Sqrt(dx * dx + dy * dy);
                    if (length > best && length > 0)
                    {
                        best = length;
                        axis = new[] { dx / length, dy / length };
                    }
                }
            }
            return axis;
        }

        private static double Dot(double[] point, double[] axis)
        {
            return point[0] * axis[0] + point[1] * axis[1];
        }

        private static double Spread(List<double> values)
        {
            return values.Count == 0 ? 0 : values.Max() - values.Min();
        }
    }
}
