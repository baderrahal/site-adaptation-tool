using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SiteAdaptation.C3DProbe
{
    // One CSV table of the data book: UTF-8, one header row, a cell with a comma, a quote or a line break is quoted (R162)
    public sealed class CsvTable : IDisposable
    {
        private readonly StreamWriter writer;

        public CsvTable(string path, IEnumerable<string> header)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            writer = new StreamWriter(path, false, new UTF8Encoding(false));
            Header = header.ToList();
            writer.Write(Line(Header));
        }

        public List<string> Header { get; }
        public int Rows { get; private set; }

        public void Add(IEnumerable<object> cells)
        {
            writer.Write(Line(cells));
            Rows++;
        }

        public void Dispose()
        {
            writer.Dispose();
        }

        public static string Line(IEnumerable<object> cells)
        {
            return string.Join(",", cells.Select(Cell)) + "\n";
        }

        public static string Cell(object value)
        {
            if (value == null)
            {
                return "";
            }
            string text;
            if (value is double number)
            {
                text = double.IsNaN(number) || double.IsInfinity(number) ? "" : Round.Text(number, 4);
            }
            else if (value is bool flag)
            {
                text = flag ? "yes" : "no";
            }
            else
            {
                text = Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }
    }
}
