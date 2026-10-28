using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R347. A refusal or a failure of the worker with its exit code, thrown by the runner and by WorkerContext and caught by the runner
    internal sealed class WorkerStop : Exception
    {
        public readonly int Code;

        public WorkerStop(int code, string message) : base(message)
        {
            Code = code;
        }
    }

    // R347. What a command of the worker gets: the request, its issues and counts, each input by its role with the free memory
    // checked against the measured peak of that file plus the floor right before the read, and against the rest of the whole peak of
    // the command when config names one (Q083), and each output written by its
    // temporary name with the run stamp. The runner moves the outputs onto their names only once every check of the run passed,
    // and deletes them under their temporary names when it did not
    public sealed class WorkerContext
    {
        private readonly AddinConfig config;
        private readonly Func<double?> freeMiB;
        private readonly Func<double?> privateMiB;
        private readonly List<KeyValuePair<string, string>> written = new List<KeyValuePair<string, string>>();

        internal WorkerContext(WorkerRequest request, WorkerAnswer answer, AddinConfig config, Func<double?> freeMiB, Func<double?> privateMiB)
        {
            Request = request;
            Issues = answer.Issues;
            Counts = answer.Counts;
            this.config = config;
            this.freeMiB = freeMiB;
            this.privateMiB = privateMiB;
        }

        public WorkerRequest Request { get; }

        public List<string> Issues { get; }

        public Dictionary<string, int> Counts { get; }

        // The temporary name and the name of each output written so far, in the order written
        internal IReadOnlyList<KeyValuePair<string, string>> Written => written;

        // The path of the input of a role, which the command reads next. For a large read the free memory is read now and the read
        // refused with exit 3 when it is under the measured peak of that file plus the floor, and for a command with a whole peak when
        // it is under the rest of that peak plus the floor, so memory Revit took since the start of the run counts
        public string Input(string role)
        {
            WorkerFile input = Request.Inputs.FirstOrDefault(item => item.Role == role);
            if (input == null)
            {
                throw new InvalidDataException("the request names no input " + role);
            }
            string refusal = WorkerRunner.MemoryRefusal(input, config, freeMiB);
            if (refusal != null)
            {
                throw new WorkerStop(WorkerExit.Memory, refusal);
            }
            // Q083. Every input of a command whose whole peak config names, the road surfaces and the workbooks with the drawings, is
            // held against the rest of that peak
            double? whole = config.WorkerCommandPeakMiB(Request.Command);
            if (whole.HasValue)
            {
                refusal = WorkerMemory.CommandRefusal(Request.Command, freeMiB(), whole.Value, privateMiB(), config.MinFreeGiB);
                if (refusal != null)
                {
                    throw new WorkerStop(WorkerExit.Memory, refusal);
                }
            }
            return input.Path;
        }

        // R347, R348. A refusal of the request found by a command before it used anything of an input, such as a drawing whose units
        // are not Meters: the run ends with exit 2 and the message, the command throws what this gives
        public Exception Refusal(string message)
        {
            return new WorkerStop(WorkerExit.Refused, message);
        }

        // An output of the run, in the output or the steps folder of the request only, where the kill finds its temporary name, with
        // the run stamp of RunStamp, written by its temporary name. Its name is given back, the file is there once the run is done
        public string WriteJson(string folder, string name, JObject content)
        {
            return Write(folder, name, (writer, exact, stamp) =>
            {
                bool placed = false;
                foreach (JProperty item in content.Properties())
                {
                    if (item.Name == RunStamp.Key)
                    {
                        writer.WritePropertyName(item.Name);
                        stamp.WriteTo(writer, exact);
                        placed = true;
                        continue;
                    }
                    item.WriteTo(writer, exact);
                }
                if (!placed)
                {
                    writer.WritePropertyName(RunStamp.Key);
                    stamp.WriteTo(writer, exact);
                }
            });
        }

        // R345, Q107. An output whose keys write gives to the writer with the converter of exact doubles, such as a step file by StepFile.Write,
        // the run stamp after them, so no object of the whole output is made first
        public string WriteJson(string folder, string name, Action<JsonWriter, JsonConverter> write)
        {
            return Write(folder, name, (writer, exact, stamp) =>
            {
                write(writer, exact);
                writer.WritePropertyName(RunStamp.Key);
                stamp.WriteTo(writer, exact);
            });
        }

        private string Write(string folder, string name, Action<JsonWriter, JsonConverter, JObject> body)
        {
            string full = Path.GetFullPath(folder ?? "");
            bool allowed = new[] { Request.OutputFolder, Request.StepsFolder }.Any(place => !string.IsNullOrEmpty(place) && string.Equals(Path.GetFullPath(place).TrimEnd(Path.DirectorySeparatorChar), full.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
            if (!allowed || name != Path.GetFileName(name) || WorkFiles.IsTemporary(name))
            {
                throw new InvalidOperationException("an output of the worker goes only into the output or the steps folder of the request, under a plain name: " + Path.Combine(folder ?? "", name));
            }
            JObject stamp = RunStamp.Of(Request);
            string path = Path.Combine(full, name);
            Directory.CreateDirectory(full);
            string temporary = WorkFiles.TemporaryName(path, Request.RunId);
            // R350. Every double written so it reads back as the same double, which the format R of net48 does not give. Q107: streamed to the
            // file, the stamp in place of a run key of the content or after its last key, the text a copy of the content with the stamp set
            // gives, as that copy and the text of the whole file held a step file of a whole build twice more in memory
            var exact = new ExactDouble.Converter();
            using (var file = new StreamWriter(temporary, false, new System.Text.UTF8Encoding(false)))
            using (var writer = new JsonTextWriter(file) { Formatting = Formatting.Indented })
            {
                writer.WriteStartObject();
                body(writer, exact, stamp);
                writer.WriteEndObject();
            }
            written.Add(new KeyValuePair<string, string>(temporary, path));
            return path;
        }
    }

    // R347. Every output of the worker holds the run id, the code that wrote it, the SHA-256 of the config it read and the input
    // files with their SHA-256 it was made from, so a reader refuses an output whose inputs changed since it was written and the
    // parity of R345 names which run and which build wrote each step file
    public static class RunStamp
    {
        public const string Key = "run";

        // SiteAdaptation.Common and its informational version, which the SDK writes with the commit the build was made from
        public static string Code
        {
            get
            {
                Assembly common = typeof(RunStamp).Assembly;
                string version = common.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? common.GetName().Version.ToString();
                return "SiteAdaptation.Common " + version;
            }
        }

        public static JObject Of(WorkerRequest request)
        {
            var inputs = new JArray();
            foreach (WorkerFile input in request.Inputs ?? new List<WorkerFile>())
            {
                inputs.Add(new JObject { ["role"] = input.Role, ["path"] = input.Path, ["sha256"] = input.Sha256 });
            }
            string config = (request.Inputs ?? new List<WorkerFile>()).FirstOrDefault(input => input.Role == "config")?.Sha256;
            return new JObject { ["runId"] = request.RunId, ["code"] = Code, ["configSha256"] = config, ["inputs"] = inputs };
        }

        // Null when every input the stamp names is on disk with its SHA-256, else why not. A stamp of no input could never be stale
        public static string Refusal(JObject output)
        {
            JArray inputs = (output?[Key] as JObject)?["inputs"] as JArray;
            if (inputs == null)
            {
                return "the output holds no run stamp with its inputs (R347)";
            }
            if (inputs.Count == 0)
            {
                return "the run stamp of the output names no input, so a change of its inputs could not be seen (R347)";
            }
            foreach (JToken input in inputs)
            {
                string role = (string)input["role"], path = (string)input["path"], sha = (string)input["sha256"];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return "the input " + role + " of the output is not on disk: " + path;
                }
                if (!string.Equals(FileHash.Sha256(path), sha, StringComparison.OrdinalIgnoreCase))
                {
                    return "the input " + role + " of the output changed since the output was written, its SHA-256 is not the one of the stamp: " + path;
                }
            }
            return null;
        }

        // An output of the worker read back, refused when an input it was made from changed or is gone
        public static JObject Read(string path)
        {
            JObject output = JObject.Parse(WorkFiles.ReadText(path));
            string refusal = Refusal(output);
            if (refusal != null)
            {
                throw new InvalidDataException(refusal + " (" + path + ")");
            }
            return output;
        }
    }
}
