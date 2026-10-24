using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.C3DProbe;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The plain parts of round DATA: the Phase 1 border, the CSV tables and what the watcher clicks. They say nothing about
    // what Civil 3D shows or returns
    public class DataBookTests
    {
        private static Border Square()
        {
            return new Border(new List<double[]> { new double[] { 0, 0 }, new double[] { 10, 0 }, new double[] { 10, 10 }, new double[] { 0, 10 }, new double[] { 0, 0 } });
        }

        [Fact]
        public void R162_point_inside_and_outside_the_border()
        {
            var border = Square();
            Assert.Equal(4, border.Count);
            Assert.True(border.Contains(5, 5));
            Assert.False(border.Contains(15, 5));
            Assert.False(border.Contains(-0.1, 5));
        }

        [Fact]
        public void R162_a_line_wholly_inside_is_kept_whole_and_not_cut()
        {
            var clip = Square().Clip(new List<double[]> { new double[] { 1, 1, 170 }, new double[] { 9, 1, 171 } });
            Assert.Single(clip.Parts);
            Assert.False(clip.Cut);
            Assert.Equal(8, Border.Length(clip.Parts[0]), 6);
        }

        [Fact]
        public void R162_a_line_across_the_border_is_cut_with_z_carried()
        {
            var clip = Square().Clip(new List<double[]> { new double[] { 5, 5, 100 }, new double[] { 15, 5, 110 } });
            Assert.True(clip.Cut);
            Assert.Single(clip.Parts);
            var end = clip.Parts[0].Last();
            Assert.Equal(10, end[0], 6);
            Assert.Equal(105, end[2], 6);
        }

        [Fact]
        public void R162_a_polyline_that_leaves_and_comes_back_gives_two_parts()
        {
            var points = new List<double[]> { new double[] { 2, 5 }, new double[] { 12, 5 }, new double[] { 12, 8 }, new double[] { 2, 8 } };
            var clip = Square().Clip(points);
            Assert.True(clip.Cut);
            Assert.Equal(2, clip.Parts.Count);
            Assert.Equal(8, Border.Length(clip.Parts[0]), 6);
            Assert.Equal(8, Border.Length(clip.Parts[1]), 6);
        }

        [Fact]
        public void R162_a_line_wholly_outside_gives_nothing()
        {
            var clip = Square().Clip(new List<double[]> { new double[] { 20, 20 }, new double[] { 30, 20 } });
            Assert.Empty(clip.Parts);
            Assert.False(clip.Cut);
        }

        [Fact]
        public void R162_border_reads_x_y_from_the_border_csv_and_skips_the_header()
        {
            var border = Border.FromCsv(new[] { "x,y,vertex,area", "0,0,0,100", "10,0,1,100", "10,10,2,100", "0,10,3,100" });
            Assert.Equal(4, border.Count);
            Assert.True(border.Contains(1, 1));
        }

        [Fact]
        public void R162_csv_quotes_commas_quotes_and_line_breaks_and_writes_yes_no()
        {
            Assert.Equal("\"a,b\",\"say \"\"hi\"\"\",yes,,170.1235\n", CsvTable.Line(new object[] { "a,b", "say \"hi\"", true, null, 170.12345 }));
            string path = Path.Combine(Path.GetTempPath(), "data_book_test", "t.csv");
            using (var table = new CsvTable(path, new[] { "a", "b" }))
            {
                table.Add(new object[] { 1, "x" });
                Assert.Equal(1, table.Rows);
            }
            Assert.Equal("a,b\n1,x\n", File.ReadAllText(path));
        }

        private const string Probe = @"C:\repo\dotnet\SiteAdaptation.C3DProbe\Civil\bin\Release\net8.0-windows\SiteAdaptation.C3DProbe.dll";

        [Fact]
        public void R161_load_once_only_for_the_security_dialog_that_names_the_probe_path()
        {
            var answer = CivilDialogs.Decide("Security - Unsigned Executable File", new List<string> { "File: " + Probe }, new List<string> { "&Always Load", "&Load Once", "&Do Not Load" }, Probe);
            Assert.Equal(DialogAction.LoadOnce, answer.Action);
            Assert.Equal("Load Once", answer.Button);
        }

        [Fact]
        public void R240_annotation_scale_dialog_gets_no_and_a_purge_button_is_never_clicked()
        {
            var scale = CivilDialogs.Decide("Annotation Scale - Large Number of Scales", new List<string> { "Annotation Scale - Large Number of Scales" }, new List<string> { "Yes", "No", "Close" }, Probe);
            Assert.Equal(DialogAction.Safe, scale.Action);
            Assert.Equal("No", scale.Button);
            var purge = CivilDialogs.Decide("Annotation Scale", new List<string> { "Purge?" }, new List<string> { "Purge and Continue", "Close" }, Probe);
            Assert.Equal("Close", purge.Button);
            var only = CivilDialogs.Decide("Scales", new List<string> { "x" }, new List<string> { "Reset", "Purge all" }, Probe);
            Assert.Equal(DialogAction.Leave, only.Action);
            var other = CivilDialogs.Decide("Security - Unsigned Executable File", new List<string> { @"File: C:\other\RcrcGreen.dll" }, new List<string> { "Always Load", "Load Once", "Do Not Load" }, Probe);
            Assert.Equal("Do Not Load", other.Button);
        }

        [Fact]
        public void R197_own_only_clicks_the_probe_dialog_and_leaves_every_other_dialog()
        {
            var own = CivilDialogs.Decide("Security - Unsigned Executable File", new List<string> { "File: " + Probe }, new List<string> { "Always Load", "Load Once", "Do Not Load" }, Probe, true);
            Assert.Equal(DialogAction.LoadOnce, own.Action);
            var other = CivilDialogs.Decide("Security - Unsigned Executable File", new List<string> { @"File: C:\other\Z4.dll" }, new List<string> { "Always Load", "Load Once", "Do Not Load" }, Probe, true);
            Assert.Equal(DialogAction.Leave, other.Action);
            Assert.Null(other.Button);
            var scale = CivilDialogs.Decide("Annotation Scale", new List<string> { "Annotation scale changed" }, new List<string> { "Yes", "No" }, Probe, true);
            Assert.Equal(DialogAction.Leave, scale.Action);
            Assert.Null(scale.Button);
        }

        [Fact]
        public void R161_do_not_load_for_another_file_even_of_the_same_name()
        {
            var answer = CivilDialogs.Decide("Security - Unsigned Executable File", new List<string> { @"File: C:\other\SiteAdaptation.C3DProbe.dll" }, new List<string> { "Always Load", "Load Once", "Do Not Load" }, Probe);
            Assert.Equal(DialogAction.DoNotLoad, answer.Action);
            Assert.Equal("Do Not Load", answer.Button);
        }

        [Fact]
        public void R161_load_once_when_name_and_location_lines_join_to_the_probe_path()
        {
            string shown = "Name:                SiteAdaptation.C3DProbe.dll\r\nPublisher:           Unknown Publisher\r\nLocation:            " + Path.GetDirectoryName(Probe) + "\r\nIssuer:                 None";
            var answer = CivilDialogs.Decide("Security - Unsigned Executable File", new List<string> { "MainInstructionIcon", shown }, new List<string> { "Always Load", "Load Once", "Do Not Load" }, Probe);
            Assert.Equal(DialogAction.LoadOnce, answer.Action);
        }

        [Fact]
        public void R161_do_not_load_when_the_location_line_is_another_folder()
        {
            foreach (string folder in new[] { @"C:\other", Path.GetDirectoryName(Probe) + @"\sub", @"C:\repo" })
            {
                string shown = "Name:                SiteAdaptation.C3DProbe.dll\r\nLocation:            " + folder + "\r\n";
                var answer = CivilDialogs.Decide("Security - Unsigned Executable File", new List<string> { shown }, new List<string> { "Always Load", "Load Once", "Do Not Load" }, Probe);
                Assert.Equal(DialogAction.DoNotLoad, answer.Action);
            }
        }

        [Fact]
        public void R161_other_dialogs_get_the_safe_button_and_never_yes_or_save()
        {
            Assert.Equal("No", CivilDialogs.Decide("AutoCAD", new List<string> { "Save changes to a.dwg?" }, new List<string> { "Yes", "No", "Cancel" }, Probe).Button);
            Assert.Equal("Cancel", CivilDialogs.Decide("Save", new List<string>(), new List<string> { "Save", "Cancel" }, Probe).Button);
            Assert.Equal("OK", CivilDialogs.Decide("Proxy Information", new List<string>(), new List<string> { "OK" }, Probe).Button);
            Assert.Equal(DialogAction.Leave, CivilDialogs.Decide("Question", new List<string>(), new List<string> { "Yes", "Save" }, Probe).Action);
        }

        [Fact]
        public void R161_a_button_matches_its_word_and_not_a_longer_word()
        {
            Assert.Equal(DialogAction.Leave, CivilDialogs.Decide("Pick", new List<string>(), new List<string> { "None", "Yesterday" }, Probe).Action);
            Assert.Equal("No thanks", CivilDialogs.Decide("Pick", new List<string>(), new List<string> { "No thanks" }, Probe).Button);
        }
    }
}
