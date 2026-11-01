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
    // the outer loop of their union holds the middle of the largest ground slab and covers minShare to maxShare of its area (CHOSEN, both
    // in config), maxShare so a garden wall of Exterior function in the union is not taken for the house. Else the base is the slab, the
    // base of R404, and the prototype is flagged
    public static class WallRing
    {
        public const string FromWalls = "wallRing";
        public const string FromSlab = "slab";

        public static MassBase Choose(IEnumerable<IReadOnlyList<double[]>> loops, IReadOnlyList<double[]> slab, double minShare, double maxShare)
        {
            List<double[]> slabRing = Ring(slab);
            if (slabRing.Count < 3)
            {
                throw new InvalidDataException("the largest ground slab holds " + slabRing.Count + " corners, a mass base needs 3 (R451)");
            }
            double slabArea = Math.Abs(RingMath.Area(slabRing));
            List<double[]> outer = (loops ?? Enumerable.Empty<IReadOnlyList<double[]>>()).Where(loop => loop != null).Select(Ring).Where(ring => ring.Count >= 3)
                .OrderByDescending(ring => Math.Abs(RingMath.Area(ring))).FirstOrDefault();
            if (outer == null)
            {
                return new MassBase(slabRing, FromSlab, "no exterior wall stands at the ground level");
            }
            if (!FaceRings.IsSimple(outer))
            {
                return new MassBase(slabRing, FromSlab, "the outer loop of the exterior walls is not a simple ring");
            }
            if (!RingMath.Holds(outer, RingMath.Centroid(slabRing)))
            {
                return new MassBase(slabRing, FromSlab, "the outer loop of the exterior walls does not hold the middle of the slab, the walls do not close into a ring");
            }
            double share = slabArea == 0 ? 0 : Math.Abs(RingMath.Area(outer)) / slabArea;
            string shown = share.ToString("0.###", CultureInfo.InvariantCulture);
            if (share < minShare)
            {
                return new MassBase(slabRing, FromSlab, "the outer loop of the exterior walls covers " + shown + " of the slab area, under minShare " + minShare.ToString(CultureInfo.InvariantCulture));
            }
            if (share > maxShare)
            {
                return new MassBase(slabRing, FromSlab, "the outer loop of the exterior walls covers " + shown + " of the slab area, over maxShare " + maxShare.ToString(CultureInfo.InvariantCulture));
            }
            return new MassBase(outer, FromWalls, "the exterior walls close into a ring of " + shown + " of the slab area");
        }

        private static List<double[]> Ring(IReadOnlyList<double[]> points)
        {
            return FaceRings.Clean((points ?? new double[0][]).Where(point => point != null && point.Length >= 2).Select(point => new[] { point[0], point[1] }));
        }
    }
}
