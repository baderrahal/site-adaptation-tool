using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // R462. The rows of one sheet of the parameter sheet, read inside Revit by button 1 and button 7, where no OpenXml is loaded (R350).
    // A plain read of the xlsx parts: the sheet by its name in workbook.xml and its relationship, each cell as text, a shared string,
    // an inline string or the text of a number or a boolean as stored, a row of no value left out, which the sheet reader skips anyway,
    // each row as wide as the widest. The file is opened read only and shared, so a sheet open in Excel still reads
    public static class ParameterBook
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Relations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelations = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static IReadOnlyList<object[]> Read(string path, string sheetName)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("the parameter sheet is not on disk (R462): " + path);
            }
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (ZipArchive zip = Open(stream, path))
                {
                    XDocument workbook = Part(zip, "xl/workbook.xml") ?? throw new InvalidDataException("the parameter sheet " + Path.GetFileName(path) + " holds no workbook (R462)");
                    XElement sheet = workbook.Descendants(Main + "sheet").FirstOrDefault(item => string.Equals((string)item.Attribute("name"), sheetName, StringComparison.Ordinal))
                        ?? throw new InvalidDataException("the parameter sheet " + Path.GetFileName(path) + " has no sheet " + sheetName + " (R462)");
                    string id = (string)sheet.Attribute(Relations + "id");
                    XElement relation = (Part(zip, "xl/_rels/workbook.xml.rels")?.Descendants(PackageRelations + "Relationship") ?? Enumerable.Empty<XElement>())
                        .FirstOrDefault(item => (string)item.Attribute("Id") == id) ?? throw new InvalidDataException("the sheet " + sheetName + " names no part (R462)");
                    string target = ((string)relation.Attribute("Target")).Replace('\\', '/');
                    string partName = target.StartsWith("/", StringComparison.Ordinal) ? target.Substring(1) : "xl/" + target;
                    XDocument cells = Part(zip, partName) ?? throw new InvalidDataException("the part " + partName + " of the sheet " + sheetName + " is missing (R462)");
                    return Rows(cells, Strings(zip));
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception error) when (error is IOException || error is System.Xml.XmlException)
            {
                throw new InvalidDataException("the parameter sheet " + Path.GetFileName(path) + " is no xlsx workbook that reads (R462): " + error.Message, error);
            }
        }

        private static ZipArchive Open(Stream stream, string path)
        {
            try
            {
                return new ZipArchive(stream, ZipArchiveMode.Read);
            }
            catch (InvalidDataException error)
            {
                throw new InvalidDataException("the parameter sheet " + Path.GetFileName(path) + " is no xlsx workbook that reads (R462): " + error.Message, error);
            }
        }

        private static XDocument Part(ZipArchive zip, string name)
        {
            ZipArchiveEntry entry = zip.GetEntry(name);
            if (entry == null)
            {
                return null;
            }
            using (Stream part = entry.Open())
            {
                return XDocument.Load(part);
            }
        }

        private static List<string> Strings(ZipArchive zip)
        {
            XDocument shared = Part(zip, "xl/sharedStrings.xml");
            return shared == null ? new List<string>() : shared.Root.Elements(Main + "si").Select(Text).ToList();
        }

        // The text of a shared or inline string, its plain t or the t of each run, phonetic runs left out
        private static string Text(XElement item)
        {
            XElement plain = item.Element(Main + "t");
            return plain != null ? plain.Value : string.Concat(item.Elements(Main + "r").Select(run => (string)run.Element(Main + "t") ?? ""));
        }

        private static List<object[]> Rows(XDocument sheet, List<string> strings)
        {
            var found = new SortedDictionary<int, Dictionary<int, string>>();
            int width = 0;
            int nextRow = 1;
            foreach (XElement row in sheet.Descendants(Main + "row"))
            {
                int number = int.TryParse((string)row.Attribute("r"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : nextRow;
                nextRow = number + 1;
                var cells = new Dictionary<int, string>();
                int nextColumn = 1;
                foreach (XElement cell in row.Elements(Main + "c"))
                {
                    int column = ColumnOf((string)cell.Attribute("r")) ?? nextColumn;
                    nextColumn = column + 1;
                    string value = Value(cell, strings);
                    if (value != null)
                    {
                        cells[column] = value;
                        width = Math.Max(width, column);
                    }
                }
                if (cells.Count > 0)
                {
                    found[number] = cells;
                }
            }
            return found.Values.Select(cells => Enumerable.Range(1, width).Select(column => cells.TryGetValue(column, out string text) ? (object)text : null).ToArray()).ToList();
        }

        private static string Value(XElement cell, List<string> strings)
        {
            string type = (string)cell.Attribute("t");
            if (type == "inlineStr")
            {
                XElement inline = cell.Element(Main + "is");
                return inline == null ? null : Text(inline);
            }
            string stored = (string)cell.Element(Main + "v");
            if (stored == null)
            {
                return null;
            }
            if (type == "s")
            {
                int at = int.Parse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture);
                return at >= 0 && at < strings.Count ? strings[at] : throw new InvalidDataException("a cell names the shared string " + stored + ", which the workbook lacks (R462)");
            }
            if (type == "b")
            {
                return stored == "1" ? "True" : "False";
            }
            return stored;
        }

        private static int? ColumnOf(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return null;
            }
            int column = 0;
            foreach (char letter in reference.TakeWhile(char.IsLetter))
            {
                column = column * 26 + (char.ToUpperInvariant(letter) - 'A' + 1);
            }
            return column == 0 ? (int?)null : column;
        }
    }
}
