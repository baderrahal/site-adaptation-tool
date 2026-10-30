using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Update;

namespace CadReader
{
    // R402. The command updateFromReport of the worker: the level Excel and the report Excel of one NBH, as many of the two as the request
    // names, read back by UpdateWorkbook and compared with the plan of the model by UpdateFromReport of Common, the list given back as
    // update_from_report.json. OpenXml stays in the worker, never in Revit (R364), and the worker writes nothing but that list, the Excel
    // files are opened read only and shared. A refusal of the Excel, another build, is part of the list and not a refusal of the worker
    public static class UpdateFromReportWorker
    {
        public const string PlanRole = "plan";
        public const string OverridesRole = "overrides";
        public const string RulesRole = "rules";
        public const string LevelRole = "levelExcel";
        public const string ReportRole = "reportExcel";
        public const string OutputName = "update_from_report.json";

        // The add-in holds no OpenXml, so it can not tell a level Excel from a report Excel and names the files excel1, excel2. The sheets
        // each holds say which it is
        private static bool IsExcelRole(string role)
        {
            return role == LevelRole || role == ReportRole || (role != null && role.StartsWith(UpdateWorkerRequest.ExcelRolePrefix, System.StringComparison.Ordinal));
        }

        public static void Command(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            List<string> roles = (request.Inputs ?? new List<WorkerFile>()).Select(input => input.Role).Where(IsExcelRole).ToList();
            if (roles.Count == 0)
            {
                throw run.Refusal("the request names no level Excel or report Excel to read (R402)");
            }
            string planPath = run.Input(PlanRole);
            JObject plan = PyJson.Parse(WorkFiles.ReadText(planPath)) as JObject ?? throw new InvalidDataException("the plan is no JSON object (R402): " + planPath);
            JObject rules = PyJson.Parse(WorkFiles.ReadText(run.Input(RulesRole))) as JObject ?? throw new InvalidDataException("the rules profile is no JSON object (R402)");
            // The override list, read only, so Compare marks a change of a plot Apply can not save before the window offers it
            JObject overrides = PyJson.Parse(WorkFiles.ReadText(run.Input(OverridesRole))) as JObject ?? throw new InvalidDataException("the override list is no JSON object (R402)");
            var books = new List<UpdateBook>();
            foreach (string role in roles)
            {
                books.Add(UpdateWorkbook.Read(run.Input(role)));
            }
            UpdateList list = UpdateFromReport.Compare(books, plan, FileHash.Sha256(planPath), rules, request.LevelProfile, overrides);
            run.Counts["changes"] = list.Changes.Count;
            run.Counts["ignored"] = list.Ignored.Count;
            run.WriteJson(request.OutputFolder, OutputName, UpdateFromReport.ResultJson(list, null));
        }
    }
}
