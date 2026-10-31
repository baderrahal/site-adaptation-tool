using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Scan
{
    // R436. The picks of the window What the tool read laid over the settings, and the export of what the window shows. Neither writes a
    // model. The settings reach the model with the next real run (R367), as the setup of R432 does
    public static class ScanPicks
    {
        public const string ValuesSheet = "Values";
        public const string FilesSheet = "Files";
        public const string ConflictsSheet = "Conflicts";

        private static readonly string[] ValueHeaders = { "Value", "Source", "Covers", "Checks", "Status" };
        private static readonly string[] FileHeaders = { "File", "Kind", "What it found", "Status" };
        private static readonly string[] ConflictHeaders = { "Plot", "Value", "Use", "Choice A", "Choice B" };

        // The picks given replace the pick held for the same kind or conflict, every other pick stays. The settings given are not changed,
        // and the result is read back from its own text, so a pick the reader refuses never reaches the pane
        public static ProjectSettings Apply(IDictionary<string, string> valuePicks, IDictionary<string, string> conflictPicks, ProjectSettings current)
        {
            ProjectSettings next = SettingsJson.Read(SettingsJson.Write(current ?? new ProjectSettings()));
            next.ScanPicks = Lay(next.ScanPicks, valuePicks);
            next.ConflictPicks = Lay(next.ConflictPicks, conflictPicks);
            return SettingsJson.Read(SettingsJson.Write(next));
        }

        // Three sheets as the window shows them now, the picks included. A path that exists is refused, so no earlier export is lost
        public static void Export(string path, ScanViewData view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            if (File.Exists(path))
            {
                throw new IOException("the file " + Path.GetFileName(path) + " is there already, give a new name");
            }
            var values = view.Values.Select(value => (IReadOnlyList<string>)new[]
            {
                value.Label, value.Sources.FirstOrDefault(source => source.Raw == value.Selected)?.Shown ?? string.Empty, value.Covers, value.Checks, value.Status,
            }).ToList();
            var files = view.Files.Select(file => (IReadOnlyList<string>)new[] { file.Name, file.Kind, file.Found, file.Status }).ToList();
            var conflicts = view.Conflicts.Select(conflict => (IReadOnlyList<string>)new[] { conflict.Plot, conflict.Value, conflict.SelectedChoice, conflict.ChoiceA, conflict.ChoiceB }).ToList();
            TemplateBook.Write(path, new[]
            {
                new TemplateSheet(ValuesSheet, 1, ValueHeaders, values),
                new TemplateSheet(FilesSheet, 1, FileHeaders, files),
                new TemplateSheet(ConflictsSheet, 1, ConflictHeaders, conflicts),
            });
        }

        private static SortedDictionary<string, string> Lay(SortedDictionary<string, string> held, IDictionary<string, string> picks)
        {
            var laid = held == null ? new SortedDictionary<string, string>(StringComparer.Ordinal) : new SortedDictionary<string, string>(held, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pick in picks ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrEmpty(pick.Key) && !string.IsNullOrEmpty(pick.Value))
                {
                    laid[pick.Key] = pick.Value;
                }
            }
            return laid.Count == 0 ? held : laid;
        }
    }
}
