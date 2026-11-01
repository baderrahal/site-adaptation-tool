using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.Common.Settings
{
    public sealed class TemplateSheet
    {
        public TemplateSheet(string name, int headerRow, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows = null)
        {
            Name = name;
            HeaderRow = headerRow;
            Headers = headers;
            Rows = rows ?? new IReadOnlyList<string>[0];
        }

        public string Name { get; }

        // The row the reader takes the column names from, counted from 1
        public int HeaderRow { get; }

        public IReadOnlyList<string> Headers { get; }

        // R436, the rows under the header of an export. A template has none
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
    }

    // R433, Get template of the New project window. An empty workbook with the sheet names and the header row the readers of that
    // input take, so the team that gives the file fills it in. The parameter sheet takes the columns of ParameterSheet (R461),
    // the sheet name CHOSEN by Code. A workbook of the sales or the DLUS role takes each sheet of dataBook.rev9 of config whose
    // workbook addin.workbookRoles gives that role, its header row and its column names in the order config lists them. Every other
    // input has no template. Written as a plain zip of the xlsx parts with inline strings, since the add-in holds no xlsx library
    // and an xlsx is read by the worker (R350), the parameter sheet alone also by ParameterBook (R462). A test reads it back with the reader of the worker
    public static class TemplateBook
    {
        public const string ParameterSheetName = ParameterSheet.SheetName;
        public const string Extension = ".xlsx";

        public static IReadOnlyList<TemplateSheet> SheetsFor(string input, JObject config)
        {
            if (input == InputNames.ParameterSheet)
            {
                return new[] { new TemplateSheet(ParameterSheetName, 1, ParameterSheet.Columns) };
            }
            if (input != InputNames.Sales && input != InputNames.Dlus)
            {
                return new TemplateSheet[0];
            }
            JObject roles = config?.SelectToken("addin.workbookRoles") as JObject;
            JObject rev9 = config?.SelectToken("dataBook.rev9") as JObject;
            if (roles == null || rev9 == null)
            {
                throw new InvalidDataException("config holds no addin.workbookRoles or dataBook.rev9, the sheets of the " + input + " workbook");
            }
            var workbooks = new HashSet<string>(roles.Properties().Where(role => (string)role.Value == input).Select(role => role.Name), StringComparer.Ordinal);
            var sheets = new List<TemplateSheet>();
            foreach (JProperty entry in rev9.Properties().Where(entry => entry.Name != "workbooks"))
            {
                if (!(entry.Value is JObject sheet) || !workbooks.Contains((string)sheet["workbook"] ?? ""))
                {
                    continue;
                }
                string name = (string)sheet["sheet"];
                int? row = sheet["headerRow"]?.Type == JTokenType.Integer ? (int?)sheet["headerRow"] : null;
                JObject columns = sheet["columns"] as JObject;
                if (string.IsNullOrEmpty(name) || row == null || row < 1 || columns == null)
                {
                    throw new InvalidDataException("dataBook.rev9." + entry.Name + " of config names no sheet, headerRow from 1 or columns");
                }
                sheets.Add(new TemplateSheet(name, row.Value, columns.Properties().Select(column => (string)column.Value).ToList()));
            }
            return sheets;
        }

        public static void Write(string path, IReadOnlyList<TemplateSheet> sheets)
        {
            if (sheets == null || sheets.Count == 0)
            {
                throw new ArgumentException("a template holds one sheet or more", nameof(sheets));
            }
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                Part(zip, "[Content_Types].xml", ContentTypes(sheets.Count));
                Part(zip, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Part(zip, "xl/workbook.xml", Workbook(sheets));
                Part(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(sheets.Count));
                for (int at = 0; at < sheets.Count; at++)
                {
                    Part(zip, "xl/worksheets/sheet" + (at + 1) + ".xml", Sheet(sheets[at]));
                }
            }
        }

        private static void Part(ZipArchive zip, string name, string xml)
        {
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                writer.Write(xml);
            }
        }

        private static string ContentTypes(int count)
        {
            var text = new StringBuilder("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            text.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            text.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            text.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (int at = 1; at <= count; at++)
            {
                text.Append("<Override PartName=\"/xl/worksheets/sheet").Append(at).Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            return text.Append("</Types>").ToString();
        }

        private static string Workbook(IReadOnlyList<TemplateSheet> sheets)
        {
            var text = new StringBuilder("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            for (int at = 0; at < sheets.Count; at++)
            {
                text.Append("<sheet name=\"").Append(SecurityElement.Escape(sheets[at].Name)).Append("\" sheetId=\"").Append(at + 1).Append("\" r:id=\"rId").Append(at + 1).Append("\"/>");
            }
            return text.Append("</sheets></workbook>").ToString();
        }

        private static string WorkbookRels(int count)
        {
            var text = new StringBuilder("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int at = 1; at <= count; at++)
            {
                text.Append("<Relationship Id=\"rId").Append(at).Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet").Append(at).Append(".xml\"/>");
            }
            return text.Append("</Relationships>").ToString();
        }

        private static string Sheet(TemplateSheet sheet)
        {
            int width = Math.Max(Math.Max(sheet.Headers.Count, sheet.Rows.Count == 0 ? 0 : sheet.Rows.Max(row => row.Count)), 1);
            string last = Column(width) + (sheet.HeaderRow + sheet.Rows.Count);
            var text = new StringBuilder("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><dimension ref=\"A1:").Append(last).Append("\"/><sheetData>");
            Row(text, sheet.HeaderRow, sheet.Headers);
            for (int at = 0; at < sheet.Rows.Count; at++)
            {
                Row(text, sheet.HeaderRow + 1 + at, sheet.Rows[at]);
            }
            return text.Append("</sheetData></worksheet>").ToString();
        }

        private static void Row(StringBuilder text, int number, IReadOnlyList<string> cells)
        {
            text.Append("<row r=\"").Append(number).Append("\">");
            for (int at = 0; at < cells.Count; at++)
            {
                text.Append("<c r=\"").Append(Column(at + 1)).Append(number).Append("\" t=\"inlineStr\"><is><t>").Append(SecurityElement.Escape(cells[at])).Append("</t></is></c>");
            }
            text.Append("</row>");
        }

        private static string Column(int number)
        {
            string name = string.Empty;
            for (; number > 0; number = (number - 1) / 26)
            {
                name = (char)('A' + (number - 1) % 26) + name;
            }
            return name;
        }
    }
}
