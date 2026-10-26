using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CadReader;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R347, R350. The cells, texts and numbers of the reads of button 2 where the C# went apart from openpyxl and the Python readers
    // with no failure, and the memory of a whole read, found in the review of PR 53, on made up workbooks and values (R10), on net48
    // and net8.0. python/tests/test_read_edges.py holds the Python side of the same cases
    public sealed class ReadEdgeTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r350e_" + Guid.NewGuid().ToString("N"));

        public ReadEdgeTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        private static Cell Text(string reference, string text)
        {
            return new Cell { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) };
        }

        private static Cell Value(string reference, string value, CellValues? type = null)
        {
            var cell = new Cell { CellReference = reference, CellValue = new CellValue(value) };
            if (type.HasValue)
            {
                cell.DataType = type.Value;
            }
            return cell;
        }

        // A made up workbook of one sheet S with the dimension given and the rows given
        private string Book(string name, string dimension, params Row[] rows)
        {
            string path = Path.Combine(folder, name);
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart book = document.AddWorkbookPart();
                book.Workbook = new Workbook();
                var part = book.AddNewPart<WorksheetPart>();
                part.Worksheet = new Worksheet(new SheetDimension { Reference = dimension }, new SheetData(rows));
                book.Workbook.AppendChild(new Sheets()).Append(new Sheet { Id = book.GetIdOfPart(part), SheetId = 1, Name = "S" });
            }
            return path;
        }

        // openpyxl from_ISO8601 gives a date, a time or a datetime by what the text holds, the microseconds as int(float(.f) * 1e6), an
        // offset after the time read past. DateTime.Parse gave a time alone today's date and a date alone T00:00:00
        [Fact]
        public void R350_a_date_cell_of_type_d_reads_as_openpyxl_from_iso8601_gives_it()
        {
            string path = Book("dates.xlsx", "A1:B6",
                new Row(Text("A1", "Plot No"), Text("B1", "NBH")) { RowIndex = 1 },
                new Row(Value("A2", "101"), Value("B2", "2024-03-05", CellValues.Date)) { RowIndex = 2 },
                new Row(Value("A3", "102"), Value("B3", "12:30:00", CellValues.Date)) { RowIndex = 3 },
                new Row(Value("A4", "103"), Value("B4", "2024-03-05T07:08:09.5Z", CellValues.Date)) { RowIndex = 4 },
                new Row(Value("A5", "104"), Value("B5", "2024-03-05T07:08", CellValues.Date)) { RowIndex = 5 },
                new Row(Value("A6", "105"), Value("B6", "2024-03-05T00:00:00+03:00", CellValues.Date)) { RowIndex = 6 });
            XlsxSheet.Sheet sheet = XlsxSheet.Read(path, "S");
            string[] texts = sheet.Rows.Skip(1).Select(row => ((CellTime)row[1]).PyText).ToArray();
            Assert.Equal(new[] { "2024-03-05", "12:30:00", "2024-03-05 07:08:09.500000", "2024-03-05 07:08:00", "2024-03-05 00:00:00" }, texts);
            string[] iso = sheet.Rows.Skip(1).Select(row => ((CellTime)row[1]).Iso).ToArray();
            Assert.Equal(new[] { "2024-03-05", "12:30:00", "2024-03-05T07:08:09.500000", "2024-03-05T07:08:00", "2024-03-05T00:00:00" }, iso);
            Assert.Equal(5, sheet.DateCells);
            Assert.Equal("00:00:00.290000", XlsxSheet.FromIso8601("00:00:00.29").PyText);
            Assert.Equal("2024-03-05 12:30:00", XlsxSheet.FromIso8601("2024-03-0512:30").PyText);
            // A duration openpyxl gives as a timedelta and a text of no date are refused, never read as another value
            Assert.Contains("a duration", Assert.Throws<InvalidDataException>(() => XlsxSheet.FromIso8601("PT12H")).Message);
            Assert.Throws<InvalidDataException>(() => XlsxSheet.FromIso8601("T"));
            Assert.Throws<InvalidDataException>(() => XlsxSheet.FromIso8601("garbage"));
        }

        // area_key.by_plot and read_steps.setback_key take str() of the cell, which for a date is 2024-03-05 00:00:00. The C# rows held
        // the ISO text of a date and read it back, 2024-03-05T00:00:00
        [Fact]
        public void R350_a_date_in_a_key_column_gives_the_text_python_str_gives()
        {
            var columns = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("plot", "Plot No"), new KeyValuePair<string, string>("nbh", "NBH"), new KeyValuePair<string, string>("zone", "Zone") };
            var day = new CellTime { Value = new DateTime(2024, 3, 5), Kind = CellTimeKind.DateTime };
            var rows = SheetRows.Rows("S", new List<object[]> { new object[] { "Plot No", "NBH", "Zone" }, new object[] { 103L, day, 4L } }, 1, columns);
            SheetRows.AreaKey key = SheetRows.ByPlot(rows);
            Assert.Equal("2024-03-05 00:00:00", key.Values["103"].Value);
            Assert.Equal("2024-03-05 00:00:00", ReadSteps.SetbackKey(day));
            Assert.Equal("2024-03-05", ReadSteps.SetbackKey(new CellTime { Value = new DateTime(2024, 3, 5), Kind = CellTimeKind.Date }));
            // The step and the output still hold a date as its ISO text, as python/read_steps.py jsonable writes it
            Assert.Equal("2024-03-05T00:00:00", (string)SheetRows.ToJson(rows[0])["nbh"]);
        }

        // Python \d of str matches every decimal digit of Unicode, the C# [0-9] only ASCII, and the Unicode tables of net48 are older than
        // those of Python 3.12. Both take ASCII digits alone now, Zone ٤ stays its text and a plot of Arabic-Indic digits is no plot
        [Fact]
        public void R350_a_zone_or_plot_digit_is_an_ascii_digit_on_both_sides()
        {
            Assert.Equal("Zone \u0664", SheetRows.ZoneText("Zone \u0664"));
            Assert.Equal("Zone 4", SheetRows.ZoneText("Zone 04"));
            var columns = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("plot", "Plot No"), new KeyValuePair<string, string>("zone", "Zone") };
            var rows = SheetRows.Rows("S", new List<object[]> { new object[] { "Plot No", "Zone" }, new object[] { "\u0661\u0660\u0661", 4L }, new object[] { "101", 4L } }, 1, columns);
            Assert.Equal(new[] { "101" }, SheetRows.ByPlot(rows).Order);
        }

        // The doubles of the breaker's 199,961 whose repr net48 gave other digits, its format E16 rounding the 17 digit text again, 817
        // of them, 0 on net8.0, each with the text Python repr gives
        [Theory]
        [InlineData(4835064857805549689L, "1801514316094494.2")]
        [InlineData(4798063416020226498L, "6087316892596.439")]
        [InlineData(4710397203007304264L, "8045874.566311426")]
        [InlineData(-4394631746638881742L, "-673136162761606.2")]
        [InlineData(4696685135590086615L, "982303.5795009088")]
        [InlineData(-4665489685493885008L, "-0.0005142509778817052")]
        [InlineData(4832954052434917501L, "1273812973436447.2")]
        [InlineData(-4633849570826196094L, "-0.06741892274196284")]
        [InlineData(-4483393171699795677L, "-798184488.8435405")]
        [InlineData(-4387510341464295203L, "-2000723712327223.2")]
        [InlineData(4738977534679792874L, "678816594.2909214")]
        [InlineData(-4389760851170529695L, "-1438096285768600.2")]
        [InlineData(-5710247810770547145L, "-7.571968556055867e-74")]
        [InlineData(-9169024999139350470L, "-4.864392898254824e-305")]
        [InlineData(9107596838788332512L, "6.925920284413674e+300")]
        [InlineData(790706017582745008L, "8.375633764677203e-256")]
        [InlineData(-2060745854411667784L, "-6.871635160984614e+170")]
        [InlineData(-7266666601120500299L, "-7.284255993577017e-178")]
        [InlineData(-2658756912711649151L, "-7.263825536959451e+130")]
        [InlineData(-8478029746861438745L, "-7.800693862460423e-259")]
        [InlineData(-5682668798624284200L, "-5.406474452584441e-72")]
        [InlineData(-1747366271575789088L, "-5.741526795455604e+191")]
        [InlineData(-5037072588063150334L, "-7.801302747793051e-29")]
        [InlineData(99156138108340191L, "4.746044527019988e-302")]
        [InlineData(4503599627370496L, "2.2250738585072014e-308")]
        [InlineData(1L, "5e-324")]
        [InlineData(9218868437227405311L, "1.7976931348623157e+308")]
        public void R350_repr_of_a_double_is_the_shortest_text_that_reads_back_as_python_repr_gives(long bits, string text)
        {
            Assert.Equal(text, SheetRows.PyRepr(BitConverter.Int64BitsToDouble(bits)));
        }

        // A second method on random doubles: the text of PyRepr reads back as the same double on both runtimes, and on net8.0 its
        // digits equal those of the format R, the shortest round trip there
        [Fact]
        public void R350_repr_of_random_doubles_reads_back_as_the_same_double()
        {
            var random = new Random(53);
            var bytes = new byte[8];
            int checkedCount = 0;
            for (int draw = 0; draw < 20000; draw++)
            {
                random.NextBytes(bytes);
                double value = BitConverter.ToDouble(bytes, 0);
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    continue;
                }
                string text = SheetRows.PyRepr(value);
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(ExactDouble.Parse(text)));
#if NET8_0_OR_GREATER
                Assert.Equal(Digits(value.ToString("R", CultureInfo.InvariantCulture)), Digits(text));
#endif
                checkedCount++;
            }
            Assert.True(checkedCount > 19000);
        }

        private static string Digits(string text)
        {
            int mark = text.IndexOfAny(new[] { 'e', 'E' });
            string body = (mark < 0 ? text : text.Substring(0, mark)).Replace("-", "").Replace(".", "").TrimStart('0').TrimEnd('0');
            return body.Length == 0 ? "0" : body;
        }

        // Python float gives inf or 0.0 at once for an exponent past any double, the C# took seconds on 1e1000000 and ran until the kill
        // of the time limit on 0.5e-2147483648, whose exponent less its one decimal wrapped to a large positive power
        [Theory]
        [InlineData("1e1000000", "inf")]
        [InlineData("-1e1000000", "-inf")]
        [InlineData("0.5e-2147483648", "0")]
        [InlineData("-0.5e-2147483648", "-0")]
        [InlineData("1e99999999999999999999", "inf")]
        [InlineData("1e-99999999999999999999", "0")]
        [InlineData("0e99999999999999999999", "0")]
        [InlineData("123456789e-2147483647", "0")]
        [InlineData("1.7976931348623159e308", "inf")]
        [InlineData("2.4703282292062328e-324", "5e-324")]
        public void R350_a_number_text_of_any_exponent_reads_at_once_as_python_float_gives(string text, string expected)
        {
            var watch = Stopwatch.StartNew();
            double value = ExactDouble.Parse(text);
            Assert.True(watch.ElapsedMilliseconds < 1000, text + " took " + watch.ElapsedMilliseconds + " ms");
            string shown = double.IsPositiveInfinity(value) ? "inf" : double.IsNegativeInfinity(value) ? "-inf" : value == 0 ? (1 / value < 0 ? "-0" : "0") : SheetRows.PyRepr(value);
            Assert.Equal(expected, shown);
            Assert.Throws<FormatException>(() => ExactDouble.Parse("1e"));
            Assert.Throws<FormatException>(() => ExactDouble.Parse("1e+"));
        }

        // openpyxl reads a sheet only to the stated dimension, so a sheet whose dimension is out of date gave 0 rows on both sides and
        // the parity held at 0 items. A cell with a value past the stated dimension is refused on both sides, a row of no value is not
        [Fact]
        public void R350_a_sheet_with_a_value_past_its_stated_dimension_is_refused()
        {
            string stale = Book("stale.xlsx", "A1:B1",
                new Row(Text("A1", "Plot No"), Text("B1", "NBH")) { RowIndex = 1 },
                new Row(Value("A2", "101"), Text("B2", "012")) { RowIndex = 2 });
            Assert.Contains("past its stated dimension A1:B1", Assert.Throws<InvalidDataException>(() => XlsxSheet.Read(stale, "S")).Message);
            string wide = Book("wide.xlsx", "A1:B2",
                new Row(Text("A1", "Plot No"), Text("B1", "NBH")) { RowIndex = 1 },
                new Row(Value("A2", "101"), Text("B2", "012"), Text("C2", "x")) { RowIndex = 2 });
            Assert.Contains("C2", Assert.Throws<InvalidDataException>(() => XlsxSheet.Read(wide, "S")).Message);
            string formatted = Book("formatted.xlsx", "A1:B2",
                new Row(Text("A1", "Plot No"), Text("B1", "NBH")) { RowIndex = 1 },
                new Row(Value("A2", "101"), Text("B2", "012")) { RowIndex = 2 },
                new Row(new Cell { CellReference = "A3", StyleIndex = 0 }) { RowIndex = 3 });
            Assert.Equal(2, XlsxSheet.Read(formatted, "S").Rows.Count);
        }

        // The worker read refuses a table of no row, the area key, the DLUS setbacks or the sales rows, so a step is never written empty
        [Fact]
        public void R350_the_worker_read_refuses_a_table_of_no_row()
        {
            JObject book = (JObject)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["dataBook"];
            JObject area = (JObject)book["data5"]["areaKey"]["dlus"], sales = (JObject)book["rev9"]["sales"], setbacks = (JObject)book["rev9"]["dlus"];
            // Each sheet holds its header row of the config columns and no row under it
            Func<string, JObject[], string> headerOnly = (name, entries) =>
            {
                string path = Path.Combine(folder, name);
                using (SpreadsheetDocument document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
                {
                    WorkbookPart part = document.AddWorkbookPart();
                    part.Workbook = new Workbook();
                    Sheets sheets = part.Workbook.AppendChild(new Sheets());
                    uint id = 1;
                    foreach (JObject entry in entries)
                    {
                        uint row = (uint)entry["headerRow"];
                        string[] names = ((JObject)entry["columns"]).Properties().Select(column => (string)column.Value).ToArray();
                        Cell[] cells = names.Select((text, at) => Text(((char)('A' + at)).ToString() + row, text)).ToArray();
                        var worksheet = part.AddNewPart<WorksheetPart>();
                        worksheet.Worksheet = new Worksheet(new SheetData(new Row(cells) { RowIndex = row }));
                        sheets.Append(new Sheet { Id = part.GetIdOfPart(worksheet), SheetId = id++, Name = (string)entry["sheet"] });
                    }
                }
                return path;
            };
            string dlus = headerOnly("dlus.xlsx", new[] { area });
            string salesBook = headerOnly("sales.xlsx", new[] { sales, setbacks });
            string configPath = Path.Combine(folder, "project.json");
            File.Copy(WorkerRunTests.RepoConfig(), configPath);
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "e1", Command = "read", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { Input("config", configPath), Input("dlus", dlus), Input("sales", salesBook) },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = Path.Combine(folder, "out"), Steps = new List<string> { ReadSteps.AreaKey, ReadSteps.DlusSetbacks, ReadSteps.Sales },
                StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "e1"));
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            Assert.Contains("holds no row", answer.Message);
            Assert.False(File.Exists(Path.Combine(folder, "steps", "step_areaKey_csharp.json")));
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        // The sales rows take their workbook from dataBook.locationPlan.sales.workbook, a key of dataBook.rev9.workbooks as every other
        // workbook setting, never from the workbook of dataBook.rev9.sales
        [Fact]
        public void R350_the_sales_rows_take_the_workbook_their_own_setting_names()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal("sales", new ReadSettings(config).SalesRows().Role);
            JObject book = (JObject)config["dataBook"];
            foreach (string path in new[] { "rev9.dlus", "rev9.sales", "data5.areaKey.dlus", "data5.areaKey.sales", "locationPlan.sales" })
            {
                JToken entry = book.SelectToken(path);
                Assert.True(((JObject)book["rev9"]["workbooks"]).ContainsKey((string)entry["workbook"]), path + " names no key of dataBook.rev9.workbooks");
            }
            config["dataBook"]["locationPlan"]["sales"]["workbook"] = "tbd";
            Assert.Equal("dlus", new ReadSettings(config).SalesRows().Role);
        }

        // R347, Q083. A whole read of button 2 peaked at up to 4,008 MiB, over the 2,612 MiB of the Road Layout alone the check held per input.
        // Before the first read the free memory is held against the whole read plus the floor, before each later read against the rest
        // of it, the whole less what the worker holds, plus the floor
        [Fact]
        public void R347_q083_free_memory_is_held_against_the_whole_read_before_the_first_read_and_against_its_rest_before_each_read()
        {
            AddinConfig repo = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal(3900, repo.WorkerCommandPeakMiB("read"));
            Assert.Null(repo.WorkerCommandPeakMiB("build"));
            // A made up config of the repo config with a whole read of 3,900 MiB and the floor of 2.0 GiB
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["addin"]["workerPeakMiB"]["commands"]["read"] = 3900;
            config["siteBuild"]["minFreeGB"] = 2.0;
            JObject bad = (JObject)config.DeepClone();
            bad["addin"]["workerPeakMiB"]["commands"]["build"] = "a lot";
            string badPath = Path.Combine(folder, "bad.json");
            File.WriteAllText(badPath, bad.ToString());
            Assert.Throws<InvalidDataException>(() => AddinConfig.Load(badPath).WorkerCommandPeakMiB("build"));
            string configPath = Path.Combine(folder, "project.json");
            File.WriteAllText(configPath, config.ToString());
            string sheet = Path.Combine(folder, "dlus.xlsx");
            File.WriteAllText(sheet, "a made up workbook");
            double free = 5900, held = 50;
            bool read = false;
            var commands = new Dictionary<string, WorkerCommand>
            {
                ["read"] = run =>
                {
                    run.Input("config");
                    // Revit and the reads so far took memory, the worker holds part of it
                    free = 2500;
                    run.Input("dlus");
                    read = true;
                },
            };
            Func<string, WorkerRequest> request = id => new WorkerRequest
            {
                FormatVersion = 1, RunId = id, Command = "read", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { Input("config", configPath), Input("dlus", sheet) }, Scope = new WorkerScope { Kind = "project" },
                OutputFolder = Path.Combine(folder, "out"), TimeLimitS = 600,
            };
            // 5,900 MiB free is under the 3,900 of the whole read and the 2,048 of the floor, nothing is read
            Assert.Equal(3, WorkerRunner.Run(request("m1").Write(folder), commands, () => free, () => held));
            Assert.False(read);
            Assert.Equal("free memory 5.76 GiB, the whole read needs 3.81 GiB and the floor 2.00 GiB (Q083)", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "m1")).Message);
            // 6,000 MiB passes the start, then 2,500 MiB free with 1,000 held is under the 2,900 left of the read and the floor
            free = 6000;
            held = 1000;
            Assert.Equal(3, WorkerRunner.Run(request("m2").Write(folder), commands, () => free, () => held));
            Assert.False(read);
            Assert.Equal("free memory 2.44 GiB, the rest of the whole read needs 2.83 GiB, its peak 3.81 GiB less the 0.98 GiB the worker holds, and the floor 2.00 GiB (Q083)",
                WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "m2")).Message);
            // With 3,800 MiB held the rest is 100 MiB and the floor, 2,500 MiB free is enough
            free = 6000;
            held = 3800;
            Assert.Equal(0, WorkerRunner.Run(request("m3").Write(folder), commands, () => free, () => held));
            Assert.True(read);
        }
    }
}
