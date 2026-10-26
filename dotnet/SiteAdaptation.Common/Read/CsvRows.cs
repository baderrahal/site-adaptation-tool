using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SiteAdaptation.Common
{
    // R345. The rows of a CSV file of the data book by its header, as csv.DictReader of Python reads them: a field in double quotes
    // holds commas and line breaks, a doubled quote is one quote, a line ends at a line feed or a carriage return and line feed.
    // A short row has only the fields it holds
    public static class CsvRows
    {
        public static List<Dictionary<string, string>> Read(string path)
        {
            return Parse(WorkFiles.ReadText(path));
        }

        public static List<Dictionary<string, string>> Parse(string text)
        {
            List<List<string>> records = Records(text);
            var rows = new List<Dictionary<string, string>>();
            if (records.Count == 0)
            {
                return rows;
            }
            List<string> header = records[0];
            for (int index = 1; index < records.Count; index++)
            {
                List<string> record = records[index];
                // csv.DictReader skips a line with no field at all
                if (record.Count == 1 && record[0].Length == 0)
                {
                    continue;
                }
                var row = new Dictionary<string, string>();
                for (int field = 0; field < record.Count && field < header.Count; field++)
                {
                    row[header[field]] = record[field];
                }
                rows.Add(row);
            }
            return rows;
        }

        private static List<List<string>> Records(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            bool any = false;
            for (int index = 0; index < text.Length; index++)
            {
                char letter = text[index];
                if (quoted)
                {
                    if (letter == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(letter);
                    }
                    continue;
                }
                if (letter == '"' && field.Length == 0)
                {
                    quoted = true;
                    any = true;
                }
                else if (letter == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    any = true;
                }
                else if (letter == '\n' || letter == '\r')
                {
                    if (letter == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }
                    record.Add(field.ToString());
                    records.Add(record);
                    record = new List<string>();
                    field.Clear();
                    any = false;
                }
                else
                {
                    field.Append(letter);
                    any = true;
                }
            }
            if (any || field.Length > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            if (quoted)
            {
                throw new InvalidDataException("a quoted field of the CSV text never ends");
            }
            return records;
        }
    }
}
