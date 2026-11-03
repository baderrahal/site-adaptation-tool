using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R505. The windows of Lane C against the boards of docs/design/boards. These tests read the XAML text only, so they run on
    // net48 and on net8.0. The colour and the size of each token are held by the theme gate, here only which element takes which token
    public sealed class R505_WindowCheckTests
    {
        internal static readonly System.Collections.Generic.Dictionary<string, string> BoardColours = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "SA.WarnBorder", "#E8D08A" },
            { "SA.ConflictCount", "#FFD580" },
            { "SA.Found", "#0B7A3E" },
            { "SA.StepWaitName", "#6B7682" },
            { "SA.ButtonHover", "#EEF3FA" },
            { "SA.ButtonHoverBorder", "#8FA6C4" },
            { "SA.AccentHover", "#003F9E" },
        };

        private const string WarnBox = "Background=\"\\{StaticResource SA.WarnGround\\}\" BorderBrush=\"\\{StaticResource SA.WarnBorder\\}\"";

        private static string Addin(params string[] parts)
        {
            string path = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn");
            foreach (string part in parts)
            {
                path = Path.Combine(path, part);
            }
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        private static string Theme { get { return Addin("Theme", "Theme.xaml"); } }
        private static string Pane { get { return Addin("Pane", "SitePanePage.xaml"); } }

        private static string RowMark()
        {
            string pane = Pane;
            int at = pane.IndexOf("x:Key=\"RowMark\"", StringComparison.Ordinal);
            return pane.Substring(at, pane.IndexOf("x:Key=\"FileRow\"", StringComparison.Ordinal) - at);
        }

        private static string Style(string xaml, string key)
        {
            Match open = Regex.Match(xaml, "<Style x:Key=\"" + Regex.Escape(key) + "\"");
            Assert.True(open.Success, "no style " + key);
            int end = xaml.IndexOf("\n  </Style>", open.Index, StringComparison.Ordinal);
            return xaml.Substring(open.Index, end - open.Index);
        }

        [Fact]
        public void R505_the_base_and_the_primary_button_react_to_the_mouse_and_a_disabled_one_wins()
        {
            string theme = Theme;
            foreach (string key in new[] { "SA.Button.Base", "SA.Button.Primary" })
            {
                string style = Style(theme, key);
                int hover = style.IndexOf("Property=\"IsMouseOver\"", StringComparison.Ordinal);
                int disabled = style.IndexOf("Property=\"IsEnabled\"", StringComparison.Ordinal);
                Assert.True(hover > 0, key + " has no IsMouseOver trigger");
                Assert.True(disabled > hover, key + " must set the disabled look after the hover");
            }
        }

        [Fact]
        public void R505_the_warn_boxes_take_the_warn_border_token()
        {
            Assert.Contains("x:Key=\"SA.WarnBorder\" Color=\"#E8D08A\"", Theme);
            foreach (string file in new[] { "ScanResults/ScanResultsWindow.xaml", "NewProject/NewProjectWindow.xaml" })
            {
                string xaml = Addin(file.Split('/'));
                Assert.Matches(WarnBox, xaml);
                Assert.DoesNotMatch("Background=\"\\{StaticResource SA.WarnGround\\}\" BorderBrush=\"\\{StaticResource SA.Border\\}\"", xaml);
            }
            Assert.Matches(WarnBox, Pane);
        }

        [Fact]
        public void R505_the_conflicts_count_has_its_own_colour()
        {
            Assert.Contains("x:Key=\"SA.ConflictCount\" Color=\"#FFD580\"", Theme);
            Assert.Matches("Foreground=\"\\{StaticResource SA.ConflictCount\\}\"[^>]*Text=\"\\{Binding ConflictCount\\}\"", Addin("ScanResults", "ScanResultsWindow.xaml"));
        }

        [Fact]
        public void R505_each_pane_section_but_the_first_has_a_line_above()
        {
            Assert.Equal(3, Regex.Matches(Pane, "<Border BorderBrush=\"\\{StaticResource SA.Border\\}\" BorderThickness=\"\\{StaticResource SA.Line.Over\\}\"").Count);
            Assert.Contains("x:Key=\"SA.Line.Over\"", Theme);
        }

        [Fact]
        public void R505_a_found_row_mark_in_the_pane_is_a_green_disc_with_a_white_tick_at_18()
        {
            string theme = Theme;
            Assert.Matches("x:Key=\"SA.Size.PaneMark\">18<", theme);
            Assert.Contains("x:Key=\"SA.Found\"", theme);
            string rowMark = RowMark();
            Assert.Contains("SA.Size.PaneMark", rowMark);
            Assert.Contains("Fill=\"{StaticResource SA.Found}\"", rowMark);
            Assert.Contains("Stroke=\"{StaticResource SA.Surface}\"", rowMark);
        }

        [Fact]
        public void R505_a_missing_row_mark_in_the_pane_is_an_amber_ring_with_an_amber_sign()
        {
            string rowMark = RowMark();
            Assert.Contains("SA.Mark.PaneMissingRing", rowMark);
            Assert.DoesNotContain("Fill=\"{StaticResource SA.Warn}\"", rowMark);
            Assert.Contains("Stroke=\"{StaticResource SA.Warn}\"", rowMark);
        }

        [Fact]
        public void R505_the_box_line_and_the_start_line_are_in_the_text_colour()
        {
            string pane = Pane;
            Assert.Matches("Style=\"\\{StaticResource SA.Pane.Help\\}\" Foreground=\"\\{StaticResource SA.Text\\}\" Text=\"\\{Binding BoxLine\\}\"", pane);
            Assert.Matches("Style=\"\\{StaticResource SA.Pane.Help\\}\" Foreground=\"\\{StaticResource SA.Text\\}\" Margin=\"\\{StaticResource SA.Gap.Top12\\}\"", pane);
        }

        [Fact]
        public void R505_step_names_are_medium_the_next_one_semibold_and_the_chip_semibold()
        {
            string theme = Theme;
            string pane = Pane;
            Assert.Matches("x:Key=\"SA.Type.StepName.Weight\">Medium<", theme);
            Assert.Contains("SA.Type.StepName.Weight", pane);
            Assert.Contains("x:Key=\"SA.Pane.Chip\"", theme);
            Assert.Contains("SA.Pane.Chip", pane);
        }

        [Fact]
        public void R505_the_dry_run_box_has_a_border_of_one_pixel()
        {
            Assert.Matches("<Border Margin=\"\\{StaticResource SA.Gap.Top8\\}\" Background=\"\\{StaticResource SA.Panel\\}\" BorderBrush=\"\\{StaticResource SA.Border\\}\" BorderThickness=\"\\{StaticResource SA.Line.All\\}\"", Pane);
        }

        [Fact]
        public void R505_a_link_in_a_line_of_text_is_semibold()
        {
            Assert.Matches("(?s)x:Key=\"SA.Pane.Link\".*?FontWeight\" Value=\"\\{StaticResource SA.Type.Label.Weight\\}\"", Theme);
        }

        [Fact]
        public void R505_on_first_open_the_steps_have_grey_names_and_a_plain_status()
        {
            string pane = Pane;
            Assert.Contains("x:Key=\"SA.StepWaitName\" Color=\"#6B7682\"", Theme);
            Assert.Contains("DataContext.FirstOpen", pane.Substring(pane.IndexOf("x:Key=\"StepRow\"", StringComparison.Ordinal)));
            Assert.Contains("SA.StepWaitName", pane);
        }

        [Fact]
        public void R505_the_band_line_of_new_project_and_how_it_works_has_the_max_width_of_its_board()
        {
            string theme = Theme;
            Assert.Matches("x:Key=\"SA.Size.NewProjectLine\">640<", theme);
            Assert.Matches("x:Key=\"SA.Size.HowLine\">660<", theme);
            Assert.Matches("MaxWidth=\"\\{StaticResource SA.Size.NewProjectLine\\}\"[^>]*Text=\"\\{x:Static np:NewProjectText.Line\\}\"", Addin("NewProject", "NewProjectWindow.xaml"));
            Assert.Matches("MaxWidth=\"\\{StaticResource SA.Size.HowLine\\}\"[^>]*Text=\"\\{x:Static how:HowItWorksText.Line\\}\"", Addin("HowItWorks", "HowItWorksWindow.xaml"));
        }

        [Fact]
        public void R505_a_missing_mark_of_new_project_is_an_amber_sign_at_14_with_no_disc()
        {
            string xaml = Addin("NewProject", "NewProjectWindow.xaml");
            Assert.Matches("x:Key=\"SA.Size.StatusMark\">14<", Theme);
            Assert.Contains("SA.Mark.StatusMissing", xaml);
            int at = xaml.IndexOf("IsMissing", StringComparison.Ordinal);
            Assert.DoesNotContain("Fill=\"{StaticResource SA.Warn}\"", xaml.Substring(at, 600));
        }

        [Fact]
        public void R505_the_selected_text_of_a_combo_clears_the_arrow_and_ends_with_an_ellipsis()
        {
            string theme = Theme;
            Assert.Matches("x:Key=\"SA.Pad.Combo\">8,0,24,0<", theme);
            string combo = Style(theme, "SA.Pane.Combo");
            Assert.Contains("<ContentPresenter IsHitTestVisible=\"False\" Margin=\"{StaticResource SA.Pad.Combo}\"", combo);
            Assert.Matches("(?s)<ContentPresenter.Resources>.*TextTrimming\" Value=\"CharacterEllipsis\"", combo);
        }

        [Fact]
        public void R505_team_check_of_review_data_keeps_not_checked_whole()
        {
            Assert.Matches("x:Key=\"SA.Grid.ReviewMirror\">72<", Theme);
            string xaml = Addin("ReviewData", "ReviewDataWindow.xaml");
            Assert.Equal(2, Regex.Matches(xaml, "SA.Grid.ReviewMirror").Count);
            int chip = xaml.IndexOf("Text=\"{Binding Check}\"", StringComparison.Ordinal);
            Assert.Contains("Property=\"Margin\" Value=\"{StaticResource SA.Gap.None}\"", xaml.Substring(chip, 400));
        }

        [Fact]
        public void R505_the_tool_value_column_of_the_check_table_has_a_width_of_its_own_in_the_head_and_the_rows()
        {
            string theme = Theme;
            Assert.Matches("x:Key=\"SA.Grid.CheckTool\">120<", theme);
            Assert.Matches("x:Key=\"SA.Grid.CheckNbh\">48<", theme);
            Assert.Matches("x:Key=\"SA.Grid.CheckPlot\">64<", theme);
            string xaml = Addin("CheckTable", "CheckTableWindow.xaml");
            Assert.Equal(2, Regex.Matches(xaml, "<ColumnDefinition Width=\"\\{StaticResource SA.Grid.CheckTool\\}\" />").Count);
            Assert.DoesNotContain("CheckType}\"/><ColumnDefinitionWidth=\"*\"", xaml.Replace(" ", "").Replace("\r", "").Replace("\n", ""));
        }

        [Fact]
        public void R505_a_long_file_name_of_new_project_is_cut_by_the_text_with_an_ellipsis()
        {
            string xaml = Addin("NewProject", "NewProjectWindow.xaml");
            Assert.DoesNotContain("<ColumnDefinition Width=\"Auto\" MaxWidth", xaml);
            Assert.Matches("MaxWidth=\"\\{StaticResource SA.Size.SetupFileName\\}\"[^>]*TextTrimming=\"CharacterEllipsis\" Text=\"\\{Binding FileName\\}\" ToolTip=\"\\{Binding ToolTip\\}\"", Regex.Replace(xaml, "\\s+", " "));
        }

        private static string ScanWith(params string[] notRead)
        {
            var checks = new Newtonsoft.Json.Linq.JArray();
            foreach (string source in notRead)
            {
                checks.Add(new Newtonsoft.Json.Linq.JObject { ["what"] = "notRead", ["source"] = source, ["plot"] = "", ["value"] = "x" });
            }
            return new Newtonsoft.Json.Linq.JObject
            {
                ["inputs"] = new Newtonsoft.Json.Linq.JArray { new Newtonsoft.Json.Linq.JObject { ["role"] = "scanCad:1", ["name"] = "site.dwg" } },
                ["rows"] = new Newtonsoft.Json.Linq.JArray { new Newtonsoft.Json.Linq.JObject { ["kind"] = "cadLayer", ["source"] = "site.dwg|A", ["found"] = "4 closed outlines, 6 texts, 1 blocks" } },
                ["checks"] = checks,
                ["conflicts"] = new Newtonsoft.Json.Linq.JArray(),
            }.ToString();
        }

        [Fact]
        public void R505_each_board_colour_is_drawn_on_a_board()
        {
            string boards = string.Concat(Directory.GetFiles(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "boards")).Select(File.ReadAllText));
            foreach (var board in BoardColours)
            {
                Assert.True(boards.IndexOf(board.Value, StringComparison.OrdinalIgnoreCase) >= 0, board.Key + " " + board.Value + " is on no board");
            }
        }

        [Fact]
        public void R505_the_row_curbs_says_it_builds_no_curb_and_its_input_name_stays()
        {
            string text = Addin("Pane", "PaneText.cs");
            Assert.Contains("CurbsRow = \"Plot fronts (curb lines)\"", text);
            Assert.Contains("CurbsHint = \"Read only. Finds the front of each plot. No curb is built.\"", text);
            // The name of the run input is a key of the step hashes in a model (R366), a new name would mark every step of a saved model changed
            Assert.Contains("Curbs = \"Curbs\"", text);
            Assert.Contains("new RunInput(PaneText.Curbs,", Addin("Commands", "ReadDataBody.cs"));
            Assert.Contains("new FileRowViewModel(PaneText.CurbsRow, false, InputKeys.Curbs) { Hint = PaneText.CurbsHint }", Addin("Pane", "PaneViewModel.cs"));
            Assert.Contains("Text=\"{Binding Hint}\"", Pane);
            Assert.Contains("[InputNames.Curbs] = \"Plot fronts (curb lines)\"", Addin("SetUpSite", "SetUpSiteText.cs"));
            Assert.Contains("Pick plots, roads, curb lines below.", Addin("NewProject", "NewProjectText.cs"));
        }

        [Fact]
        public void R505_pick_surfaces_stands_on_its_own_line_so_browse_is_whole()
        {
            int row = Pane.IndexOf("x:Key=\"FileRow\"", StringComparison.Ordinal);
            int browse = Pane.IndexOf("Click=\"OnBrowse\"", row, StringComparison.Ordinal);
            int line = Pane.IndexOf("{Binding SurfacesLine}", row, StringComparison.Ordinal);
            int pick = Pane.IndexOf("Click=\"OnPickSurfaces\"", row, StringComparison.Ordinal);
            Assert.True(browse > 0 && browse < line && line < pick, "Browse, then the line, then Pick surfaces");
        }

        [Fact]
        public void R505_plain_words_where_a_code_name_or_a_wrong_name_showed()
        {
            Assert.Contains("ColumnPlot = \"CAD label\"", Addin("ScanResults", "ScanResultsText.cs"));
            Assert.Contains("StepLine8 = \"Boundary walls\"", Addin("Pane", "PaneText.cs"));
            Assert.Contains("\"Boundary walls, corner plots included\"", Addin("HowItWorks", "HowItWorksText.cs"));
            Assert.Contains("NoChanges = \"No changes found in this Excel.\"", Addin("Update", "UpdateFromReportText.cs"));
            Assert.Contains("{Binding NoChangesLine}", Addin("Update", "UpdateFromReportWindow.xaml"));
            Assert.Equal("Ramp joint by the rule", SiteAdaptation.Common.CheckTable.CheckShown.RuleName("R471"));
            Assert.DoesNotContain("SiteAdaptation_Settings", SiteAdaptation.Common.SetUp.SetUpTexts.SignSettings);
        }

        [Fact]
        public void R505_a_file_with_only_xref_checks_reads_and_says_how_many_references_were_not_read()
        {
            var settings = new SiteAdaptation.Common.Settings.ProjectSettings();
            var file = SiteAdaptation.Common.Scan.ScanView.Read(ScanWith("site.dwg|xref a", "site.dwg|xref b", "site.dwg|xref c"), settings).Files.Single();
            Assert.Equal("Read", file.Status);
            Assert.EndsWith(". External references not read: 3.", file.Found);
            Assert.StartsWith("1 layers, 4 closed outlines", file.Found);
        }

        [Fact]
        public void R505_a_file_with_a_depth_limit_a_cycle_or_layer_pairs_check_stays_not_read()
        {
            var settings = new SiteAdaptation.Common.Settings.ProjectSettings();
            foreach (string source in new[] { "site.dwg|depth limit", "site.dwg|cycle", "site.dwg|layer pairs" })
            {
                var file = SiteAdaptation.Common.Scan.ScanView.Read(ScanWith("site.dwg|xref a", source), settings).Files.Single();
                Assert.Equal("Not read", file.Status);
            }
            Assert.DoesNotContain("External", SiteAdaptation.Common.Scan.ScanView.Read(ScanWith(), settings).Files.Single().Found);
        }
    }
}
