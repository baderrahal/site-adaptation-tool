using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R530, Q391. The lines of the plot in the frame of a prototype, addin.lightMode.massPartyLines by unit. A prototype file models the wall it
    // shares whole, so its ring stands past the plot on that side on every plot of the unit
    public sealed class PartyLine
    {
        public double? MinX { get; set; }
        public double? MaxX { get; set; }

        public string Text
        {
            get { return (MinX?.ToString("R", CultureInfo.InvariantCulture) ?? "") + " to " + (MaxX?.ToString("R", CultureInfo.InvariantCulture) ?? ""); }
        }
    }

    // R530. The base of a mass cut at the party lines of its unit, the sides of the ring inside them stay on the outer wall faces. Mirror and
    // rotation are the placement's, so one cut holds on every plot of the unit
    public static class MassCut
    {
        public static MassBase Apply(MassBase mass, PartyLine line, double shortestEdgeM)
        {
            if (line == null || !mass.Ring.Any(point => Past(point, line)))
            {
                return mass;
            }
            // No prototype has an arc wall (READ, the 13 sidecars), a cut across one is not built, so the base stays whole and says so
            if (mass.Edges.Any(edge => edge.IsArc))
            {
                return Whole(mass, "not cut at the party lines " + line.Text + ", the ring holds an arc");
            }
            List<double[]> ring = mass.Ring.ToList();
            if (line.MinX.HasValue)
            {
                ring = Clip(ring, line.MinX.Value, 1);
            }
            if (line.MaxX.HasValue)
            {
                ring = Clip(ring, line.MaxX.Value, -1);
            }
            ring = NoShortEdge(ring, shortestEdgeM);
            // A concave side the line crosses more than once would join two parts along the line
            if (ring.Count < 3 || !FaceRings.IsSimple(ring))
            {
                return Whole(mass, "not cut at the party lines " + line.Text + ", the cut does not leave one ring");
            }
            return Copy(mass, ring, mass.Reason + ", cut at the party lines " + line.Text, Math.Abs(RingMath.Area(mass.Ring)), null);
        }

        private static bool Past(double[] point, PartyLine line)
        {
            return (line.MinX.HasValue && point[0] < line.MinX.Value) || (line.MaxX.HasValue && point[0] > line.MaxX.Value);
        }

        // The ring on the side of x that side gives, 1 keeps what is over x, -1 what is under it
        private static List<double[]> Clip(IReadOnlyList<double[]> ring, double x, int side)
        {
            var kept = new List<double[]>();
            for (int at = 0; at < ring.Count; at++)
            {
                double[] from = ring[at], to = ring[(at + 1) % ring.Count];
                double a = (from[0] - x) * side, b = (to[0] - x) * side;
                if (a >= 0)
                {
                    kept.Add(from);
                }
                if ((a > 0 && b < 0) || (a < 0 && b > 0))
                {
                    kept.Add(new[] { x, from[1] + (to[1] - from[1]) * (x - from[0]) / (to[0] - from[0]) });
                }
            }
            return kept;
        }

        // R503. A corner closer to the corner kept before it than the shortest edge goes, as the ring builder merges its short edges
        private static List<double[]> NoShortEdge(IReadOnlyList<double[]> ring, double shortestM)
        {
            var kept = new List<double[]>();
            foreach (double[] point in ring)
            {
                if (kept.Count == 0 || RingGeometry.Distance(kept[kept.Count - 1], point) >= shortestM)
                {
                    kept.Add(point);
                }
            }
            if (kept.Count > 1 && RingGeometry.Distance(kept[kept.Count - 1], kept[0]) < shortestM)
            {
                kept.RemoveAt(kept.Count - 1);
            }
            return kept;
        }

        private static MassBase Whole(MassBase mass, string why)
        {
            return Copy(mass, mass.Ring, mass.Reason + ", " + why, null, why);
        }

        private static MassBase Copy(MassBase mass, IReadOnlyList<double[]> ring, string reason, double? uncutArea, string notCut)
        {
            bool same = ReferenceEquals(ring, mass.Ring);
            return new MassBase(ring, mass.Source, reason, same ? mass.Edges : null)
            {
                Build = mass.Build, Skipped = mass.Skipped, BackFace = mass.BackFace, BuiltAreaM2 = mass.BuiltAreaM2, SlabAreaM2 = mass.SlabAreaM2, UncutAreaM2 = uncutArea, NotCut = notCut,
            };
        }
    }
}
