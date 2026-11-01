using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // R456. A plot corner of the baseplan in shared metres, checked against the shared point Revit gives back for it
    public sealed class ControlPoint
    {
        public string Name { get; set; }
        public string Plot { get; set; }
        public double[] SharedM { get; set; }
    }

    // R456. The control points of button 1 from CAD. The first is the south west corner, the least east plus north, the next each the
    // corner whose nearest chosen corner is the farthest, so the points spread over the whole site. A tie takes the least north, then
    // the least east, then the plot name in ordinal order, so the pick never hangs on the order of the read (CHOSEN, Q329)
    public static class ControlPoints
    {
        public static List<ControlPoint> Pick(IReadOnlyList<CadPlot> plots, int count)
        {
            // A corner two plots share is one point, kept under the first plot of the order, so no point is picked twice
            List<KeyValuePair<string, double[]>> corners = plots.Where(plot => plot.Vertices != null)
                .SelectMany(plot => plot.Vertices.Select(vertex => new KeyValuePair<string, double[]>(plot.Plot, new[] { vertex[0], vertex[1], vertex.Length > 2 ? vertex[2] : 0.0 })))
                .OrderBy(corner => corner.Value[1]).ThenBy(corner => corner.Value[0]).ThenBy(corner => corner.Key, StringComparer.Ordinal)
                .GroupBy(corner => new KeyValuePair<double, double>(corner.Value[0], corner.Value[1])).Select(same => same.First()).ToList();
            if (corners.Count < count)
            {
                throw new InvalidDataException("The baseplan holds " + corners.Count.ToString(CultureInfo.InvariantCulture) + " distinct plot corners, fewer than the " + count.ToString(CultureInfo.InvariantCulture) + " control points of coordinates.controlPointCount, so button 1 cannot check the site position (R456)");
            }
            var chosen = new List<KeyValuePair<string, double[]>> { corners.OrderBy(corner => corner.Value[0] + corner.Value[1]).First() };
            while (chosen.Count < count)
            {
                KeyValuePair<string, double[]> best = corners[0];
                double bestGap = -1;
                foreach (KeyValuePair<string, double[]> corner in corners)
                {
                    double gap = chosen.Min(have => Plan(have.Value, corner.Value));
                    if (gap > bestGap)
                    {
                        best = corner;
                        bestGap = gap;
                    }
                }
                chosen.Add(best);
            }
            return chosen.Select((corner, index) => new ControlPoint { Name = SetUpTexts.ControlPoint(index + 1, corner.Key), Plot = corner.Key, SharedM = corner.Value }).ToList();
        }

        // The CAD point taken to its internal point by the position, then to shared by Revit, as the survey point row of 3.1 Checks is.
        // revitShared null leaves the Revit column empty before the dry run
        public static CheckPointRow Check(ControlPoint point, SitePosition position, Func<double[], double[]> revitShared, double limitMm, int decimals)
        {
            var row = new CheckPointRow { Point = point.Name, Cad = SetUpTexts.Coordinate(point.SharedM) };
            if (revitShared != null)
            {
                double[] revit = revitShared(SiteCoordinates.ToInternal(position, point.SharedM));
                row.Revit = SetUpTexts.Coordinate(revit);
                double gapMm = SiteCoordinates.Gap(point.SharedM, revit) * 1000.0;
                row.DiffMm = Rounding.Fixed(gapMm, decimals);
                // The gap as measured is held to the limit, the shown 0.1 of a gap of 0.104 mm must not pass a limit of 0.1 mm
                row.Pass = gapMm <= limitMm;
            }
            return row;
        }

        private static double Plan(double[] a, double[] b)
        {
            return Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
        }
    }
}
