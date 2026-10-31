using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Tests
{
    // R378, R379. A document of floors with no Revit type: a flat floor stands at its level plus its height offset, a shaped one lifts
    // each corner by its offset. The shape editor gives its corners in the reverse order of the sketch, as Revit lists them in its own
    internal sealed class FakeFloors : IFloorDocument
    {
        public sealed class Floor
        {
            public string UniqueId;
            public string TypeKey;
            public double LevelM;
            public double HeightOffset;
            public List<double[]> Vertices = new List<double[]>();
            public double[] CornerOffsets;
            public PlacedTag Tag;
            public Dictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.Ordinal);

            public double Top => LevelM + HeightOffset;
        }

        public readonly List<Floor> Items = new List<Floor>();
        public readonly List<FloorTypeEntry> Types = new List<FloorTypeEntry>
        {
            new FloorTypeEntry { Name = "Generic 150mm", Key = "1" }, new FloorTypeEntry { Name = "Yard", Key = "2" }, new FloorTypeEntry { Name = "Ramp 0.5", Key = "3" },
        };
        public readonly List<LevelEntry> LevelList = new List<LevelEntry> { new LevelEntry { Name = "Level 0", Key = "10", ElevationM = 0.0 } };
        public readonly List<string> Calls = new List<string>();
        public bool RefusePlot;

        // Metres added to every read back, a model that does not hold what was set
        public double Drift;
        private int next;

        public string Title => "Site.rvt";

        public SurveyReading Survey()
        {
            return new SurveyReading { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 }, AngleToTrueNorthDeg = 0 };
        }

        public IReadOnlyList<FloorTypeEntry> FloorTypes() => Types;

        public IReadOnlyList<LevelEntry> Levels() => LevelList;

        public IReadOnlyList<FloorElement> Floors(string plotParameter)
        {
            return Items.Select(floor => new FloorElement
            {
                UniqueId = floor.UniqueId, TypeName = Types.FirstOrDefault(type => type.Key == floor.TypeKey)?.Name,
                PlotValue = floor.Parameters.TryGetValue(plotParameter, out string value) ? value : null, Tag = floor.Tag,
                Vertices = floor.Vertices.Select(point => new[] { point[0], point[1] }).ToList(), State = Read(floor),
            }).ToList();
        }

        public string CreateFloor(IList<FloorLoop> loops, string typeKey, string levelKey)
        {
            string id = "floor-" + (++next).ToString(CultureInfo.InvariantCulture);
            Calls.Add("create " + id);
            Items.Add(new Floor
            {
                UniqueId = id, TypeKey = typeKey, LevelM = LevelList.Single(level => level.Key == levelKey).ElevationM,
                Vertices = loops.SelectMany(loop => loop.Segments.Select(segment => new[] { segment.Start[0], segment.Start[1] })).ToList(),
            });
            return id;
        }

        public void SetHeightOffset(string uniqueId, double metres)
        {
            Calls.Add("offset " + uniqueId);
            Of(uniqueId).HeightOffset = metres;
        }

        public void SetType(string uniqueId, string typeKey)
        {
            Calls.Add("type " + uniqueId);
            Of(uniqueId).TypeKey = typeKey;
        }

        public void Regenerate()
        {
            Calls.Add("regenerate");
        }

        public IList<double[]> ShapeCorners(string uniqueId)
        {
            Calls.Add("shape " + uniqueId);
            Floor floor = Of(uniqueId);
            floor.CornerOffsets = new double[floor.Vertices.Count];
            return Enumerable.Range(0, floor.Vertices.Count).Reverse().Select(index => new[] { floor.Vertices[index][0], floor.Vertices[index][1], floor.Top }).ToList();
        }

        public void SetCornerOffset(string uniqueId, int corner, double metres)
        {
            Floor floor = Of(uniqueId);
            floor.CornerOffsets[floor.Vertices.Count - 1 - corner] = metres;
        }

        public FloorReadBack ReadBack(string uniqueId)
        {
            return Read(Of(uniqueId));
        }

        public ParameterWrite WriteText(string uniqueId, string name, string value)
        {
            if (RefusePlot)
            {
                return ParameterWrite.Missing;
            }
            Of(uniqueId).Parameters[name] = value;
            return ParameterWrite.Written;
        }

        public void Tag(string uniqueId, PlacedTag tag)
        {
            Of(uniqueId).Tag = tag;
        }

        public Floor Of(string uniqueId)
        {
            return Items.Single(floor => floor.UniqueId == uniqueId);
        }

        // A floor of the plot and kind placed by hand, with the plot value and no tag
        public Floor ByHand(string typeKey, string plotValue, double top, params double[][] vertices)
        {
            var floor = new Floor { UniqueId = "hand-" + (++next).ToString(CultureInfo.InvariantCulture), TypeKey = typeKey, HeightOffset = top, Vertices = vertices.ToList() };
            floor.Parameters["Plot"] = plotValue;
            Items.Add(floor);
            return floor;
        }

        private FloorReadBack Read(Floor floor)
        {
            if (floor.CornerOffsets == null)
            {
                return new FloorReadBack { TopMax = floor.Top + Drift };
            }
            List<double[]> corners = Enumerable.Range(0, floor.Vertices.Count).Select(index => new[] { floor.Vertices[index][0], floor.Vertices[index][1], floor.Top + floor.CornerOffsets[index] + Drift }).ToList();
            return new FloorReadBack { TopMax = corners.Max(corner => corner[2]), Corners = corners };
        }
    }

    // R378, R379. The plan of NBH 012 the floor tests share: plot 10n stands at x 1000 + 100 n in shared coordinates, its Generic floor
    // at FFL 10.5, its yard 0.1 below and its ramp of four corners in front of the face
    internal static class FloorPlans
    {
        public static PlanFile Plan(params PlanPlot[] plots)
        {
            return new PlanFile
            {
                Format = PlanFile.FormatName, FormatVersion = PlanFile.Version, Neighbourhood = "012",
                SurveyPoint = new PlanSurveyPoint { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 } },
                AngleToTrueNorthDeg = 0, PrototypeFolder = @"C:\Prototypes",
                FloorSettings = new PlanFloorSettings
                {
                    Types = new Dictionary<string, List<string>> { ["generic"] = new List<string> { "Generic 150mm" }, ["yard"] = new List<string> { "Yard" }, ["ramp"] = new List<string> { "Ramp 0.5" } },
                    Levels = new Dictionary<string, string> { ["generic"] = "Level 0", ["yard"] = "Level 0", ["ramp"] = "Level 0" },
                    PlotParameter = "Plot",
                },
                Plots = plots.ToList(),
            };
        }

        public static PlanPlot Plot(string number, double ffl = 10.5, bool yard = true)
        {
            double x = 1000 + 100 * (double.Parse(number, CultureInfo.InvariantCulture) - 100);
            string value = "VD4-" + number;
            return new PlanPlot
            {
                Plot = number,
                Levels = new PlanLevels { Ffl = new Sourced<double?> { Value = ffl } },
                Floors = new PlanFloors
                {
                    Generic = Flat("Generic 150mm", ffl, value, Square(x, 2000, 10, 20)),
                    Yard = yard ? Flat("Yard", ffl - 0.1, value, Square(x, 2020, 10, 10)) : null,
                    Ramp = Ramp(value, new[] { new[] { x, 1995, 10.0 }, new[] { x + 10, 1995, 10.1 }, new[] { x + 10, 2000, 10.3 }, new[] { x, 2000, 10.2 } }),
                },
            };
        }

        public static List<double[]> Square(double x, double y, double width, double depth)
        {
            return new List<double[]> { new[] { x, y }, new[] { x + width, y }, new[] { x + width, y + depth }, new[] { x, y + depth } };
        }

        public static PlanFloor Flat(string type, double z, string value, List<double[]> points)
        {
            return new PlanFloor
            {
                Type = new Sourced<string> { Value = type }, Level = new Sourced<string> { Value = "Level 0" }, LevelElevation = new Sourced<double?> { Value = 0.0 },
                Z = new Sourced<double?> { Value = z }, PlotValue = new Sourced<string> { Value = value },
                Outline = new Sourced<PlanOutline> { Value = new PlanOutline { Loops = new List<PlanLoop> { new PlanLoop { Points = points } } } },
            };
        }

        public static PlanFloor Ramp(string value, double[][] corners)
        {
            return new PlanFloor
            {
                Type = new Sourced<string> { Value = "Ramp 0.5" }, Level = new Sourced<string> { Value = "Level 0" }, LevelElevation = new Sourced<double?> { Value = 0.0 },
                PlotValue = new Sourced<string> { Value = value }, Corners = new Sourced<List<double[]>> { Value = corners.ToList() },
                Outline = new Sourced<PlanOutline> { Value = new PlanOutline { Loops = new List<PlanLoop> { new PlanLoop { Points = corners.Select(corner => new[] { corner[0], corner[1] }).ToList() } } } },
            };
        }
    }
}
