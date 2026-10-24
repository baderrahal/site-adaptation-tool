using System;

namespace SiteAdaptation.Common
{
    // R220. The rotation the reader writes for a placed instance. Revit cannot give the location rotation of some instances,
    // a past site model holds such ones, and then the turn of the X axis of the instance transform stands in
    public static class PlacementRotation
    {
        public static bool NeedsTransform(double? locationRadians)
        {
            return !locationRadians.HasValue;
        }

        // Degrees from the location rotation when there is one, else from the transform X axis, anticlockwise in plan
        public static double Degrees(double? locationRadians, double axisXx, double axisXy)
        {
            return UnitMath.RadiansToDegrees(locationRadians ?? Math.Atan2(axisXy, axisXx));
        }
    }
}
