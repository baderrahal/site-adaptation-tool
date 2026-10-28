using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Icons;
using SiteAdaptation.AddIn.Theme;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R370, R371. The add-in shell of 5a on net48, the target Revit 2024 runs: the ribbon layout against the approved mock, the
    // commands, the resolver of docs/design/08-install.md, the icons rendered from the theme and the bundled font. No Revit type is
    // touched, so no Revit is needed
    public sealed class AddInTests
    {
        // 7.1 of docs/design/12-ui-spec.md holds the ribbon of Bader's changes of 28 Oct 2026 and prevails over item 1 of
        // docs/addin-mock.md until the fold in, line 10 of its Rules
        [Fact]
        public void R371_the_ribbon_layout_is_the_tab_the_panels_and_the_labels_of_7_1_of_the_ui_spec_in_its_order()
        {
            string spec = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "12-ui-spec.md")).Replace("\r\n", "\n");
            string ribbon = spec.Split('\n').SkipWhile(text => text != "### 7.1 Ribbon").Skip(1).First(text => text.StartsWith("Ribbon tab ", StringComparison.Ordinal));
            Match line = Regex.Match(ribbon, @"^Ribbon tab ""([^""]+)"" with four panels in this order \([^)]*\): (.*)\. Large buttons");
            Assert.True(line.Success, ribbon);
            List<Tuple<string, string[]>> panels = Regex.Matches(line.Groups[2].Value, @"(\w+) \(([^)]*)\)").Cast<Match>()
                .Select(match => Tuple.Create(match.Groups[1].Value, match.Groups[2].Value.Split(',').Select(label => label.Trim()).ToArray())).ToList();

            Assert.Equal(RibbonLayout.Tab, line.Groups[1].Value);
            Assert.Equal(4, panels.Count);
            Assert.Equal(panels.Select(panel => panel.Item1), RibbonLayout.Panels.Select(panel => panel.Name));
            for (int at = 0; at < panels.Count; at++)
            {
                Assert.Equal(panels[at].Item2, RibbonLayout.Panels[at].Buttons.Select(button => button.Label));
            }
            Assert.Equal(Enumerable.Range(1, 9), RibbonLayout.Buttons.Select(button => button.Number));
            Assert.All(RibbonLayout.Buttons, button => Assert.StartsWith(button.Number + " ", button.Label));
            Assert.Equal(new[] { "Site Adaptation Rules", "Update from report" }, RibbonLayout.AllButtons.Where(button => button.Number == 0).Select(button => button.Label));
            Assert.Equal(11, RibbonLayout.AllButtons.Select(button => button.Key).Distinct().Count());
            Assert.All(RibbonLayout.AllButtons, button => Assert.Equal(ThemeGateTests.IconKey(button.Number > 0 ? button.Number.ToString(CultureInfo.InvariantCulture) : null, button.Label), button.Key));
            Assert.Equal("SA.Icon.UpdateFromReport.16", RibbonLayout.ButtonOf("UpdateFromReportCommand").IconKey16);
        }

        // A second reading of the order, from the part Order of docs/design/13-workflow-changes.md
        [Fact]
        public void R371_the_numbered_buttons_are_the_nine_steps_of_the_order_of_the_workflow_changes()
        {
            string order = File.ReadAllLines(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "13-workflow-changes.md")).Single(text => text.StartsWith("- Nine numbered steps: ", StringComparison.Ordinal));
            string[] steps = order.Substring("- Nine numbered steps: ".Length).TrimEnd('.').Split(',').Select(step => step.Trim()).ToArray();

            Assert.Equal(steps, RibbonLayout.Buttons.Select(button => button.Label));
        }

        [Fact]
        public void R371_each_button_has_its_own_command_class_that_runs_its_number_or_its_name_and_the_buttons_share_one_availability_class()
        {
            string source = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Commands", "ButtonCommands.cs"));
            var classes = RibbonLayout.AllButtons.Select(button => button.CommandClass).ToList();

            Assert.Equal(11, classes.Distinct().Count());
            foreach (RibbonButtonSpec button in RibbonLayout.AllButtons)
            {
                string name = button.CommandClass.Substring(button.CommandClass.LastIndexOf('.') + 1);
                Assert.StartsWith("SiteAdaptation.AddIn.Commands.", button.CommandClass);
                Match body = Regex.Match(source, @"public sealed class " + name + @" : IExternalCommand\s*\{.*?Buttons\.Run\((\d+|nameof\(\w+\)), ", RegexOptions.Singleline);
                Assert.True(body.Success, name);
                Assert.Equal(button.Number > 0 ? button.Number.ToString(CultureInfo.InvariantCulture) : "nameof(" + name + ")", body.Groups[1].Value);
                Assert.Same(button, button.Number > 0 ? RibbonLayout.Button(button.Number) : RibbonLayout.ButtonOf(name));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => RibbonLayout.ButtonOf("NoSuchCommand"));
            Assert.Matches(@"namespace SiteAdaptation\.AddIn\.Commands", source);
            Assert.Matches(@"public sealed class " + RibbonLayout.AvailabilityClass.Split('.').Last() + @" : IExternalCommandAvailability", source);
        }

        [Fact]
        public void R371_the_buttons_are_grey_with_no_document_and_in_a_family_document()
        {
            Assert.False(RibbonLayout.IsAvailable(false, false));
            Assert.False(RibbonLayout.IsAvailable(true, true));
            Assert.True(RibbonLayout.IsAvailable(true, false));
        }

        [Fact]
        public void R371_the_one_entry_of_a_button_tells_its_label_and_refuses_a_number_off_the_ribbon()
        {
            Assert.Equal("7 Place villas is not built yet.", Buttons.NotBuiltText(7));
            Assert.Equal("9 Report is not built yet.", Buttons.NotBuiltText(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => Buttons.NotBuiltText(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Buttons.NotBuiltText(10));
        }

        // The flow of Common is handed every dependency as null, so a not-built body that touched one would throw. The two buttons
        // with no number take the same path, built in 5c and 5f (CHOSEN in docs/design/13-workflow-changes.md)
        [Fact]
        public void R382_a_button_with_no_body_says_it_is_not_built_yet_through_the_flow_before_any_check()
        {
            var untouched = new ButtonFlow(null, null, null, null, null);
            var told = new List<string>();
            foreach (RibbonButtonSpec button in RibbonLayout.AllButtons)
            {
                RunOutcome outcome = untouched.Run(button.Number, button.Label, Bodies.Of(button), null);
                Assert.Equal(RunEnd.Stopped, outcome.End);
                Assert.Equal(button.Label + " is not built yet.", outcome.Message);
                told.Add(outcome.Message);
            }
            Assert.Equal(11, told.Count);
            Assert.Contains("Site Adaptation Rules is not built yet.", told);
            Assert.Contains("Update from report is not built yet.", told);
            Assert.Equal("7 Place villas is not built yet.", untouched.Run(7, "7 Place villas", Bodies.Of(7), @"C:\Models\Site.rvt").Message);
        }

        [Fact]
        public void R370_the_resolver_serves_its_own_folder_of_the_same_or_a_higher_version_to_the_tool_only()
        {
            string folder = Path.Combine("C:", "addin");
            string common = Path.Combine(folder, "SiteAdaptation.Common.dll");
            var files = new Dictionary<string, Version> { { common, new Version(1, 2, 0, 0) } };
            Func<string, bool> exists = files.ContainsKey;
            Func<string, Version> versionOf = path => files[path];
            Func<string, string, string> serve = (asked, requester) => OwnFolderResolver.PathToServe(folder, new AssemblyName(asked), requester, exists, versionOf);

            Assert.Equal(common, serve("SiteAdaptation.Common, Version=1.2.0.0", "SiteAdaptation.AddIn"));
            Assert.Equal(common, serve("SiteAdaptation.Common, Version=1.0.0.0", "SiteAdaptation.Placer"));
            Assert.Equal(common, serve("SiteAdaptation.Common, Version=1.2.0.0", null));
            Assert.Equal(common, serve("SiteAdaptation.Common", "RevitPrototypeReader"));
            Assert.Null(serve("SiteAdaptation.Common, Version=1.3.0.0", "SiteAdaptation.AddIn"));
            Assert.Null(serve("SiteAdaptation.Common, Version=1.2.0.0", "OtherVendor.Addin"));
            Assert.Null(serve("Newtonsoft.Json, Version=13.0.0.0", "SiteAdaptation.Common"));
            Assert.True(OwnFolderResolver.IsToolAssembly("SiteAdaptation.TestHost"));
            Assert.True(OwnFolderResolver.IsToolAssembly("CadReader"));
            Assert.False(OwnFolderResolver.IsToolAssembly("SiteAdaptationX"));
            Assert.False(OwnFolderResolver.IsToolAssembly(null));
        }

        [Fact]
        public void R371_each_icon_renders_from_the_theme_at_32_and_16_units_at_scale_1_and_1_5_with_drawn_pixels_of_its_colours()
        {
            var counts = new List<string>();
            OnSta(() =>
            {
                ResourceDictionary theme = ThemeDictionary.Load();
                Color heading = ((SolidColorBrush)theme["SA.Heading"]).Color;
                Color accent = ((SolidColorBrush)theme["SA.IconAccent"]).Color;
                foreach (RibbonButtonSpec button in RibbonLayout.AllButtons)
                {
                    foreach (double scale in new[] { 1.0, 1.5 })
                    foreach (int units in new[] { 32, 16 })
                    {
                        BitmapSource bitmap = IconRenderer.Render(theme, units == 32 ? button.IconKey32 : button.IconKey16, units, scale);
                        int size = (int)(units * scale);
                        Assert.Equal(size, bitmap.PixelWidth);
                        Assert.Equal(size, bitmap.PixelHeight);
                        Assert.Equal(units, bitmap.Width, 6);
                        Assert.Equal(units, bitmap.Height, 6);
                        Assert.Equal(96 * scale, bitmap.DpiX, 6);
                        Assert.True(bitmap.IsFrozen);
                        byte[] pixels = new byte[size * size * 4];
                        bitmap.CopyPixels(pixels, size * 4, 0);
                        int drawn = 0;
                        int headingLike = 0;
                        int accentLike = 0;
                        for (int at = 0; at < pixels.Length; at += 4)
                        {
                            if (pixels[at + 3] == 0)
                            {
                                continue;
                            }
                            drawn++;
                            if (pixels[at + 3] >= 128)
                            {
                                headingLike += Near(pixels, at, heading) ? 1 : 0;
                                accentLike += Near(pixels, at, accent) ? 1 : 0;
                            }
                        }
                        counts.Add(button.Key + "@" + units + "x" + scale.ToString(CultureInfo.InvariantCulture) + " drawn " + drawn + " heading " + headingLike + " accent " + accentLike);
                        Assert.True(drawn >= size, counts.Last());
                        Assert.True(drawn < size * size, counts.Last());
                        Assert.True(headingLike > 0, counts.Last());
                        Assert.True(accentLike > 0 || button.Number == 4, counts.Last());
                    }
                }
            });
            Assert.Equal(44, counts.Count);
        }

        // The scale of WPF against the one GDI gives for the screen, a second reading of the same DPI. The test process is DPI
        // aware once WPF has loaded, as Revit is
        [Fact]
        public void R371_the_icon_scale_is_the_system_dpi_of_the_process_over_96()
        {
            double scale = 0;
            OnSta(() => scale = IconRenderer.ScreenScale());
            IntPtr screen = GetDC(IntPtr.Zero);
            int gdiDpi;
            try
            {
                gdiDpi = GetDeviceCaps(screen, LogPixelsX);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
            }
            Assert.True(scale >= 1.0, scale.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(gdiDpi / 96.0, scale, 6);
        }

        [Fact]
        public void R371_an_icon_of_no_pixel_or_a_key_off_the_theme_is_refused()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = ThemeDictionary.Load();
                Assert.Throws<ArgumentOutOfRangeException>(() => IconRenderer.Render(theme, "SA.Icon.1.16", 16, 0.01));
                Assert.Throws<ArgumentException>(() => IconRenderer.Render(theme, "SA.Icon.10.32", 32, 1.0));
            });
        }

        private const int LogPixelsX = 88;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr context);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr context, int index);

        [Fact]
        public void R371_the_theme_font_is_the_bundled_libre_franklin_with_a_real_semibold_and_regular()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = ThemeDictionary.Load();
                var family = (FontFamily)theme["SA.Font"];
                var widths = new List<double>();
                var ink = new List<int>();
                foreach (FontWeight weight in new[] { (FontWeight)theme["SA.Type.Body.Weight"], (FontWeight)theme["SA.Type.Title.Weight"] })
                {
                    var typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
                    Assert.True(typeface.TryGetGlyphTypeface(out GlyphTypeface glyph));
                    // The Win32 name of a named instance holds its weight, the typographic family name is the one of the family
                    Assert.Contains("Libre Franklin", glyph.FamilyNames.Values);
                    Assert.Equal(weight.ToOpenTypeWeight(), glyph.Weight.ToOpenTypeWeight());
                    Assert.Equal(StyleSimulations.None, glyph.StyleSimulations);
                    Assert.StartsWith("pack://application:,,,/SiteAdaptation.AddIn;component/Theme/Fonts/", glyph.FontUri.ToString(), StringComparison.OrdinalIgnoreCase);
                    var text = new FormattedText("Site Adaptation 0123456789", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 100, TextBrush(theme), 1.0);
                    widths.Add(text.Width);
                    ink.Add(Ink(text));
                }
                Assert.Equal(400, ((FontWeight)theme["SA.Type.Body.Weight"]).ToOpenTypeWeight());
                Assert.Equal(600, ((FontWeight)theme["SA.Type.Title.Weight"]).ToOpenTypeWeight());
                Assert.True(widths[1] > widths[0], string.Join(" ", widths));
                // An advance width can change with the metrics alone, the drawn outline shows the weight. 5a measured 28,901 and 42,033
                Assert.True(ink[1] > ink[0] * 1.2, string.Join(" ", ink));
            });
        }

        // Libre Franklin 2.000 and 3.000 hold no tnum feature and WPF takes no other font for one feature, so the digits stay
        // proportional. The theme asks for it in every type style all the same, question QL9 of steps/lane-b.md
        [Fact]
        public void R371_every_type_style_asks_for_tabular_figures()
        {
            OnSta(() =>
            {
                ResourceDictionary theme = ThemeDictionary.Load();
                List<string> styles = theme.Keys.OfType<string>().Where(key => key.StartsWith("SA.Type.", StringComparison.Ordinal) && theme[key] is Style).ToList();
                Assert.Equal(10, styles.Count);
                foreach (string key in styles)
                {
                    var block = new TextBlock { Text = "0123456789", Style = (Style)theme[key] };
                    Assert.Equal(FontNumeralAlignment.Tabular, Typography.GetNumeralAlignment(block));
                    Assert.Same(theme["SA.Font"], block.FontFamily);
                }
            });
        }

        private static int Ink(FormattedText text)
        {
            const int width = 1600;
            const int height = 160;
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawText(text, new Point(4, 4));
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            byte[] pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            int count = 0;
            for (int at = 3; at < pixels.Length; at += 4)
            {
                count += pixels[at] >= 128 ? 1 : 0;
            }
            return count;
        }

        private static SolidColorBrush TextBrush(ResourceDictionary theme)
        {
            return (SolidColorBrush)theme["SA.Text"];
        }

        // The bitmap is premultiplied, so a pixel at an edge is taken back to its own colour before the compare
        private static bool Near(byte[] pixels, int at, Color colour)
        {
            double alpha = pixels[at + 3] / 255.0;
            return Math.Abs(pixels[at] / alpha - colour.B) <= 24 && Math.Abs(pixels[at + 1] / alpha - colour.G) <= 24 && Math.Abs(pixels[at + 2] / alpha - colour.R) <= 24;
        }

        // WPF objects need a single threaded apartment, the xunit runner gives none
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
