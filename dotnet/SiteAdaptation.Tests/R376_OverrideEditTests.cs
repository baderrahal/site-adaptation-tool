using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R376, R386. Use and Save overrides on a made up override list, held against PlanOverrides.Load, the C# of plan_rules.load_overrides
    // that the parity of 4g3 holds to Python
    public sealed class R376_OverrideEditTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "override-edit-" + Guid.NewGuid().ToString("N"));

        private const string List = "{\n  \"note\": \"made up\",\n  \"overrides\": [\n"
            + "    {\"plot\": \"1\", \"field\": \"heightM\", \"value\": {\"gapToRuleM\": 0.05}, \"source\": \"parsons\", \"model\": \"kx-site.rvt\", \"key\": \"kx\"},\n"
            + "    {\"plot\": \"4\", \"field\": \"rampFrontFallM\", \"value\": {\"gapToRuleM\": 1e-05}, \"source\": \"parsons\", \"model\": \"kx-site.rvt\", \"key\": \"kx\", \"reason\": \"é kept\"},\n"
            + "    {\"plot\": \"5\", \"field\": \"steps\", \"value\": 3, \"source\": \"parsons\", \"model\": \"kx-site.rvt\", \"key\": \"kx\"}\n  ],\n"
            + "  \"cannot\": [\n    {\"plot\": \"8\", \"field\": \"entryM\", \"reason\": \"none\"}\n  ]\n}\n";

        public R376_OverrideEditTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static JObject Config()
        {
            return JObject.Parse("{ 'majority': { 'groups': { 'native': ['n1'], 'parsons': ['kx', 'ky'] }, 'overrideDefaultSources': ['parsons'] } }");
        }

        private static List<CheckRow> Rows()
        {
            return new List<CheckRow>
            {
                new CheckRow { Index = 0, Plot = "1", Check = "Level", Field = "heightM", From = "override", OpenUse = CheckUse.Past, PastForm = "gapToRuleM", PastOffered = true, Key = "kx", Model = "kx-site.rvt" },
                new CheckRow { Index = 1, Plot = "2", Check = "Hand", Field = "mirrored", From = "missing", OpenUse = CheckUse.Rule, PastForm = "trueOrFalse", PastValue = true, PastOffered = true, Key = "ky", Model = "ky-site.rvt" },
                new CheckRow { Index = 2, Plot = "3", Check = "Team check", From = "team", OpenUse = CheckUse.Rule, PastWhy = "the team marked it" },
                new CheckRow { Index = 3, Plot = "5", Check = "Level", Field = "steps", From = "override", OpenUse = CheckUse.Past, PastForm = "wholeNumber", PastOffered = true, Key = "kx", Model = "kx-site.rvt" },
                new CheckRow { Index = 4, Plot = "7", Check = "Hand", Field = "mirrored", From = "missing", OpenUse = CheckUse.Rule, PastForm = "trueOrFalse", PastWhy = "no answer key holds plot 7" },
            };
        }

        [Fact]
        public void R376_save_writes_only_the_changed_rows_keeps_every_other_entry_cell_for_cell_and_the_build_reads_them_back()
        {
            string path = Path.Combine(folder, "list.json");
            File.WriteAllText(path, List);
            var uses = new[] { new CheckChoice(0, CheckUse.Rule), new CheckChoice(1, CheckUse.Past), new CheckChoice(2, CheckUse.Skip), new CheckChoice(3, CheckUse.Past) };

            int changed = OverrideEdits.Save(path, Rows(), uses, Config(), "R4-1");

            Assert.Equal(3, changed);
            string text = File.ReadAllText(path);
            string[] lines = text.Split('\n');
            // Plot 4 and the cannot entry stay byte for byte, plot 5 set back to its opening Past is no change
            Assert.Contains(List.Split('\n')[4], lines);
            Assert.Contains(List.Split('\n')[5].TrimEnd(), lines.Select(line => line.TrimEnd(',')));
            Assert.Contains("    {\"plot\": \"8\", \"field\": \"entryM\", \"reason\": \"none\"}", lines);
            Assert.Contains("    {\"plot\": \"1\", \"field\": \"heightM\", \"value\": {\"gapToRuleM\": 0.05}, \"source\": \"parsons\", \"model\": \"kx-site.rvt\", \"key\": \"kx\", \"use\": \"rule\", \"byHand\": true},", lines);
            Assert.Contains(lines, line => line.StartsWith("    {\"plot\": \"2\", \"field\": \"mirrored\", \"value\": true, \"source\": \"parsons\", \"model\": \"ky-site.rvt\", \"key\": \"ky\"", StringComparison.Ordinal) && line.Contains("\"byHand\": true"));
            Assert.Contains(lines, line => line.StartsWith("    {\"plot\": \"3\", \"reason\": ", StringComparison.Ordinal) && line.EndsWith("\"byHand\": true}", StringComparison.Ordinal));
            Assert.False(File.Exists(path + ".tmp-R4-1"));

            OverrideList read = PlanOverrides.Load(path, Config(), JObject.Parse("{ 'job': 'parsons' }"), "p", "project.json");
            Assert.Equal("3", read.Skip.Keys.Single());
            Assert.True(read.Ruled["1"].ContainsKey("heightM"));
            Assert.Equal(true, read.Of("2")["mirrored"].Value<bool>());
            Assert.Equal(3, read.Of("5")["steps"].Value<int>());
        }

        [Fact]
        public void R376_a_row_set_back_to_its_opening_value_is_no_change_and_the_list_is_not_written()
        {
            string path = Path.Combine(folder, "list.json");
            File.WriteAllText(path, List);
            DateTime before = File.GetLastWriteTimeUtc(path);

            Assert.Equal(0, OverrideEdits.Save(path, Rows(), new[] { new CheckChoice(0, CheckUse.Past), new CheckChoice(1, CheckUse.Rule) }, Config(), "R4-2"));
            Assert.Equal(List, File.ReadAllText(path));
            Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        }

        [Fact]
        public void R376_past_where_it_is_not_offered_is_refused_with_its_reason_and_the_list_kept()
        {
            string path = Path.Combine(folder, "list.json");
            File.WriteAllText(path, List);

            var refused = Assert.Throws<InvalidOperationException>(() => OverrideEdits.Save(path, Rows(), new[] { new CheckChoice(4, CheckUse.Past) }, Config(), "R4-3"));
            Assert.Contains("no answer key holds plot 7", refused.Message);
            Assert.Equal(List, File.ReadAllText(path));
        }

        [Fact]
        public void R376_rule_on_an_override_row_marks_use_rule_and_past_on_a_row_the_build_left_marks_use_past()
        {
            List<CheckRow> rows = Rows();
            rows[0].OpenUse = CheckUse.Rule;
            JObject entry = JObject.Parse("{ 'plot': '1', 'field': 'heightM', 'value': 1, 'use': 'rule', 'byHand': true }");

            JObject changed = OverrideEdits.Entry(entry, rows[0], CheckUse.Past);

            Assert.Equal("past", (string)changed["use"]);
            Assert.True((bool)changed["byHand"]);
            Assert.Equal("rule", (string)entry["use"]);
        }
    }
}
