using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Settings
{
    // R484. The project mode of the settings. Apply lays it over designBasis.mode where a run loads its config, so every reader of the mode takes it and none
    // keeps a copy of its own. design_basis.take_mode of the Python gives the same line
    public static class ProjectModes
    {
        public static JObject Apply(JObject config, string mode)
        {
            if (mode == null)
            {
                return config;
            }
            JObject block = config?["designBasis"] as JObject;
            JArray modes = block?["modes"] as JArray;
            if (modes == null || !modes.Any(item => item.Type == JTokenType.String && (string)item == mode))
            {
                throw new InvalidDataException("the project mode " + mode + " is no mode of designBasis.modes " + (modes == null ? "none" : string.Join(", ", modes.Select(item => item.ToString()))) + " (R484)");
            }
            block["mode"] = mode;
            return config;
        }

        // The config a settings file names, null for none, one the reader does not give, or one that does not read, so a window shows any setup
        public static JObject ReadConfig(string configFile, Func<string, string> readText)
        {
            string text = string.IsNullOrEmpty(configFile) || readText == null ? null : readText(configFile);
            if (text == null)
            {
                return null;
            }
            try
            {
                return JObject.Parse(text);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // designBasis.mode of config as a text, null for none
        public static string OfConfig(JObject config)
        {
            JToken mode = (config?["designBasis"] as JObject)?["mode"];
            return mode != null && mode.Type == JTokenType.String ? (string)mode : null;
        }

        // designBasis.newProjectMode of config when designBasis.modes holds it, else null, so a hand edit of it falls back on the mode of config
        public static string NewProject(JObject config)
        {
            return Listed(config, "newProjectMode");
        }

        // designBasis.mode of config when designBasis.modes holds it, the mode a saved project with none takes, else null, so a config of before the
        // modes stays on its own mode with nothing written
        public static string SavedMode(JObject config)
        {
            return Listed(config, "mode");
        }

        private static string Listed(JObject config, string key)
        {
            JObject block = config?["designBasis"] as JObject;
            JToken mode = block?[key];
            return mode != null && mode.Type == JTokenType.String && block["modes"] is JArray modes && modes.Any(item => item.Type == JTokenType.String && (string)item == (string)mode) ? (string)mode : null;
        }

        // Empty project of New project. A project takes the mode of a new project only when the settings before this Save name no config file. One that names
        // a config and holds no mode stays on the mode of its config, so a plain Save setup of an older project never moves it
        public static void TakeNewProject(string configBefore, ProjectSettings next, Func<string, string> readText)
        {
            if (next.ProjectMode == null && string.IsNullOrEmpty(configBefore))
            {
                next.ProjectMode = NewProject(ReadConfig(next.ConfigFile, readText));
            }
        }
    }
}
