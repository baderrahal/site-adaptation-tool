using System;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.Settings
{
    // W4 of steps/lane-b.md, docs/design/13-workflow-changes.md Pane. A pre-setting carries the files that hold the rules of a past job
    // to a new one: the Settings file, which names the overrides and the ramp slopes, the Rules file, the level profile, the parameter
    // sheet, the villa mode and the dry run tick. It is the settings JSON of R367 itself, so its reader refuses any number with
    // decimals and no coordinate or level can ride in it, and it holds no drawing input, no scope and no last run (CHOSEN)
    public static class PreSetting
    {
        public static readonly string[] Inputs = { InputNames.ParameterSheet };

        public static string Export(ProjectSettings job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }
            var kept = new ProjectSettings
            {
                ConfigFile = job.ConfigFile,
                RulesFile = job.RulesFile,
                LevelProfile = job.LevelProfile,
                DryRunFirst = job.DryRunFirst,
                VillaMode = job.VillaMode,

                // R436. The source picked for each value of What the tool read goes with a saved project, the conflicts of the plots
                // belong to that project and stay
                ScanPicks = job.ScanPicks == null ? null : new System.Collections.Generic.SortedDictionary<string, string>(job.ScanPicks, StringComparer.Ordinal),
            };
            foreach (string name in Inputs)
            {
                // The path alone, the size and hash belong to the file on the machine of the past job
                if (job.Inputs.TryGetValue(name, out SettingsInput input) && input != null && !string.IsNullOrEmpty(input.Path))
                {
                    kept.Inputs[name] = new SettingsInput { Path = input.Path };
                }
            }
            return SettingsJson.Write(kept);
        }

        // A pick in memory, as Browse is: the settings of the pre-setting replace those of the project, every drawing input, the scope
        // and the last runs of the project stay. One that holds a drawing input, a scope other than the whole project or a last run
        // is refused by its name, so a settings JSON taken out of a project is not imported with its paths
        public static void Import(string json, ProjectSettings into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            ProjectSettings read = SettingsJson.Read(json);
            string drawing = read.Inputs.Keys.FirstOrDefault(name => !Inputs.Contains(name));
            if (drawing != null)
            {
                throw new InvalidDataException("a pre-setting holds no drawing path, this one names the input " + drawing);
            }
            if (read.LastRuns.Count > 0 || read.Scope.Kind != ScopeKinds.Project)
            {
                throw new InvalidDataException("a pre-setting holds no scope and no last run, this one is the settings of a project");
            }
            into.ConfigFile = read.ConfigFile;
            into.RulesFile = read.RulesFile;
            into.LevelProfile = read.LevelProfile;
            into.DryRunFirst = read.DryRunFirst;
            into.VillaMode = read.VillaMode;
            if (read.ScanPicks != null)
            {
                into.ScanPicks = read.ScanPicks;
            }
            foreach (string name in Inputs)
            {
                if (read.Inputs.TryGetValue(name, out SettingsInput input))
                {
                    into.Inputs[name] = new SettingsInput { Path = input.Path };
                }
                else
                {
                    into.Inputs.Remove(name);
                }
            }
        }
    }
}
