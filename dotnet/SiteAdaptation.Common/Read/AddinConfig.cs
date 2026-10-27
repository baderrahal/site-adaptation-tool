using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R345, R347. The addin block of config/project.json (docs/design/06-settings.md) that the worker and the tests read: the
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

        // The config as json.loads of Python reads it (PyJson), so a text stays a text and -1E400 is -Infinity on net48 as on net8.0, where
        // Newtonsoft took a date text for a date and threw on -1E400 on net48 only, and a value refused is written as json.dumps writes it (H2)
        public static AddinConfig Load(string path)
        {
            JObject root = PyJson.Parse(WorkFiles.ReadText(path)) as JObject ?? throw new InvalidDataException("the config is no JSON object (R347): " + path);
            return new AddinConfig(root, path);
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
            return measured != null ? (double)measured["peakMiB"] : ScaledPeakMiB(files, bytes);
        }

        // R347, the review of PR 54. The peak of a drawing with no measured peak scaled by its size from the measured ones, never the
        // largest known: the measured peaks by size made to rise with it, each at least the peak of a smaller file, then the smallest
        // peak under the smallest size, the line between the two sizes around it, and the largest peak per byte over the largest size
        private static double ScaledPeakMiB(JArray files, long bytes)
        {
            var points = files.Select(file => new[] { (double)(long)file["bytes"], (double)file["peakMiB"] }).OrderBy(point => point[0]).ThenBy(point => point[1]).ToList();
            double rising = 0;
            foreach (double[] point in points)
            {
                rising = System.Math.Max(rising, point[1]);
                point[1] = rising;
            }
            if (bytes <= points[0][0])
            {
                return points[0][1];
            }
            for (int index = 1; index < points.Count; index++)
            {
                double[] low = points[index - 1], high = points[index];
                if (bytes <= high[0])
                {
                    return high[0] == low[0] ? high[1] : low[1] + (bytes - low[0]) * (high[1] - low[1]) / (high[0] - low[0]);
                }
            }
            double[] largest = points[points.Count - 1];
            return largest[1] * bytes / largest[0];
        }

        // R347, Q083. The peak private memory in MiB of a whole command, addin.workerPeakMiB.commands, null for a command it names none.
        // A value that is no finite number above 0 is refused
        public double? WorkerCommandPeakMiB(string command)
        {
            JToken value = ((addin["workerPeakMiB"] as JObject)?["commands"] as JObject)?[command ?? ""];
            if (value == null)
            {
                return null;
            }
            double peak = value.Type == JTokenType.Integer || value.Type == JTokenType.Float ? value.Value<double>() : double.NaN;
            if (double.IsNaN(peak) || double.IsInfinity(peak) || peak <= 0)
            {
                throw new InvalidDataException("addin.workerPeakMiB.commands." + command + " is " + value.ToString(Newtonsoft.Json.Formatting.None) + ", a peak is a number of MiB above 0 (R347)");
            }
            return peak;
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

        // R347. The shortest time limit a launch takes, addin.workerTimeLimitMinS, so a limit typed too short never kills a sound run
        public double WorkerTimeLimitMinS
        {
            get
            {
                JToken value = addin["workerTimeLimitMinS"];
                double seconds = value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) ? value.Value<double>() : double.NaN;
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                {
                    throw new InvalidDataException("the config holds no addin.workerTimeLimitMinS of a number of seconds above 0, the shortest time limit of a run (R347)");
                }
                return seconds;
            }
        }

        // R345. The tolerance before rounding of a kind of field, null for a kind compared exactly. True would read as 1 and NaN
        // passes every comparison, so a tolerance is a finite number of zero or more, as python/parity.py takes it
        public double? ParityTolerance(string kind)
        {
            JObject kinds = Parity()["kinds"] as JObject;
            if (kinds == null || !kinds.ContainsKey(kind))
            {
                throw new InvalidDataException("addin.parity.kinds names no kind " + kind + " (R345)");
            }
            return Tolerance(kinds, kind);
        }

        // R345. A tolerance of addin.parity.kinds, its value written as json.dumps of Python writes it, so both languages give one line for a
        // text, a list and an object as for a float (G3, H2 of the fifth review of PR 55), parity.tolerance_of
        private static double? Tolerance(JObject kinds, string kind)
        {
            JToken value = kinds[kind];
            if (value.Type == JTokenType.Null)
            {
                return null;
            }
            double tolerance = value.Type == JTokenType.Integer || value.Type == JTokenType.Float ? value.Value<double>() : double.NaN;
            if (double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance < 0)
            {
                throw new InvalidDataException("addin.parity.kinds." + kind + " is " + PyJson.Dumps(value)
                    + ", a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)");
            }
            return tolerance;
        }

        // R345. Every tolerance the fields of addin.parity.steps name, held in their order with the lines of parity.tolerances, which
        // read_steps.py holds before any read, so a read of steps refuses what Python refuses (H2 of the fifth review of PR 55)
        public void ParityTolerances()
        {
            JObject parity = addin["parity"] as JObject;
            JObject kinds = parity?["kinds"] as JObject, steps = parity?["steps"] as JObject;
            if (kinds == null || steps == null)
            {
                throw new InvalidDataException("config holds no addin.parity block with kinds and steps (R345)");
            }
            foreach (JProperty step in steps.Properties())
            {
                foreach (JProperty field in ((JObject)step.Value).Properties())
                {
                    JToken kind = (field.Value as JObject)?["kind"];
                    if (kind == null || kind.Type != JTokenType.String || !kinds.ContainsKey((string)kind))
                    {
                        throw new InvalidDataException("addin.parity.steps." + step.Name + "." + field.Name + " names the kind " + SiteClusters.PyText(kind)
                            + ", which addin.parity.kinds does not hold (R345)");
                    }
                    double? tolerance = Tolerance(kinds, (string)kind);
                    if (tolerance.HasValue != (((JObject)field.Value)["decimals"] != null))
                    {
                        throw new InvalidDataException("addin.parity.steps." + step.Name + "." + field.Name
                            + ": a kind with a tolerance has decimals and a kind compared exactly has none (R345)");
                    }
                }
            }
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
