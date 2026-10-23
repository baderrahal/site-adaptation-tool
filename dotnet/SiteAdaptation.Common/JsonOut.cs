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
            return JsonConvert.SerializeObject(payload, Settings(Formatting.Indented));
        }

        public static JsonSerializerSettings Settings(Formatting formatting)
        {
            return Build(formatting, true);
        }

        // Used to write one parameter detail on one line
        private static readonly JsonSerializerSettings OneLine = Build(Formatting.None, false);

        private static JsonSerializerSettings Build(Formatting formatting, bool detailOnOneLine)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false },
                },
            };
            settings.Converters.Add(new Round3Converter());
            settings.Converters.Add(new InlinePointConverter());
            if (detailOnOneLine)
            {
                settings.Converters.Add(new InlineDetailConverter());
            }
            return settings;
        }

        public static void Write(string path, object payload)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, ToJson(payload), new UTF8Encoding(false));
        }

        // Writes {"source": ..., "<name>": [ item per line ]} one item at a time, so a large drawing never sits in memory twice
        public sealed class ArrayWriter : IDisposable
        {
            private readonly StreamWriter file;
            private readonly JsonTextWriter writer;
            private readonly JsonSerializer serializer;

            public int Count { get; private set; }

            public ArrayWriter(string path, string source, string arrayName)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                file = new StreamWriter(path, false, new UTF8Encoding(false));
                writer = new JsonTextWriter(file) { Formatting = Formatting.None };
                serializer = JsonSerializer.Create(Settings(Formatting.None));
                writer.WriteStartObject();
                writer.WritePropertyName("source");
                writer.WriteValue(source);
                writer.WritePropertyName(arrayName);
                writer.WriteStartArray();
            }

            public void Add(object item)
            {
                writer.WriteWhitespace("\n");
                serializer.Serialize(writer, item);
                Count++;
            }

            public void Dispose()
            {
                writer.WriteWhitespace("\n");
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();
                file.Dispose();
            }
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

        // A file holds a hundred thousand parameter details, so each one is a single line and not seven
        private sealed class InlineDetailConverter : JsonConverter
        {
            public override bool CanRead { get { return false; } }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(ParameterDetail);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteRawValue(JsonConvert.SerializeObject(value, OneLine));
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
