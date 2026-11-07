using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;

namespace SiteAdaptation.Common.ReadData
{
    // R375. One input of the read of button 2, known by its role in the request, its full path, its size and its SHA-256
    public sealed class ReadInput
    {
        public ReadInput(string role, string path, long bytes, string sha256, int filesHeld = 0)
        {
            Role = role;
            Path = path;
            Bytes = bytes;
            Sha256 = sha256;
            FilesHeld = filesHeld;
        }

        public string Role { get; }
        public string Path { get; }
        public long Bytes { get; }
        public string Sha256 { get; }

        // R639. The files a folder input holds as it was walked, 0 for a file. Outside the hash, a count says nothing the list of the folder does not
        public int FilesHeld { get; }
    }

    // R375. Every input of the read with the one hash over them and the read steps, or the stops that keep it from starting
    public sealed class ReadPlan
    {
        public ReadPlan(IReadOnlyList<ReadInput> inputs, IReadOnlyList<string> steps, IReadOnlyList<string> stops, string configPath, string roadOrder = null, bool fromDataCheck = false, IReadOnlyList<string> pastLacks = null)
        {
            FromDataCheck = fromDataCheck;
            PastLacks = pastLacks ?? new string[0];
            RoadOrder = roadOrder;
            Inputs = inputs;
            Steps = steps;
            Stops = stops;
            ConfigPath = configPath;
            var named = inputs.ToDictionary(input => input.Role, input => input.Sha256, StringComparer.Ordinal);
            named["steps"] = string.Join(",", steps);
            if (roadOrder != null)
            {
                named["roadOrder"] = roadOrder;
            }
            Hash = StepHash.Combine(named);
        }

        public IReadOnlyList<ReadInput> Inputs { get; }
        public IReadOnlyList<string> Steps { get; }
        public IReadOnlyList<string> Stops { get; }
        public string ConfigPath { get; }

        // R492. The effective road order under rev6, one line per name, in the hash so a new order is a new read. Null under the past job, whose hash stays
        public string RoadOrder { get; }

        // R635. The read of a project with no past files, by the worker command dataCheck on the folder and with no read step
        public bool FromDataCheck { get; }

        // R639. What the old read lacks, the reason this read takes the folder, one line each. Outside the hash, it says why and changes no read
        public IReadOnlyList<string> PastLacks { get; }

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
        public const string ScanFolder = "scanFolder";

        private static readonly string[] Drawings = { "baseplan", "villaBlocks", "curbs" };

        // Every step of the command read in name order, the location plan and the plot table among them
        public static IReadOnlyList<string> ReadSteps()
        {
            return WorkerRunner.StepCommand.Where(step => step.Value == ReadDataRun.Command).Select(step => step.Key).OrderBy(step => step, StringComparer.Ordinal).ToList();
        }

        // roles names the role of each input of the pane by its name there. workFolder is the work folder of the document, whose
        // parent is the work root of R372 the data book folder lies in. picked gives the road surfaces picked in Set up, read under rev6 alone (R492)
        public static ReadPlan Make(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string workFolder, IReadOnlyList<string> picked = null, string mode = null)
        {
            return Make(pane, roles, workFolder, picked, mode, false);
        }

        // R740. The steps of the location plan and the plot table, which the read for the build leaves out: the plot table comes from the data check
        public static readonly IReadOnlyList<string> PlotTableSteps = new[] { LocationPlan.LabelStepName, LocationPlan.FitStepName, LocationPlan.CheckStepName, LocationPlan.FacesStepName, PlotTable.StepName };

        // R740. The read 4 Check needs on a project whose stage 1 read the folder: the drawings, the road surfaces, the workbooks and the data book of
        // the old read, with no schedule and no plot number file, as the plot table of the data check stands for the location plan
        public static ReadPlan ForBuild(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string workFolder, IReadOnlyList<string> picked = null, string mode = null)
        {
            return Make(pane, roles, workFolder, picked, mode, true);
        }

        private static ReadPlan Make(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string workFolder, IReadOnlyList<string> picked, string mode, bool forBuild)
        {
            var stops = new List<string>();
            var found = new List<KeyValuePair<string, string>>();
            RunInput configInput = pane.FirstOrDefault(input => roles.TryGetValue(input.Name, out string role) && role == Config);
            if (configInput == null || string.IsNullOrEmpty(configInput.Path) || !File.Exists(configInput.Path))
            {
                return new ReadPlan(new ReadInput[0], ReadSteps(), new[] { "the read names no settings file on disk" }, null);
            }
            string configPath = Path.GetFullPath(configInput.Path);
            JObject config;
            try
            {
                // R484. The project mode of the settings laid once here, the road roles and the hash of the read take it
                config = ProjectModes.Apply(JObject.Parse(WorkFiles.ReadText(configPath)), mode);
            }
            catch (InvalidDataException refused)
            {
                return new ReadPlan(new ReadInput[0], ReadSteps(), new[] { refused.Message }, null);
            }
            var referenceOnly = new HashSet<string>(Strings(config.SelectToken("drawings.referenceOnly")), StringComparer.OrdinalIgnoreCase);
            string order = RoadSurfaceChoices.OrderText(config, picked);
            foreach (RunInput input in pane)
            {
                if (!roles.TryGetValue(input.Name, out string role))
                {
                    continue;
                }
                if (role == RoadSurface)
                {
                    // R492. Under rev6 the surfaces named or picked in their order, the past job those of siteBuild.roadSurfaces
                    bool rev6 = RoadSurfaceChoices.IsRev6(config);
                    string listed = rev6 ? "the road surfaces named" : "siteBuild.roadSurfaces";
                    if (rev6 && RoadSurfaceChoices.Named(config, picked).Count == 0)
                    {
                        stops.Add(input.Name + ": config names no road surface and Set up picked none (R490)");
                    }
                    foreach (string name in RoadNames(config, picked))
                    {
                        // R492. A surface siteBuild.roadFolder.notTaken names covers corners with the wrong level, so it is refused with its reason
                        string why = rev6 ? RoadSurfaceChoices.NotTakenWhy(config, name) : null;
                        if (why != null)
                        {
                            stops.Add(input.Name + ": " + name + " is no road surface: " + why + " (R492)");
                            continue;
                        }
                        why = RoadSurfaceChoices.NotRoadWhy(config, name);
                        if (why != null)
                        {
                            stops.Add(RoadSurfaceChoices.NotRoadLine(name, why));
                            continue;
                        }
                        string file = Path.Combine(input.Path, name + ".xml");
                        Add(found, stops, WorkerRunner.RoadSurfaceRole + name, file, input.Name + ": " + name + ".xml of " + listed + " is not in the folder " + input.Path);
                    }
                    continue;
                }
                if (Drawings.Contains(role) && referenceOnly.Contains(Path.GetFileName(input.Path)))
                {
                    stops.Add(input.Name + ": " + Path.GetFileName(input.Path) + " is kept for reference only (drawings.referenceOnly) and is never read (R300)");
                    continue;
                }
                if (forBuild && role == LocationPlan.PlotNumbersRole)
                {
                    continue;
                }
                Add(found, stops, role, input.Path, input.Name + " is not on disk: " + input.Path);
            }
            JObject read = config.SelectToken("addin.readData") as JObject;
            if (read == null)
            {
                stops.Add("config names no addin.readData, the files of the read beside the pane (R375)");
                return Plan(found, stops, configPath, order);
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
            if (!forBuild)
            {
                Schedules(config, read, found, stops);
            }
            DataBook(read, workFolder, found, stops, forBuild);
            ReadPlan plan = Plan(found, stops, configPath, order);
            return forBuild ? new ReadPlan(plan.Inputs, plan.Steps.Where(step => !PlotTableSteps.Contains(step)).ToList(), plan.Stops, plan.ConfigPath, plan.RoadOrder) : plan;
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

        // R635. True when the read of this project takes its plot table from the data check: a rev6 project whose input root is on disk and which
        // lacks a past file of the old read, a schedule or a data book file. Past job mode keeps the old read and its stops, and so does a project
        // that holds them all. The add-in asks this to leave out what only the old read needs, the files of the pane and the scan before it
        public static bool NoPastFiles(string configPath, string mode, string workFolder)
        {
            return RawConfig(configPath, mode, workFolder, out _) != null;
        }

        // R635. The plan of that read, null when the old read applies. The folder is known by a hash of the name, size and write time of its files,
        // so a changed file is another read. The worker marks each file it opens before and after, the guard of D19
        public static ReadPlan Raw(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string workFolder, string mode = null)
        {
            RunInput configInput = (pane ?? new RunInput[0]).FirstOrDefault(input => roles.TryGetValue(input.Name, out string role) && role == Config);
            string configPath = configInput?.Path;
            // R639. Once, a second read of a file locked in between gives null here
            JObject config = RawConfig(configPath, mode, workFolder, out List<string> lacks);
            if (config == null)
            {
                return null;
            }
            string folder = Path.GetFullPath((string)config["inputRoot"]);
            configPath = Path.GetFullPath(configPath);
            HashSet<string> skip;
            try
            {
                skip = DataCheckSettings.SkipFoldersOf(config);
            }
            catch (InvalidDataException refused)
            {
                return new ReadPlan(new ReadInput[0], new string[0], new[] { refused.Message }, configPath, null, true, lacks);
            }
            var inputs = new List<ReadInput>
            {
                new ReadInput(Config, configPath, new FileInfo(configPath).Length, FileHash.Sha256(configPath)),
                FolderInput(folder, skip),
            };
            string profile = Path.Combine(Path.GetFullPath(workFolder), ConfirmedProfile.FileName);
            if (File.Exists(profile))
            {
                inputs.Add(new ReadInput(ConfirmedProfile.Role, profile, new FileInfo(profile).Length, FileHash.Sha256(profile)));
            }
            return new ReadPlan(inputs, new string[0], new string[0], configPath, null, true, lacks);
        }

        // Null where the old read applies, and for a settings file that does not read, which the old read says in its own line. R639: the pane refresh
        // reaches this, so a locked file is held as PaneRunState.ConfigOf holds the same refresh. lacks names what sends the project to the folder
        private static JObject RawConfig(string configPath, string mode, string workFolder, out List<string> lacks)
        {
            lacks = new List<string>();
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return null;
            }
            try
            {
                JObject config = ProjectModes.Apply(JObject.Parse(WorkFiles.ReadText(configPath)), mode);
                string root = config["inputRoot"]?.Type == JTokenType.String ? (string)config["inputRoot"] : null;
                if (!RoadSurfaceChoices.IsRev6(config) || string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    return null;
                }
                var found = new List<KeyValuePair<string, string>>();
                JObject read = config.SelectToken("addin.readData") as JObject;
                if (read != null)
                {
                    Schedules(config, read, found, lacks);
                    DataBook(read, workFolder, found, lacks, false);
                }
                return read == null || lacks.Count > 0 ? config : null;
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is Newtonsoft.Json.JsonException || failure is IOException || failure is UnauthorizedAccessException || failure is ArgumentException || failure is NotSupportedException)
            {
                return null;
            }
        }

        // R492. The road surfaces the old read takes: those named or picked under rev6, those of siteBuild.roadSurfaces under the past job
        public static IEnumerable<string> RoadNames(JObject config, IReadOnlyList<string> picked)
        {
            return RoadSurfaceChoices.IsRev6(config) ? RoadSurfaceChoices.Named(config, picked) : Strings(config.SelectToken("siteBuild.roadSurfaces"));
        }

        // R635. Every file of the folder as a raw read walks it: a folder of skip at any depth and, as the worker walks it, a link or junction are never followed
        public static IEnumerable<string> FilesOf(string folder, HashSet<string> skip)
        {
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string here = pending.Pop();
                foreach (string file in Directory.GetFiles(here))
                {
                    yield return file;
                }
                foreach (string child in Directory.GetDirectories(here).Where(child => !skip.Contains(Path.GetFileName(child)) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0))
                {
                    pending.Push(child);
                }
            }
        }

        private static ReadInput FolderInput(string folder, HashSet<string> skip)
        {
            long bytes = 0;
            var lines = new List<string>();
            foreach (string file in FilesOf(folder, skip))
            {
                var info = new FileInfo(file);
                bytes += info.Length;
                lines.Add(file.Substring(folder.Length) + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks);
            }
            lines.Sort(StringComparer.Ordinal);
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
                return new ReadInput(ScanFolder, folder, bytes, string.Concat(digest.Select(item => item.ToString("x2"))), lines.Count);
            }
        }

        // The road levels read plots.csv and labels.csv and the plot table the border of the data book, by addin.readData of config
        // R740. The read for the build leaves out the border, which only the plot table takes, as the worker starts the plot table on a border
        private static void DataBook(JObject read, string workFolder, List<KeyValuePair<string, string>> found, List<string> stops, bool forBuild)
        {
            string folder = Text(read, "dataBookFolder");
            JObject files = read["dataBookFiles"] as JObject;
            if (folder == null || files == null)
            {
                stops.Add("config names no addin.readData.dataBookFolder or dataBookFiles, the data book files of the road levels and the plot table");
                return;
            }
            string book = Path.IsPathRooted(folder) ? folder : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(workFolder)), folder);
            foreach (JProperty file in files.Properties().Where(item => !forBuild || item.Name != PlotTable.BorderRole).OrderBy(item => item.Name, StringComparer.Ordinal))
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

        private static ReadPlan Plan(List<KeyValuePair<string, string>> found, List<string> stops, string configPath, string order)
        {
            var inputs = new List<ReadInput>();
            if (stops.Count == 0)
            {
                inputs.AddRange(found.Select(item => new ReadInput(item.Key, item.Value, new FileInfo(item.Value).Length, FileHash.Sha256(item.Value))));
            }
            return new ReadPlan(inputs, ReadSteps(), stops, configPath, order);
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
