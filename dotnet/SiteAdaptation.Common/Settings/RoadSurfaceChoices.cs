using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Settings
{
    // R492. The road surfaces a project can pick in Set up and the order the read and the build take. This reads and never refuses, the build holds
    // the list to R490 in DesignBasis, so the windows can show any config
    public static class RoadSurfaceChoices
    {
        // The stems of the LandXML files of the Road surface folder sorted ordinal, none for no folder, with no name siteBuild.roadFolder.notTaken
        // of config refuses. GetFiles of *.xml on Windows also finds longer extensions, so the extension is held again
        public static IReadOnlyList<string> InFolder(string folder, JObject config)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return new string[0];
            }
            return Directory.GetFiles(folder, "*.xml").Where(file => string.Equals(Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileNameWithoutExtension).Where(name => NotTakenWhy(config, name) == null).OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        // design_basis.not_taken_why. The reason of siteBuild.roadFolder.notTaken that names this surface by its short name, as a whole name, null for
        // none. Those reasons are the one list of R344 of what is no road, read in the Roads folder, so no list of names stands in code
        public static string NotTakenWhy(JObject config, string name)
        {
            JObject reasons = ((config?["siteBuild"] as JObject)?["roadFolder"] as JObject)?["notTaken"] as JObject;
            if (reasons == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            var whole = new Regex(@"(?<!\w)" + Regex.Escape(ReadSettings.RoadName(name)) + @"(?!\w)", RegexOptions.CultureInvariant);
            return reasons.Properties().Where(item => item.Value.Type == JTokenType.String).Select(item => (string)item.Value).FirstOrDefault(reason => whole.IsMatch(reason));
        }

        // R492. The effective road order as one text under rev6, null otherwise, so what the past job hashes stays as it was
        public static string OrderText(JObject config, IReadOnlyList<string> picked)
        {
            return IsRev6(config) ? string.Join("\n", Named(config, picked)) : null;
        }

        // The effective order: the pick when one was made, else the surfaces of designBasis.roadLevel, or its one surface of an old config, none when neither
        public static IReadOnlyList<string> Named(JObject config, IReadOnlyList<string> picked)
        {
            if (picked != null)
            {
                return picked;
            }
            JObject road = (config?["designBasis"] as JObject)?["roadLevel"] as JObject;
            if (road?["surfaces"] is JArray surfaces)
            {
                return surfaces.Where(name => name.Type == JTokenType.String).Select(name => (string)name).ToList();
            }
            JToken surface = road?["surface"];
            return surface != null && surface.Type == JTokenType.String ? new[] { (string)surface } : new string[0];
        }

        public static bool IsRev6(JObject config)
        {
            JToken mode = (config?["designBasis"] as JObject)?["mode"];
            return mode != null && mode.Type == JTokenType.String && (string)mode == DesignBasis.Rev6;
        }

        // What a worker request carries: the pick under rev6, null otherwise, so a request of the past job stays byte-equal
        public static List<string> ForRequest(JObject config, IReadOnlyList<string> picked)
        {
            return IsRev6(config) && picked != null ? new List<string>(picked) : null;
        }
    }
}
