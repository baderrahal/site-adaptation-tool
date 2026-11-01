using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R451. The plan outline of one exterior wall from its location line, its width and the side its exterior faces, so the doors and
    // windows that cut its solid never open the ring (a horizontal cut of the solids crossed them). The Revit side unions the outlines
    // and takes the outer loop, WallRing.Choose decides. Metres in the internal plan of the prototype
    public static class WallStrips
    {
        // The values of the Revit parameter Location Line, WallLocationLine
        public const int WallCenterline = 0;
        public const int CoreCenterline = 1;
        public const int FinishFaceExterior = 2;
        public const int FinishFaceInterior = 3;
        public const int CoreExterior = 4;
        public const int CoreInterior = 5;

        // How far the location line lies from the middle of the wall toward its exterior face. layers are the widths of the compound
        // structure from the exterior side in, firstCore and lastCore its core, under 0 for a wall with none, whose core lines are
        // taken at the middle
        public static double LocationOffset(int locationLine, double width, IReadOnlyList<double> layers, int firstCore, int lastCore)
        {
            double half = width / 2;
            bool core = layers != null && firstCore >= 0 && lastCore >= firstCore && lastCore < layers.Count;
            double coreExterior = core ? half - layers.Take(firstCore).Sum() : 0;
            double coreInterior = core ? -half + layers.Skip(lastCore + 1).Sum() : 0;
            switch (locationLine)
            {
                case FinishFaceExterior:
                    return half;
                case FinishFaceInterior:
                    return -half;
                case CoreExterior:
                    return coreExterior;
                case CoreInterior:
                    return coreInterior;
                case CoreCenterline:
                    return (coreExterior + coreInterior) / 2;
                default:
                    return 0;
            }
        }

        // One counter clockwise ring: the path offset to the exterior face and to the interior face, mitred at each inner corner of the
        // path, a tessellated arc, and carried on by half the width at both ends so the corners of two walls that meet fill. exterior
        // is a plan vector to the exterior side, Wall.Orientation, which picks the side of the whole path. Null under two points
        public static double[][] Outline(IReadOnlyList<double[]> path, double width, double locationOffset, double[] exterior)
        {
            List<double[]> points = FaceRings.Clean(path.Where(point => point != null && point.Length >= 2).Select(point => new[] { point[0], point[1] }));
            if (points.Count < 2 || !(width > 0))
            {
                return null;
            }
            int segments = points.Count - 1;
            var directions = new double[segments][];
            var normals = new double[segments][];
            for (int i = 0; i < segments; i++)
            {
                double dx = points[i + 1][0] - points[i][0], dy = points[i + 1][1] - points[i][1], length = Math.Sqrt(dx * dx + dy * dy);
                directions[i] = new[] { dx / length, dy / length };
                normals[i] = new[] { -dy / length, dx / length };
            }
            double[] middle = normals[segments / 2];
            double side = middle[0] * exterior[0] + middle[1] * exterior[1] < 0 ? -1 : 1;
            double outer = width / 2 - locationOffset;
            List<double[]> face = Offset(points, directions, normals, side * outer, width / 2);
            List<double[]> back = Offset(points, directions, normals, side * (outer - width), width / 2);
            back.Reverse();
            List<double[]> ring = face.Concat(back).ToList();
            if (RingMath.Area(ring) < 0)
            {
                ring.Reverse();
            }
            return ring.ToArray();
        }

        // The path moved by distance along its left normal, the ends carried on by extend, each inner corner where the two moved
        // segments meet. A corner that turns back on itself has no meeting point, there the moved corner of the next segment is taken
        private static List<double[]> Offset(List<double[]> points, double[][] directions, double[][] normals, double distance, double extend)
        {
            int last = points.Count - 1;
            var moved = new List<double[]>();
            for (int i = 0; i <= last; i++)
            {
                if (i == 0 || i == last)
                {
                    int segment = i == 0 ? 0 : last - 1;
                    double along = i == 0 ? -extend : extend;
                    moved.Add(new[] { points[i][0] + directions[segment][0] * along + normals[segment][0] * distance, points[i][1] + directions[segment][1] * along + normals[segment][1] * distance });
                    continue;
                }
                double[] before = normals[i - 1], after = normals[i];
                double mx = before[0] + after[0], my = before[1] + after[1], length = Math.Sqrt(mx * mx + my * my);
                double cos = length == 0 ? 0 : (mx * after[0] + my * after[1]) / length;
                if (cos < 0.1)
                {
                    moved.Add(new[] { points[i][0] + after[0] * distance, points[i][1] + after[1] * distance });
                    continue;
                }
                moved.Add(new[] { points[i][0] + mx / length * distance / cos, points[i][1] + my / length * distance / cos });
            }
            return moved;
        }
    }
}
