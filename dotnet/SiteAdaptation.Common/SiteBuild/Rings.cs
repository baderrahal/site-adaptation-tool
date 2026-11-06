using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace SiteAdaptation.Common.SiteBuild
{
    // R646. Outlines as the build needs them: area, box, hull, an overlap over a distance and a boundary grown from the outlines of a neighbourhood. All
    // clipping is done on whole millimetres counted from one origin of the site, so a coordinate of millions of metres leaves room in a long
    internal static class Rings
    {
        public static double Area(IList<double[]> ring)
        {
            double sum = 0;
            for (int at = 1; at < ring.Count - 1; at++)
            {
                sum += (ring[at][0] - ring[0][0]) * (ring[at + 1][1] - ring[0][1]) - (ring[at + 1][0] - ring[0][0]) * (ring[at][1] - ring[0][1]);
            }
            return Math.Abs(sum) / 2;
        }

        // Least east, most east, least north, most north
        public static double[] Box(IEnumerable<double[]> points)
        {
            List<double[]> all = points.ToList();
            return new[] { all.Min(point => point[0]), all.Max(point => point[0]), all.Min(point => point[1]), all.Max(point => point[1]) };
        }

        // The convex hull by the monotone chain, counter clockwise, the corner points only. The hull is what the toposolid of the past job was cut to, kept for the
        // comparison with the cut to the neighbourhood
        public static List<double[]> Hull(IEnumerable<double[]> points)
        {
            List<double[]> sorted = points.OrderBy(point => point[0]).ThenBy(point => point[1]).ToList();
            if (sorted.Count < 3)
            {
                return sorted;
            }
            var hull = new List<double[]>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                foreach (double[] point in pass == 0 ? sorted : Enumerable.Reverse(sorted))
                {
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                    {
                        hull.RemoveAt(hull.Count - 1);
                    }
                    hull.Add(point);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }

        private static double Cross(double[] o, double[] a, double[] b)
        {
            return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
        }

        private static Path64 Grid(IEnumerable<double[]> ring, double[] origin)
        {
            var path = new Path64();
            foreach (double[] point in ring)
            {
                path.Add(new Point64((long)Math.Round((point[0] - origin[0]) * 1000.0), (long)Math.Round((point[1] - origin[1]) * 1000.0)));
            }
            // The clipper grows a ring of positive orientation outward and shrinks it inward, so every ring is turned to that orientation first
            if (!Clipper.IsPositive(path))
            {
                path.Reverse();
            }
            return path;
        }

        // True when the two outlines overlap by more than minMm: the first, shrunk by minMm all round, still meets the second. Two outlines that share an edge
        // or touch at a corner do not overlap by that measure
        public static bool OverlapsOver(IList<double[]> one, IList<double[]> other, double minMm, double[] origin)
        {
            Paths64 shrunk = Clipper.InflatePaths(new Paths64 { Grid(one, origin) }, -minMm, JoinType.Miter, EndType.Polygon);
            if (shrunk.Count == 0)
            {
                return false;
            }
            Paths64 met = Clipper.Intersect(shrunk, new Paths64 { Grid(other, origin) }, FillRule.NonZero);
            return met.Count > 0 && Math.Abs(met.Sum(path => Clipper.Area(path))) > 0;
        }

        // One boundary ring of a neighbourhood and whether it is a hole in the boundary
        internal sealed class Boundary
        {
            public List<double[]> Ring { get; set; }
            public bool Hole { get; set; }
        }

        // The outlines joined and grown by marginM all round, so the boundary follows the plots and not a hull of them. Holes of the join that the margin leaves are
        // given as holes. Rings in the coordinates of the outlines
        public static List<Boundary> Grown(IEnumerable<IList<double[]>> outlines, double marginM, double[] origin)
        {
            var paths = new Paths64();
            foreach (IList<double[]> ring in outlines)
            {
                paths.Add(Grid(ring, origin));
            }
            Paths64 joined = Clipper.Union(paths, FillRule.NonZero);
            Paths64 grown = Clipper.InflatePaths(joined, marginM * 1000.0, JoinType.Miter, EndType.Polygon, 2.0);
            var found = new List<Boundary>();
            foreach (Path64 path in grown)
            {
                if (path.Count < 3)
                {
                    continue;
                }
                found.Add(new Boundary
                {
                    Ring = path.Select(point => new[] { point.X / 1000.0 + origin[0], point.Y / 1000.0 + origin[1] }).ToList(),
                    Hole = !Clipper.IsPositive(path),
                });
            }
            return found;
        }
    }
}
