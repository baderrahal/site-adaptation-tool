using System.Collections.Generic;

namespace SiteAdaptation.Common
{
    // placer_report.json as R87 lists it
    public class PlacerReport
    {
        public string Tool { get; set; } = "SiteAdaptation.Placer";
        public string Mode { get; set; }
        public string PlanFile { get; set; }
        public string Document { get; set; }
        public CoordinateCheck Coordinates { get; set; }
        public PlacerCounts Counts { get; set; }
        public bool Stopped { get; set; }
        public bool RolledBack { get; set; }
        public string Failure { get; set; }
        public List<PlacedPlot> Placed { get; set; }
        public List<SkippedPlot> Skipped { get; set; }
        public List<string> Issues { get; set; }

        // Kept out of the file, the counts carry it
        [Newtonsoft.Json.JsonIgnore]
        public int ParametersWritten { get; set; }
    }

    public class CoordinateCheck
    {
        public bool Match { get; set; }
        public string Message { get; set; }
        public double[] PlanPosition { get; set; }
        public double[] PlanSharedPosition { get; set; }
        public double? PlanAngleDeg { get; set; }
        public double[] DocumentPosition { get; set; }
        public double[] DocumentSharedPosition { get; set; }
        public double DocumentAngleDeg { get; set; }
    }

    public class PlacerCounts
    {
        public int PlotsInPlan { get; set; }
        public int Placed { get; set; }
        public int Skipped { get; set; }
        public int ParametersWritten { get; set; }
        public int Issues { get; set; }
    }

    // In a dry run this is a plot that would be placed, with no instance id and the parameters it would write
    public class PlacedPlot
    {
        public string Plot { get; set; }
        public string File { get; set; }
        public double RotationDeg { get; set; }
        public bool Mirrored { get; set; }
        public long? InstanceId { get; set; }
        public List<string> Parameters { get; set; }
    }

    public class SkippedPlot
    {
        public string Plot { get; set; }
        public string Reason { get; set; }
    }
}
