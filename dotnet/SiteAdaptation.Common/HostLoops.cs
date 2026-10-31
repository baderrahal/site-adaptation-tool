using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R404, D19. The loops the test host reads of each prototype for its corner compare after 7, its own read apart from ShapeCache of
    // the add-in, kept in one JSON file across runs by the SHA-256 of the prototype file. A 2021 prototype takes about 100 s to open in
    // Revit 2024, so each file is read once, and the file is written again after each new loop so a step cut short keeps what it read
    public sealed class HostLoopCache
    {
        public const string Format = "site-adaptation-host-loops";
        // Raised whenever the read of the host gives another loop, so every kept loop is read again
        public const int ReaderVersion = 1;
        public const string DefaultFile = "prototype_loops.json";
        private const string WriteId = "hostloops";

        private readonly string file;
        private readonly Func<string, string> hash;
        private readonly List<KeyValuePair<string, List<double[]>>> order = new List<KeyValuePair<string, List<double[]>>>();
        private readonly Dictionary<string, List<double[]>> bySha = new Dictionary<string, List<double[]>>(StringComparer.Ordinal);
        // The corner compare asks for the loop of every candidate plot, so a file is hashed once per run
        private readonly Dictionary<string, string> shaByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);

        private HostLoopCache(string file, Func<string, string> hash)
        {
            this.file = file;
            this.hash = hash;
        }

        // The loops the file held when it was loaded
        public int AtStart { get; private set; }

        // The loops read from a prototype since the load
        public int Read { get; private set; }

        public static HostLoopCache Load(string file, Func<string, string> hash)
        {
            var cache = new HostLoopCache(file, hash);
            foreach (KeyValuePair<string, List<double[]>> entry in Kept(file))
            {
                cache.Keep(entry.Key, entry.Value, null);
            }
            cache.AtStart = cache.order.Count;
            return cache;
        }

        public List<double[]> Get(string prototypePath, Func<string, List<double[]>> read)
        {
            if (!shaByPath.TryGetValue(prototypePath, out string sha))
            {
                sha = hash(prototypePath);
                shaByPath[prototypePath] = sha;
            }
            if (bySha.TryGetValue(sha, out List<double[]> known))
            {
                return known;
            }
            List<double[]> loop = read(prototypePath);
            if (hash(prototypePath) != sha)
            {
                throw new IOException(Path.GetFileName(prototypePath) + " changed while its loop was read (D19)");
            }
            Keep(sha, loop, Path.GetFileName(prototypePath));
            Read++;
            Write();
            return loop;
        }

        private void Keep(string sha, List<double[]> loop, string name)
        {
            order.Add(new KeyValuePair<string, List<double[]>>(sha, loop));
            bySha[sha] = loop;
            if (name != null)
            {
                names[sha] = name;
            }
        }

        private void Write()
        {
            var json = new JObject
            {
                ["format"] = Format,
                ["readerVersion"] = ReaderVersion,
                ["loops"] = new JArray(order.Select(entry => new JObject
                {
                    ["sha256"] = entry.Key,
                    ["file"] = names.TryGetValue(entry.Key, out string name) ? name : null,
                    ["points"] = new JArray(entry.Value.Select(point => new JArray(point[0], point[1]))),
                })),
            };
            WorkFiles.WriteText(file, WriteId, json.ToString(Formatting.None));
        }

        // A file of another format or reader version, or one that does not read, keeps nothing and is written over by the next loop.
        // An entry with no ring of 3 corners is dropped, so its prototype is read again
        private static List<KeyValuePair<string, List<double[]>>> Kept(string file)
        {
            var kept = new List<KeyValuePair<string, List<double[]>>>();
            if (!File.Exists(file))
            {
                return kept;
            }
            try
            {
                JObject json = JObject.Parse(WorkFiles.ReadText(file));
                if ((string)json["format"] != Format || (int?)json["readerVersion"] != ReaderVersion || !(json["loops"] is JArray loops))
                {
                    return kept;
                }
                foreach (JToken entry in loops)
                {
                    string sha = (string)entry["sha256"];
                    if (string.IsNullOrEmpty(sha) || !(entry["points"] is JArray points))
                    {
                        continue;
                    }
                    List<double[]> loop = points.Select(point => new[] { (double)point[0], (double)point[1] }).ToList();
                    if (loop.Count >= 3 && kept.All(each => each.Key != sha))
                    {
                        kept.Add(new KeyValuePair<string, List<double[]>>(sha, loop));
                    }
                }
                return kept;
            }
            catch (Exception failure) when (failure is IOException || failure is JsonException || failure is InvalidCastException || failure is ArgumentException
                || failure is NullReferenceException || failure is InvalidOperationException)
            {
                return new List<KeyValuePair<string, List<double[]>>>();
            }
        }
    }
}
