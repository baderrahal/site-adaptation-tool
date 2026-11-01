using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R451, R453. The one ring area and point test of the mass base, the footprint check and the house rings of the CAD read, so the
    // three never part on a ring
    public static class RingMath
    {
        // Signed by the shoelace, counter clockwise above 0
        public static double Area(IReadOnlyList<double[]> ring)
        {
            double area = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                double[] a = ring[i], b = ring[(i + 1) % ring.Count];
                area += a[0] * b[1] - b[0] * a[1];
            }
            return area / 2;
        }

        // By the crossings of a ray to the east, a point on an edge may count in or out
        public static bool Holds(IReadOnlyList<double[]> ring, double[] point)
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

        // The area centroid, so a ring with many corners on one side is not pulled toward them as the mean of the corners would be
        public static double[] Centroid(IReadOnlyList<double[]> ring)
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
    }
}
