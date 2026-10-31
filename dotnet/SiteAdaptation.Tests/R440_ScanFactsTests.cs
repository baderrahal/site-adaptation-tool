using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R440 Drawing facts from the scan, and the scan picks of Save setup (R432 with R436), SCAN-UI of Lane C
    public class R440_ScanFactsTests
    {
        [Fact]
        public void R440_the_facts_count_the_layers_of_the_plot_drawing_the_plots_and_the_villa_types_and_name_the_proposed_layer()
        {
            var scan = R435_ScanViewTests.Scan(true, 0);
            scan["plots"][0]["villaType"] = "A1";
            scan["plots"][1]["villaType"] = "B2";
            scan["plots"][2]["villaType"] = "A1";
            ScanFactsData facts = ScanFacts.Of(scan.ToString());
            Assert.Equal(2, facts.Layers);
            Assert.Equal(5, facts.Plots);
            Assert.Equal(2, facts.VillaTypes);
            Assert.Equal("OUTLINE", facts.PlotLayer);
            Assert.Equal("site.dwg|OUTLINE", facts.LayerProposed);
            Assert.Equal(new[] { "site.dwg|OUTLINE", "site.dwg|OUTLINE + EDGE" }, facts.LayerChoices.Select(source => source.Raw));
        }

        [Fact]
        public void R440_the_sheets_and_columns_are_the_excel_villa_type_sources_and_no_block_text_or_page_source()
        {
            ScanFactsData facts = ScanFacts.Of(R435_ScanViewTests.Scan(false, 0).ToString());
            ScanSheetChoice sheet = Assert.Single(facts.Sheets);
            Assert.Equal("list.xlsx, Sheet 1", sheet.Shown);
            Assert.Equal("Type", Assert.Single(sheet.Columns).Column);
            Assert.Equal("list.xlsx|Sheet 1|Type|shift 0", sheet.Columns[0].Raw);
            Assert.Null(facts.VillaTypeProposed);
        }

        [Fact]
        public void R440_a_sheet_name_with_a_bar_keeps_the_column_and_shift_from_the_end()
        {
            Assert.True(ScanFacts.Excel("a.xlsx|Plots|2024|Code|shift 100", out string file, out string sheet, out string column));
            Assert.Equal(new[] { "a.xlsx", "Plots|2024", "Code" }, new[] { file, sheet, column });
            Assert.False(ScanFacts.Excel("a.dwg|block|VL", out _, out _, out _));
            Assert.False(ScanFacts.Excel("a.pdf|page 2|shift 0", out _, out _, out _));
        }

        [Fact]
        public void R440_save_setup_lays_the_scan_picks_over_those_held_and_keeps_the_conflict_picks_of_the_window()
        {
            var current = new ProjectSettings { ScanPicks = new SortedDictionary<string, string> { ["plotNumber"] = "site.dwg|NUMBERS" } };
            var draft = new SetupDraft
            {
                ScanPicks = new Dictionary<string, string> { ["plotOutline"] = "site.dwg|OUTLINE + EDGE" },
                ConflictPicks = new Dictionary<string, string> { ["1|villa type"] = "feet.dwg|block|BLOCKS" },
            };
            ProjectSettings next = SetupSave.Apply(draft, current);
            Assert.Equal("site.dwg|NUMBERS", next.ScanPicks["plotNumber"]);
            Assert.Equal("site.dwg|OUTLINE + EDGE", next.ScanPicks["plotOutline"]);
            Assert.Equal("feet.dwg|block|BLOCKS", next.ConflictPicks["1|villa type"]);
            Assert.False(current.ScanPicks.ContainsKey("plotOutline"));
        }
    }
}
