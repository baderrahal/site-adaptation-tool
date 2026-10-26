using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R345. The step file of the C# side, the same form as python/parity.py step_file: per plot the raw values, every digit of a
    // double kept, and the written values, each number of a field with decimals rounded by R385
    public static class StepFile
    {
        public const string Format = "siteAdaptationStep";
        public const int FormatVersion = 1;

        public static string Text(string step, string side, IDictionary<string, Dictionary<string, object>> plots, IReadOnlyList<ParityField> fields)
        {
            var byName = fields.ToDictionary(field => field.Name);
            var fieldBlock = new JObject();
            foreach (ParityField field in fields)
            {
                var item = new JObject { ["kind"] = field.Kind };
                if (field.Decimals.HasValue)
                {
                    item["decimals"] = field.Decimals.Value;
                }
                fieldBlock[field.Name] = item;
            }
            var plotBlock = new JObject();
            foreach (KeyValuePair<string, Dictionary<string, object>> plot in plots)
            {
                var raw = new JObject();
                var written = new JObject();
                foreach (KeyValuePair<string, object> value in plot.Value)
                {
                    ParityField field;
                    int? decimals = byName.TryGetValue(value.Key, out field) ? field.Decimals : null;
                    raw[value.Key] = Token(value.Value, null);
                    written[value.Key] = Token(value.Value, decimals);
                }
                plotBlock[plot.Key] = new JObject { ["raw"] = raw, ["written"] = written };
            }
            var file = new JObject { ["format"] = Format, ["formatVersion"] = FormatVersion, ["step"] = step, ["side"] = side, ["fields"] = fieldBlock, ["plots"] = plotBlock };
            return file.ToString(Formatting.Indented);
        }

        public static string Write(string folder, string step, string runId, string text)
        {
            return WorkFiles.WriteText(Path.Combine(folder, "step_" + step + ".json"), runId, text);
        }

        // A double as its 17 digit text, which reads back as the same double on net48 and net8.0, rounded first when decimals are named
        private static JToken Token(object value, int? decimals)
        {
            if (value is double number)
            {
                return Number(number, decimals);
            }
            if (value is double[] numbers)
            {
                return new JArray(numbers.Select(item => Number(item, decimals)));
            }
            return value == null ? JValue.CreateNull() : JToken.FromObject(value);
        }

        private static JToken Number(double value, int? decimals)
        {
            double shown = decimals.HasValue ? Rounding.Fixed(value, decimals.Value) : value;
            if (double.IsNaN(shown) || double.IsInfinity(shown))
            {
                return JValue.CreateNull();
            }
            return new JRaw(shown.ToString("G17", CultureInfo.InvariantCulture));
        }
    }
}
