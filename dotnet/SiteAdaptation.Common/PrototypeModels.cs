using System.Collections.Generic;
using Newtonsoft.Json;

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
        public List<LinkedFileEntry> LinkedFiles { get; set; }
        public List<WallRow> Walls { get; set; }
        public List<FloorRow> Floors { get; set; }
        public List<RampRow> Ramps { get; set; }
        public List<SpotRow> SpotElevations { get; set; }
        public List<string> Errors { get; set; }
    }

    // One Revit link instance. The name and parameters live on the instance, so they are read even when the link is not loaded.
    // The five named fields and nameParts are always written, as null when the source holds nothing, so a gap shows
    public class LinkEntry
    {
        public string File { get; set; }
        public LinkTransform Transform { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public object PlotNumber { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public object Prototype { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public object Ffl { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public object Steps { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public object Case { get; set; }

        public NameParts NameParts { get; set; }

        // The Name exactly as Revit gave it, with the file name in front and the location suffix still on
        public string RawName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        // The file name to look up in linkedFiles, present when that file has contents there
        public string ContentsRef { get; set; }
        public List<string> Errors { get; set; }
    }

    // The Name of a link instance split on " | ". A part the name does not hold is null
    public class NameParts
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string Plot { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string Prototype { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string Steps { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string FacadeColor { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string DesignOption { get; set; }
    }

    // What one linked file holds, written once however many links point at it.
    // Instance points are in the linked model's own coordinates, the transform of a link takes them to the host.
    // Links inside the file are entries like any other and their transform is relative to this file
    public class LinkedFileEntry
    {
        public string File { get; set; }
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

        // Written only when the wall holds a Plot parameter. A value that does not parse is null and has a line in errors
        public string PlotNumber { get; set; }
        public string Unit { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    // SketchBoundary is the outer loop of the floor sketch as vertices in order, closed by going from the last back to the first.
    // Its z is the height of the sketch plane and not an elevation of the floor, so read the levels from Corners.
    // A curve that is not a line is cut into straight pieces for the vertices, and every arc is also listed in Arcs
    public class FloorRow
    {
        public string TypeName { get; set; }
        public long? ElementId { get; set; }
        public string Level { get; set; }
        public double? HeightOffset { get; set; }
        public double? TopElevation { get; set; }
        public double? Thickness { get; set; }
        public double? Area { get; set; }
        public List<double[]> SketchBoundary { get; set; }
        public List<FloorArc> Arcs { get; set; }

        // One [x, y, z] per corner in the same coordinates as every other point. CornersSource says where z came from
        public List<double[]> Corners { get; set; }
        public string CornersSource { get; set; }

        // Shape editing points that are not corners, written only when the floor has some
        public List<double[]> ShapePoints { get; set; }
        public SlopeInfo Slope { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string PlotNumber { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string Unit { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        // What the source saw, so the reader can choose the corners. Not written.
        // BoundaryEnds is the start of every edge of the outer loop and holds no points cut out of a spline
        [JsonIgnore]
        public ShapeEditing Shape { get; set; }

        [JsonIgnore]
        public List<double[]> BoundaryEnds { get; set; }
    }

    // What the floor reports about a slope. Parameter is the Slope parameter as Revit holds it, null when the floor has none.
    // Arrow says whether a slope arrow is in the sketch and is null when it could not be read
    public class SlopeInfo
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public object Parameter { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public bool? Arrow { get; set; }
    }

    // Slab shape editing of one floor. Corners and Others are [x, y, z] vertices, Others being edge and interior points
    public class ShapeEditing
    {
        public bool Enabled { get; set; }
        public List<double[]> Corners { get; set; }
        public List<double[]> Others { get; set; }
    }

    // One spot elevation annotation. Origin is the point it measures, in the same coordinates as every other point.
    // Value is the elevation the annotation shows. ElementId is what it points to when the API gives one
    public class SpotRow
    {
        public long? ElementId { get; set; }
        public double[] Origin { get; set; }
        public double? Value { get; set; }
        public string View { get; set; }
        public long? Target { get; set; }
    }

    // Edge i runs from boundary vertex i to vertex i + 1. Ccw is the sweep from start to end seen from above
    public class FloorArc
    {
        public int Edge { get; set; }
        public double[] Center { get; set; }
        public double Radius { get; set; }
        public bool Ccw { get; set; }
    }

    // One piece of a boundary as the source sees it. Radius is set for an arc.
    // Via holds the points between start and end for a curve that is neither a line nor an arc
    public class BoundaryEdge
    {
        public double[] Start { get; set; }
        public double[] End { get; set; }
        public double[] Center { get; set; }
        public double? Radius { get; set; }
        public bool Ccw { get; set; }
        public List<double[]> Via { get; set; }
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
