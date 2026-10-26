using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R345, R347. The addin block of config/project.json (docs/addin-design.md section 6) that the worker and the tests read: the
    // measured peak of each large input, the time limit of each command and the parity tolerances, and the floor siteBuild.minFreeGB
    public sealed class AddinConfig
    {
        private readonly JObject root;
        private readonly JObject addin;

        private AddinConfig(JObject root, string path)
        {
            this.root = root;
            addin = root["addin"] as JObject;
            if (addin == null)
            {
                throw new InvalidDataException("the config holds no addin block (R347): " + path);
            }
        }

        public static AddinConfig Load(string path)
        {
            return new AddinConfig(JObject.Parse(WorkFiles.ReadText(path)), path);
        }

        // The peak private memory in MiB the read of an input file needs, null for an input of no large read. A file of a role that
        // addin.workerPeakMiB.roles names takes the peak measured on the file of its name, without case, and its size in bytes, and a
        // file with no measured peak, another drawing or the same name at another size, the largest peak measured on any
        public double? WorkerPeakMiB(string role, string fileName, long bytes)
        {
            JObject block = addin["workerPeakMiB"] as JObject;
            JArray roles = block?["roles"] as JArray;
            JArray files = block?["files"] as JArray;
            if (roles == null || files == null || files.Count == 0)
            {
                throw new InvalidDataException("the config holds no addin.workerPeakMiB with the roles of a large read and the files measured (R347)");
            }
            if (!roles.Any(item => (string)item == role))
            {
                return null;
            }
            JToken measured = files.FirstOrDefault(file => string.Equals((string)file["name"], fileName, System.StringComparison.OrdinalIgnoreCase) && (long)file["bytes"] == bytes);
            return measured != null ? (double)measured["peakMiB"] : files.Max(file => (double)file["peakMiB"]);
        }

        // R200. The floor of free memory in GiB, siteBuild.minFreeGB
        public double MinFreeGiB
        {
            get
            {
                JToken value = (root["siteBuild"] as JObject)?["minFreeGB"];
                if (value == null)
                {
                    throw new InvalidDataException("the config holds no siteBuild.minFreeGB, the floor of free memory (R200)");
                }
                return value.Value<double>();
            }
        }

        public double WorkerTimeLimitS(string command)
        {
            JToken value = (addin["workerTimeLimitS"] as JObject)?[command];
            if (value == null)
            {
                throw new InvalidDataException("the config holds no addin.workerTimeLimitS." + command + " (R347)");
            }
            return value.Value<double>();
        }

        // R345. The tolerance before rounding of a kind of field, null for a kind compared exactly
        public double? ParityTolerance(string kind)
        {
            JObject kinds = Parity()["kinds"] as JObject;
            if (kinds == null || !kinds.ContainsKey(kind))
            {
                throw new InvalidDataException("addin.parity.kinds names no kind " + kind + " (R345)");
            }
            JToken value = kinds[kind];
            return value.Type == JTokenType.Null ? (double?)null : value.Value<double>();
        }

        // R345. The fields of a step with the kind and the decimals of each, as python/parity.py reads them
        public IReadOnlyList<ParityField> StepFields(string step)
        {
            JObject fields = (Parity()["steps"] as JObject)?[step] as JObject;
            if (fields == null)
            {
                throw new InvalidDataException("addin.parity.steps names no step " + step + " (R345)");
            }
            return fields.Properties().Select(field => new ParityField
            {
                Name = field.Name,
                Kind = (string)field.Value["kind"],
                Decimals = field.Value["decimals"] == null ? (int?)null : field.Value["decimals"].Value<int>(),
            }).ToList();
        }

        private JObject Parity()
        {
            JObject parity = addin["parity"] as JObject;
            if (parity == null)
            {
                throw new InvalidDataException("the config holds no addin.parity block (R345)");
            }
            return parity;
        }
    }

    public sealed class ParityField
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public int? Decimals { get; set; }
    }
}
