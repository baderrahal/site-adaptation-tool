using System;
using System.IO;
using System.Linq;
using CadReader;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R433, Get template. The template is written by the zip writer of Common and read back by the OpenXML reader of the worker, a
    // second method, so a sheet name or a header that the readers would not take fails here
    public class R433_TemplateBookTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r423_" + Guid.NewGuid().ToString("N"));

        public R433_TemplateBookTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        [Fact]
        public void R433_the_parameter_sheet_template_holds_the_eight_columns_the_sheet_reader_takes()
        {
            string path = Path.Combine(folder, "parameters.xlsx");
            TemplateBook.Write(path, TemplateBook.SheetsFor(InputNames.ParameterSheet, null));

            XlsxSheet.Sheet sheet = XlsxSheet.Read(path, TemplateBook.ParameterSheetName);

            Assert.Single(sheet.Rows);
            Assert.Equal(ParameterSheet.Columns, sheet.Rows[0].Cast<string>().ToArray());
            Assert.Empty(ParameterSheet.FromRows("parameters.xlsx", sheet.Rows).Parameters);
        }

        [Fact]
        public void R433_the_sales_template_holds_each_sheet_of_the_sales_workbook_at_its_header_row()
        {
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            var sheets = TemplateBook.SheetsFor(InputNames.Sales, config);
            string path = Path.Combine(folder, "sales.xlsx");
            TemplateBook.Write(path, sheets);

            Assert.Equal(new[] { "dlus", "sales" }.Select(key => (string)config.SelectToken("dataBook.rev9." + key + ".sheet")).ToArray(), sheets.Select(sheet => sheet.Name).ToArray());
            foreach (TemplateSheet expected in sheets)
            {
                XlsxSheet.Sheet read = XlsxSheet.Read(path, expected.Name);
                Assert.Equal(expected.HeaderRow, read.Rows.Count);
                Assert.All(read.Rows.Take(expected.HeaderRow - 1), row => Assert.All(row, Assert.Null));
                Assert.Equal(expected.Headers.ToArray(), read.Rows[expected.HeaderRow - 1].Cast<string>().ToArray());
            }
        }

        [Fact]
        public void R433_an_input_with_no_reader_sheet_has_no_template_and_a_config_with_no_sheets_is_refused()
        {
            Assert.Empty(TemplateBook.SheetsFor(InputNames.Baseplan, new JObject()));
            Assert.Throws<InvalidDataException>(() => TemplateBook.SheetsFor(InputNames.Sales, new JObject()));
            Assert.Throws<ArgumentException>(() => TemplateBook.Write(Path.Combine(folder, "none.xlsx"), new TemplateSheet[0]));
        }

        [Fact]
        public void R433_a_name_or_header_with_markup_is_kept_as_text()
        {
            string path = Path.Combine(folder, "markup.xlsx");
            TemplateBook.Write(path, new[] { new TemplateSheet("A & B", 1, new[] { "<plot>", "Side & rear" }) });

            Assert.Equal(new object[] { "<plot>", "Side & rear" }, XlsxSheet.Read(path, "A & B").Rows[0]);
        }
    }
}
