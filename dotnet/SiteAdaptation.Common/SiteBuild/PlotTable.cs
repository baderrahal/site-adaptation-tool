using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SiteBuild
{
    // R640. The plot table of stage 1, plots.json of dataCheck, read for the build. Nothing here guesses: a row that cannot stand is said in an issue and left
    // out, a table that cannot be read at all is a blocking line
    internal static class PlotTable
    {
        // The rows of the table. Null with the fault in plain words when the file is no plot table
        public static List<PlotIn> Read(string path, List<PlanIssue> issues, out string fault)
        {
            fault = null;
            JObject table;
            try
            {
                table = JObject.Parse(WorkFiles.ReadText(path));
            }
            catch (Exception failure) when (failure is JsonException || failure is System.IO.IOException)
            {
                fault = PlanTexts.TableUnreadable(failure.Message);
                return null;
            }
            JArray rows = table["plots"] as JArray;
            if (rows == null)
            {
                fault = PlanTexts.TableUnreadable("it holds no list of plots");
                return null;
            }
            var found = new List<PlotIn>();
            foreach (JObject row in rows.OfType<JObject>())
            {
                string number = Text(row["number"]);
                if (number == null)
                {
                    // A label that held two numbers has none of its own, stage 1 gave the reason
                    issues.Add(new PlanIssue { Line = PlanTexts.PlotWithoutNumber(Text(row["missing"]?["number"])) });
                    continue;
                }
                found.Add(new PlotIn
                {
                    Number = number,
                    VillaType = Text(row["villaType"]),
                    Mirror = row["mirror"]?.Type == JTokenType.Boolean ? (bool?)row["mirror"] : null,
                    Area = Text(row["area"]),
                    AreaM2 = row["areaM2"] != null && (row["areaM2"].Type == JTokenType.Float || row["areaM2"].Type == JTokenType.Integer) ? (double?)row["areaM2"] : null,
                    Groups = (row["groups"] as JArray ?? new JArray()).Select(Text).ToList(),
                    Outline = Outline(row["outline"]),
                    Source = row["source"] as JObject ?? new JObject(),
                    MissingWhy = row["missing"] as JObject ?? new JObject(),
                    Disputed = row["disputed"] as JObject ?? new JObject(),
                    Weak = row["weak"] as JObject ?? new JObject(),
                });
            }
            return found;
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            if (token.Type == JTokenType.Float)
            {
                return ((double)token).ToString("R", CultureInfo.InvariantCulture);
            }
            var value = token as JValue;
            // Trimmed, so "101" and "101 " are one plot and a blank is no number
            string text = value == null ? null : Convert.ToString(value.Value, CultureInfo.InvariantCulture)?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        // Null for a ring of under 3 points or a point that is no pair of numbers, never a ring made of what is left
        private static List<double[]> Outline(JToken token)
        {
            var ring = token as JArray;
            if (ring == null || ring.Count < 3)
            {
                return null;
            }
            var points = new List<double[]>();
            foreach (JToken item in ring)
            {
                var pair = item as JArray;
                if (pair == null || pair.Count < 2 || !IsNumber(pair[0]) || !IsNumber(pair[1]))
                {
                    return null;
                }
                points.Add(new[] { (double)pair[0], (double)pair[1] });
            }
            return points;
        }

        private static bool IsNumber(JToken token)
        {
            return token.Type == JTokenType.Float || token.Type == JTokenType.Integer;
        }
    }
}
