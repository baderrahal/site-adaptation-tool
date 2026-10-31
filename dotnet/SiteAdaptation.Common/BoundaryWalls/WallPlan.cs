using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.BoundaryWalls
{
    // R380, R362. One boundary wall of plan_<NBH>.json as BuildPlan writes the walls of Common/Plan: the plot it is placed from, its edge,
    // the plots it stands on, its two ends in shared coordinates, its base and top in shared elevation and the plots of the base and top
    public sealed class PlanWallItem
    {
        public string Plot { get; set; }
        public int Edge { get; set; }
        public List<string> Plots { get; set; } = new List<string>();
        public double[][] Ends { get; set; }
        public double Base { get; set; }
        public double Top { get; set; }
        public string BasePlot { get; set; }
        public string TopPlot { get; set; }
    }

    // R380. One wall of the plan as the document takes it, its ends and levels internal, with its item of R368
    internal sealed class PlannedWall
    {
        public PlanWallItem Wall;
        public PlanItem Item;
        public bool InScope;
        public double[] Start;
        public double[] End;
        public double Base;
        public double Top;

        public string Key => WallPlan.KeyOf(Item.Key.Plot, Item.Key.Part);
        public string Label => "plot " + Item.Key.Plot + " wall " + Item.Key.Part;
    }

    public static class WallPlan
    {
        public const string Line = "line";
        public const string Base = "base";
        public const string Top = "top";

        public static string KeyOf(string plot, string part)
        {
            return plot + "|" + part;
        }

        // R380, R368, Q115. The part of the tag of a wall: its plots, its edge and its piece, so two pieces of one edge part keyRules.wallOpen
        // left a span open between keep two keys. The piece counts the walls of the same plot, edge and plots in plan order from 1
        public static List<string> Parts(IEnumerable<PlanWallItem> walls)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var parts = new List<string>();
            foreach (PlanWallItem wall in walls)
            {
                string stem = string.Join("+", wall.Plots) + "|e" + wall.Edge.ToString(CultureInfo.InvariantCulture);
                string counted = wall.Plot + "\n" + stem;
                seen[counted] = seen.TryGetValue(counted, out int count) ? count + 1 : 1;
                parts.Add(stem + "|p" + seen[counted].ToString(CultureInfo.InvariantCulture));
            }
            return parts;
        }

        // The walls array of the plan, each value of a wall the value of its sourced field. A wall the plan holds with no plot, no two ends
        // or no base or top stops the button by its index, since placing the others would leave a gap no list shows
        public static List<PlanWallItem> Parse(JObject plan)
        {
            var found = new List<PlanWallItem>();
            JArray walls = plan?["walls"] as JArray ?? new JArray();
            for (int index = 0; index < walls.Count; index++)
            {
                JObject wall = walls[index] as JObject;
                try
                {
                    double[][] ends = ((JArray)Value(wall, "ends")).Select(end => ((JArray)end).Select(number => (double)number).ToArray()).ToArray();
                    if (ends.Length != 2 || ends.Any(end => end.Length < 2))
                    {
                        throw new InvalidDataException("its ends are not two points");
                    }
                    found.Add(new PlanWallItem
                    {
                        Plot = ((string)wall["plot"])?.Trim() ?? throw new InvalidDataException("it names no plot"), Edge = (int)wall["edge"],
                        Plots = ((JArray)Value(wall, "plots")).Select(plot => ((string)plot).Trim()).ToList(),
                        Ends = ends, Base = (double)Value(wall, "base"), Top = (double)Value(wall, "top"),
                        BasePlot = (string)wall["basePlot"], TopPlot = (string)wall["topPlot"],
                    });
                }
                catch (Exception failure) when (failure is InvalidCastException || failure is ArgumentException || failure is NullReferenceException || failure is InvalidDataException || failure is FormatException)
                {
                    throw new InvalidDataException("wall " + index + " of the plan cannot be read, " + failure.Message + ", nothing was changed (R380)");
                }
            }
            return found;
        }

        private static JToken Value(JObject wall, string field)
        {
            JToken value = wall?[field]?["value"];
            if (value == null || value.Type == JTokenType.Null)
            {
                throw new InvalidDataException("it has no " + field);
            }
            return value;
        }

        // R368, R385. The values of a wall: its location line start then end, its base and its top, internal metres
        public static PlacedValues Values(double[] start, double[] end, double bottom, double top)
        {
            return new PlacedValues(new Dictionary<string, double[]>(StringComparer.Ordinal)
            {
                [Line] = new[] { start[0], start[1], end[0], end[1] }, [Base] = new[] { bottom }, [Top] = new[] { top },
            });
        }

        internal static PlacedValues Current(WallReadBack state)
        {
            return state?.Start == null || state.End == null ? new PlacedValues(new Dictionary<string, double[]>()) : Values(state.Start, state.End, state.Base, state.Top);
        }

        // R380. Null when the wall reads back on its line, base and top within the tolerance and centred on its line, else what differs
        internal static string Mismatch(WallReadBack read, PlannedWall wall, double tolerance)
        {
            if (read?.Start == null || read.End == null)
            {
                return "it gives no location line";
            }
            var off = new List<string>();
            if (Flat(read.Start, wall.Start) > tolerance || Flat(read.End, wall.End) > tolerance)
            {
                off.Add(Line);
            }
            if (Math.Abs(read.Base - wall.Base) > tolerance)
            {
                off.Add(Base);
            }
            if (Math.Abs(read.Top - wall.Top) > tolerance)
            {
                off.Add(Top);
            }
            if (!read.Centred)
            {
                off.Add("location line not on the wall centre");
            }
            return off.Count == 0 ? null : string.Join(", ", off) + " off by more than " + tolerance.ToString("0.###", CultureInfo.InvariantCulture) + " m";
        }

        internal static double Flat(double[] a, double[] b)
        {
            return Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));
        }
    }
}
