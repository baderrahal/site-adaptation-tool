using System.Collections.Generic;

namespace SiteAdaptation.Common
{
    // Points are [x, y, z] in metres. Angles are degrees.
    public class PrototypesFile
    {
        public string Tool { get; set; }
        public string RevitVersion { get; set; }
        public string LengthUnit { get; set; }
        public string AngleUnit { get; set; }
        public List<DocumentEntry> Documents { get; set; }
    }

    public class DocumentEntry
    {
        public string File { get; set; }
        public string Kind { get; set; }
        public string Error { get; set; }
        public ProjectPointInfo SurveyPoint { get; set; }
        public ProjectPointInfo BasePoint { get; set; }
        public double? AngleToTrueNorthDeg { get; set; }
        public List<PrototypeRow> Prototypes { get; set; }
        public List<LinkEntry> Links { get; set; }
        public List<WallRow> Walls { get; set; }
        public List<RampRow> Ramps { get; set; }
        public List<string> Errors { get; set; }
    }

    // One Revit link. Instance points are in the linked model's own coordinates, the transform takes them to the host
    public class LinkEntry
    {
        public string File { get; set; }
        public LinkTransform Transform { get; set; }
        public List<PrototypeRow> Instances { get; set; }
        public List<LinkEntry> Links { get; set; }
        public int? DeeperLinks { get; set; }
        public List<string> Errors { get; set; }
    }

    public class LinkTransform
    {
        public double[] Origin { get; set; }
        public double RotationDeg { get; set; }
        public bool Mirrored { get; set; }
        public double[] AxisX { get; set; }
        public double[] AxisY { get; set; }
        public double[] AxisZ { get; set; }
    }

    // Kind is line, arc, point or the Revit curve class name. Arcs add Center and Radius
    public class LocationInfo
    {
        public string Kind { get; set; }
        public double[] Start { get; set; }
        public double[] End { get; set; }
        public double[] Center { get; set; }
        public double? Radius { get; set; }
        public double[] Point { get; set; }
    }

    public class WallRow
    {
        public string TypeName { get; set; }
        public long? ElementId { get; set; }
        public LocationInfo Location { get; set; }
        public double? Length { get; set; }
        public double? Height { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class RampRow
    {
        public string TypeName { get; set; }
        public long? ElementId { get; set; }
        public LocationInfo Location { get; set; }
        public BoxInfo BoundingBox { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class ProjectPointInfo
    {
        public double[] Position { get; set; }
        public double[] SharedPosition { get; set; }
    }

    public class BoxInfo
    {
        public double[] Min { get; set; }
        public double[] Max { get; set; }
    }

    public class PrototypeRow
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string Placement { get; set; }
        public long? ElementId { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public BoxInfo BoundingBox { get; set; }
        public double[] InsertionPoint { get; set; }
        public double? RotationDeg { get; set; }
        public string Level { get; set; }
        public bool? Mirrored { get; set; }
    }
}
