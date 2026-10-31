using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, R367, docs/design/06-settings.md. The settings of one document as the pane holds them in memory, the ProjectSettings of
    // R367 itself since join J1 of steps/lane-b.md, so the pane and the Settings entity read and write one JSON. A pick changes them
    // here only, the entity is written with the record of a real run (SettingsStore)
    public sealed class PaneSettings
    {
        public PaneSettings()
            : this(new ProjectSettings())
        {
        }

        public PaneSettings(ProjectSettings project)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public ProjectSettings Project { get; }

        public int FormatVersion { get { return Project.FormatVersion; } set { Project.FormatVersion = value; } }
        public string ConfigFile { get { return Project.ConfigFile; } set { Project.ConfigFile = value; } }
        public string RulesFile { get { return Project.RulesFile; } set { Project.RulesFile = value; } }
        public string LevelProfile { get { return Project.LevelProfile; } set { Project.LevelProfile = value; } }
        public bool DryRunFirst { get { return Project.DryRunFirst; } set { Project.DryRunFirst = value; } }
        public IDictionary<string, SettingsInput> Inputs { get { return Project.Inputs; } }

        public SettingsScope Scope
        {
            get { return Project.Scope ?? (Project.Scope = new SettingsScope()); }
            set { Project.Scope = value ?? new SettingsScope(); }
        }

        // R404. villaModeDefault of addin.lightMode of config, which the pane reads from the Settings file, null when it gives none
        public string VillaModeDefault { get; set; }

        // The pick of the row Villa mode, else the default of config. Only a pick reaches villaMode of the settings JSON
        public string VillaMode
        {
            get { return Project.VillaMode ?? VillaModeDefault; }
            set { Project.VillaMode = value; }
        }

        public bool Chosen
        {
            get { return !string.IsNullOrEmpty(ConfigFile); }
        }

        public string InputPath(string key)
        {
            return Inputs.TryGetValue(key, out SettingsInput input) ? input.Path : null;
        }

        public void Pick(string key, string path)
        {
            if (!InputKeys.All.Contains(key))
            {
                throw new ArgumentException("no input " + key, nameof(key));
            }
            Inputs[key] = new SettingsInput { Path = path };
        }
    }

    // The inputs of 06-settings.md by their names in the settings JSON of R367, and the two the pane picks as folders
    public static class InputKeys
    {
        public const string Baseplan = InputNames.Baseplan;
        public const string VillaBlocks = InputNames.VillaBlocks;
        public const string Curbs = InputNames.Curbs;
        public const string RoadSurface = InputNames.RoadSurface;
        public const string Dlus = InputNames.Dlus;
        public const string Sales = InputNames.Sales;
        public const string PlotNumbers = InputNames.PlotNumbers;
        public const string Prototypes = InputNames.Prototypes;
        public const string ParameterSheet = InputNames.ParameterSheet;
        public static readonly string[] All = InputNames.All;
        public static readonly string[] Folders = { RoadSurface, Prototypes };
    }

    // J1. The pane reads a settings JSON by SettingsJson of R367, its small reader of 5a is gone
    public static class SettingsFile
    {
        public const int Version = SettingsJson.Version;

        public static PaneSettings Read(string json)
        {
            return new PaneSettings(SettingsJson.Read(json));
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

        // The text of project of the rules file, its own name for the job it holds the rules of, null when it gives none
        public static string Name(string json)
        {
            JToken project = JObject.Parse(json)["project"];
            return project != null && project.Type == JTokenType.String && ((string)project).Trim().Length > 0 ? ((string)project).Trim() : null;
        }
    }
}
