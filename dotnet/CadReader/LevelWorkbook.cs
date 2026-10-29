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
    // R364. The command levelExcel of the worker: one level Excel <NBH>_Levels_<profile>.xlsx of docs/design/07-mock.md 7.5 per NBH of the plan
    // as written, from its override list, the step clusters, levels and hands and plots.csv of its build, the rules profile and config, written
    // with DocumentFormat.OpenXml by its temporary name into the output folder of the request. OpenXml stays in the worker, never in Revit, so the sheets are made by
    // LevelSheets of Common and only written here
    public static class LevelWorkbook
    {
        public const string PlanRole = "plan";
        public const string OverridesRole = "overrides";
        public const string ClustersRole = "clusters";
        public const string LevelsRole = "levels";
        public const string HandsRole = "hands";

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
            string configPath = run.Input("config");
            var inputs = new LevelInputs
            {
                Plan = plan,
                // Bader, 2026-10-28: the SHA-256 of the plan bytes the Excel came from, taken again here and not from the request
                PlanSha256 = FileHash.Sha256(planPath),
                Rules = rules,
                RulesName = Path.GetFileName(rulesPath),
                RulesSha256 = FileHash.Sha256(rulesPath),
                Config = PyJson.Parse(WorkFiles.ReadText(configPath)) as JObject,
                RoadOrder = Fronts.RoadNames((JObject)rules["levelProfiles"][request.LevelProfile], ReadSettings.Load(configPath)),
                Overrides = PyJson.Parse(WorkFiles.ReadText(overridesPath)) as JObject,
                OverridesSha256 = FileHash.Sha256(overridesPath),
                Clusters = PyJson.Parse(WorkFiles.ReadText(run.Input(ClustersRole))) as JObject,
                Levels = PyJson.Parse(WorkFiles.ReadText(run.Input(LevelsRole))) as JObject,
                Hands = PyJson.Parse(WorkFiles.ReadText(run.Input(HandsRole))) as JObject,
                PlotRows = CsvRows.Read(run.Input("plotsCsv"), "locationPlanNumber", "zone", "nbhId"),
                RunId = request.RunId,
                Profile = request.LevelProfile,
                Now = DateTime.UtcNow,
            };
            LevelResult result = LevelSheets.Build(inputs);
            run.Issues.AddRange(result.Issues);
            foreach (KeyValuePair<string, int> count in result.Counts)
            {
                run.Counts[count.Key] = count.Value;
            }
            foreach (LevelBook book in result.Books)
            {
                run.WriteFile(request.OutputFolder, book.FileName, stream => Write(stream, book));
            }
        }

        // Each sheet in order: the top line over the header in one merged bold cell, the header, the editable headers of the Levels sheet in
        // the one fill, then one row per item with no blank row, each number in the format of its column
        public static void Write(Stream stream, LevelBook book)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart part = document.AddWorkbookPart();
                part.Workbook = new Workbook();
                part.AddNewPart<WorkbookStylesPart>().Stylesheet = XlsxCells.Styles();
                Sheets sheets = part.Workbook.AppendChild(new Sheets());
                uint id = 1;
                foreach (LevelSheet sheet in book.Sheets)
                {
                    var data = new SheetData();
                    int index = 1;
                    if (sheet.TopLine != null)
                    {
                        data.Append(XlsxCells.MakeRow(index++, new object[] { sheet.TopLine }, column => XlsxCells.Bold));
                    }
                    bool levels = sheet.Name == LevelExcelText.LevelsSheet;
                    data.Append(XlsxCells.MakeRow(index++, sheet.Header, column => levels && LevelExcelText.Editable.Contains(sheet.Header[column]) ? XlsxCells.Editable : XlsxCells.Plain));
                    foreach (object[] values in sheet.Rows)
                    {
                        data.Append(XlsxCells.MakeRow(index++, values, column => sheet.Formats[column] != null && !(values[column] is string) ? XlsxCells.FormatStyle[sheet.Formats[column]] : XlsxCells.Plain));
                    }
                    var worksheet = new Worksheet(data);
                    if (sheet.TopLine != null)
                    {
                        worksheet.Append(new MergeCells(new MergeCell { Reference = "A1:" + XlsxSheet.ColumnLetters(sheet.Header.Length) + "1" }) { Count = 1 });
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
