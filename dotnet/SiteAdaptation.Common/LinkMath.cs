using System;

namespace SiteAdaptation.Common
{
    public static class LinkMath
    {
        // Origin is in metres. The axes are the unit vectors of the link transform
        public static LinkTransform Describe(double[] origin, double[] axisX, double[] axisY, double[] axisZ)
        {
            return new LinkTransform
            {
                Origin = origin,
                RotationDeg = PlanAngleDeg(axisX),
                Mirrored = Determinant(axisX, axisY, axisZ) < 0,
                AxisX = axisX,
                AxisY = axisY,
                AxisZ = axisZ,
            };
        }

        // A negative value means the three axes form a mirror image
        public static double Determinant(double[] x, double[] y, double[] z)
        {
            return x[0] * (y[1] * z[2] - y[2] * z[1])
                 - x[1] * (y[0] * z[2] - y[2] * z[0])
                 + x[2] * (y[0] * z[1] - y[1] * z[0]);
        }

        // Counter clockwise angle of the X axis in plan, from 0 up to but not including 360
        public static double PlanAngleDeg(double[] axisX)
        {
            double degrees = Rounding.Fixed(UnitMath.RadiansToDegrees(Math.Atan2(axisX[1], axisX[0])), 6);
            if (degrees < 0)
            {
                degrees += 360;
            }
            return degrees >= 360 ? degrees - 360 : degrees;
        }
    }
}
