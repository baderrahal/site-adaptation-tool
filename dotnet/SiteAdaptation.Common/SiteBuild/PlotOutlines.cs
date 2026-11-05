using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SiteBuild
{
    // R640. The outline of each plot of the plot table, in metres, so the build and the preview have a ring to draw. Stage 1 finds the rings and keeps only their
    // numbers, so the same scan of the same reads gives them again. A plot whose ring cannot be given stays without an outline and a note says why, the table
    // never gets an empty ring in its place
    public static class PlotOutlines
    {
        // Adds "outline" to each row of the plots of dataCheck. Gives the notes in plain words, empty when every plot got its ring
        public static List<string> Add(JObject plots, DataCheckSettings settings, DataCheckInput input)
        {
            var notes = new List<string>();
            JArray rows = plots?["plots"] as JArray;
            if (rows == null || rows.Count == 0 || input.CadReads.Count == 0)
            {
                return notes;
            }
            var prototypes = new HashSet<string>(input.PrototypeNames.Select(settings.PrototypeCode).Where(code => code != null), StringComparer.Ordinal);
            var codes = new HashSet<string>(prototypes, StringComparer.Ordinal);
            if (input.Confirmed != null)
            {
                codes.UnionWith(input.Confirmed.CodeMap.Select(pair => pair.Key));
            }
            List<KeyValuePair<string, List<double[]>>> rings;
            string plotSource;
            try
            {
                ScanLogic.Result(settings.Scan, codes, prototypes, settings.StrongShare, new HashSet<string>(StringComparer.Ordinal), input.CadReads, input.Excels, input.Pdfs, new JArray(), out rings, out plotSource);
            }
            catch (Exception failure)
            {
                notes.Add("The outlines of the plots could not be found again, so the plot table holds none: " + failure.GetType().Name + ": " + failure.Message);
                return notes;
            }
            string drawing = plotSource == null ? null : plotSource.Split('|')[0];
            JObject read = drawing == null ? null : input.CadReads.FirstOrDefault(item => (string)item["source"] == drawing);
            double? scale = read == null ? null : DataCheckSettings.MetersPerUnit((string)read["units"]);
            if (!scale.HasValue)
            {
                notes.Add("The unit of the plot drawing is not one the tool knows, so the plot table holds no outline.");
                return notes;
            }
            Dictionary<string, List<double[]>> byNumber = rings.GroupBy(ring => ring.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
            int without = 0;
            foreach (JObject row in rows.OfType<JObject>())
            {
                string number = row["number"]?.Type == JTokenType.String ? (string)row["number"] : null;
                List<double[]> ring;
                if (number != null && byNumber.TryGetValue(number, out ring) && ring.Count >= 3)
                {
                    row["outline"] = new JArray(ring.Select(point => new JArray(point[0] * scale.Value, point[1] * scale.Value)));
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
