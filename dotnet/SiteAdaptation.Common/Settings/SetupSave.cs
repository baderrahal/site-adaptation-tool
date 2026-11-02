using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common.Settings
{
    // What the New project window holds when Save setup is pressed: the text of the saved project chosen under Start from, null for
    // Empty project, and the path picked for each input by its name in the settings JSON
    public sealed class SetupDraft
    {
        public string StartText { get; set; }
        public IDictionary<string, string> Picks { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        // R436, R440. The scan picks of the window, laid over those the settings hold, null for none
        public IDictionary<string, string> ScanPicks { get; set; }

        public IDictionary<string, string> ConflictPicks { get; set; }
    }

    // R432, Save setup. The settings of the project with the draft laid over them, in the settings JSON of R367 and nothing else, so
    // its format and field names stay as they are: the saved project as Import of a pre-setting lays it, then each pick as Browse of
    // the pane does. The scope, the dry run tick, the last runs and every input not picked stay. The settings given are not changed,
    // and the result is read back from its own text, so a value the reader refuses never reaches the pane. SetupWrite puts them into
    // the model at once (R445, Q280)
    public static class SetupSave
    {
        public static ProjectSettings Apply(SetupDraft draft, ProjectSettings current)
        {
            if (draft == null)
            {
                throw new ArgumentNullException(nameof(draft));
            }
            ProjectSettings next = SettingsJson.Read(SettingsJson.Write(current ?? new ProjectSettings()));
            if (draft.StartText != null)
            {
                // Review of NEWPROJ-UI: the dry run tick of the project and a parameter sheet the saved project does not name stay
                bool dryRun = next.DryRunFirst;
                next.Inputs.TryGetValue(InputNames.ParameterSheet, out SettingsInput sheet);
                PreSetting.Import(draft.StartText, next);
                next.DryRunFirst = dryRun;
                if (sheet != null && !next.Inputs.ContainsKey(InputNames.ParameterSheet))
                {
                    next.Inputs[InputNames.ParameterSheet] = sheet;
                }
            }
            foreach (KeyValuePair<string, string> pick in draft.Picks)
            {
                if (Array.IndexOf(InputNames.All, pick.Key) < 0)
                {
                    throw new ArgumentException("no input " + pick.Key + " in the settings JSON", nameof(draft));
                }
                if (string.IsNullOrEmpty(pick.Value))
                {
                    continue;
                }
                // The size and hash the settings held belong to the file before the pick
                if (!next.Inputs.TryGetValue(pick.Key, out SettingsInput held) || held == null || held.Path != pick.Value)
                {
                    next.Inputs[pick.Key] = new SettingsInput { Path = pick.Value };
                }
            }
            if (draft.ScanPicks != null && draft.ScanPicks.Count > 0)
            {
                next.ScanPicks = next.ScanPicks ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pick in draft.ScanPicks)
                {
                    next.ScanPicks[pick.Key] = pick.Value;
                }
            }
            if (draft.ConflictPicks != null && draft.ConflictPicks.Count > 0)
            {
                next.ConflictPicks = new SortedDictionary<string, string>(draft.ConflictPicks, StringComparer.Ordinal);
            }
            return SettingsJson.Read(SettingsJson.Write(next));
        }
    }
}
