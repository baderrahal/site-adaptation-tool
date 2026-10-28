using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace CadReader
{
    // R365. The command reportExcel of the worker: one report Excel <NBH>_Report.xlsx of docs/design/07-mock.md 7.6 per NBH of the plan as written,
    // from its override list, the step checkRows of its build, plots.csv, the rules profile and config, written with DocumentFormat.OpenXml by its
    // temporary name into the output folder of the request. The sheets are made by ReportSheets of Common and only written here, as LevelWorkbook does
    public static class ReportWorkbook
    {
        public const string PlanRole = "plan";
        public const string OverridesRole = "overrides";
        public const string CheckRowsRole = "checkRows";

        public static void Command(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            string planPath = run.Input(PlanRole);
            JObject plan = PyJson.Parse(WorkFiles.ReadText(planPath)) as JObject ?? throw new InvalidDataException("the plan is no JSON object (R365): " + planPath);
            string rulesPath = run.Input("rules");
            JObject rules = PyJson.Parse(WorkFiles.ReadText(rulesPath)) as JObject ?? throw new InvalidDataException("the rules profile is no JSON object (R365): " + rulesPath);
            string refusal = ReportSheets.Refusal(plan, rules, request.LevelProfile);
            if (refusal != null)
            {
                throw run.Refusal(refusal);
            }
            string overridesPath = run.Input(OverridesRole);
            var inputs = new ReportInputs
            {
                Plan = plan,
                // Bader, 2026-10-28: the SHA-256 of the plan bytes the Excel came from, taken again here and not from the request
                PlanSha256 = FileHash.Sha256(planPath),
                Rules = rules,
                RulesSha256 = FileHash.Sha256(rulesPath),
                Config = PyJson.Parse(WorkFiles.ReadText(run.Input("config"))) as JObject,
                Overrides = PyJson.Parse(WorkFiles.ReadText(overridesPath)) as JObject,
                OverridesSha256 = FileHash.Sha256(overridesPath),
                CheckRows = PyJson.Parse(WorkFiles.ReadText(run.Input(CheckRowsRole))) as JObject,
                PlotRows = CsvRows.Read(run.Input("plotsCsv"), "locationPlanNumber", "zone", "nbhId", "typologyRev9"),
                RunId = request.RunId,
                Profile = request.LevelProfile,
                Now = DateTime.UtcNow,
            };
            ReportResult result = ReportSheets.Build(inputs);
            run.Issues.AddRange(result.Issues);
            foreach (KeyValuePair<string, int> count in result.Counts)
            {
                run.Counts[count.Key] = count.Value;
            }
            foreach (ReportBook book in result.Books)
            {
                run.WriteFile(request.OutputFolder, book.FileName, stream => Write(stream, book));
            }
        }

        // The style indexes of the stylesheet Styles writes
        private const uint Plain = 0;
        private const uint Bold = 1;
        private const uint Editable = 2;
        private const uint Boxed = 3;
        private const uint BoxedShare = 4;

        private static Stylesheet Styles()
        {
            Func<BorderPropertiesType, BorderPropertiesType> thin = side => { side.Style = BorderStyleValues.Thin; return side; };
            return new Stylesheet(
                new NumberingFormats(new NumberingFormat { NumberFormatId = 164, FormatCode = ReportExcelText.ShareFormat }) { Count = 1 },
                new Fonts(new Font(), new Font(new Bold())) { Count = 2 },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                    new Fill(new PatternFill(new ForegroundColor { Rgb = LevelExcelText.EditableFill }) { PatternType = PatternValues.Solid })) { Count = 3 },
                new Borders(new Border(), new Border(thin(new LeftBorder()), thin(new RightBorder()), thin(new TopBorder()), thin(new BottomBorder()), new DiagonalBorder())) { Count = 2 },
                new CellStyleFormats(new CellFormat()) { Count = 1 },
                new CellFormats(
                    new CellFormat(),
                    new CellFormat { FontId = 1, ApplyFont = true },
                    new CellFormat { FillId = 2, ApplyFill = true },
                    new CellFormat { BorderId = 1, ApplyBorder = true },
                    new CellFormat { BorderId = 1, ApplyBorder = true, NumberFormatId = 164, ApplyNumberFormat = true }) { Count = 5 },
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
        }

        private static Cell MakeCell(string reference, object value, uint style)
        {
            if (value is string text)
            {
                return new Cell(new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve })) { CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style };
            }
            if (value is bool flag)
            {
                return new Cell(new CellValue(flag ? "1" : "0")) { CellReference = reference, DataType = CellValues.Boolean, StyleIndex = style };
            }
            return new Cell(new CellValue(LevelWorkbook.NumberText(value))) { CellReference = reference, StyleIndex = style };
        }

        // A row of cells, an empty cell written only where its style draws a border, so a bordered range holds no gap
        private static Row MakeRow(int index, IEnumerable<object> values, Func<int, uint> style)
        {
            var row = new Row { RowIndex = (uint)index };
            int column = 0;
            foreach (object value in values)
            {
                column++;
                uint chosen = style(column - 1);
                if (value != null)
                {
                    row.Append(MakeCell(XlsxSheet.ColumnLetters(column) + index, value, chosen));
                }
                else if (chosen == Boxed || chosen == BoxedShare)
                {
                    row.Append(new Cell { CellReference = XlsxSheet.ColumnLetters(column) + index, StyleIndex = chosen });
                }
            }
            return row;
        }

        // Each sheet in order: the top line in one merged bold cell and a blank row under it, then each table, its bold title, its header with the
        // editable headers in the one fill, its rows with no blank row, and a blank row before the next table. 7.6 puts the Summary table on row 3
        public static void Write(Stream stream, ReportBook book)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart part = document.AddWorkbookPart();
                part.Workbook = new Workbook();
                part.AddNewPart<WorkbookStylesPart>().Stylesheet = Styles();
                Sheets sheets = part.Workbook.AppendChild(new Sheets());
                uint id = 1;
                foreach (ReportSheet sheet in book.Sheets)
                {
                    var data = new SheetData();
                    int index = 1;
                    if (sheet.TopLine != null)
                    {
                        data.Append(MakeRow(index++, new object[] { sheet.TopLine }, column => Bold));
                        index++;
                    }
                    bool first = true;
                    foreach (ReportTable table in sheet.Tables)
                    {
                        if (!first)
                        {
                            index++;
                        }
                        first = false;
                        if (table.Title != null)
                        {
                            data.Append(MakeRow(index++, new object[] { table.Title }, column => Bold));
                        }
                        data.Append(MakeRow(index++, table.Header, column => table.Editable.Contains(table.Header[column]) ? Editable : table.Bordered ? Boxed : Plain));
                        foreach (object[] values in table.Rows)
                        {
                            data.Append(MakeRow(index++, values, column => table.Formats[column] != null && !(values[column] is string) && values[column] != null ? BoxedShare
                                : table.Bordered ? Boxed : Plain));
                        }
                    }
                    var worksheet = new Worksheet(data);
                    if (sheet.TopLine != null)
                    {
                        int width = sheet.Tables.Count == 0 ? 1 : sheet.Tables[0].Header.Length;
                        worksheet.Append(new MergeCells(new MergeCell { Reference = "A1:" + XlsxSheet.ColumnLetters(width) + "1" }) { Count = 1 });
                    }
                    WorksheetPart page = part.AddNewPart<WorksheetPart>();
                    page.Worksheet = worksheet;
                    sheets.Append(new Sheet { Id = part.GetIdOfPart(page), SheetId = id++, Name = sheet.Name });
                }
                part.Workbook.Save();
            }
        }
    }
}
