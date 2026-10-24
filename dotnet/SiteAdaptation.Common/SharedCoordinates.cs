using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    // Shared coordinates are the internal ones plus the survey point offset on each axis, which is its shared position minus its position.
    // The rotation to true north is not applied
    public static class SharedCoordinates
    {
        public static double[] Offset(double[] position, double[] sharedPosition)
        {
            return new[] { sharedPosition[0] - position[0], sharedPosition[1] - position[1], sharedPosition[2] - position[2] };
        }

        // A point in feet as Revit gives it to a point in metres in shared coordinates
        public static double[] ToShared(double[] feet, double[] offsetMetres)
        {
            return new[]
            {
                UnitMath.FeetToMetres(feet[0]) + offsetMetres[0],
                UnitMath.FeetToMetres(feet[1]) + offsetMetres[1],
                UnitMath.FeetToMetres(feet[2]) + offsetMetres[2],
            };
        }

        public static double[] Shift(double[] metres, double[] offsetMetres)
        {
            return new[] { metres[0] + offsetMetres[0], metres[1] + offsetMetres[1], metres[2] + offsetMetres[2] };
        }
    }

    public static class BoxMath
    {
        // Bottom four corners going round, then the top four in the same order
        public static List<double[]> Corners(double[] min, double[] max)
        {
            return new List<double[]>
            {
                new[] { min[0], min[1], min[2] },
                new[] { max[0], min[1], min[2] },
                new[] { max[0], max[1], min[2] },
                new[] { min[0], max[1], min[2] },
                new[] { min[0], min[1], max[2] },
                new[] { max[0], min[1], max[2] },
                new[] { max[0], max[1], max[2] },
                new[] { min[0], max[1], max[2] },
            };
        }

        // The box around every box given as its min and max corner, or null when there is none
        public static double[][] Union(IEnumerable<double[][]> boxes)
        {
            double[] min = null;
            double[] max = null;
            foreach (double[][] box in boxes)
            {
                if (min == null)
                {
                    min = (double[])box[0].Clone();
                    max = (double[])box[1].Clone();
                    continue;
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    min[axis] = Math.Min(min[axis], box[0][axis]);
                    max[axis] = Math.Max(max[axis], box[1][axis]);
                }
            }
            return min == null ? null : new[] { min, max };
        }

        // The 8 corners of a box put through a transform, origin plus each axis times its coordinate, and the box around them.
        // A turned box grows to hold its corners, so the result can be larger than the turned box itself
        public static double[][] Through(double[] min, double[] max, double[] origin, double[] axisX, double[] axisY, double[] axisZ)
        {
            var moved = new List<double[][]>();
            foreach (double[] corner in Corners(min, max))
            {
                var point = new double[3];
                for (int axis = 0; axis < 3; axis++)
                {
                    point[axis] = origin[axis] + axisX[axis] * corner[0] + axisY[axis] * corner[1] + axisZ[axis] * corner[2];
                }
                moved.Add(new[] { point, point });
            }
            return Union(moved);
        }
    }

    // Lowest and highest z of a set of points
    public class ElevationRange
    {
        public double Min { get; set; }
        public double Max { get; set; }

        public static ElevationRange Of(IEnumerable<double[]> points)
        {
            double[] zs = points.Where(point => point != null && point.Length > 2).Select(point => point[2]).ToArray();
            return zs.Length == 0 ? null : new ElevationRange { Min = zs.Min(), Max = zs.Max() };
        }
    }
}
