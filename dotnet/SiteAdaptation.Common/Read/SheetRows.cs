using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R350. What a date or time cell is in Python: a datetime, a date alone or a time alone
    public enum CellTimeKind
    {
        DateTime,
        Date,
        Time,
    }

    // R350. A date or time cell as openpyxl gives it: from a number of a date style a datetime, or a time alone for a day fraction,
    // and from a cell of type d a date, a time or a datetime by what its ISO 8601 text holds
    public sealed class CellTime
    {
        public DateTime Value { get; set; }

        public CellTimeKind Kind { get; set; }

        private string Fraction => Value.Ticks % TimeSpan.TicksPerSecond == 0 ? "" : "." + (Value.Ticks % TimeSpan.TicksPerSecond / 10).ToString("000000", CultureInfo.InvariantCulture);

        private string Day => Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private string Clock => Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + Fraction;

        // Python str() of a datetime, date or time
        public string PyText => Kind == CellTimeKind.Date ? Day : Kind == CellTimeKind.Time ? Clock : Day + " " + Clock;

        // Python isoformat(), as a step file holds a date
        public string Iso => Kind == CellTimeKind.Date ? Day : Kind == CellTimeKind.Time ? Clock : Day + "T" + Clock;
    }

    // R350. The rows of a sheet as the Python readers of the DLUS and sales workbooks give them, on the cells of the xlsx reader of the
    // worker, which gives each cell as openpyxl gives it read only with values only: null, a whole number (long), a number with a point
    // or an exponent (double), a text, true or false, or a date (CellTime). rev9_workbooks.sheet_rows and plain, corner_front.sales_rows,
    // plan_from_past.plot_number and area_key.by_plot with plot_text, zone_text and nbh_text, step for step
    public static class SheetRows
    {
        private static readonly HashSet<string> NoValue = new HashSet<string> { "", "-", "0", "N/A", "NONE" };
        private static readonly Regex Digits = new Regex("[0-9]+");

        // Python str.strip(): the white space of str.isspace, which also holds the four separators 0x1C to 0x1F
        public static string PyStrip(string text)
        {
            int start = 0, end = text.Length;
            while (start < end && IsPySpace(text[start]))
            {
                start++;
            }
            while (end > start && IsPySpace(text[end - 1]))
            {
                end--;
            }
            return text.Substring(start, end - start);
        }

        private static bool IsPySpace(char letter)
        {
            return char.IsWhiteSpace(letter) || (letter >= '\u001c' && letter <= '\u001f');
        }

        // Python str() of a cell
        public static string PyText(object value)
        {
            switch (value)
            {
                case null:
                    return "None";
                case bool flag:
                    return flag ? "True" : "False";
                case double number:
                    return PyRepr(number);
                case CellTime time:
                    return time.PyText;
                case string text:
                    return text;
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        // Python repr() of a float: the shortest digits that read back as the same double (ExactDouble.Shortest), in fixed notation from
        // 1e-4 up to under 1e16
        public static string PyRepr(double value)
        {
            if (double.IsNaN(value))
            {
                return "nan";
            }
            if (double.IsInfinity(value))
            {
                return value > 0 ? "inf" : "-inf";
            }
            if (value == 0)
            {
                return 1 / value < 0 ? "-0.0" : "0.0";
            }
            string sign = value < 0 ? "-" : "";
            string digits;
            int point;
            ExactDouble.Shortest(Math.Abs(value), out digits, out point);
            if (point <= -4 || point > 16)
            {
                string mantissa = digits.Length == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
                int exponent = point - 1;
                return sign + mantissa + "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
            }
            if (point <= 0)
            {
                return sign + "0." + new string('0', -point) + digits;
            }
            if (point >= digits.Length)
            {
                return sign + digits + new string('0', point - digits.Length) + ".0";
            }
            return sign + digits.Substring(0, point) + "." + digits.Substring(point);
        }

        // Python round(value, decimals) on a float: the exact binary value rounded half to even at the decimals, read back as the
        // nearest double
        public static double PyRound(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value == 0)
            {
                return value;
            }
            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            long mantissa = exponent == 0 ? fraction : fraction | (1L << 52);
            int power = (exponent == 0 ? 1 : exponent) - 1075;
            if (power >= 0)
            {
                return value;
            }
            BigInteger numerator = new BigInteger(mantissa) * BigInteger.Pow(10, decimals);
            BigInteger denominator = BigInteger.One << -power;
            BigInteger remainder;
            BigInteger quotient = BigInteger.DivRem(numerator, denominator, out remainder);
            int half = (remainder * 2).CompareTo(denominator);
            if (half > 0 || (half == 0 && !quotient.IsEven))
            {
                quotient += 1;
            }
            double result = quotient < (BigInteger.One << 53)
                ? (double)quotient / Math.Pow(10, decimals)
                : ExactDouble.Parse(quotient.ToString(CultureInfo.InvariantCulture) + "E-" + decimals.ToString(CultureInfo.InvariantCulture));
            return negative ? -result : result;
        }

        // rev9_workbooks.plain: a text stripped, an empty text no value, a float rounded to 6 decimals by Python round
        public static object Plain(object value)
        {
            if (value is string text)
            {
                string stripped = PyStrip(text);
                return stripped.Length == 0 ? null : stripped;
            }
            if (value is double number)
            {
                return PyRound(number, 6);
            }
            return value;
        }

        private static bool IsNumber(object value)
        {
            return value is long || value is BigInteger || (value is double number && !double.IsNaN(number) && !double.IsInfinity(number));
        }

        private static string Whole(double number)
        {
            return new BigInteger(Math.Truncate(number)).ToString(CultureInfo.InvariantCulture);
        }

        // plan_from_past.plot_number: a whole number held as a number or as ASCII digits, as its text with no leading zero, else null
        public static string PlotNumber(object value)
        {
            if (IsNumber(value))
            {
                if (value is double number)
                {
                    return Math.Floor(number) == number ? Whole(number) : null;
                }
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            string text = value == null ? "" : PyStrip(PyText(value));
            if (text.Length == 0 || text.Any(letter => letter < '0' || letter > '9'))
            {
                return null;
            }
            string whole = text.TrimStart('0');
            return whole.Length == 0 ? "0" : whole;
        }

        // area_key.plot_text: a float that is whole as a whole number, every value as its text stripped, no value as an empty text
        public static string PlotText(object value)
        {
            if (value is double number && !double.IsNaN(number) && !double.IsInfinity(number) && Math.Floor(number) == number)
            {
                return Whole(number);
            }
            return value == null ? "" : PyStrip(PyText(value));
        }

        // area_key.zone_text: Zone 4, 4 and 4.0 are all Zone 4, a dash, a zero, N/A, none or nothing is no zone
        public static string ZoneText(object value)
        {
            string text = PlotText(value);
            if (NoValue.Contains(text.ToUpperInvariant()))
            {
                return null;
            }
            Match digits = Digits.Match(text);
            return digits.Success ? "Zone " + BigInteger.Parse(digits.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : text;
        }

        // area_key.nbh_text: in upper case, so 01a and 01A are one key
        public static string NbhText(object value)
        {
            string text = PlotText(value).ToUpperInvariant();
            return NoValue.Contains(text) ? null : text;
        }

        public sealed class AreaKey
        {
            public List<string> Order { get; } = new List<string>();
            public Dictionary<string, KeyValuePair<string, string>> Values { get; } = new Dictionary<string, KeyValuePair<string, string>>();
            public HashSet<string> TwoRows { get; } = new HashSet<string>();
        }

        // area_key.by_plot: plot number to zone and neighbourhood in the order first met, a row with neither left out, a row whose plot is
        // no number of ASCII digits left out, and the numbers with two rows that differ. The cells are those of the sheet, so a date takes
        // the text Python str() gives it
        public static AreaKey ByPlot(IEnumerable<Dictionary<string, object>> rows)
        {
            var found = new AreaKey();
            foreach (Dictionary<string, object> row in rows)
            {
                string plot = PlotText(Of(row, "plot"));
                var value = new KeyValuePair<string, string>(ZoneText(Of(row, "zone")), NbhText(Of(row, "nbh")));
                if (!IsAsciiDigits(plot) || (value.Key == null && value.Value == null))
                {
                    continue;
                }
                KeyValuePair<string, string> earlier;
                if (found.Values.TryGetValue(plot, out earlier))
                {
                    if (earlier.Key != value.Key || earlier.Value != value.Value)
                    {
                        found.TwoRows.Add(plot);
                    }
                }
                else
                {
                    found.Order.Add(plot);
                }
                found.Values[plot] = value;
            }
            return found;
        }

        // R350. A text of one ASCII digit or more. Python \d and isdigit hold every decimal digit of Unicode and net48 knows fewer of them
        // than Python 3.12, so both sides take the ten ASCII digits alone
        public static bool IsAsciiDigits(string text)
        {
            return !string.IsNullOrEmpty(text) && text.All(letter => letter >= '0' && letter <= '9');
        }

        private static object Of(Dictionary<string, object> row, string key)
        {
            object value;
            return row.TryGetValue(key, out value) ? value : null;
        }

        // rev9_workbooks.sheet_rows: from the row under headerRow, each row as the config names of its columns with plain values in the
        // order of the columns, a row with no plot value left out. A column the header does not hold is refused with every one named
        public static List<Dictionary<string, object>> Rows(string sheet, IEnumerable<object[]> rows, int headerRow, IReadOnlyList<KeyValuePair<string, string>> columns)
        {
            var found = new List<Dictionary<string, object>>();
            Dictionary<string, int> at = null;
            int index = 0;
            foreach (object[] row in rows)
            {
                index++;
                if (index < headerRow)
                {
                    continue;
                }
                if (index == headerRow)
                {
                    List<string> header = row.Select(cell => cell == null ? "" : PyStrip(PyText(cell))).ToList();
                    List<string> missing = columns.Select(column => column.Value).Where(column => !header.Contains(column)).ToList();
                    if (missing.Count > 0)
                    {
                        throw new System.IO.InvalidDataException(sheet + " has no column " + string.Join(", ", missing));
                    }
                    at = columns.ToDictionary(column => column.Key, column => header.IndexOf(column.Value));
                    continue;
                }
                var values = new Dictionary<string, object>();
                foreach (KeyValuePair<string, string> column in columns)
                {
                    int place = at[column.Key];
                    values[column.Key] = place < row.Length ? Plain(row[place]) : null;
                }
                if (Of(values, "plot") == null)
                {
                    continue;
                }
                found.Add(values);
            }
            return found;
        }

        // A row of the sheet as JSON in the order of its columns, a date as its ISO text
        public static JObject ToJson(Dictionary<string, object> row)
        {
            var found = new JObject();
            foreach (KeyValuePair<string, object> cell in row)
            {
                found[cell.Key] = Token(cell.Value);
            }
            return found;
        }

        // corner_front.sales_rows: the first row is the header, each later row the cells by header, a later cell of one header over an
        // earlier one, keyed by plot_number of its plot column, a later row of one plot over an earlier one. A header cell of no value is
        // the key null, as json writes the key None
        public static JObject SalesRows(IReadOnlyList<object[]> rows, string plotColumn)
        {
            if (rows.Count == 0)
            {
                throw new System.IO.InvalidDataException("the sales sheet holds no row, its first row is the header");
            }
            List<string> header = rows[0].Select(cell => cell == null ? null : PyStrip(PyText(cell))).ToList();
            var found = new JObject();
            for (int index = 1; index < rows.Count; index++)
            {
                object[] row = rows[index];
                var item = new JObject();
                object plot = null;
                bool held = false;
                for (int place = 0; place < header.Count && place < row.Length; place++)
                {
                    item[header[place] ?? "null"] = Token(row[place]);
                    if (header[place] == plotColumn)
                    {
                        plot = row[place];
                        held = true;
                    }
                }
                string number = PlotNumber(held ? plot : null);
                if (number != null)
                {
                    found[number] = item;
                }
            }
            return found;
        }

        // A cell as a JSON value: a date as its ISO text, as python/read_steps.py writes it
        public static JToken Token(object value)
        {
            switch (value)
            {
                case null:
                    return JValue.CreateNull();
                case CellTime time:
                    return new JValue(time.Iso);
                case BigInteger big:
                    return new JValue(big);
                default:
                    return new JValue(value);
            }
        }
    }
}
