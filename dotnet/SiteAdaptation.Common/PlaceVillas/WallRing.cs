using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R451, R503. The base of a mass in the internal plan coordinates of its prototype, metres, with where it came from and why. Ring is the
    // points R404, the footprint check and the sidecar read, Edges the same ring with its arcs kept for the mass file, one line per edge
    // for a slab
    public sealed class MassBase
    {
        public MassBase(IReadOnlyList<double[]> ring, string source, string reason, IReadOnlyList<RingEdge> edges = null)
        {
            Ring = ring;
            Source = source;
            Reason = reason;
            Edges = edges ?? Enumerable.Range(0, ring.Count).Select(i => new RingEdge(ring[i], ring[(i + 1) % ring.Count])).ToList();
        }

        public IReadOnlyList<double[]> Ring { get; }
        public IReadOnlyList<RingEdge> Edges { get; }

        // WallRing.FromWalls or WallRing.FromSlab
        public string Source { get; }
        public string Reason { get; }

        // The walls of the ring and the walls left out with why, null for a base read back from a sidecar
        public WallRingBuild Build { get; set; }

        // A base taken from the slab since the walls do not close, flagged with Q279
        public bool Flagged => Source == WallRing.FromSlab;
    }

    // R504, Q321. The one place of the slab fallback. A ring of WallRingBuilder is the base when it closed, is one simple ring, holds the
    // middle of the largest ground slab and covers minShare to maxShare of its area, maxShare so a ring that took in a garden wall of
    // Exterior function is not taken for the house. Else the base is the slab, the base of R404, flagged with the reason
    public static class WallRing
    {
        public const string FromWalls = "wallRing";
        public const string FromSlab = "slab";

        public static MassBase Choose(WallRingBuild build, IReadOnlyList<double[]> slab, double minShare, double maxShare, double arcStepM)
        {
            List<double[]> slabRing = Ring(slab);
            if (slabRing.Count < 3)
            {
                throw new InvalidDataException("the largest ground slab holds " + slabRing.Count + " corners, a mass base needs 3 (R504)");
            }
            MassBase Flag(string reason) => new MassBase(slabRing, FromSlab, reason) { Build = build };
            if (build == null || build.Edges.Count == 0)
            {
                return Flag(build?.Reason ?? "no exterior wall stands at the ground level");
            }
            List<double[]> outer = RingEdges.Points(build.Edges, arcStepM);
            if (!FaceRings.IsSimple(outer))
            {
                return Flag("the ring of the exterior walls crosses itself");
            }
            if (!RingMath.Holds(outer, RingMath.Centroid(slabRing)))
            {
                return Flag("the ring of the exterior walls does not hold the middle of the slab");
            }
            double slabArea = Math.Abs(RingMath.Area(slabRing));
            double share = slabArea == 0 ? 0 : Math.Abs(RingMath.Area(outer)) / slabArea;
            string shown = share.ToString("0.###", CultureInfo.InvariantCulture);
            if (share < minShare)
            {
                return Flag("the ring of the exterior walls covers " + shown + " of the slab area, under wallRingMinShare " + minShare.ToString(CultureInfo.InvariantCulture));
            }
            if (share > maxShare)
            {
                return Flag("the ring of the exterior walls covers " + shown + " of the slab area, over wallRingMaxShare " + maxShare.ToString(CultureInfo.InvariantCulture));
            }
            return new MassBase(outer, FromWalls, "the exterior walls close into a ring of " + shown + " of the slab area", build.Edges) { Build = build };
        }

        private static List<double[]> Ring(IReadOnlyList<double[]> points)
        {
            return FaceRings.Clean((points ?? new double[0][]).Where(point => point != null && point.Length >= 2).Select(point => new[] { point[0], point[1] }));
        }
    }
}
