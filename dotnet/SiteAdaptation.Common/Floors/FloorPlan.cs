using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.Floors
{
    // R378, R379. One floor of the plan as the document can take it, the candidate of R113 with its item of R368
    internal sealed class PlannedFloor
    {
        public FloorPlacer.Candidate Candidate;
        public PlanItem Item;
        public bool InScope;

        public string Plot => Candidate.Plot;
        public string Kind => Candidate.Kind;
        public string Key => FloorPlan.KeyOf(Candidate.Plot, Candidate.Kind);
        public string Label => "plot " + Candidate.Plot + " " + Candidate.Kind;
        public bool IsRamp => Candidate.Kind == FloorPlacer.Ramp;
    }

    // R378, R379. The plan floors of buttons 5 and 6 held against the document: the checks of R83 and R113, every floor the document can
    // take, the reason each other one of the scope is skipped, and the floors the document holds now
    internal sealed class FloorPlanView
    {
        public double[] Offset;
        public string PlotParameter;
        public PlanFloorSettings Settings;
        public readonly List<PlannedFloor> Floors = new List<PlannedFloor>();
        public readonly List<string> Listed = new List<string>();
        public IReadOnlyList<FloorElement> Elements = new List<FloorElement>();

        // kind|plot value to the plot number, so a floor with no tag is found by the plot value of R114
        public readonly Dictionary<string, string> PlotOfValue = new Dictionary<string, string>(StringComparer.Ordinal);

        public string KindOf(FloorElement element)
        {
            return element.Tag != null ? (FloorPlacer.Kinds.Contains(element.Tag.Key.Kind) ? element.Tag.Key.Kind : null) : FloorPlacer.KindOf(Settings, element.TypeName);
        }

        // The plot of a floor with no tag by its kind and plot value, null when the plan holds no such floor
        public string PlotOf(FloorElement element)
        {
            string kind = FloorPlacer.KindOf(Settings, element.TypeName);
            return kind == null || string.IsNullOrWhiteSpace(element.PlotValue) ? null : PlotOfValue.TryGetValue(kind + "|" + element.PlotValue.Trim(), out string plot) ? plot : null;
        }
    }

    internal static class FloorPlan
    {
        public const string Outline = "outline";
        public const string Top = "top";
        public const string Corners = "corners";

        public static string KeyOf(string plot, string kind)
        {
            return plot + "|" + kind;
        }

        // The checks of R83 and R113 before anything changes, each stop named, then every floor of the plan in plan order and generic,
        // yard, ramp within a plot
        public static FloorPlanView Read(IFloorDocument document, PlanFile plan, Func<string, bool> inScope, ICollection<string> lastPlaced)
        {
            string format = Placer.CheckFormat(plan);
            // The prototype folder is the business of 7 Place villas, a floor needs none
            if (format != null && !format.Contains("prototypeFolder"))
            {
                throw new InvalidDataException(format);
            }
            string settings = FloorPlacer.CheckSettings(plan);
            if (settings != null)
            {
                throw new InvalidDataException(settings);
            }
            SurveyReading survey = document.Survey();
            CoordinateCheck coordinates = Placer.CheckCoordinates(plan, survey);
            if (!coordinates.Match)
            {
                throw new InvalidDataException(coordinates.Message ?? "the plan or the document gives no survey point, nothing was changed (R83)");
            }
            var view = new FloorPlanView
            {
                Offset = SharedCoordinates.Offset(survey.Position, survey.SharedPosition), Settings = plan.FloorSettings, PlotParameter = plan.FloorSettings.PlotParameter,
            };
            var types = new Dictionary<string, FloorTypeEntry>(StringComparer.Ordinal);
            foreach (FloorTypeEntry type in document.FloorTypes() ?? new List<FloorTypeEntry>())
            {
                if (type?.Name != null && !types.ContainsKey(type.Name))
                {
                    types[type.Name] = type;
                }
            }
            var levels = new Dictionary<string, LevelEntry>(StringComparer.Ordinal);
            foreach (LevelEntry level in document.Levels() ?? new List<LevelEntry>())
            {
                if (level?.Name != null && !levels.ContainsKey(level.Name))
                {
                    levels[level.Name] = level;
                }
            }
            var missingTypes = new SortedSet<string>(StringComparer.Ordinal);
            var stops = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var none = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlanPlot plot in plan.Plots ?? new List<PlanPlot>())
            {
                string number = plot?.Plot?.Trim();
                if (string.IsNullOrEmpty(number))
                {
                    continue;
                }
                bool scoped = inScope == null || inScope(number);
                bool first = seen.Add(number);
                foreach (string kind in FloorPlacer.Kinds)
                {
                    PlanFloor floor = kind == FloorPlacer.Generic ? plot.Floors?.Generic : kind == FloorPlacer.Yard ? plot.Floors?.Yard : plot.Floors?.Ramp;
                    string why = !first ? "the plan lists this plot number more than once, only its first entry is tried"
                        : floor == null ? "the plan has no " + kind + " floor for this plot" : null;
                    FloorPlacer.Candidate candidate = null;
                    if (why == null)
                    {
                        string value = floor.PlotValue?.Value?.Trim();
                        if (!string.IsNullOrEmpty(value))
                        {
                            view.PlotOfValue[kind + "|" + value] = number;
                        }
                        try
                        {
                            why = FloorPlacer.Check(plan.FloorSettings, number, kind, floor, view.Offset, types, levels, none, missingTypes, stops, out candidate);
                        }
                        catch (Exception failure) when (!(failure is OutOfMemoryException))
                        {
                            why = "the floor could not be read: " + failure.Message;
                        }
                    }
                    if (why != null)
                    {
                        if (scoped)
                        {
                            view.Listed.Add("plot " + number + " " + kind + ": skipped, " + why);
                        }
                        continue;
                    }
                    if (candidate != null)
                    {
                        string key = KeyOf(number, kind);
                        view.Floors.Add(new PlannedFloor
                        {
                            Candidate = candidate, InScope = scoped,
                            Item = new PlanItem(new TagKey(number, kind, ""), PlanValues(candidate), scoped, lastPlaced != null && lastPlaced.Contains(key)),
                        });
                    }
                }
            }
            if (missingTypes.Count > 0)
            {
                stops.Insert(0, "the document has no floor type " + string.Join(", ", missingTypes));
            }
            if (stops.Count > 0)
            {
                throw new InvalidDataException("Stopped before any floor was changed: " + string.Join(". ", stops.Distinct()) + " (R113).");
            }
            view.Elements = document.Floors(view.PlotParameter) ?? new List<FloorElement>();
            return view;
        }

        // R368. The values of a floor: its outline as the corners of every loop, sorted so the order Revit lists them in does not count,
        // and the top of a flat floor or the four corners of a ramp with their z, in internal metres
        public static PlacedValues Values(IEnumerable<double[]> vertices, string kind, double? top, IEnumerable<double[]> corners)
        {
            var values = new Dictionary<string, double[]>(StringComparer.Ordinal) { [Outline] = Sorted(vertices, false) };
            if (kind == FloorPlacer.Ramp)
            {
                if (corners != null)
                {
                    values[Corners] = Sorted(corners, true);
                }
            }
            else if (top.HasValue)
            {
                values[Top] = new[] { top.Value };
            }
            return new PlacedValues(values);
        }

        public static PlacedValues PlanValues(FloorPlacer.Candidate candidate)
        {
            return Values(candidate.Loops.SelectMany(loop => loop.Segments.Select(segment => segment.Start)), candidate.Kind, candidate.TopInternal, candidate.CornersInternal);
        }

        public static PlacedValues Current(FloorElement element, string kind)
        {
            return Values(element.Vertices ?? new List<double[]>(), kind, element.State?.TopMax, element.State?.Corners);
        }

        private static double[] Sorted(IEnumerable<double[]> points, bool withZ)
        {
            return points.Select(point => withZ ? new[] { JsonOut.Round3(point[0]), JsonOut.Round3(point[1]), JsonOut.Round3(point[2]) } : new[] { JsonOut.Round3(point[0]), JsonOut.Round3(point[1]) })
                .OrderBy(point => point[0]).ThenBy(point => point[1]).SelectMany(point => point).ToArray();
        }

        // R114. A ramp is shaped again from flat: each corner vertex takes the plan corner that sits on it in plan within 2 mm, whatever
        // order Revit lists them in, and is lifted from the lowest corner to its own z
        public static void Shape(IFloorDocument document, string id, PlannedFloor floor)
        {
            List<double[]> plan = floor.Candidate.CornersInternal;
            IList<double[]> vertices = document.ShapeCorners(id);
            if (vertices == null || vertices.Count != plan.Count)
            {
                throw new InvalidOperationException(floor.Label + ": the slab shape editor gives " + (vertices == null ? 0 : vertices.Count) + " corners, the plan has " + plan.Count);
            }
            var used = new HashSet<int>();
            for (int index = 0; index < vertices.Count; index++)
            {
                int match = Enumerable.Range(0, plan.Count).Where(corner => !used.Contains(corner) && Flat(vertices[index], plan[corner]) <= FloorPlacer.CornerOnVertex + 1e-9).DefaultIfEmpty(-1).First();
                if (match < 0)
                {
                    throw new InvalidOperationException(floor.Label + ": shape corner " + index + " sits on no plan corner");
                }
                used.Add(match);
                document.SetCornerOffset(id, index, plan[match][2] - floor.Candidate.TopInternal);
            }
        }

        // R116. Null when the floor reads back as the plan within 1 mm, else what differs
        public static string Mismatch(IFloorDocument document, string id, PlannedFloor floor)
        {
            return FloorPlacer.Mismatch(document.ReadBack(id), new PlanFloorCheck { Top = floor.Candidate.TopInternal, Corners = floor.Candidate.CornersInternal });
        }

        public static double Flat(double[] a, double[] b)
        {
            return Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));
        }

        public static string Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
