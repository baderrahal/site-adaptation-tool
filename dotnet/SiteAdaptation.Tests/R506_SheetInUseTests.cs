using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if NET48
using SiteAdaptation.AddIn.Pane;
#endif
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R506, R507. The sheet in use, by New project and by the pane, and the real sheet of config held to links only
    public sealed class R506_SheetInUseTests
    {
        // A folder of this system, the Linux runner reads no drive letter as a root
        private static readonly string Job = Path.Combine(Path.GetTempPath(), "r506-job");
        private static readonly string Config = Path.Combine(Job, "config", "project.json");
        private const string Named = "{\"addin\":{\"parameterSheet\":\"config/shared_parameters.xlsx\"}}";
        private static readonly string InConfig = Path.Combine(Job, "config/shared_parameters.xlsx");

        private static SetupRowState Row(ProjectSettings settings, Func<string, bool> there, string configText)
        {
            return SetupCheck.Run(settings, (path, folder) => there(path), name => configText).Rows.Single(item => item.Row.Id == SetupRows.ParameterSheet);
        }

        [Fact]
        public void R506_new_project_finds_the_sheet_config_names_when_none_is_chosen()
        {
            SetupRowState row = Row(new ProjectSettings { ConfigFile = Config }, path => true, Named);
            Assert.Equal(SetupState.Found, row.State);
            Assert.Equal(new[] { InConfig }, row.Paths);
        }

        [Fact]
        public void R506_new_project_says_missing_only_when_no_sheet_is_named_or_found()
        {
            Assert.Equal(SetupState.Missing, Row(new ProjectSettings { ConfigFile = Config }, path => path != InConfig, Named).State);
            Assert.Equal(SetupState.Missing, Row(new ProjectSettings { ConfigFile = Config }, path => true, "{\"addin\":{}}").State);
            Assert.Equal(SetupState.Missing, Row(new ProjectSettings { ConfigFile = Config }, path => true, "not json").State);
            Assert.Equal(SetupState.Missing, Row(new ProjectSettings(), path => true, Named).State);
        }

        [Fact]
        public void R506_a_chosen_sheet_prevails_and_one_gone_does_not_fall_back_on_config()
        {
            var settings = new ProjectSettings { ConfigFile = Config };
            settings.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = @"X:\mine.xlsx" };
            Assert.Equal(new[] { @"X:\mine.xlsx" }, Row(settings, path => true, Named).Paths);
            // Button 1 stops on a chosen sheet that is another file, so a row reading the sheet of config would say Found on a stop
            Assert.Equal(SetupState.Missing, Row(settings, path => path != @"X:\mine.xlsx", Named).State);
        }

        [Fact]
        public void R506_config_sheet_is_null_when_the_config_gives_none_and_the_disk_failure_stays_null()
        {
            Assert.Equal(InConfig, SetupCheck.ConfigSheet(Config, name => Named));
            Assert.Null(SetupCheck.ConfigSheet(null, name => Named));
            Assert.Null(SetupCheck.ConfigSheet(Config, name => throw new IOException("locked")));
            Assert.Null(SetupCheck.ConfigSheet(Config, name => "{}"));
        }

        [Fact]
        public void R507_the_sheet_of_config_binds_every_villa_parameter_on_the_links_alone_and_plot_on_floors()
        {
            ParameterSheet sheet = ParameterSheet.FromFile(Path.Combine(ThemeGateTests.RepoRoot(), "config", "shared_parameters.xlsx"));
            Assert.Equal("11 parameters, 12 left out", sheet.Counts);
            List<ParameterSpec> links = sheet.Parameters.Where(item => item.Name != "Plot").ToList();
            Assert.Equal(10, links.Count);
            Assert.Equal(9, links.Count(item => item.Name.StartsWith("RRE_ZZ_SP_", StringComparison.Ordinal)));
            Assert.Contains(links, item => item.Name == "CASE");
            Assert.All(links, item => Assert.True(item.LinksOnly, item.Name));
            ParameterSpec plot = sheet.Parameters.Single(item => item.Name == "Plot");
            Assert.Equal(new[] { "Floors" }, plot.Categories);
            Assert.False(plot.LinksOnly);
            // R465. Button 7 fills link rows only, so no parameter it writes can stand on a floor
            Assert.DoesNotContain(sheet.Parameters.Where(item => item.FilledBy == ParameterSheet.PlaceVillasButton), item => item.Categories.Contains("Floors"));
        }

        [Fact]
        public void R506_the_counts_leave_out_no_left_out_part_when_none_is_left_out()
        {
            var rows = new List<object[]>
            {
                new object[] { "Parameter", "GUID", "Data type", "Group", "Category", "Filled by", "Sample value", "Keep" },
                new object[] { "One", "c0b2c4ee-e129-4a42-bcb6-225759b874eb", "TEXT", "Text", "RVT Links", "7", null, "Yes" },
            };
            Assert.Equal("1 parameters", ParameterSheet.FromRows("a.xlsx", rows).Counts);
        }

#if NET48
        private const string Sheet = @"X:\job\config\shared_parameters.xlsx";

        private static FileRowViewModel PaneRow(ProjectSettings settings, SheetProbe probe)
        {
            var pane = new PaneViewModel(probe);
            pane.Load(new DocumentSession("s", Path.Combine(Path.GetTempPath(), "x.rvt"), "x"));
            pane.LoadSettings(new PaneSettings(settings));
            return pane.InputRows.Single(item => item.Keys.Contains(InputKeys.ParameterSheet));
        }

        [Fact]
        public void R506_the_pane_row_shows_the_sheet_of_config_green_with_its_counts()
        {
            FileRowViewModel row = PaneRow(new ProjectSettings { ConfigFile = "cfg" }, new SheetProbe { Config = Sheet, Read = new SheetRead { Line = "11 parameters, 12 left out" } });
            Assert.True(row.Found);
            Assert.Equal("shared_parameters.xlsx", row.FileName);
            Assert.Equal("11 parameters, 12 left out", row.Hint);
            Assert.True(row.HasHint);
            Assert.False(row.HasNote);
        }

        [Fact]
        public void R506_the_pane_row_is_amber_when_no_sheet_is_named_or_found()
        {
            FileRowViewModel none = PaneRow(new ProjectSettings { ConfigFile = "cfg" }, new SheetProbe());
            Assert.False(none.Found);
            Assert.Equal(PaneText.NotChosen, none.FileName);
            Assert.False(none.HasHint);
            FileRowViewModel gone = PaneRow(new ProjectSettings { ConfigFile = "cfg" }, new SheetProbe { Config = Sheet, Gone = true, Read = new SheetRead { Line = "11 parameters" } });
            Assert.False(gone.Found);
            Assert.False(gone.HasHint);
        }

        [Fact]
        public void R506_a_sheet_that_does_not_read_is_amber_with_its_reason()
        {
            FileRowViewModel row = PaneRow(new ProjectSettings { ConfigFile = "cfg" }, new SheetProbe { Config = Sheet, Read = new SheetRead { Refused = "no column Keep" } });
            Assert.False(row.Found);
            Assert.Equal("no column Keep", row.Note);
            Assert.False(row.HasHint);
        }

        [Fact]
        public void R506_a_chosen_sheet_shows_and_counts_itself_not_the_sheet_of_config()
        {
            var settings = new ProjectSettings { ConfigFile = "cfg" };
            settings.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = @"X:\mine.xlsx" };
            var probe = new SheetProbe { Config = Sheet, Read = new SheetRead { Line = "2 parameters" } };
            FileRowViewModel row = PaneRow(settings, probe);
            Assert.Equal("mine.xlsx", row.FileName);
            Assert.Equal(@"X:\mine.xlsx", probe.LastRead);
            // Button 1 stops on it, so the row says why and shows no code name
            Assert.True(row.Found);
            Assert.Equal("mine.xlsx is not the parameter sheet of config, shared_parameters.xlsx, the one every button reads. Edit shared_parameters.xlsx instead", row.Note);
        }

        [Fact]
        public void R506_a_refused_sheet_shows_its_reason_with_no_rule_id()
        {
            FileRowViewModel row = PaneRow(new ProjectSettings { ConfigFile = "cfg" }, new SheetProbe { Config = Sheet, Read = new SheetRead { Refused = "the sheet has no column Keep (R461)" } });
            Assert.Equal("the sheet has no column Keep", row.Note);
        }

        private sealed class SheetProbe : IInputProbe
        {
            public string Config { get; set; }
            public SheetRead Read { get; set; }
            public bool Gone { get; set; }
            public string LastRead { get; private set; }

            public InputFacts Look(string path, bool folder)
            {
                return new InputFacts { Found = !string.IsNullOrEmpty(path) && !Gone, Size = 5, Stamp = 7 };
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
                return Config;
            }

            public SheetRead ReadSheet(string path)
            {
                LastRead = path;
                return Read;
            }

            public string VillaModeDefault(string configFile)
            {
                return null;
            }
        }
#endif
    }
}
