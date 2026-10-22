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
    }
}
