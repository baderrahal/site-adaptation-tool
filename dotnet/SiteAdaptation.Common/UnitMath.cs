using System;

namespace SiteAdaptation.Common
{
    // What a Revit number is measured in. Revit keeps lengths in feet, areas in square feet and angles in radians
    public enum ValueUnit
    {
        None,
        Length,
        Area,
        Volume,
        Angle,
    }

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

        public static double MetresToFeet(double metres)
        {
            return metres * FeetPerMetre;
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        // Metres, square metres, cubic metres or degrees. A number with no unit stays as Revit holds it
        public static double FromInternal(double value, ValueUnit unit)
        {
            switch (unit)
            {
                case ValueUnit.Length:
                    return FeetToMetres(value);
                case ValueUnit.Area:
                    return value / (FeetPerMetre * FeetPerMetre);
                case ValueUnit.Volume:
                    return value / (FeetPerMetre * FeetPerMetre * FeetPerMetre);
                case ValueUnit.Angle:
                    return RadiansToDegrees(value);
                default:
                    return value;
            }
        }

        public static string UnitName(ValueUnit unit)
        {
            switch (unit)
            {
                case ValueUnit.Length:
                    return "m";
                case ValueUnit.Area:
                    return "m2";
                case ValueUnit.Volume:
                    return "m3";
                case ValueUnit.Angle:
                    return "deg";
                default:
                    return null;
            }
        }
    }
}
