using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Review;
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
        public ReadPlan(IReadOnlyList<ReadInput> inputs, IReadOnlyList<string> steps, IReadOnlyList<string> stops, string configPath, string roadOrder = null)
        {
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

        // One SHA-256 over every role and hash and the step list, which names the folder of the read
        public string Hash { get; }
    }

    // R635, R847. The inputs of the two reads. Step 2 reads the project folder, Raw: the settings file, the folder by a hash of its file
    // list, and what a person confirmed. The read for the build, ForBuild, names the files of the pane by their role, each LandXML file
    // of the road surfaces in the Road surface folder, the layer list beside config, and the plots and labels of the plot table of the
    // latest read (R840). Every file by full path and SHA-256, never by name. No read names a data book file or a schedule
    public static class ReadInputs
    {
        public const string Config = "config";
        public const string RoadSurface = "roadSurface";
        public const string CadLayers = "cadLayers";
        public const string ScanFolder = "scanFolder";

        // R847. The stops of a read of the folder that cannot start
        public const string NoSettingsFile = "the read names no settings file on disk";
        public const string NoFolder = "the project folder is not on disk: ";

        private static readonly string[] Drawings = { "baseplan", "villaBlocks", "curbs" };

        // Every step of the command read in name order, the location plan and the plot table among them
        public static IReadOnlyList<string> ReadSteps()
        {
            return WorkerRunner.StepCommand.Where(step => step.Value == ReadDataRun.Command).Select(step => step.Key).OrderBy(step => step, StringComparer.Ordinal).ToList();
        }

        // R740. The steps of the location plan and the plot table, which the read for the build leaves out: the plot table comes from the data check
        public static readonly IReadOnlyList<string> PlotTableSteps = new[] { LocationPlan.LabelStepName, LocationPlan.FitStepName, LocationPlan.CheckStepName, LocationPlan.FacesStepName, PlotTable.StepName };

        // R740. The read 4 Check needs on a project whose stage 1 read the folder: the drawings, the road surfaces and the workbooks,
        // with no schedule and no plot number file, as the plot table of the data check stands for the location plan.
        // R840. The road levels take the plots and the labels from that plot table, the one the Review window and Check show, never from a data book.
        // roles names the role of each input of the pane by its name there, picked gives the road surfaces picked in Set up (R492)
        public static ReadPlan ForBuild(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string workFolder, IReadOnlyList<string> picked = null, string mode = null)
        {
            var stops = new List<string>();
            var found = new List<KeyValuePair<string, string>>();
            RunInput configInput = pane.FirstOrDefault(input => roles.TryGetValue(input.Name, out string role) && role == Config);
            if (configInput == null || string.IsNullOrEmpty(configInput.Path) || !File.Exists(configInput.Path))
            {
                return new ReadPlan(new ReadInput[0], ReadSteps(), new[] { NoSettingsFile }, null);
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
                if (role == LocationPlan.PlotNumbersRole)
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
            Plots(workFolder, found, stops);
            ReadPlan plan = Plan(found, stops, configPath, order);
            return new ReadPlan(plan.Inputs, plan.Steps.Where(step => !PlotTableSteps.Contains(step)).ToList(), plan.Stops, plan.ConfigPath, plan.RoadOrder);
        }

        // R635, R847. The read of step 2, the one read a project has: the worker command dataCheck on the project folder, with no read step.
        // The folder is known by a hash of the name, size and write time of its files, so a changed file is another read. The worker marks
        // each file it opens before and after, the guard of D19. A settings file that does not read, or a folder not on disk, is a stop
        public static ReadPlan Raw(IReadOnlyList<RunInput> pane, IReadOnlyDictionary<string, string> roles, string workFolder, string mode = null)
        {
            RunInput configInput = (pane ?? new RunInput[0]).FirstOrDefault(input => roles.TryGetValue(input.Name, out string role) && role == Config);
            string configPath = configInput?.Path;
            // R639. Once, a second read of a file locked in between gives another answer here
            JObject config = RawConfig(configPath, mode, out string stop);
            if (config == null)
            {
                return new ReadPlan(new ReadInput[0], new string[0], new[] { stop }, null);
            }
            string folder = Path.GetFullPath((string)config["inputRoot"]);
            configPath = Path.GetFullPath(configPath);
            HashSet<string> skip;
            List<Regex> skipFiles;
            try
            {
                skip = DataCheckSettings.SkipFoldersOf(config);
                skipFiles = DataCheckSettings.SkipFilesOf(config);
            }
            catch (InvalidDataException refused)
            {
                return new ReadPlan(new ReadInput[0], new string[0], new[] { refused.Message }, configPath);
            }
            var inputs = new List<ReadInput>
            {
                new ReadInput(Config, configPath, new FileInfo(configPath).Length, FileHash.Sha256(configPath)),
                FolderInput(folder, skip, skipFiles),
            };
            string profile = Path.Combine(Path.GetFullPath(workFolder), ConfirmedProfile.FileName);
            if (File.Exists(profile))
            {
                inputs.Add(new ReadInput(ConfirmedProfile.Role, profile, new FileInfo(profile).Length, FileHash.Sha256(profile)));
            }
            return new ReadPlan(inputs, new string[0], new string[0], configPath);
        }

        // The settings of a read of the folder, null with the stop when the settings file does not read or names no folder on disk
        private static JObject RawConfig(string configPath, string mode, out string stop)
        {
            stop = NoSettingsFile;
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return null;
            }
            try
            {
                JObject config = ProjectModes.Apply(JObject.Parse(WorkFiles.ReadText(configPath)), mode);
                string root = config["inputRoot"]?.Type == JTokenType.String ? (string)config["inputRoot"] : null;
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    stop = NoFolder + (string.IsNullOrEmpty(root) ? "the settings file names no inputRoot" : root);
                    return null;
                }
                return config;
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is Newtonsoft.Json.JsonException || failure is IOException || failure is UnauthorizedAccessException || failure is ArgumentException || failure is NotSupportedException)
            {
                stop = "the settings file does not read: " + failure.Message;
                return null;
            }
        }

        // R492. The road surfaces the read for the build takes: those named or picked under rev6, those of siteBuild.roadSurfaces under the past job
        public static IEnumerable<string> RoadNames(JObject config, IReadOnlyList<string> picked)
        {
            return RoadSurfaceChoices.IsRev6(config) ? RoadSurfaceChoices.Named(config, picked) : Strings(config.SelectToken("siteBuild.roadSurfaces"));
        }

        // R635. Every file of the folder as a raw read walks it: a folder of skip at any depth and, as the worker walks it, a link or junction are never followed
        // R831, R832. A file whose name fits skipFiles is left out as the worker leaves it out, so the hash and the folder roles see what the worker reads
        public static IEnumerable<string> FilesOf(string folder, HashSet<string> skip, List<Regex> skipFiles)
        {
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string here = pending.Pop();
                foreach (string file in Directory.GetFiles(here).Where(file => !DataCheckSettings.SkipsFile(skipFiles, file)))
                {
                    yield return file;
                }
                foreach (string child in Directory.GetDirectories(here).Where(child => !skip.Contains(Path.GetFileName(child)) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0))
                {
                    pending.Push(child);
                }
            }
        }

        private static ReadInput FolderInput(string folder, HashSet<string> skip, List<Regex> skipFiles)
        {
            long bytes = 0;
            var lines = new List<string>();
            foreach (string file in FilesOf(folder, skip, skipFiles))
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

        // R840. The plots and the labels of the road levels of the build, written from the plot table of the latest read of the work folder as
        // Check takes them (ProjectPlots, R780). No read, or a read with no plot table, is a stop that names it
        private static void Plots(string workFolder, List<KeyValuePair<string, string>> found, List<string> stops)
        {
            string read = string.IsNullOrEmpty(workFolder) ? null : ReviewLoad.LatestReadFolder(workFolder);
            ProjectPlots.Written plots = ProjectPlots.Write(read, workFolder);
            if (plots.Missing != null)
            {
                stops.Add(plots.Missing);
                return;
            }
            found.Add(new KeyValuePair<string, string>(ProjectPlots.PlotsRole, plots.Plots));
            found.Add(new KeyValuePair<string, string>(ProjectPlots.LabelsRole, plots.Labels));
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
