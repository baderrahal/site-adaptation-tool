using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Update;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Update;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R402, 5f, W8 of steps/lane-b.md. The window Update from report: its view model answers the ticks, the counts, the chips and the refusal with
    // no click, its session the four buttons, and its run writes the override list only on Apply of a real run. net48 only, the add-in is
    public sealed class UpdateFromReportWindowTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r402w_" + Guid.NewGuid().ToString("N"));
        private const string Profile = "parsons2024Rev6";

        public UpdateFromReportWindowTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }

        private static UpdateChange Change(string plot, string field, object model, object excel, bool ticked, string invalid = null, string manual = null, string breaks = null)
        {
            var change = new UpdateChange { Plot = plot, Field = field, FileName = "012_Levels_" + Profile + ".xlsx", Sheet = "Levels", Row = 3, InModel = model, InExcel = excel, Ticked = ticked, Invalid = invalid };
            change.Checks.Add(UpdateFromReport.FflOverEntry);
            if (manual != null)
            {
                change.ManualRules.Add(manual);
            }
            if (breaks != null)
            {
                change.Breaks.Add(breaks);
            }
            return change;
        }

        // Four changes: one that follows the rules, one that breaks a level rule only, one that breaks a manual rule and one that can not be applied
        private static UpdateList MadeList()
        {
            var list = new UpdateList();
            list.Changes.Add(Change("102", UpdateText.Entry, 10.2, 10.15, true));
            list.Changes.Add(Change("103", UpdateText.Steps, 2L, 3L, true, breaks: "stepsRule"));
            list.Changes.Add(Change("101", UpdateText.Ffl, 10.2, 10.25, false, breaks: "stepHeights", manual: "M08"));
            list.Changes.Add(Change("101", UpdateText.Entry, 10.0, null, false, invalid: "the cell holds no number"));
            list.Ignored.Add(new IgnoredCell { Plot = "101", Column = "SSL", FileName = "x", Sheet = "Levels", Why = "not editable" });
            return list;
        }

        [Fact]
        public void R402_the_window_shows_the_tiles_chips_and_columns_in_the_words_of_the_design_and_the_counts_of_the_list()
        {
            var model = new UpdateFromReportViewModel();
            model.Load(MadeList(), new[] { "012_Levels_" + Profile + ".xlsx" });
            Assert.Equal(new[] { "Changes found 4", "Follow the rules 1", "Break a manual rule 1", "Not editable ignored 1" }, model.Tiles.Select(tile => tile.Label + " " + tile.Count).ToArray());
            Assert.Equal(new[] { "All 4", "FFL 1", "El @ Entry 2", "Steps 1", "Villa type 0", "Mirror 0", "Breaks a rule 3" }, model.Chips.Select(chip => chip.Label + " " + chip.Count).ToArray());
            Assert.Equal(UpdateText.Columns, UpdateFromReportText.Columns);
            Assert.Equal(UpdateText.Tiles, new[] { UpdateFromReportText.ChangesFound, UpdateFromReportText.FollowTheRules, UpdateFromReportText.BreakAManualRule, UpdateFromReportText.NotEditableIgnored });
            Assert.Equal(UpdateText.Chips, UpdateFromReportText.Chips);
            Assert.Equal("Updates the levels, floors, links and parameters of the changed plots only.", UpdateFromReportText.Foot);
            Assert.Equal(new[] { "Apply", "Plot", "Field", "In model", "In Excel", "Rule check" }, UpdateFromReportText.Columns);
            // Review 5f: the rule column shows the plain name of each rule, its id is the tooltip
            Assert.Equal(new[] { "102 El @ Entry 10.2000 10.1500 follows the rules", "103 Steps 2 3 Steps by the steps rule", "101 FFL 10.200 10.250 Height in the step legend, M08", "101 El @ Entry 10.0000  the cell holds no number" },
                model.Rows.Select(row => row.Plot + " " + row.Field + " " + row.InModel + " " + row.InExcel + " " + row.RuleCheck).ToArray());
            // Breaker 2 of 5f: a row with no rule ids gives the tooltip no text, so WPF shows no empty tooltip
            Assert.Equal(new[] { "fflOverEntry", "stepsRule", "stepHeights, M08", null }, model.Rows.Select(row => row.RuleIds).ToArray());
        }

        [Fact]
        public void R402_a_change_that_breaks_a_manual_rule_starts_unticked_with_keep_the_model_and_apply_anyway_plus_a_note()
        {
            var model = new UpdateFromReportViewModel();
            model.Load(MadeList(), new[] { "x.xlsx" });
            Assert.Equal(new[] { true, true, false, false }, model.Rows.Select(row => row.Apply).ToArray());
            Assert.Equal(new[] { false, false, true, false }, model.Rows.Select(row => row.BreaksManual).ToArray());
            Assert.Equal(new[] { true, true, true, false }, model.Rows.Select(row => row.CanTick).ToArray());
            UpdateRowViewModel manual = model.Rows[2];
            Assert.True(manual.KeepModel);
            Assert.False(manual.ApplyAnyway);
            Assert.Equal("Apply 2 changes", model.ApplyLabel);
            manual.ApplyAnyway = true;
            manual.Note = "Bader agreed on site";
            Assert.True(manual.Apply);
            Assert.False(manual.KeepModel);
            Assert.Equal("Apply 3 changes", model.ApplyLabel);
            manual.KeepModel = true;
            Assert.False(manual.Apply);
            Assert.Equal("Apply 2 changes", model.ApplyLabel);
            Assert.Equal(new[] { "Keep the model", "Apply anyway", "Note" }, new[] { UpdateFromReportText.KeepModel, UpdateFromReportText.ApplyAnyway, UpdateFromReportText.Note });
        }

        [Fact]
        public void R402_a_change_that_can_not_be_applied_is_never_ticked_and_a_chip_filters_the_rows_and_keeps_the_ticks()
        {
            var model = new UpdateFromReportViewModel();
            model.Load(MadeList(), new[] { "x.xlsx" });
            model.Rows[3].Apply = true;
            Assert.False(model.Rows[3].Apply);
            model.Rows[0].Apply = false;
            Assert.Equal("Apply 1 changes", model.ApplyLabel);
            model.SelectChip(UpdateText.Entry);
            Assert.Equal(new[] { "102", "101" }, model.Rows.Select(row => row.Plot).ToArray());
            Assert.True(model.Chips.Single(chip => chip.Label == UpdateText.Entry).Selected);
            Assert.False(model.Chips.Single(chip => chip.Label == UpdateText.All).Selected);
            Assert.Equal("Apply 1 changes", model.ApplyLabel);
            model.SelectChip(UpdateText.BreaksARule);
            Assert.Equal(new[] { "103", "101", "101" }, model.Rows.Select(row => row.Plot).ToArray());
            model.SelectChip(UpdateText.All);
            Assert.False(model.Rows[0].Apply);
            Assert.Equal(4, model.Rows.Count);
            Assert.Equal(new[] { "103" }, model.TickedChanges.Select(change => change.Plot).ToArray());
        }

        [Fact]
        public void R402_a_report_of_another_build_is_refused_with_its_one_line_and_nothing_is_listed_or_applied()
        {
            var list = new UpdateList { Refusal = "012_Levels_x.xlsx is of another build, its Plan build id is a, the plan of the model b (R402)" };
            var model = new UpdateFromReportViewModel();
            model.Load(list, new[] { "012_Levels_x.xlsx" });
            Assert.True(model.ShowsRefusal);
            Assert.Equal(list.Refusal, model.RefusalLine);
            Assert.Empty(model.Rows);
            Assert.False(model.CanApply);
            Assert.False(model.CanDryRun);
            Assert.Equal(new[] { "Changes found 0", "Follow the rules 0", "Break a manual rule 0", "Not editable ignored 0" }, model.Tiles.Select(tile => tile.Label + " " + tile.Count).ToArray());
        }

        [Fact]
        public void R402_before_a_file_is_picked_there_is_nothing_to_apply_and_the_lacks_of_the_rules_are_told()
        {
            var model = new UpdateFromReportViewModel();
            Assert.False(model.CanApply);
            Assert.False(model.CanDryRun);
            Assert.False(model.ShowsRefusal);
            Assert.Equal("Apply 0 changes", model.ApplyLabel);
            UpdateList list = MadeList();
            list.Lacks.Add("the rules profile holds no block manualRules, so no change breaks a manual rule (R402)");
            model.Load(list, new[] { "x.xlsx" });
            Assert.True(model.ShowsLacks);
            Assert.Equal(list.Lacks[0], model.LacksLine);
        }

        [Fact]
        public void R402_the_session_dry_run_saves_nothing_and_apply_hands_the_ticked_changes_and_the_notes_on()
        {
            var seen = new List<string>();
            var callbacks = new UpdateFromReportCallbacks
            {
                Read = files => { seen.Add("read " + string.Join("|", files)); return MadeList(); },
                Preview = ticked =>
                {
                    seen.Add("preview " + ticked.Count);
                    var applied = new UpdateApplied();
                    applied.Entries.AddRange(new[] { new JObject(), new JObject() });
                    applied.Replaced.Add(new JObject());
                    return applied;
                },
            };
            var picks = new Queue<string>(new[] { @"C:\w\012_Levels_x.xlsx", @"C:\w\012_Report.xlsx", null });
            var session = new UpdateFromReportSession(callbacks, () => picks.Dequeue());
            session.Browse();
            session.Browse();
            session.Browse();
            Assert.Equal(new[] { @"read C:\w\012_Levels_x.xlsx", @"read C:\w\012_Levels_x.xlsx|C:\w\012_Report.xlsx" }, seen.ToArray());
            Assert.Equal("012_Levels_x.xlsx, 012_Report.xlsx", session.Model.FilesLine);
            session.DryRun();
            Assert.Equal("preview 2", seen.Last());
            Assert.Equal("Dry run: 2 changes would be saved, 1 replaced. Nothing was changed.", session.Model.DryLine);
            Assert.True(session.Model.DryShown);
            Assert.False(session.Answer.Apply);
            session.Model.Rows[2].ApplyAnyway = true;
            session.Model.Rows[2].Note = "agreed";
            session.Apply();
            Assert.True(session.Answer.Apply);
            Assert.Equal(new[] { "102", "103", "101" }, session.Answer.Ticked.Select(change => change.Plot).ToArray());
            Assert.Equal("agreed", session.Answer.Notes[session.Answer.Ticked[2]]);
            Assert.Equal(1, session.Answer.Notes.Count);
        }

        [Fact]
        public void R402_cancel_and_a_read_that_fails_leave_the_answer_false_and_the_window_open_with_the_reason()
        {
            var failing = new UpdateFromReportSession(new UpdateFromReportCallbacks { Read = files => throw new InvalidOperationException("no plan of NBH 012") }, () => @"C:\w\012_Report.xlsx");
            bool finished = false;
            failing.Finished += () => finished = true;
            failing.Browse();
            Assert.False(finished);
            Assert.Equal("no plan of NBH 012", failing.Model.ProblemLine);
            Assert.True(failing.Model.ProblemShown);
            Assert.Equal("", failing.Model.FilesLine);
            failing.Apply();
            Assert.False(finished);
            failing.Cancel();
            Assert.True(finished);
            Assert.False(failing.Answer.Apply);
        }

        // The run on a made up repo: config, rules, the override list beside them, the plan of NBH 012 in the work folder. The worker is a stand in
        // that answers with the list Compare would give, the dialog is scripted
        private sealed class Repo
        {
            public string Config;
            public string Overrides;
            public string Work;
            public string Excel;
        }

        private Repo MadeRepo()
        {
            var repo = new Repo { Config = Path.Combine(root, "config", "project.json"), Overrides = Path.Combine(root, "config", "overrides", "roshn-makkah.json"), Work = Path.Combine(root, "work") };
            Directory.CreateDirectory(Path.GetDirectoryName(repo.Overrides));
            Directory.CreateDirectory(Path.Combine(root, "rules"));
            Directory.CreateDirectory(repo.Work);
            JObject config = R402_UpdateFromReportTests.WorkerConfig();
            config["profile"] = "rules/roshn-makkah.json";
            File.WriteAllText(repo.Config, config.ToString());
            JObject rules = R402_UpdateFromReportTests.MadeRules();
            rules["levelProfiles"]["default"] = Profile;
            File.WriteAllText(Path.Combine(root, "rules", "roshn-makkah.json"), rules.ToString());
            File.WriteAllText(repo.Overrides, UpdateFromReport.ListText(R402_UpdateFromReportTests.MadeOverrides()));
            File.WriteAllText(UpdateWorkerRequest.PlanPath(repo.Work, "012"), R402_UpdateFromReportTests.MadePlan().ToString());
            repo.Excel = Path.Combine(repo.Work, "012_Levels_" + Profile + ".xlsx");
            File.WriteAllBytes(repo.Excel, new byte[] { 1 });
            return repo;
        }

        private sealed class ScriptedDialog : IUpdateFromReportDialog
        {
            public Action<UpdateFromReportSession> Script;
            public UpdateFromReportSession Session;
            public string Pick;

            public UpdateFromReportAnswer Show(UpdateFromReportCallbacks callbacks)
            {
                Session = new UpdateFromReportSession(callbacks, () => Pick);
                Script(Session);
                return Session.Answer;
            }
        }

        private sealed class State : IRunState
        {
            public string Work;
            public string Config;
            public bool DryFirst;
            public readonly List<int> Told = new List<int>();

            public string WorkFolder(string documentPath) { return Work; }
            public bool DryRunFirst { get { return DryFirst; } }
            public int MissingStep(int button) { if (button < 1) throw new InvalidOperationException("a button with no number has no step"); return 0; }
            public IReadOnlyList<RunInput> Inputs(int button) { return new[] { new RunInput("Settings", Config, false) }; }
            public void Committed(int button, RunRecord record) { if (button < 1) throw new InvalidOperationException("a button with no number has no step"); Told.Add(button); }
        }

        private sealed class Transaction : IModelTransaction
        {
            public bool Committed;
            public bool Commit() { Committed = true; return true; }
            public void RollBack() { }
            public void Dispose() { }
            public IReadOnlyList<string> Warnings { get { return new string[0]; } }
            public IReadOnlyList<string> Errors { get { return new string[0]; } }
        }

        private sealed class Model : IModel
        {
            public int Started;
            public IModelTransaction Start(string name) { Started++; return new Transaction(); }
        }

        private sealed class Never : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun) { throw new InvalidOperationException("the window is the confirm"); }
        }

        // The list the worker would give for the Excel of 012: the El @ Entry of 102 follows the rules and the FFL of 101 breaks M08
        private static UpdateList WorkerList()
        {
            var list = new UpdateList();
            list.Changes.Add(Change("102", UpdateText.Entry, 10.2, 10.15, true));
            list.Changes.Add(Change("101", UpdateText.Ffl, 10.2, 10.25, false, breaks: "stepHeights", manual: "M08"));
            return list;
        }

        private RunOutcome Run(Repo repo, Action<UpdateFromReportSession> script, bool dryFirst, out Model model, out State state, out List<WorkerRequest> asked)
        {
            var requests = new List<WorkerRequest>();
            asked = requests;
            var dialog = new ScriptedDialog { Script = script, Pick = repo.Excel };
            var run = new UpdateFromReportRun(dialog, WorkFiles.ReadText, (request, work) => { requests.Add(request); return UpdateFromReport.ResultJson(WorkerList(), null); },
                new WorkerDocument { Path = @"C:\Models\Site.rvt", SessionId = "s" });
            model = new Model();
            state = new State { Work = repo.Work, Config = repo.Config, DryFirst = dryFirst };
            var flow = new ButtonFlow(state, model, new Never(), InputHash.Of, () => "0_run1");
            return flow.Run(0, "Update from report", run.Body(), @"C:\Models\Site.rvt");
        }

        [Fact]
        public void R402_cancel_and_a_dry_run_of_the_window_leave_the_override_list_as_it_was_and_apply_of_a_real_run_writes_it()
        {
            Repo repo = MadeRepo();
            string before = File.ReadAllText(repo.Overrides);
            Model model;
            State state;
            List<WorkerRequest> asked;

            RunOutcome outcome = Run(repo, session => { session.Browse(); session.Cancel(); }, false, out model, out state, out asked);
            Assert.Equal(RunEnd.Cancelled, outcome.End);
            Assert.Equal(before, File.ReadAllText(repo.Overrides));
            Assert.Equal(0, model.Started);

            outcome = Run(repo, session => { session.Browse(); session.DryRun(); session.Cancel(); }, false, out model, out state, out asked);
            Assert.Equal(RunEnd.Cancelled, outcome.End);
            Assert.Equal(before, File.ReadAllText(repo.Overrides));

            outcome = Run(repo, session => { session.Browse(); session.DryRun(); session.Apply(); }, true, out model, out state, out asked);
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            // Breaker 5f: the message after Apply names what was saved and the plots changed, and the run writes them for the buttons
            Assert.Equal("Saved 1 changes to the override list, plots changed: 102.", outcome.Message);
            JObject plots = JObject.Parse(File.ReadAllText(Path.Combine(repo.Work, UpdateFromReportRun.AppliedName)));
            Assert.Equal(new[] { "102" }, plots["plots"].Select(item => (string)item).ToArray());
            Assert.Equal("0_run1", (string)plots["runId"]);
            Assert.Equal(1, (int)plots["saved"]);
            Assert.Empty((JArray)plots["notApplied"]);
            Assert.Equal(1, model.Started);
            Assert.Empty(state.Told);
            string after = File.ReadAllText(repo.Overrides);
            Assert.NotEqual(before, after);
            JObject written = JObject.Parse(after);
            JObject entry = written["overrides"].OfType<JObject>().Single(item => (string)item["decision"] == "Update from report");
            Assert.Equal("102", (string)entry["plot"]);
            Assert.Equal("entryM", (string)entry["field"]);
            Assert.Equal("Update from report 0_run1", (string)entry["reason"]);
            Assert.Equal(-0.05, (double)entry["value"]["gapToRuleM"], 6);
            Assert.True((bool)entry["byHand"]);
            Assert.Equal(1, written["overrides"].Count(item => (string)item["decision"] == "Update from report"));
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(repo.Overrides), "*.tmp-*"));
            Assert.Equal("updateFromReport", asked.Last().Command);
            Assert.Equal(new[] { "config", "rules", "plan", "overrides", "excel1" }, asked.Last().Inputs.Select(item => item.Role).ToArray());
            Assert.Equal(repo.Overrides, asked.Last().Inputs.Single(item => item.Role == "overrides").Path);
        }

        [Fact]
        public void R402_apply_anyway_with_a_note_saves_the_change_with_the_note_in_its_reason_and_changes_no_other_entry()
        {
            Repo repo = MadeRepo();
            JObject beforeList = JObject.Parse(File.ReadAllText(repo.Overrides));
            Model model;
            State state;
            List<WorkerRequest> asked;
            RunOutcome outcome = Run(repo, session =>
            {
                session.Browse();
                session.Model.Rows[1].ApplyAnyway = true;
                session.Model.Rows[1].Note = "agreed with Bader";
                session.Apply();
            }, false, out model, out state, out asked);
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            JObject written = JObject.Parse(File.ReadAllText(repo.Overrides));
            JObject ffl = written["overrides"].OfType<JObject>().Single(item => (string)item["plot"] == "101" && (string)item["decision"] == "Update from report");
            Assert.Equal("Update from report 0_run1. agreed with Bader", (string)ffl["reason"]);
            Assert.Equal(beforeList["overrides"].Count() + 1, written["overrides"].Count());
            Assert.Equal(beforeList["cannot"].ToString(), written["cannot"].ToString());
        }

        // Breaker 5f: a ticked change Apply can not save is named in the message after Apply, never dropped with no word
        [Fact]
        public void R402_a_ticked_change_apply_can_not_save_is_named_in_the_message_after_apply()
        {
            Repo repo = MadeRepo();
            Model model;
            State state;
            List<WorkerRequest> asked;
            RunOutcome outcome = Run(repo, session =>
            {
                session.Browse();
                session.Model.Rows[1].Change.Field = UpdateText.VillaType;
                session.Model.Rows[1].Change.InExcel = "DP1";
                session.Model.Rows[1].ApplyAnyway = true;
                session.Model.Rows[0].Change.Plot = "109";
                session.Apply();
            }, false, out model, out state, out asked);
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            Assert.Equal("Saved 1 changes to the override list, plots changed: 101.\n1 ticked changes were not saved:\nplot 109 El @ Entry: the plan holds no levelRule of the plot, so no gap to the rule (R402)",
                outcome.Message);
        }

        [Fact]
        public void R402_a_plan_that_is_not_in_the_work_folder_stops_the_read_with_its_name_and_changes_nothing()
        {
            Repo repo = MadeRepo();
            File.Delete(UpdateWorkerRequest.PlanPath(repo.Work, "012"));
            string before = File.ReadAllText(repo.Overrides);
            Model model;
            State state;
            List<WorkerRequest> asked;
            string shown = null;
            RunOutcome outcome = Run(repo, session => { session.Browse(); shown = session.Model.ProblemLine; session.Cancel(); }, false, out model, out state, out asked);
            Assert.Equal(RunEnd.Cancelled, outcome.End);
            Assert.Contains("plan_012.json", shown);
            Assert.Empty(asked);
            Assert.Equal(before, File.ReadAllText(repo.Overrides));
        }

        [Fact]
        public void R402_a_button_with_no_number_runs_through_the_flow_without_asking_for_a_step_or_telling_the_pane()
        {
            Repo repo = MadeRepo();
            Model model;
            State state;
            List<WorkerRequest> asked;
            RunOutcome outcome = Run(repo, session => { session.Browse(); session.Apply(); }, false, out model, out state, out asked);
            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Empty(state.Told);
        }

        [Fact]
        public void R402_the_entry_of_the_button_hands_the_application_to_its_body_in_a_few_lines_of_buttons_cs()
        {
            string addin = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn");
            string buttons = File.ReadAllText(Path.Combine(addin, "Commands", "Buttons.cs"));
            Assert.Contains("Bodies.Of(button, () => SetUpSiteBody.For(application))", buttons);
            Assert.Contains("UpdateFromReportBody.For(application)", buttons);
            Assert.True(File.Exists(Path.Combine(addin, "Commands", "UpdateFromReportBody.cs")));
            Assert.Equal("Update from report is not built yet.", Bodies.Of(RibbonLayout.ButtonOf("UpdateFromReportCommand")).NotBuilt);
        }
    }
}
