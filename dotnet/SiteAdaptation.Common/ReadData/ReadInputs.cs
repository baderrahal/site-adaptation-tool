using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Steps;

namespace SiteAdaptation.Common.ReadData
{
    // R375. One input of the read of button 2, known by its role in the request, its full path, its size and its SHA-256
    public sealed class ReadInput
    {
        public ReadInput(string role, string path, long bytes, string sha256)
        {
            Role = role;
            Path = path;
            Bytes = bytes;
            Sha256 = sha256;
        }

        public string Role { get; }
        public string Path { get; }
        public long Bytes { get; }
        public string Sha256 { get; }
    }

    // R375. Every input of the read with the one hash over them and the read steps, or the stops that keep it from starting
    public sealed class ReadPlan
    {
        public ReadPlan(IReadOnlyList<ReadInput> inputs, IReadOnlyList<string> steps, IReadOnlyList<string> stops, string configPath)
        {
            Inputs = inputs;
            Steps = steps;
            Stops = stops;
            ConfigPath = configPath;
            var named = inputs.ToDictionary(input => input.Role, input => input.Sha256, StringComparer.Ordinal);
            named["steps"] = string.Join(",", steps);
            Hash = StepHash.Combine(named);
        }

        public IReadOnlyList<ReadInput> Inputs { get; }
        public IReadOnlyList<string> Steps { get; }
        public IReadOnlyList<string> Stops { get; }
        public string ConfigPath { get; }

        // One SHA-256 over every role and hash and the step list, which names the folder of the read
        public string Hash { get; }
    }

    // R375. The inputs of the worker command read: the files of the pane by their role, each LandXML file of siteBuild.roadSurfaces in
    // the Road surface folder, the layer list beside config, the schedule of each neighbourhood of connect.neighbourhoods in the
    // schedule folder of inputRoot, and the data book files the road levels and the plot table read. Every input by full path and
    // SHA-256, never by name
    public static class ReadInputs
    {
        public const string Config = "config";
        public const string RoadSurface = "roadSurface";
        public const string CadLayers = "cadLayers";

        private static readonly string[] Drawings = { "baseplan", "villaBlocks", "curbs" };

        // Every step of the command read in name order, the location plan and the plot table among them
        public static IReadOnlyList<string> ReadSteps()
        {
            return WorkerRunner.StepCommand.Where(step => step.Value == ReadDataRun.Command).Select(step => step.Key).OrderBy(step => step, StringComparer.Ordinal).ToList();
        }

        // roles names the role of each input of the pane by its name there. workFolder is the work folder of the document, whose
        // parent is the work root of R372 the data book folder lies in
        public static ReadPlan Make(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string workFolder)
        {
            var stops = new List<string>();
            var found = new List<KeyValuePair<string, string>>();
            RunInput configInput = pane.FirstOrDefault(input => roles.TryGetValue(input.Name, out string role) && role == Config);
            if (configInput == null || string.IsNullOrEmpty(configInput.Path) || !File.Exists(configInput.Path))
            {
                return new ReadPlan(new ReadInput[0], ReadSteps(), new[] { "the read names no settings file on disk" }, null);
            }
            string configPath = Path.GetFullPath(configInput.Path);
            JObject config = JObject.Parse(WorkFiles.ReadText(configPath));
            var referenceOnly = new HashSet<string>(Strings(config.SelectToken("drawings.referenceOnly")), StringComparer.OrdinalIgnoreCase);
            foreach (RunInput input in pane)
            {
                if (!roles.TryGetValue(input.Name, out string role))
                {
                    continue;
                }
                if (role == RoadSurface)
                {
                    foreach (string name in Strings(config.SelectToken("siteBuild.roadSurfaces")))
                    {
                        string file = Path.Combine(input.Path, name + ".xml");
                        Add(found, stops, WorkerRunner.RoadSurfaceRole + name, file, input.Name + ": " + name + ".xml of siteBuild.roadSurfaces is not in the folder " + input.Path);
                    }
                    continue;
                }
                if (Drawings.Contains(role) && referenceOnly.Contains(Path.GetFileName(input.Path)))
                {
                    stops.Add(input.Name + ": " + Path.GetFileName(input.Path) + " is kept for reference only (drawings.referenceOnly) and is never read (R300)");
                    continue;
                }
                Add(found, stops, role, input.Path, input.Name + " is not on disk: " + input.Path);
            }
            JObject read = config.SelectToken("addin.readData") as JObject;
            if (read == null)
            {
                stops.Add("config names no addin.readData, the files of the read beside the pane (R375)");
                return Plan(found, stops, configPath);
            }
            string layers = Text(read, "cadLayers");
            if (layers == null)
            {
                stops.Add("addin.readData.cadLayers of config names no layer list");
            }
            else
            {
                Add(found, stops, CadLayers, Path.Combine(Path.GetDirectoryName(configPath), layers), "the layer list " + layers + " of addin.readData.cadLayers is not on disk beside config");
            }
            Schedules(config, read, found, stops);
            DataBook(read, workFolder, found, stops);
            return Plan(found, stops, configPath);
        }

        // R351. The fit schedule must be named, and every schedule named must be on disk, as the worker refuses the read without them
        private static void Schedules(JObject config, JObject read, List<KeyValuePair<string, string>> found, List<string> stops)
        {
            string fit = config.SelectToken("dataBook.locationPlan.fitNeighbourhood")?.Type == JTokenType.String ? (string)config.SelectToken("dataBook.locationPlan.fitNeighbourhood") : null;
            JObject hoods = config.SelectToken("connect.neighbourhoods") as JObject;
            string fitSchedule = fit == null || hoods == null ? null : hoods[fit]?.Type == JTokenType.Object ? Text((JObject)hoods[fit], "schedule") : null;
            if (fitSchedule == null)
            {
                stops.Add("the fit schedule of " + (fit ?? "dataBook.locationPlan.fitNeighbourhood") + " is not named in connect.neighbourhoods of config (R351)");
            }
            string root = config["inputRoot"]?.Type == JTokenType.String ? (string)config["inputRoot"] : null;
            string folder = Text(read, "scheduleFolder");
            if (root == null || folder == null)
            {
                stops.Add("config names no inputRoot or no addin.readData.scheduleFolder, the folder of the neighbourhood schedules (R351)");
                return;
            }
            foreach (JProperty hood in hoods == null ? Enumerable.Empty<JProperty>() : hoods.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                string schedule = hood.Value.Type == JTokenType.Object ? Text((JObject)hood.Value, "schedule") : null;
                if (schedule == null)
                {
                    continue;
                }
                string path = Path.GetFullPath(Path.Combine(root, folder, schedule));
                Add(found, stops, "schedule:" + hood.Name, path, "the schedule of " + hood.Name + (hood.Name == fit ? ", the fit schedule," : "") + " is not on disk: " + path);
            }
        }

        // The road levels read plots.csv and labels.csv and the plot table the border of the data book, by addin.readData of config
        private static void DataBook(JObject read, string workFolder, List<KeyValuePair<string, string>> found, List<string> stops)
        {
            string folder = Text(read, "dataBookFolder");
            JObject files = read["dataBookFiles"] as JObject;
            if (folder == null || files == null)
            {
                stops.Add("config names no addin.readData.dataBookFolder or dataBookFiles, the data book files of the road levels and the plot table");
                return;
            }
            string book = Path.IsPathRooted(folder) ? folder : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(workFolder)), folder);
            foreach (JProperty file in files.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                string name = file.Value.Type == JTokenType.String ? (string)file.Value : null;
                if (name == null)
                {
                    stops.Add("addin.readData.dataBookFiles." + file.Name + " of config names no file");
                    continue;
                }
                string path = Path.GetFullPath(Path.Combine(book, name));
                Add(found, stops, file.Name, path, file.Name + " of the data book is not on disk: " + path);
            }
        }

        private static void Add(List<KeyValuePair<string, string>> found, List<string> stops, string role, string path, string missing)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                stops.Add(missing);
                return;
            }
            found.Add(new KeyValuePair<string, string>(role, Path.GetFullPath(path)));
        }

        private static ReadPlan Plan(List<KeyValuePair<string, string>> found, List<string> stops, string configPath)
        {
            var inputs = new List<ReadInput>();
            if (stops.Count == 0)
            {
                inputs.AddRange(found.Select(item => new ReadInput(item.Key, item.Value, new FileInfo(item.Value).Length, FileHash.Sha256(item.Value))));
            }
            return new ReadPlan(inputs, ReadSteps(), stops, configPath);
        }

        private static string Text(JObject parent, string name)
        {
            JToken value = parent[name];
            return value != null && value.Type == JTokenType.String && ((string)value).Length > 0 ? (string)value : null;
        }

        private static IEnumerable<string> Strings(JToken token)
        {
            return token is JArray list ? list.Where(item => item.Type == JTokenType.String).Select(item => (string)item) : Enumerable.Empty<string>();
        }
    }
}
