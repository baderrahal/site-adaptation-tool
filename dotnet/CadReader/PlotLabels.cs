using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CadReader
{
    // Gives every closed face its plot number from the label text inside it.
    // One label makes the face a plot. No label or several labels send the face to the other faces list with the reason.
    public static class PlotLabels
    {
        private static readonly Regex NumberLine = new Regex(@"^(?:(?<type>.*?)\s+)?(?<number>\d{1,6})$", RegexOptions.Compiled);
        private const double ArcStep = Math.PI / 36;

        public sealed class Label
        {
            public string Number;
            public string Type;
            public double X;
            public double Y;
        }

        public sealed class Result
        {
            public List<PlotEntry> Plots = new List<PlotEntry>();
            public List<PlotEntry> Other = new List<PlotEntry>();
            public PlotsSummary Summary = new PlotsSummary();
        }

        public static Label Parse(TextEntry text)
        {
            if (string.IsNullOrWhiteSpace(text.Text))
            {
                return null;
            }
            var lines = text.Text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            Match match = NumberLine.Match(lines[lines.Count - 1]);
            if (!match.Success)
            {
                return null;
            }
            var type = new List<string>(lines.Take(lines.Count - 1));
            if (match.Groups["type"].Success && match.Groups["type"].Value.Length > 0)
            {
                type.Add(match.Groups["type"].Value);
            }
            return new Label
            {
                Number = match.Groups["number"].Value,
                Type = type.Count == 0 ? null : string.Join(" ", type),
                X = text.X,
                Y = text.Y,
            };
        }

        public static Result Join(List<PlotEntry> shapes, IEnumerable<TextEntry> texts, LayerConfig config)
        {
            var result = new Result();
            var labels = new List<Label>();
            int withoutNumber = 0;
            foreach (TextEntry text in texts.Where(entry => config.IsPlotLabel(entry.Layer)))
            {
                Label label = Parse(text);
                if (label == null)
                {
                    withoutNumber++;
                }
                else
                {
                    labels.Add(label);
                }
            }

            var inside = new HashSet<Label>();
            var inAnyFace = new HashSet<Label>();
            foreach (PlotEntry shape in shapes)
            {
                var ring = Ring(shape);
                double[] box = Box(ring);
                var held = labels.Where(label => label.X >= box[0] && label.X <= box[2] && label.Y >= box[1] && label.Y <= box[3] && Contains(ring, label.X, label.Y)).ToList();
                shape.Center = Centroid(ring);
                inAnyFace.UnionWith(held);
                if (held.Count == 1)
                {
                    shape.Plot = held[0].Number;
                    shape.Type = held[0].Type;
                    result.Plots.Add(shape);
                    inside.Add(held[0]);
                    continue;
                }
                shape.Kind = "other_face";
                shape.Reason = held.Count == 0 ? "no_label" : "many_labels";
                if (held.Count > 1)
                {
                    shape.Labels = held.Select(label => label.Number).ToList();
                }
                result.Other.Add(shape);
                if (held.Count == 0)
                {
                    result.Summary.NoLabel++;
                }
                else
                {
                    result.Summary.ManyLabels++;
                }
            }

            result.Summary.Shapes = shapes.Count;
            result.Summary.Plots = result.Plots.Count;
            result.Summary.OtherFaces = result.Other.Count;
            result.Summary.Labels = labels.Count;
            result.Summary.LabelsWithoutNumber = withoutNumber;
            result.Summary.LabelsOutsideShapes = labels.Count - inAnyFace.Count;
            result.Summary.DuplicateNumbers = result.Plots.GroupBy(plot => plot.Plot).Where(group => group.Count() > 1).Select(group => group.Key).OrderBy(number => number).ToList();
            return result;
        }

        public static List<double[]> Ring(PlotEntry shape)
        {
            var ring = new List<double[]>();
            int count = shape.Vertices.Count;
            var arcs = shape.Arcs == null ? new Dictionary<int, ArcEntry>() : shape.Arcs.ToDictionary(arc => arc.Segment);
            for (int index = 0; index < count; index++)
            {
                double[] a = shape.Vertices[index];
                double[] b = shape.Vertices[(index + 1) % count];
                ring.Add(new[] { a[0], a[1] });
                ArcEntry arc;
                if (!arcs.TryGetValue(index, out arc))
                {
                    continue;
                }
                double from = Math.Atan2(a[1] - arc.Center[1], a[0] - arc.Center[0]);
                double to = Math.Atan2(b[1] - arc.Center[1], b[0] - arc.Center[0]);
                double sweep = arc.Ccw ? to - from : from - to;
                while (sweep <= 0)
                {
                    sweep += 2 * Math.PI;
                }
                if (!arc.Ccw)
                {
                    sweep = -sweep;
                }
                int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / ArcStep));
                for (int step = 1; step < steps; step++)
                {
                    double angle = from + sweep * step / steps;
                    ring.Add(new[] { arc.Center[0] + arc.Radius * Math.Cos(angle), arc.Center[1] + arc.Radius * Math.Sin(angle) });
                }
            }
            return ring;
        }

        public static double[] Box(List<double[]> ring)
        {
            return new[] { ring.Min(p => p[0]), ring.Min(p => p[1]), ring.Max(p => p[0]), ring.Max(p => p[1]) };
        }

        // One copy of each ring rule in Common, R451 and R453
        public static bool Contains(List<double[]> ring, double x, double y)
        {
            return SiteAdaptation.Common.ScanLogic.Contains(ring, x, y);
        }

        public static double[] Centroid(List<double[]> ring)
        {
            return SiteAdaptation.Common.PlaceVillas.RingMath.Centroid(ring);
        }
    }
}
