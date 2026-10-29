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
    // from its override list, the step checkRows and summary of its build, plots.csv, the rules profile and config, written with DocumentFormat.OpenXml by its
    // temporary name into the output folder of the request. The sheets are made by ReportSheets of Common and only written here, as LevelWorkbook does
    public static class ReportWorkbook
    {
        public const string PlanRole = "plan";
        public const string OverridesRole = "overrides";
        public const string CheckRowsRole = "checkRows";
        // Review 1 of PR 96: the step summary of the build, step 15, the unit of each plot of the scope that 2 Read data counts
        public const string SummaryRole = "summary";

        public static void Command(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            string planPath = run.Input(PlanRole);
            JObject plan = PyJson.Parse(WorkFiles.ReadText(planPath)) as JObject ?? throw new InvalidDataException("the plan is no JSON object (R365): " + planPath);
            string rulesPath = run.Input("rules");
            JObject rules = PyJson.Parse(WorkFiles.ReadText(rulesPath)) as JObject ?? throw new InvalidDataException("the rules profile is no JSON object (R365): " + rulesPath);
            JObject config = PyJson.Parse(WorkFiles.ReadText(run.Input("config"))) as JObject;
            string refusal = ReportSheets.Refusal(plan, rules, config, request.LevelProfile);
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
                Config = config,
                Overrides = PyJson.Parse(WorkFiles.ReadText(overridesPath)) as JObject,
                OverridesSha256 = FileHash.Sha256(overridesPath),
                CheckRows = PyJson.Parse(WorkFiles.ReadText(run.Input(CheckRowsRole))) as JObject,
                Summary = PyJson.Parse(WorkFiles.ReadText(run.Input(SummaryRole))) as JObject,
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

        // Each sheet in order: the top line in one merged bold cell and a blank row under it, then each table, its bold title, its header with the
        // editable headers in the one fill, its rows with no blank row, and a blank row before the next table. 7.6 puts the Summary table on row 3
        public static void Write(Stream stream, ReportBook book)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart part = document.AddWorkbookPart();
                part.Workbook = new Workbook();
                part.AddNewPart<WorkbookStylesPart>().Stylesheet = XlsxCells.Styles();
                Sheets sheets = part.Workbook.AppendChild(new Sheets());
                uint id = 1;
                foreach (ReportSheet sheet in book.Sheets)
                {
                    var data = new SheetData();
                    int index = 1;
                    if (sheet.TopLine != null)
                    {
                        data.Append(XlsxCells.MakeRow(index++, new object[] { sheet.TopLine }, column => XlsxCells.Bold));
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
                            data.Append(XlsxCells.MakeRow(index++, new object[] { table.Title }, column => XlsxCells.Bold));
                        }
                        data.Append(XlsxCells.MakeRow(index++, table.Header, column => table.Editable.Contains(table.Header[column]) ? XlsxCells.Editable : table.Bordered ? XlsxCells.Boxed : XlsxCells.Plain));
                        foreach (object[] values in table.Rows)
                        {
                            data.Append(XlsxCells.MakeRow(index++, values, column => table.Formats[column] != null && !(values[column] is string) && values[column] != null
                                ? (table.Bordered ? XlsxCells.BoxedThree : XlsxCells.FormatStyle[table.Formats[column]]) : table.Bordered ? XlsxCells.Boxed : XlsxCells.Plain));
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
