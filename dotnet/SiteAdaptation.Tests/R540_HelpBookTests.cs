using System;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.Help;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R540. The file python/build_help.py builds, read as the add-in reads it. test_r540_help.py holds it equal to the guide
    public class R540_HelpBookTests
    {
        // R558. Numbers of the Revit pass of 4 Nov at 133 percent, in window units: work area 1440 by 855, pane 607 to 1081, box 319 wide
        [Theory]
        [InlineData(513.0, 912.0, 283.0, 0.0, 319.0)]
        [InlineData(558.0, 700.0, 283.0, 155.0, 319.0)]
        [InlineData(100.0, 400.0, 283.0, 100.0, 319.0)]
        [InlineData(300.0, 855.0, 283.0, 0.0, 319.0)]
        public void R558_the_box_stands_beside_the_host_and_inside_the_work_area(double iconTop, double height, double expectedLeft, double expectedTop, double width)
        {
            HelpBoxSpot spot = HelpPlace.Of(607, 1081, iconTop, width, height, 0, 0, 1440, 855, 5);
            Assert.Equal(expectedLeft, spot.Left);
            Assert.Equal(expectedTop, spot.Top);
            Assert.Equal(855, spot.MaxHeight);
            Assert.True(spot.Left + width <= 607, "the box stands on the pane");
            Assert.True(spot.Top + Math.Min(height, spot.MaxHeight) <= 855, "Close is under the screen");
        }

        // R558. No room left of a wide window, as New project at 49 to 1399 pixels: the box goes right of it, and to the edge only when neither side fits
        [Fact]
        public void R558_the_box_takes_the_other_side_then_the_edge()
        {
            Assert.Equal(1055, HelpPlace.Of(37, 1050, 100, 319, 200, 0, 0, 1440, 855, 5).Left);
            Assert.Equal(0, HelpPlace.Of(37, 1300, 100, 319, 200, 0, 0, 1440, 855, 5).Left);
        }

        private static string Root()
        {
            string folder = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(folder, "CLAUDE.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            return folder;
        }

        private static string Addin(params string[] parts)
        {
            return Path.Combine(new[] { Root(), "dotnet", "SiteAdaptation.AddIn" }.Concat(parts).ToArray());
        }

        private static HelpBook Book()
        {
            return HelpBook.Parse(File.ReadAllText(Addin("Help", "help.json")));
        }

        [Fact]
        public void R540_every_step_and_new_project_row_has_a_line_and_its_parts()
        {
            HelpBook book = Book();
            foreach (string key in Enumerable.Range(1, 9).Select(HelpBook.StepKey).Concat(SetupRows.All.Select(row => row.Id)))
            {
                HelpEntry entry = book.Find(key);
                Assert.NotNull(entry);
                Assert.False(string.IsNullOrWhiteSpace(entry.Line), key);
                Assert.False(string.IsNullOrWhiteSpace(entry.What), key);
                Assert.NotEmpty(entry.Files);
                Assert.False(string.IsNullOrWhiteSpace(entry.Before), key);
                Assert.NotEmpty(entry.Click);
            }
            Assert.Null(book.Find("no such row"));
        }

        [Fact]
        public void R540_the_file_ships_inside_the_addin_and_each_i_opens_the_box()
        {
            Assert.Contains("<EmbeddedResource Include=\"Help\\help.json\"", File.ReadAllText(Addin("SiteAdaptation.AddIn.csproj")));
            Assert.Contains("Click=\"OnStepHelp\"", File.ReadAllText(Addin("Pane", "SitePanePage.xaml")));
            Assert.Contains("Click=\"OnRowHelp\"", File.ReadAllText(Addin("NewProject", "NewProjectWindow.xaml")));
            Assert.Contains("HelpBox.Show(", File.ReadAllText(Addin("Pane", "SitePanePage.xaml.cs")));
            Assert.Contains("HelpBox.Show(", File.ReadAllText(Addin("NewProject", "NewProjectWindow.xaml.cs")));
        }
    }
}
