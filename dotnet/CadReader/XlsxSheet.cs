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
    // shared or inline string with the text of its runs, a number of a date style as a date by the 1900 or 1904 epoch of the workbook,
    // a cell of type d by its ISO 8601 text. A value past the stated dimension is refused, where openpyxl leaves it out. The file is
    // opened read only and shared, never written (R1)
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

        // openpyxl ISO_REGEX and ISO_DURATION, matched from the start of the text as re.match does, \d every decimal digit as in Python
        private static readonly Regex Iso = new Regex(@"^(?<date>(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}))?T?(?<time>(?<hour>\d{2}):(?<minute>\d{2})(:(?<second>\d{2})(?<microsecond>\.\d{1,3})?)?)?Z?");
        private static readonly Regex IsoDuration = new Regex(@"^PT((?<hours>\d+)H)?((?<minutes>\d+)M)?((?<seconds>\d+(\.\d{1,3})?)S)?");

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
                return Rows(sheetName, worksheet, strings, dates, durations, date1904);
            }
        }

        // R351, R9. The name of the sheet excel_reader.find_sheet takes: the first whose name stripped of blanks equals the one wanted
        // without case, refused with the sheets the workbook holds when none does
        public static string SheetName(string path, string wanted)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SpreadsheetDocument book = SpreadsheetDocument.Open(stream, false))
            {
                List<string> names = book.WorkbookPart?.Workbook.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Select(item => item.Name?.Value ?? "").ToList() ?? new List<string>();
                string key = SheetRows.PyStrip(wanted ?? "").ToLowerInvariant();
                string found = names.FirstOrDefault(name => SheetRows.PyStrip(name).ToLowerInvariant() == key);
                if (found == null)
                {
                    throw new InvalidDataException("Sheet '" + wanted + "' not found. Sheets present: " + string.Join(", ", names));
                }
                return found;
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

        // R350. Every row up to the stated dimension as openpyxl gives it, and the rows past it read on: a cell with a value there, below
        // the last row or right of the last column the dimension states, is refused with its reference, since openpyxl leaves it out with
        // no word and a dimension out of date gave 0 rows on both sides (the review of PR 53). A row of no value past it is no refusal
        private static Sheet Rows(string name, WorksheetPart worksheet, List<string> strings, HashSet<int> dates, HashSet<int> durations, bool date1904)
        {
            var sheet = new Sheet { Rows = new List<object[]>() };
            int? maxColumn = null, maxRow = null;
            string dimension = null;
            bool dataSeen = false, dimensionRead = false;
            int counter = 1, index = 1, rowCounter = 0;
            object[] empty = new object[0];
            bool past = false;
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
                        dimension = ((SheetDimension)reader.LoadCurrentElement()).Reference?.Value;
                        int?[] bounds = Boundaries(dimension);
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
                        past = true;
                        Past(name, dimension, row, index, null, strings, dates, durations, date1904);
                        continue;
                    }
                    for (; counter < index; counter++)
                    {
                        sheet.Rows.Add((object[])empty.Clone());
                    }
                    if (counter <= index)
                    {
                        sheet.Rows.Add(Cells(name, dimension, row, index, strings, dates, durations, date1904, maxColumn, sheet));
                        counter++;
                    }
                }
            }
            if (past && maxRow.HasValue && maxRow.Value < index)
            {
                for (; counter <= maxRow.Value; counter++)
                {
                    sheet.Rows.Add((object[])empty.Clone());
                }
            }
            return sheet;
        }

        // A cell with a value past the stated dimension, right of the column given or anywhere in a row below it when none, refused with
        // its reference and the dimension
        private static void Past(string name, string dimension, Row row, int index, int? maxColumn, List<string> strings, HashSet<int> dates, HashSet<int> durations, bool date1904)
        {
            var counts = new Sheet();
            int columnCounter = 0;
            foreach (Cell cell in row.Elements<Cell>())
            {
                string reference = cell.CellReference?.Value;
                columnCounter = reference != null ? Column(new string(reference.TakeWhile(char.IsLetter).ToArray())) : columnCounter + 1;
                if ((!maxColumn.HasValue || columnCounter > maxColumn.Value) && Value(cell, strings, dates, durations, date1904, counts) != null)
                {
                    throw new InvalidDataException("the sheet " + name + " holds a value in " + (reference ?? ColumnLetters(columnCounter) + index.ToString(CultureInfo.InvariantCulture))
                        + " past its stated dimension " + dimension + ", which openpyxl and this reader leave out, so the dimension is out of date: save the workbook again in Excel (R350)");
                }
            }
        }

        private static string ColumnLetters(int column)
        {
            string letters = "";
            for (; column > 0; column = (column - 1) / 26)
            {
                letters = (char)('A' + (column - 1) % 26) + letters;
            }
            return letters;
        }

        private static object[] Cells(string name, string dimension, Row row, int index, List<string> strings, HashSet<int> dates, HashSet<int> durations, bool date1904, int? maxColumn, Sheet sheet)
        {
            if (maxColumn.HasValue)
            {
                Past(name, dimension, row, index, maxColumn, strings, dates, durations, date1904);
            }
            var cells = new List<KeyValuePair<int, object>>();
            int columnCounter = 0;
            foreach (Cell cell in row.Elements<Cell>())
            {
                string reference = cell.CellReference?.Value;
                columnCounter = reference != null ? Column(new string(reference.TakeWhile(char.IsLetter).ToArray())) : columnCounter + 1;
                if (maxColumn.HasValue && columnCounter > maxColumn.Value)
                {
                    continue;
                }
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
                    return FromIso8601(text);
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
                return new CellTime { Value = new DateTime(1, 1, 1).Add(diff), Kind = CellTimeKind.Time };
            }
            if (value > 0 && value < 60 && !date1904)
            {
                day += 1;
            }
            DateTime epoch = date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 30);
            return new CellTime { Value = epoch.AddDays(day).Add(diff), Kind = CellTimeKind.DateTime };
        }

        // R350. openpyxl from_ISO8601, the value of a cell of type d: a date alone, a time alone or a datetime by what the text holds,
        // the microseconds int(float(".f") * 1e6), what follows the time, a Z or an offset, read past. DateTime.Parse gave a time alone
        // the date of the day it ran and a date alone a time of 00:00:00 (the review of PR 53). A duration, which openpyxl gives as a
        // timedelta, and a text of no date or time are refused
        public static CellTime FromIso8601(string text)
        {
            Match match = Iso.Match(text ?? "");
            bool date = match.Groups["date"].Success, time = match.Groups["time"].Success;
            if (date || time)
            {
                Func<string, int> part = key => match.Groups[key].Success ? int.Parse(Ascii(match.Groups[key].Value), CultureInfo.InvariantCulture) : 0;
                long microsecond = match.Groups["microsecond"].Success ? (long)(ExactDouble.Parse(Ascii(match.Groups["microsecond"].Value)) * 1000000) : 0;
                try
                {
                    if (!date)
                    {
                        return new CellTime { Value = new DateTime(1, 1, 1, part("hour"), part("minute"), part("second")).AddTicks(microsecond * 10), Kind = CellTimeKind.Time };
                    }
                    if (!time)
                    {
                        return new CellTime { Value = new DateTime(part("year"), part("month"), part("day")), Kind = CellTimeKind.Date };
                    }
                    return new CellTime { Value = new DateTime(part("year"), part("month"), part("day"), part("hour"), part("minute"), part("second")).AddTicks(microsecond * 10), Kind = CellTimeKind.DateTime };
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new InvalidDataException("the date cell holds " + text + ", which is no date or time (R350)");
                }
            }
            Match duration = IsoDuration.Match(text ?? "");
            if (duration.Groups["hours"].Success || duration.Groups["minutes"].Success || duration.Groups["seconds"].Success)
            {
                throw new InvalidDataException("the date cell holds a duration, " + text + ", which openpyxl gives as a timedelta, a duration is not read (R350)");
            }
            throw new InvalidDataException("the date cell holds " + text + ", no ISO 8601 date or time openpyxl reads (R350)");
        }

        // The digits of a text as ASCII digits, each decimal digit of Unicode by its value as Python int() takes it
        private static string Ascii(string digits)
        {
            return new string(digits.Select(letter => char.IsDigit(letter) ? (char)('0' + (int)char.GetNumericValue(letter)) : letter).ToArray());
        }
    }
}
