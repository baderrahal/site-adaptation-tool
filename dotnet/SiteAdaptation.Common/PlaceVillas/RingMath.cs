using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R451, R453. The ring area and point test of the mass base, the footprint check and the house rings of the CAD read, so the three
    // never part on a ring. The area is FloorMath.SignedArea and the point test ScanLogic.Contains, one copy each in Common, and the
    // centroid here is the one PlotLabels.Centroid of the CAD reader calls
    public static class RingMath
    {
        // Signed by the shoelace, counter clockwise above 0
        public static double Area(IReadOnlyList<double[]> ring)
        {
            return FloorMath.SignedArea(ring);
        }

        // By the crossings of a ray to the east, a point on an edge may count in or out
        public static bool Holds(IReadOnlyList<double[]> ring, double[] point)
        {
            return ScanLogic.Contains(ring, point[0], point[1]);
        }

        // The area centroid, so a ring with many corners on one side is not pulled toward them as the mean of the corners would be. The
        // sums run from the closing edge as the label join of the CAD reader has always run them, so its centres keep every bit
        public static double[] Centroid(IReadOnlyList<double[]> ring)
        {
            double twice = 0;
            double cx = 0;
            double cy = 0;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                double cross = ring[j][0] * ring[i][1] - ring[i][0] * ring[j][1];
                twice += cross;
                cx += (ring[j][0] + ring[i][0]) * cross;
                cy += (ring[j][1] + ring[i][1]) * cross;
            }
            if (Math.Abs(twice) < 1e-12)
            {
                return new[] { ring.Average(p => p[0]), ring.Average(p => p[1]) };
            }
            return new[] { cx / (3 * twice), cy / (3 * twice) };
        }
    }
}
