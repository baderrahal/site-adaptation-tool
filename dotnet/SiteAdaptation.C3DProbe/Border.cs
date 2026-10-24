using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.C3DProbe
{
    // The Phase 1 border of the data book (R162). A point is inside when it lies in the ring, a line is kept where any part
    // of it lies inside and is cut at the border, so every row of a table is either inside or counted as dropped
    public sealed class Border
    {
        private readonly double[] xs;
        private readonly double[] ys;

        public Border(IList<double[]> ring)
        {
            var points = ring.ToList();
            if (points.Count > 1 && points[0][0] == points[points.Count - 1][0] && points[0][1] == points[points.Count - 1][1])
            {
                points.RemoveAt(points.Count - 1);
            }
            if (points.Count < 3)
            {
                throw new ArgumentException("a border needs three vertices or more");
            }
            xs = points.Select(p => p[0]).ToArray();
            ys = points.Select(p => p[1]).ToArray();
            MinX = xs.Min();
            MinY = ys.Min();
            MaxX = xs.Max();
            MaxY = ys.Max();
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public int Count => xs.Length;

        public static Border FromCsv(IEnumerable<string> lines)
        {
            var ring = new List<double[]>();
            foreach (string line in lines)
            {
                string[] cells = line.Split(',');
                if (cells.Length >= 2
                    && double.TryParse(cells[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                    && double.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    ring.Add(new[] { x, y });
                }
            }
            return new Border(ring);
        }

        // Even-odd ray test. A point on an edge may fall either way, the tables only need one fixed answer
        public bool Contains(double x, double y)
        {
            if (x < MinX || x > MaxX || y < MinY || y > MaxY)
            {
                return false;
            }
            bool inside = false;
            for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
            {
                if ((ys[i] > y) != (ys[j] > y) && x < (xs[j] - xs[i]) * (y - ys[i]) / (ys[j] - ys[i]) + xs[i])
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // The parts of a polyline inside the border. Each segment is split where it crosses a border edge and the pieces
        // whose middle is inside are kept, joined while they touch. Z is carried along each segment. Cut says the parts are
        // not the whole line
        public ClipResult Clip(IList<double[]> points)
        {
            var result = new ClipResult();
            if (points == null || points.Count == 0)
            {
                return result;
            }
            if (points.Count == 1)
            {
                if (Contains(points[0][0], points[0][1]))
                {
                    result.Parts.Add(new List<double[]> { points[0] });
                }
                result.Cut = false;
                return result;
            }
            List<double[]> current = null;
            bool anyOutside = false;
            for (int k = 0; k + 1 < points.Count; k++)
            {
                double[] a = points[k];
                double[] b = points[k + 1];
                var ts = new List<double> { 0, 1 };
                ts.AddRange(Crossings(a, b));
                ts.Sort();
                for (int s = 0; s + 1 < ts.Count; s++)
                {
                    double t0 = ts[s];
                    double t1 = ts[s + 1];
                    if (t1 - t0 < 1e-12)
                    {
                        continue;
                    }
                    double[] mid = At(a, b, (t0 + t1) / 2);
                    if (Contains(mid[0], mid[1]))
                    {
                        double[] start = At(a, b, t0);
                        if (current == null)
                        {
                            current = new List<double[]> { start };
                            result.Parts.Add(current);
                        }
                        current.Add(At(a, b, t1));
                    }
                    else
                    {
                        anyOutside = true;
                        current = null;
                    }
                }
            }
            result.Cut = anyOutside && result.Parts.Count > 0;
            return result;
        }

        private IEnumerable<double> Crossings(double[] a, double[] b)
        {
            double dx = b[0] - a[0];
            double dy = b[1] - a[1];
            for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
            {
                double ex = xs[i] - xs[j];
                double ey = ys[i] - ys[j];
                double den = dx * ey - dy * ex;
                if (Math.Abs(den) < 1e-15)
                {
                    continue;
                }
                double t = ((xs[j] - a[0]) * ey - (ys[j] - a[1]) * ex) / den;
                double u = ((xs[j] - a[0]) * dy - (ys[j] - a[1]) * dx) / den;
                if (t > 0 && t < 1 && u >= 0 && u <= 1)
                {
                    yield return t;
                }
            }
        }

        private static double[] At(double[] a, double[] b, double t)
        {
            double za = a.Length > 2 ? a[2] : 0;
            double zb = b.Length > 2 ? b[2] : 0;
            return new[] { a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]), za + t * (zb - za) };
        }

        public static double Length(IList<double[]> part)
        {
            double total = 0;
            for (int k = 0; k + 1 < part.Count; k++)
            {
                total += Math.Sqrt(Math.Pow(part[k + 1][0] - part[k][0], 2) + Math.Pow(part[k + 1][1] - part[k][1], 2));
            }
            return total;
        }
    }

    public sealed class ClipResult
    {
        public List<List<double[]>> Parts { get; } = new List<List<double[]>>();
        public bool Cut { get; set; }
    }
}
