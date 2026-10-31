using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.CheckTable
{
    // R443. One entry of the dropdown Door level from: the source, its label with the plot count, and whether it can be picked
    public sealed class DoorLevelChoice
    {
        public string Source { get; set; }

        public string Label { get; set; }

        public int Plots { get; set; }

        public bool Enabled { get; set; }
    }

    // R443 Door level source counts. What the window of 4 Check shows in the row Door level from, from designBasis.doorLevel of config,
    // the step levels of the run with doorSource per plot (R408) and the pick the settings keep
    public sealed class DoorLevelPick
    {
        public const string HelpLine = DoorLevelText.NoCivil;

        private const string Slot = "<n>";

        public IReadOnlyList<DoorLevelChoice> Choices { get; private set; }

        // The source the dropdown opens on
        public string Selected { get; private set; }

        // True when config names no civil file or the file is not on disk, so the civil choice is grey
        public bool CivilGrey { get; private set; }

        // The help line under the dropdown, null while the civil choice can be picked
        public string Help => CivilGrey ? HelpLine : null;

        // Null when config holds no designBasis.doorLevel, so a config of before round 4i shows no row. levelsStep is the step file
        // step_levels_csharp.json of the run, null when the run wrote none. pick is DoorLevelSource of the settings
        public static DoorLevelPick Of(JObject config, string configPath, JObject levelsStep, string pick)
        {
            JObject door = config?["designBasis"]?["doorLevel"] as JObject;
            if (door == null)
            {
                return null;
            }
            bool civil = File.Exists(CivilPath(door, configPath) ?? "");
            var made = new DoorLevelPick { CivilGrey = !civil };
            made.Choices = DoorLevelSources.All.Select(source => new DoorLevelChoice
            {
                Source = source,
                Plots = Count(levelsStep, source),
                Enabled = source != DoorLevelSources.CivilFile || civil,
            }).ToList();
            foreach (DoorLevelChoice choice in made.Choices)
            {
                choice.Label = Label(choice.Source, choice.Plots);
            }
            JToken named = door["source"];
            string configured = named != null && named.Type == JTokenType.String ? (string)named : null;
            // A grey choice cannot stand selected in a dropdown, so a pick or a config source of a civil file that is not there falls
            // through to road level plus slope, the source that needs no file
            made.Selected = new[] { pick, configured }.FirstOrDefault(source => made.Choices.Any(choice => choice.Source == source && choice.Enabled))
                ?? DoorLevelSources.RoadPlusSlope;
            return made;
        }

        public static string Label(string source, int plots)
        {
            string text = source == DoorLevelSources.CivilFile ? DoorLevelText.Civil : DoorLevelText.Road;
            return text.Replace(Slot, plots.ToString("N0", CultureInfo.InvariantCulture));
        }

        // The plots of the step whose doorSource is source, on the written side as the plan takes it, else the raw side
        public static int Count(JObject levelsStep, string source)
        {
            int count = 0;
            foreach (JProperty plot in (levelsStep?["plots"] as JObject ?? new JObject()).Properties())
            {
                JToken value = plot.Value["written"]?["doorSource"] ?? plot.Value["raw"]?["doorSource"];
                if (value != null && value.Type == JTokenType.String && (string)value == source)
                {
                    count++;
                }
            }
            return count;
        }

        // civilFile.path of config, a relative path taken from the folder above the folder of config as addin.overrideFile and profile
        // are (CheckRun.OverridesPath), null when config names none
        public static string CivilPath(JObject door, string configPath)
        {
            JToken path = door?["civilFile"]?["path"];
            if (path == null || path.Type != JTokenType.String || ((string)path).Length == 0)
            {
                return null;
            }
            string named = (string)path;
            if (Path.IsPathRooted(named) || string.IsNullOrEmpty(configPath))
            {
                return named;
            }
            string root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? "");
            return string.IsNullOrEmpty(root) ? named : Path.Combine(root, named);
        }
    }
}
