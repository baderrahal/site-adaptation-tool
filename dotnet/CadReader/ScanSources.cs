using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DocumentFormat.OpenXml.Packaging;
using SiteAdaptation.Common;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CadReader
{
    // R426. The Excel and PDF files of a scan read in place, read only and shared, never written (R1): every worksheet of a workbook with
    // every cell as its text, and every page of a PDF file as its text lines
    public static class ScanSources
    {
        // Every worksheet in the order of the workbook, each read by XlsxSheet as openpyxl reads it with read_only and data_only, so the
        // cells equal those python/scan.py takes, each cell as its text by ScanLogic.CellText
        public static ScanWorkbook Workbook(string path)
        {
            List<string> names;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SpreadsheetDocument book = SpreadsheetDocument.Open(stream, false))
            {
                WorkbookPart part = book.WorkbookPart ?? throw new InvalidDataException("the file holds no workbook");
                // openpyxl worksheets leaves the chart sheets out
                names = (part.Workbook.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>() ?? Enumerable.Empty<DocumentFormat.OpenXml.Spreadsheet.Sheet>())
                    .Where(sheet => sheet.Id?.Value != null && part.GetPartById(sheet.Id.Value) is WorksheetPart)
                    .Select(sheet => sheet.Name?.Value ?? "").ToList();
            }
            var sheets = new List<ScanSheet>();
            foreach (string name in names)
            {
                XlsxSheet.Sheet read = XlsxSheet.Read(path, name);
                sheets.Add(ScanSheet.Of(name, read.Rows.Select(row => (IList<string>)row.Select(ScanLogic.CellText).ToList())));
            }
            return new ScanWorkbook { Name = Path.GetFileName(path), Sheets = sheets };
        }

        // R351. The one character box of the location plan read, pdfminer LTChar as PdfPage builds it, taken from PdfPage so the scan and the
        // location plan never box a character two ways
        private static readonly MethodInfo DescentsOf = typeof(PdfPage).GetMethod("Descents", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("PdfPage", "Descents");

        private static readonly MethodInfo CharOf = typeof(PdfPage).GetMethod("Char", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("PdfPage", "Char");

        // Every page in order, its characters boxed as PdfPage boxes them, from the top left, then joined into lines by ScanLogic.PdfLines
        // with y from the bottom left
        public static ScanPdf Pdf(string path, double gap, double tolerance)
        {
            var pages = new List<List<string>>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (PdfDocument document = PdfDocument.Open(stream))
            {
                object descents = Call(DescentsOf, document);
                for (int number = 1; number <= document.NumberOfPages; number++)
                {
                    Page page = document.GetPage(number);
                    double height = page.Height;
                    List<PageChar> chars = page.Letters.Select(letter => (PageChar)Call(CharOf, letter, descents, height, path)).ToList();
                    pages.Add(ScanLogic.PdfLines(chars, height, gap, tolerance));
                }
            }
            return new ScanPdf { Name = Path.GetFileName(path), Pages = pages };
        }

        // The refusal a method of PdfPage throws comes out as itself, not wrapped
        private static object Call(MethodInfo method, params object[] arguments)
        {
            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        }
    }
}
