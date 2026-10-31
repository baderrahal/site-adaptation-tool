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
        public const string TypeValue = "type";
        public const string PlotValue = "plot";

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
        // the top of a flat floor or the four corners of a ramp with their z, in internal metres, and its floor type and plot value as the
        // code points of their text, since a placed value is a list of numbers and the plan types a ramp by its slope
        public static PlacedValues Values(IEnumerable<double[]> vertices, string kind, double? top, IEnumerable<double[]> corners, string typeName, string plotValue)
        {
            var values = new Dictionary<string, double[]>(StringComparer.Ordinal) { [Outline] = Sorted(vertices, false) };
            if (typeName != null)
            {
                values[TypeValue] = CodePoints(typeName);
            }
            if (!string.IsNullOrWhiteSpace(plotValue))
            {
                values[PlotValue] = CodePoints(plotValue.Trim());
            }
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
            return Values(candidate.Loops.SelectMany(loop => loop.Segments.Select(segment => segment.Start)), candidate.Kind, candidate.TopInternal, candidate.CornersInternal,
                candidate.Type.Name, candidate.Floor.PlotValue?.Value);
        }

        public static PlacedValues Current(FloorElement element, string kind)
        {
            return Values(element.Vertices ?? new List<double[]>(), kind, element.State?.TopMax, element.State?.Corners, element.TypeName, element.PlotValue);
        }

        // R368. A tag of 5d written before the type and the plot value were placed values names neither, so it is read as holding what
        // the floor holds now for them, and such a floor is not changed by hand for that alone
        public static PlacedTag Named(PlacedTag tag, PlacedValues current)
        {
            var values = tag.Placed.Values.ToDictionary(value => value.Key, value => value.Value.ToArray(), StringComparer.Ordinal);
            bool filled = false;
            foreach (string name in new[] { TypeValue, PlotValue })
            {
                if (!values.ContainsKey(name) && current.Values.TryGetValue(name, out IReadOnlyList<double> now))
                {
                    values[name] = now.ToArray();
                    filled = true;
                }
            }
            return filled ? new PlacedTag(tag.Version, tag.Key, tag.Run, tag.Profile, tag.PlacedId, new PlacedValues(values)) : tag;
        }

        // R379. What of a floor besides its level is not the plan, its outline, floor type or plot value, which 5 changes or lists and 6
        // never sets, by the values 5 compares
        public static IReadOnlyList<string> OffPlan(FloorElement element, PlannedFloor floor)
        {
            var words = new Dictionary<string, string>(StringComparer.Ordinal) { [Outline] = "outline", [TypeValue] = "floor type", [PlotValue] = "Plot value" };
            return Current(element, floor.Kind).Differences(floor.Item.Plan).Where(words.ContainsKey).Select(name => words[name]).ToList();
        }

        private static double[] CodePoints(string text)
        {
            return text.Select(character => (double)character).ToArray();
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
