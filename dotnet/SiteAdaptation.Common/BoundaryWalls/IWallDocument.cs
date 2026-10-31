using System;
using System.Collections.Generic;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.BoundaryWalls
{
    // R380. The thin Revit side of button 8, which only reads and carries out what WallsRun decides. One folder per button beside Floors,
    // PlaceVillas and Report. Points are in metres in internal coordinates, a level by Level.Elevation in metres. The transaction is the
    // one of the flow (R382)
    public interface IWallDocument
    {
        string Title { get; }

        SurveyReading Survey();

        IReadOnlyList<WallTypeEntry> WallTypes();

        IReadOnlyList<LevelEntry> Levels();

        // Every wall of the document with its type name, its tag of R368 or none and its state now
        IReadOnlyList<WallElement> Walls();

        // The rest inside the one transaction of the button. CreateWall gives the UniqueId of the new wall, its location line on the wall
        // centre, its base on the level plus baseOffset and its top height above that, with no top constraint
        string CreateWall(double[] start, double[] end, string typeKey, string levelKey, double baseOffset, double height);

        // Moves a wall the tool placed onto the plan in place, so it keeps its UniqueId
        void SetWall(string uniqueId, double[] start, double[] end, string levelKey, double baseOffset, double height);

        // Brings the geometry up to date. A run calls it once, never once per wall
        void Regenerate();

        WallReadBack ReadBack(string uniqueId);

        void Tag(string uniqueId, PlacedTag tag);
    }

    public sealed class WallTypeEntry
    {
        public string Name { get; set; }
        public string Key { get; set; }
    }

    // What a wall holds now: the two ends of its location line in plan, its base and top in internal metres and whether its location
    // line is the wall centre
    public sealed class WallReadBack
    {
        public double[] Start { get; set; }
        public double[] End { get; set; }
        public double Base { get; set; }
        public double Top { get; set; }
        public bool Centred { get; set; }
    }

    public sealed class WallElement
    {
        public string UniqueId { get; set; }
        public string TypeName { get; set; }

        // Null for a wall with no entity of the schema
        public PlacedTag Tag { get; set; }
        public WallReadBack State { get; set; } = new WallReadBack();
    }

    // R380. What one run of button 8 reads, given by WallLoad
    public sealed class WallInputs
    {
        public PlanFile Plan { get; set; }
        public IReadOnlyList<PlanWallItem> Walls { get; set; } = new List<PlanWallItem>();
        public string PlanPath { get; set; }

        // The level profile the plan was built by, written in each tag
        public string Profile { get; set; }

        // R366, null for every plot of the plan
        public Func<string, bool> InScope { get; set; }

        // majority.wallTypes.boundary of the config, the pattern of the wall type BW, the one R335 measured the key walls by
        public string BoundaryType { get; set; }

        // The walls the last real run of 8 placed, as plot|part, so a wall deleted by hand is told from one never placed (R368)
        public ICollection<string> LastPlaced { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        // The UniqueIds the user set to the plan
        public ICollection<string> SetToPlan { get; set; } = new HashSet<string>(StringComparer.Ordinal);
    }
}
