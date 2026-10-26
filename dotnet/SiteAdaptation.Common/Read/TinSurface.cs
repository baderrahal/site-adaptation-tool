using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    // R349. The visible triangles of one TIN with a grid index, the C# of python/surface_lookup.Surface step for step, so a level is
    // the one the Python lookup gives: the corners taken about the lowest easting and northing of every point, a triangle of no area
    // or a determinant that is not finite dropped, the cell the median of the larger side of the triangles, at least 0.5 m, made 1.5
    // times larger until the grid holds at most 12 entries per triangle, and the triangles of a cell in the order of their index
    public sealed class TinSurface
    {
        // Barycentric tolerance, a point on an edge or a vertex counts as inside
        public const double Eps = 1e-9;
        private const int MaxEntries = 12;
        private const double MinCell = 0.5;

        private readonly double[] x;
        private readonly double[] y;
        private readonly double[] z;
        private readonly double[] det;
        private readonly Dictionary<long, int[]> cells = new Dictionary<long, int[]>();

        public TinSurface(string name, double[] points, int[] faces)
        {
            Name = name;
            PointCount = points.Length / 3;
            double originX = 0, originY = 0;
            if (PointCount > 0)
            {
                originX = double.PositiveInfinity;
                originY = double.PositiveInfinity;
                for (int point = 0; point < PointCount; point++)
                {
                    originX = Math.Min(originX, points[point * 3]);
                    originY = Math.Min(originY, points[point * 3 + 1]);
                }
            }
            OriginX = originX;
            OriginY = originY;
            int triangles = faces.Length / 3;
            var keepX = new List<double>(triangles * 3);
            var keepY = new List<double>(triangles * 3);
            var keepZ = new List<double>(triangles * 3);
            var keepDet = new List<double>(triangles);
            for (int face = 0; face < triangles; face++)
            {
                var cx = new double[3];
                var cy = new double[3];
                var cz = new double[3];
                for (int corner = 0; corner < 3; corner++)
                {
                    int point = faces[face * 3 + corner];
                    cx[corner] = points[point * 3] - originX;
                    cy[corner] = points[point * 3 + 1] - originY;
                    cz[corner] = points[point * 3 + 2];
                }
                double d = (cy[1] - cy[2]) * (cx[0] - cx[2]) + (cx[2] - cx[1]) * (cy[0] - cy[2]);
                if (double.IsNaN(d) || double.IsInfinity(d) || d == 0)
                {
                    Degenerate++;
                    continue;
                }
                keepX.AddRange(cx);
                keepY.AddRange(cy);
                keepZ.AddRange(cz);
                keepDet.Add(d);
            }
            x = keepX.ToArray();
            y = keepY.ToArray();
            z = keepZ.ToArray();
            det = keepDet.ToArray();
            Count = det.Length;
            BuildGrid();
        }

        public string Name { get; }
        public int PointCount { get; }
        public int Count { get; }
        public int Degenerate { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public double Cell { get; private set; } = 1.0;
        public long Nx { get; private set; }
        public long Ny { get; private set; }

        public static TinSurface Of(LandXmlSurface surface)
        {
            return new TinSurface(surface.Name, surface.Points, surface.Faces);
        }

        private void BuildGrid()
        {
            if (Count == 0)
            {
                return;
            }
            var loX = new double[Count];
            var hiX = new double[Count];
            var loY = new double[Count];
            var hiY = new double[Count];
            var span = new double[Count];
            for (int t = 0; t < Count; t++)
            {
                loX[t] = Math.Min(Math.Min(x[t * 3], x[t * 3 + 1]), x[t * 3 + 2]);
                hiX[t] = Math.Max(Math.Max(x[t * 3], x[t * 3 + 1]), x[t * 3 + 2]);
                loY[t] = Math.Min(Math.Min(y[t * 3], y[t * 3 + 1]), y[t * 3 + 2]);
                hiY[t] = Math.Max(Math.Max(y[t * 3], y[t * 3 + 1]), y[t * 3 + 2]);
                span[t] = Math.Max(hiX[t] - loX[t], hiY[t] - loY[t]);
            }
            double cell = Math.Max(Median(span), MinCell);
            long[] i0, i1, j0, j1;
            while (true)
            {
                i0 = loX.Select(value => (long)Math.Floor(value / cell)).ToArray();
                i1 = hiX.Select(value => (long)Math.Floor(value / cell)).ToArray();
                j0 = loY.Select(value => (long)Math.Floor(value / cell)).ToArray();
                j1 = hiY.Select(value => (long)Math.Floor(value / cell)).ToArray();
                long entries = 0;
                for (int t = 0; t < Count; t++)
                {
                    entries += (i1[t] - i0[t] + 1) * (j1[t] - j0[t] + 1);
                }
                if (entries <= (long)MaxEntries * Count)
                {
                    break;
                }
                cell *= 1.5;
            }
            Cell = cell;
            Nx = i1.Max() + 1;
            Ny = j1.Max() + 1;
            var lists = new Dictionary<long, List<int>>();
            for (int t = 0; t < Count; t++)
            {
                for (long i = i0[t]; i <= i1[t]; i++)
                {
                    for (long j = j0[t]; j <= j1[t]; j++)
                    {
                        long key = i * Ny + j;
                        List<int> list;
                        if (!lists.TryGetValue(key, out list))
                        {
                            list = new List<int>();
                            lists[key] = list;
                        }
                        list.Add(t);
                    }
                }
            }
            foreach (KeyValuePair<long, List<int>> pair in lists)
            {
                cells[pair.Key] = pair.Value.ToArray();
            }
        }

        // numpy.median: the middle value, or the mean of the two middle values of an even count
        private static double Median(double[] values)
        {
            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
        }

        private int[] Candidates(double px, double py)
        {
            long i = (long)Math.Floor(px / Cell), j = (long)Math.Floor(py / Cell);
            if (i < 0 || j < 0 || i >= Nx || j >= Ny)
            {
                return new int[0];
            }
            int[] found;
            return cells.TryGetValue(i * Ny + j, out found) ? found : new int[0];
        }

        // The level interpolated from the visible triangle that holds the point, or null. The triangle of the lowest index in the
        // cell wins on a shared edge
        public double? ZAt(double easting, double northing)
        {
            if (Count == 0)
            {
                return null;
            }
            double px = easting - OriginX, py = northing - OriginY;
            foreach (int t in Candidates(px, py))
            {
                int a = t * 3, b = a + 1, c = a + 2;
                double l0 = ((y[b] - y[c]) * (px - x[c]) + (x[c] - x[b]) * (py - y[c])) / det[t];
                double l1 = ((y[c] - y[a]) * (px - x[c]) + (x[a] - x[c]) * (py - y[c])) / det[t];
                double l2 = 1.0 - l0 - l1;
                if (l0 >= -Eps && l1 >= -Eps && l2 >= -Eps)
                {
                    return l0 * z[a] + l1 * z[b] + l2 * z[c];
                }
            }
            return null;
        }

        // R157, c3d_connect.snap_z. The level where the point is, or at the nearest point of a triangle side within tolerance, else
        // null: a plot corner on the line a surface stops at falls outside its last triangle by a fraction of a millimetre
        public double? SnapZ(double easting, double northing, double tolerance)
        {
            double? level = ZAt(easting, northing);
            if (level.HasValue || Count == 0)
            {
                return level;
            }
            double px = easting - OriginX, py = northing - OriginY;
            long i0 = (long)Math.Floor((px - tolerance) / Cell), i1 = (long)Math.Floor((px + tolerance) / Cell);
            long j0 = Math.Max((long)Math.Floor((py - tolerance) / Cell), 0), j1 = Math.Min((long)Math.Floor((py + tolerance) / Cell), Ny - 1);
            var near = new SortedSet<int>();
            bool any = false;
            for (long i = Math.Max(i0, 0); i <= Math.Min(i1, Nx - 1); i++)
            {
                any = true;
                for (long j = j0; j <= j1; j++)
                {
                    int[] found;
                    if (cells.TryGetValue(i * Ny + j, out found))
                    {
                        near.UnionWith(found);
                    }
                }
            }
            if (!any || j0 > j1 || near.Count == 0)
            {
                return null;
            }
            double bestDistance = 0, bestLevel = 0;
            bool held = false;
            foreach (int[] side in new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 0 } })
            {
                double sideDistance = double.PositiveInfinity, sideLevel = 0;
                foreach (int t in near)
                {
                    int a = t * 3 + side[0], b = t * 3 + side[1];
                    double dx = x[b] - x[a], dy = y[b] - y[a];
                    double length = dx * dx + dy * dy;
                    double along = length > 0 ? ((px - x[a]) * dx + (py - y[a]) * dy) / length : 0;
                    along = Math.Min(Math.Max(along, 0), 1);
                    double ex = px - (x[a] + along * dx), ey = py - (y[a] + along * dy);
                    double distance = Math.Sqrt(ex * ex + ey * ey);
                    // numpy argmin keeps the first of equal distances
                    if (distance < sideDistance)
                    {
                        sideDistance = distance;
                        sideLevel = z[a] + along * (z[b] - z[a]);
                    }
                }
                if (!held || sideDistance < bestDistance)
                {
                    held = true;
                    bestDistance = sideDistance;
                    bestLevel = sideLevel;
                }
            }
            return bestDistance <= tolerance ? bestLevel : (double?)null;
        }

        // R209, plan_from_book.road_level. The level of the first surface of the order that covers the point, snapped to its edge
        // within snap, and the name of that surface, else no level and no name
        public static KeyValuePair<double?, string> RoadLevel(IEnumerable<TinSurface> surfaces, double easting, double northing, double snap)
        {
            foreach (TinSurface surface in surfaces)
            {
                double? level = surface.SnapZ(easting, northing, snap);
                if (level.HasValue)
                {
                    return new KeyValuePair<double?, string>(level, surface.Name);
                }
            }
            return new KeyValuePair<double?, string>(null, null);
        }
    }
}
