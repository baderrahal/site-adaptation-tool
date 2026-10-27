using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354. The baseplan face of a plot as plan_from_book.py takes it (yard_rule.face_shapes): the plot shape of a whole label of three
    // vertices or more with an area, as shapely gives Polygon(vertices).buffer(0). On the 2,962 faces of the baseplan buffer(0) gave every
    // ring as its first vertex and then the others in reverse, each drawn anticlockwise, and keeps a clockwise ring as it is, drops a
    // repeated vertex and the closing one and keeps a collinear one (round_addin logs p4e_buffer_order.txt), so the C# takes the ring by
    // that rule. A build walks the edges of the ring in that order (R355, R357), so a label of two rings, which face_shapes joins by a
    // union whose ring order only shapely knows, is refused here and in Python alike
    public sealed class BuildFace
    {
        public string Label { get; private set; }

        // The ring as shapely gives it, the closing vertex left out
        public double[][] Ring { get; private set; }

        public double Area { get; private set; }

        // R357. The centroid as GEOS gives it, Centroid::addShell: the triangles from the first vertex of the ring, each its area times the
        // sum of its three corners, summed and divided by 3 and then by the area, the same bit for bit as shapely on the 2,962 faces of the
        // baseplan, where a side neighbour of 1076 lies 2.3E-10 m along the front of 1129 (round_addin logs p4e_margins_hands.txt)
        public double CentroidX { get; private set; }

        public double CentroidY { get; private set; }

        public double[] Low { get; private set; }

        public double[] High { get; private set; }

        // The face of a ring, null for a ring of under three vertices or no area, which buffer(0) gives as empty
        public static BuildFace Of(string label, IEnumerable<double[]> vertices)
        {
            List<double[]> ring = FaceRings.Clean(vertices);
            if (ring.Count < 3)
            {
                return null;
            }
            double twice = 0;
            for (int index = 0; index < ring.Count; index++)
            {
                double[] a = ring[index], b = ring[(index + 1) % ring.Count];
                twice += (a[0] - ring[0][0]) * (b[1] - ring[0][1]) - (b[0] - ring[0][0]) * (a[1] - ring[0][1]);
            }
            if (twice == 0)
            {
                return null;
            }
            double[][] ordered = twice > 0 ? new[] { ring[0] }.Concat(Enumerable.Range(1, ring.Count - 1).Select(index => ring[ring.Count - index])).ToArray() : ring.ToArray();
            double[] origin = ordered[0];
            double sumX = 0, sumY = 0, sumArea = 0;
            for (int index = 0; index < ordered.Length; index++)
            {
                double[] p1 = ordered[index], p2 = ordered[(index + 1) % ordered.Length];
                double x3 = origin[0] + p1[0] + p2[0], y3 = origin[1] + p1[1] + p2[1];
                double area2 = (p1[0] - origin[0]) * (p2[1] - origin[1]) - (p2[0] - origin[0]) * (p1[1] - origin[1]);
                sumX += area2 * x3;
                sumY += area2 * y3;
                sumArea += area2;
            }
            return new BuildFace
            {
                Label = label, Ring = ordered, Area = FaceStep.Ring(ordered).Area, CentroidX = sumX / 3 / sumArea, CentroidY = sumY / 3 / sumArea,
                Low = new[] { ordered.Min(point => point[0]), ordered.Min(point => point[1]) }, High = new[] { ordered.Max(point => point[0]), ordered.Max(point => point[1]) },
            };
        }

        private static IEnumerable<KeyValuePair<string, double[][]>> Shapes(JArray plots)
        {
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
                yield return new KeyValuePair<string, double[][]>(label, vertices.Select(vertex => new[] { Number(vertex[0]), Number(vertex[1]) }).ToArray());
            }
        }

        // A coordinate as Python takes a JSON number, an integer as the nearest double
        public static double Number(JToken token)
        {
            if (token.Type == JTokenType.Integer)
            {
                object value = ((JValue)token).Value;
                return value is System.Numerics.BigInteger large ? (double)large : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            return (double)token;
        }

        // Refused when a whole label holds two rings or more that the build would take, with the labels in the order of their numbers
        public static void OneRing(JArray plots)
        {
            var count = new Dictionary<string, int>();
            foreach (KeyValuePair<string, double[][]> shape in Shapes(plots))
            {
                if (Of(shape.Key, shape.Value) != null)
                {
                    count[shape.Key] = count.TryGetValue(shape.Key, out int held) ? held + 1 : 1;
                }
            }
            List<string> many = count.Where(item => item.Value > 1).Select(item => item.Key).OrderBy(label => label, PyMath.SortKey).ToList();
            if (many.Count > 0)
            {
                throw new InvalidDataException(many.Count + " baseplan labels hold two rings or more: " + string.Join(", ", many)
                    + ", a build takes a face of one ring, whose edges it walks in the order shapely gives them, so plan_from_book.py and the C# refuse such a label (R354)");
            }
        }

        // The face of each plot number, the label numbered by the rows of plots.csv (FaceStep.NumberOfLabel), a later label of one number
        // over an earlier one, as plan_from_book.py takes the faces. The faces a build refuses are refused first: a ring that is not simple and
        // rings of one label that overlap (R345), then a label of two rings
        public static Dictionary<string, BuildFace> Faces(JArray plots, IReadOnlyDictionary<string, string> numberOf, double overlapM2)
        {
            List<string> refused = FaceRings.Refusals(plots, overlapM2);
            if (refused.Count > 0)
            {
                throw new InvalidDataException(refused.Count + " baseplan faces refused: " + string.Join(", ", refused));
            }
            OneRing(plots);
            var byLabel = new Dictionary<string, BuildFace>();
            foreach (KeyValuePair<string, double[][]> shape in Shapes(plots))
            {
                BuildFace face = Of(shape.Key, shape.Value);
                if (face != null)
                {
                    byLabel[shape.Key] = face;
                }
            }
            var found = new Dictionary<string, BuildFace>();
            foreach (KeyValuePair<string, BuildFace> face in byLabel)
            {
                string number;
                if (numberOf.TryGetValue(face.Key, out number))
                {
                    found[number] = face.Value;
                }
            }
            return found;
        }
    }
}
