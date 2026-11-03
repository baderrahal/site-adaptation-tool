using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Settings
{
    // What a project names after it took the shipped set
    public sealed class ProjectSet
    {
        public string ConfigFile { get; set; }
        public string RulesFile { get; set; }
        public string LevelProfile { get; set; }

        // The copied parameter sheet, the file button 1 reads by addin.parameterSheet of the config
        public string ParameterSheet { get; set; }
    }

    // Why a project can not take the shipped set
    public enum ShippedSetRefusal
    {
        None,
        NotSaved,
        NoSafeName,
    }

    // R486, R487, Q377, Q378. The files of a build the install ships under the add-in folder, copied once to the folder of a new project under the work root. A second
    // Save setup keeps the copy, so Save overrides and hand edits survive. The copy holds an override list with no entries, a past override never lands on a new project
    public sealed class ShippedSet
    {
        public const string FolderName = "ShippedSet";
        public const string ProjectsFolder = "New projects";
        private const string ConfigFolder = "config";
        private const string ConfigName = "project.json";
        private const string EmptyListNote = "R487, Q378. The override list of a new project, no entries until Save overrides writes one";

        private static readonly char[] NotInName = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        private static readonly string[] Devices = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        private const int MostInName = 100;

        private readonly string shipped;
        private readonly string workRoot;
        private readonly string name;
        private readonly bool saved;

        // folderName is the name of the folder of this model, the last part of its work folder of WorkFolder.For, so two models of one name in two folders never share a set
        public ShippedSet(string shippedFolder, string workRoot, string folderName, bool modelSaved)
        {
            shipped = shippedFolder;
            this.workRoot = workRoot;
            name = folderName ?? string.Empty;
            saved = modelSaved;
        }

        private string ShippedConfig => Path.Combine(shipped ?? string.Empty, ConfigFolder, ConfigName);

        public bool There => !string.IsNullOrEmpty(shipped) && File.Exists(ShippedConfig);

        public ShippedSetRefusal Refusal => !saved ? ShippedSetRefusal.NotSaved : !SafeName(name) ? ShippedSetRefusal.NoSafeName : ShippedSetRefusal.None;

        public string ProjectFolder => Path.Combine(workRoot, ProjectsFolder, name);

        public static bool SafeName(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > MostInName || text != text.Trim() || text.EndsWith(".", StringComparison.Ordinal) || text.IndexOfAny(NotInName) >= 0)
            {
                return false;
            }
            foreach (char letter in text)
            {
                if (char.IsControl(letter))
                {
                    return false;
                }
            }
            string stem = text.Split('.')[0];
            return Array.FindIndex(Devices, device => string.Equals(device, stem, StringComparison.OrdinalIgnoreCase)) < 0;
        }

        // designBasis.newProjectMode of the shipped config, what a Save would give, null when it gives none
        public string NewMode(Func<string, string> readText)
        {
            return ProjectModes.NewProject(ProjectModes.ReadConfig(ShippedConfig, readText));
        }

        // The set in the project folder, copied once, by a name next to it and a rename, so a copy that fails leaves nothing that reads as a set. Throws what the disk throws
        public ProjectSet Take(Func<string, string> readText)
        {
            string configPath = Path.Combine(ProjectFolder, ConfigFolder, ConfigName);
            if (!File.Exists(configPath))
            {
                CopyOnce();
            }
            JObject config = JObject.Parse(readText(configPath));
            string profile = config["profile"]?.Type == JTokenType.String ? (string)config["profile"] : throw new InvalidDataException("config names no rules profile, profile (R486)");
            string rulesPath = Path.GetFullPath(Path.Combine(ProjectFolder, profile));
            JObject rules = JObject.Parse(readText(rulesPath));
            JToken level = rules["levelProfiles"]?["default"];
            if (level == null || level.Type != JTokenType.String)
            {
                throw new InvalidDataException("the rules profile names no levelProfiles.default (R486)");
            }
            string list = CheckTable.CheckRun.OverridesPath(config, configPath, rulesPath);
            if (!File.Exists(list))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(list));
                File.WriteAllText(list, CheckTable.OverrideEdits.EmptyList(EmptyListNote));
            }
            return new ProjectSet { ConfigFile = configPath, RulesFile = rulesPath, LevelProfile = (string)level, ParameterSheet = SetUp.ParameterSheet.PathOf(configPath, config) };
        }

        private void CopyOnce()
        {
            if (Directory.Exists(ProjectFolder))
            {
                throw new IOException("the folder " + ProjectFolder + " holds files but no settings file");
            }
            string staging = ProjectFolder + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                CopyTree(shipped, staging);
                Directory.Move(staging, ProjectFolder);
            }
            catch
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, true);
                }
                throw;
            }
        }

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            }
            foreach (string folder in Directory.GetDirectories(from))
            {
                CopyTree(folder, Path.Combine(to, Path.GetFileName(folder)));
            }
        }
    }
}
