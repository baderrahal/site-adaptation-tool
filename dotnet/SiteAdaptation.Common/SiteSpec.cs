using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SiteAdaptation.Common
{
    // R212. What a newSite job sets up in a file made from a template, written by python/site_spec.py from CAD and config/project.json
    public class SiteSpec
    {
        public const string FormatText = "site-adaptation-site";

        public string Format { get; set; }
        public SitePosition ProjectPosition { get; set; }
        public List<string> Levels { get; set; }
        // generic, yard and ramp, as floors.types of config/project.json
        public Dictionary<string, List<string>> FloorTypes { get; set; }
        public string FloorParameter { get; set; }
        public List<string> LinkParameters { get; set; }
        // The shared parameter file the job writes for the new parameters. It must not be there yet
        public string SharedParameterFile { get; set; }
        public string ParameterGroup { get; set; }
        public List<SiteCheckPoint> CheckPoints { get; set; }

        public static SiteSpec Parse(string text)
        {
            return JsonConvert.DeserializeObject<SiteSpec>(text);
        }

        public static SiteSpec Read(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public IEnumerable<string> FloorTypeNames()
        {
            return (FloorTypes ?? new Dictionary<string, List<string>>()).Values.Where(names => names != null).SelectMany(names => names);
        }
    }

    // R211. The shared position of the internal origin in metres and the angle to true north in degrees
    public class SitePosition
    {
        public double? EastM { get; set; }
        public double? NorthM { get; set; }
        public double? ElevationM { get; set; }
        public double? AngleDeg { get; set; }
    }

    // A point given in shared metres, such as a control point of the location plan, whose internal position the job checks
    public class SiteCheckPoint
    {
        public string Name { get; set; }
        public List<double> Shared { get; set; }
    }

    public class CheckPointResult
    {
        public string Name { get; set; }
        public double[] ExpectedInternalM { get; set; }
        public double[] RevitInternalM { get; set; }
        public double GapMm { get; set; }
    }

    // R211. The shared coordinates of a new site file. shared = Rz(angle) * internal + (east, north, elevation), counter clockwise
    public static class SiteCoordinates
    {
        public const double MetresPerFoot = 0.3048;
        public const double ToleranceMm = 1.0;
        public static readonly string[] FloorKinds = { "generic", "yard", "ramp" };
        private const double MaxPlanM = 1e8;
        private const double MaxElevationM = 1e4;

        // Null when the spec can be carried out as it is, else why not
        public static string Check(SiteSpec spec)
        {
            if (spec == null || spec.Format != SiteSpec.FormatText)
            {
                return "the site file is not " + SiteSpec.FormatText;
            }
            SitePosition at = spec.ProjectPosition;
            if (at == null || !Within(at.EastM, MaxPlanM) || !Within(at.NorthM, MaxPlanM) || !Within(at.ElevationM, MaxElevationM) || !Within(at.AngleDeg, 360))
            {
                return "projectPosition needs eastM, northM, elevationM and angleDeg, finite and in range";
            }
            if (!Names(spec.Levels))
            {
                return "levels is a list of different names";
            }
            if (spec.FloorTypes == null || FloorKinds.Any(kind => !spec.FloorTypes.ContainsKey(kind) || !Names(spec.FloorTypes[kind])) || spec.FloorTypes.Keys.Any(kind => !FloorKinds.Contains(kind)))
            {
                return "floorTypes holds generic, yard and ramp, each a list of names";
            }
            if (!Names(spec.FloorTypeNames().ToList()))
            {
                return "a floor type name is given twice";
            }
            if (string.IsNullOrWhiteSpace(spec.FloorParameter) || !Names(spec.LinkParameters) || spec.LinkParameters.Contains(spec.FloorParameter))
            {
                return "floorParameter and linkParameters are different names";
            }
            if (!TestHostGuard.IsPlainFullPath(spec.SharedParameterFile) || !spec.SharedParameterFile.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return "sharedParameterFile is not a plain full path to a .txt file";
            }
            if (string.IsNullOrWhiteSpace(spec.ParameterGroup))
            {
                return "parameterGroup is empty";
            }
            List<SiteCheckPoint> points = spec.CheckPoints ?? new List<SiteCheckPoint>();
            if (points.Any(point => point == null || string.IsNullOrWhiteSpace(point.Name) || point.Shared == null || point.Shared.Count != 3 || !point.Shared.All(value => Within(value, MaxPlanM)))
                || points.Select(point => point.Name).Distinct().Count() != points.Count)
            {
                return "each check point has a name of its own and three finite shared metres";
            }
            return null;
        }

        private static bool Within(double? value, double limit)
        {
            return value.HasValue && value.Value >= -limit && value.Value <= limit;
        }

        private static bool Names(List<string> names)
        {
            return names != null && names.Count > 0 && names.All(name => !string.IsNullOrWhiteSpace(name) && name == name.Trim())
                && names.Distinct(StringComparer.Ordinal).Count() == names.Count;
        }

        // ProjectPosition of Revit: east west, north south and elevation in feet, angle in radians
        public static double[] ProjectPositionFeet(SitePosition at)
        {
            return new[] { at.EastM.Value / MetresPerFoot, at.NorthM.Value / MetresPerFoot, at.ElevationM.Value / MetresPerFoot, at.AngleDeg.Value * Math.PI / 180.0 };
        }

        public static double[] ToShared(SitePosition at, double[] internalM)
        {
            double angle = at.AngleDeg.Value * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return new[]
            {
                cos * internalM[0] - sin * internalM[1] + at.EastM.Value,
                sin * internalM[0] + cos * internalM[1] + at.NorthM.Value,
                internalM[2] + at.ElevationM.Value,
            };
        }

        public static double[] ToInternal(SitePosition at, double[] sharedM)
        {
            double angle = at.AngleDeg.Value * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double east = sharedM[0] - at.EastM.Value;
            double north = sharedM[1] - at.NorthM.Value;
            return new[] { cos * east + sin * north, -sin * east + cos * north, sharedM[2] - at.ElevationM.Value };
        }

        // The survey point marks the shared origin, so its internal position is the internal point of shared 0, 0, 0
        public static double[] SurveyPointInternal(SitePosition at)
        {
            return ToInternal(at, new[] { 0.0, 0.0, 0.0 });
        }

        // R211. Each check point as the spec puts it against what Revit gives for the same shared point, and the gap
        public static List<CheckPointResult> CompareCheckPoints(SiteSpec spec, Func<double[], double[]> revitInternalOfShared)
        {
            var rows = new List<CheckPointResult>();
            foreach (SiteCheckPoint point in spec.CheckPoints ?? new List<SiteCheckPoint>())
            {
                double[] shared = point.Shared.ToArray();
                double[] expected = ToInternal(spec.ProjectPosition, shared);
                double[] revit = revitInternalOfShared(shared);
                rows.Add(new CheckPointResult { Name = point.Name, ExpectedInternalM = expected, RevitInternalM = revit, GapMm = Gap(expected, revit) * 1000.0 });
            }
            return rows;
        }

        public static double Gap(double[] a, double[] b)
        {
            return Math.Sqrt(Enumerable.Range(0, 3).Sum(i => (a[i] - b[i]) * (a[i] - b[i])));
        }
    }
}
