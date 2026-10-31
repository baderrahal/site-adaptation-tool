using System;
using System.Collections.Generic;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.Floors
{
    // R378, R379. The thin Revit side of buttons 5 and 6, which only reads and carries out what FloorsRun and LevelsRun decide. Points are
    // in metres in internal coordinates, a level by Level.Elevation in metres. The transaction is the one of the flow (R382)
    public interface IFloorDocument
    {
        string Title { get; }

        SurveyReading Survey();

        IReadOnlyList<FloorTypeEntry> FloorTypes();

        IReadOnlyList<LevelEntry> Levels();

        // Every floor of the document with its type name, the text of the plot parameter, its tag of R368 or none and its state now
        IReadOnlyList<FloorElement> Floors(string plotParameter);

        // The rest inside the one transaction of the button. CreateFloor gives the UniqueId of the new floor
        string CreateFloor(IList<FloorLoop> loops, string typeKey, string levelKey);

        void SetHeightOffset(string uniqueId, double metres);

        // Gives the floor the type of the key in place, so it keeps its UniqueId when the plan types it anew
        void SetType(string uniqueId, string typeKey);

        // Brings the geometry up to date. A run calls it twice, never once per floor
        void Regenerate();

        // Sets the slab shape back to flat, turns shape editing on and gives its corner vertices, so a ramp is shaped again from flat
        IList<double[]> ShapeCorners(string uniqueId);

        // Lifts one corner vertex of ShapeCorners by metres above the top of the floor
        void SetCornerOffset(string uniqueId, int corner, double metres);

        FloorReadBack ReadBack(string uniqueId);

        ParameterWrite WriteText(string uniqueId, string name, string value);

        void Tag(string uniqueId, PlacedTag tag);
    }

    public sealed class FloorElement
    {
        public string UniqueId { get; set; }
        public string TypeName { get; set; }

        // The text of the plot parameter, null where it has none
        public string PlotValue { get; set; }

        // Null for a floor with no entity of the schema
        public PlacedTag Tag { get; set; }

        // The corners of every loop of its sketch, x and y
        public IReadOnlyList<double[]> Vertices { get; set; } = new List<double[]>();

        // The highest point and the slab shape corners, as a read back gives them
        public FloorReadBack State { get; set; } = new FloorReadBack();
    }

    // R378. What one run of button 5 reads, given by FloorLoad
    public sealed class FloorInputs
    {
        public PlanFile Plan { get; set; }
        public string PlanPath { get; set; }

        // The level profile the plan was built by, written in each tag
        public string Profile { get; set; }

        // R366, null for every plot of the plan
        public Func<string, bool> InScope { get; set; }

        // The floors the last real run of 5 placed, as plot|kind, so a floor deleted by hand is told from one never placed (R368)
        public ICollection<string> LastPlaced { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        // The UniqueIds the user set to the plan
        public ICollection<string> SetToPlan { get; set; } = new HashSet<string>(StringComparer.Ordinal);
    }

    // R379. What one run of button 6 reads: the floors of button 5 and what the worker command levelExcel takes
    public sealed class LevelRunInputs
    {
        public PlanFile Plan { get; set; }
        public string PlanPath { get; set; }

        // Plot number to why the plan holds no FFL for it, from missing of the plan
        public IReadOnlyDictionary<string, string> NoFfl { get; set; } = new Dictionary<string, string>();
        public string Profile { get; set; }
        public Func<string, bool> InScope { get; set; }

        // The inputs the plan was built on, by role: plan, config, rules, overrides, plotsCsv, clusters, levels and hands
        public IReadOnlyList<WorkerFile> ExcelInputs { get; set; } = new List<WorkerFile>();

        // addin.workerTimeLimitS.levelExcel
        public double TimeLimitS { get; set; }
        public WorkerDocument Document { get; set; }
    }
}
