using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.SetUp
{
    // One file of the setup in the section Current files of 1 Set up site
    public sealed class CurrentFileRow
    {
        // The input by its name in the settings JSON
        public string Input { get; set; }

        public string FileName { get; set; }

        // The site drawing of R430, the one tick Apply waits for
        public bool Site { get; set; }

        public bool Current { get; set; }
    }

    // A saved rules profile for the Rules dropdown: the name it gives itself and its path
    public sealed class RulesChoice
    {
        public string Name { get; set; }

        public string Path { get; set; }
    }

    // R441 Current files gate. Input: the settings of the project and the rules profiles beside the one it names. Output: one row per
    // file of the setup with its Current tick, the profiles, Apply of 1 Set up site allowed only once the site drawing is ticked, and
    // the line Current files of the report of button 9. The prototypes are a folder of models, no drawing to tick, so they are left out
    public static class CurrentFiles
    {
        public const string ReportLabel = "Current files: ";
        public const string NoneTicked = "none ticked";

        public static List<CurrentFileRow> Rows(ProjectSettings settings)
        {
            var rows = new List<CurrentFileRow>();
            if (settings == null)
            {
                return rows;
            }
            ICollection<string> ticked = settings.CurrentFiles ?? new List<string>();
            foreach (string name in InputNames.All.Where(name => name != InputNames.Prototypes))
            {
                if (!settings.Inputs.TryGetValue(name, out SettingsInput input) || input == null || string.IsNullOrEmpty(input.Path))
                {
                    continue;
                }
                string file = Path.GetFileName(input.Path);
                rows.Add(new CurrentFileRow { Input = name, FileName = file, Site = name == InputNames.Baseplan, Current = ticked.Contains(Entry(name, file)) });
            }
            return rows;
        }

        // Breaker of SCAN-UI: a tick holds the input and the name of its file, so a new file picked for the input is not ticked
        public static string Entry(string input, string fileName)
        {
            return input + "|" + fileName;
        }

        public static bool CanApply(IEnumerable<CurrentFileRow> rows)
        {
            return rows != null && rows.Any(row => row.Site && row.Current);
        }

        // The settings with the ticks and the rules file of the window laid over them, the settings given unchanged. Null ticks, as a
        // stand-in of the test host gives, keep the ticks the settings hold
        public static ProjectSettings Apply(ProjectSettings current, IEnumerable<string> ticked, string rulesFile)
        {
            ProjectSettings next = SettingsJson.Read(SettingsJson.Write(current ?? new ProjectSettings()));
            if (ticked != null)
            {
                next.CurrentFiles = ticked.Distinct(StringComparer.Ordinal).OrderBy(name => Array.IndexOf(InputNames.All, name))
                    .Where(name => next.Inputs.TryGetValue(name, out SettingsInput input) && input != null && !string.IsNullOrEmpty(input.Path))
                    .Select(name => Entry(name, Path.GetFileName(next.Inputs[name].Path))).ToList();
            }
            if (!string.IsNullOrEmpty(rulesFile))
            {
                next.RulesFile = rulesFile;
            }
            return SettingsJson.Read(SettingsJson.Write(next));
        }

        public static string ReportLine(ProjectSettings settings)
        {
            List<string> names = Rows(settings).Where(row => row.Current).Select(row => row.FileName).ToList();
            return ReportLabel + (names.Count == 0 ? NoneTicked : string.Join(", ", names));
        }

        // The body of button 9 with the line Current files after the lines of its preview
        public static ButtonBody WithReportLine(ButtonBody body, Func<ProjectSettings> settings)
        {
            Func<RunContext, RunSummary> preview = body.Preview;
            if (preview != null)
            {
                body.Preview = context =>
                {
                    RunSummary summary = preview(context);
                    return new RunSummary(summary.Counts, summary.PerPlot.Concat(new[] { ReportLine(settings()) }).ToList());
                };
            }
            return body;
        }

        // The profiles in the folder of the rules file the settings name, else in the folder rules beside the folder of config, by the
        // name each gives itself in its field project, else its file name. A file that does not read as a profile is left out
        public static List<RulesChoice> Profiles(ProjectSettings settings, Func<string, IEnumerable<string>> listFiles, Func<string, string> readText)
        {
            var choices = new List<RulesChoice>();
            string folder = RulesFolder(settings);
            if (folder == null)
            {
                return choices;
            }
            IEnumerable<string> files;
            try
            {
                files = listFiles(folder) ?? new string[0];
            }
            catch (IOException)
            {
                return choices;
            }
            catch (UnauthorizedAccessException)
            {
                return choices;
            }
            foreach (string path in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    JObject profile = JObject.Parse(readText(path));
                    string name = profile["project"]?.Type == JTokenType.String ? (string)profile["project"] : System.IO.Path.GetFileNameWithoutExtension(path);
                    choices.Add(new RulesChoice { Name = name, Path = path });
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is Newtonsoft.Json.JsonException)
                {
                }
            }
            return choices;
        }

        private static string RulesFolder(ProjectSettings settings)
        {
            if (settings == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(settings.RulesFile))
            {
                return System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(settings.RulesFile));
            }
            if (!string.IsNullOrEmpty(settings.ConfigFile))
            {
                string configFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(settings.ConfigFile));
                string root = System.IO.Path.GetDirectoryName(configFolder);
                return root == null ? null : System.IO.Path.Combine(root, "rules");
            }
            return null;
        }
    }

    // R441. The window of button 1 with the section Current files: the rows and the profiles go into every view it shows, the one a
    // dry run or a sheet gives back too, and Apply lays the ticks and the rules file chosen into the settings before the run records them
    public sealed class CurrentFilesDialog : ISetUpSiteDialog
    {
        private readonly ISetUpSiteDialog inner;
        private readonly Func<ProjectSettings> settings;
        private readonly Action<ProjectSettings> keep;
        private readonly Func<string, IEnumerable<string>> listFiles;
        private readonly Func<string, string> readText;

        public CurrentFilesDialog(ISetUpSiteDialog inner, Func<ProjectSettings> settings, Action<ProjectSettings> keep, Func<string, IEnumerable<string>> listFiles, Func<string, string> readText)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.keep = keep ?? throw new ArgumentNullException(nameof(keep));
            this.listFiles = listFiles;
            this.readText = readText;
        }

        public SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks)
        {
            ProjectSettings now = settings();
            List<CurrentFileRow> rows = CurrentFiles.Rows(now);
            List<RulesChoice> profiles = listFiles == null || readText == null ? new List<RulesChoice>() : CurrentFiles.Profiles(now, listFiles, readText);
            Func<SetUpSiteView, SetUpSiteView> fill = next =>
            {
                if (next != null)
                {
                    next.CurrentFiles = rows;
                    next.RulesProfiles = profiles;
                    next.RulesFile = now?.RulesFile;
                }
                return next;
            };
            var wrapped = new SetUpSiteCallbacks();
            if (callbacks?.DryRun != null)
            {
                wrapped.DryRun = slope => fill(callbacks.DryRun(slope));
            }
            if (callbacks?.SheetChosen != null)
            {
                wrapped.SheetChosen = path => fill(callbacks.SheetChosen(path));
            }
            SetUpSiteAnswer answer = inner.Show(fill(view), wrapped);
            if (answer != null && answer.Apply)
            {
                keep(CurrentFiles.Apply(settings(), answer.CurrentFiles, answer.RulesFile));
            }
            return answer;
        }
    }
}
