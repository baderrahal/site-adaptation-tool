using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R541. The values of Site Adaptation Rules against the real rules profile and catalog of the repo, on files in a temporary folder
    public class R541_RuleValuesTests
    {
        private static string Repo(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(folder, "CLAUDE.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }

        private static string Catalog => File.ReadAllText(Repo("config", RuleValues.CatalogName));

        private static string Rules => File.ReadAllText(Repo("rules", "roshn-makkah.json"));

        [Fact]
        public void R541_each_row_reads_a_number_of_the_profile_and_starts_at_the_manual_value()
        {
            var rows = RuleValues.Read(Catalog, Rules);
            Assert.Equal(9, rows.Count);
            Assert.Equal(rows.Count, rows.Select(row => row.Key).Distinct().Count());
            Assert.All(rows, row => Assert.False(row.Changed, row.Key));
            Assert.All(rows, row => Assert.InRange(row.Manual, row.Min, row.Max));
        }

        [Fact]
        public void R541_apply_changes_the_value_and_nothing_else_of_the_file()
        {
            var rows = RuleValues.Read(Catalog, Rules);
            RuleValue row = rows.Single(item => item.Key == "frontSlopeMax");
            row.Project = 1;
            JObject next = JObject.Parse(RuleValues.Apply(Rules, rows));
            JObject before = JObject.Parse(Rules);
            Assert.Equal(1.0, (double)next["slopes_percent"]["front_max"]);
            before["slopes_percent"]["front_max"] = next["slopes_percent"]["front_max"];
            Assert.True(JToken.DeepEquals(before, next));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("99")]
        [InlineData("-1")]
        [InlineData("NaN")]
        public void R541_a_value_that_is_no_number_or_past_its_limits_is_refused(string text)
        {
            RuleValue row = RuleValues.Read(Catalog, Rules).Single(item => item.Key == "frontSlopeMax");
            Assert.Null(RuleValues.Parse(row, text, out string refusal));
            Assert.NotNull(refusal);
        }

        [Fact]
        public void R541_export_and_import_give_the_same_values_and_a_strange_file_changes_nothing()
        {
            var rows = RuleValues.Read(Catalog, Rules);
            rows.Single(item => item.Key == "wallTop").Project = 2000;
            string text = RuleValues.ExportText(rows);
            var values = RuleValues.Import(text, RuleValues.Read(Catalog, Rules), out string refusal);
            Assert.Null(refusal);
            Assert.Equal(2000, values["wallTop"]);
            Assert.Null(RuleValues.Import("a,b\r\n1,2", rows, out refusal));
            Assert.NotNull(refusal);
            Assert.Null(RuleValues.Import(text.Replace("wallTop", "noSuchRule"), rows, out refusal));
            Assert.Contains("noSuchRule", refusal);
            Assert.Null(RuleValues.Import(text.Replace("2000", "90000"), rows, out refusal));
            Assert.NotNull(refusal);
        }

        [Fact]
        public void R541_a_smallest_over_its_largest_and_a_fraction_of_steps_are_refused()
        {
            var rows = RuleValues.Read(Catalog, Rules);
            Assert.Null(RuleValues.Contradiction(rows));
            rows.Single(item => item.Key == "frontSlopeMin").Project = 3;
            rows.Single(item => item.Key == "frontSlopeMax").Project = 1;
            Assert.Contains("Front ramp slope", RuleValues.Contradiction(rows));
            RuleValue steps = rows.Single(item => item.Key == "stepsMax");
            Assert.Null(RuleValues.Parse(steps, "2.5", out string refusal));
            Assert.NotNull(refusal);
            Assert.Equal(2, RuleValues.Parse(steps, "2", out refusal));
            Assert.Null(RuleValues.Parse(rows.Single(item => item.Key == "irregularTolerance"), "0", out refusal));
        }

        [Fact]
        public void R541_a_changed_rules_file_moves_its_hash_so_4_check_goes_back_to_ready()
        {
            string root = Path.Combine(Path.GetTempPath(), "r541_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string file = Path.Combine(root, "rules.json");
                File.WriteAllText(file, Rules);
                string before = SiteAdaptation.Common.Flow.InputHash.Of(new SiteAdaptation.Common.Flow.RunInput("Rules", file, false));
                var rows = RuleValues.Read(Catalog, Rules);
                rows.Single(item => item.Key == "frontSlopeMax").Project = 1;
                RuleValues.WriteCopy(file, RuleValues.Apply(Rules, rows), rows);
                Assert.NotEqual(before, SiteAdaptation.Common.Flow.InputHash.Of(new SiteAdaptation.Common.Flow.RunInput("Rules", file, false)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void R541_a_partial_import_changes_nothing()
        {
            var rows = RuleValues.Read(Catalog, Rules);
            string[] lines = RuleValues.ExportText(rows).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Null(RuleValues.Import(string.Join("\r\n", lines.Take(2)), rows, out string refusal));
            Assert.Contains("no line", refusal);
        }

        [Fact]
        public void R541_the_copy_lies_under_the_work_root_and_lists_what_changed_and_the_read_file_stays()
        {
            string root = Path.Combine(Path.GetTempPath(), "r541_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string source = Path.Combine(root, "shipped", "roshn-makkah.json");
                Directory.CreateDirectory(Path.GetDirectoryName(source));
                File.WriteAllText(source, Rules);
                string copy = RuleValues.CopyPath(Path.Combine(root, "work"), "Model_12345678", source);
                Assert.Equal(Path.Combine(root, "work", ShippedSet.ProjectsFolder, "Model_12345678", "rules", "roshn-makkah.json"), copy);

                var rows = RuleValues.Read(Catalog, Rules);
                rows.Single(item => item.Key == "wallTop").Project = 2000;
                RuleValues.WriteCopy(copy, RuleValues.Apply(Rules, rows), rows);
                Assert.Equal(Rules, File.ReadAllText(source));
                Assert.Equal(2000, RuleValues.Read(Catalog, File.ReadAllText(copy)).Single(item => item.Key == "wallTop").Project);
                JArray changed = (JArray)JObject.Parse(File.ReadAllText(RuleValues.ChangedPath(copy)))["changed"];
                Assert.Equal("wallTop", (string)changed.Single()["key"]);

                rows.Single(item => item.Key == "wallTop").Project = 1700;
                RuleValues.WriteCopy(copy, RuleValues.Apply(File.ReadAllText(copy), rows), rows);
                Assert.True(JToken.DeepEquals(JObject.Parse(Rules), JObject.Parse(File.ReadAllText(copy))));
                Assert.Empty((JArray)JObject.Parse(File.ReadAllText(RuleValues.ChangedPath(copy)))["changed"]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
