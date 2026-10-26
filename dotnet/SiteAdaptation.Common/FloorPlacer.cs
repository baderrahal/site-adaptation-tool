using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.Common
{
    // R113 to R116. Every decision of the floor step of Place from plan. The Revit side only carries out what this class asks for
    public static class FloorPlacer
    {
        public const string Generic = "generic";
        public const string Yard = "yard";
        public const string Ramp = "ramp";
        public static readonly string[] Kinds = { Generic, Yard, Ramp };
        public const double TopTolerance = 0.001;
        public const double LevelTolerance = 0.001;
        // A ramp corner sits on an outline vertex when it lies this close in plan. Both are rounded to the mm by R2
        public const double CornerOnVertex = 0.002;
        // Revit refuses a curve shorter than about 0.8 mm, so a shorter edge is a plan error found before any change
        public const double ShortestEdge = 0.001;

        private sealed class Candidate
        {
            public string Plot;
            public string Kind;
            public PlanFloor Floor;
            public FloorTypeEntry Type;
            public LevelEntry Level;
            public List<FloorLoop> Loops;
            public double HeightOffset;
            public double TopInternal;
            public List<double[]> CornersInternal;
            public int Arcs;
        }

        // plots null tries every plot of the plan, else only the plot numbers listed
        public static FloorReport Run(PlanFile plan, string planPath, IFloorTarget target, bool real, ICollection<string> plots)
        {
            var report = new FloorReport
            {
                Mode = real ? "real" : "dryRun",
                PlanFile = planPath,
                Document = target.DocumentTitle,
                PlotFilter = plots == null ? null : plots.OrderBy(plot => plot, StringComparer.Ordinal).ToList(),
                Placed = new List<PlacedFloor>(),
                Skipped = new List<SkippedFloor>(),
                Issues = new List<string>(),
            };
            int filteredOut = 0;
            int written = 0;
            List<Candidate> candidates;
            try
            {
                SurveyReading survey = target.Survey();
                report.Coordinates = Placer.CheckCoordinates(plan, survey);
                string stop = Placer.CheckFormat(plan) ?? report.Coordinates.Message ?? CheckSettings(plan) ?? CheckFilter(plan, plots);
                if (stop != null)
                {
                    return Stop(report, plan, stop, filteredOut, written);
                }
                report.FloorsBefore = CountFloors(plan, target);
                candidates = Choose(plan, survey, target, report, plots, out filteredOut, out stop);
                if (stop != null)
                {
                    return Stop(report, plan, stop, filteredOut, written);
                }
            }
            catch (Exception failure)
            {
                // Nothing has changed yet, so the step stops and the report still says why
                return Stop(report, plan, "Stopped before any floor was placed: " + failure.Message, filteredOut, written);
            }
            if (!real)
            {
                report.Placed.AddRange(candidates.Select(candidate => Entry(candidate, null)));
                report.FloorsAfter = report.FloorsBefore;
                return Count(report, plan, filteredOut, written);
            }
            written = PlaceAll(plan, candidates, target, report);
            try
            {
                report.FloorsAfter = CountFloors(plan, target);
            }
            catch (Exception failure)
            {
                report.Issues.Add("the floors after the run could not be counted: " + failure.Message);
            }
            return Count(report, plan, filteredOut, written);
        }

        public static FloorReport StoppedReport(string mode, string planPath, string document, string failure)
        {
            var report = new FloorReport { Mode = mode, PlanFile = planPath, Document = document, Placed = new List<PlacedFloor>(), Skipped = new List<SkippedFloor>(), Issues = new List<string>() };
            return Stop(report, null, failure, 0, 0);
        }

        // R112. The plan must name the floor types and the level of every kind and the plot parameter
        public static string CheckSettings(PlanFile plan)
        {
            PlanFloorSettings settings = plan.FloorSettings;
            if (settings == null || settings.Types == null || settings.Levels == null || string.IsNullOrWhiteSpace(settings.PlotParameter))
            {
                return "The plan has no floorSettings with types, levels and plotParameter, no floor was placed.";
            }
            foreach (string kind in Kinds)
            {
                List<string> names;
                string level;
                if (!settings.Types.TryGetValue(kind, out names) || names == null || names.Count == 0 || names.Any(string.IsNullOrWhiteSpace))
                {
                    return "floorSettings.types has no type names for " + kind + ", no floor was placed.";
                }
                if (!settings.Levels.TryGetValue(kind, out level) || string.IsNullOrWhiteSpace(level))
                {
                    return "floorSettings.levels has no level for " + kind + ", no floor was placed.";
                }
            }
            return null;
        }

        // A plot list that names a plot the plan does not hold stops the step, so a typing error places nothing and says why
        public static string CheckFilter(PlanFile plan, ICollection<string> plots)
        {
            if (plots == null)
            {
                return null;
            }
            if (plots.Count == 0)
            {
                return "The plot list is empty, no floor was placed.";
            }
            var known = new HashSet<string>((plan.Plots ?? new List<PlanPlot>()).Where(plot => plot != null && plot.Plot != null).Select(plot => plot.Plot.Trim()), StringComparer.Ordinal);
            List<string> unknown = plots.Where(plot => plot == null || !known.Contains(plot.Trim())).ToList();
            return unknown.Count == 0 ? null : "The plan has no plot " + string.Join(", ", unknown) + ", no floor was placed.";
        }

        public static string KindOf(PlanFloorSettings settings, string typeName)
        {
            foreach (string kind in Kinds)
            {
                List<string> names;
                if (typeName != null && settings.Types.TryGetValue(kind, out names) && names != null && names.Contains(typeName))
                {
                    return kind;
                }
            }
            return null;
        }

        private static int CountFloors(PlanFile plan, IFloorTarget target)
        {
            return target.Floors(plan.FloorSettings.PlotParameter).Count(floor => KindOf(plan.FloorSettings, floor.TypeName) != null);
        }

        private static PlanFloor FloorOf(PlanFloors floors, string kind)
        {
            if (floors == null)
            {
                return null;
            }
            return kind == Generic ? floors.Generic : kind == Yard ? floors.Yard : floors.Ramp;
        }

        // R113 and R114. The floors that can be placed, in plan order and generic, yard, ramp within a plot. Every other one is skipped with its reason.
        // A floor type or a level the document does not have, or a level that is not where the plan says, stops the step before any change
        private static List<Candidate> Choose(PlanFile plan, SurveyReading survey, IFloorTarget target, FloorReport report, ICollection<string> plots, out int filteredOut, out string stop)
        {
            stop = null;
            filteredOut = 0;
            PlanFloorSettings settings = plan.FloorSettings;
            double[] offset = SharedCoordinates.Offset(survey.Position, survey.SharedPosition);
            var wanted = plots == null ? null : new HashSet<string>(plots.Select(plot => plot.Trim()), StringComparer.Ordinal);
            var types = new Dictionary<string, FloorTypeEntry>(StringComparer.Ordinal);
            foreach (FloorTypeEntry type in target.FloorTypes() ?? new List<FloorTypeEntry>())
            {
                if (type != null && type.Name != null && !types.ContainsKey(type.Name))
                {
                    types[type.Name] = type;
                }
            }
            var levels = new Dictionary<string, LevelEntry>(StringComparer.Ordinal);
            foreach (LevelEntry level in target.Levels() ?? new List<LevelEntry>())
            {
                if (level != null && level.Name != null && !levels.ContainsKey(level.Name))
                {
                    levels[level.Name] = level;
                }
            }
            // R114. A floor of a kind whose plot value is already held by a floor of that kind is placed already
            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExistingFloor floor in target.Floors(settings.PlotParameter) ?? new List<ExistingFloor>())
            {
                string kind = floor == null ? null : KindOf(settings, floor.TypeName);
                if (kind != null && !string.IsNullOrWhiteSpace(floor.PlotValue))
                {
                    existing.Add(kind + "|" + floor.PlotValue.Trim());
                }
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<Candidate>();
            var missingTypes = new SortedSet<string>(StringComparer.Ordinal);
            var stops = new List<string>();
            foreach (PlanPlot plot in plan.Plots ?? new List<PlanPlot>())
            {
                string number = plot == null || plot.Plot == null ? null : plot.Plot.Trim();
                bool first = string.IsNullOrEmpty(number) || seen.Add(number);
                if (wanted != null && (number == null || !wanted.Contains(number)))
                {
                    filteredOut++;
                    continue;
                }
                if (plot == null || string.IsNullOrEmpty(number))
                {
                    report.Skipped.Add(new SkippedFloor { Reason = "an entry in plots with no plot number" });
                    continue;
                }
                foreach (string kind in Kinds)
                {
                    PlanFloor floor = FloorOf(plot.Floors, kind);
                    if (!first)
                    {
                        report.Skipped.Add(new SkippedFloor { Plot = number, Kind = kind, Reason = "the plan lists this plot number more than once, only its first entry is tried" });
                        continue;
                    }
                    if (floor == null)
                    {
                        report.Skipped.Add(new SkippedFloor { Plot = number, Kind = kind, Reason = "the plan has no " + kind + " floor for this plot" });
                        continue;
                    }
                    Candidate candidate;
                    string why;
                    try
                    {
                        why = Check(settings, number, kind, floor, offset, types, levels, existing, missingTypes, stops, out candidate);
                    }
                    catch (Exception failure)
                    {
                        why = "the floor could not be read: " + failure.Message;
                        candidate = null;
                    }
                    if (why != null)
                    {
                        report.Skipped.Add(new SkippedFloor { Plot = number, Kind = kind, Reason = why });
                        continue;
                    }
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
            }
            if (missingTypes.Count > 0)
            {
                stops.Insert(0, "the document has no floor type " + string.Join(", ", missingTypes));
            }
            if (stops.Count > 0)
            {
                stop = "Stopped before any floor was placed: " + string.Join(". ", stops.Distinct()) + ".";
            }
            return candidates;
        }

        // Null and a candidate when the floor can be placed, null and no candidate when it stops the step, else the reason it is skipped
        private static string Check(PlanFloorSettings settings, string plot, string kind, PlanFloor floor, double[] offset, Dictionary<string, FloorTypeEntry> types,
            Dictionary<string, LevelEntry> levels, HashSet<string> existing, SortedSet<string> missingTypes, List<string> stops, out Candidate candidate)
        {
            candidate = null;
            string typeName = floor.Type == null ? null : floor.Type.Value;
            string levelName = floor.Level == null ? null : floor.Level.Value;
            string plotValue = floor.PlotValue == null ? null : floor.PlotValue.Value;
            var lacking = new List<string>();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                lacking.Add("type");
            }
            if (string.IsNullOrWhiteSpace(levelName))
            {
                lacking.Add("level");
            }
            if (floor.LevelElevation == null || !floor.LevelElevation.Value.HasValue)
            {
                lacking.Add("levelElevation");
            }
            if (string.IsNullOrWhiteSpace(plotValue))
            {
                lacking.Add("plotValue");
            }
            if (floor.Outline == null || floor.Outline.Value == null || floor.Outline.Value.Loops == null || floor.Outline.Value.Loops.Count == 0)
            {
                lacking.Add("outline");
            }
            if (kind == Ramp && (floor.Corners == null || floor.Corners.Value == null))
            {
                lacking.Add("corners");
            }
            if (kind != Ramp && (floor.Z == null || !floor.Z.Value.HasValue))
            {
                lacking.Add("z");
            }
            if (lacking.Count > 0)
            {
                return "the plan lacks " + string.Join(", ", lacking);
            }
            if (existing.Contains(kind + "|" + plotValue.Trim()))
            {
                return "already placed, a " + kind + " floor of the document has " + settings.PlotParameter + " " + plotValue.Trim();
            }
            // R113. Any type or level problem stops the whole step, a plan that does not fit the document places nothing
            if (KindOf(settings, typeName) != kind)
            {
                stops.Add("plot " + plot + ": " + typeName + " is not a " + kind + " type of floorSettings");
                return null;
            }
            FloorTypeEntry type;
            if (!types.TryGetValue(typeName, out type))
            {
                missingTypes.Add(typeName);
                return null;
            }
            if (levelName != settings.Levels[kind])
            {
                stops.Add("plot " + plot + ": the " + kind + " floor is on " + levelName + ", floorSettings.levels says " + settings.Levels[kind]);
                return null;
            }
            LevelEntry level;
            if (!levels.TryGetValue(levelName, out level))
            {
                stops.Add("the document has no level " + levelName);
                return null;
            }
            double sharedElevation = level.ElevationM + offset[2];
            if (Math.Abs(sharedElevation - floor.LevelElevation.Value.Value) > LevelTolerance)
            {
                stops.Add("level " + levelName + " is at " + Text(sharedElevation) + " m, the plan says " + Text(floor.LevelElevation.Value.Value) + " m");
                return null;
            }
            int arcs;
            List<FloorLoop> loops;
            string bad = BuildLoops(floor.Outline.Value, offset, level.ElevationM, out loops, out arcs);
            if (bad != null)
            {
                return bad;
            }
            var found = new Candidate { Plot = plot, Kind = kind, Floor = floor, Type = type, Level = level, Loops = loops, Arcs = arcs };
            if (kind == Ramp)
            {
                List<double[]> corners = floor.Corners.Value;
                if (corners.Count != 4 || corners.Any(corner => !IsPoint(corner)))
                {
                    return "a ramp needs four corners with x, y and z, the plan has " + corners.Count;
                }
                if (loops.Count != 1 || !CornersOnLoop(corners, floor.Outline.Value.Loops[0].Points))
                {
                    return "the ramp corners are not the vertices of its one outline loop";
                }
                found.CornersInternal = corners.Select(corner => PlacementMath.InternalPoint(corner, offset)).ToList();
                double lowest = found.CornersInternal.Min(corner => corner[2]);
                found.TopInternal = lowest;
                found.HeightOffset = lowest - level.ElevationM;
            }
            else
            {
                found.TopInternal = floor.Z.Value.Value - offset[2];
                found.HeightOffset = found.TopInternal - level.ElevationM;
            }
            if (floor.HeightOffset != null && floor.HeightOffset.Value.HasValue && Math.Abs(floor.HeightOffset.Value.Value - found.HeightOffset) > LevelTolerance)
            {
                return "the plan height offset " + Text(floor.HeightOffset.Value.Value) + " m does not fit its top and level, " + Text(found.HeightOffset) + " m";
            }
            candidate = found;
            return null;
        }

        // R115. Plan loops in shared coordinates turned into internal loops at the height z, flat as Floor.Create wants them.
        // An arc stays an arc, through its start, the middle of its sweep and its end
        public static string BuildLoops(PlanOutline outline, double[] offset, double z, out List<FloorLoop> loops, out int arcs)
        {
            loops = new List<FloorLoop>();
            arcs = 0;
            foreach (PlanLoop loop in outline.Loops)
            {
                if (loop == null || loop.Points == null || loop.Points.Count < 3 || loop.Points.Any(point => point == null || point.Length < 2 || !Finite(point[0]) || !Finite(point[1])))
                {
                    return "an outline loop has fewer than 3 points or a point with no x and y";
                }
                var byEdge = new Dictionary<int, PlanArc>();
                foreach (PlanArc arc in loop.Arcs ?? new List<PlanArc>())
                {
                    if (arc == null || arc.Edge < 0 || arc.Edge >= loop.Points.Count || byEdge.ContainsKey(arc.Edge) || arc.Center == null || arc.Center.Length < 2 || !(arc.Radius > 0))
                    {
                        return "an outline arc has no edge of its loop, no centre or no radius";
                    }
                    byEdge[arc.Edge] = arc;
                }
                var segments = new List<FloorSegment>();
                int count = loop.Points.Count;
                for (int edge = 0; edge < count; edge++)
                {
                    double[] start = Internal(loop.Points[edge], offset, z);
                    double[] end = Internal(loop.Points[(edge + 1) % count], offset, z);
                    if (Math.Sqrt(Math.Pow(end[0] - start[0], 2) + Math.Pow(end[1] - start[1], 2)) < ShortestEdge)
                    {
                        return "an outline edge is shorter than 1 mm";
                    }
                    var segment = new FloorSegment { Start = start, End = end };
                    PlanArc arc;
                    if (byEdge.TryGetValue(edge, out arc))
                    {
                        segment.Mid = ArcMiddle(start, end, Internal(arc.Center, offset, z), arc.Radius, arc.Ccw);
                        arcs++;
                    }
                    segments.Add(segment);
                }
                loops.Add(new FloorLoop { Segments = segments });
            }
            return null;
        }

        // The point halfway along the sweep from start to end round the centre, counter clockwise when ccw
        public static double[] ArcMiddle(double[] start, double[] end, double[] center, double radius, bool ccw)
        {
            double from = Math.Atan2(start[1] - center[1], start[0] - center[0]);
            double to = Math.Atan2(end[1] - center[1], end[0] - center[0]);
            double sweep = ccw ? to - from : from - to;
            while (sweep <= 0)
            {
                sweep += 2 * Math.PI;
            }
            while (sweep > 2 * Math.PI)
            {
                sweep -= 2 * Math.PI;
            }
            double middle = from + (ccw ? sweep : -sweep) / 2;
            return new[] { center[0] + radius * Math.Cos(middle), center[1] + radius * Math.Sin(middle), start[2] };
        }

        private static bool CornersOnLoop(List<double[]> corners, List<double[]> points)
        {
            if (points == null || points.Count != corners.Count)
            {
                return false;
            }
            var used = new HashSet<int>();
            foreach (double[] corner in corners)
            {
                int match = Enumerable.Range(0, points.Count).FirstOrDefault(index => !used.Contains(index) && Distance(corner, points[index]) <= CornerOnVertex + 1e-9);
                if (used.Contains(match) || Distance(corner, points[match]) > CornerOnVertex + 1e-9)
                {
                    return false;
                }
                used.Add(match);
            }
            return true;
        }

        // R113 to R116. One transaction, and any failure rolls all of it back. Returns the plot values written.
        // The work goes in phases so the model regenerates twice and not once per floor: make every floor, then shape the ramps, then read all back
        private static int PlaceAll(PlanFile plan, List<Candidate> candidates, IFloorTarget target, FloorReport report)
        {
            var placed = new List<PlacedFloor>();
            var ids = new List<long>();
            int written = 0;
            string parameter = plan.FloorSettings.PlotParameter;
            try
            {
                target.Begin();
                foreach (Candidate candidate in candidates)
                {
                    long id = target.CreateFloor(candidate.Loops, candidate.Type.Key, candidate.Level.Key);
                    target.SetHeightOffset(id, candidate.HeightOffset);
                    // R114. Without its plot value a later run could not tell the floor is placed and would place it twice
                    ParameterWrite write = target.WriteText(id, parameter, candidate.Floor.PlotValue.Value.Trim());
                    if (write != ParameterWrite.Written)
                    {
                        throw new Placer.PlacerFailure(Label(candidate) + parameter + " was not written, " + write);
                    }
                    written++;
                    ids.Add(id);
                }
                target.Regenerate();
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (candidates[index].Kind == Ramp)
                    {
                        SetCorners(target, ids[index], candidates[index], Label(candidates[index]));
                    }
                }
                target.Regenerate();
                for (int index = 0; index < candidates.Count; index++)
                {
                    string mismatch = Mismatch(target.ReadBack(ids[index]), candidates[index]);
                    if (mismatch != null)
                    {
                        throw new Placer.PlacerFailure(Label(candidates[index]) + "the placed floor does not match the plan, " + mismatch);
                    }
                    placed.Add(Entry(candidates[index], ids[index]));
                }
                CommitResult commit = target.Commit();
                AddWarnings(target, report);
                if (commit == null || !commit.Committed)
                {
                    report.RolledBack = true;
                    report.Failure = "the transaction did not commit, nothing stays: " + (commit == null ? "no result" : commit.Reason);
                    return 0;
                }
            }
            catch (Exception failure)
            {
                target.RollBack();
                AddWarnings(target, report);
                report.RolledBack = true;
                report.Failure = "rolled back, nothing stays: " + failure.Message;
                return 0;
            }
            report.Placed.AddRange(placed);
            return written;
        }

        private static string Label(Candidate candidate)
        {
            return "plot " + candidate.Plot + " " + candidate.Kind + ": ";
        }

        // R114. Each shape corner of the new floor takes the plan corner that sits on it in plan and is lifted from the lowest corner to its own z
        private static void SetCorners(IFloorTarget target, long id, Candidate candidate, string label)
        {
            IList<double[]> vertices = target.ShapeCorners(id);
            if (vertices == null || vertices.Count != candidate.CornersInternal.Count)
            {
                throw new Placer.PlacerFailure(label + "the slab shape editor gives " + (vertices == null ? 0 : vertices.Count) + " corners, the plan has " + candidate.CornersInternal.Count);
            }
            var used = new HashSet<int>();
            for (int index = 0; index < vertices.Count; index++)
            {
                int match = -1;
                for (int corner = 0; corner < candidate.CornersInternal.Count; corner++)
                {
                    if (!used.Contains(corner) && Distance(vertices[index], candidate.CornersInternal[corner]) <= CornerOnVertex + 1e-9)
                    {
                        match = corner;
                        break;
                    }
                }
                if (match < 0)
                {
                    throw new Placer.PlacerFailure(label + "shape corner " + index + " sits on no plan corner");
                }
                used.Add(match);
                target.SetCornerOffset(id, index, candidate.CornersInternal[match][2] - candidate.TopInternal);
            }
        }

        // Null when the read back floor is the planned one, else what differs
        public static string Mismatch(FloorReadBack actual, PlanFloorCheck planned)
        {
            if (actual == null)
            {
                return "nothing was read back";
            }
            if (planned.Corners == null)
            {
                if (!actual.TopMax.HasValue || Math.Abs(actual.TopMax.Value - planned.Top) > TopTolerance)
                {
                    return "the top is at " + (actual.TopMax.HasValue ? Text(actual.TopMax.Value) : "nothing") + " m, the plan says " + Text(planned.Top);
                }
                return null;
            }
            if (actual.Corners == null || actual.Corners.Count != planned.Corners.Count)
            {
                return "the floor has " + (actual.Corners == null ? 0 : actual.Corners.Count) + " shape corners, the plan " + planned.Corners.Count;
            }
            // The highest point of the slab must be the highest planned corner, so a vertex list that agrees with itself cannot hide a wrong slab
            double highest = planned.Corners.Max(corner => corner[2]);
            if (!actual.TopMax.HasValue || Math.Abs(actual.TopMax.Value - highest) > TopTolerance)
            {
                return "the highest point is at " + (actual.TopMax.HasValue ? Text(actual.TopMax.Value) : "nothing") + " m, the highest plan corner at " + Text(highest);
            }
            foreach (double[] corner in planned.Corners)
            {
                double[] near = actual.Corners.OrderBy(found => Distance(found, corner)).First();
                if (Distance(near, corner) > CornerOnVertex + 1e-9 || Math.Abs(near[2] - corner[2]) > TopTolerance)
                {
                    return "a corner is at " + Text(near[2]) + " m, the plan says " + Text(corner[2]);
                }
            }
            return null;
        }

        private static string Mismatch(FloorReadBack actual, Candidate candidate)
        {
            return Mismatch(actual, new PlanFloorCheck { Top = candidate.TopInternal, Corners = candidate.CornersInternal });
        }

        private static void AddWarnings(IFloorTarget target, FloorReport report)
        {
            foreach (string warning in target.Warnings ?? new List<string>())
            {
                report.Issues.Add("Revit warning: " + warning);
            }
        }

        private static PlacedFloor Entry(Candidate candidate, long? id)
        {
            return new PlacedFloor
            {
                Plot = candidate.Plot,
                Kind = candidate.Kind,
                Type = candidate.Type.Name,
                Level = candidate.Level.Name,
                HeightOffset = Rounding.Fixed(candidate.HeightOffset, 4),
                Loops = candidate.Loops.Count,
                Arcs = candidate.Arcs,
                InstanceId = id,
            };
        }

        private static FloorReport Stop(FloorReport report, PlanFile plan, string failure, int filteredOut, int written)
        {
            report.Stopped = true;
            report.Failure = failure;
            report.FloorsAfter = report.FloorsBefore;
            return Count(report, plan, filteredOut, written);
        }

        private static FloorReport Count(FloorReport report, PlanFile plan, int filteredOut, int written)
        {
            report.Counts = new FloorCounts
            {
                PlotsInPlan = plan == null || plan.Plots == null ? 0 : plan.Plots.Count,
                FilteredOut = filteredOut,
                Placed = report.Placed.Count,
                Skipped = report.Skipped.Count,
                PlotValuesWritten = written,
                Issues = report.Issues.Count,
                PlacedByKind = Kinds.ToDictionary(kind => kind, kind => report.Placed.Count(floor => floor.Kind == kind)),
                SkippedByKind = Kinds.ToDictionary(kind => kind, kind => report.Skipped.Count(floor => floor.Kind == kind)),
            };
            return report;
        }

        private static double[] Internal(double[] shared, double[] offset, double z)
        {
            return new[] { shared[0] - offset[0], shared[1] - offset[1], z };
        }

        private static double Distance(double[] a, double[] b)
        {
            return Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));
        }

        private static bool IsPoint(double[] point)
        {
            return point != null && point.Length == 3 && point.All(Finite);
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    // What a placed floor must read back as, in internal metres. Corners null for a flat floor
    public class PlanFloorCheck
    {
        public double Top { get; set; }
        public List<double[]> Corners { get; set; }
    }
}
