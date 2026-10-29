using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Tags
{
    // R368, 03a Element tags. The kinds of element a button places
    public static class PlacedKinds
    {
        public const string Link = "link";
        public const string Generic = "generic";
        public const string Yard = "yard";
        public const string Ramp = "ramp";
        public const string BoundaryWall = "boundaryWall";
        public static readonly string[] All = { Link, Generic, Yard, Ramp, BoundaryWall };
    }

    // One version of the schema SiteAdaptation_Placed, its GUID, name and field names, all strings
    public sealed class SchemaVersion
    {
        public SchemaVersion(int version, Guid guid, string name, IReadOnlyList<string> fields)
        {
            if (!PlacedSchema.IsName(name) || fields.Any(field => !PlacedSchema.IsName(field)))
            {
                throw new ArgumentException("a schema name and a field name hold letters, digits and underscore only: " + name + " " + string.Join(" ", fields));
            }
            Version = version;
            Guid = guid;
            Name = name;
            Fields = fields.ToArray();
        }

        public int Version { get; }
        public Guid Guid { get; }
        public string Name { get; }
        public IReadOnlyList<string> Fields { get; }
    }

    // R368, 03a Schema versions. Every version ever used stays in Versions, the last is the one a run writes. A new version takes a new
    // GUID and the name of NameOf, so an element an earlier add-in tagged is still found and never placed twice
    public static class PlacedSchema
    {
        public const string BaseName = "SiteAdaptation_Placed";
        public const string VendorId = SettingsSchema.VendorId;

        public const string Plot = "plot";
        public const string Kind = "kind";
        public const string Part = "part";
        public const string Run = "run";
        public const string Profile = "profile";
        public const string PlacedId = "placedId";
        public const string Placed = "placed";

        // SchemaBuilder throws on a name with a dot, such as SiteAdaptation.Placed
        private static readonly Regex Name = new Regex("^[A-Za-z][A-Za-z0-9_]*$");

        public static readonly IReadOnlyList<SchemaVersion> Versions = new[]
        {
            new SchemaVersion(1, new Guid("4437df95-47ea-4c18-98ba-8aa3d97386ac"), BaseName, new[] { Plot, Kind, Part, Run, Profile, PlacedId, Placed }),
        };

        public static SchemaVersion Latest
        {
            get { return Versions[Versions.Count - 1]; }
        }

        public static string NameOf(int version)
        {
            return version == 1 ? BaseName : BaseName + "_" + version.ToString(CultureInfo.InvariantCulture);
        }

        public static bool IsName(string name)
        {
            return name != null && Name.IsMatch(name);
        }
    }

    // The key a second run finds an element by: plot, kind and part, the edge of a wall, else empty
    public sealed class TagKey : IEquatable<TagKey>
    {
        public TagKey(string plot, string kind, string part)
        {
            if (string.IsNullOrEmpty(plot))
            {
                throw new ArgumentException("a tag names its plot", nameof(plot));
            }
            if (!PlacedKinds.All.Contains(kind))
            {
                throw new ArgumentException("the kind " + kind + " is not one of " + string.Join(", ", PlacedKinds.All), nameof(kind));
            }
            Plot = plot;
            Kind = kind;
            Part = part ?? "";
        }

        public string Plot { get; }
        public string Kind { get; }
        public string Part { get; }

        public bool Equals(TagKey other)
        {
            return other != null && string.Equals(Plot, other.Plot, StringComparison.Ordinal) && string.Equals(Kind, other.Kind, StringComparison.Ordinal) && string.Equals(Part, other.Part, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TagKey);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Plot + "\n" + Kind + "\n" + Part);
        }

        public override string ToString()
        {
            return "plot " + Plot + " " + Kind + (Part.Length == 0 ? "" : " " + Part);
        }
    }

    // R368, R385. The values the tool gave an element, each rounded by R385 at the decimals of R2, so two are equal when they are equal
    // at those decimals: for a link its internal origin, rotation and mirror flag, for a floor its outline and top, for a wall its
    // line, base and top
    public sealed class PlacedValues
    {
        private readonly SortedDictionary<string, IReadOnlyList<double>> values = new SortedDictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal);

        public PlacedValues(IDictionary<string, double[]> values)
        {
            foreach (KeyValuePair<string, double[]> value in values)
            {
                if (value.Value == null || value.Value.Any(number => double.IsNaN(number) || double.IsInfinity(number)))
                {
                    throw new ArgumentException("the placed value " + value.Key + " holds no finite numbers");
                }
                this.values[value.Key] = value.Value.Select(JsonOut.Round3).ToArray();
            }
        }

        public IReadOnlyDictionary<string, IReadOnlyList<double>> Values
        {
            get { return values; }
        }

        public bool SameAs(PlacedValues other)
        {
            return Differences(other).Count == 0;
        }

        // The names of the values that differ, a value one side lacks included. Names only, so a list never holds a coordinate
        public IReadOnlyList<string> Differences(PlacedValues other)
        {
            return values.Keys.Union(other.values.Keys).OrderBy(name => name, StringComparer.Ordinal)
                .Where(name => !values.ContainsKey(name) || !other.values.ContainsKey(name) || !values[name].SequenceEqual(other.values[name]))
                .ToList();
        }

        public string ToText()
        {
            return "{" + string.Join(",", values.Select(value => JsonConvert.ToString(value.Key) + ":[" + string.Join(",", value.Value.Select(number => number.ToString("0.###", CultureInfo.InvariantCulture))) + "]")) + "}";
        }

        public static PlacedValues Parse(string text)
        {
            try
            {
                var read = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (JProperty value in JObject.Parse(text).Properties())
                {
                    if (!(value.Value is JArray numbers) || numbers.Any(number => number.Type != JTokenType.Integer && number.Type != JTokenType.Float))
                    {
                        throw new InvalidDataException("the placed value " + value.Name + " is no list of numbers");
                    }
                    read[value.Name] = numbers.Select(number => number.Value<double>()).ToArray();
                }
                return new PlacedValues(read);
            }
            catch (JsonException error)
            {
                throw new InvalidDataException("the placed values are no JSON object: " + error.Message, error);
            }
        }
    }

    // R368. The entity of the schema SiteAdaptation_Placed on an element a button placed
    public sealed class PlacedTag
    {
        public PlacedTag(int version, TagKey key, string run, string profile, string placedId, PlacedValues placed)
        {
            Version = version;
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Run = run ?? "";
            Profile = profile ?? "";
            PlacedId = placedId ?? "";
            Placed = placed ?? throw new ArgumentNullException(nameof(placed));
        }

        // The schema version the entity was read from
        public int Version { get; }
        public TagKey Key { get; }
        public string Run { get; }
        public string Profile { get; }

        // The UniqueId of the element when the tool placed it, so a copy that carried the entity is told from the element placed
        public string PlacedId { get; }
        public PlacedValues Placed { get; }

        public static PlacedTag ForPlacement(PlanItem item, string run, string profile, string uniqueId)
        {
            return new PlacedTag(PlacedSchema.Latest.Version, item.Key, run, profile, uniqueId, item.Plan);
        }

        // The fields of the latest version, every one a string
        public IReadOnlyDictionary<string, string> ToFields()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { PlacedSchema.Plot, Key.Plot },
                { PlacedSchema.Kind, Key.Kind },
                { PlacedSchema.Part, Key.Part },
                { PlacedSchema.Run, Run },
                { PlacedSchema.Profile, Profile },
                { PlacedSchema.PlacedId, PlacedId },
                { PlacedSchema.Placed, Placed.ToText() },
            };
        }

        // An entity of any listed version, read by the field names of that version
        public static PlacedTag FromFields(SchemaVersion version, IReadOnlyDictionary<string, string> fields)
        {
            foreach (string name in new[] { PlacedSchema.Plot, PlacedSchema.Kind, PlacedSchema.Part, PlacedSchema.Run, PlacedSchema.Profile, PlacedSchema.PlacedId, PlacedSchema.Placed })
            {
                if (!version.Fields.Contains(name) || !fields.TryGetValue(name, out string value) || value == null)
                {
                    throw new InvalidDataException("the entity of " + version.Name + " holds no field " + name);
                }
            }
            TagKey key;
            try
            {
                key = new TagKey(fields[PlacedSchema.Plot], fields[PlacedSchema.Kind], fields[PlacedSchema.Part]);
            }
            catch (ArgumentException error)
            {
                throw new InvalidDataException("the entity of " + version.Name + ": " + error.Message, error);
            }
            return new PlacedTag(version.Version, key, fields[PlacedSchema.Run], fields[PlacedSchema.Profile], fields[PlacedSchema.PlacedId], PlacedValues.Parse(fields[PlacedSchema.Placed]));
        }
    }
}
