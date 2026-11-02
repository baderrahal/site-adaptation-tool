using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R372, control 2 of docs/design/12-ui-spec.md. Every text of the XAML and the view models of the add-in is in the label list
    // docs/design/14-ui-labels.md under its window, and every entry of a built window is shown by a file of that window. Files
    // only, so it runs on net48 and on the net8.0 leg of Linux
    public sealed class UiLabelTests
    {
        [Fact]
        public void R372_every_text_of_the_addin_is_in_the_label_list_and_every_entry_of_a_built_window_is_shown()
        {
            string root = ThemeGateTests.RepoRoot();
            Dictionary<string, UiLabels.Window> list = UiLabels.List(File.ReadAllText(Path.Combine(root, UiLabels.ListFile)));
            // Review 5f: the texts of Update from report that Common gives the window, the rule column, the refusal and the lacks, are held too,
            // as the plain rule names and the words of a past value of the check table that Common gives so the net8.0 tests reach them
            Dictionary<string, List<UiLabels.Found>> shown = UiLabels.Texts(Path.Combine(root, "dotnet", "SiteAdaptation.AddIn"), Path.Combine(root, "dotnet", "SiteAdaptation.Common", "Flow", "RunTexts.cs"),
                new Dictionary<string, string>
                {
                    [Path.Combine(root, "dotnet", "SiteAdaptation.Common", "CheckTable", "CheckShownText.cs")] = "Check table",
                    [Path.Combine(root, "dotnet", "SiteAdaptation.Common", "CheckTable", "DoorLevelText.cs")] = "Check table",
                    [Path.Combine(root, "dotnet", "SiteAdaptation.Common", "Update", "UpdateText.cs")] = "Update from report",
                    [Path.Combine(root, "dotnet", "SiteAdaptation.Common", "Scan", "ScanViewText.cs")] = "What the tool read",
                    [Path.Combine(root, "dotnet", "SiteAdaptation.Common", "Review", "ClusterConfirmText.cs")] = "Review data",
                });

            Assert.Equal(new[] { "Ribbon", "Pane", "Button messages", "Set up site", "Review data", "Site Adaptation Rules", "Check table", "Update from report", "New project", "How it works", "What the tool read", "Loader" }, list.Keys.ToArray());
            Assert.Equal(new[] { "Ribbon", "Pane", "Button messages", "Set up site", "Review data", "Site Adaptation Rules", "Check table", "Update from report", "New project", "How it works", "What the tool read", "Loader" }, list.Values.Where(window => window.Built).Select(window => window.Name).ToArray());
            Assert.Equal(16, list["Ribbon"].Entries.Count);
            Assert.Contains(shown["Ribbon"], found => found.Text == "Update from report");
            Assert.Contains(shown["Pane"], found => found.Text == "Browse" && found.Where.EndsWith(".xaml", StringComparison.Ordinal));
            Assert.Contains(shown["Pane"], found => found.Text == "Needs step <n>" && found.Where.EndsWith("PaneText.cs", StringComparison.Ordinal));
            Assert.Contains(shown[UiLabels.Messages], found => found.Text == " needs step " && found.Where.EndsWith("RunTexts.cs", StringComparison.Ordinal));
            Assert.Contains(shown[UiLabels.Messages], found => found.Text == "Cancel" && found.Where.EndsWith("RevitModel.cs", StringComparison.Ordinal));
            // 31 since Lane B 5c, the 11 messages of 2 Read data (R375)
            Assert.Equal(31, list[UiLabels.Messages].Entries.Count);
            Assert.Contains(shown["Update from report"], found => found.Text == "follows the rules" && found.Where.EndsWith("UpdateText.cs", StringComparison.Ordinal));
            Assert.Contains(shown["Update from report"], found => found.Text == "<name> <n>" && found.Where.EndsWith("UpdateFromReportText.cs", StringComparison.Ordinal));

            Assert.Empty(UiLabels.Offenders(list, shown));
        }

        // Lane C NEWPROJ-UI: no file name ending in .json in any text of the add-in a user reads, in the label list or in the code. A message
        // that puts a file name of a constant into its text is not seen here (steps/HEADSUP.md, Open risks)
        [Fact]
        public void NEWPROJ_no_text_of_any_window_names_a_json_file()
        {
            string root = ThemeGateTests.RepoRoot();
            Dictionary<string, UiLabels.Window> list = UiLabels.List(File.ReadAllText(Path.Combine(root, UiLabels.ListFile)));
            Dictionary<string, List<UiLabels.Found>> shown = UiLabels.Texts(Path.Combine(root, "dotnet", "SiteAdaptation.AddIn"), Path.Combine(root, "dotnet", "SiteAdaptation.Common", "Flow", "RunTexts.cs"));
            var json = new Regex(@"\.json\b", RegexOptions.IgnoreCase);

            Assert.Empty(list.Values.SelectMany(window => window.Entries).Where(entry => json.IsMatch(entry.Text)).Select(entry => entry.Text));
            Assert.Empty(shown.Values.SelectMany(found => found).Where(found => json.IsMatch(found.Text)).Select(found => found.Where + ": " + found.Text));
        }

        [Fact]
        public void R372_the_text_test_catches_each_planted_text_and_each_entry_no_window_shows()
        {
            string root = Path.Combine(Path.GetTempPath(), "ui-labels-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "App"));
                Directory.CreateDirectory(Path.Combine(root, "Pane"));
                Directory.CreateDirectory(Path.Combine(root, "Theme"));
                File.WriteAllText(Path.Combine(root, "App", "RibbonLayout.cs"),
                    "public const string Tab = \"Site Adaptation\";\nnew RibbonPanelSpec(\"Setup\", new[] { new RibbonButtonSpec(1, \"1 Set up site\", \"SetUpSiteCommand\") });\nnew RibbonPanelSpec(\"Planted panel\", null);\nnew RibbonButtonSpec(\"Planted button with no number\", \"PlantedCommand\");");
                File.WriteAllText(Path.Combine(root, "Theme", "Theme.xaml"),
                    "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><FontFamily x:Key=\"SA.Font\">Libre Franklin</FontFamily><Style x:Key=\"a\" TargetType=\"TextBlock\"><Setter Property=\"Text\" Value=\"Planted in the theme\" /></Style></ResourceDictionary>");
                File.WriteAllText(Path.Combine(root, "Pane", "Page.xaml"),
                    "<Page xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:sys=\"clr-namespace:System;assembly=mscorlib\" x:Class=\"A.Page\">\n"
                    + "<!-- a comment \"Not text\" -->\n"
                    + "<Page.Resources><sys:String x:Key=\"s\">Planted string</sys:String></Page.Resources>\n"
                    + "<StackPanel>\n"
                    + "<TextBlock Text=\"Browse\" />\n"
                    + "<TextBlock Text=\"Planted attribute\" />\n"
                    + "<TextBlock>Planted element</TextBlock>\n"
                    + "<TextBlock><Run Text=\"Planted run\" /></TextBlock>\n"
                    + "<Button Content=\"Planted content\" ToolTip=\"Planted tooltip\" />\n"
                    + "<TextBlock Text=\"{Binding Name, FallbackValue=Planted fallback}\" />\n"
                    + "<TextBlock Text=\"{Binding Name, TargetNullValue='Planted null'}\" />\n"
                    + "<TextBlock><TextBlock.Style><Style TargetType=\"TextBlock\"><Setter Value=\"Planted setter\" Property=\"Text\" /></Style></TextBlock.Style></TextBlock>\n"
                    + "<TextBlock Text=\"{Binding Name}\" Tag=\"Not text\" />\n"
                    + "</StackPanel></Page>");
                File.WriteAllText(Path.Combine(root, "Pane", "Page.xaml.cs"), "var a = \"Planted code behind\";");
                File.WriteAllText(Path.Combine(root, "Pane", "PaneText.cs"),
                    "// a comment \"Not text\"\n/* \"Not text\" */\npublic const string A = \"Needs step <n>\";\npublic const string B = @\"Planted \"\"verbatim\"\"\";\nchar c = '\"';\nstring d = \", \";");
                File.WriteAllText(Path.Combine(root, "Pane", "PlantedViewModel.cs"), "var a = \"Planted view model\";\nvar b = $\"Planted {a}\";");
                File.WriteAllText(Path.Combine(root, "Pane", "Helper.cs"), "var a = \"Not a window text\";");
                File.WriteAllText(Path.Combine(root, "Other.xaml"),
                    "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><TextBlock Text=\"Planted out of a window\" /></Grid>");
                Directory.CreateDirectory(Path.Combine(root, "Revit"));
                File.WriteAllText(Path.Combine(root, "Revit", "Model.cs"), "dialog.CommonButtons = TaskDialogCommonButtons.Cancel;\nvar a = \"Not a message text\";");
                string runTexts = Path.Combine(root, "RunTexts.cs");
                File.WriteAllText(runTexts, "return label + \" needs step \" + step;\nreturn label + \" needs \" + input.Name + \", the \" + Kind(input) + \" is not chosen\";\n"
                    + "return input.IsFolder ? \"folder\" : \"file\";\nreturn \"Planted message\";");

                const string listText = "### Ribbon\n\n- `Site Adaptation`\n- `Setup`\n- `1 Set up site`\n- `Planted entry no ribbon shows`\n\n### Pane\n\n- `Browse`\n- `Needs step <n>`\n- `Planted entry no pane shows` CHOSEN, a note\n\n"
                    + "### Button messages\n\n- `<n> needs step <n>`\n- `<n> needs <n>, the file is not chosen`\n- `<n> needs <n>, the folder is not chosen`\n- `Cancel`\n- `<n> needs <n>, the file is not on disk`\n\n"
                    + "### Check table\n\nNot built yet, Phase 5c.\n\n- `Entry of a window not built`\n";
                Dictionary<string, UiLabels.Window> list = UiLabels.List(listText);
                List<string> offences = UiLabels.Offenders(list, UiLabels.Texts(root, runTexts));
                string all = string.Join("\n", offences);

                foreach (string planted in new[] { "Planted panel", "Planted button with no number", "Planted in the theme", "Planted string", "Planted attribute", "Planted element", "Planted run", "Planted content",
                    "Planted tooltip", "Planted fallback", "Planted null", "Planted setter", "Planted code behind", "Planted \"verbatim\"", "Planted view model", "Planted {a}",
                    "Planted out of a window", "Planted entry no ribbon shows", "Planted entry no pane shows", "Planted message", "the file is not on disk" })
                {
                    Assert.True(offences.Count(line => line.Contains(planted)) == 1, planted + " in\n" + all);
                }
                Assert.Equal(21, offences.Count);
                Assert.DoesNotContain("Not a message text", all);
                Assert.DoesNotContain("Not text", all);
                Assert.DoesNotContain("Not a window text", all);
                Assert.DoesNotContain("Entry of a window not built", all);
                Assert.DoesNotContain("Libre Franklin", all);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // A second reading: each entry not marked CHOSEN is found word for word in the spec, the mock or the design, a value standing for
        // any short text there
        [Fact]
        public void R372_each_entry_of_the_list_not_marked_chosen_is_written_in_the_ui_spec_the_mock_or_the_design()
        {
            string root = ThemeGateTests.RepoRoot();
            // The messages of a button are written in 3 of the design, What every button shares, the work folder in 6 and Bader's
            // changes of 28 Oct 2026 in 13
            // Lane C NEWPROJ-UI: the boards of docs/design/boards, Bader's of 31 Oct 2026, their text with the markup taken out
            string source = string.Join("\n", new[] { "12-ui-spec.md", "13-workflow-changes.md", "07-mock.md", "03a-buttons-shared-and-1.md", "06-settings.md" }
                .Select(name => File.ReadAllText(Path.Combine(root, "docs", "design", name)))
                .Concat(Directory.GetFiles(Path.Combine(root, "docs", "design", "boards"), "*.dc.html").OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => System.Net.WebUtility.HtmlDecode(Regex.Replace(File.ReadAllText(path), "<[^>]+>", "")))));
            Dictionary<string, UiLabels.Window> list = UiLabels.List(File.ReadAllText(Path.Combine(root, UiLabels.ListFile)));
            var missing = new List<string>();
            int chosen = 0;
            int matched = 0;
            foreach (UiLabels.Window window in list.Values)
            {
                foreach (UiLabels.Entry entry in window.Entries)
                {
                    if (entry.Chosen)
                    {
                        chosen++;
                        continue;
                    }
                    string pattern = string.Join(".{1,40}?", UiLabels.Slot.Split(entry.Text).Select(Regex.Escape));
                    if (Regex.IsMatch(source, pattern))
                    {
                        matched++;
                    }
                    else
                    {
                        missing.Add(window.Name + ": " + entry.Text);
                    }
                }
            }
            Assert.Empty(missing);
            // 22 since Lane B 5b, the line of a second run of Set up site. Lane B 5c added 70: 29 of the plain rule names of Check table, 7 more
            // of Check table, 11 of Review data, the 11 messages of 2 Read data (R375) and 12 of Site Adaptation Rules. 5f added 48: Browse,
            // Note and the dry run line of Update from report, the two lines of the message after Apply, the texts of the 5f fix that Common
            // gives the window and the two refusals of a second book of a kind. The UI review of 5c added 4: the heading Corner, the two lines of a
            // Rules command with no usable rules file and the chip Team check. The second logic review of 5c added 1, the line under Why of a
            // row on Skip. Lane C NEWPROJ-UI added 21: 3 of the pane, Settings among them, and 18 of New project. Lane C SCAN-UI added
            // 15 of What the tool read, 8 of the section Current files of Set up site and 2 of New project. Lane C SCAN-UI task 5 added
            // 8 of the cluster confirm of Review data (R442), task 6 4 of Door level from of Check table (R443). Lane C DEMO-FIX added 7,
            // Confirm all and Confirm all shown of Review data and 5 of the Loader (R444). FIX-1NOV-A2 took out 1, the 5f line of a plan not
            // in the work folder, as Update from report finds the plan of 4 Check by its run log (R472). Lane C FIX-1NOV-C added 2 of New
            // project, the no model line and the TaskDialog of a Save setup that rolls back (R445)
            Assert.Equal(211, chosen);
            Assert.True(matched > 100, matched.ToString());
        }

        // Review 1 of PR 92: the windows not built yet take Bader's changes of 28 Oct 2026 too, the quoted texts of Set up site
        // in docs/design/13-workflow-changes.md, the pass limit as a value while QL17 is open, and Check under its number 4
        [Fact]
        public void R372_the_entries_of_windows_not_built_yet_hold_the_texts_of_the_workflow_changes()
        {
            string root = ThemeGateTests.RepoRoot();
            Dictionary<string, UiLabels.Window> list = UiLabels.List(File.ReadAllText(Path.Combine(root, UiLabels.ListFile)));
            string changes = File.ReadAllText(Path.Combine(root, "docs", "design", "13-workflow-changes.md")).Replace("\r\n", "\n");
            int start = changes.IndexOf("### Set up site", StringComparison.Ordinal);
            string setUp = changes.Substring(start, changes.IndexOf("\n### ", start, StringComparison.Ordinal) - start);
            List<string> quoted = Regex.Matches(setUp, @"""([^""]+)""").Cast<Match>().Select(match => match.Groups[1].Value.Replace("...", "<n>")).ToList();
            List<string> setUpEntries = list["Set up site"].Entries.Select(entry => entry.Text).ToList();
            List<string> checkTitles = list["Check table"].Entries.Select(entry => entry.Text).Where(text => Regex.IsMatch(text, @"^\d Check")).ToList();

            Assert.Equal(new[] { "Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below", "Ramp slope", "Default 0.5%. Choices 0.5%, 1%, 2%, then 3% to 15%", "From the parameter sheet: <n>" }, quoted);
            Assert.All(quoted, text => Assert.Contains(text, setUpEntries));
            Assert.Contains("Pass limit <n> mm. <n> of <n> pass", setUpEntries);
            Assert.DoesNotContain(setUpEntries, text => text.Contains(" 1 mm") || text.Contains("0.1 mm"));
            Assert.Equal(new[] { "4 Check, NBH <n>, <n> plots", "4 Check, NBH all, <n> plots" }, checkTitles);
            Assert.Contains("4 Check", list["Ribbon"].Entries.Select(entry => entry.Text));
            // Lane B 5b built the window of button 1, so its entries are held against its files as every built window is
            Assert.True(list["Set up site"].Built);
            // Lane B 5c built the check table
            Assert.True(list["Check table"].Built);
        }

        // Review 1 of PR 92: docs/design/README.md finds a part by its number, so no two parts share one, and the label list is 14
        [Fact]
        public void R372_each_part_of_docs_design_has_a_number_of_its_own_and_the_label_list_is_part_14()
        {
            string design = Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design");
            List<string> numbers = Directory.GetFiles(design, "*.md").Select(Path.GetFileName)
                .Select(name => Regex.Match(name, @"^(\d+[a-z]?)-").Groups[1].Value).Where(number => number.Length > 0).ToList();

            Assert.True(numbers.Count >= 14, string.Join(" ", numbers));
            Assert.Equal(numbers.Distinct().Count(), numbers.Count);
            Assert.Equal(Path.Combine("docs", "design", "14-ui-labels.md"), UiLabels.ListFile);
            Assert.Equal("## 14. UI labels", File.ReadAllLines(Path.Combine(design, "14-ui-labels.md"))[0]);
        }
    }

    internal static class UiLabels
    {
        public static readonly string ListFile = Path.Combine("docs", "design", "14-ui-labels.md");
        public static readonly Regex Slot = new Regex(@"<[a-z]+>");
        public const string Messages = "Button messages";

        // The folder of the add-in each window's files are in, and the ribbon in its layout file
        private static readonly Dictionary<string, string> Folders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Pane", "Pane" },
            { "Set up site", "SetUpSite" },
            { "Review data", "ReviewData" },
            { "Site Adaptation Rules", "RulesWindow" },
            { "Check table", "CheckTable" },
            { "Update from report", "Update" },
            { "New project", "NewProject" },
            { "How it works", "HowItWorks" },
            { "What the tool read", "ScanResults" },
            { "Loader", "Loader" },
        };

        private const string RibbonFile = "RibbonLayout.cs";
        private const string NoWindow = "no window";
        private static readonly string[] TextAttributes = { "Text", "Content", "Header", "ToolTip", "Title" };
        private static readonly string[] TextElements = { "TextBlock", "Run", "Button", "Label", "CheckBox", "RadioButton", "TextBox", "ToolTip", "ComboBoxItem", "ListBoxItem",
            "Hyperlink", "Span", "Bold", "Italic", "Underline", "Paragraph", "GroupBox", "Expander", "TabItem", "MenuItem", "ToggleButton", "String" };

        public sealed class Window
        {
            public string Name { get; set; }
            public bool Built { get; set; }
            public List<Entry> Entries { get; } = new List<Entry>();
        }

        public sealed class Entry
        {
            public string Text { get; set; }
            public bool Chosen { get; set; }
        }

        public sealed class Found
        {
            public string Text { get; set; }
            public string Where { get; set; }
        }

        public static Dictionary<string, Window> List(string text)
        {
            var windows = new Dictionary<string, Window>(StringComparer.Ordinal);
            Window at = null;
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    at = new Window { Name = line.Substring(4).Trim(), Built = true };
                    windows.Add(at.Name, at);
                    continue;
                }
                if (at == null)
                {
                    continue;
                }
                if (line.StartsWith("Not built yet", StringComparison.Ordinal))
                {
                    at.Built = false;
                }
                Match entry = Regex.Match(line, @"^- `([^`]+)`(.*)$");
                if (entry.Success)
                {
                    at.Entries.Add(new Entry { Text = entry.Groups[1].Value, Chosen = entry.Groups[2].Value.Contains("CHOSEN") });
                }
            }
            return windows;
        }

        // The texts of each window, and under "no window" those of a XAML file that belongs to none, the theme included
        // runTexts is RunTexts.cs of the flow, whose texts and parts of texts are the messages of a button, with the common buttons
        // of a TaskDialog the add-in names, as Cancel
        // common names a file outside the add-in by its window, a file of Common whose texts that window shows
        public static Dictionary<string, List<Found>> Texts(string addin, string runTexts = null, IDictionary<string, string> common = null)
        {
            var texts = new Dictionary<string, List<Found>>(StringComparer.Ordinal) { { "Ribbon", new List<Found>() }, { NoWindow, new List<Found>() }, { Messages, new List<Found>() } };
            if (runTexts != null)
            {
                texts[Messages].AddRange(CodeTexts(File.ReadAllText(runTexts)).Select(text => new Found { Text = text, Where = Path.GetFileName(runTexts) }));
            }
            foreach (string path in Directory.EnumerateFiles(addin, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(path), @"\bTaskDialogCommonButtons\.(\w+)"))
                {
                    texts[Messages].Add(new Found { Text = match.Groups[1].Value, Where = Path.GetFileName(path) });
                }
            }
            foreach (string name in Folders.Keys)
            {
                texts[name] = new List<Found>();
            }
            foreach (string path in Directory.EnumerateFiles(addin, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                string relative = path.Substring(addin.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                string[] parts = relative.Split(Path.DirectorySeparatorChar);
                if (parts.Any(part => part == "bin" || part == "obj"))
                {
                    continue;
                }
                string file = parts.Last();
                string window = Folders.Where(pair => parts.Length > 1 && parts[0] == pair.Value).Select(pair => pair.Key).FirstOrDefault();
                if (file == RibbonFile)
                {
                    texts["Ribbon"].AddRange(RibbonTexts(File.ReadAllText(path)).Select(text => new Found { Text = text, Where = relative }));
                }
                else if (file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                {
                    texts[window ?? NoWindow].AddRange(XamlTexts(File.ReadAllText(path)).Select(text => new Found { Text = text, Where = relative }));
                }
                else if (window != null && (file.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith("ViewModel.cs", StringComparison.Ordinal) || file.EndsWith("Text.cs", StringComparison.Ordinal)))
                {
                    texts[window].AddRange(CodeTexts(File.ReadAllText(path)).Select(text => new Found { Text = text, Where = relative }));
                }
            }
            foreach (KeyValuePair<string, string> file in common ?? new Dictionary<string, string>())
            {
                texts[file.Value].AddRange(CodeTexts(File.ReadAllText(file.Key)).Select(text => new Found { Text = text, Where = Path.GetFileName(file.Key) }));
            }
            return texts;
        }

        public static List<string> Offenders(Dictionary<string, Window> list, Dictionary<string, List<Found>> texts)
        {
            var offences = new List<string>();
            foreach (KeyValuePair<string, List<Found>> window in texts)
            {
                List<string> entries = list.TryGetValue(window.Key, out Window listed) ? listed.Entries.Select(entry => entry.Text).ToList() : new List<string>();
                Func<string, bool> allowed = window.Key == Messages
                    ? (Func<string, bool>)(text => entries.Any(entry => entry.Contains(text)))
                    : text => entries.Contains(text);
                foreach (Found found in window.Value.Where(found => !allowed(found.Text)))
                {
                    offences.Add(found.Where + ": text not in the list of " + window.Key + ": " + found.Text);
                }
            }
            foreach (Window window in list.Values.Where(window => window.Built))
            {
                HashSet<string> shown = texts.TryGetValue(window.Name, out List<Found> found) ? new HashSet<string>(found.Select(item => item.Text), StringComparer.Ordinal) : new HashSet<string>();
                Func<string, bool> isShown = window.Name == Messages
                    ? (Func<string, bool>)(text => Slot.Split(text).All(part => !part.Any(char.IsLetter) || Covered(part, shown)))
                    : text => shown.Contains(text);
                foreach (Entry entry in window.Entries.Where(entry => !isShown(entry.Text)))
                {
                    offences.Add(ListFile + ": no file of " + window.Name + " shows " + entry.Text);
                }
            }
            return offences;
        }

        // A message is built from parts, so a part of an entry is shown when the parts of the code make it up end to end
        private static bool Covered(string part, HashSet<string> pieces)
        {
            var reach = new bool[part.Length + 1];
            reach[0] = true;
            for (int at = 0; at < part.Length; at++)
            {
                if (!reach[at])
                {
                    continue;
                }
                foreach (string piece in pieces.Where(piece => piece.Length > 0 && at + piece.Length <= part.Length && string.CompareOrdinal(part, at, piece, 0, piece.Length) == 0))
                {
                    reach[at + piece.Length] = true;
                }
            }
            return reach[part.Length];
        }

        private static IEnumerable<string> RibbonTexts(string code)
        {
            foreach (Match match in Regex.Matches(code, @"\bTab\s*=\s*""([^""]*)""|new RibbonPanelSpec\(\s*""([^""]*)""|new RibbonButtonSpec\(\s*(?:\d+\s*,\s*)?""([^""]*)"""))
            {
                yield return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            }
        }

        private static IEnumerable<string> XamlTexts(string xaml)
        {
            XDocument document = XDocument.Parse(xaml);
            foreach (XElement element in document.Descendants())
            {
                foreach (XAttribute attribute in element.Attributes())
                {
                    string value = attribute.Value;
                    if (value.StartsWith("{}", StringComparison.Ordinal))
                    {
                        yield return value.Substring(2);
                        continue;
                    }
                    if (value.StartsWith("{", StringComparison.Ordinal))
                    {
                        foreach (Match match in Regex.Matches(value, @"\b(?:FallbackValue|TargetNullValue|StringFormat)\s*=\s*(?:'([^']*)'|([^,}]*))"))
                        {
                            string text = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
                            if (HasLetter(text))
                            {
                                yield return text;
                            }
                        }
                        continue;
                    }
                    if (TextAttributes.Contains(attribute.Name.LocalName) && HasLetter(value))
                    {
                        yield return value;
                    }
                }
                if (element.Name.LocalName == "Setter" && TextAttributes.Contains((string)element.Attribute("Property")) && element.Attribute("Value") != null
                    && !((string)element.Attribute("Value")).StartsWith("{", StringComparison.Ordinal) && HasLetter((string)element.Attribute("Value")))
                {
                    yield return (string)element.Attribute("Value");
                }
                if (TextElements.Contains(element.Name.LocalName))
                {
                    foreach (XText node in element.Nodes().OfType<XText>())
                    {
                        string text = node.Value.Trim();
                        if (HasLetter(text))
                        {
                            yield return text;
                        }
                    }
                }
            }
        }

        // Every string literal outside comments that holds a letter, a regular, verbatim or interpolated one, the last as it is
        // written, so it never matches the list and the test names it
        private static IEnumerable<string> CodeTexts(string code)
        {
            int at = 0;
            while (at < code.Length)
            {
                char letter = code[at];
                char next = at + 1 < code.Length ? code[at + 1] : '\0';
                if (letter == '/' && next == '/')
                {
                    at = code.IndexOf('\n', at) < 0 ? code.Length : code.IndexOf('\n', at);
                    continue;
                }
                if (letter == '/' && next == '*')
                {
                    int end = code.IndexOf("*/", at + 2, StringComparison.Ordinal);
                    at = end < 0 ? code.Length : end + 2;
                    continue;
                }
                if (letter == '\'')
                {
                    at++;
                    while (at < code.Length && code[at] != '\'')
                    {
                        at += code[at] == '\\' ? 2 : 1;
                    }
                    at++;
                    continue;
                }
                bool verbatim = (letter == '@' && next == '"') || (letter == '$' && next == '@') || (letter == '@' && next == '$');
                bool interpolated = letter == '$';
                if (letter == '"' || ((letter == '@' || letter == '$') && (next == '"' || next == '@' || next == '$')))
                {
                    while (code[at] != '"')
                    {
                        at++;
                    }
                    at++;
                    var text = new StringBuilder();
                    while (at < code.Length)
                    {
                        if (verbatim && code[at] == '"' && at + 1 < code.Length && code[at + 1] == '"')
                        {
                            text.Append('"');
                            at += 2;
                            continue;
                        }
                        if (!verbatim && code[at] == '\\' && at + 1 < code.Length)
                        {
                            text.Append(code[at + 1] == '"' ? '"' : code[at + 1] == '\\' ? '\\' : code[at + 1]);
                            at += 2;
                            continue;
                        }
                        if (code[at] == '"')
                        {
                            break;
                        }
                        text.Append(code[at]);
                        at++;
                    }
                    at++;
                    if (HasLetter(text.ToString()) || interpolated)
                    {
                        yield return text.ToString();
                    }
                    continue;
                }
                at++;
            }
        }

        private static bool HasLetter(string text)
        {
            return text.Any(char.IsLetter);
        }
    }
}
