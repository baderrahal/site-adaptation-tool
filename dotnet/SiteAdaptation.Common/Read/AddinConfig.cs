using System.IO;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R347. The addin block of config/project.json (docs/addin-design.md section 6) that the worker and the tests read: the
    // measured peak of each large input and the time limit of each command, and the floor siteBuild.minFreeGB
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

        // The measured peak private memory of the read of an input role in MiB, null for an input with none measured
        public double? WorkerPeakMiB(string role)
        {
            JToken value = (addin["workerPeakMiB"] as JObject)?[role];
            return value == null || value.Type == JTokenType.Null ? (double?)null : value.Value<double>();
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
    }
}
