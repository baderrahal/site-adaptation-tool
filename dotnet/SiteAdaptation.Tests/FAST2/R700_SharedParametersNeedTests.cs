using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.SetUp;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    public class R700_SharedParametersNeedTests
    {
        private static readonly object[] Header = { "Parameter", "GUID", "Data type", "Group", "Category", "Filled by", "Sample value", "Keep" };

        private static ParameterSheet Sheet(params object[][] rows)
        {
            return ParameterSheet.FromRows("sheet.xlsx", new List<object[]> { Header }.Concat(rows).ToList());
        }

        private static object[] Row(string name, string guid, string category = "RVT Links", string filledBy = "7")
        {
            return new object[] { name, guid, "TEXT", "Text", category, filledBy, null, "Yes" };
        }

        [Fact]
        public void R700_a_sheet_of_link_parameters_each_with_a_guid_is_strong()
        {
            var input = new DataCheckInput { SheetName = "sheet.xlsx", SheetFound = true, Sheet = Sheet(Row("A", Guid.NewGuid().ToString()), Row("B", Guid.NewGuid().ToString())) };
            JObject need = DataCheckLogic.SharedParametersNeed(input);
            Assert.Equal("sharedParameters", (string)need["need"]);
            Assert.Equal("strong", (string)need["mark"]);
            Assert.Equal("2 parameters for the villa links in sheet.xlsx, each with a GUID.", (string)need["line"]);
        }

        [Fact]
        public void R700_a_link_parameter_with_no_guid_is_usable_and_named()
        {
            var input = new DataCheckInput { SheetName = "sheet.xlsx", SheetFound = true, Sheet = Sheet(Row("A", Guid.NewGuid().ToString()), Row("B", null)) };
            JObject need = DataCheckLogic.SharedParametersNeed(input);
            Assert.Equal("usable", (string)need["mark"]);
            Assert.Contains("1 with no GUID: B", (string)need["line"]);
        }

        [Fact]
        public void R700_a_sheet_with_no_link_row_is_missing()
        {
            var input = new DataCheckInput { SheetName = "sheet.xlsx", SheetFound = true, Sheet = Sheet(Row("Plot", Guid.NewGuid().ToString(), "Floors", "5")) };
            Assert.Equal("missing", (string)DataCheckLogic.SharedParametersNeed(input)["mark"]);
        }

        [Fact]
        public void R700_a_sheet_not_there_or_not_read_says_so_in_plain_words()
        {
            JObject gone = DataCheckLogic.SharedParametersNeed(new DataCheckInput { SheetName = "sheet.xlsx" });
            Assert.Equal("missing", (string)gone["mark"]);
            Assert.Equal("The parameter sheet sheet.xlsx is not there, so no shared parameter can go on the villa links.", (string)gone["line"]);
            JObject broken = DataCheckLogic.SharedParametersNeed(new DataCheckInput { SheetName = "sheet.xlsx", SheetFound = true, SheetProblem = "the sheet has no column GUID" });
            Assert.Equal("weak", (string)broken["mark"]);
            Assert.Equal("The parameter sheet sheet.xlsx could not be read: the sheet has no column GUID", (string)broken["line"]);
            JObject none = DataCheckLogic.SharedParametersNeed(new DataCheckInput());
            Assert.Equal("missing", (string)none["mark"]);
            Assert.Equal("No parameter sheet was given to the data check.", (string)none["line"]);
        }

        [Fact]
        public void R700_the_shipped_sheet_of_config_is_strong_and_never_gives_the_ffl_a_button()
        {
            string config = Path.Combine(RepoRoot(), "config", "project.json");
            ParameterSheet sheet = ParameterSheet.FromConfig(config, JObject.Parse(File.ReadAllText(config)));
            var input = new DataCheckInput { SheetName = "shared_parameters.xlsx", SheetFound = true, Sheet = sheet };
            Assert.Equal("strong", (string)DataCheckLogic.SharedParametersNeed(input)["mark"]);
            Assert.Null(sheet.Parameters.Single(item => item.Name == Placer.FflParameter).FilledBy);
            Assert.All(sheet.Parameters.Where(item => item.FilledBy == ParameterSheet.PlaceVillasButton), item => Assert.True(item.LinksOnly, item.Name));
        }

        internal static string RepoRoot()
        {
            string at = AppContext.BaseDirectory;
            while (at != null && !File.Exists(Path.Combine(at, "CLAUDE.md")))
            {
                at = Path.GetDirectoryName(at);
            }
            return at ?? throw new InvalidOperationException("the repo root with CLAUDE.md was not found above " + AppContext.BaseDirectory);
        }
    }
}
