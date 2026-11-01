using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R451. The base of a mass in the internal plan coordinates of its prototype, metres, with where it came from and why
    public sealed class MassBase
    {
        public MassBase(IReadOnlyList<double[]> ring, string source, string reason)
        {
            Ring = ring;
            Source = source;
            Reason = reason;
        }

        public IReadOnlyList<double[]> Ring { get; }

        // WallRing.FromWalls or WallRing.FromSlab
        public string Source { get; }
        public string Reason { get; }

        // A base taken from the slab since the walls do not close, flagged with Q279
        public bool Flagged => Source == WallRing.FromSlab;
    }

    // R451. The mass base follows the outer face of the exterior walls at ground level, every corner. The walls close into a ring when
    // the outer loop of their union holds the middle of the largest ground slab and covers at least minShare of its area (CHOSEN, minShare
    // in config). Else the base is the slab, the base of R404, and the prototype is flagged
    public static class WallRing
    {
        public const string FromWalls = "wallRing";
        public const string FromSlab = "slab";

        public static MassBase Choose(IEnumerable<IReadOnlyList<double[]>> loops, IReadOnlyList<double[]> slab, double minShare)
        {
            List<double[]> slabRing = Ring(slab);
            if (slabRing.Count < 3)
            {
                throw new InvalidDataException("the largest ground slab holds " + slabRing.Count + " corners, a mass base needs 3 (R451)");
            }
            double slabArea = Math.Abs(Area(slabRing));
            List<double[]> outer = (loops ?? Enumerable.Empty<IReadOnlyList<double[]>>()).Where(loop => loop != null).Select(Ring).Where(ring => ring.Count >= 3)
                .OrderByDescending(ring => Math.Abs(Area(ring))).FirstOrDefault();
            if (outer == null)
            {
                return new MassBase(slabRing, FromSlab, "no exterior wall was cut at the ground level");
            }
            if (!FaceRings.IsSimple(outer))
            {
                return new MassBase(slabRing, FromSlab, "the outer loop of the exterior walls is not a simple ring");
            }
            if (!Holds(outer, Centroid(slabRing)))
            {
                return new MassBase(slabRing, FromSlab, "the outer loop of the exterior walls does not hold the middle of the slab, the walls do not close into a ring");
            }
            double share = slabArea == 0 ? 0 : Math.Abs(Area(outer)) / slabArea;
            string shown = share.ToString("0.###", CultureInfo.InvariantCulture);
            if (share < minShare)
            {
                return new MassBase(slabRing, FromSlab, "the outer loop of the exterior walls covers " + shown + " of the slab area, under minShare " + minShare.ToString(CultureInfo.InvariantCulture));
            }
            return new MassBase(outer, FromWalls, "the exterior walls close into a ring of " + shown + " of the slab area");
        }

        private static List<double[]> Ring(IReadOnlyList<double[]> points)
        {
            return FaceRings.Clean((points ?? new double[0][]).Where(point => point != null && point.Length >= 2).Select(point => new[] { point[0], point[1] }));
        }

        internal static double Area(IReadOnlyList<double[]> ring)
        {
            double area = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                double[] a = ring[i], b = ring[(i + 1) % ring.Count];
                area += a[0] * b[1] - b[0] * a[1];
            }
            return area / 2;
        }

        // The area centroid, so a slab with many corners on one side is not pulled toward them as the mean of the corners would be
        private static double[] Centroid(IReadOnlyList<double[]> ring)
        {
            double area = Area(ring), x = 0, y = 0;
            if (area == 0)
            {
                return new[] { ring.Average(point => point[0]), ring.Average(point => point[1]) };
            }
            for (int i = 0; i < ring.Count; i++)
            {
                double[] a = ring[i], b = ring[(i + 1) % ring.Count];
                double cross = a[0] * b[1] - b[0] * a[1];
                x += (a[0] + b[0]) * cross;
                y += (a[1] + b[1]) * cross;
            }
            return new[] { x / (6 * area), y / (6 * area) };
        }

        private static bool Holds(IReadOnlyList<double[]> ring, double[] point)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                if ((ring[i][1] > point[1]) != (ring[j][1] > point[1])
                    && point[0] < (ring[j][0] - ring[i][0]) * (point[1] - ring[i][1]) / (ring[j][1] - ring[i][1]) + ring[i][0])
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
