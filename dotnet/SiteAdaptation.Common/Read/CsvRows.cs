using System;
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
        // The text of the file as Python opens it with utf-8-sig, book_rows and nbh_plots of plan_from_book.py: a byte order mark of UTF-8
        // dropped and a byte that is no UTF-8 refused, where File.ReadAllText took 0xE9 as U+FFFD (F5 of the third review of PR 55)
        public static List<Dictionary<string, string>> Read(string path)
        {
            byte[] bytes = WorkFiles.ReadBytes(path);
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                int at = FirstInvalid(bytes, start);
                throw new InvalidDataException(Path.GetFileName(path) + " is no UTF-8 text, the byte 0x" + bytes[at].ToString("X2") + " at " + at + " is none, as Python reads it with utf-8-sig (R345)");
            }
            return Parse(text);
        }

        // The place in the file of the first byte that starts no UTF-8 sequence, found by the table of well formed sequences, as the index of
        // DecoderFallbackException differs between net48 and net8.0
        private static int FirstInvalid(byte[] bytes, int at)
        {
            while (at < bytes.Length)
            {
                byte lead = bytes[at];
                int count = lead < 0x80 ? 0 : lead >= 0xC2 && lead <= 0xDF ? 1 : lead >= 0xE0 && lead <= 0xEF ? 2 : lead >= 0xF0 && lead <= 0xF4 ? 3 : -1;
                byte low = lead == 0xE0 ? (byte)0xA0 : lead == 0xF0 ? (byte)0x90 : (byte)0x80;
                byte high = lead == 0xED ? (byte)0x9F : lead == 0xF4 ? (byte)0x8F : (byte)0xBF;
                if (count < 0 || at + count >= bytes.Length)
                {
                    return at;
                }
                for (int next = 1; next <= count; next++)
                {
                    byte follow = bytes[at + next];
                    if (follow < (next == 1 ? low : 0x80) || follow > (next == 1 ? high : 0xBF))
                    {
                        return at;
                    }
                }
                at += count + 1;
            }
            return bytes.Length - 1;
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
