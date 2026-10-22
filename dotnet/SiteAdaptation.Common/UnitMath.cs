using System;

namespace SiteAdaptation.Common
{
    public static class UnitMath
    {
        public const double FeetPerMetre = 3.280839895013123;

        public static double FeetToMetres(double feet)
        {
            return feet / FeetPerMetre;
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
    }
}
