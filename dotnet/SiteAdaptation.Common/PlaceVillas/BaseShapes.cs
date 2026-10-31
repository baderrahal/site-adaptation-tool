using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R404. The base boundary of one prototype in its own internal coordinates, metres in plan, every corner in ring order with the first
    // not repeated
    public sealed class BaseShape
    {
        public BaseShape(string source, IReadOnlyList<double[]> points)
        {
            Source = source;
            Points = points;
        }

        public string Source { get; }
        public IReadOnlyList<double[]> Points { get; }
    }

    // R404. The Revit side opens a prototype file and gives the outer loop of the element config names, every corner of its curves
    public interface IBaseShapeReader
    {
        IReadOnlyList<double[]> Read(string prototypePath, LightModeConfig config);
    }

    // R404, open point 5 of 11-light-mode.md. A shape is read once per prototype file and kept in the work folder by the SHA-256 of the
    // file, the version of the reader and the source config names, so a new file, a changed reader or an answer of Q164 reads it again
    public sealed class ShapeCache
    {
        // Raised whenever the Revit reader of the shape changes what it gives, so every cached shape is read again
        public const int ReaderVersion = 1;
        public const string Folder = "base_shapes";
        public const string Format = "site-adaptation-base-shape";

        private readonly string folder;
        private readonly IBaseShapeReader reader;
        private readonly Func<string, string> hash;
        private readonly Dictionary<string, BaseShape> held = new Dictionary<string, BaseShape>(StringComparer.Ordinal);

        public ShapeCache(string workFolder, IBaseShapeReader reader, Func<string, string> hash)
        {
            folder = Path.Combine(workFolder, Folder);
            this.reader = reader;
            this.hash = hash;
        }

        public BaseShape Get(string prototypePath, LightModeConfig config)
        {
            string source = config.BaseBoundary == BaseBoundarySources.FloorType ? config.BaseBoundary + "-" + config.BaseBoundaryFloorType : config.BaseBoundary;
            string sha = hash(prototypePath);
            string key = sha + "-v" + ReaderVersion.ToString(CultureInfo.InvariantCulture) + "-" + Safe(source);
            if (held.TryGetValue(key, out BaseShape known))
            {
                return known;
            }
            string file = Path.Combine(folder, key + ".json");
            BaseShape shape = Cached(file, sha, source);
            if (shape == null)
            {
                shape = new BaseShape(source, Checked(reader.Read(prototypePath, config), prototypePath));
                var json = new JObject
                {
                    ["format"] = Format, ["readerVersion"] = ReaderVersion, ["sha256"] = sha, ["source"] = source,
                    ["points"] = new JArray(shape.Points.Select(point => new JArray(point[0], point[1]))),
                };
                Directory.CreateDirectory(folder);
                WorkFiles.WriteText(file, "cache", json.ToString(Newtonsoft.Json.Formatting.None));
            }
            held[key] = shape;
            return shape;
        }

        private static BaseShape Cached(string file, string sha, string source)
        {
            if (!File.Exists(file))
            {
                return null;
            }
            try
            {
                JObject json = JObject.Parse(WorkFiles.ReadText(file));
                if ((string)json["format"] != Format || (int?)json["readerVersion"] != ReaderVersion || (string)json["sha256"] != sha || (string)json["source"] != source)
                {
                    return null;
                }
                return new BaseShape(source, Checked(((JArray)json["points"]).Select(point => new[] { (double)point[0], (double)point[1] }).ToList(), file));
            }
            catch (Exception failure) when (failure is IOException || failure is InvalidDataException || failure is Newtonsoft.Json.JsonException || failure is InvalidCastException || failure is NullReferenceException)
            {
                // A cache file that does not read is read again from the prototype, never trusted
                return null;
            }
        }

        // The base is one closed ring with at least three corners whose edges meet only at their ends, as R354 holds a footprint block
        private static IReadOnlyList<double[]> Checked(IReadOnlyList<double[]> points, string where)
        {
            List<double[]> ring = FaceRings.Clean((points ?? new double[0][]).Where(point => point != null && point.Length >= 2).Select(point => new[] { point[0], point[1] }));
            if (ring.Count < 3)
            {
                throw new InvalidDataException("the base boundary of " + Path.GetFileName(where) + " holds " + ring.Count + " corners, a ring needs 3 (R404)");
            }
            if (!FaceRings.IsSimple(ring))
            {
                throw new InvalidDataException("the base boundary of " + Path.GetFileName(where) + " is not a simple ring, two of its edges meet (R404)");
            }
            return ring;
        }

        private static string Safe(string text)
        {
            return new string(text.Select(letter => char.IsLetterOrDigit(letter) ? letter : '_').ToArray());
        }
    }

    // R404. The base of a villa in shared plan coordinates: the local Y flipped when mirrored, turned by rotationDeg, moved to the
    // insert, the transform PlacementMath.Steps gives the link
    public static class ShapeMath
    {
        public static List<double[]> Place(IEnumerable<double[]> shape, double[] insert, double rotationDeg, bool mirrored)
        {
            double turn = UnitMath.DegreesToRadians(rotationDeg);
            double cos = Math.Cos(turn), sin = Math.Sin(turn);
            return shape.Select(point =>
            {
                double x = point[0], y = mirrored ? -point[1] : point[1];
                return new[] { insert[0] + x * cos - y * sin, insert[1] + x * sin + y * cos };
            }).ToList();
        }
    }
}
