using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Tags;
using SiteAdaptation.Common.BoundaryWalls;

namespace SiteAdaptation.Tests
{
    // R380. A document of walls with no Revit type: a wall stands from its level plus its base offset up its height, its location line
    // on the wall centre unless a test moves it
    internal sealed class FakeWalls : IWallDocument
    {
        public sealed class Wall
        {
            public string UniqueId;
            public string TypeKey;
            public string LevelKey;
            public double LevelM;
            public double[] Start;
            public double[] End;
            public double BaseOffset;
            public double Height;
            public bool Centred = true;
            public PlacedTag Tag;

            public double Base => LevelM + BaseOffset;
            public double Top => Base + Height;
        }

        public readonly List<Wall> Items = new List<Wall>();
        public readonly List<WallTypeEntry> Types = new List<WallTypeEntry> { new WallTypeEntry { Name = "BW", Key = "1" }, new WallTypeEntry { Name = "Generic Wall-150mm_ Garden", Key = "2" } };
        public readonly List<LevelEntry> LevelList = new List<LevelEntry> { new LevelEntry { Name = "Level 0", Key = "10", ElevationM = 0.0 } };
        public readonly List<string> Calls = new List<string>();

        // Metres added to every base read back, a model that does not hold what was set
        public double Drift;
        private int next;

        public string Title => "Site.rvt";

        public SurveyReading Survey()
        {
            return new SurveyReading { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 }, AngleToTrueNorthDeg = 0 };
        }

        public IReadOnlyList<WallTypeEntry> WallTypes() => Types;

        public IReadOnlyList<LevelEntry> Levels() => LevelList;

        public IReadOnlyList<WallElement> Walls()
        {
            return Items.Select(wall => new WallElement { UniqueId = wall.UniqueId, TypeName = Types.FirstOrDefault(type => type.Key == wall.TypeKey)?.Name, Tag = wall.Tag, State = Read(wall) }).ToList();
        }

        public string CreateWall(double[] start, double[] end, string typeKey, string levelKey, double baseOffset, double height)
        {
            string id = "wall-" + (++next).ToString(CultureInfo.InvariantCulture);
            Calls.Add("create " + id);
            Items.Add(new Wall { UniqueId = id, TypeKey = typeKey, LevelKey = levelKey, LevelM = Level(levelKey), Start = start, End = end, BaseOffset = baseOffset, Height = height });
            return id;
        }

        public void SetWall(string uniqueId, double[] start, double[] end, string levelKey, double baseOffset, double height)
        {
            Calls.Add("set " + uniqueId);
            Wall wall = Of(uniqueId);
            wall.Start = start;
            wall.End = end;
            wall.LevelKey = levelKey;
            wall.LevelM = Level(levelKey);
            wall.BaseOffset = baseOffset;
            wall.Height = height;
        }

        public void Regenerate()
        {
            Calls.Add("regenerate");
        }

        public WallReadBack ReadBack(string uniqueId)
        {
            return Read(Of(uniqueId));
        }

        public void Tag(string uniqueId, PlacedTag tag)
        {
            Of(uniqueId).Tag = tag;
        }

        public Wall Of(string uniqueId)
        {
            return Items.Single(wall => wall.UniqueId == uniqueId);
        }

        // A wall of type BW placed by hand, with no tag
        public Wall ByHand(double[] start, double[] end, double bottom, double top)
        {
            var wall = new Wall { UniqueId = "hand-" + (++next).ToString(CultureInfo.InvariantCulture), TypeKey = "1", LevelKey = "10", Start = start, End = end, BaseOffset = bottom, Height = top - bottom };
            Items.Add(wall);
            return wall;
        }

        private double Level(string key) => LevelList.Single(level => level.Key == key).ElevationM;

        private WallReadBack Read(Wall wall)
        {
            return new WallReadBack { Start = wall.Start, End = wall.End, Base = wall.Base + Drift, Top = wall.Top + Drift, Centred = wall.Centred };
        }
    }

    // R380. The walls of the plan of NBH 012 the wall tests share, on the plots of FloorPlans: plot 101 at x 1000 to 1010 and plot 102 at
    // x 1100, the edge both share at x 1010, the back edge of 101 at y 2020, both at FFL 10.5, so the base 10.4 and the top 12.2
    internal static class WallPlans
    {
        public static PlanWallItem Wall(string plot, int edge, double[] start, double[] end, params string[] plots)
        {
            return new PlanWallItem { Plot = plot, Edge = edge, Plots = plots.ToList(), Ends = new[] { start, end }, Base = 10.4, Top = 12.2, BasePlot = plot, TopPlot = plot };
        }

        public static List<PlanWallItem> Two()
        {
            return new List<PlanWallItem>
            {
                Wall("101", 1, new[] { 1010.0, 2000.0 }, new[] { 1010.0, 2020.0 }, "101", "102"),
                Wall("101", 2, new[] { 1010.0, 2020.0 }, new[] { 1000.0, 2020.0 }, "101"),
            };
        }

        // The walls array as BuildPlan writes it, each value sourced
        public static JArray Json(IEnumerable<PlanWallItem> walls)
        {
            return new JArray(walls.Select(wall => new JObject
            {
                ["plot"] = wall.Plot, ["edge"] = wall.Edge,
                ["plots"] = new JObject { ["value"] = new JArray(wall.Plots), ["source"] = "locationPlan", ["status"] = "DOCUMENT" },
                ["ends"] = new JObject { ["value"] = new JArray(wall.Ends.Select(end => new JArray(end))), ["source"] = "keyRules", ["status"] = "UNKNOWN" },
                ["base"] = new JObject { ["value"] = wall.Base, ["source"] = "keyRules" }, ["top"] = new JObject { ["value"] = wall.Top, ["source"] = "keyRules" },
                ["basePlot"] = wall.BasePlot, ["topPlot"] = wall.TopPlot,
            }));
        }
    }
}
