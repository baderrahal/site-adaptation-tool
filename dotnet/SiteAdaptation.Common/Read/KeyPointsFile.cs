using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R353. A floor of an answer key as the step keyPoints of the read job takes it: its element id, type, the text of its Plot parameter
    // and the corners of the outer loop of its sketch, the start of each edge, in internal and in shared coordinates
    public sealed class KeyPointFloor
    {
        public long ElementId { get; set; }
        public string TypeName { get; set; }
        public string PlotText { get; set; }
        public List<double[]> Corners { get; set; }
        public List<double[]> SharedCorners { get; set; }
    }

    // R353. testhost_<code>_points.json of a read job: the project position, the survey point, the project base point and the corners
    // of every floor of the model in internal and shared coordinates, every double written by ExactDouble so it reads back as the same
    // double, where the reader of R311 writes 3 decimals, so the check points of button 1 hold the CAD against a key at full precision.
    // The plot of a floor is the number of its Plot parameter as R37 reads it (PlotParts). The Revit side only collects
    public static class KeyPointsFile
    {
        public const string FormatText = "site-adaptation-key-points";
        public const int FormatVersion = 1;

        private static JArray Point(double[] point)
        {
            return new JArray(point.Select(value => (object)value).ToArray());
        }

        public static JObject Build(string model, double[] position, double angleDeg, double[] surveyInternal, double[] surveyShared, double[] baseInternal, double[] baseShared,
            IEnumerable<KeyPointFloor> floors)
        {
            return new JObject
            {
                ["format"] = FormatText, ["formatVersion"] = FormatVersion, ["model"] = model, ["lengthUnit"] = "m", ["angleUnit"] = "deg",
                ["projectPosition"] = new JObject { ["eastWest"] = position[0], ["northSouth"] = position[1], ["elevation"] = position[2], ["angleDeg"] = angleDeg },
                ["surveyPoint"] = new JObject { ["position"] = Point(surveyInternal), ["sharedPosition"] = Point(surveyShared) },
                ["basePoint"] = new JObject { ["position"] = Point(baseInternal), ["sharedPosition"] = Point(baseShared) },
                ["floors"] = new JArray(floors.OrderBy(floor => floor.ElementId).Select(floor =>
                {
                    string unit, number;
                    return new JObject
                    {
                        ["elementId"] = floor.ElementId, ["typeName"] = floor.TypeName, ["plotText"] = floor.PlotText,
                        // A null string set as a value is a JValue of type String and was counted as a plot, so a floor of no plot is the JSON null
                        ["plot"] = PlotParts.TryParse(floor.PlotText, out unit, out number) ? new JValue(number) : JValue.CreateNull(),
                        ["corners"] = new JArray(floor.Corners.Select(Point)), ["sharedCorners"] = new JArray(floor.SharedCorners.Select(Point)),
                    };
                })),
            };
        }

        // The floors of a key points file whose plot is a number, built in memory or read back
        public static int WithPlot(JObject file)
        {
            return (file["floors"] as JArray ?? new JArray()).Count(item => item["plot"] != null && item["plot"].Type == JTokenType.String && ((string)item["plot"]).Length > 0);
        }

        // The line of the step keyPoints: the floors and those with a plot
        public static string Summary(JObject file)
        {
            return "floors " + (file["floors"] as JArray ?? new JArray()).Count + ", with a plot " + WithPlot(file);
        }

        // The file text, every double by ExactDouble.Text
        public static string Text(JObject file)
        {
            return file.ToString(Formatting.Indented, new ExactDouble.Converter());
        }

        // A key points file read as JSON, each number with a point read by ExactDouble as the nearest double of its text, where double.Parse
        // of net48 can take one unit of the last bit off, refused when it is no file of the step keyPoints in metres and degrees
        public static JObject PointsOf(string text)
        {
            JObject found;
            using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal })
            {
                found = JObject.Load(reader);
            }
            foreach (JValue value in found.Descendants().OfType<JValue>().Where(item => item.Type == JTokenType.Float).ToList())
            {
                value.Value = ExactDouble.Parse(((decimal)value.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if ((string)found["format"] != FormatText || (string)found["lengthUnit"] != "m" || (string)found["angleUnit"] != "deg")
            {
                throw new InvalidDataException("the file is no key points file of the read job in m and deg, its format is " + ((string)found["format"] ?? "none") + " (R353)");
            }
            return found;
        }
    }
}
