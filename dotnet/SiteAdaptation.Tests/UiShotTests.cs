using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using Path = System.IO.Path;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.UiShots;
using Xunit;
using Xunit.Abstractions;

namespace SiteAdaptation.Tests
{
    // R372, control 3 of docs/design/12-ui-spec.md. Each window renders offscreen with sample data at 100 percent scale through the
    // one entry the command line uses, and a window Bader approved holds to its baseline PNG within 0.5 percent of its pixels.
    // WPF, so net48 only
    public sealed class UiShotTests
    {
        public const double MostDiffering = 0.005;
        private const int RecordMax = 100 * 1024;
        private readonly ITestOutputHelper output;

        public UiShotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void R372_each_window_renders_at_96_dpi_the_pane_380_wide_and_each_png_within_the_100_kb_of_r400()
        {
            var sizes = new List<string>();
            OnSta(() =>
            {
                foreach (string window in Shots.Windows)
                {
                    BitmapSource bitmap = Shots.Render(window);
                    byte[] png = Shots.Png(bitmap);
                    sizes.Add(window + " " + bitmap.PixelWidth + "x" + bitmap.PixelHeight + " " + png.Length + " bytes");
                    Assert.Equal(96.0, bitmap.DpiX, 6);
                    Assert.True(png.Length <= RecordMax, sizes.Last());
                    Assert.True(bitmap.PixelHeight > 64, sizes.Last());
                    if (window != Shots.RibbonIcons)
                    {
                        Assert.Equal(380, bitmap.PixelWidth);
                        Assert.True(bitmap.PixelHeight > 1000, sizes.Last());
                    }
                }
                Assert.Throws<ArgumentException>(() => Shots.Render("set-up-site"));
            });
            sizes.ForEach(output.WriteLine);
            Assert.Equal(3, sizes.Count);
        }

        // Control 4, the sizes of docs/design/12-ui-spec.md read off the laid out pane: rows 32, inputs 30, Browse 28, the main
        // button 34, radius 3, the step marks 26, and the project name at the right of the band as 7.2 of the mock
        [Fact]
        public void R372_the_pane_has_rows_32_inputs_30_with_radius_3_and_the_project_name_at_the_right_of_the_band()
        {
            var counts = new List<string>();
            OnSta(() =>
            {
                foreach (bool inUse in new[] { true, false })
                {
                    SitePanePage page = Shots.LaidOutPane(inUse);
                    List<FrameworkElement> all = Visuals(page).OfType<FrameworkElement>().ToList();
                    List<Button> browse = all.OfType<Button>().Where(button => "Browse".Equals(button.Content)).ToList();
                    Assert.Equal(10, browse.Count);
                    Assert.All(browse, button => Assert.Equal(32.0, ((FrameworkElement)VisualTreeHelper.GetParent(button)).ActualHeight));
                    Assert.All(browse, button => Assert.Equal(28.0, button.ActualHeight));
                    List<Control> inputs = all.OfType<Control>().Where(control => control is ComboBox || control is TextBox).ToList();
                    Assert.Equal(3, inputs.Count);
                    Assert.All(inputs, input => Assert.Equal(30.0, input.ActualHeight));
                    Assert.All(inputs, input => Assert.Contains(Visuals(input).OfType<Border>(), border => border.CornerRadius.TopLeft == 3.0
                        && border.BorderBrush == page.FindResource("SA.Border")));
                    Button primary = all.OfType<Button>().Single(button => button.Style == page.FindResource("SA.Button.Primary"));
                    Assert.Equal(34.0, primary.ActualHeight);
                    Assert.Equal(9, all.OfType<Ellipse>().Count(mark => mark.ActualWidth == 26.0));

                    // Review 1 of PR 90: the Level profile and Neighbourhood drop downs and the Plot list box start at one x
                    List<double> lefts = inputs.Select(input => input.TransformToAncestor(page).Transform(new Point(0, 0)).X).ToList();
                    Assert.All(lefts, left => Assert.Equal(lefts[0], left, 3));

                    // Review 1 of PR 90: one section after the other at 16, a value of the spacing scale of the spec
                    List<StackPanel> sections = all.OfType<StackPanel>().Where(panel => panel.Margin == (Thickness)page.FindResource("SA.Pad.Section")).ToList();
                    Assert.Equal(4, sections.Count);
                    for (int at = 1; at < sections.Count; at++)
                    {
                        double bottom = sections[at - 1].TransformToAncestor(page).Transform(new Point(0, sections[at - 1].ActualHeight)).Y;
                        double top = sections[at].TransformToAncestor(page).Transform(new Point(0, 0)).Y;
                        Assert.Equal(16.0, top - bottom, 3);
                    }

                    TextBlock name = all.OfType<TextBlock>().Single(block => block.Text == "01101-PSN-IMC-R17B-TEST-NEW");
                    double right = name.TransformToAncestor(page).Transform(new Point(name.ActualWidth, 0)).X;
                    Assert.Equal(page.ActualWidth - 16, right, 1);
                    counts.Add((inUse ? "in use" : "first open") + ": " + browse.Count + " rows of 32, " + inputs.Count + " inputs of 30, name right edge " + right);
                }
            });
            counts.ForEach(output.WriteLine);
        }

        // QL14 and QL15, Bader 29 Oct 2026, read off the laid out pane at 380: each step row 40 high with its line on one line, cut
        // with an ellipsis and its full text as tooltip, and each input row the file name alone, its size and hash in the tooltip
        [Fact]
        public void R372_step_rows_are_40_with_the_line_on_one_line_and_input_rows_show_the_file_name_only()
        {
            var counts = new List<string>();
            OnSta(() =>
            {
                foreach (bool inUse in new[] { true, false })
                {
                    SitePanePage page = Shots.LaidOutPane(inUse);
                    List<FrameworkElement> all = Visuals(page).OfType<FrameworkElement>().ToList();
                    List<TextBlock> lines = all.OfType<TextBlock>().Where(block => PaneText.StepLines.Contains(block.Text)
                        && block.DataContext is StepRowViewModel).ToList();
                    Assert.Equal(9, lines.Count);
                    var one = new TextBlock { Style = (Style)page.FindResource("SA.Pane.Detail"), Text = "Ag" };
                    one.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    foreach (TextBlock line in lines)
                    {
                        Assert.Equal(TextWrapping.NoWrap, line.TextWrapping);
                        Assert.Equal(TextTrimming.CharacterEllipsis, line.TextTrimming);
                        Assert.Equal(line.Text, line.ToolTip as string);
                        Assert.Equal(one.DesiredSize.Height, line.ActualHeight, 1);
                        Grid row = Ancestors(line).OfType<Grid>().First(grid => grid.Margin == (Thickness)page.FindResource("SA.Gap.Row"));
                        Assert.Equal(40.0, row.ActualHeight);
                    }

                    List<Grid> rows = all.OfType<Button>().Where(button => "Browse".Equals(button.Content))
                        .Select(button => (Grid)VisualTreeHelper.GetParent(button)).ToList();
                    int detailed = 0;
                    foreach (Grid row in rows)
                    {
                        var file = (FileRowViewModel)row.DataContext;
                        TextBlock name = Visuals(row).OfType<TextBlock>().Single(block => Grid.GetColumn(block) == 2);
                        Assert.Equal(file.FileName, new TextRange(name.ContentStart, name.ContentEnd).Text);
                        Assert.Equal(file.ToolTip, (string)name.ToolTip);
                        List<string> sizes = (file.ToolTip ?? string.Empty).Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                            .Where(line => line.Contains(" MiB")).ToList();
                        if (sizes.Count > 0)
                        {
                            detailed++;
                            Assert.All(sizes, size => Assert.DoesNotContain(size, new TextRange(name.ContentStart, name.ContentEnd).Text));
                        }
                    }
                    Assert.Equal(32.0, rows.First().ActualHeight);
                    counts.Add((inUse ? "in use" : "first open") + ": " + lines.Count + " step lines on one line in rows of 40, " + rows.Count
                        + " input rows with the file name only, " + detailed + " with a size in the tooltip");
                    if (inUse)
                    {
                        Assert.True(detailed > 0, counts.Last());
                    }
                }
            });
            counts.ForEach(output.WriteLine);
        }

        private static IEnumerable<DependencyObject> Ancestors(DependencyObject child)
        {
            for (DependencyObject at = VisualTreeHelper.GetParent(child); at != null; at = VisualTreeHelper.GetParent(at))
            {
                yield return at;
            }
        }

        // Bader, 28 Oct 2026, 7.2 of docs/design/12-ui-spec.md: the row Pre-setting right under Level profile with its two buttons,
        // the row Shared parameters right after Prototypes, and on first open only the line of a past job with its link
        [Fact]
        public void R372_the_pane_shows_pre_setting_under_level_profile_shared_parameters_after_prototypes_and_the_past_job_line_on_first_open()
        {
            string spec = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "12-ui-spec.md"));
            string line = Regex.Match(spec, @"""(Or start from a past job: [^""]+)""").Groups[1].Value;
            Assert.StartsWith("Or start from a past job: Import pre-setting. ", line);
            var counts = new List<string>();
            OnSta(() =>
            {
                foreach (bool inUse in new[] { true, false })
                {
                    SitePanePage page = Shots.LaidOutPane(inUse);
                    List<FrameworkElement> all = Visuals(page).OfType<FrameworkElement>().ToList();
                    Func<FrameworkElement, Point> at = element => element.TransformToAncestor(page).Transform(new Point(0, 0));
                    Func<string, TextBlock> label = text => all.OfType<TextBlock>().Single(block => block.Text == text);

                    Assert.Equal(at(label("Level profile")).Y + 32, at(label("Pre-setting")).Y, 3);
                    Assert.Equal(at(label("Prototypes")).Y + 32, at(label("Shared parameters")).Y, 3);
                    Assert.Equal(inUse ? "The drawings and schedules the tool reads. All 8 found." : "The drawings and schedules the tool reads. 0 of 8 found.", Model(page).InputHelp);
                    Assert.True(at(label("Pre-setting")).Y < at(label("Input files")).Y);
                    foreach (string text in new[] { PaneText.ImportPastJob, PaneText.Export })
                    {
                        Button button = all.OfType<Button>().Single(item => text.Equals(item.Content));
                        Assert.Equal(28.0, button.ActualHeight);
                        Assert.Equal(at(label("Pre-setting")).Y + label("Pre-setting").ActualHeight / 2, at(button).Y + button.ActualHeight / 2, 1);
                        Assert.True(at(button).X + button.ActualWidth <= page.ActualWidth - 16, text + " runs past the pane at " + (at(button).X + button.ActualWidth));
                        Assert.Same(text == PaneText.Export ? Model(page).ExportCommand : Model(page).ImportCommand, button.Command);
                    }
                    Button import = all.OfType<Button>().Single(item => PaneText.ImportPastJob.Equals(item.Content));
                    Button export = all.OfType<Button>().Single(item => PaneText.Export.Equals(item.Content));
                    Assert.Equal(page.ActualWidth - 16, at(export).X + export.ActualWidth, 1);
                    Assert.True(at(import).X >= at(label("Pre-setting")).X + label("Pre-setting").DesiredSize.Width + 8, "Import from past job stands on the label");
                    ComboBox profile = all.OfType<ComboBox>().First();
                    double gap = at(import).Y - (at(profile).Y + profile.ActualHeight);
                    Assert.Equal(3.0, gap, 3);
                    counts.Add((inUse ? "in use" : "first open") + ": Import from past job at x " + at(import).X + ", " + gap + " under Level profile");

                    // Every row label shows in full, Shared parameters the widest, with a gap of 8 before the file name
                    double column = ((GridLength)page.FindResource("SA.Grid.Label")).Value;
                    foreach (string text in Model(page).InputRows.Select(row => row.Label).Concat(new[] { "Settings", "Rules", "Level profile", "Pre-setting" }))
                    {
                        var measure = new TextBlock { Text = text, Style = (Style)page.FindResource("SA.Type.Body") };
                        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Assert.True(measure.DesiredSize.Width + 8 <= column, text + " is " + measure.DesiredSize.Width + " wide in a column of " + column);
                    }

                    TextBlock pastJob = all.OfType<TextBlock>().Single(block => new TextRange(block.ContentStart, block.ContentEnd).Text == line);
                    Hyperlink link = pastJob.Inlines.OfType<Hyperlink>().Single();
                    Assert.Equal("Import pre-setting", new TextRange(link.ContentStart, link.ContentEnd).Text);
                    Assert.Same(Model(page).ImportCommand, link.Command);
                    Assert.Equal(inUse ? Visibility.Collapsed : Visibility.Visible, pastJob.Visibility);
                    Assert.Equal(inUse, pastJob.ActualHeight == 0);
                    counts.Add((inUse ? "in use" : "first open") + ": past job line " + pastJob.ActualHeight + " high");
                }
            });
            counts.ForEach(output.WriteLine);
        }

        // Review 1 of PR 92: with no document the link cannot run, so it takes the look of the grey buttons, SA.Wait and no
        // underline, from its style in Theme.xaml, and with a document the accent and the underline
        [Fact]
        public void R372_the_link_import_pre_setting_is_accent_and_underlined_only_while_its_command_can_run()
        {
            var looks = new List<string>();
            OnSta(() =>
            {
                foreach (bool open in new[] { false, true })
                {
                    SitePanePage page = Shots.LaidOutPane(false, open);
                    PaneViewModel model = Model(page);
                    Hyperlink link = Visuals(page).OfType<TextBlock>().SelectMany(block => block.Inlines.OfType<Hyperlink>()).Single();
                    Color accent = ((SolidColorBrush)page.FindResource("SA.Accent")).Color;
                    Color wait = ((SolidColorBrush)page.FindResource("SA.Wait")).Color;
                    Color shown = ((SolidColorBrush)link.Foreground).Color;
                    looks.Add((open ? "document" : "no document") + ": enabled " + link.IsEnabled + ", " + shown + ", underline " + (link.TextDecorations != null && link.TextDecorations.Count > 0));

                    Assert.Equal(open, model.ImportCommand.CanExecute(null));
                    Assert.Equal(open, link.IsEnabled);
                    if (!open)
                    {
                        Assert.NotEqual(accent, shown);
                    }
                    Assert.Equal(open ? accent : wait, shown);
                    Assert.Equal(open, link.TextDecorations != null && link.TextDecorations.Count > 0);
                    Assert.Same(page.FindResource("SA.Pane.Link"), link.Style);
                }
            });
            looks.ForEach(output.WriteLine);
            string xaml = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Pane", "SitePanePage.xaml"));
            Assert.DoesNotMatch(@"<Hyperlink[^>]*\bForeground=", xaml);
        }

        private static PaneViewModel Model(SitePanePage page)
        {
            return (PaneViewModel)page.DataContext;
        }

        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            for (int at = 0; at < VisualTreeHelper.GetChildrenCount(root); at++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, at);
                yield return child;
                foreach (DependencyObject below in Visuals(child))
                {
                    yield return below;
                }
            }
        }

        [Fact]
        public void R372_the_drafts_in_steps_ui_shots_are_there_each_within_100_kb()
        {
            string folder = Path.Combine(ThemeGateTests.RepoRoot(), "steps", "ui-shots");
            foreach (string window in Shots.Windows)
            {
                var file = new FileInfo(Path.Combine(folder, window + ".png"));
                Assert.True(file.Exists, file.FullName);
                Assert.True(file.Length <= RecordMax, file.FullName + " " + file.Length);
            }
            Assert.True(File.Exists(Path.Combine(folder, "README.md")));
            Assert.True(Directory.Exists(Path.Combine(folder, "baseline")));
        }

        // Empty until Bader approves a draft, then the window must render within 0.5 percent of its pixels of the baseline
        [Fact]
        public void R372_each_window_with_an_approved_baseline_renders_within_half_a_percent_of_its_pixels()
        {
            string baseline = Path.Combine(ThemeGateTests.RepoRoot(), "steps", "ui-shots", "baseline");
            var compared = new List<string>();
            OnSta(() =>
            {
                foreach (string window in Shots.Windows)
                {
                    string path = Path.Combine(baseline, window + ".png");
                    if (!File.Exists(path))
                    {
                        continue;
                    }
                    double share = Shots.Differ(Shots.Load(path), Shots.AsPng(window));
                    compared.Add(window + " differs on " + (share * 100).ToString("0.000") + " percent of its pixels");
                    Assert.True(share <= MostDiffering, compared.Last());
                }
            });
            if (compared.Count == 0)
            {
                output.WriteLine("No baseline yet in " + baseline + ", nothing to compare until Bader approves a draft");
            }
            compared.ForEach(output.WriteLine);
            Assert.Empty(Directory.EnumerateFiles(baseline, "*.png").Select(Path.GetFileNameWithoutExtension).Except(Shots.Windows));
        }

        [Fact]
        public void R372_the_compare_counts_the_pixels_that_differ_and_takes_a_size_change_as_all_of_them()
        {
            OnSta(() =>
            {
                BitmapSource ground = Plain(100, 100, 0);
                Assert.Equal(0.0, Shots.Differ(ground, Plain(100, 100, 0)));
                Assert.Equal(0.004, Shots.Differ(ground, Plain(100, 100, 40)), 6);
                Assert.Equal(0.006, Shots.Differ(ground, Plain(100, 100, 60)), 6);
                Assert.True(Shots.Differ(ground, Plain(100, 100, 40)) <= MostDiffering);
                Assert.False(Shots.Differ(ground, Plain(100, 100, 60)) <= MostDiffering);
                Assert.Equal(1.0, Shots.Differ(ground, Plain(100, 101, 0)));

                BitmapSource pane = Shots.Render(Shots.Pane);
                BitmapSource again = Shots.AsPng(Shots.Pane);
                Assert.Equal(0.0, Shots.Differ(Shots.AsPng(Shots.Pane), again));
                Assert.True(Shots.Differ(again, Shots.AsPng(Shots.PaneFirstOpen)) > MostDiffering);
                Assert.Equal(pane.PixelWidth, again.PixelWidth);
            });
        }

        // A white bitmap with its first count pixels black
        private static BitmapSource Plain(int width, int height, int count)
        {
            byte[] pixels = Enumerable.Repeat((byte)255, width * height * 4).ToArray();
            for (int at = 0; at < count; at++)
            {
                pixels[at * 4] = 0;
                pixels[at * 4 + 1] = 0;
                pixels[at * 4 + 2] = 0;
            }
            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException("on the STA thread: " + failure);
            }
        }
    }
}
