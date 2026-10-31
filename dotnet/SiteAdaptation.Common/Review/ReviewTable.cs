using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Review
{
    // R406. The rows of the Review data table from the plot table of button 2 (R352), one per baseplan face whose neighbourhood is the
    // NBH. plotTable is the plot table as a JSON object keyed by the baseplan label. plan holds what the build of button 4 knows per
    // baseplan label and rings the baseplan ring of each plot per label, both null before that, and site is site.json of button 1
    // (SetUpRun.SiteJson). Plot No is the location plan number, the plot of R208, and the official number when the face holds none
    // (CHOSEN by Code). Mirror is the one of the plan when it has one, else the mirror of the footprint block (CHOSEN)
    public static class ReviewTable
    {
        public static List<ReviewRow> Rows(JObject plotTable, string nbh, IReadOnlyDictionary<string, ReviewPlanFact> plan, IReadOnlyDictionary<string, double[][]> rings, JObject site)
        {
            var rows = new List<ReviewRow>();
            foreach (JProperty entry in plotTable.Properties())
            {
                var face = entry.Value as JObject;
                // The NBH of a plot is its area key nbhId of R256, the neighbourhood of the schedule is an IM letter (CLAUDE.md). A table
                // with no nbhId field, as the made up tables of the tests, falls back on the neighbourhood
                if (face == null || Text(face.Property("nbhId") != null ? face["nbhId"] : face["neighbourhood"]) != nbh)
                {
                    continue;
                }
                string label = Text(face["baseplanLabel"]) ?? entry.Name;
                string number = Text(face["locationPlanNumber"]) ?? Text(face["officialNumber"]);
                if (number == null)
                {
                    continue;
                }
                ReviewPlanFact fact = null;
                plan?.TryGetValue(label, out fact);
                double[][] ring = null;
                rings?.TryGetValue(label, out ring);
                JToken mirrored = face["footprintMirrored"];
                bool? footprint = mirrored != null && mirrored.Type == JTokenType.Boolean ? (bool?)mirrored.Value<bool>() : null;
                var row = new ReviewRow
                {
                    PlotNo = number,
                    CadLabel = label,
                    Nbh = nbh,
                    VillaType = Text(face["typologyRev9"]),
                    Mirror = fact?.Mirror ?? footprint,
                    Cluster = fact?.Cluster,
                    AreaM2 = face["faceAreaM2"] == null || face["faceAreaM2"].Type == JTokenType.Null ? 0 : face["faceAreaM2"].Value<double>(),
                    RoadAtFront = fact?.RoadAtFront,
                    RuleId = ReviewRules.RowRule,
                };
                if (fact?.FrontEdge != null && ring != null)
                {
                    row.FrontEdge = fact.FrontEdge.Value + 1;
                    row.Corners = Corners(ring, fact.FrontEdge.Value, site);
                }
                rows.Add(row);
            }
            return rows.OrderBy(row => long.TryParse(row.PlotNo, NumberStyles.None, CultureInfo.InvariantCulture, out long value) ? value : long.MaxValue)
                .ThenBy(row => row.PlotNo, StringComparer.Ordinal).ToList();
        }

        // The model coordinates of a CAD point by the survey point of site.json: the CAD point less the shared position of the survey point
        // plus its internal position. R83 stops a model whose angle to true north is not 0, so with another angle in the site file no
        // model coordinate is given
        public static List<ReviewCorner> Corners(IReadOnlyList<double[]> ring, int frontEdge, JObject site)
        {
            double[] internalAt = Point(site?["surveyPoint"]?["position"]);
            double[] sharedAt = Point(site?["surveyPoint"]?["sharedPosition"]);
            JToken angle = site?["angleToTrueNorthDeg"];
            bool model = internalAt != null && sharedAt != null && angle != null && angle.Type != JTokenType.Null && Math.Abs(angle.Value<double>()) < 1e-9;
            double[][] ordered = ReviewCorners.Order(ring, frontEdge);
            var corners = new List<ReviewCorner>();
            for (int at = 0; at < ordered.Length; at++)
            {
                corners.Add(new ReviewCorner
                {
                    Number = at + 1,
                    SharedE = ordered[at][0],
                    SharedN = ordered[at][1],
                    X = model ? (double?)(ordered[at][0] - sharedAt[0] + internalAt[0]) : null,
                    Y = model ? (double?)(ordered[at][1] - sharedAt[1] + internalAt[1]) : null,
                });
            }
            return corners;
        }

        private static double[] Point(JToken token)
        {
            var array = token as JArray;
            return array == null || array.Count < 2 ? null : new[] { array[0].Value<double>(), array[1].Value<double>() };
        }

        private static string Text(JToken token)
        {
            string text = token == null || token.Type == JTokenType.Null ? null : token.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
