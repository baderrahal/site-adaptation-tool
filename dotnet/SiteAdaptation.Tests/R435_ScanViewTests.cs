using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using CadReader;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R435 and R436. The window What the tool read as data, with no window and no Revit. The scan.json of each test is made here, small,
    // with made up names and no coordinate
    public sealed class R435_ScanViewTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_scanview_" + Guid.NewGuid().ToString("N"));

        public R435_ScanViewTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        public static JObject Row(string kind, string source, string found, int score, string use)
        {
            return new JObject { ["kind"] = kind, ["source"] = source, ["found"] = found, ["proposedUse"] = use, ["score"] = score, ["conflicts"] = 0 };
        }

        public static JObject Conflict(string plot, string field, string a, string sourceA, string b, string sourceB)
        {
            return new JObject { ["plot"] = plot, ["field"] = field, ["valueA"] = a, ["sourceA"] = sourceA, ["valueB"] = b, ["sourceB"] = sourceB };
        }

        public static JObject Scan(bool villaClear, int conflicts)
        {
            var rows = new JArray
            {
                Row("cadLayer", "site.dwg|OUTLINE", "4 closed outlines, 6 texts, 1 blocks", 0, ""),
                Row("cadLayer", "site.dwg|NUMBERS", "0 closed outlines, 5 texts, 0 blocks", 0, ""),
                Row("cadLayer", "feet.dwg|BLOCKS", "0 closed outlines, 0 texts, 7 blocks", 0, ""),
                Row("plotOutline", "site.dwg|OUTLINE", "5 faces", 5, "plot outlines"),
                Row("plotOutline", "site.dwg|OUTLINE + EDGE", "7 faces", 3, "check"),
                Row("plotNumber", "site.dwg|NUMBERS", "5 labels in plots", 5, "plot numbers"),
                Row("villaBlock", "feet.dwg|BLOCKS", "7 inserts", 4, "villa blocks"),
                Row("villaType", "feet.dwg|block|BLOCKS", "A1, B2", 4, villaClear ? "villa type" : "check"),
                Row("villaType", "feet.dwg|text|NOTES", "A1", 2, "check"),
                Row("villaType", "list.xlsx|Sheet 1|Type|shift 0", "A1, B2", 3, "check"),
                Row("villaType", "pages.pdf|page 2|shift 100", "B2", 1, "check"),
            };
            var found = new JArray();
            for (int plot = 1; plot <= conflicts; plot++)
            {
                found.Add(Conflict(plot.ToString(), "villa type", "A1", "feet.dwg|block|BLOCKS", "B2", "list.xlsx|Sheet 1|Type|shift 0"));
            }
            return new JObject
            {
                ["inputs"] = new JArray
                {
                    new JObject { ["role"] = "scanCad:1", ["name"] = "site.dwg", ["size"] = 1, ["sha256"] = "00" },
                    new JObject { ["role"] = "scanCad:2", ["name"] = "feet.dwg", ["size"] = 1, ["sha256"] = "00" },
                    new JObject { ["role"] = "scanExcel:1", ["name"] = "list.xlsx", ["size"] = 1, ["sha256"] = "00" },
                    new JObject { ["role"] = "scanPdf:1", ["name"] = "pages.pdf", ["size"] = 1, ["sha256"] = "00" },
                    new JObject { ["role"] = "prototype:Type A.rvt", ["name"] = "Type A.rvt", ["size"] = 1, ["sha256"] = "00" },
                },
                ["rows"] = rows,
                ["conflicts"] = found,
                ["checks"] = new JArray { new JObject { ["what"] = "notRead", ["source"] = "feet.dwg|xref base", ["plot"] = "", ["value"] = "external reference" } },
                ["plots"] = new JArray(Enumerable.Range(1, 5).Select(number => new JObject { ["plot"] = number.ToString() })),
            };
        }

        [Fact]
        public void R435_a_value_with_a_proposed_source_is_clear_and_shows_its_source_covers_and_checks()
        {
            ScanViewData view = ScanView.Read(Scan(true, 0).ToString(), new ProjectSettings());

            Assert.Equal(new[] { "Plot outline", "Plot number", "Villa block", "Villa type" }, view.Values.Select(value => value.Label));
            ScanValueRow outline = view.Values[0];
            Assert.Equal("Clear", outline.Status);
            Assert.Equal("site.dwg|OUTLINE", outline.Selected);
            Assert.Equal(new[] { "site.dwg, layer OUTLINE", "site.dwg, layer OUTLINE + EDGE" }, outline.Sources.Select(source => source.Shown));
            Assert.Equal("5 plots", outline.Covers);
            Assert.Equal("1 files", outline.Checks);

            ScanValueRow villa = view.Values[3];
            Assert.Equal("Clear", villa.Status);
            Assert.Equal(new[] { "feet.dwg, blocks on BLOCKS", "feet.dwg, texts on NOTES", "list.xlsx, sheet Sheet 1, column Type", "pages.pdf, page 2" }, villa.Sources.Select(source => source.Shown));
            Assert.Equal("4 of 5", villa.Covers);
            Assert.Equal("3 files", villa.Checks);
            Assert.Empty(view.Unclear);
            Assert.False(view.NeedsWindow);
        }

        [Fact]
        public void R435_a_value_with_no_proposed_source_is_pick_and_says_what_the_best_covers()
        {
            ScanViewData view = ScanView.Read(Scan(false, 0).ToString(), new ProjectSettings());

            ScanValueRow villa = view.Values.Single(value => value.Kind == "villaType");
            Assert.Equal("Pick", villa.Status);
            Assert.Null(villa.Selected);
            Assert.Equal("4 of 5", villa.Covers);
            ScanUnclear unclear = view.Unclear.Single();
            Assert.Equal("Villa type is not clear", unclear.Title);
            Assert.Equal("No source matches your prototype names on enough plots. The best covers 4 of 5.", unclear.Line);
            Assert.Equal("What to do: pick the source in the row Villa type, or ask the master planner which file is current.", unclear.ToDo);
            Assert.True(view.NeedsWindow);

            // Another value with no proposal says it in the other words
            JObject scan = Scan(true, 0);
            foreach (JToken row in scan["rows"].Where(row => (string)row["kind"] == "plotNumber"))
            {
                row["proposedUse"] = "check";
            }
            ScanUnclear number = ScanView.Read(scan.ToString(), new ProjectSettings()).Unclear.Single();
            Assert.Equal("Plot number is not clear", number.Title);
            Assert.Equal("No source covers enough plots. The best covers 5 of 5.", number.Line);
            Assert.Equal("What to do: pick the source in the row Plot number.", number.ToDo);
        }

        [Fact]
        public void R435_a_saved_pick_that_names_a_source_wins_over_the_proposal_and_one_that_names_none_asks_again()
        {
            var settings = new ProjectSettings
            {
                ScanPicks = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["villaType"] = "list.xlsx|Sheet 1|Type|shift 0",
                    ["plotOutline"] = "gone.dwg|NOWHERE",
                },
            };
            ScanViewData view = ScanView.Read(Scan(false, 0).ToString(), settings);

            ScanValueRow villa = view.Values.Single(value => value.Kind == "villaType");
            Assert.Equal("Clear", villa.Status);
            Assert.Equal("list.xlsx|Sheet 1|Type|shift 0", villa.Selected);
            Assert.Equal("3 of 5", villa.Covers);
            // Breaker of SCAN-UI: the plot outline pick names no source of this scan, so it asks again
            Assert.Null(view.Values.Single(value => value.Kind == "plotOutline").Selected);
            Assert.True(view.NeedsWindow);

            ScanViewData proposed = ScanView.Read(Scan(true, 0).ToString(), new ProjectSettings { ScanPicks = settings.ScanPicks });
            Assert.Equal("list.xlsx|Sheet 1|Type|shift 0", proposed.Values.Single(value => value.Kind == "villaType").Selected);
            Assert.Equal("feet.dwg|block|BLOCKS", proposed.Values.Single(value => value.Kind == "villaType").Proposed);
        }

        [Fact]
        public void R435_a_conflict_is_open_until_a_saved_pick_names_one_of_its_two_sources()
        {
            string text = Scan(true, 3).ToString();
            ScanViewData open = ScanView.Read(text, new ProjectSettings());

            Assert.Equal(3, open.ConflictCount);
            Assert.All(open.Conflicts, conflict => Assert.True(conflict.Open));
            Assert.All(open.Conflicts, conflict => Assert.Equal(conflict.SourceA, conflict.Selected));
            ScanConflictRow first = open.Conflicts[0];
            Assert.Equal("Villa type", first.Value);
            Assert.Equal("A1 from feet.dwg", first.ChoiceA);
            Assert.Equal("B2 from list.xlsx", first.ChoiceB);
            Assert.True(open.NeedsWindow);

            var settings = new ProjectSettings
            {
                ConflictPicks = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["1|villa type|list.xlsx|Sheet 1|Type|shift 0"] = "list.xlsx|Sheet 1|Type|shift 0",
                    ["2|villa type|list.xlsx|Sheet 1|Type|shift 0"] = "feet.dwg|block|BLOCKS",
                    ["3|villa type|list.xlsx|Sheet 1|Type|shift 0"] = "neither.dwg|X",
                },
            };
            ScanViewData picked = ScanView.Read(text, settings);
            Assert.Equal(new[] { false, false, true }, picked.Conflicts.Select(conflict => conflict.Open));
            Assert.Equal("B2 from list.xlsx", picked.Conflicts[0].SelectedChoice);
            Assert.Equal("A1 from feet.dwg", picked.Conflicts[1].SelectedChoice);
            Assert.Equal("feet.dwg|block|BLOCKS", picked.Conflicts[2].Selected);
            Assert.True(picked.NeedsWindow);

            settings.ConflictPicks["3|villa type|list.xlsx|Sheet 1|Type|shift 0"] = "feet.dwg|block|BLOCKS";
            Assert.False(ScanView.Read(text, settings).NeedsWindow);
        }

        [Fact]
        public void R435_each_file_shows_its_kind_what_it_found_and_not_read_when_a_check_names_it()
        {
            ScanViewData view = ScanView.Read(Scan(true, 2).ToString(), new ProjectSettings());

            Assert.Equal(5, view.FilesRead);
            Assert.Equal(5, view.PlotsFound);
            Assert.Equal(2, view.ConflictCount);
            Assert.Equal(new[] { "site.dwg", "feet.dwg", "list.xlsx", "pages.pdf", "Type A.rvt" }, view.Files.Select(file => file.Name));
            Assert.Equal(new[] { "CAD", "CAD", "Excel", "PDF", "Prototype" }, view.Files.Select(file => file.Kind));
            Assert.Equal("2 layers, 4 closed outlines, 11 texts, 1 blocks", view.Files[0].Found);
            Assert.Equal("1 layers, 0 closed outlines, 0 texts, 7 blocks", view.Files[1].Found);
            Assert.Equal("1 villa type sources", view.Files[2].Found);
            Assert.Equal("1 villa type sources", view.Files[3].Found);
            Assert.Equal(new[] { "Read", "Not read", "Read", "Read", "Read" }, view.Files.Select(file => file.Status));
        }

        [Fact]
        public void R435_the_settings_file_of_role_config_is_no_file_row_and_not_counted()
        {
            JObject scan = Scan(true, 0);
            ((JArray)scan["inputs"]).Insert(0, new JObject { ["role"] = "config", ["name"] = "project.json", ["size"] = 1, ["sha256"] = "00" });
            ScanViewData data = ScanView.Read(scan.ToString(), new ProjectSettings());
            Assert.DoesNotContain(data.Files, file => file.Name == "project.json");
            Assert.Equal(5, data.FilesRead);
        }

        [Fact]
        public void R435_two_conflicts_of_one_plot_and_field_keep_a_pick_each_by_their_other_source()
        {
            JObject scan = Scan(true, 0);
            ((JArray)scan["conflicts"]).Add(Conflict("7", "villa type", "A1", "feet.dwg|block|BLOCKS", "B2", "list.xlsx|Sheet 1|Type|shift 0"));
            ((JArray)scan["conflicts"]).Add(Conflict("7", "villa type", "A1", "feet.dwg|block|BLOCKS", "C3", "pages.pdf|page 2|shift 100"));
            ScanViewData open = ScanView.Read(scan.ToString(), new ProjectSettings());
            Assert.NotEqual(open.Conflicts[0].Key, open.Conflicts[1].Key);
            var settings = new ProjectSettings { ConflictPicks = new SortedDictionary<string, string> { [open.Conflicts[0].Key] = "list.xlsx|Sheet 1|Type|shift 0" } };
            ScanViewData data = ScanView.Read(scan.ToString(), settings);
            Assert.False(data.Conflicts[0].Open);
            Assert.Equal("list.xlsx|Sheet 1|Type|shift 0", data.Conflicts[0].Selected);
            Assert.True(data.Conflicts[1].Open);
            Assert.True(data.NeedsWindow);
        }

        [Fact]
        public void R435_a_scan_with_no_villa_type_source_or_no_plot_outline_asks_for_a_pick()
        {
            JObject scan = Scan(true, 0);
            foreach (JToken row in ((JArray)scan["rows"]).Where(row => (string)row["kind"] == "villaType" || (string)row["kind"] == "plotOutline").ToList())
            {
                row.Remove();
            }
            ScanViewData data = ScanView.Read(scan.ToString(), new ProjectSettings());
            Assert.Equal("Pick", data.Values.Single(value => value.Kind == "villaType").Status);
            Assert.Equal("Pick", data.Values.Single(value => value.Kind == "plotOutline").Status);
            Assert.True(data.NeedsWindow);
        }

        [Fact]
        public void R436_apply_lays_the_picks_over_the_settings_and_keeps_the_settings_given_and_every_other_pick()
        {
            var current = new ProjectSettings
            {
                ScanPicks = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["plotNumber"] = "site.dwg|NUMBERS" },
                ConflictPicks = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["1|villa type|list.xlsx|Sheet 1|Type|shift 0"] = "feet.dwg|block|BLOCKS" },
                VillaMode = VillaModes.Masses,
            };

            ProjectSettings next = ScanPicks.Apply(
                new Dictionary<string, string> { ["villaType"] = "list.xlsx|Sheet 1|Type|shift 0", ["plotOutline"] = "" },
                new Dictionary<string, string> { ["1|villa type|list.xlsx|Sheet 1|Type|shift 0"] = "list.xlsx|Sheet 1|Type|shift 0", ["2|villa type|list.xlsx|Sheet 1|Type|shift 0"] = "feet.dwg|block|BLOCKS" },
                current);

            Assert.Equal(new[] { "plotNumber", "villaType" }, next.ScanPicks.Keys);
            Assert.Equal("list.xlsx|Sheet 1|Type|shift 0", next.ScanPicks["villaType"]);
            Assert.Equal("list.xlsx|Sheet 1|Type|shift 0", next.ConflictPicks["1|villa type|list.xlsx|Sheet 1|Type|shift 0"]);
            Assert.Equal("feet.dwg|block|BLOCKS", next.ConflictPicks["2|villa type|list.xlsx|Sheet 1|Type|shift 0"]);
            Assert.Equal(VillaModes.Masses, next.VillaMode);
            Assert.Single(current.ScanPicks);
            Assert.Equal("feet.dwg|block|BLOCKS", current.ConflictPicks["1|villa type|list.xlsx|Sheet 1|Type|shift 0"]);

            // The round trip: what the settings JSON holds reads back as the picks
            ProjectSettings again = SettingsJson.Read(SettingsJson.Write(next));
            Assert.Equal(next.ScanPicks, again.ScanPicks);
            Assert.Equal(next.ConflictPicks, again.ConflictPicks);

            // No picks leaves the fields as they were, null when they were null
            ProjectSettings none = ScanPicks.Apply(null, null, new ProjectSettings());
            Assert.Null(none.ScanPicks);
            Assert.Null(none.ConflictPicks);
        }

        [Fact]
        public void R436_export_writes_the_values_files_and_conflicts_sheets_and_refuses_a_file_that_exists()
        {
            ScanViewData view = ScanView.Read(Scan(false, 2).ToString(), new ProjectSettings());
            view.Conflicts[1].Selected = view.Conflicts[1].SourceB;
            view.Conflicts[1].Picked = true;
            string path = Path.Combine(folder, "read.xlsx");

            ScanPicks.Export(path, view);

            XlsxSheet.Sheet values = XlsxSheet.Read(path, "Values");
            Assert.Equal(new object[] { "Value", "Source", "Covers", "Checks", "Status" }, values.Rows[0]);
            Assert.Equal(5, values.Rows.Count);
            Assert.Equal(new object[] { "Plot outline", "site.dwg, layer OUTLINE", "5 plots", "1 files", "Clear" }, values.Rows[1]);
            Assert.Equal(new object[] { "Villa type", "", "4 of 5", "3 files", "Pick" }, values.Rows[4]);
            XlsxSheet.Sheet files = XlsxSheet.Read(path, "Files");
            Assert.Equal(6, files.Rows.Count);
            Assert.Equal(new object[] { "feet.dwg", "CAD", "1 layers, 0 closed outlines, 0 texts, 7 blocks", "Not read" }, files.Rows[2]);
            XlsxSheet.Sheet conflicts = XlsxSheet.Read(path, "Conflicts");
            Assert.Equal(3, conflicts.Rows.Count);
            Assert.Equal(new object[] { "1", "Villa type", "A1 from feet.dwg", "A1 from feet.dwg", "B2 from list.xlsx" }, conflicts.Rows[1]);
            Assert.Equal("B2 from list.xlsx", conflicts.Rows[2][2]);

            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => ScanPicks.Export(path, view));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
    }
}
