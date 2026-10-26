using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SiteAdaptation.Common;

namespace CadReader
{
    // R350. One sheet of an xlsx workbook read row by row with DocumentFormat.OpenXml as openpyxl 3.1.5 reads it with read_only and
    // data_only and gives it by iter_rows(values_only=True), so the C# rows equal the rows the Python readers take: from row 1, every
    // row up to the last one read numbered by its r, a row the file leaves out as a row of no values, each row as wide as the dimension
    // of the sheet names, the cached value of a formula, a number with a point or an exponent as a double and else a whole number, a
    // shared or inline string with the text of its runs, a number of a date style as a date by the 1900 or 1904 epoch of the workbook.
    // The file is opened read only and shared, never written (R1)
    public static class XlsxSheet
    {
        private static readonly Dictionary<int, string> Builtin = new Dictionary<int, string>
        {
            [0] = "General", [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00", [9] = "0%", [10] = "0.00%", [11] = "0.00E+00", [12] = "# ?/?", [13] = "# ??/??",
            [14] = "mm-dd-yy", [15] = "d-mmm-yy", [16] = "d-mmm", [17] = "mmm-yy", [18] = "h:mm AM/PM", [19] = "h:mm:ss AM/PM", [20] = "h:mm", [21] = "h:mm:ss",
            [22] = "m/d/yy h:mm", [37] = "#,##0_);(#,##0)", [38] = "#,##0_);[Red](#,##0)", [39] = "#,##0.00_);(#,##0.00)", [40] = "#,##0.00_);[Red](#,##0.00)",
            [45] = "mm:ss", [46] = "[h]:mm:ss", [47] = "mmss.0", [48] = "##0.0E+0", [49] = "@",
        };

        private static readonly Regex Strip = new Regex("\".*?\"|\\[(?!hh?\\]|mm?\\]|ss?\\])[^\\]]*\\]");
        private static readonly Regex DateLetter = new Regex("(?<![_\\\\])[dmhysDMHYS]");
        private static readonly Regex Duration = new Regex(@"\[hh?\](:mm(:ss(\.0*)?)?)?|\[mm?\](:ss(\.0*)?)?|\[ss?\](\.0*)?", RegexOptions.IgnoreCase);
        private static readonly Regex Range = new Regex(@"^\$?([A-Za-z]{1,3})?\$?(\d+)?(:\$?([A-Za-z]{1,3})?\$?(\d+)?)?$");

        public sealed class Sheet
        {
            public List<object[]> Rows { get; set; }
            // Cells of a duration style, which openpyxl gives as a timedelta, kept here as their number and counted
            public int DurationCells { get; set; }
            public int DateCells { get; set; }
        }

        public static Sheet Read(string path, string sheetName)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SpreadsheetDocument book = SpreadsheetDocument.Open(stream, false))
            {
                WorkbookPart part = book.WorkbookPart ?? throw new InvalidDataException(Path.GetFileName(path) + " holds no workbook");
                DocumentFormat.OpenXml.Spreadsheet.Sheet found = part.Workbook.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().FirstOrDefault(item => item.Name == sheetName)
                    ?? throw new InvalidDataException(Path.GetFileName(path) + " holds no sheet " + sheetName);
                var worksheet = (WorksheetPart)part.GetPartById(found.Id);
                List<string> strings = SharedStrings(part);
                HashSet<int> dates, durations;
                DateStyles(part, out dates, out durations);
                bool date1904 = part.Workbook.WorkbookProperties?.Date1904?.Value ?? false;
                return Rows(worksheet, strings, dates, durations, date1904);
            }
        }

        // The shared strings, each the text of its t and of the t of each run, x005F_ dropped as openpyxl drops it
        private static List<string> SharedStrings(WorkbookPart part)
        {
            var strings = new List<string>();
            SharedStringTablePart table = part.SharedStringTablePart;
            if (table == null)
            {
                return strings;
            }
            using (OpenXmlReader reader = OpenXmlReader.Create(table))
            {
                while (reader.Read())
                {
                    if (reader.ElementType == typeof(SharedStringItem) && reader.IsStartElement)
                    {
                        strings.Add(Content((SharedStringItem)reader.LoadCurrentElement()).Replace("x005F_", ""));
                    }
                }
            }
            return strings;
        }

        private static string Content(OpenXmlElement item)
        {
            string plain = item.Elements<Text>().FirstOrDefault()?.Text ?? "";
            return plain + string.Concat(item.Elements<Run>().Select(run => run.Text?.Text ?? ""));
        }

        // The cell styles whose number format is a date or a duration, by openpyxl is_date_format and is_timedelta_format
        private static void DateStyles(WorkbookPart part, out HashSet<int> dates, out HashSet<int> durations)
        {
            dates = new HashSet<int>();
            durations = new HashSet<int>();
            Stylesheet styles = part.WorkbookStylesPart?.Stylesheet;
            if (styles?.CellFormats == null)
            {
                return;
            }
            var custom = new Dictionary<int, string>();
            foreach (NumberingFormat format in styles.NumberingFormats?.Elements<NumberingFormat>() ?? Enumerable.Empty<NumberingFormat>())
            {
                custom[(int)format.NumberFormatId.Value] = format.FormatCode?.Value;
            }
            int index = 0;
            foreach (CellFormat style in styles.CellFormats.Elements<CellFormat>())
            {
                int id = (int)(style.NumberFormatId?.Value ?? 0);
                string code;
                if (!custom.TryGetValue(id, out code))
                {
                    Builtin.TryGetValue(id, out code);
                }
                if (IsDate(code))
                {
                    dates.Add(index);
                }
                if (code != null && Duration.IsMatch(code.Split(';')[0]))
                {
                    durations.Add(index);
                }
                index++;
            }
        }

        public static bool IsDate(string code)
        {
            return code != null && DateLetter.IsMatch(Strip.Replace(code.Split(';')[0], ""));
        }

        // The column number of a letter part, A 1, Z 26, AA 27
        private static int Column(string letters)
        {
            int column = 0;
            foreach (char letter in letters.ToUpperInvariant())
            {
                column = column * 26 + (letter - 'A' + 1);
            }
            return column;
        }

        // The minimum column, minimum row, maximum column and maximum row of a range text, null where it names none (range_boundaries)
        public static int?[] Boundaries(string text)
        {
            Match match = Range.Match(text ?? "");
            if (!match.Success)
            {
                throw new InvalidDataException(text + " is not a valid coordinate or range");
            }
            int? minColumn = match.Groups[1].Success ? Column(match.Groups[1].Value) : (int?)null;
            int? minRow = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : (int?)null;
            int? maxColumn = match.Groups[3].Success ? (match.Groups[4].Success ? Column(match.Groups[4].Value) : (int?)null) : minColumn;
            int? maxRow = match.Groups[3].Success ? (match.Groups[5].Success ? int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture) : (int?)null) : minRow;
            return new[] { minColumn, minRow, maxColumn, maxRow };
        }

        private static Sheet Rows(WorksheetPart worksheet, List<string> strings, HashSet<int> dates, HashSet<int> durations, bool date1904)
        {
            var sheet = new Sheet { Rows = new List<object[]>() };
            int? maxColumn = null, maxRow = null;
            bool dataSeen = false, dimensionRead = false;
            int counter = 1, index = 1, rowCounter = 0;
            object[] empty = new object[0];
            bool broke = false;
            using (OpenXmlReader reader = OpenXmlReader.Create(worksheet))
            {
                while (reader.Read())
                {
                    if (!reader.IsStartElement)
                    {
                        continue;
                    }
                    if (reader.ElementType == typeof(SheetDimension) && !dataSeen && !dimensionRead)
                    {
                        // openpyxl reads the dimension only before the sheet data
                        int?[] bounds = Boundaries(((SheetDimension)reader.LoadCurrentElement()).Reference?.Value);
                        maxColumn = bounds[2];
                        maxRow = bounds[3];
                        dimensionRead = true;
                        empty = maxColumn.HasValue ? new object[Math.Max(maxColumn.Value, 0)] : new object[0];
                        continue;
                    }
                    if (reader.ElementType == typeof(SheetData))
                    {
                        dataSeen = true;
                        continue;
                    }
                    if (reader.ElementType != typeof(Row))
                    {
                        continue;
                    }
                    var row = (Row)reader.LoadCurrentElement();
                    rowCounter = row.RowIndex != null ? (int)row.RowIndex.Value : rowCounter + 1;
                    index = rowCounter;
                    if (maxRow.HasValue && index > maxRow.Value)
                    {
                        broke = true;
                        break;
                    }
                    for (; counter < index; counter++)
                    {
                        sheet.Rows.Add((object[])empty.Clone());
                    }
                    if (counter <= index)
                    {
                        sheet.Rows.Add(Cells(row, strings, dates, durations, date1904, maxColumn, sheet));
                        counter++;
                    }
                }
            }
            if (broke && maxRow.HasValue && maxRow.Value < index)
            {
                for (; counter <= maxRow.Value; counter++)
                {
                    sheet.Rows.Add((object[])empty.Clone());
                }
            }
            return sheet;
        }

        private static object[] Cells(Row row, List<string> strings, HashSet<int> dates, HashSet<int> durations, bool date1904, int? maxColumn, Sheet sheet)
        {
            var cells = new List<KeyValuePair<int, object>>();
            int columnCounter = 0;
            foreach (Cell cell in row.Elements<Cell>())
            {
                string reference = cell.CellReference?.Value;
                columnCounter = reference != null ? Column(new string(reference.TakeWhile(char.IsLetter).ToArray())) : columnCounter + 1;
                cells.Add(new KeyValuePair<int, object>(columnCounter, Value(cell, strings, dates, durations, date1904, sheet)));
            }
            if (cells.Count == 0 && !maxColumn.HasValue)
            {
                return new object[0];
            }
            int width = maxColumn ?? cells[cells.Count - 1].Key;
            var values = new object[Math.Max(width, 0)];
            foreach (KeyValuePair<int, object> cell in cells.Where(cell => cell.Key >= 1 && cell.Key <= width))
            {
                values[cell.Key - 1] = cell.Value;
            }
            return values;
        }

        private static object Value(Cell cell, List<string> strings, HashSet<int> dates, HashSet<int> durations, bool date1904, Sheet sheet)
        {
            string type = cell.DataType?.InnerText ?? "n";
            int style = cell.StyleIndex != null ? (int)cell.StyleIndex.Value : 0;
            if (type == "inlineStr")
            {
                InlineString inline = cell.InlineString;
                return inline == null ? null : Content(inline);
            }
            string text = cell.CellValue?.Text;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            switch (type)
            {
                case "n":
                    object number = Number(text);
                    if (dates.Contains(style))
                    {
                        if (durations.Contains(style))
                        {
                            sheet.DurationCells++;
                            return number;
                        }
                        sheet.DateCells++;
                        return FromExcel(Convert.ToDouble(number, CultureInfo.InvariantCulture), date1904);
                    }
                    return number;
                case "s":
                    return strings[int.Parse(text, CultureInfo.InvariantCulture)];
                case "b":
                    return int.Parse(text, CultureInfo.InvariantCulture) != 0;
                case "d":
                    sheet.DateCells++;
                    return new CellTime { Value = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) };
                default:
                    return text;
            }
        }

        // openpyxl _cast_number: a text with a point or an exponent is a float, else a whole number
        public static object Number(string text)
        {
            if (text.Contains(".") || text.Contains("E") || text.Contains("e"))
            {
                return ExactDouble.Parse(text);
            }
            long whole;
            return long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out whole) ? whole : (object)BigInteger.Parse(text.Trim(), CultureInfo.InvariantCulture);
        }

        // openpyxl from_excel: the day and the fraction rounded to the millisecond, a fraction of a day alone a time, a serial under 60
        // one day on for the leap day 1900 never had
        public static CellTime FromExcel(double value, bool date1904)
        {
            double day = Math.Floor(value);
            double fraction = value - day;
            long milliseconds = (long)Math.Round(fraction * 86400 * 1000, MidpointRounding.ToEven);
            TimeSpan diff = TimeSpan.FromTicks(milliseconds * TimeSpan.TicksPerMillisecond);
            if (value >= 0 && value < 1 && diff.Days == 0)
            {
                return new CellTime { Value = new DateTime(1, 1, 1).Add(diff), TimeOnly = true };
            }
            if (value > 0 && value < 60 && !date1904)
            {
                day += 1;
            }
            DateTime epoch = date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 30);
            return new CellTime { Value = epoch.AddDays(day).Add(diff) };
        }
    }
}
