using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.Floors
{
    // R570. The block floors.roadTopography of config, each key refused by its name when missing, as DesignValues refuses its own. The snap
    // of a level is siteBuild.snapM, the one a front corner took its road level by (R491)
    public sealed class RoadTopographySettings
    {
        public const string Block = "floors.roadTopography.";

        public string TypeName { get; set; }
        public string Surface { get; set; }
        public double MarginM { get; set; }
        public double LimitMm { get; set; }
        public double MinCoverShare { get; set; }
        public double PointGapM { get; set; }
        public int MaxPoints { get; set; }
        public double SnapM { get; set; }

        public static RoadTopographySettings Of(JObject config)
        {
            JToken block = config?["floors"]?["roadTopography"];
            return new RoadTopographySettings
            {
                TypeName = Text(block, "typeName"), Surface = Text(block, "surface"), MarginM = Number(block, "marginM"), LimitMm = Number(block, "limitMm"),
                MinCoverShare = Number(block, "minCoverShare"), PointGapM = Number(block, "pointGapM"), MaxPoints = (int)Number(block, "maxPoints"),
                SnapM = (double?)config?["siteBuild"]?["snapM"] ?? throw new InvalidDataException("config names no siteBuild.snapM"),
            };
        }

        private static string Text(JToken block, string key)
        {
            string value = block?[key]?.Type == JTokenType.String ? (string)block[key] : null;
            return string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException("config names no " + Block + key) : value;
        }

        private static double Number(JToken block, string key)
        {
            JTokenType? type = block?[key]?.Type;
            return type == JTokenType.Float || type == JTokenType.Integer ? (double)block[key] : throw new InvalidDataException("config names no " + Block + key);
        }
    }

    // R572. What FloorLoad read for the toposolid of one run
    public sealed class RoadSurfaceRead
    {
        public RoadTopographySettings Settings { get; set; }
        public LandXmlSurface Surface { get; set; }
        public InputMark Mark { get; set; }

        private TinSurface tin;

        // Built once, a press plans twice
        public TinSurface Tin => tin ?? (tin = TinSurface.Of(Surface));
    }

    // R571. The road edge of one ramp in shared coordinates, its two front corners with their z
    public sealed class RampEdge
    {
        public string Plot { get; set; }
        public double[] Start { get; set; }
        public double[] End { get; set; }

        // R573. A ramp this press leaves as it stands, changed by hand or its outline changed
        public bool Skipped { get; set; }
    }

    // R573. One point of a ramp road edge, levels in shared metres
    public sealed class RoadEdgePoint
    {
        public string Plot { get; set; }
        public string At { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double RampLevel { get; set; }
        public double? SurfaceLevel { get; set; }
        public double? ModelLevel { get; set; }

        // R573. Of a ramp this press leaves as it stands, whose level the plan does not give, so it counts in no largest and no limit
        public bool Skipped { get; set; }

        public double? DifferenceMm => SurfaceLevel.HasValue ? (RampLevel - SurfaceLevel.Value) * 1000 : (double?)null;
        public double? ModelDifferenceMm => ModelLevel.HasValue ? (RampLevel - ModelLevel.Value) * 1000 : (double?)null;
    }

    // R571. In shared coordinates, the boundary with the level of the surface at each corner. Refused says why there is none
    public sealed class ToposolidPlan
    {
        public List<double[]> Boundary { get; set; } = new List<double[]>();
        public List<double[]> Points { get; set; } = new List<double[]>();
        public List<RoadEdgePoint> Edges { get; set; } = new List<RoadEdgePoint>();
        public int VerticesInside { get; set; }
        public int CornersOffSurface { get; set; }
        public int Thinned { get; set; }
        public string Refused { get; set; }
    }

    // R570 to R573. The toposolid of one NBH decided with no Revit type: the boundary, the points, which toposolid is its own, what it says
    // and the report
    public static class RoadTopography
    {
        public const string Kind = PlacedKinds.Toposolid;
        public const string RecordFormat = "site-adaptation-toposolid";
        public const string Line = "Road topography: ";
        public const string Make = "make";
        public const string Replace = "replace";
        public const string Keep = "keep";
        public const string TickOffLine = Line + "the tick is off, the toposolid of this NBH stands as it was made and this press did not check it";
        public const string PlotListLine = Line + "left as it is, a plot list does not cover the NBH. Press with the NBH as the scope to make or replace the toposolid";

        // A triangle this flat in plan, a side of the solid, gives a point no level
        private const double NoPlanArea = 1e-9;
        private const string KeyValue = "key";

        public static string RecordName(string nbh)
        {
            return "toposolid_" + nbh + ".json";
        }

        // R571. The boundary is the convex hull of the plots grown by the margin, CHOSEN by the main session. A boundary corner off the surface
        // takes the level of the nearest vertex inside, as the road surface has holes under the plots and a hull corner often falls in one.
        // The samples of each road edge join the points, so Revit, which triangulates the points its own way, holds the level there
        public static ToposolidPlan Plan(RoadSurfaceRead read, IEnumerable<double[]> outlines, IEnumerable<RampEdge> edges)
        {
            var plan = new ToposolidPlan();
            LandXmlSurface surface = read.Surface;
            RoadTopographySettings settings = read.Settings;
            TinSurface tin = read.Tin;
            string name = settings.Surface;
            List<RampEdge> ramps = edges.ToList();
            plan.Edges = ramps.SelectMany(edge => Samples(edge, tin, settings.SnapM)).ToList();
            List<double[]> hull = Hull(outlines);
            if (hull.Count < 3)
            {
                plan.Refused = "the plots in scope give no area to make it on";
                return plan;
            }
            plan.Boundary = Grow(hull, settings.MarginM, settings.PointGapM);
            List<double[]> inside = Vertices(surface, plan.Boundary, settings.PointGapM);
            plan.VerticesInside = inside.Count;
            int off = plan.Edges.Count(edge => !edge.SurfaceLevel.HasValue);
            if (plan.Edges.Count > 0 && (plan.Edges.Count - off) / (double)plan.Edges.Count < settings.MinCoverShare)
            {
                plan.Refused = "the surface " + name + " does not cover the scope, " + off + " of " + plan.Edges.Count + " ramp road edge points are off it";
                return plan;
            }
            // A small scope can stand on one large triangle with no vertex in it, so what is known of the surface here counts and not the vertices alone
            List<double[]> onSurface = plan.Boundary.Where(corner => tin.SnapZ(corner[0], corner[1], settings.SnapM).HasValue).Select(corner => new[] { corner[0], corner[1], tin.SnapZ(corner[0], corner[1], settings.SnapM).Value })
                .Concat(plan.Edges.Where(edge => edge.SurfaceLevel.HasValue).Select(edge => new[] { edge.X, edge.Y, edge.SurfaceLevel.Value })).Concat(inside).ToList();
            if (onSurface.Count == 0)
            {
                plan.Refused = "the surface " + name + " does not cover the scope, none of it lies inside the boundary of the plots";
                return plan;
            }
            foreach (double[] corner in plan.Boundary)
            {
                double? level = tin.SnapZ(corner[0], corner[1], settings.SnapM);
                if (!level.HasValue)
                {
                    plan.CornersOffSurface++;
                    level = Nearest(onSurface, corner)[2];
                }
                corner[2] = level.Value;
            }
            var seen = new PointSpacing(settings.PointGapM);
            var kept = new List<double[]>();
            foreach (double[] corner in plan.Boundary)
            {
                seen.Add(corner);
                kept.Add(corner);
            }
            foreach (RoadEdgePoint edge in plan.Edges.Where(edge => edge.SurfaceLevel.HasValue && Inside(plan.Boundary, edge.X, edge.Y, settings.PointGapM)))
            {
                var point = new[] { edge.X, edge.Y, edge.SurfaceLevel.Value };
                if (seen.Add(point))
                {
                    kept.Add(point);
                }
            }
            int room = settings.MaxPoints - kept.Count;
            if (room < 0)
            {
                plan.Refused = "the corners and ramp road edges alone are " + kept.Count + " points, over the " + settings.MaxPoints + " of " + RoadTopographySettings.Block + "maxPoints";
                return plan;
            }
            List<double[]> spaced = inside.Where(seen.Add).ToList();
            List<double[]> thinned = Thin(spaced, room);
            plan.Thinned = spaced.Count - thinned.Count;
            kept.AddRange(thinned);
            plan.Points = kept;
            return plan;
        }

        // The hull by the monotone chain, counter clockwise from the lowest easting
        private static List<double[]> Hull(IEnumerable<double[]> points)
        {
            List<double[]> sorted = points.Select(point => new[] { point[0], point[1] }).OrderBy(point => point[0]).ThenBy(point => point[1]).ToList();
            var unique = new List<double[]>();
            foreach (double[] point in sorted)
            {
                if (unique.Count == 0 || unique[unique.Count - 1][0] != point[0] || unique[unique.Count - 1][1] != point[1])
                {
                    unique.Add(point);
                }
            }
            if (unique.Count < 3)
            {
                return unique;
            }
            var hull = new List<double[]>();
            foreach (double[] point in unique)
            {
                Push(hull, point, 2);
            }
            int lower = hull.Count + 1;
            for (int index = unique.Count - 2; index >= 0; index--)
            {
                Push(hull, unique[index], lower);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static void Push(List<double[]> hull, double[] point, int least)
        {
            while (hull.Count >= least && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(point);
        }

        private static double Cross(double[] origin, double[] a, double[] b)
        {
            return (a[0] - origin[0]) * (b[1] - origin[1]) - (a[1] - origin[1]) * (b[0] - origin[0]);
        }

        // Each side of a counter clockwise hull moved out by the margin, the corners where the moved sides meet, and a corner within the gap
        // of the last one dropped, as Revit refuses a side that short. Each corner has room for its level
        private static List<double[]> Grow(List<double[]> hull, double margin, double gap)
        {
            var grown = new List<double[]>();
            for (int index = 0; index < hull.Count; index++)
            {
                double[] before = hull[(index + hull.Count - 1) % hull.Count], at = hull[index], after = hull[(index + 1) % hull.Count];
                double[] one = Outward(before, at), two = Outward(at, after);
                double scale = margin / (1 + one[0] * two[0] + one[1] * two[1]);
                var corner = new[] { at[0] + (one[0] + two[0]) * scale, at[1] + (one[1] + two[1]) * scale, 0.0 };
                if (grown.Count == 0 || FloorPlan.Flat(grown[grown.Count - 1], corner) >= gap)
                {
                    grown.Add(corner);
                }
            }
            if (grown.Count > 3 && FloorPlan.Flat(grown[grown.Count - 1], grown[0]) < gap)
            {
                grown.RemoveAt(grown.Count - 1);
            }
            return grown;
        }

        private static double[] Outward(double[] from, double[] to)
        {
            double dx = to[0] - from[0], dy = to[1] - from[1], length = Math.Sqrt(dx * dx + dy * dy);
            return new[] { dy / length, -dx / length };
        }

        // At least the gap inside every side of a counter clockwise boundary, so no point touches the sketch of the toposolid
        private static bool Inside(List<double[]> boundary, double x, double y, double gap)
        {
            for (int index = 0; index < boundary.Count; index++)
            {
                double[] a = boundary[index], b = boundary[(index + 1) % boundary.Count];
                double dx = b[0] - a[0], dy = b[1] - a[1];
                if ((dx * (y - a[1]) - dy * (x - a[0])) / Math.Sqrt(dx * dx + dy * dy) < gap)
                {
                    return false;
                }
            }
            return true;
        }

        // The vertices of the visible faces inside the boundary, as a LandXML point no visible face uses is no part of the surface
        private static List<double[]> Vertices(LandXmlSurface surface, List<double[]> boundary, double gap)
        {
            double west = boundary.Min(corner => corner[0]), east = boundary.Max(corner => corner[0]), south = boundary.Min(corner => corner[1]), north = boundary.Max(corner => corner[1]);
            var found = new List<double[]>();
            foreach (int vertex in surface.Faces.Distinct().OrderBy(vertex => vertex))
            {
                double x = surface.Points[vertex * 3], y = surface.Points[vertex * 3 + 1];
                if (x >= west && x <= east && y >= south && y <= north && Inside(boundary, x, y, gap))
                {
                    found.Add(new[] { x, y, surface.Points[vertex * 3 + 2] });
                }
            }
            return found;
        }

        private static double[] Nearest(List<double[]> points, double[] to)
        {
            return points.OrderBy(point => FloorPlan.Flat(point, to)).First();
        }

        // R571. Only over the limit of the settings. The first point of each cell of a grid, the grid made larger until the room holds them,
        // so the points stay spread over the road, where a stride along the file would leave the parts the file lists late
        private static List<double[]> Thin(List<double[]> points, int room)
        {
            if (points.Count <= room)
            {
                return points;
            }
            if (room == 0)
            {
                return new List<double[]>();
            }
            double west = points.Min(point => point[0]), south = points.Min(point => point[1]);
            double span = Math.Max(points.Max(point => point[0]) - west, points.Max(point => point[1]) - south);
            double cell = Math.Max(span, 1e-3) / Math.Sqrt(points.Count);
            while (true)
            {
                var taken = new HashSet<long>();
                var kept = new List<double[]>();
                foreach (double[] point in points)
                {
                    if (taken.Add(((long)((point[0] - west) / cell) << 32) ^ (long)((point[1] - south) / cell)))
                    {
                        kept.Add(point);
                    }
                }
                if (kept.Count <= room)
                {
                    return kept;
                }
                cell *= 1.1;
            }
        }

        private static IEnumerable<RoadEdgePoint> Samples(RampEdge edge, TinSurface tin, double snap)
        {
            var at = new[] { "start", "middle", "end" };
            for (int index = 0; index < at.Length; index++)
            {
                double share = index / 2.0;
                double x = edge.Start[0] + (edge.End[0] - edge.Start[0]) * share, y = edge.Start[1] + (edge.End[1] - edge.Start[1]) * share;
                yield return new RoadEdgePoint
                {
                    Plot = edge.Plot, At = at[index], X = x, Y = y, Skipped = edge.Skipped, RampLevel = edge.Start[2] + (edge.End[2] - edge.Start[2]) * share, SurfaceLevel = tin.SnapZ(x, y, snap),
                };
            }
        }

        // R573. The level of the model at each sample, the highest triangle over it, as a solid gives its underside and layers too. The
        // triangles are internal, a sample and its level shared
        public static void ReadBack(IEnumerable<RoadEdgePoint> edges, double[] triangles, double[] offset)
        {
            foreach (RoadEdgePoint edge in edges)
            {
                double px = edge.X - offset[0], py = edge.Y - offset[1];
                double? top = null;
                for (int t = 0; t + 8 < triangles.Length; t += 9)
                {
                    double ax = triangles[t], ay = triangles[t + 1], az = triangles[t + 2], bx = triangles[t + 3], by = triangles[t + 4], bz = triangles[t + 5], cx = triangles[t + 6], cy = triangles[t + 7], cz = triangles[t + 8];
                    double d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
                    if (Math.Abs(d) < NoPlanArea)
                    {
                        continue;
                    }
                    double l0 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d, l1 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d, l2 = 1 - l0 - l1;
                    if (l0 >= -TinSurface.Eps && l1 >= -TinSurface.Eps && l2 >= -TinSurface.Eps)
                    {
                        double level = l0 * az + l1 * bz + l2 * cz;
                        top = top.HasValue ? Math.Max(top.Value, level) : level;
                    }
                }
                edge.ModelLevel = top + offset[2];
            }
        }

        // R572. Its own by the NBH and by the placed id, since a copy of a toposolid carries the entity of the one it was copied from
        public static bool IsOwn(ToposolidElement element, string nbh)
        {
            PlacedTag tag = element.Tag;
            return tag != null && tag.Key.Kind == Kind && tag.Key.Plot == nbh && tag.PlacedId == element.UniqueId;
        }

        // R572. What the toposolid was made from: the file of the surface by its hash, the boundary, every point and the settings that shape it,
        // so a press with the same key has nothing to change. The limit and the cover share only judge it, so they stay out
        public static string KeyOf(RoadSurfaceRead read, ToposolidPlan plan)
        {
            RoadTopographySettings settings = read.Settings;
            var text = new StringBuilder();
            text.Append(read.Mark?.Sha256).Append('|').Append(settings.TypeName).Append('|').Append(settings.Surface);
            foreach (double value in new[] { settings.MarginM, settings.PointGapM, settings.MaxPoints, settings.SnapM })
            {
                text.Append('|').Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
            foreach (double[] point in plan.Boundary.Concat(plan.Points))
            {
                text.Append('|').Append(point[0].ToString("F3", CultureInfo.InvariantCulture)).Append(',').Append(point[1].ToString("F3", CultureInfo.InvariantCulture)).Append(',').Append(point[2].ToString("F3", CultureInfo.InvariantCulture));
            }
            using (SHA256 hash = SHA256.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        public static ToposolidElement Matching(IEnumerable<ToposolidElement> own, string key)
        {
            return own.FirstOrDefault(element => element.Tag.Placed.Values.TryGetValue(KeyValue, out IReadOnlyList<double> held) && new string(held.Select(code => (char)code).ToArray()) == key);
        }

        public static string ActionOf(IReadOnlyCollection<ToposolidElement> own, string key)
        {
            return own.Count == 0 ? Make : Matching(own, key) != null ? Keep : Replace;
        }

        public static PlacedTag Tag(string nbh, string runId, string profile, string uniqueId, ToposolidPlan plan, string key)
        {
            var values = new Dictionary<string, double[]>
            {
                ["points"] = new double[] { plan.Points.Count }, ["corners"] = new double[] { plan.Boundary.Count },
                // A placed value is a list of numbers, so the key as the code points of its text, as FloorPlan keeps a type name
                [KeyValue] = key.Select(character => (double)character).ToArray(),
            };
            return new PlacedTag(PlacedSchema.Latest.Version, new TagKey(nbh, Kind, ""), runId, profile, uniqueId, new PlacedValues(values));
        }

        public static string NoToposolid(string why)
        {
            return Line + "no toposolid, " + why + ". The floors stand";
        }

        public static string PreviewLine(ToposolidPlan plan, RoadTopographySettings settings, string action, IEnumerable<ToposolidElement> own)
        {
            string against = Against(Counted(plan).Select(edge => edge.DifferenceMm), settings.LimitMm, "the surface") + SkippedText(plan);
            if (action == Keep)
            {
                return Line + "the toposolid of this NBH was made from this surface, these plots and these settings, so it is left as it is, " + against + Invented(plan, settings);
            }
            return Line + (action == Replace ? ReplaceText(own) + ", " : "") + "toposolid of type " + settings.TypeName + " from " + settings.Surface + ", " + plan.Points.Count + " points in a boundary of " + plan.Boundary.Count + " corners, "
                + plan.Edges.Count + " ramp road edge points, " + against + Invented(plan, settings);
        }

        // R571, R572. Null when no corner was made up, no point left out and nothing replaced
        public static string DoneLine(ToposolidPlan plan, RoadTopographySettings settings, string action, IEnumerable<ToposolidElement> own)
        {
            string invented = Invented(plan, settings);
            if (invented.Length == 0 && action != Replace)
            {
                return null;
            }
            string what = action == Replace ? ReplaceText(own) : action == Keep ? "the toposolid of this NBH is left as it is" : "toposolid made";
            return Line + what + ", " + plan.Points.Count + " points" + invented;
        }

        private static string ReplaceText(IEnumerable<ToposolidElement> own)
        {
            int subdivisions = own.Sum(element => element.SubDivisions);
            return "replaces the toposolid of this NBH, made from another surface, other plots or other settings, a hand edit on it is lost" + (subdivisions > 0 ? " and so are its " + subdivisions + " subdivisions" : "");
        }

        private static string Invented(ToposolidPlan plan, RoadTopographySettings settings)
        {
            return (plan.CornersOffSurface > 0 ? ", " + plan.CornersOffSurface + " of " + plan.Boundary.Count + " boundary corners lie off the surface and take the level of the nearest surface point" : "")
                + (plan.Thinned > 0 ? ", " + plan.Thinned + " surface points left out to stay within " + settings.MaxPoints : "");
        }

        private static string SkippedText(ToposolidPlan plan)
        {
            int skipped = plan.Edges.Count(edge => edge.Skipped);
            return skipped > 0 ? ", " + skipped + " points of ramps left as they stand are not counted" : "";
        }

        private static IEnumerable<RoadEdgePoint> Counted(ToposolidPlan plan)
        {
            return plan.Edges.Where(edge => !edge.Skipped);
        }

        // R573. Null when every road edge point counted is on the surface and on the model within the limit. Off the limit stops nothing
        public static string LimitLine(ToposolidPlan plan, RoadTopographySettings settings, string nbh)
        {
            List<RoadEdgePoint> edges = Counted(plan).ToList();
            bool fine = edges.All(edge => edge.DifferenceMm.HasValue && edge.ModelDifferenceMm.HasValue && Math.Abs(edge.DifferenceMm.Value) <= settings.LimitMm && Math.Abs(edge.ModelDifferenceMm.Value) <= settings.LimitMm);
            if (fine)
            {
                return null;
            }
            return Line + Against(edges.Select(edge => edge.DifferenceMm), settings.LimitMm, "the surface") + ", " + Against(edges.Select(edge => edge.ModelDifferenceMm), settings.LimitMm, "the model") + SkippedText(plan) + ", see " + RecordName(nbh);
        }

        private static string Against(IEnumerable<double?> differences, double limit, string what)
        {
            List<double?> all = differences.ToList();
            int over = all.Count(value => value.HasValue && Math.Abs(value.Value) > limit), off = all.Count(value => !value.HasValue);
            double? largest = Largest(all);
            return over + " of " + all.Count + " over " + FloorPlan.Text(limit) + " mm to " + what + (largest.HasValue ? ", largest " + Mm(largest.Value) + " mm" : "") + (off > 0 ? ", " + off + " off it" : "");
        }

        private static double? Largest(IEnumerable<double?> values)
        {
            return values.Where(value => value.HasValue).Select(value => (double?)Math.Abs(value.Value)).DefaultIfEmpty(null).Max();
        }

        private static string Mm(double value)
        {
            return Rounding.Fixed(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
        }

        // R573. The record of a run: the file read with its mark, the counts and each road edge point by plot, its levels and differences with
        // no x and no y. A read of the model that failed or was not made stays null. Largest and limit leave out the points marked skipped,
        // of ramps this press left as they stand: the plan, not the model, gives their ramp level
        public static JObject Report(string runId, string nbh, RoadSurfaceRead read, ToposolidPlan plan, string element, bool readBack, string problem, string action, string typeDuplicatedFrom)
        {
            List<RoadEdgePoint> edges = plan?.Edges ?? new List<RoadEdgePoint>();
            List<RoadEdgePoint> counted = edges.Where(edge => !edge.Skipped).ToList();
            double limit = read?.Settings?.LimitMm ?? 0;
            List<double?> surface = counted.Select(edge => edge.DifferenceMm).ToList();
            List<double?> model = counted.Select(edge => readBack ? edge.ModelDifferenceMm : null).ToList();
            return new JObject
            {
                ["format"] = RecordFormat, ["runId"] = runId, ["nbh"] = nbh, ["made"] = element != null, ["action"] = action, ["element"] = element, ["problem"] = problem,
                ["type"] = read?.Settings?.TypeName, ["typeDuplicatedFrom"] = typeDuplicatedFrom,
                ["surface"] = read == null ? null : new JObject
                {
                    ["name"] = read.Settings.Surface, ["file"] = read.Mark?.Name, ["size"] = read.Mark?.Size, ["writeTimeUtc"] = read.Mark?.WriteTimeUtc, ["sha256"] = read.Mark?.Sha256,
                },
                ["marginM"] = read?.Settings?.MarginM,
                ["boundaryCorners"] = plan?.Boundary.Count, ["points"] = plan?.Points.Count, ["verticesInside"] = plan?.VerticesInside,
                ["cornersOffSurface"] = plan?.CornersOffSurface, ["thinned"] = plan?.Thinned,
                ["limitMm"] = limit, ["skippedPoints"] = edges.Count - counted.Count,
                ["largestMm"] = Rounded(Largest(surface)), ["overLimit"] = surface.Count(value => value.HasValue && Math.Abs(value.Value) > limit), ["offSurface"] = surface.Count(value => !value.HasValue),
                ["largestModelMm"] = Rounded(Largest(model)), ["overLimitModel"] = model.Count(value => value.HasValue && Math.Abs(value.Value) > limit), ["offModel"] = model.Count(value => !value.HasValue),
                ["edges"] = new JArray(edges.Select(edge => new JObject
                {
                    ["plot"] = edge.Plot, ["at"] = edge.At, ["skipped"] = edge.Skipped, ["rampLevelM"] = JsonOut.Round3(edge.RampLevel), ["surfaceLevelM"] = Level(edge.SurfaceLevel), ["modelLevelM"] = Level(readBack ? edge.ModelLevel : null),
                    ["differenceMm"] = Rounded(edge.DifferenceMm), ["modelDifferenceMm"] = Rounded(readBack ? edge.ModelDifferenceMm : null),
                })),
            };
        }

        private static double? Level(double? value)
        {
            return value.HasValue ? JsonOut.Round3(value.Value) : (double?)null;
        }

        private static double? Rounded(double? value)
        {
            return value.HasValue ? Rounding.Fixed(value.Value, 1) : (double?)null;
        }

        // Points within the gap of a point held are one, by a grid of the gap, whose eight neighbours hold every point near enough
        private sealed class PointSpacing
        {
            private readonly double gap;
            private readonly Dictionary<long, List<double[]>> cells = new Dictionary<long, List<double[]>>();

            public PointSpacing(double gap)
            {
                this.gap = gap;
            }

            public bool Add(double[] point)
            {
                long i = (long)Math.Floor(point[0] / gap), j = (long)Math.Floor(point[1] / gap);
                for (long di = -1; di <= 1; di++)
                {
                    for (long dj = -1; dj <= 1; dj++)
                    {
                        if (cells.TryGetValue(Key(i + di, j + dj), out List<double[]> near) && near.Any(other => FloorPlan.Flat(other, point) < gap))
                        {
                            return false;
                        }
                    }
                }
                long key = Key(i, j);
                if (!cells.TryGetValue(key, out List<double[]> list))
                {
                    list = new List<double[]>();
                    cells[key] = list;
                }
                list.Add(point);
                return true;
            }

            // Shared coordinates reach millions of metres, so at a 1 cm grid each index fits well under 2 to the power 31
            private static long Key(long i, long j)
            {
                return (i << 32) ^ (j & 0xFFFFFFFFL);
            }
        }
    }
}
