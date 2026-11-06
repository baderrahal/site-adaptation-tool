using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SiteBuild
{
    // R645. The outline of each plot of the plot table, in metres, so the build and the preview have a ring to draw. The rings are the ones stage 1 itself chose
    // (DataCheckOutput.Rings, after the tie of candidate outlines was broken), nothing is scanned again. A plot whose ring cannot be given stays without an
    // outline and a note says why, the table never gets an empty ring in its place
    public static class PlotOutlines
    {
        // Adds "outline" to each row of the plots of the output. Gives the notes in plain words, empty when every plot got its ring
        public static List<string> Add(DataCheckOutput output)
        {
            var notes = new List<string>();
            JArray rows = output?.Plots?["plots"] as JArray;
            if (rows == null || rows.Count == 0)
            {
                return notes;
            }
            if (!output.MetersPerUnit.HasValue)
            {
                notes.Add("The unit of the plot drawing is not one the tool knows, so the plot table holds no outline.");
                return notes;
            }
            double scale = output.MetersPerUnit.Value;
            Dictionary<string, List<double[]>> byNumber = output.Rings.GroupBy(ring => ring.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
            int without = 0;
            foreach (JObject row in rows.OfType<JObject>())
            {
                string number = row["number"]?.Type == JTokenType.String ? (string)row["number"] : null;
                List<double[]> ring;
                if (number != null && byNumber.TryGetValue(number, out ring) && ring.Count >= 3)
                {
                    row["outline"] = new JArray(ring.Select(point => new JArray(point[0] * scale, point[1] * scale)));
                }
                else
                {
                    without++;
                }
            }
            if (without > 0)
            {
                notes.Add(without + " plots have no outline in the plot table, the drawing gave no ring for them.");
            }
            return notes;
        }
    }
}
