using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace SiteAdaptation.Common.Settings
{
    // R367. The settings JSON read and written with Newtonsoft as JsonOut writes every file of Common
    public static class SettingsJson
    {
        public const int Version = 1;

        private static readonly Regex Sha256 = new Regex("^[0-9a-f]{64}$");

        // A field of a later add-in or a hand edit is refused, so no coordinate or level value rides in on a name the add-in skips
        private static readonly JsonSerializer Reader = JsonSerializer.Create(new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false } },
        });

        public static string Write(ProjectSettings settings)
        {
            return JsonConvert.SerializeObject(settings, JsonOut.Settings(Formatting.Indented));
        }

        public static ProjectSettings Read(string json)
        {
            JObject root;
            try
            {
                // The time of a run stays the text it was written as, Newtonsoft would read it as a DateTime and write it back changed
                using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                {
                    root = JToken.ReadFrom(reader) as JObject;
                    if (root != null && reader.Read())
                    {
                        throw new InvalidDataException("the settings JSON holds more after its object");
                    }
                }
            }
            catch (JsonException error)
            {
                throw new InvalidDataException("the settings JSON is no JSON object: " + error.Message, error);
            }
            if (root == null)
            {
                throw new InvalidDataException("the settings JSON is no JSON object");
            }
            JToken version = root["formatVersion"];
            if (version == null || version.Type != JTokenType.Integer || version.Value<long>() < 1)
            {
                throw new InvalidDataException("the settings JSON has no whole formatVersion from 1");
            }
            long read = version.Value<long>();
            if (read > Version)
            {
                throw new InvalidDataException("the settings JSON is formatVersion " + read + ", this add-in reads formatVersion " + Version + " and earlier");
            }
            // Newtonsoft turns 12.5 into the count 12 without a word, so a number with decimals is refused before it is read
            JToken fraction = root.Descendants().FirstOrDefault(token => token.Type == JTokenType.Float);
            if (fraction != null)
            {
                throw new InvalidDataException("the settings JSON holds the number with decimals " + fraction.ToString(Formatting.None) + " at " + fraction.Path + ", it holds no coordinate or level value");
            }
            ProjectSettings settings;
            try
            {
                settings = root.ToObject<ProjectSettings>(Reader);
            }
            catch (JsonException error)
            {
                throw new InvalidDataException("the settings JSON does not fit formatVersion " + read + ": " + error.Message, error);
            }
            Check(settings);
            return settings;
        }

        private static void Check(ProjectSettings settings)
        {
            settings.Inputs = settings.Inputs ?? new SortedDictionary<string, SettingsInput>(StringComparer.Ordinal);
            settings.Scope = settings.Scope ?? new SettingsScope();
            settings.LastRuns = settings.LastRuns ?? new SortedDictionary<string, ButtonRun>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SettingsInput> input in settings.Inputs)
            {
                if (!InputNames.All.Contains(input.Key))
                {
                    throw new InvalidDataException("the settings JSON names an input " + input.Key + ", not one of " + string.Join(", ", InputNames.All));
                }
                if (input.Value == null)
                {
                    throw new InvalidDataException("the settings JSON holds no file for the input " + input.Key);
                }
                if (input.Value.Size < 0)
                {
                    throw new InvalidDataException("the settings JSON gives the input " + input.Key + " the size " + input.Value.Size.Value.ToString(CultureInfo.InvariantCulture));
                }
                Hash(input.Value.Sha256, "the input " + input.Key);
            }
            Scope(settings.Scope, "the scope");
            OneOf(settings.VillaMode, VillaModes.All, "villaMode");
            OneOf(settings.DoorLevelSource, DoorLevelSources.All, "doorLevelSource");
            Picks(settings.ScanPicks, "scanPicks");
            Picks(settings.ConflictPicks, "conflictPicks");
            foreach (string entry in settings.CurrentFiles ?? new List<string>())
            {
                string name = (entry ?? "").Split('|')[0];
                if (!InputNames.All.Contains(name))
                {
                    throw new InvalidDataException("the settings JSON ticks " + name + " as a current file, not one of " + string.Join(", ", InputNames.All));
                }
            }
            foreach (KeyValuePair<string, ButtonRun> last in settings.LastRuns)
            {
                ButtonRun run = last.Value;
                if (run == null)
                {
                    throw new InvalidDataException("the settings JSON holds no record for the last run of button " + last.Key);
                }
                run.InputHashes = run.InputHashes ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
                run.Counts = run.Counts ?? new SortedDictionary<string, int>(StringComparer.Ordinal);
                run.Issues = run.Issues ?? new List<string>();
                OneOf(run.Mode, RunModes.All, "the mode of the last run of button " + last.Key);
                if (run.Scope != null)
                {
                    Scope(run.Scope, "the scope of the last run of button " + last.Key);
                }
                foreach (KeyValuePair<string, string> hash in run.InputHashes)
                {
                    Hash(hash.Value, "the input " + hash.Key + " of the last run of button " + last.Key);
                }
                foreach (KeyValuePair<string, int> count in run.Counts.Where(count => count.Value < 0))
                {
                    throw new InvalidDataException("the settings JSON gives the count " + count.Key + " of the last run of button " + last.Key + " the value " + count.Value.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private static void Picks(SortedDictionary<string, string> picks, string what)
        {
            if (picks != null && picks.Any(pick => string.IsNullOrEmpty(pick.Key) || string.IsNullOrEmpty(pick.Value)))
            {
                throw new InvalidDataException("the settings JSON holds an empty pick in " + what);
            }
        }

        private static void Scope(SettingsScope scope, string what)
        {
            if (!ScopeKinds.All.Contains(scope.Kind))
            {
                throw new InvalidDataException("the settings JSON names " + what + " " + scope.Kind + ", not one of " + string.Join(", ", ScopeKinds.All));
            }
        }

        private static void OneOf(string value, string[] all, string what)
        {
            if (value != null && !all.Contains(value))
            {
                throw new InvalidDataException("the settings JSON names " + what + " " + value + ", not one of " + string.Join(", ", all));
            }
        }

        private static void Hash(string value, string what)
        {
            if (value != null && !Sha256.IsMatch(value))
            {
                throw new InvalidDataException("the settings JSON gives " + what + " the SHA-256 " + value + ", not 64 lower case hex digits");
            }
        }
    }
}
