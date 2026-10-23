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
