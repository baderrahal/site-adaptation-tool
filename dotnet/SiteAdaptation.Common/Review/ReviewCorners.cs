using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common.Review
{
    // R406. Corner 1 of a plot is the left front corner seen from the road: standing on the road in front of the front edge and facing
    // the plot, the end of the front edge that lies on the left. The corners then follow the ring from corner 1 along the front edge
    // to corner 2, so the ring runs on the way it started
    public static class ReviewCorners
    {
        public static double[][] Order(IReadOnlyList<double[]> ring, int frontEdge)
        {
            if (ring == null || ring.Count < 3)
            {
                throw new ArgumentException("a plot ring needs three points");
            }
            if (frontEdge < 0 || frontEdge >= ring.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(frontEdge), "the front edge " + frontEdge + " is not an edge of a ring of " + ring.Count + " points");
            }
            double[] start = ring[frontEdge], end = ring[(frontEdge + 1) % ring.Count];
            double dx = end[0] - start[0], dy = end[1] - start[1];
            // The outward normal of an edge of a ring that runs counter clockwise is (dy, -dx), the opposite for one that runs clockwise
            double sign = SignedArea(ring) >= 0 ? 1 : -1;
            double nx = sign * dy, ny = sign * -dx;
            // Facing the plot is facing against the normal, and the left of a direction (a, b) is (-b, a)
            double leftX = ny, leftY = -nx;
            bool startIsLeft = start[0] * leftX + start[1] * leftY >= end[0] * leftX + end[1] * leftY;
            var ordered = new double[ring.Count][];
            for (int at = 0; at < ring.Count; at++)
            {
                int index = startIsLeft ? (frontEdge + at) % ring.Count : ((frontEdge + 1 - at) % ring.Count + ring.Count) % ring.Count;
                ordered[at] = new[] { ring[index][0], ring[index][1] };
            }
            return ordered;
        }

        public static double SignedArea(IReadOnlyList<double[]> ring)
        {
            double twice = 0;
            for (int at = 0; at < ring.Count; at++)
            {
                double[] one = ring[at], two = ring[(at + 1) % ring.Count];
                twice += one[0] * two[1] - two[0] * one[1];
            }
            return twice / 2;
        }
    }
}
