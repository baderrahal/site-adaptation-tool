using System;
using System.IO;
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
    }
}
