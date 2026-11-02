using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Settings
{
    // R492. The road surfaces a project can pick in Set up and the order the read and the build take. This reads and never refuses, the build holds
    // the list to R490 in DesignBasis, so the windows can show any config
    public static class RoadSurfaceChoices
    {
        // The stems of the LandXML files of the Road surface folder sorted ordinal, none for no folder. GetFiles of *.xml on Windows also finds
        // longer extensions, so the extension is held again
        public static IReadOnlyList<string> InFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return new string[0];
            }
            return Directory.GetFiles(folder, "*.xml").Where(file => string.Equals(Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name, StringComparer.Ordinal).ToList();
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
