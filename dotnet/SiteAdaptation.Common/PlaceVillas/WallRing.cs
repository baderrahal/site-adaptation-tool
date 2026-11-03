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

        // The ids of the walls with a face that sees the outside that the walk of this ring passed by, each a doubt on the ring (R502)
        public IReadOnlyList<string> Skipped { get; set; } = new List<string>();

        // The ids of the ring walls whose far face is the outer one, their Orientation faces the house (R501)
        public IReadOnlyList<string> BackFace { get; set; } = new List<string>();

        // R504, for the table per prototype of the sidecar. Built is the area of the ring the walls gave, null when they gave none, also
        // when the slab was chosen over it
        public double? BuiltAreaM2 { get; set; }
        public double SlabAreaM2 { get; set; }

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
            double slabArea = Math.Abs(RingMath.Area(slabRing));
            List<double[]> outer = build != null && build.Edges.Count > 0 ? RingEdges.Points(build.Edges, arcStepM) : null;
            double? built = outer == null ? (double?)null : Math.Abs(RingMath.Area(outer));
            MassBase Flag(string reason) => new MassBase(slabRing, FromSlab, reason) { Build = build, BuiltAreaM2 = built, SlabAreaM2 = slabArea };
            if (outer == null)
            {
                return Flag(build?.Reason ?? "no exterior wall stands at the ground level");
            }
            if (!FaceRings.IsSimple(outer))
            {
                return Flag("the ring of the exterior walls crosses itself");
            }
            if (!RingMath.Holds(outer, RingMath.Centroid(slabRing)))
            {
                return Flag("the ring of the exterior walls does not hold the middle of the slab");
            }
            double share = slabArea == 0 ? 0 : built.Value / slabArea;
            string shown = share.ToString("0.###", CultureInfo.InvariantCulture);
            if (share < minShare)
            {
                return Flag("the ring of the exterior walls covers " + shown + " of the slab area, under wallRingMinShare " + minShare.ToString(CultureInfo.InvariantCulture));
            }
            if (share > maxShare)
            {
                return Flag("the ring of the exterior walls covers " + shown + " of the slab area, over wallRingMaxShare " + maxShare.ToString(CultureInfo.InvariantCulture));
            }
            return new MassBase(outer, FromWalls, "the exterior walls close into a ring of " + shown + " of the slab area", build.Edges) { Build = build, Skipped = build.Skipped.Select(wall => wall.Id).ToList(), BackFace = build.BackFace.ToList(), BuiltAreaM2 = built, SlabAreaM2 = slabArea };
        }

        private static List<double[]> Ring(IReadOnlyList<double[]> points)
        {
            return FaceRings.Clean((points ?? new double[0][]).Where(point => point != null && point.Length >= 2).Select(point => new[] { point[0], point[1] }));
        }
    }
}
