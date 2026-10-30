using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Update;

namespace CadReader
{
    // R402. The level Excel and the report Excel read back for Update from report: the sheets Levels, Placed and Settings that the file holds,
    // each read row by row by XlsxSheet as openpyxl reads it, the file opened read only and shared and never written. OpenXml stays in the
    // worker, never in Revit, so the changes are worked out by UpdateFromReport of Common and only read here
    public static class UpdateWorkbook
    {
        public static readonly string[] Sheets = { LevelExcelText.LevelsSheet, ReportExcelText.PlacedSheet, LevelExcelText.SettingsSheet };

        public static UpdateBook Read(string path)
        {
            List<string> names;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SpreadsheetDocument book = SpreadsheetDocument.Open(stream, false))
            {
                names = book.WorkbookPart?.Workbook.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Select(item => item.Name?.Value).ToList() ?? new List<string>();
            }
            var found = new UpdateBook { FileName = Path.GetFileName(path) };
            foreach (string name in Sheets.Where(names.Contains))
            {
                found.Sheets.Add(new UpdateSheet { Name = name, Rows = XlsxSheet.Read(path, name).Rows });
            }
            return found;
        }
    }
}
