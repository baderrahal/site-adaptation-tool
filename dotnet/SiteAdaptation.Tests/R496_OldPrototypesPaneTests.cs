#if NET48
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R496. The line under the row Prototypes. The look is given by the test, so no disk and no Revit are read
    public sealed class R496_OldPrototypesPaneTests
    {
        private const string Expected2021 = "These files are Revit 2021 and upgrade on each use, use the 2024 copies. A remake from them takes 20 to 41 minutes.";
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "r496-pane-protos");

        private static FileRowViewModel Row(PaneViewModel pane)
        {
            return pane.InputRows.Single(item => item.Keys.Contains(InputKeys.Prototypes));
        }

        private static PaneViewModel Pane(Action<string, Action<int?>> look, int? running, bool folderThere = true, string path = null)
        {
            var settings = new ProjectSettings { ConfigFile = "cfg" };
            settings.Inputs[InputNames.Prototypes] = new SettingsInput { Path = path ?? Folder };
            var pane = new PaneViewModel(new FolderProbe(folderThere)) { FormatLook = look, RunningRevit = running };
            pane.Load(new DocumentSession("s", Path.Combine(Path.GetTempPath(), "x.rvt"), "x"));
            pane.LoadSettings(new PaneSettings(settings));
            return pane;
        }

        [Fact]
        public void R496_a_folder_of_2021_files_warns_under_the_row_with_the_year_read_and_the_running_revit()
        {
            PaneViewModel pane = Pane((path, done) => done(2021), 2024);
            Assert.Equal(Expected2021, Row(pane).Note);
            Assert.True(Row(pane).HasNote);
            Assert.Equal("These files are Revit 2022 and upgrade on each use, use the 2025 copies. A remake from them takes 20 to 41 minutes.", Row(Pane((path, done) => done(2022), 2025)).Note);
        }

        [Fact]
        public void R496_a_folder_of_the_running_format_or_a_newer_one_does_not_warn()
        {
            Assert.Equal(string.Empty, Row(Pane((path, done) => done(2024), 2024)).Note);
            Assert.False(Row(Pane((path, done) => done(2024), 2024)).HasNote);
            // Review 1, the colour pair of docs/design/12-ui-spec.md: SA.Warn stands on SA.WarnGround
            string page = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Pane", "SitePanePage.xaml"));
            int note = page.IndexOf("Text=\"{Binding Note}\"", StringComparison.Ordinal);
            Assert.True(page.LastIndexOf("SA.WarnGround", note, StringComparison.Ordinal) > page.LastIndexOf("HasNote", note, StringComparison.Ordinal));
            Assert.Equal(string.Empty, Row(Pane((path, done) => done(2025), 2024)).Note);
        }

        [Fact]
        public void R496_no_file_read_no_folder_no_look_and_no_running_revit_give_no_warning()
        {
            Assert.Equal(string.Empty, Row(Pane((path, done) => done(null), 2024)).Note);
            Assert.Equal(string.Empty, Row(Pane((path, done) => done(2021), 2024, folderThere: false)).Note);
            Assert.Equal(string.Empty, Row(Pane(null, 2024)).Note);
            Assert.Equal(string.Empty, Row(Pane((path, done) => done(2021), null)).Note);
            Assert.Equal(string.Empty, Row(Pane((path, done) => done(2021), 2024, path: string.Empty)).Note);
        }

        [Fact]
        public void R496_the_warning_shows_only_when_the_look_comes_back_and_the_folder_is_looked_at_once()
        {
            var asked = new List<string>();
            Action<int?> later = null;
            PaneViewModel pane = Pane((path, done) =>
            {
                asked.Add(path);
                later = done;
            }, 2024);
            Assert.Equal(string.Empty, Row(pane).Note);
            Assert.Single(asked);
            later(2021);
            Assert.Equal(Expected2021, Row(pane).Note);
            Assert.Single(asked);
            pane.LoadSettings(pane.Settings);
            Assert.Single(asked);
        }

        [Fact]
        public void R496_the_warning_stops_nothing_and_the_row_still_counts_as_found()
        {
            PaneViewModel pane = Pane((path, done) => done(2021), 2024);
            Assert.True(Row(pane).Found);
            Assert.Equal(PaneRunStates.NoteLeavesStepsAlone(pane), PaneRunStates.NoteLeavesStepsAlone(Pane(null, 2024)));
        }

        private static class PaneRunStates
        {
            public static string NoteLeavesStepsAlone(PaneViewModel pane)
            {
                return string.Join(",", StepWords.Of(pane));
            }
        }

        private sealed class FolderProbe : IInputProbe
        {
            private readonly bool there;

            public FolderProbe(bool there)
            {
                this.there = there;
            }

            public InputFacts Look(string path, bool folder)
            {
                return new InputFacts { Found = there && !string.IsNullOrEmpty(path), Size = 5, Stamp = 7 };
            }

            public void Hash(string path, bool folder, Action<string> done)
            {
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = null;
                return new string[0];
            }

            public string RulesName(string rulesFile)
            {
                return null;
            }

            public string ConfigSheet(string configFile)
            {
                return null;
            }

            public SiteAdaptation.AddIn.Pane.SheetRead ReadSheet(string path)
            {
                return null;
            }

            public string VillaModeDefault(string configFile)
            {
                return null;
            }
        }
    }
}
#endif
