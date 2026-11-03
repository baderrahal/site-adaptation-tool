using SiteAdaptation.Common.SetUp;
using Xunit;
#if NET48
using System.IO;
using SiteAdaptation.AddIn.Pane;
#endif

namespace SiteAdaptation.Tests
{
    public class R543_StepOneLineTests
    {
        [Theory]
        [InlineData("{\"positionSource\":\"cad\"}", SiteSources.Cad)]
        [InlineData("{\"positionSource\":\"typed\"}", SiteSources.Typed)]
        [InlineData("{\"other\":1}", null)]
        [InlineData("{\"positionSource\":3}", null)]
        [InlineData("not json", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void R543_the_source_is_read_or_is_none(string siteText, string source)
        {
            Assert.Equal(source, SiteSources.Written(siteText));
        }

#if NET48
        [Theory]
        [InlineData("cad", PaneText.StepLine1Cad)]
        [InlineData("typed", PaneText.StepLine1Typed)]
        [InlineData("none", PaneText.StepLine1)]
        public void R543_each_source_has_its_line(string source, string line)
        {
            Assert.Equal(line, PaneText.StepOneLine("{\"positionSource\":\"" + source + "\"}"));
        }

        [Fact]
        public void R543_the_position_file_has_its_line()
        {
            Assert.Equal(PaneText.StepLine1File, PaneText.StepOneLine("{\"positionSource\":\"" + SiteSources.PositionFile + "\"}"));
        }

        [Fact]
        public void R543_no_site_file_gives_the_plain_line()
        {
            Assert.Equal(PaneText.StepLine1, PaneText.StepOneLine(null));
        }

        [Fact]
        public void R543_the_pane_row_and_the_box_read_site_json_of_the_work_folder()
        {
            string asked = null;
            var model = new PaneViewModel(new PaneTests.FakeProbe())
            {
                WorkFolderOf = path => "work",
                ReadText = path =>
                {
                    asked = path;
                    return "{\"positionSource\":\"typed\"}";
                },
            };
            model.Load(new DocumentSession("s", "C:\\made\\Site.rvt", "Site"));
            Assert.Equal(Path.Combine("work", SetUpRun.SiteFile), asked);
            Assert.Equal(PaneText.StepLine1Typed, model.Steps[0].Line);
        }
#endif
    }
}
