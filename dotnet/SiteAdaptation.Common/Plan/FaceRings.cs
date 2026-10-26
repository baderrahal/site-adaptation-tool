using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R345. A baseplan face the build refuses in C# and in Python alike: a ring that is not simple, two of its edges meeting other
    // than two neighbours at their shared vertex, such as a bow tie, a fold back or a ring that touches itself, and a label whose
    // rings overlap, such as an outline drawn twice. yard_rule.face_shapes mends both by buffer(0) and a union, buffer(0) of a bow tie
    // keeping one lobe, while FaceStep sums the ring areas, so a face of either kind would differ on the two sides of the parity.
    // python/face_rings.py holds the same checks step for step, plain doubles about the first vertex
    public static class FaceRings
    {
        // The vertices of a ring with a vertex equal to the one before it dropped, the closing one among them
        public static List<double[]> Clean(IEnumerable<double[]> ring)
        {
            var found = new List<double[]>();
            foreach (double[] vertex in ring)
            {
                if (found.Count == 0 || vertex[0] != found[found.Count - 1][0] || vertex[1] != found[found.Count - 1][1])
                {
                    found.Add(new[] { vertex[0], vertex[1] });
                }
            }
            if (found.Count > 1 && found[0][0] == found[found.Count - 1][0] && found[0][1] == found[found.Count - 1][1])
            {
                found.RemoveAt(found.Count - 1);
            }
            return found;
        }

        private static double Orient(double[] p, double[] q, double[] r)
        {
            return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]);
        }

        // q, on the line of p and r, lies on the segment from p to r
        private static bool Within(double[] p, double[] q, double[] r)
        {
            return Math.Min(p[0], r[0]) <= q[0] && q[0] <= Math.Max(p[0], r[0]) && Math.Min(p[1], r[1]) <= q[1] && q[1] <= Math.Max(p[1], r[1]);
        }

        private static bool Crossing(double d1, double d2, double d3, double d4)
        {
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        // The closed segments p1 p2 and q1 q2 share a point
        private static bool Meet(double[] p1, double[] p2, double[] q1, double[] q2)
        {
            double d1 = Orient(q1, q2, p1), d2 = Orient(q1, q2, p2), d3 = Orient(p1, p2, q1), d4 = Orient(p1, p2, q2);
            if (Crossing(d1, d2, d3, d4))
            {
                return true;
            }
            return (d1 == 0 && Within(q1, p1, q2)) || (d2 == 0 && Within(q1, p2, q2)) || (d3 == 0 && Within(p1, q1, p2)) || (d4 == 0 && Within(p1, q2, p2));
        }

        // Two neighbour edges that go back along one line from their shared vertex
        private static bool Folds(double[] before, double[] at, double[] after)
        {
            return Orient(before, at, after) == 0 && (before[0] - at[0]) * (after[0] - at[0]) + (before[1] - at[1]) * (after[1] - at[1]) > 0;
        }

        private static List<double[]> Relative(IReadOnlyList<double[]> ring, double[] origin)
        {
            return ring.Select(vertex => new[] { vertex[0] - origin[0], vertex[1] - origin[1] }).ToList();
        }

        // A cleaned ring of three vertices or more is simple when no two of its edges meet but neighbours at their shared vertex and no
        // two neighbours fold back along one line. Taken about its first vertex, as the area is
        public static bool IsSimple(IReadOnlyList<double[]> ring)
        {
            List<double[]> points = Relative(ring, ring[0]);
            int count = points.Count;
            for (int one = 0; one < count; one++)
            {
                double[] a = points[one], b = points[(one + 1) % count];
                for (int other = one + 1; other < count; other++)
                {
                    double[] c = points[other], d = points[(other + 1) % count];
                    if (other == one + 1)
                    {
                        if (Folds(a, b, d))
                        {
                            return false;
                        }
                    }
                    else if (one == 0 && other == count - 1)
                    {
                        if (Folds(c, a, b))
                        {
                            return false;
                        }
                    }
                    else if (Meet(a, b, c, d))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // The x where two segments cross at a point inside both, null when they do not
        private static double? CrossingX(double[] p1, double[] p2, double[] q1, double[] q2)
        {
            double d1 = Orient(q1, q2, p1), d2 = Orient(q1, q2, p2), d3 = Orient(p1, p2, q1), d4 = Orient(p1, p2, q2);
            if (Crossing(d1, d2, d3, d4))
            {
                return p1[0] + d1 / (d1 - d2) * (p2[0] - p1[0]);
            }
            return null;
        }

        // The area the union of cleaned simple rings covers, by vertical slabs about the first vertex of the first ring: between two
        // neighbouring x of the vertices and of the crossings of edges of two rings no edge begins, ends or crosses another, so the
        // length the union covers across a slab is linear in x and its value at the middle times the width is the area of the slab
        public static double UnionArea(IReadOnlyList<IReadOnlyList<double[]>> rings)
        {
            double[] origin = rings[0][0];
            var edges = new List<List<double[][]>>();
            foreach (IReadOnlyList<double[]> ring in rings)
            {
                List<double[]> points = Relative(ring, origin);
                edges.Add(Enumerable.Range(0, points.Count).Select(index => new[] { points[index], points[(index + 1) % points.Count] }).ToList());
            }
            var xs = new HashSet<double>(edges.SelectMany(ringEdges => ringEdges.Select(edge => edge[0][0])));
            for (int one = 0; one < edges.Count; one++)
            {
                for (int other = one + 1; other < edges.Count; other++)
                {
                    foreach (double[][] p in edges[one])
                    {
                        foreach (double[][] q in edges[other])
                        {
                            double? x = CrossingX(p[0], p[1], q[0], q[1]);
                            if (x.HasValue)
                            {
                                xs.Add(x.Value);
                            }
                        }
                    }
                }
            }
            List<double> sorted = xs.OrderBy(x => x).ToList();
            double area = 0;
            for (int index = 0; index + 1 < sorted.Count; index++)
            {
                double left = sorted[index], right = sorted[index + 1], middle = (left + right) / 2;
                var spans = new List<double[]>();
                foreach (List<double[][]> ringEdges in edges)
                {
                    List<double> ys = ringEdges.Where(edge => Math.Min(edge[0][0], edge[1][0]) < middle && middle < Math.Max(edge[0][0], edge[1][0]))
                        .Select(edge => edge[0][1] + (edge[1][1] - edge[0][1]) * (middle - edge[0][0]) / (edge[1][0] - edge[0][0])).OrderBy(y => y).ToList();
                    for (int pair = 0; pair + 1 < ys.Count; pair += 2)
                    {
                        spans.Add(new[] { ys[pair], ys[pair + 1] });
                    }
                }
                double covered = 0;
                double? end = null;
                foreach (double[] span in spans.OrderBy(item => item[0]).ThenBy(item => item[1]))
                {
                    if (!end.HasValue || span[0] > end.Value)
                    {
                        covered += span[1] - span[0];
                        end = span[1];
                    }
                    else if (span[1] > end.Value)
                    {
                        covered += span[1] - end.Value;
                        end = span[1];
                    }
                }
                area += (right - left) * covered;
            }
            return area;
        }

        // Why the build refuses the faces of the plot_shape entries of plots.json, one line per label, none when it takes every one,
        // the same lines as python/face_rings.py. The entries the build takes: a whole number label and three vertices or more. A
        // ring of under three distinct vertices has no area and is left as both sides leave it. A label whose union falls short of the
        // sum of its ring areas by more than overlapM2, the areas tolerance of addin.parity, has rings that overlap
        public static List<string> Refusals(JArray plots, double overlapM2)
        {
            var found = new List<string>();
            // Whole labels with no leading zero, in the order of their numbers: by length, then by their digits
            var rings = new SortedDictionary<string, List<List<double[]>>>(Comparer<string>.Create((one, other) => one.Length != other.Length ? one.Length.CompareTo(other.Length) : string.CompareOrdinal(one, other)));
            foreach (JToken entry in plots)
            {
                if ((string)entry["kind"] != "plot_shape")
                {
                    continue;
                }
                JToken plot = entry["plot"];
                string label = FaceStep.WholeLabel(plot == null || plot.Type == JTokenType.Null ? null : plot.ToString());
                JArray vertices = entry["vertices"] as JArray ?? new JArray();
                if (label == null || vertices.Count < 3)
                {
                    continue;
                }
                List<double[]> ring = Clean(vertices.Select(vertex => new[] { (double)vertex[0], (double)vertex[1] }));
                if (ring.Count < 3)
                {
                    continue;
                }
                if (!IsSimple(ring))
                {
                    found.Add("the baseplan face of label " + label + " is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# would not, so the build refuses it (R345)");
                    continue;
                }
                if (!rings.ContainsKey(label))
                {
                    rings[label] = new List<List<double[]>>();
                }
                rings[label].Add(ring);
            }
            foreach (KeyValuePair<string, List<List<double[]>>> label in rings.Where(item => item.Value.Count > 1))
            {
                double shortfall = label.Value.Sum(ring => FaceStep.Ring(ring).Area) - UnionArea(label.Value);
                if (shortfall > overlapM2)
                {
                    found.Add("the baseplan faces of label " + label.Key + " overlap by " + shortfall.ToString("0.000", CultureInfo.InvariantCulture)
                        + " m2, shapely joins them by a union and the C# would sum them, so the build refuses them (R345)");
                }
            }
            return found;
        }
    }
}
