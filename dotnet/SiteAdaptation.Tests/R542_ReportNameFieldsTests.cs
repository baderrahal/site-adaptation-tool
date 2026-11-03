using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
#if NET48
using SiteAdaptation.AddIn.NewProject;
using SiteAdaptation.Common.Settings;
#endif
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    public class R542_ReportNameFieldsTests
    {
        private const string Config = "made/config.json";

        private static string ConfigText(bool block = true)
        {
            var report = new JObject();
            if (block)
            {
                report["fileName"] = new JObject
                {
                    ["note"] = "not a field",
                    ["project"] = "111",
                    ["originator"] = "AAA",
                    ["volume"] = ReportFile.NbhToken,
                    ["level"] = "XXX",
                    ["type"] = "SCH",
                    ["role"] = "TU",
                    ["number"] = "000001",
                    ["revision"] = "P01",
                };
            }
            return new JObject { ["addin"] = new JObject { ["report"] = report } }.ToString();
        }

#if NET48
        private static NewProjectViewModel Window(ProjectSettings settings, bool block = true)
        {
            settings.ConfigFile = Config;
            string text = ConfigText(block);
            return new NewProjectViewModel(settings, "Site", new SavedStart[0], null, (path, isFolder) => true, null, path => text, null, true, (inFolder, effective) => null);
        }

        private static ReportFieldRow Row(NewProjectViewModel model, string key)
        {
            return model.ReportRows.Single(row => row.Key == key);
        }

        [Fact]
        public void R542_the_rows_follow_the_name_then_revision_status_and_project_name()
        {
            Assert.Equal(
                ReportFile.NameFields.Concat(new[] { ReportFile.Revision, ReportFile.Status, ReportFile.ProjectName }),
                Window(new ProjectSettings()).ReportRows.Select(row => row.Key));
        }

        [Fact]
        public void R542_the_line_reads_the_config_with_the_token_kept()
        {
            NewProjectViewModel model = Window(new ProjectSettings());
            Assert.True(model.ReportShown);
            Assert.Equal("111", Row(model, "project").Value);
            Assert.Equal(NewProjectText.ReportNameLine + "111-AAA-<nbh>-XXX-SCH-TU-000001.xlsx", model.ReportLine);
        }

        [Fact]
        public void R542_an_edit_moves_the_line_and_the_save_writes_only_that_field()
        {
            NewProjectViewModel model = Window(new ProjectSettings());
            Row(model, "originator").Value = "BBB";
            Assert.Contains("111-BBB-", model.ReportLine);
            model.Save();
            Assert.Equal(new[] { "originator" }, model.Result.ReportFields.Keys);
            Assert.Equal("BBB", model.Result.ReportFields["originator"]);
        }

        [Fact]
        public void R542_the_saved_field_is_what_the_report_reads()
        {
            NewProjectViewModel model = Window(new ProjectSettings());
            Row(model, "number").Value = "000002";
            model.Save();
            SortedDictionary<string, string> fields = ReportFile.Fields(JObject.Parse(ConfigText()), model.Result.ReportFields);
            Assert.Equal("111-AAA-012-XXX-SCH-TU-000002.xlsx", ReportFile.Name("012", fields));
        }

        [Fact]
        public void R542_an_edit_back_to_the_shown_value_writes_nothing()
        {
            NewProjectViewModel model = Window(new ProjectSettings());
            Row(model, "level").Value = "ZZZ";
            Row(model, "level").Value = "XXX";
            model.Save();
            Assert.Null(model.Result.ReportFields);
        }

        [Fact]
        public void R542_an_emptied_field_goes_back_to_config()
        {
            var settings = new ProjectSettings { ReportFields = new SortedDictionary<string, string> { ["role"] = "QQ" } };
            NewProjectViewModel model = Window(settings);
            Assert.Equal("QQ", Row(model, "role").Value);
            Row(model, "role").Value = string.Empty;
            model.Save();
            Assert.Null(model.Result.ReportFields);
        }

        [Fact]
        public void R542_a_config_with_no_block_hides_the_section_and_writes_nothing()
        {
            NewProjectViewModel model = Window(new ProjectSettings(), block: false);
            Assert.False(model.ReportShown);
            Assert.Equal(string.Empty, model.ReportLine);
            model.Save();
            Assert.Null(model.Result.ReportFields);
        }

        [Fact]
        public void R542_a_field_no_file_name_can_hold_shows_the_refusal_as_the_report_would_refuse()
        {
            NewProjectViewModel model = Window(new ProjectSettings());
            Row(model, "type").Value = "A/B";
            Assert.Equal(NewProjectText.ReportNameRefused, model.ReportLine);
            model.Save();
            SortedDictionary<string, string> fields = ReportFile.Fields(JObject.Parse(ConfigText()), model.Result.ReportFields);
            Assert.Throws<InvalidDataException>(() => ReportFile.Name("012", fields));
        }

#endif

        [Fact]
        public void R542_a_field_still_empty_shows_by_its_name()
        {
            Assert.Equal("<project>-A-<volume>-<level>-<type>-<role>-<number>.xlsx", ReportFile.Shown(new Dictionary<string, string> { ["originator"] = "A" }));
        }

        [Fact]
        public void R550_the_report_still_refuses_a_block_with_a_field_missing()
        {
            var config = JObject.Parse(ConfigText());
            ((JObject)config["addin"]["report"]["fileName"]).Remove("number");
            Assert.Throws<InvalidDataException>(() => ReportFile.Fields(config, null));
            Assert.False(ReportFile.Laid(config, null).ContainsKey("number"));
        }
    }
}
