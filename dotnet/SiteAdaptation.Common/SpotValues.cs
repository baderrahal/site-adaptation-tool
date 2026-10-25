namespace SiteAdaptation.Common
{
    // R320. What the reader takes from one spot elevation for the value it shows, as the Revit API gives it: the Single/Upper Value
    // and the Lower Value in feet, the type name, the Elevation Base of the type and the Relative Base and Display Elevations of the
    // instance as Revit shows them. RevitModelSource fills it and only reads, SpotValues.Apply converts
    public class RawSpotValue
    {
        public double? UpperFeet { get; set; }
        public double? LowerFeet { get; set; }
        public string TypeName { get; set; }
        public string ElevationBase { get; set; }
        public string RelativeBase { get; set; }
        public string DisplayElevations { get; set; }
    }

    public static class SpotValues
    {
        // R320. value is the Single/Upper Value in metres and lowerValue the Lower Value, each null when it does not read. Neither
        // stands in for the other, so a spot that shows one value never gets a second one it does not show
        public static void Apply(SpotRow row, RawSpotValue raw)
        {
            row.Value = Metres(raw.UpperFeet);
            row.LowerValue = Metres(raw.LowerFeet);
            row.TypeName = raw.TypeName;
            row.ElevationBase = raw.ElevationBase;
            row.RelativeBase = raw.RelativeBase;
            row.DisplayElevations = raw.DisplayElevations;
        }

        private static double? Metres(double? feet)
        {
            return feet.HasValue ? UnitMath.FeetToMetres(feet.Value) : (double?)null;
        }
    }
}
