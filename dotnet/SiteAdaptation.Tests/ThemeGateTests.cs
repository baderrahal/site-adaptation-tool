using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R371, docs/design/12-ui-spec.md. Theme.xaml holds every colour, font size and icon of the add-in. These tests read files
    // only, so they run on net48 and on the net8.0 leg of Linux: the gate on every other XAML and C# file of the add-in, and Theme.xaml
    // against the text of the spec, a second reading of the values the theme was written from
    public sealed class ThemeGateTests
    {
        private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void R371_no_colour_literal_and_no_font_size_in_any_file_of_the_addin_but_the_theme()
        {
            string addin = Path.Combine(RepoRoot(), "dotnet", "SiteAdaptation.AddIn");
            Assert.True(File.Exists(Path.Combine(addin, ThemeGate.ThemeFile)), "no theme file in " + addin);
            List<string> scanned = ThemeGate.Files(addin).ToList();
            Assert.Contains(scanned, file => file.EndsWith("App.cs", StringComparison.Ordinal));
            Assert.Empty(ThemeGate.Offenders(addin));
        }

        [Fact]
        public void R371_the_gate_catches_each_planted_literal_and_leaves_the_theme_and_a_clean_file_alone()
        {
            var planted = new Dictionary<string, string>
            {
                { "Hex3.xaml", "<Border Background=\"#FFF\" />" },
                { "Hex4.xaml", "<Border Background=\"#8FFF\" />" },
                { "Hex6.cs", "var text = \"#1B3349\";" },
                { "Hex8.cs", "var text = \"#FF1B3349\";" },
                { "FromRgb.cs", "var colour = Color.FromRgb(1, 2, 3);" },
                { "FromArgb.cs", "var colour = System.Windows.Media.Color.FromArgb(255, 1, 2, 3);" },
                { "Colors.cs", "var colour = Colors.Red;" },
                { "Brushes.cs", "var brush = Brushes.White;" },
                { "Converter.cs", "var colour = ColorConverter.ConvertFromString(name);" },
                { "Named.xaml", "<TextBlock Foreground=\"Red\" />" },
                { "NamedSetter.xaml", "<Setter Property=\"Background\"\n        Value=\"White\" />" },
                { "NamedElement.xaml", "<SolidColorBrush x:Key=\"a\">Gray</SolidColorBrush>" },
                { "SizeXaml.xaml", "<TextBlock FontSize=\"13\" />" },
                { "SizeSetter.xaml", "<Setter Property=\"FontSize\" Value=\"13\" />" },
                { "SizeCs.cs", "block.FontSize = 13;" },
                { "SizeCast.cs", "block.FontSize = (double)13;" },
                { "SizeProperty.cs", "block.SetValue(TextBlock.FontSizeProperty, 13.0);" },
            };
            string root = Path.Combine(Path.GetTempPath(), "theme-gate-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Theme"));
                Directory.CreateDirectory(Path.Combine(root, "obj"));
                foreach (KeyValuePair<string, string> file in planted)
                {
                    File.WriteAllText(Path.Combine(root, file.Key), file.Value);
                }
                File.WriteAllText(Path.Combine(root, ThemeGate.ThemeFile), "<SolidColorBrush x:Key=\"SA.Text\" Color=\"#1B1F24\" />");
                File.WriteAllText(Path.Combine(root, "obj", "Generated.g.cs"), "var colour = Colors.Red;");
                File.WriteAllText(Path.Combine(root, "Clean.xaml"),
                    "<TextBlock Foreground=\"{StaticResource SA.Text}\" Background=\"Transparent\" FontSize=\"{StaticResource SA.Type.Body.Size}\" Text=\"&#35;1 Set up site\" />");
                File.WriteAllText(Path.Combine(root, "Clean.cs"), "#region Ribbon\nvar size = (double)theme[\"SA.Type.Body.Size\"];\nblock.FontSize = size;\n#endregion");

                List<string> caught = ThemeGate.Offenders(root).Select(line => line.Split(':')[0]).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();

                Assert.Equal(planted.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList(), caught);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void R371_every_colour_of_the_spec_is_in_the_theme_with_its_value_and_the_theme_holds_no_other_colour()
        {
            string colours = Part(SpecText(), "### Colours");
            var expected = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(colours, @"(SA\.[A-Za-z]+) (#[0-9A-Fa-f]{6})(?: on (#[0-9A-Fa-f]{6}))?"))
            {
                expected.Add(match.Groups[1].Value, match.Groups[2].Value.ToUpperInvariant());
                if (match.Groups[3].Success)
                {
                    expected.Add(match.Groups[1].Value + "Ground", match.Groups[3].Value.ToUpperInvariant());
                }
            }
            Dictionary<string, string> brushes = Theme().Descendants(Wpf + "SolidColorBrush")
                .ToDictionary(element => Key(element), element => ((string)element.Attribute("Color")).ToUpperInvariant(), StringComparer.Ordinal);

            Assert.Equal(21, expected.Count - expected.Keys.Count(key => key.EndsWith("Ground", StringComparison.Ordinal)));
            Assert.Equal(26, expected.Count);
            Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal), brushes.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        }

        [Fact]
        public void R371_every_type_key_of_the_spec_with_the_four_of_QL7_has_its_size_and_weight_and_none_is_below_12()
        {
            string font = Part(SpecText(), "### Font");
            var expected = new Dictionary<string, Tuple<double, string>>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(font, @"(SA\.Type\.\w+) (\d+) (SemiBold|Regular)"))
            {
                Assert.False(expected.ContainsKey(match.Groups[1].Value), match.Value);
                expected.Add(match.Groups[1].Value, Tuple.Create(double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), match.Groups[3].Value));
            }
            XElement theme = Theme();
            Dictionary<string, double> doubles = Doubles(theme);
            Dictionary<string, string> weights = theme.Elements(Wpf + "FontWeight").ToDictionary(element => Key(element), element => element.Value.Trim(), StringComparer.Ordinal);

            Assert.Equal(9, expected.Count);
            foreach (KeyValuePair<string, Tuple<double, string>> type in expected)
            {
                Assert.Equal(type.Value.Item1, doubles[type.Key + ".Size"]);
                Assert.Equal(type.Value.Item2, weights[type.Key + ".Weight"]);
                Assert.True(type.Value.Item1 >= 12, type.Key);
            }
            Assert.Equal(expected.Keys.Select(key => key + ".Size").OrderBy(key => key, StringComparer.Ordinal),
                doubles.Keys.Where(key => key.StartsWith("SA.Type.", StringComparison.Ordinal)).OrderBy(key => key, StringComparer.Ordinal));
            Assert.Contains("Tabular", Part(SpecText(), "### Font").Split(' ').Select(word => word.Trim('.')));
            Assert.Equal("Tabular", theme.Elements(Wpf + "FontNumeralAlignment").Single(element => Key(element) == "SA.Figures").Value.Trim());
        }

        [Fact]
        public void R371_spacing_control_sizes_and_radii_are_the_values_of_the_spec()
        {
            string spec = SpecText();
            string sizes = Part(spec, "### Sizes");
            XElement theme = Theme();
            Dictionary<string, double> doubles = Doubles(theme);

            List<double> spacing = Regex.Match(sizes, @"Spacing ([\d, ]+) only").Groups[1].Value.Split(',').Select(text => double.Parse(text.Trim(), CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(spacing, doubles.Where(pair => pair.Key.StartsWith("SA.Space.", StringComparison.Ordinal)).Select(pair => pair.Value).OrderBy(value => value));

            Assert.Equal(Number(sizes, @"Main button (\d+) high"), doubles["SA.Size.MainButton"]);
            Assert.Equal(Number(sizes, @"other buttons (\d+) to \d+"), doubles["SA.Size.ButtonMin"]);
            Assert.Equal(Number(sizes, @"other buttons \d+ to (\d+)"), doubles["SA.Size.ButtonMax"]);
            Assert.Equal(Number(sizes, @"rows (\d+)"), doubles["SA.Size.Row"]);
            Assert.Equal(Number(sizes, @"inputs (\d+)"), doubles["SA.Size.Input"]);
            Assert.Equal(Number(sizes, @"Pane starts (\d+) wide"), doubles["SA.Size.Pane"]);
            Assert.Equal(Number(spec, @"Side box, (\d+) wide"), doubles["SA.Size.SideBox"]);
            Assert.Equal(Number(spec, @"round number mark (\d+) px"), doubles["SA.Size.StepMark"]);
            Assert.Equal(Number(spec, @"a (\d+) px SA\.Accent mark"), doubles["SA.Size.SelectedMark"]);

            Dictionary<string, string> radii = theme.Elements(Wpf + "CornerRadius").ToDictionary(element => Key(element), element => element.Value.Trim(), StringComparer.Ordinal);
            Assert.Equal(Number(sizes, @"Radius (\d+) on controls").ToString(CultureInfo.InvariantCulture), radii["SA.Radius.Control"]);
            Assert.Equal(Number(sizes, @"(\d+) on chips").ToString(CultureInfo.InvariantCulture), radii["SA.Radius.Chip"]);
        }

        [Fact]
        public void R371_each_icon_of_the_spec_is_in_the_theme_part_by_part_with_its_pen_or_fill_at_32_and_at_16_px()
        {
            string icons = Regex.Match(Part(SpecText(), "### Icons"), "```text\n(.*?)```", RegexOptions.Singleline).Groups[1].Value;
            XElement theme = Theme();
            Dictionary<string, double> doubles = Doubles(theme);
            Dictionary<string, XElement> pens = theme.Elements(Wpf + "Pen").ToDictionary(element => Key(element), StringComparer.Ordinal);
            var compared = 0;

            foreach (string line in icons.Split('\n').Where(text => text.Trim().Length > 0))
            {
                Match head = Regex.Match(line, @"^(\d) [^:]+: (.*)$");
                Assert.True(head.Success, line);
                string number = head.Groups[1].Value;
                foreach (int size in new[] { 32, 16 })
                {
                    List<string> expected = head.Groups[2].Value.Split('|').Select(part => part.Trim())
                        .Select(part => ExpectedDrawing(part, size)).Where(part => part != null).ToList();
                    List<string> found = theme.Elements(Wpf + "DrawingGroup").Single(element => Key(element) == "SA.Icon." + number + "." + size)
                        .Elements(Wpf + "GeometryDrawing").Select(element => ThemeDrawing(element, pens, doubles)).ToList();
                    Assert.Equal(expected, found);
                    compared += found.Count;
                }
            }
            Assert.Equal(8, theme.Elements(Wpf + "DrawingGroup").Count() / 2);
            Assert.Equal(31 + 27, compared);
            Assert.All(pens.Values, pen =>
            {
                Assert.Equal("Round", (string)pen.Attribute("StartLineCap"));
                Assert.Equal("Round", (string)pen.Attribute("EndLineCap"));
                Assert.Equal("Round", (string)pen.Attribute("LineJoin"));
            });
        }

        // One part of an icon line of the spec as "geometry, stroke colour, width, fill colour" at a size, or null when it is not drawn there
        private static string ExpectedDrawing(string part, int size)
        {
            bool only32 = part.Contains("(32 only)") || part.StartsWith("32 px joints ", StringComparison.Ordinal);
            bool only16 = part.StartsWith("16 px joints ", StringComparison.Ordinal);
            if ((only32 && size != 32) || (only16 && size != 16))
            {
                return null;
            }
            string text = Regex.Replace(part, @"^(?:32|16) px joints ", "");
            int end = new[] { " fill", " stroke", " width", " (", " no stroke" }.Select(word => text.IndexOf(word, StringComparison.Ordinal)).Where(at => at >= 0).DefaultIfEmpty(text.Length).Min();
            string geometry = text.Substring(0, end).Trim();
            string fill = Colour(Regex.Match(text, @"\bfill (SA\.\w+|white)").Groups[1].Value);
            bool noStroke = text.Contains("no stroke");
            string stroke = noStroke ? "" : Colour(Regex.Match(text, @"\bstroke (SA\.\w+|white)").Groups[1].Value);
            if (!noStroke && stroke.Length == 0)
            {
                stroke = "SA.Heading";
            }
            string width = "";
            if (!noStroke)
            {
                Match wide = Regex.Match(text, @"width ([\d.]+) \(([\d.]+) at 16 px\)");
                width = wide.Success ? wide.Groups[size == 32 ? 1 : 2].Value : (size == 32 ? "2" : "2.4");
            }
            return geometry + ", " + stroke + ", " + width + ", " + fill;
        }

        private static string ThemeDrawing(XElement drawing, Dictionary<string, XElement> pens, Dictionary<string, double> doubles)
        {
            string fill = Reference((string)drawing.Attribute("Brush"));
            string stroke = "";
            string width = "";
            string penKey = Reference((string)drawing.Attribute("Pen"));
            if (penKey.Length > 0)
            {
                XElement pen = pens[penKey];
                stroke = Reference((string)pen.Attribute("Brush"));
                width = doubles[Reference((string)pen.Attribute("Thickness"))].ToString(CultureInfo.InvariantCulture);
            }
            return (string)drawing.Attribute("Geometry") + ", " + stroke + ", " + width + ", " + fill;
        }

        private static string Colour(string word)
        {
            return word == "white" ? "SA.Surface" : word;
        }

        private static string Reference(string value)
        {
            return value == null ? "" : Regex.Match(value, @"^\{StaticResource ([\w.]+)\}$").Groups[1].Value;
        }

        private static double Number(string text, string pattern)
        {
            Match match = Regex.Match(text, pattern);
            Assert.True(match.Success, pattern);
            return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, double> Doubles(XElement theme)
        {
            XNamespace system = "clr-namespace:System;assembly=mscorlib";
            return theme.Elements(system + "Double").ToDictionary(element => Key(element), element => double.Parse(element.Value.Trim(), CultureInfo.InvariantCulture), StringComparer.Ordinal);
        }

        private static string Key(XElement element)
        {
            return (string)element.Attribute(Xaml + "Key");
        }

        private static XElement Theme()
        {
            return XDocument.Load(Path.Combine(RepoRoot(), "dotnet", "SiteAdaptation.AddIn", ThemeGate.ThemeFile)).Root;
        }

        private static string SpecText()
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), "docs", "design", "12-ui-spec.md")).Replace("\r\n", "\n");
        }

        private static string Part(string text, string heading)
        {
            int start = text.IndexOf(heading + "\n", StringComparison.Ordinal);
            Assert.True(start >= 0, heading);
            int end = text.IndexOf("\n### ", start + heading.Length, StringComparison.Ordinal);
            return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
        }

        internal static string RepoRoot()
        {
            string at = AppContext.BaseDirectory;
            while (at != null && !File.Exists(Path.Combine(at, "config", "project.json")))
            {
                at = Path.GetDirectoryName(at);
            }
            return at ?? throw new InvalidOperationException("no repo above " + AppContext.BaseDirectory);
        }
    }

    // R371. The gate of docs/design/12-ui-spec.md: a colour or a font size is written in Theme.xaml only
    internal static class ThemeGate
    {
        public static readonly string ThemeFile = Path.Combine("Theme", "Theme.xaml");

        private const string ColourProperty = @"(?:Foreground|Background|BorderBrush|Fill|Stroke|Color|CaretBrush|SelectionBrush|OpacityMask|Brush)";

        // Transparent is no colour, it lets the ground through, and a value in braces is a resource or a binding
        private const string NamedValue = @"""(?!\{)(?!Transparent"")[A-Za-z]+""";

        private static readonly Regex[] Colour =
        {
            new Regex(@"(?<![\w&])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{3})\b"),
            new Regex(@"\bColor\.From\w*\s*\("),
            new Regex(@"\b(?:Colors|Brushes)\.[A-Z]\w*"),
            new Regex(@"\b(?:ColorConverter|BrushConverter)\b"),
            new Regex(@"\b" + ColourProperty + @"\s*=\s*" + NamedValue),
            new Regex(@"Property\s*=\s*""[\w.]*" + ColourProperty + @"""\s+Value\s*=\s*" + NamedValue),
            new Regex(@"<(?:Color|SolidColorBrush)\b[^>/]*>\s*[A-Za-z]"),
        };

        private static readonly Regex[] FontSize =
        {
            new Regex(@"FontSize\s*=\s*""\s*[\d.]"),
            new Regex(@"Property\s*=\s*""[\w.]*FontSize""\s+Value\s*=\s*""\s*[\d.]"),
            new Regex(@"FontSize\s*=\s*(?:\(\s*double\s*\)\s*)?[\d.]"),
            new Regex(@"FontSizeProperty\s*,\s*(?:\(\s*double\s*\)\s*)?[\d.]"),
        };

        public static IEnumerable<string> Files(string root)
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Where(path => !Relative(root, path).Split(Path.DirectorySeparatorChar).Any(part => part == "bin" || part == "obj"))
                .Where(path => !string.Equals(Relative(root, path), ThemeFile, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        // Each offence as "relative path:line: what", the colour and the size patterns over the whole text, so a setter split over lines is caught
        public static List<string> Offenders(string root)
        {
            var found = new List<string>();
            foreach (string path in Files(root))
            {
                string text = File.ReadAllText(path);
                foreach (Regex pattern in Colour.Concat(FontSize))
                {
                    foreach (Match match in pattern.Matches(text))
                    {
                        int line = text.Take(match.Index).Count(letter => letter == '\n') + 1;
                        found.Add(Relative(root, path) + ":" + line + ": " + match.Value.Replace("\n", " "));
                    }
                }
            }
            return found;
        }

        private static string Relative(string root, string path)
        {
            return path.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
        }
    }
}
