using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SiteAdaptation.Common
{
    public static class JsonOut
    {
        public const int Decimals = 3;

        // Negative zero would print as -0 and look like a real offset
        public static double Round3(double value)
        {
            double rounded = Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
            return rounded == 0 ? 0 : rounded;
        }

        public static string ToJson(object payload)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false },
                },
            };
            settings.Converters.Add(new Round3Converter());
            settings.Converters.Add(new InlinePointConverter());
            return JsonConvert.SerializeObject(payload, settings);
        }

        public static void Write(string path, object payload)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, ToJson(payload), new UTF8Encoding(false));
        }

        // A point is written on one line so a polyline with many vertices stays small
        private sealed class InlinePointConverter : JsonConverter
        {
            public override bool CanRead { get { return false; } }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(double[]);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var parts = ((double[])value).Select(number => Round3(number).ToString("0.###", CultureInfo.InvariantCulture));
                writer.WriteRawValue("[" + string.Join(", ", parts) + "]");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class Round3Converter : JsonConverter
        {
            public override bool CanRead { get { return false; } }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(double) || objectType == typeof(double?) || objectType == typeof(float);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                double number = Convert.ToDouble(value);
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    writer.WriteNull();
                    return;
                }
                writer.WriteValue(Round3(number));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}
