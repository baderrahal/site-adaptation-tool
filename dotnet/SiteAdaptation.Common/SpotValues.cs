namespace SiteAdaptation.Common
{
    // R320. What the reader takes from one spot elevation for the value it shows, as the Revit API gives it: the Single/Upper Value
    // and the Lower Value in feet, the type name, the Elevation Base of the type and the Relative Base and Display Elevations of the
    // instance as Revit shows them, and why the value or the base did not read. RevitModelSource fills it and only reads,
    // SpotValues.Apply converts
    public class RawSpotValue
    {
        public double? UpperFeet { get; set; }
        public double? LowerFeet { get; set; }
        public string TypeName { get; set; }
        public string ElevationBase { get; set; }
        public string RelativeBase { get; set; }
        public string DisplayElevations { get; set; }

        // Why the Single/Upper Value or the Elevation Base did not read, one of the reasons of SpotValues or the error Revit gave
        public string UpperMissing { get; set; }
        public string BaseMissing { get; set; }
    }

    public static class SpotValues
    {
        // The reasons a parameter does not read, so the same reason on a thousand spots is one error line
        public const string NoParameter = "the element has no such parameter";
        public const string NoValue = "the parameter holds no value";
        public const string NotANumber = "the parameter holds no number";

        // R320. value is the Single/Upper Value in metres and lowerValue the Lower Value, each null when it does not read. Neither
        // stands in for the other, so a spot that shows one value never gets a second one it does not show. A value or base that
        // does not read keeps its reason on the row for the one error line per reason of ModelReader. A Lower Value that does not
        // read has no reason, a spot of one value has none
        public static void Apply(SpotRow row, RawSpotValue raw)
        {
            row.Value = Metres(raw.UpperFeet);
            row.LowerValue = Metres(raw.LowerFeet);
            row.TypeName = raw.TypeName;
            row.ElevationBase = raw.ElevationBase;
            row.RelativeBase = raw.RelativeBase;
            row.DisplayElevations = raw.DisplayElevations;
            row.ValueFailure = raw.UpperFeet.HasValue ? null : "no Single/Upper Value, " + (raw.UpperMissing ?? NoValue);
            row.BaseFailure = string.IsNullOrWhiteSpace(raw.ElevationBase) ? "no Elevation Base, " + (raw.BaseMissing ?? NoValue) : null;
        }

        private static double? Metres(double? feet)
        {
            return feet.HasValue ? UnitMath.FeetToMetres(feet.Value) : (double?)null;
        }
    }
}
