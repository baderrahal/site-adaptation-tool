using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SiteBuild
{
    // R646. A check on the whole site for outlines that overlap by more than limitMm: the plot outlines always, and the masses when the request gives their
    // rings. Two outlines that share an edge or touch do not overlap. The pairs are listed up to ListLimit, the count is always whole
    internal static class MassOverlap
    {
        public const int ListLimit = 100;

        private sealed class Item
        {
            public string Plot;
            public List<double[]> Ring;
            public double[] Box;
        }

        // masses is the optional file of the request: {"masses": [{"plot": "101", "ring": [[x, y], ...]}]}, the base ring of the mass of each plot in the coordinates of the outlines
        public static JObject Check(IReadOnlyList<PlanPlot> plots, JObject masses, double limitMm, double[] origin, List<PlanIssue> issues)
        {
            List<Item> outlines = plots.Where(plot => plot.In.Outline != null).Select(plot => Make(plot.Number, plot.In.Outline)).ToList();
            List<Item> bodies = Masses(masses);
            JObject plotResult = Pairs(outlines, limitMm, origin, "plots", issues);
            JObject massResult = masses == null ? null : Pairs(bodies, limitMm, origin, "masses", issues);
            return new JObject
            {
                ["limitMm"] = limitMm,
                ["plots"] = plotResult,
                ["masses"] = massResult ?? (JToken)new JObject { ["checked"] = 0, ["count"] = 0, ["line"] = PlanTexts.NoMassesGiven },
            };
        }

        private static List<Item> Masses(JObject masses)
        {
            var found = new List<Item>();
            foreach (JObject row in (masses?["masses"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var ring = new List<double[]>();
                foreach (JToken point in row["ring"] as JArray ?? new JArray())
                {
                    var pair = point as JArray;
                    if (pair != null && pair.Count >= 2 && (pair[0].Type == JTokenType.Float || pair[0].Type == JTokenType.Integer) && (pair[1].Type == JTokenType.Float || pair[1].Type == JTokenType.Integer))
                    {
                        ring.Add(new[] { (double)pair[0], (double)pair[1] });
                    }
                }
                if (ring.Count >= 3)
                {
                    found.Add(Make((string)row["plot"] ?? "?", ring));
                }
            }
            return found;
        }

        private static Item Make(string plot, List<double[]> ring)
        {
            return new Item { Plot = plot, Ring = ring, Box = Rings.Box(ring) };
        }

        // Sweep along east: an item is tried against the later ones whose box starts before it ends, so a site of thousands of plots tries a few neighbours each
        private static JObject Pairs(List<Item> items, double limitMm, double[] origin, string kind, List<PlanIssue> issues)
        {
            double limitM = limitMm / 1000.0;
            List<Item> sorted = items.OrderBy(item => item.Box[0]).ToList();
            int count = 0;
            var listed = new JArray();
            for (int first = 0; first < sorted.Count; first++)
            {
                Item one = sorted[first];
                for (int second = first + 1; second < sorted.Count && sorted[second].Box[0] <= one.Box[1] - limitM; second++)
                {
                    Item other = sorted[second];
                    bool northMeets = Math.Min(one.Box[3], other.Box[3]) - Math.Max(one.Box[2], other.Box[2]) > limitM;
                    if (!northMeets || (!Rings.OverlapsOver(one.Ring, other.Ring, limitMm, origin) && !Rings.OverlapsOver(other.Ring, one.Ring, limitMm, origin)))
                    {
                        continue;
                    }
                    count++;
                    if (listed.Count < ListLimit)
                    {
                        listed.Add(new JObject { ["a"] = one.Plot, ["b"] = other.Plot });
                        issues.Add(new PlanIssue { Plot = one.Plot, Line = PlanTexts.Overlap(kind, one.Plot, other.Plot, limitMm) });
                    }
                }
            }
            if (count > ListLimit)
            {
                issues.Add(new PlanIssue { Line = PlanTexts.OverlapMore(kind, count - ListLimit) });
            }
            return new JObject { ["checked"] = items.Count, ["count"] = count, ["listed"] = listed, ["line"] = PlanTexts.OverlapLine(kind, items.Count, count, limitMm) };
        }
    }
}
