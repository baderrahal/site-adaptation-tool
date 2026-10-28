using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace CadReader
{
    // R364. The command levelExcel of the worker: the level Excel <NBH>_Levels_<profile>.xlsx of docs/design/07-mock.md 7.5 from the plan as
    // written, its override list, the step clusters and plots.csv of its build, the rules profile and config, written with DocumentFormat.OpenXml
    // by its temporary name into the output folder of the request. OpenXml stays in the worker, never in Revit, so the sheets are made by
    // LevelSheets of Common and only written here
    public static class LevelWorkbook
    {
        public const string PlanRole = "plan";
        public const string OverridesRole = "overrides";
        public const string ClustersRole = "clusters";

        public static void Command(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            string planPath = run.Input(PlanRole);
            JObject plan = PyJson.Parse(WorkFiles.ReadText(planPath)) as JObject ?? throw new InvalidDataException("the plan is no JSON object (R364): " + planPath);
            string rulesPath = run.Input("rules");
            JObject rules = PyJson.Parse(WorkFiles.ReadText(rulesPath)) as JObject ?? throw new InvalidDataException("the rules profile is no JSON object (R364): " + rulesPath);
            string refusal = LevelSheets.Refusal(plan, rules, request.LevelProfile);
            if (refusal != null)
            {
                throw run.Refusal(refusal);
            }
            string overridesPath = run.Input(OverridesRole);
            var inputs = new LevelInputs
            {
                Plan = plan,
                // Bader, 2026-10-28: the SHA-256 of the plan bytes the Excel came from, taken again here and not from the request
                PlanSha256 = FileHash.Sha256(planPath),
                Rules = rules,
                RulesName = Path.GetFileName(rulesPath),
                RulesSha256 = FileHash.Sha256(rulesPath),
                Config = PyJson.Parse(WorkFiles.ReadText(run.Input("config"))) as JObject,
                Overrides = PyJson.Parse(WorkFiles.ReadText(overridesPath)) as JObject,
                OverridesSha256 = FileHash.Sha256(overridesPath),
                Clusters = PyJson.Parse(WorkFiles.ReadText(run.Input(ClustersRole))) as JObject,
                PlotRows = CsvRows.Read(run.Input("plotsCsv"), "locationPlanNumber", "zone"),
                RunId = request.RunId,
                Profile = request.LevelProfile,
                Now = DateTime.UtcNow,
            };
            LevelBook book = LevelSheets.Build(inputs);
            run.Issues.AddRange(book.Issues);
            foreach (KeyValuePair<string, int> count in book.Counts)
            {
                run.Counts[count.Key] = count.Value;
            }
            run.WriteFile(request.OutputFolder, book.FileName, stream => Write(stream, book));
        }

        // The style indexes of the stylesheet Styles writes
        private const uint Plain = 0;
        private const uint Bold = 1;
        private const uint Editable = 2;
        private static readonly Dictionary<string, uint> FormatStyle = new Dictionary<string, uint>
        {
            [LevelExcelText.FourDecimals] = 3,
            [LevelExcelText.ThreeDecimals] = 4,
            [LevelExcelText.Whole] = 5,
        };

        private static Stylesheet Styles()
        {
            return new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat { NumberFormatId = 164, FormatCode = LevelExcelText.FourDecimals },
                    new NumberingFormat { NumberFormatId = 165, FormatCode = LevelExcelText.ThreeDecimals }) { Count = 2 },
                new Fonts(new Font(), new Font(new Bold())) { Count = 2 },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                    new Fill(new PatternFill(new ForegroundColor { Rgb = LevelExcelText.EditableFill }) { PatternType = PatternValues.Solid })) { Count = 3 },
                new Borders(new Border()) { Count = 1 },
                new CellStyleFormats(new CellFormat()) { Count = 1 },
                new CellFormats(
                    new CellFormat(),
                    new CellFormat { FontId = 1, ApplyFont = true },
                    new CellFormat { FillId = 2, ApplyFill = true },
                    new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 1, ApplyNumberFormat = true }) { Count = 6 },
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
        }

        public static string Letter(int column)
        {
            string text = "";
            for (; column > 0; column = (column - 1) / 26)
            {
                text = (char)('A' + (column - 1) % 26) + text;
            }
            return text;
        }

        // A number as the shortest text that reads back as the same double, as Python repr gives it, G17 where R of net48 gives another double
        private static string NumberText(object value)
        {
            if (value is long whole)
            {
                return whole.ToString(CultureInfo.InvariantCulture);
            }
            double number = (double)value;
            string text = number.ToString("R", CultureInfo.InvariantCulture);
            return ExactDouble.Parse(text) == number ? text : number.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static Cell MakeCell(string reference, object value, uint style)
        {
            if (value is string text)
            {
                return new Cell(new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve })) { CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style };
            }
            return new Cell(new CellValue(NumberText(value))) { CellReference = reference, StyleIndex = style };
        }

        private static Row MakeRow(int index, IEnumerable<object> values, Func<int, uint> style)
        {
            var row = new Row { RowIndex = (uint)index };
            int column = 0;
            foreach (object value in values)
            {
                column++;
                if (value != null)
                {
                    row.Append(MakeCell(Letter(column) + index, value, style(column - 1)));
                }
            }
            return row;
        }

        // Each sheet in order: the top line over the header in one merged bold cell, the header, the editable headers of the Levels sheet in
        // the one fill, then one row per item with no blank row, each number in the format of its column
        public static void Write(Stream stream, LevelBook book)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart part = document.AddWorkbookPart();
                part.Workbook = new Workbook();
                part.AddNewPart<WorkbookStylesPart>().Stylesheet = Styles();
                Sheets sheets = part.Workbook.AppendChild(new Sheets());
                uint id = 1;
                foreach (LevelSheet sheet in book.Sheets)
                {
                    var data = new SheetData();
                    int index = 1;
                    if (sheet.TopLine != null)
                    {
                        data.Append(MakeRow(index++, new object[] { sheet.TopLine }, column => Bold));
                    }
                    bool levels = sheet.Name == LevelExcelText.LevelsSheet;
                    data.Append(MakeRow(index++, sheet.Header, column => levels && LevelExcelText.Editable.Contains(sheet.Header[column]) ? Editable : Plain));
                    foreach (object[] values in sheet.Rows)
                    {
                        data.Append(MakeRow(index++, values, column => sheet.Formats[column] != null && !(values[column] is string) ? FormatStyle[sheet.Formats[column]] : Plain));
                    }
                    var worksheet = new Worksheet(data);
                    if (sheet.TopLine != null)
                    {
                        worksheet.Append(new MergeCells(new MergeCell { Reference = "A1:" + Letter(sheet.Header.Length) + "1" }) { Count = 1 });
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
