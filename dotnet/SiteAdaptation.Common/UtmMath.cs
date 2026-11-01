using System;
using System.IO;

namespace SiteAdaptation.Common
{
    // R455. A UTM zone of WGS 84 by its EPSG code, 326zz north and 327zz south, the numbering of the EPSG registry
    public sealed class UtmZone
    {
        public int Number { get; set; }
        public bool North { get; set; }

        public double CentralMeridianDeg
        {
            get { return Number * 6 - 183; }
        }
    }

    // R455. UTM easting and northing on WGS 84 to latitude and longitude in degrees, the inverse Krueger series to n cubed, which holds
    // to about a mm within 3000 km of the central meridian. The ellipsoid and the UTM constants are of the WGS 84 and UTM definitions,
    // not of a project
    public static class UtmMath
    {
        private const double SemiMajorM = 6378137.0;
        private const double Flattening = 1 / 298.257223563;
        private const double ScaleOnMeridian = 0.9996;
        private const double FalseEastingM = 500000.0;
        private const double FalseNorthingSouthM = 10000000.0;
        private const int NorthFirst = 32601;
        private const int SouthFirst = 32701;
        private const int Zones = 60;

        public static UtmZone ZoneOf(int epsg)
        {
            if (epsg >= NorthFirst && epsg < NorthFirst + Zones)
            {
                return new UtmZone { Number = epsg - NorthFirst + 1, North = true };
            }
            if (epsg >= SouthFirst && epsg < SouthFirst + Zones)
            {
                return new UtmZone { Number = epsg - SouthFirst + 1, North = false };
            }
            throw new InvalidDataException("EPSG " + epsg + " is no UTM zone of WGS 84, 32601 to 32660 or 32701 to 32760 (R455)");
        }

        // Latitude and longitude in degrees of a point in metres of the zone
        public static double[] ToLatLon(double eastM, double northM, UtmZone zone)
        {
            double n = Flattening / (2 - Flattening);
            double rectifying = SemiMajorM / (1 + n) * (1 + n * n / 4 + n * n * n * n / 64);
            double[] beta = { n / 2 - 2 * n * n / 3 + 37 * n * n * n / 96, n * n / 48 + n * n * n / 15, 17 * n * n * n / 480 };
            double[] delta = { 2 * n - 2 * n * n / 3 - 2 * n * n * n, 7 * n * n / 3 - 8 * n * n * n / 5, 56 * n * n * n / 15 };
            double xi = (northM - (zone.North ? 0.0 : FalseNorthingSouthM)) / (ScaleOnMeridian * rectifying);
            double eta = (eastM - FalseEastingM) / (ScaleOnMeridian * rectifying);
            double xiPrime = xi, etaPrime = eta;
            for (int j = 1; j <= 3; j++)
            {
                xiPrime -= beta[j - 1] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta);
                etaPrime -= beta[j - 1] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta);
            }
            double chi = Math.Asin(Math.Sin(xiPrime) / Math.Cosh(etaPrime));
            double latitude = chi;
            for (int j = 1; j <= 3; j++)
            {
                latitude += delta[j - 1] * Math.Sin(2 * j * chi);
            }
            double longitude = zone.CentralMeridianDeg * Math.PI / 180 + Math.Atan2(Math.Sinh(etaPrime), Math.Cos(xiPrime));
            return new[] { latitude * 180 / Math.PI, longitude * 180 / Math.PI };
        }
    }
}
