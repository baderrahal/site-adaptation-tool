using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // What Apply sets, each part only where the document differs from the target, so a second run sets nothing (03a Second run)
    public sealed class SetUpChanges
    {
        public bool Units { get; set; }
        public bool Position { get; set; }
        public bool SurveyPoint { get; set; }
        public bool BasePoint { get; set; }
        public bool LevelBase { get; set; }

        // R455. The named position and the site location of a CAD site
        public bool Location { get; set; }

        // Each level of the target whose elevation is set, made first when the document lacks its name
        public List<PositionLevel> Levels { get; set; } = new List<PositionLevel>();
        public List<string> LevelsToMake { get; set; } = new List<string>();
        public List<string> FloorTypes { get; set; } = new List<string>();
        public List<string> WallTypes { get; set; } = new List<string>();

        // Each parameter bound by its GUID on the categories of the sheet, also one already there on fewer of them
        public List<ParameterSpec> Parameters { get; set; } = new List<ParameterSpec>();

        public bool Any
        {
            get { return Units || Location || Position || SurveyPoint || BasePoint || LevelBase || Levels.Count > 0 || FloorTypes.Count > 0 || WallTypes.Count > 0 || Parameters.Count > 0; }
        }
    }

    // R373, R374, docs/design/03a-buttons-shared-and-1.md 1 Set up site and 07-mock.md 7.3. The judgement of button 1 with no Revit
    // type: the badge and its signs, the stops, the rows of the dialog and what Apply sets
    public static class SetUpPlanner
    {
        // R331, the read back and every difference within half a mm, the angle within a millionth of a degree
        // R405, Q103. designValues.samePoint of the config
        public static double SameMm => DesignValues.Current.SameMm;
        public static double SameDeg => DesignValues.Current.SameDeg;

        // 03a, the badge: the Settings entity, a shared parameter bound with a GUID or a name of the sheet, a project position other
        // than zero or a survey point or project base point off the internal origin by more than half a mm. Types and levels are no
        // sign, the default metric template holds Generic 150mm, Level 1 and Level 2
        public static List<string> Signs(SiteState now, SetUpTarget target)
        {
            var signs = new List<string>();
            if (now.SettingsEntity)
            {
                signs.Add(SetUpTexts.SignSettings);
            }
            var names = new HashSet<string>(ToolNames(target), StringComparer.Ordinal);
            var guids = new HashSet<string>(target.Parameters.Parameters.Where(item => item.Guid != null).Select(item => item.Guid), StringComparer.OrdinalIgnoreCase);
            List<string> found = now.SharedParameters.Where(item => item.Bound && (names.Contains(item.Name) || (item.Guid != null && guids.Contains(item.Guid))))
                .Select(item => item.Name).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
            if (found.Count > 0)
            {
                signs.Add(SetUpTexts.SignParameters(found));
            }
            if (!Zero(now.Position))
            {
                signs.Add(SetUpTexts.SignPosition);
            }
            if (Length(now.Survey.InternalM) * 1000.0 > SameMm)
            {
                signs.Add(SetUpTexts.SignSurvey);
            }
            if (Length(now.ProjectBase.InternalM) * 1000.0 > SameMm)
            {
                signs.Add(SetUpTexts.SignBase);
            }
            return signs;
        }

        // 03a Shared parameters and Stop. A parameter of a name of the sheet with another GUID, listed with the GUID found and the GUID of
        // the sheet, so a second parameter of one name is never added, a parameter of the sheet with no GUID or no group (R464), the floor
        // parameter given to another button than Floors (R465), and a type with nothing to copy it from. Plot with no GUID waits on Q156
        // and is no stop
        public static List<string> Stops(SiteState now, SetUpTarget target)
        {
            var stops = new List<string>();
            foreach (ParameterSpec item in target.Parameters.Parameters)
            {
                if (item.Guid == null)
                {
                    if (item.Name != target.PlotParameter)
                    {
                        stops.Add(SetUpTexts.NoGuid(item.Name));
                    }
                    continue;
                }
                foreach (ParameterState other in now.SharedParameters.Where(other => other.Name == item.Name && !string.Equals(other.Guid, item.Guid, StringComparison.OrdinalIgnoreCase)))
                {
                    stops.Add(SetUpTexts.OtherGuid(item.Name, other.Guid, item.Guid));
                }
                if (item.Group == null)
                {
                    stops.Add(SetUpTexts.NoGroup(item.Name));
                }
                if (item.Name == target.PlotParameter && item.FilledBy != ParameterSheet.FloorsButton)
                {
                    stops.Add(SetUpTexts.FloorsOwn(item.Name, ParameterSheet.FloorsButton));
                }
            }
            SetUpChanges changes = Changes(now, target);
            if (changes.FloorTypes.Count > 0 && now.FloorTypes.Count == 0)
            {
                stops.Add(SetUpTexts.NoFloorType);
            }
            if (changes.WallTypes.Count > 0 && now.WallTypes.Count == 0)
            {
                stops.Add(SetUpTexts.NoWallType);
            }
            // R454. A conflict of the coordinate systems the inputs state, or one unlike coordinates.expectedEpsg
            if (target.Cad != null)
            {
                stops.AddRange(target.Cad.Systems.Stops);
            }
            return stops;
        }

        // Q156. Plot is bound once it has a GUID, until then it is listed and never a stop
        public static List<string> Waiting(SetUpTarget target)
        {
            bool plotHasGuid = target.Parameters.Parameters.Any(item => item.Name == target.PlotParameter && item.Guid != null);
            List<string> waiting = target.PlotParameter == null || plotHasGuid ? new List<string>() : new List<string> { SetUpTexts.PlotWaits(target.PlotParameter) };
            // R454. The coordinate system each CAD input states, a file that states none listed and never a stop (Q325)
            if (target.Cad != null)
            {
                waiting.AddRange(target.Cad.Systems.Lines);
            }
            return waiting;
        }

        public static SetUpChanges Changes(SiteState now, SetUpTarget target)
        {
            bool position = !SamePosition(now.Position, target.Position);
            // R455, LB1F and LB1G. SetProjectPosition moves the survey point internal to (-E,-N) on a new project (survey probe of
            // FIX-1NOV-B), so a point read at its target before the writes is off after them. A position change moves both points,
            // each by the gap Revit reads at that time
            var changes = new SetUpChanges
            {
                Units = !SameUnits(now.Units, target.Units),
                Location = target.Cad != null && !SameLocation(now, target.Cad),
                Position = position,
                SurveyPoint = position || Gap(now.Survey.InternalM, target.SurveyInternal) > SameMm,
                BasePoint = position || Gap(now.ProjectBase.InternalM, target.BaseInternal) > SameMm,
                // A level made takes the default level type, whose base is not known before, so its type is set too
                LevelBase = target.Levels.Any(level => now.Levels.FirstOrDefault(have => have.Name == level.Name)?.Base != target.LevelBase),
            };
            foreach (PositionLevel level in target.Levels)
            {
                LevelState have = now.Levels.FirstOrDefault(item => item.Name == level.Name);
                if (have == null)
                {
                    changes.LevelsToMake.Add(level.Name);
                }
                if (have == null || have.Base != target.LevelBase || Math.Abs(have.ElevationM - level.ElevationM) * 1000.0 > SameMm)
                {
                    changes.Levels.Add(level);
                }
            }
            changes.FloorTypes = target.FloorTypes.Where(name => !now.FloorTypes.Contains(name)).ToList();
            changes.WallTypes = target.WallTypes.Where(name => !now.WallTypes.Contains(name)).ToList();
            foreach (ParameterSpec item in target.Parameters.Parameters.Where(item => item.Guid != null))
            {
                ParameterState have = now.SharedParameters.FirstOrDefault(other => string.Equals(other.Guid, item.Guid, StringComparison.OrdinalIgnoreCase));
                // R464. A binding under another group than the sheet names is put again under that of the sheet
                bool otherGroup = have?.Group != null && item.Group != null && !string.Equals(have.Group, item.Group, StringComparison.OrdinalIgnoreCase);
                if (have == null || otherGroup || item.Categories.Any(category => !have.Categories.Contains(category)))
                {
                    changes.Parameters.Add(item);
                }
            }
            return changes;
        }

        // The dialog of 7.3. revitShared gives the shared point Revit gives for an internal point, by the transform made in memory of
        // the target position in a dry run, by the document after Apply, and null before the dry run, which leaves the Revit column empty
        public static SetUpSiteView View(SiteState now, SetUpTarget target, Func<double[], double[]> revitShared)
        {
            SetUpChanges changes = Changes(now, target);
            var view = new SetUpSiteView
            {
                Signs = Signs(now, target),
                Stops = Stops(now, target),
                Waiting = Waiting(target),
                RampSlopeChoices = target.RampSlopeChoices.ToList(),
                RampSlope = target.RampSlope,
                ParameterSheetName = target.Parameters.Sheet,
                ParameterCount = target.Parameters.Parameters.Count,
                PassLimitMm = target.PassLimitMm,
                GapDecimals = target.GapDecimals,
                CheckPoints = CheckPoints(target, revitShared),
                DryRunDone = revitShared != null,
                NothingDiffers = !changes.Any,
            };
            view.ExistingSite = view.Signs.Count > 0;
            view.Passing = view.CheckPoints.Count(row => row.Pass);
            view.Now = NowRows(now, target);
            view.WillSet = WillSetRows(now, target, changes);
            return view;
        }

        // 3.1 Checks. The CAD point is the shared point of the position file, taken to its internal point by the SiteCoordinates math
        // of R211, and the Revit point the shared point Revit gives for that internal point. The extreme plots of the whole baseplan and
        // the three control points of the location plan join at 5c, with the 0.1 mm limit of Q142 under a key of its own (W2)
        public static List<CheckPointRow> CheckPoints(SetUpTarget target, Func<double[], double[]> revitShared)
        {
            var rows = new List<CheckPointRow>();
            foreach (KeyValuePair<string, double[]> point in new[] { new KeyValuePair<string, double[]>(SetUpTexts.SurveyPoint, target.SurveyShared), new KeyValuePair<string, double[]>(SetUpTexts.ProjectBasePoint, target.BaseShared) })
            {
                var row = new CheckPointRow { Point = point.Key, Cad = SetUpTexts.Coordinate(point.Value) };
                if (revitShared != null)
                {
                    double[] revit = revitShared(SiteCoordinates.ToInternal(target.Position, point.Value));
                    row.Revit = SetUpTexts.Coordinate(revit);
                    row.DiffMm = Rounding.Fixed(SiteCoordinates.Gap(point.Value, revit) * 1000.0, target.GapDecimals);
                    row.Pass = row.DiffMm.Value <= target.PassLimitMm;
                }
                rows.Add(row);
            }
            // R456. The control points of the CAD site at coordinates.controlPointLimitMm
            rows.AddRange(target.ControlPoints.Select(point => ControlPoints.Check(point, target.Position, revitShared, target.Cad.ControlPointLimitMm, target.GapDecimals)));
            return rows;
        }

        // R331. What of the document differs from the target after Apply, null when every value is within half a mm
        public static string ReadBack(SiteState now, SetUpTarget target)
        {
            var differs = new List<string>();
            if (!SamePosition(now.Position, target.Position))
            {
                differs.Add("project position");
            }
            if (target.Cad != null && !SameLocation(now, target.Cad))
            {
                differs.Add("site location");
            }
            Check(differs, "survey point internal", now.Survey.InternalM, target.SurveyInternal);
            Check(differs, "survey point shared", now.Survey.SharedM, target.SurveyShared);
            Check(differs, "project base point internal", now.ProjectBase.InternalM, target.BaseInternal);
            Check(differs, "project base point shared", now.ProjectBase.SharedM, target.BaseShared);
            foreach (PositionLevel level in target.Levels)
            {
                LevelState have = now.Levels.FirstOrDefault(item => item.Name == level.Name);
                if (have == null || have.Base != target.LevelBase || Math.Abs(have.ElevationM - level.ElevationM) * 1000.0 > SameMm || Math.Abs(have.ProjectElevationM - level.ProjectElevationM) * 1000.0 > SameMm)
                {
                    differs.Add(level.Name);
                }
            }
            if (!SameUnits(now.Units, target.Units))
            {
                differs.Add("units");
            }
            differs.AddRange(target.FloorTypes.Where(name => !now.FloorTypes.Contains(name)).Select(name => "floor type " + name));
            differs.AddRange(target.WallTypes.Where(name => !now.WallTypes.Contains(name)).Select(name => "wall type " + name));
            differs.AddRange(Changes(now, target).Parameters.Select(item => "parameter " + item.Name));
            // A name read back other than the one written, such as a letter the parameter file did not carry
            differs.AddRange(target.Parameters.Parameters.Where(item => item.Guid != null && now.SharedParameters.Any(other => string.Equals(other.Guid, item.Guid, StringComparison.OrdinalIgnoreCase) && other.Name != item.Name))
                .Select(item => "parameter name " + item.Name));
            return differs.Count == 0 ? null : string.Join(", ", differs);
        }

        private static void Check(List<string> differs, string what, double[] have, double[] want)
        {
            if (Gap(have, want) > SameMm)
            {
                differs.Add(what);
            }
        }

        private static List<SetUpRow> NowRows(SiteState now, SetUpTarget target)
        {
            List<string> tool = ToolNames(target);
            List<string> parameters = now.SharedParameters.Select(item => item.Name).Distinct(StringComparer.Ordinal).ToList();
            return new List<SetUpRow>
            {
                new SetUpRow { Label = SetUpTexts.SurveyPoint, Value = SetUpTexts.BasePoint(now.Survey) },
                new SetUpRow { Label = SetUpTexts.ProjectBasePoint, Value = SetUpTexts.BasePoint(now.ProjectBase) },
                new SetUpRow { Label = SetUpTexts.Levels, Value = SetUpTexts.LevelsNow(now.Levels) },
                new SetUpRow { Label = SetUpTexts.FloorAndWallTypes, Value = SetUpTexts.TypesNow(now.FloorTypes, target.FloorTypes, now.WallTypes, target.WallTypes) },
                new SetUpRow { Label = SetUpTexts.SharedParameters, Value = SetUpTexts.Of(parameters, parameters.Where(name => tool.Contains(name)).ToList()) },
                new SetUpRow { Label = SetUpTexts.Units, Value = SetUpTexts.UnitsText(now.Units) },
            };
        }

        private static List<SetUpRow> WillSetRows(SiteState now, SetUpTarget target, SetUpChanges changes)
        {
            return new List<SetUpRow>
            {
                new SetUpRow { Label = SetUpTexts.CoordinateSystem, Value = SetUpTexts.CoordinateSystemText(target), Differs = changes.Location || changes.Position || changes.BasePoint },
                new SetUpRow { Label = SetUpTexts.SurveyPoint, Value = SetUpTexts.Coordinate(target.SurveyShared), Differs = changes.SurveyPoint || Gap(now.Survey.SharedM, target.SurveyShared) > SameMm },
                new SetUpRow { Label = SetUpTexts.AngleToTrueNorth, Value = SetUpTexts.Number(target.Position.AngleDeg.Value, 6), Differs = Math.Abs(now.Position.AngleDeg.Value - target.Position.AngleDeg.Value) > SameDeg },
                new SetUpRow { Label = SetUpTexts.Levels, Value = SetUpTexts.LevelsTarget(target), Differs = changes.LevelBase || changes.Levels.Count > 0 },
                new SetUpRow { Label = SetUpTexts.FloorTypes, Value = SetUpTexts.FloorTypesRow, Differs = changes.FloorTypes.Count > 0 },
                new SetUpRow { Label = SetUpTexts.RampSlope, Value = RampSlopes.Text(target.RampSlope) + "%", Differs = changes.FloorTypes.Contains(target.RampType) },
                new SetUpRow { Label = SetUpTexts.WallTypes, Value = string.Join(", ", target.WallTypes), Differs = changes.WallTypes.Count > 0 },
                new SetUpRow { Label = SetUpTexts.SharedParameters, Value = SetUpTexts.FromSheet(target.Parameters.Source), Differs = changes.Parameters.Count > 0 },
                new SetUpRow { Label = SetUpTexts.Units, Value = SetUpTexts.UnitsText(target.Units), Differs = changes.Units },
            };
        }

        private static List<string> ToolNames(SetUpTarget target)
        {
            List<string> names = target.Parameters.Parameters.Select(item => item.Name).ToList();
            if (target.PlotParameter != null && !names.Contains(target.PlotParameter))
            {
                names.Add(target.PlotParameter);
            }
            return names;
        }

        private static bool SameUnits(UnitState have, UnitState want)
        {
            return have.Length == want.Length && have.Area == want.Area && have.Angle == want.Angle && have.Slope == want.Slope
                && have.LengthAccuracy.HasValue && want.LengthAccuracy.HasValue && Math.Abs(have.LengthAccuracy.Value - want.LengthAccuracy.Value) <= want.LengthAccuracy.Value * 1e-9;
        }

        private static bool SamePosition(SitePosition have, SitePosition want)
        {
            return Math.Abs(have.EastM.Value - want.EastM.Value) * 1000.0 <= SameMm && Math.Abs(have.NorthM.Value - want.NorthM.Value) * 1000.0 <= SameMm
                && Math.Abs(have.ElevationM.Value - want.ElevationM.Value) * 1000.0 <= SameMm && Math.Abs(Turn(have.AngleDeg.Value - want.AngleDeg.Value)) <= SameDeg;
        }

        // R455. The named position active and the site location within SameDeg, the place name only when config gives one
        private static bool SameLocation(SiteState now, CadSite cad)
        {
            return now.PositionName == cad.PositionName && now.LatitudeDeg.HasValue && now.LongitudeDeg.HasValue && Math.Abs(now.LatitudeDeg.Value - cad.LatitudeDeg) <= SameDeg
                && Math.Abs(Turn(now.LongitudeDeg.Value - cad.LongitudeDeg)) <= SameDeg && (cad.PlaceName == null || now.PlaceName == cad.PlaceName);
        }

        private static bool Zero(SitePosition at)
        {
            return SamePosition(at, new SitePosition { EastM = 0, NorthM = 0, ElevationM = 0, AngleDeg = 0 });
        }

        // An angle difference brought into -180 to 180, so 359.9999999 and 0 are the same angle
        private static double Turn(double degrees)
        {
            double turned = degrees % 360.0;
            return turned > 180 ? turned - 360 : turned < -180 ? turned + 360 : turned;
        }

        // The gap in mm
        private static double Gap(double[] a, double[] b)
        {
            return SiteCoordinates.Gap(a, b) * 1000.0;
        }

        private static double Length(double[] point)
        {
            return SiteCoordinates.Gap(point, new[] { 0.0, 0.0, 0.0 });
        }

        internal static string Invariant(double value, int decimals)
        {
            return Rounding.Fixed(value, decimals).ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
