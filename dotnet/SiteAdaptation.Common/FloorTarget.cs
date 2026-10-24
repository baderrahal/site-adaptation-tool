using System.Collections.Generic;

namespace SiteAdaptation.Common
{
    // R113. The slice of the open Revit document the floor step needs. SiteAdaptation.Placer fills it from the Revit API and tests fill it by hand.
    // Points are in metres in internal coordinates, levels as Level.Elevation in metres
    public interface IFloorTarget
    {
        string DocumentTitle { get; }

        SurveyReading Survey();

        IList<FloorTypeEntry> FloorTypes();

        IList<LevelEntry> Levels();

        // Every floor of the document with its type name and the text of the plot parameter, null where it has none
        IList<ExistingFloor> Floors(string plotParameter);

        // The rest is called in a real run only, between Begin and Commit or RollBack
        void Begin();

        long CreateFloor(IList<FloorLoop> loops, string typeKey, string levelKey);

        void SetHeightOffset(long floorId, double metres);

        // Brings the geometry up to date. The step calls it twice per run, never once per floor
        void Regenerate();

        // Turns slab shape editing on and gives its corner vertices, so the ramp corners can be set one by one
        IList<double[]> ShapeCorners(long floorId);

        // Lifts one corner vertex of ShapeCorners by metres above the top of the floor
        void SetCornerOffset(long floorId, int corner, double metres);

        FloorReadBack ReadBack(long floorId);

        ParameterWrite WriteText(long floorId, string name, string value);

        CommitResult Commit();

        void RollBack();

        IList<string> Warnings { get; }
    }

    public class FloorTypeEntry
    {
        public string Name { get; set; }
        public string Key { get; set; }
    }

    public class LevelEntry
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public double ElevationM { get; set; }
    }

    public class ExistingFloor
    {
        public long Id { get; set; }
        public string TypeName { get; set; }
        public string PlotValue { get; set; }
    }

    // One closed loop. A segment with Mid is an arc through Start, Mid and End, else a straight line
    public class FloorLoop
    {
        public List<FloorSegment> Segments { get; set; }
    }

    public class FloorSegment
    {
        public double[] Start { get; set; }
        public double[] End { get; set; }
        public double[] Mid { get; set; }
    }

    // TopMax is the highest point of the floor. Corners are the slab shape corner vertices, null when shape editing is off
    public class FloorReadBack
    {
        public double? TopMax { get; set; }
        public List<double[]> Corners { get; set; }
    }

    // placer_report.json of a run with the floor step (R116). Links is the report of R87 when the links ran too
    public class PlacerRunReport
    {
        public string Tool { get; set; } = "SiteAdaptation.Placer";
        public string Steps { get; set; }
        public PlacerReport Links { get; set; }
        public FloorReport Floors { get; set; }
    }

    public class FloorReport
    {
        public string Step { get; set; } = "floors";
        public string Mode { get; set; }
        public string PlanFile { get; set; }
        public string Document { get; set; }
        // The plots this run tries, null for all of them
        public List<string> PlotFilter { get; set; }
        public CoordinateCheck Coordinates { get; set; }
        public FloorCounts Counts { get; set; }
        // Floors of the types of floorSettings in the document before and after the run
        public int? FloorsBefore { get; set; }
        public int? FloorsAfter { get; set; }
        public bool Stopped { get; set; }
        public bool RolledBack { get; set; }
        public string Failure { get; set; }
        public List<PlacedFloor> Placed { get; set; }
        public List<SkippedFloor> Skipped { get; set; }
        public List<string> Issues { get; set; }
    }

    public class FloorCounts
    {
        public int PlotsInPlan { get; set; }
        public int FilteredOut { get; set; }
        public int Placed { get; set; }
        public int Skipped { get; set; }
        public int PlotValuesWritten { get; set; }
        public int Issues { get; set; }
        public Dictionary<string, int> PlacedByKind { get; set; }
        public Dictionary<string, int> SkippedByKind { get; set; }
    }

    // In a dry run a floor that would be placed, with no instance id
    public class PlacedFloor
    {
        public string Plot { get; set; }
        public string Kind { get; set; }
        public string Type { get; set; }
        public string Level { get; set; }
        public double HeightOffset { get; set; }
        public int Loops { get; set; }
        public int Arcs { get; set; }
        public long? InstanceId { get; set; }
    }

    public class SkippedFloor
    {
        public string Plot { get; set; }
        public string Kind { get; set; }
        public string Reason { get; set; }
    }
}
