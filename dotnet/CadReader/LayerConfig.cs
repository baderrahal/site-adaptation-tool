using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace CadReader
{
    // Layer lists that Bader edits in config/cad_layers.json. Names match without case and may use * and ? wildcards.
    // Drop wins over keep. A layer on neither list is kept and reported as unlisted, so nothing disappears unseen.
    public sealed class LayerConfig
    {
        public const string Keep = "keep";
        public const string Drop = "drop";
        public const string Unlisted = "unlisted";

        public List<string> PlotLineLayers { get; set; } = new List<string> { "MP_PLOT_LINE" };
        public List<string> PlotLabelLayers { get; set; } = new List<string> { "MP_PLOT_TEXT" };
        [JsonProperty("keep")]
        public List<string> KeepLayers { get; set; } = new List<string>();
        [JsonProperty("drop")]
        public List<string> DropLayers { get; set; } = new List<string>();

        private readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private List<Regex> keepPatterns;
        private List<Regex> dropPatterns;
        private List<Regex> plotLinePatterns;

        public static LayerConfig Load(string path)
        {
            var config = JsonConvert.DeserializeObject<LayerConfig>(File.ReadAllText(path), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            if (config == null)
            {
                throw new InvalidDataException("Layer config is empty: " + path);
            }
            return config;
        }

        public string Status(string layer)
        {
            string status;
            if (cache.TryGetValue(layer, out status))
            {
                return status;
            }
            if (keepPatterns == null)
            {
                keepPatterns = (KeepLayers ?? new List<string>()).Select(Pattern).ToList();
                dropPatterns = (DropLayers ?? new List<string>()).Select(Pattern).ToList();
            }
            if (dropPatterns.Any(pattern => pattern.IsMatch(layer)))
            {
                status = Drop;
            }
            else if (keepPatterns.Any(pattern => pattern.IsMatch(layer)))
            {
                status = Keep;
            }
            else
            {
                status = Unlisted;
            }
            cache[layer] = status;
            return status;
        }

        public bool IsPlotLine(string layer)
        {
            if (plotLinePatterns == null)
            {
                plotLinePatterns = (PlotLineLayers ?? new List<string>()).Select(Pattern).ToList();
            }
            return plotLinePatterns.Any(pattern => pattern.IsMatch(layer));
        }

        public string PlotLineName
        {
            get { return string.Join("+", PlotLineLayers ?? new List<string>()); }
        }

        public bool IsPlotLabel(string layer)
        {
            return (PlotLabelLayers ?? new List<string>()).Any(name => string.Equals(name, layer, StringComparison.OrdinalIgnoreCase));
        }

        private static Regex Pattern(string wildcard)
        {
            string body = Regex.Escape(wildcard.Trim()).Replace("\\*", ".*").Replace("\\?", ".");
            return new Regex("^" + body + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}
