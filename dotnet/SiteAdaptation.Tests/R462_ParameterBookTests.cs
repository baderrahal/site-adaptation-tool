using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CadReader;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R462. The plain reader of the parameter sheet in Common held to XlsxSheet of the worker, a second method, on the sheet of the repo
    // that openpyxl wrote with shared strings and on a sheet of inline strings that TemplateBook writes
    public class R462_ParameterBookTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r462_" + Guid.NewGuid().ToString("N"));

        public R462_ParameterBookTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        // The rows as texts, a row of no value left out and the empty cells at the end of a row cut, as the sheet reader takes them
        private static string[][] Texts(IEnumerable<object[]> rows)
        {
            return rows.Select(row => row.Select(cell => cell == null ? null : Convert.ToString(cell, CultureInfo.InvariantCulture)).ToList())
                .Where(row => row.Any(cell => !string.IsNullOrEmpty(cell)))
                .Select(row => row.Take(row.FindLastIndex(cell => !string.IsNullOrEmpty(cell)) + 1).ToArray()).ToArray();
        }

        private static string Sheet(ParameterSheet sheet)
        {
            return string.Join("\n", sheet.Parameters.Select(item => string.Join("|", item.Name, item.Guid, item.DataType, item.Group, string.Join(",", item.Categories), item.FilledBy)))
                + "\n" + string.Join(",", sheet.LeftOut);
        }

        [Fact]
        public void R462_the_sheet_of_the_repo_reads_as_the_worker_reads_it()
        {
            string path = Path.Combine(ThemeGateTests.RepoRoot(), "config", "shared_parameters.xlsx");

            string[][] plain = Texts(ParameterBook.Read(path, ParameterSheet.SheetName));

            Assert.Equal(Texts(XlsxSheet.Read(path, ParameterSheet.SheetName).Rows), plain);
            Assert.Equal(Sheet(ParameterSheet.FromRows("s", XlsxSheet.Read(path, ParameterSheet.SheetName).Rows)), Sheet(ParameterSheet.FromFile(path)));
            Assert.Equal(12, plain.Length);
            Assert.Equal(ParameterSheet.Columns, plain[0]);
            Assert.Equal("RRE_ZZ_SP_Façade-Color", plain[4][0]);
        }

        [Fact]
        public void R462_inline_strings_a_gap_row_and_a_short_row_read_as_the_worker_reads_them_and_an_open_file_still_reads()
        {
            string path = Path.Combine(folder, "p.xlsx");
            var rows = new List<IReadOnlyList<string>> { new[] { "CASE", "c0b2c4ee-e129-4a42-bcb6-225759b874eb", "Text", "Data", "RVT Links", "7", "A & B <x>", "Yes" }, new[] { "Plot" } };
            TemplateBook.Write(path, new[] { new TemplateSheet(ParameterSheet.SheetName, 2, ParameterSheet.Columns, rows) });

            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                string[][] plain = Texts(ParameterBook.Read(path, ParameterSheet.SheetName));

                Assert.Equal(Texts(XlsxSheet.Read(path, ParameterSheet.SheetName).Rows), plain);
                Assert.Equal("A & B <x>", plain[1][6]);
            }
        }

        [Fact]
        public void R462_a_sheet_name_the_workbook_lacks_a_file_not_there_and_a_file_of_no_zip_are_refused_with_the_reason()
        {
            string path = Path.Combine(folder, "p.xlsx");
            TemplateBook.Write(path, new[] { new TemplateSheet("Other", 1, ParameterSheet.Columns) });
            Assert.Equal("the parameter sheet p.xlsx has no sheet Parameters (R462)", Assert.Throws<InvalidDataException>(() => ParameterBook.Read(path, ParameterSheet.SheetName)).Message);
            Assert.Throws<FileNotFoundException>(() => ParameterBook.Read(Path.Combine(folder, "none.xlsx"), ParameterSheet.SheetName));
            string text = Path.Combine(folder, "t.xlsx");
            File.WriteAllText(text, "no zip");
            Assert.StartsWith("the parameter sheet t.xlsx is no xlsx workbook that reads (R462)", Assert.Throws<InvalidDataException>(() => ParameterBook.Read(text, ParameterSheet.SheetName)).Message);
        }
    }
}
