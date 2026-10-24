using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common
{
    // Every decision of Place from plan (R83 to R87). The Revit side only carries out what this class asks for
    public static class Placer
    {
        public const double CoordinateTolerance = 0.001;
        public const double AngleTolerance = 0.001;
        public const double ZTolerance = 0.001;
        public const double ReadBackOrigin = 0.001;
        public const double ReadBackAngle = 0.01;
        public const string FflParameter = "RRE_ZZ_SP_Prototype-FFL";
        public const string PlotParameter = LinkFields.PlotNumber;

        // A failure that rolls the whole real run back
        public sealed class PlacerFailure : Exception
        {
            public PlacerFailure(string message) : base(message)
            {
            }
        }

        private sealed class Candidate
        {
            public PlanPlot Plot;
            public string File;
            public PlacementSteps Steps;
            public double[] Internal;
            public string ExistingType;
        }

        public static PlacerReport Run(PlanFile plan, string planPath, IPlacerTarget target, bool real)
        {
            return Run(plan, planPath, target, real, null);
        }

        // R100. prototypeFile null places every plot, a file name places only the plots of that prototype file
        public static PlacerReport Run(PlanFile plan, string planPath, IPlacerTarget target, bool real, string prototypeFile)
        {
            var report = new PlacerReport
            {
                Mode = real ? "real" : "dryRun",
                PlanFile = planPath,
                Document = target.DocumentTitle,
                PrototypeFilter = prototypeFile,
                Placed = new List<PlacedPlot>(),
                Skipped = new List<SkippedPlot>(),
                Issues = new List<string>(),
            };
            List<Candidate> candidates;
            try
            {
                SurveyReading survey = target.Survey();
                report.Coordinates = CheckCoordinates(plan, survey);
                string stop = CheckFormat(plan) ?? report.Coordinates.Message ?? CheckFilter(plan, prototypeFile);
                if (stop != null)
                {
                    report.Stopped = true;
                    report.Failure = stop;
                    return Count(report, plan);
                }
                candidates = Choose(plan, survey, target, report, prototypeFile);
            }
            catch (Exception failure)
            {
                // Nothing has changed yet, so the run stops and the report still says why
                report.Stopped = true;
                report.Failure = "Stopped before anything was placed: " + failure.Message;
                return Count(report, plan);
            }
            if (!real)
            {
                foreach (Candidate candidate in candidates)
                {
                    report.Placed.Add(Entry(candidate, null, WritableNames(candidate.Plot)));
                    if (candidate.Plot.Parameters.ContainsKey(FflParameter))
                    {
                        report.Issues.Add(FflIssue(candidate.Plot.Plot));
                    }
                }
                return Count(report, plan);
            }
            PlaceAll(plan, candidates, target, report);
            return Count(report, plan);
        }

        // The report of a run that stopped before Run could start, such as a plan file that does not read
        public static PlacerReport StoppedReport(string mode, string planPath, string document, string failure)
        {
            var report = new PlacerReport
            {
                Mode = mode,
                PlanFile = planPath,
                Document = document,
                Stopped = true,
                Failure = failure,
                Placed = new List<PlacedPlot>(),
                Skipped = new List<SkippedPlot>(),
                Issues = new List<string>(),
            };
            return Count(report, null);
        }

        public static string CheckFormat(PlanFile plan)
        {
            if (plan == null || plan.Format != PlanFile.FormatName || plan.FormatVersion != PlanFile.Version)
            {
                return "The plan file is not " + PlanFile.FormatName + " version " + PlanFile.Version + ", nothing was placed.";
            }
            if (plan.SurveyPoint == null || !IsPoint(plan.SurveyPoint.Position) || !IsPoint(plan.SurveyPoint.SharedPosition) || !plan.AngleToTrueNorthDeg.HasValue)
            {
                return "The plan file has no survey point or no angle to true north, nothing was placed.";
            }
            if (!IsFullPath(plan.PrototypeFolder))
            {
                return "The plan file has no full path in prototypeFolder, nothing was placed.";
            }
            return null;
        }

        // R83. Both survey points and the angle must agree within the tolerances, and the angle must be 0 in this round
        public static CoordinateCheck CheckCoordinates(PlanFile plan, SurveyReading survey)
        {
            var check = new CoordinateCheck
            {
                DocumentPosition = survey.Position,
                DocumentSharedPosition = survey.SharedPosition,
                DocumentAngleDeg = survey.AngleToTrueNorthDeg,
                Match = true,
            };
            if (plan == null || plan.SurveyPoint == null || !IsPoint(plan.SurveyPoint.Position) || !IsPoint(plan.SurveyPoint.SharedPosition) || !plan.AngleToTrueNorthDeg.HasValue)
            {
                check.Match = false;
                return check;
            }
            check.PlanPosition = plan.SurveyPoint.Position;
            check.PlanSharedPosition = plan.SurveyPoint.SharedPosition;
            check.PlanAngleDeg = plan.AngleToTrueNorthDeg.Value;
            var problems = new List<string>();
            Compare("survey point position", plan.SurveyPoint.Position, survey.Position, problems);
            Compare("survey point shared position", plan.SurveyPoint.SharedPosition, survey.SharedPosition, problems);
            if (AngleGap(plan.AngleToTrueNorthDeg.Value, survey.AngleToTrueNorthDeg) > AngleTolerance)
            {
                problems.Add("angle to true north: plan " + Text(plan.AngleToTrueNorthDeg.Value) + " deg, document " + Text(survey.AngleToTrueNorthDeg) + " deg");
            }
            else if (AngleGap(survey.AngleToTrueNorthDeg, 0) > AngleTolerance)
            {
                problems.Add("angle to true north is " + Text(survey.AngleToTrueNorthDeg) + " deg, only 0 is handled in this round");
            }
            if (problems.Count > 0)
            {
                check.Match = false;
                check.Message = "Coordinates do not match the plan, nothing was placed and the project coordinates were not changed: " + string.Join(". ", problems) + ".";
            }
            return check;
        }

        private static void Compare(string label, double[] plan, double[] document, IList<string> problems)
        {
            string[] axes = { "X", "Y", "Z" };
            if (!IsPoint(document))
            {
                problems.Add(label + ": the document gives none");
                return;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                if (Math.Abs(plan[axis] - document[axis]) > CoordinateTolerance)
                {
                    problems.Add(label + " " + axes[axis] + ": plan " + Text(plan[axis]) + " m, document " + Text(document[axis]) + " m");
                }
            }
        }

        // R100. A filter is a file name that at least one plot of the plan uses, so a typing error places nothing and says why
        public static string CheckFilter(PlanFile plan, string prototypeFile)
        {
            if (prototypeFile == null)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(prototypeFile) || Path.GetFileName(prototypeFile) != prototypeFile)
            {
                return "The prototype filter must be a file name, not a path or blank, nothing was placed.";
            }
            if (!(plan.Plots ?? new List<PlanPlot>()).Any(plot => InFilter(plot, prototypeFile)))
            {
                return "No plot of the plan uses " + prototypeFile + ", nothing was placed.";
            }
            return null;
        }

        public static bool InFilter(PlanPlot plot, string prototypeFile)
        {
            if (prototypeFile == null)
            {
                return true;
            }
            return plot != null && plot.PrototypeFile != null && plot.PrototypeFile.Value != null
                && string.Equals(plot.PrototypeFile.Value.Trim(), prototypeFile, StringComparison.OrdinalIgnoreCase);
        }

        // R84, R87 and R100. The plots that can be placed, in plan order, each with its steps. Every other plot of the filter is skipped with its reason
        private static List<Candidate> Choose(PlanFile plan, SurveyReading survey, IPlacerTarget target, PlacerReport report, string prototypeFile)
        {
            var placedAlready = new HashSet<string>(target.LinkPlotNumbers().Where(number => number != null).Select(number => number.Trim()), StringComparer.Ordinal);
            Dictionary<string, LinkTypeEntry> types = TypesByFile(target.LinkTypes());
            var seen = new HashSet<string>(StringComparer.Ordinal);
            double[] offset = SharedCoordinates.Offset(survey.Position, survey.SharedPosition);
            var candidates = new List<Candidate>();
            foreach (PlanPlot plot in plan.Plots ?? new List<PlanPlot>())
            {
                string number = plot == null || plot.Plot == null ? null : plot.Plot.Trim();
                // The first entry of a plot number is found over the whole plan, so a filter never changes which entry counts
                bool first = string.IsNullOrEmpty(number) || seen.Add(number);
                if (!InFilter(plot, prototypeFile))
                {
                    report.FilteredOut++;
                    continue;
                }
                if (plot == null)
                {
                    report.Skipped.Add(new SkippedPlot { Reason = "an empty entry in plots" });
                    continue;
                }
                string reason;
                Candidate candidate = null;
                // The second entry of a plot number is skipped whatever happens to the first, as R84 says
                if (!first)
                {
                    reason = "the plan lists this plot number more than once, only its first entry is tried";
                }
                else
                {
                    try
                    {
                        reason = Check(plan, plot, number, placedAlready, types, target, offset, out candidate);
                    }
                    catch (Exception failure)
                    {
                        reason = "the entry could not be read: " + failure.Message;
                    }
                }
                if (reason != null)
                {
                    report.Skipped.Add(new SkippedPlot { Plot = plot.Plot, Reason = reason });
                    continue;
                }
                candidates.Add(candidate);
            }
            return candidates;
        }

        // Null and the candidate when the plot can be placed, else the reason it is skipped
        private static string Check(PlanFile plan, PlanPlot plot, string number, HashSet<string> placedAlready, Dictionary<string, LinkTypeEntry> types,
            IPlacerTarget target, double[] offset, out Candidate candidate)
        {
            candidate = null;
            string reason = Lacking(plot);
            if (reason != null)
            {
                return reason;
            }
            Sourced<string> held;
            if (plot.Parameters == null || !plot.Parameters.TryGetValue(PlotParameter, out held) || held == null || held.Value == null || held.Value.Trim() != number)
            {
                return "the plan has no " + PlotParameter + " equal to the plot, so a later run could not tell it is placed";
            }
            if (placedAlready.Contains(number))
            {
                return "already placed, a link instance of the document has this plot number";
            }
            string name = plot.PrototypeFile.Value;
            if (Path.GetFileName(name) != name)
            {
                return "prototypeFile must be a file name in the prototype folder, not a path";
            }
            string file = Path.Combine(plan.PrototypeFolder, name);
            if (!target.FileExists(file))
            {
                return "prototype file not found: " + file;
            }
            LinkTypeEntry type;
            if (types.TryGetValue(name, out type) && !type.Usable)
            {
                return "the document has a link type for " + name + " that cannot be used, " + type.Why + ". Reload or remove it first";
            }
            double gap = plot.Insert.Value[2] - plot.Levels.Ffl.Value.Value;
            if (Math.Abs(gap) > ZTolerance)
            {
                return "insert Z and FFL differ by " + Text(gap * 1000) + " mm";
            }
            double[] point = PlacementMath.InternalPoint(plot.Insert.Value, offset);
            candidate = new Candidate
            {
                Plot = plot,
                File = file,
                Internal = point,
                ExistingType = type == null ? null : type.Key,
                Steps = PlacementMath.Steps(point, plot.RotationDeg.Value.Value, plot.Mirrored.Value.Value),
            };
            return null;
        }

        // One entry per file name without case. A usable type wins over one that cannot be used
        private static Dictionary<string, LinkTypeEntry> TypesByFile(IEnumerable<LinkTypeEntry> types)
        {
            var found = new Dictionary<string, LinkTypeEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (LinkTypeEntry type in types ?? new List<LinkTypeEntry>())
            {
                LinkTypeEntry before;
                if (type == null || type.FileName == null || (found.TryGetValue(type.FileName, out before) && (before.Usable || !type.Usable)))
                {
                    continue;
                }
                found[type.FileName] = type;
            }
            return found;
        }

        private static string Lacking(PlanPlot plot)
        {
            var lacking = new List<string>();
            if (string.IsNullOrWhiteSpace(plot.Plot))
            {
                lacking.Add("plot");
            }
            if (plot.PrototypeFile == null || string.IsNullOrWhiteSpace(plot.PrototypeFile.Value))
            {
                lacking.Add("prototypeFile");
            }
            if (plot.Insert == null || !IsPoint(plot.Insert.Value))
            {
                lacking.Add("insert");
            }
            if (plot.RotationDeg == null || !plot.RotationDeg.Value.HasValue)
            {
                lacking.Add("rotationDeg");
            }
            if (plot.Mirrored == null || !plot.Mirrored.Value.HasValue)
            {
                lacking.Add("mirrored");
            }
            if (plot.Levels == null || plot.Levels.Ffl == null || !plot.Levels.Ffl.Value.HasValue)
            {
                lacking.Add("levels.ffl");
            }
            return lacking.Count == 0 ? null : "the plan lacks " + string.Join(", ", lacking);
        }

        // R84 to R86. One transaction, and any failure rolls all of it back
        private static void PlaceAll(PlanFile plan, List<Candidate> candidates, IPlacerTarget target, PlacerReport report)
        {
            var placed = new List<PlacedPlot>();
            int written = 0;
            try
            {
                target.Begin();
                var loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Candidate candidate in candidates)
                {
                    string fileName = Path.GetFileName(candidate.File);
                    string key = candidate.ExistingType;
                    if (key == null && !loaded.TryGetValue(fileName, out key))
                    {
                        key = target.LoadLinkType(candidate.File);
                        if (key == null)
                        {
                            throw new PlacerFailure("plot " + candidate.Plot.Plot + ": the link " + candidate.File + " did not load");
                        }
                        loaded[fileName] = key;
                    }
                    long id = target.Place(key, candidate.Steps);
                    string mismatch = PlacementMath.Mismatch(target.ReadBack(id), candidate.Internal, candidate.Plot.RotationDeg.Value.Value, candidate.Plot.Mirrored.Value.Value);
                    if (mismatch != null)
                    {
                        throw new PlacerFailure("plot " + candidate.Plot.Plot + ": the placed link does not match the plan, " + mismatch);
                    }
                    var names = new List<string>();
                    foreach (KeyValuePair<string, Sourced<string>> parameter in candidate.Plot.Parameters ?? new Dictionary<string, Sourced<string>>())
                    {
                        string issue = WriteOne(target, id, candidate.Plot.Plot, parameter.Key, parameter.Value);
                        if (issue == null)
                        {
                            names.Add(parameter.Key);
                            written++;
                        }
                        else if (parameter.Key == PlotParameter)
                        {
                            // Without its plot number a later run could not tell the link is placed and would place it twice
                            throw new PlacerFailure(issue);
                        }
                        else
                        {
                            report.Issues.Add(issue);
                        }
                    }
                    placed.Add(Entry(candidate, id, names));
                }
                CommitResult commit = target.Commit();
                AddWarnings(target, report);
                if (commit == null || !commit.Committed)
                {
                    report.RolledBack = true;
                    report.Failure = "the transaction did not commit, nothing stays: " + (commit == null ? "no result" : commit.Reason);
                    return;
                }
            }
            catch (Exception failure)
            {
                target.RollBack();
                AddWarnings(target, report);
                report.RolledBack = true;
                report.Failure = "rolled back, nothing stays: " + failure.Message;
                return;
            }
            report.Placed.AddRange(placed);
            report.ParametersWritten = written;
        }

        private static void AddWarnings(IPlacerTarget target, PlacerReport report)
        {
            foreach (string warning in target.Warnings ?? new List<string>())
            {
                report.Issues.Add("Revit warning: " + warning);
            }
        }

        // R85. Null when the value was written, else the issue line
        private static string WriteOne(IPlacerTarget target, long id, string plot, string name, Sourced<string> value)
        {
            if (name == FflParameter)
            {
                return FflIssue(plot);
            }
            if (value == null || value.Value == null)
            {
                return "plot " + plot + ": " + name + " has no value in the plan";
            }
            ParameterWrite result = target.Write(id, name, value.Value);
            switch (result)
            {
                case ParameterWrite.Written:
                    return null;
                case ParameterWrite.Missing:
                    return "plot " + plot + ": the link instance has no parameter " + name;
                case ParameterWrite.ReadOnly:
                    return "plot " + plot + ": " + name + " is read only";
                case ParameterWrite.NotText:
                    return "plot " + plot + ": " + name + " does not hold text";
                default:
                    return "plot " + plot + ": " + name + " was not written";
            }
        }

        private static string FflIssue(string plot)
        {
            return "plot " + plot + ": " + FflParameter + " is in the plan and is never written";
        }

        private static List<string> WritableNames(PlanPlot plot)
        {
            return (plot.Parameters ?? new Dictionary<string, Sourced<string>>()).Keys.Where(name => name != FflParameter).ToList();
        }

        private static PlacedPlot Entry(Candidate candidate, long? id, List<string> parameters)
        {
            return new PlacedPlot
            {
                Plot = candidate.Plot.Plot,
                File = Path.GetFileName(candidate.File),
                RotationDeg = candidate.Plot.RotationDeg.Value.Value,
                Mirrored = candidate.Plot.Mirrored.Value.Value,
                InstanceId = id,
                Parameters = parameters,
            };
        }

        private static PlacerReport Count(PlacerReport report, PlanFile plan)
        {
            report.Counts = new PlacerCounts
            {
                PlotsInPlan = plan == null || plan.Plots == null ? 0 : plan.Plots.Count,
                FilteredOut = report.FilteredOut,
                Placed = report.Placed.Count,
                Skipped = report.Skipped.Count,
                ParametersWritten = report.ParametersWritten,
                Issues = report.Issues.Count,
            };
            return report;
        }

        // A drive and a backslash, or a server share. IsPathRooted alone takes C:Prototypes and \Prototypes, which are not full
        public static bool IsFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            bool drive = path.Length > 2 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
            return drive || path.StartsWith(@"\\", StringComparison.Ordinal);
        }

        private static bool IsPoint(double[] point)
        {
            return point != null && point.Length == 3;
        }

        public static double AngleGap(double a, double b)
        {
            double gap = Math.Abs(a - b) % 360.0;
            return gap > 180.0 ? 360.0 - gap : gap;
        }

        private static string Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    public static class PlacementMath
    {
        // R84. Shared coordinates minus the survey point offset of the open document
        public static double[] InternalPoint(double[] shared, double[] offset)
        {
            return new[] { shared[0] - offset[0], shared[1] - offset[1], shared[2] - offset[2] };
        }

        // The mirror plane holds the turned X axis and the vertical, so its normal is the turned Y axis.
        // Mirroring across it flips Y and keeps X, which is what a mirrored link of the past job holds
        public static PlacementSteps Steps(double[] internalPoint, double rotationDeg, bool mirrored)
        {
            double turn = UnitMath.DegreesToRadians(rotationDeg);
            return new PlacementSteps
            {
                Move = internalPoint,
                RotationRad = turn,
                MirrorNormal = mirrored ? new[] { -Math.Sin(turn), Math.Cos(turn), 0.0 } : null,
            };
        }

        // Null when the read back transform is the planned one, else what differs
        public static string Mismatch(PlacedTransform actual, double[] internalPoint, double rotationDeg, bool mirrored)
        {
            if (actual == null || actual.Origin == null || actual.AxisX == null || actual.AxisY == null || actual.AxisZ == null)
            {
                return "no transform was read back";
            }
            var problems = new List<string>();
            double gap = Math.Sqrt(Enumerable.Range(0, 3).Sum(axis => Math.Pow(actual.Origin[axis] - internalPoint[axis], 2)));
            if (gap > Placer.ReadBackOrigin)
            {
                problems.Add("origin is " + (gap * 1000).ToString("0.###", CultureInfo.InvariantCulture) + " mm off");
            }
            LinkTransform found = LinkMath.Describe(actual.Origin, actual.AxisX, actual.AxisY, actual.AxisZ);
            if (Placer.AngleGap(found.RotationDeg, rotationDeg) > Placer.ReadBackAngle)
            {
                problems.Add("rotation is " + found.RotationDeg.ToString("0.###", CultureInfo.InvariantCulture) + " deg, the plan says " + rotationDeg.ToString("0.###", CultureInfo.InvariantCulture));
            }
            if (found.Mirrored != mirrored)
            {
                problems.Add("mirrored is " + found.Mirrored + ", the plan says " + mirrored);
            }
            if (Math.Abs(actual.AxisZ[2] - 1.0) > 1e-6)
            {
                problems.Add("the Z axis is not up");
            }
            return problems.Count == 0 ? null : string.Join(", ", problems);
        }
    }
}
