using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Update;

namespace SiteAdaptation.AddIn.Update
{
    // R402, 5f, 13-workflow-changes.md part Update from report. The body of the button as a body of the flow, with no Revit type. The preview
    // reads the config, the rules profile and the override list, the window is the confirm and asks the worker for the list of the Excel
    // files picked, and Apply of a real run writes the override list text of UpdateFromReport to the override list of the config by its
    // temporary name, moved onto the name after the commit. A dry run of the window and Cancel write nothing. No model element changes: the
    // buttons update the plots Apply names, and the transaction of the flow holds nothing of this run
    public sealed class UpdateFromReportRun
    {
        // The file of the work folder that names the plots the last Apply changed, so the buttons can update those plots only
        public const string AppliedName = "update_from_report_applied.json";

        private readonly IUpdateFromReportDialog dialog;
        private readonly Func<string, string> readText;
        private readonly Func<WorkerRequest, string, JObject> worker;
        private readonly WorkerDocument document;

        private string configPath;
        private string rulesPath;
        private JObject rules;
        private string profile;
        private string overridesPath;
        private JObject overrides;
        private JObject plan;
        private int reads;
        private UpdateFromReportAnswer answer;
        private UpdateApplied applied;
        private string written;

        // worker takes the request and the work folder and gives back the JSON the worker command wrote
        public UpdateFromReportRun(IUpdateFromReportDialog dialog, Func<string, string> readText, Func<WorkerRequest, string, JObject> worker, WorkerDocument document)
        {
            this.dialog = dialog;
            this.readText = readText;
            this.worker = worker;
            this.document = document;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Confirm = Confirm, Apply = Apply, ReadBack = ReadBack, Done = Done };
        }

        // The override list of the project lies beside the config, config/overrides with the file name of the rules profile
        public static string OverridesPath(string configPath, string rulesPath)
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath)), "overrides", Path.GetFileName(rulesPath));
        }

        private RunSummary Preview(RunContext context)
        {
            RunInput settings = context.Inputs.FirstOrDefault(input => input.Name == PaneText.Settings);
            if (settings == null || string.IsNullOrEmpty(settings.Path))
            {
                throw new InvalidOperationException("the run names no " + PaneText.Settings + " file");
            }
            configPath = settings.Path;
            JObject config = Json(configPath, "the settings file");
            string name = config["profile"]?.Type == JTokenType.String ? (string)config["profile"] : throw new InvalidDataException("config names no rules profile, profile (R277)");
            rulesPath = Path.IsPathRooted(name) ? name : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configPath))), name);
            rules = Json(rulesPath, "the rules profile");
            profile = rules["levelProfiles"]?["default"]?.Type == JTokenType.String ? (string)rules["levelProfiles"]["default"] : throw new InvalidDataException("the rules profile names no levelProfiles.default (R277)");
            overridesPath = OverridesPath(configPath, rulesPath);
            overrides = Json(overridesPath, "the override list");
            return new RunSummary(null, null);
        }

        private bool Confirm(RunContext context, RunSummary summary)
        {
            if (dialog == null)
            {
                throw new InvalidOperationException("the Update from report window is not set");
            }
            var callbacks = new UpdateFromReportCallbacks
            {
                Read = files => Read(context, files),
                Preview = ticked => UpdateFromReport.Apply(ticked, plan, overrides, rules, profile, context.RunId),
            };
            answer = dialog.Show(callbacks);
            return answer != null && answer.Apply && answer.Ticked.Count > 0;
        }

        private UpdateList Read(RunContext context, IReadOnlyList<string> files)
        {
            string refusal;
            string nbh = UpdateWorkerRequest.Nbh(files, out refusal);
            if (refusal != null)
            {
                throw new InvalidOperationException(refusal);
            }
            string planPath = UpdateWorkerRequest.PlanPath(context.WorkFolder, nbh);
            if (!File.Exists(planPath))
            {
                throw new InvalidOperationException(TextTemplate.Fill(UpdateFromReportText.NoPlan, Path.GetFileName(planPath), nbh));
            }
            string runId = context.RunId + "-r" + (++reads).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string output = Path.Combine(context.WorkFolder, "update_" + runId);
            WorkerRequest request = UpdateWorkerRequest.Make(runId, document, configPath, rulesPath, planPath, overridesPath, profile, nbh, files, output);
            JObject json = worker(request, context.WorkFolder);
            plan = Json(planPath, "the plan");
            return UpdateListJson.Read(json);
        }

        private IReadOnlyList<string> Apply(RunContext context)
        {
            applied = UpdateFromReport.Apply(answer.Ticked, plan, overrides, rules, profile, context.RunId);
            foreach (KeyValuePair<UpdateChange, string> note in answer.Notes)
            {
                string field = UpdateFromReport.OverrideField(note.Key.Field);
                JObject entry = applied.Entries.FirstOrDefault(item => (string)item["plot"] == note.Key.Plot && (string)item["field"] == field);
                if (entry != null)
                {
                    entry["reason"] = (string)entry["reason"] + ". " + note.Value;
                }
            }
            written = context.Output(overridesPath);
            File.WriteAllText(written, UpdateFromReport.ListText(applied.List), new UTF8Encoding(false));
            var record = new JObject
            {
                ["runId"] = context.RunId, ["saved"] = applied.Entries.Count, ["replaced"] = applied.Replaced.Count, ["plots"] = new JArray(applied.Plots), ["notApplied"] = new JArray(applied.NotApplied),
            };
            File.WriteAllText(context.Output(Path.Combine(context.WorkFolder, AppliedName)), record.ToString(), new UTF8Encoding(false));
            return applied.NotApplied;
        }

        // What Apply saved and each ticked change it could not save, told once the run committed so the team sees it
        private string Done(RunContext context)
        {
            var lines = new List<string> { TextTemplate.Fill(UpdateFromReportText.Saved, applied.Entries.Count, string.Join(", ", applied.Plots)) };
            if (applied.NotApplied.Count > 0)
            {
                lines.Add(TextTemplate.Fill(UpdateFromReportText.NotSaved, applied.NotApplied.Count));
                lines.AddRange(applied.NotApplied);
            }
            return string.Join("\n", lines);
        }

        // The file as it lies under its temporary name holds the entries made, read again by the parser of the worker. WorkFiles.ReadText refuses
        // a temporary name for any input, this file is our own output
        private string ReadBack(RunContext context)
        {
            JToken back = PyJson.Parse(File.ReadAllText(written, new UTF8Encoding(false)));
            return JToken.DeepEquals(back, applied.List) ? null : "the override list written does not hold the entries Apply made";
        }

        private JObject Json(string path, string what)
        {
            return PyJson.Parse(readText(path)) as JObject ?? throw new InvalidDataException(what + " is no JSON object: " + path);
        }
    }
}
