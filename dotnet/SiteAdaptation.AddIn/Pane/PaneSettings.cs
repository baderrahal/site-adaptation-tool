using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/06-settings.md. The settings of one document as the pane holds them in memory. A pick changes them here
    // only, nothing is written to the document in 5a. Join J1 of steps/lane-b.md: 5c puts R367, the settings JSON of Common
    // built in 4h, in the place of this small reader
    public sealed class PaneSettings
    {
        public int FormatVersion { get; set; } = SettingsFile.Version;
        public string ConfigFile { get; set; }
        public string RulesFile { get; set; }
        public string LevelProfile { get; set; }
        public Dictionary<string, InputFile> Inputs { get; } = new Dictionary<string, InputFile>(StringComparer.Ordinal);
        public PaneScope Scope { get; set; } = new PaneScope();
        public bool DryRunFirst { get; set; } = true;
        public string VillaMode { get; set; } = VillaModes.Masses;

        public bool Chosen
        {
            get { return !string.IsNullOrEmpty(ConfigFile); }
        }

        public string InputPath(string key)
        {
            return Inputs.TryGetValue(key, out InputFile input) ? input.Path : null;
        }

        public void Pick(string key, string path)
        {
            if (!InputKeys.All.Contains(key))
            {
                throw new ArgumentException("no input " + key, nameof(key));
            }
            Inputs[key] = new InputFile { Path = path };
        }
    }

    public sealed class InputFile
    {
        public string Path { get; set; }
        public long? Size { get; set; }
        public string Sha256 { get; set; }
    }

    // The scope of section 3: the whole project, one neighbourhood or a list of plots
    public sealed class PaneScope
    {
        public string Kind { get; set; } = ScopeKinds.Project;
        public string Nbh { get; set; }
        public string Plots { get; set; }
    }

    public static class ScopeKinds
    {
        public const string Project = "project";
        public const string Nbh = "nbh";
        public const string Plots = "plots";
        public static readonly string[] All = { Project, Nbh, Plots };
    }

    // QL4, QL8, docs/design/11-light-mode.md. Masses by default, Links picked per run
    public static class VillaModes
    {
        public const string Masses = "masses";
        public const string Links = "links";
        public static readonly string[] All = { Masses, Links };
    }

    // The inputs of 06-settings.md by their names in the settings JSON
    public static class InputKeys
    {
        public const string Baseplan = "baseplan";
        public const string VillaBlocks = "villaBlocks";
        public const string Curbs = "curbs";
        public const string RoadSurface = "roadSurface";
        public const string Dlus = "dlus";
        public const string Sales = "sales";
        public const string PlotNumbers = "plotNumbers";
        public const string Prototypes = "prototypes";
        public static readonly string[] All = { Baseplan, VillaBlocks, Curbs, RoadSurface, Dlus, Sales, PlotNumbers, Prototypes };
        public static readonly string[] Folders = { RoadSurface, Prototypes };
    }

    public static class SettingsFile
    {
        public const int Version = 1;

        public static PaneSettings Read(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException error)
            {
                throw new InvalidDataException("the settings file is no JSON object: " + error.Message, error);
            }
            JToken version = root["formatVersion"];
            if (version == null || version.Type != JTokenType.Integer)
            {
                throw new InvalidDataException("the settings file has no whole formatVersion");
            }
            int read = version.Value<int>();
            if (read > Version)
            {
                throw new InvalidDataException("the settings file is formatVersion " + read + ", this add-in reads up to " + Version);
            }
            var settings = new PaneSettings
            {
                FormatVersion = read,
                ConfigFile = Text(root, "configFile"),
                RulesFile = Text(root, "rulesFile"),
                LevelProfile = Text(root, "levelProfile"),
                DryRunFirst = root["dryRunFirst"] == null || root["dryRunFirst"].Value<bool>(),
                VillaMode = Text(root, "villaMode") ?? VillaModes.Masses,
            };
            if (!VillaModes.All.Contains(settings.VillaMode))
            {
                throw new InvalidDataException("the settings file names villaMode " + settings.VillaMode + ", not one of " + string.Join(", ", VillaModes.All));
            }
            if (root["inputs"] is JObject inputs)
            {
                foreach (JProperty input in inputs.Properties())
                {
                    if (!InputKeys.All.Contains(input.Name))
                    {
                        throw new InvalidDataException("the settings file names an input " + input.Name + ", not one of " + string.Join(", ", InputKeys.All));
                    }
                    settings.Inputs[input.Name] = new InputFile
                    {
                        Path = Text(input.Value, "path"),
                        Size = input.Value["size"] == null || input.Value["size"].Type == JTokenType.Null ? (long?)null : input.Value["size"].Value<long>(),
                        Sha256 = Text(input.Value, "sha256"),
                    };
                }
            }
            if (root["scope"] is JObject scope)
            {
                settings.Scope = new PaneScope { Kind = Text(scope, "kind") ?? ScopeKinds.Project, Nbh = Text(scope, "nbh"), Plots = Text(scope, "plots") };
                if (!ScopeKinds.All.Contains(settings.Scope.Kind))
                {
                    throw new InvalidDataException("the settings file names scope " + settings.Scope.Kind + ", not one of " + string.Join(", ", ScopeKinds.All));
                }
            }
            return settings;
        }

        private static string Text(JToken parent, string name)
        {
            JToken value = parent[name];
            return value == null || value.Type == JTokenType.Null ? null : value.Value<string>();
        }
    }

    // The names of levelProfiles of the rules file, each an object, and its default (R277)
    public static class RulesProfiles
    {
        public static IReadOnlyList<string> Names(string json, out string fallback)
        {
            fallback = null;
            if (!(JObject.Parse(json)["levelProfiles"] is JObject profiles))
            {
                return new string[0];
            }
            fallback = profiles["default"] == null ? null : profiles["default"].Value<string>();
            return profiles.Properties().Where(profile => profile.Value.Type == JTokenType.Object).Select(profile => profile.Name).ToList();
        }
    }
}
