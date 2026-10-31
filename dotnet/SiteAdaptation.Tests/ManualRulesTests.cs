using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ManualRules;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R403. The block manualRules of the rules profile and what Common makes of it for the window Site Adaptation Rules and for the
    // lookup that 4 Check and Review data call. They read the real profile of the repo, which holds no coordinate or level
    public sealed class ManualRulesTests
    {
        private static string Profile()
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "rules", "roshn-makkah.json");
        }

        private static ManualRuleBlock Block()
        {
            return ManualRuleBlock.FromFile(Profile());
        }

        [Fact]
        public void R403_the_reader_gives_m01_to_m15_in_order_with_text_group_reference_steps_and_pages()
        {
            ManualRuleBlock block = Block();

            Assert.Equal(Enumerable.Range(1, 15).Select(n => "M" + n.ToString("00")), block.Rules.Select(rule => rule.Id));
            ManualRule m05 = block.Find("M05");
            Assert.Equal("Front yard has one slope, parallel to the road, 0.5% to 2%", m05.Text);
            Assert.Equal("Slopes", m05.Group);
            Assert.Equal("Rev 01, kept", m05.ManualRef);
            Assert.Equal(new[] { "5 Floors and ramps" }, m05.CheckedIn);
            Assert.Equal("2.3.A, p. 10 (PDF 13)", m05.SectionPage);
            Assert.Equal(new[] { "4 Check", "7 Place villas" }, block.Find("M01").CheckedIn);
            Assert.Equal(new[] { "0.5% to 2%" }, m05.Values);
        }

        [Fact]
        public void R403_the_manual_is_named_by_the_block_with_its_revision_number_file_and_folders()
        {
            ManualInfo manual = Block().Manual;

            Assert.Equal("ROSHN Makkah Site Adaptation Manual", manual.Name);
            Assert.Equal("Rev 06", manual.Revision);
            Assert.Equal("Rev 01", manual.OlderRevision);
            Assert.Equal("01101-PSN-ZZZ-XXX-RPT-ZZ-001000", manual.Number);
            Assert.EndsWith(".PDF", manual.File);
            Assert.NotEmpty(manual.Folders);
        }

        [Fact]
        public void R403_the_lookup_by_manual_rule_id_gives_the_rule_or_null_and_its_line_for_a_side_box()
        {
            ManualRuleBlock block = Block();

            Assert.Equal("Garden wall top at FFL + 1.70 m", block.Find("M12").Text);
            Assert.Null(block.Find("M99"));
            Assert.Null(block.Find(null));
            Assert.Equal("M12 Garden wall top at FFL + 1.70 m", block.Line("M12"));
            Assert.Equal("M99", block.Line("M99"));
        }

        [Fact]
        public void R403_the_reader_refuses_a_profile_with_no_block_a_repeated_id_or_a_rule_with_no_text()
        {
            Assert.Throws<InvalidDataException>(() => ManualRuleBlock.Parse(JObject.Parse("{ }")));
            JObject twice = JObject.Parse(File.ReadAllText(Profile()));
            twice["manualRules"]["rules"][1]["id"] = "M01";
            Assert.Throws<InvalidDataException>(() => ManualRuleBlock.Parse(twice));
            JObject empty = JObject.Parse(File.ReadAllText(Profile()));
            empty["manualRules"]["rules"][2]["text"] = "";
            Assert.Throws<InvalidDataException>(() => ManualRuleBlock.Parse(empty));
        }

        [Fact]
        public void R403_the_chips_are_all_then_each_group_in_the_order_of_the_block_then_past_job_differs_with_their_counts()
        {
            var table = new ManualRuleTable(Block(), null);

            Assert.Equal(new[] { "All", "Plots and clusters", "Levels and steps", "Slopes", "Walls", "Structure", "Past job differs" },
                table.Chips.Select(chip => chip.Kind == ManualChipKind.Group ? chip.Group : chip.Kind == ManualChipKind.All ? "All" : "Past job differs"));
            Assert.Equal(new[] { 15, 4, 4, 3, 3, 1, 1 }, table.Chips.Select(table.CountOf));
        }

        [Fact]
        public void R403_a_chip_picks_the_rules_of_its_group_and_past_job_differs_picks_m06_alone()
        {
            var table = new ManualRuleTable(Block(), null);

            Assert.Equal(15, table.Rows(ManualChip.All).Count);
            Assert.Equal(new[] { "M05", "M06", "M07" }, table.Rows(ManualChip.OfGroup("Slopes")).Select(rule => rule.Id));
            Assert.Equal(new[] { "M06" }, table.Rows(ManualChip.PastJobDiffers).Select(rule => rule.Id));
            Assert.Empty(table.Rows(ManualChip.OfGroup("No such group")));
        }

        [Fact]
        public void R403_the_past_job_is_the_fact_given_else_the_block_and_not_measured_has_no_text()
        {
            ManualRuleBlock block = Block();
            var plain = new ManualRuleTable(block, null);

            Assert.Equal(PastJobState.NotMeasured, plain.PastJob(block.Find("M01")).State);
            Assert.Null(plain.PastJob(block.Find("M01")).Text);
            Assert.Equal(PastJobState.Matches, plain.PastJob(block.Find("M04")).State);
            Assert.Equal("Matches", plain.PastJob(block.Find("M04")).Text);
            Assert.Equal(PastJobState.Differs, plain.PastJob(block.Find("M06")).State);

            var facts = new Dictionary<string, PastJobFact> { { "M01", new PastJobFact(PastJobState.Differs, "Differs on 3 plots") } };
            var given = new ManualRuleTable(block, id => facts.TryGetValue(id, out PastJobFact fact) ? fact : null);
            Assert.Equal("Differs on 3 plots", given.PastJob(block.Find("M01")).Text);
            Assert.Equal(new[] { "M01", "M06" }, given.Rows(ManualChip.PastJobDiffers).Select(rule => rule.Id));
            Assert.Equal(PastJobState.Matches, given.PastJob(block.Find("M04")).State);
        }

        [Fact]
        public void R403_the_manual_pdf_is_found_in_the_first_folder_that_holds_it_else_the_folders_looked_in_are_given()
        {
            ManualInfo manual = new ManualInfo { File = "a.PDF", Folders = new[] { "one", "two" } };
            var found = ManualPdf.Find(manual, path => path.Replace(Path.DirectorySeparatorChar, '/') == "two/a.PDF");
            Assert.Equal("two/a.PDF", found.Path.Replace(Path.DirectorySeparatorChar, '/'));
            Assert.Equal(2, found.Looked.Count);

            var missing = ManualPdf.Find(manual, path => false);
            Assert.Null(missing.Path);
            Assert.Equal(new[] { "one/a.PDF", "two/a.PDF" }, missing.Looked.Select(path => path.Replace(Path.DirectorySeparatorChar, '/')));
        }

        [Fact]
        public void R403_no_text_of_a_rule_and_no_name_of_the_manual_is_written_in_the_code_of_the_window_or_of_the_block()
        {
            string root = ThemeGateTests.RepoRoot();
            ManualRuleBlock block = Block();
            var code = new List<string>();
            foreach (string folder in new[] { Path.Combine("SiteAdaptation.Common", "ManualRules"), Path.Combine("SiteAdaptation.AddIn", "RulesWindow") })
            {
                code.AddRange(Directory.EnumerateFiles(Path.Combine(root, "dotnet", folder), "*.*", SearchOption.AllDirectories)
                    .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".xaml", StringComparison.Ordinal)).Select(File.ReadAllText));
            }
            Assert.NotEmpty(code);
            foreach (string source in code)
            {
                foreach (ManualRule rule in block.Rules)
                {
                    Assert.DoesNotContain(rule.Text, source);
                }
                Assert.DoesNotContain(block.Manual.Name, source);
                Assert.DoesNotContain(block.Manual.Number, source);
                Assert.DoesNotContain(block.Manual.File, source);
            }
        }
    }
}
