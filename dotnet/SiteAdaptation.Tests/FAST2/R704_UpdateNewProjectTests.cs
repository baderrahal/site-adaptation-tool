using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Update;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    // R704, Q394 CHOSEN. Update from report on a new project saves a change of a plot with no override entry as a new entry
    public class R704_UpdateNewProjectTests
    {
        private const string Sha = "aaaa000000000000000000000000000000000000000000000000000000000001";
        private const string Report = "edited report.xlsx";

        // The report as read back, its sheet Placed with the villa type of 103 changed, a plot whose entries are of another job only
        private static List<UpdateBook> Books(JObject plan)
        {
            var settings = new UpdateSheet { Name = LevelExcelText.SettingsSheet };
            settings.Rows.Add(new object[] { LevelExcelText.PlanBuildId, UpdateFromReport.BuildIdOf(plan) });
            settings.Rows.Add(new object[] { LevelExcelText.PlanSha, Sha });
            var placed = new UpdateSheet { Name = ReportExcelText.PlacedSheet };
            placed.Rows.Add(ReportExcelText.PlacedColumns.Cast<object>().ToArray());
            placed.Rows.Add(new object[] { "103", ReportExcelText.LinkKind, "VD4", false, null });
            var book = new UpdateBook { FileName = Report };
            book.Sheets.Add(settings);
            book.Sheets.Add(placed);
            return new List<UpdateBook> { book };
        }

        private static UpdateList Compare(bool newProject)
        {
            JObject plan = R402_UpdateFromReportTests.MadePlan();
            return UpdateFromReport.Compare(Books(plan), plan, Sha, R402_UpdateFromReportTests.MadeRules(), "parsons2024Rev6", new JObject { ["overrides"] = new JArray() }, newProject);
        }

        [Fact]
        public void R704_on_a_new_project_a_plot_with_no_entry_is_a_new_entry_and_in_the_past_job_mode_it_cannot_be_saved()
        {
            UpdateChange fresh = Compare(true).Changes.Single(change => change.Plot == "103");
            Assert.True(fresh.NewEntry);
            Assert.Null(fresh.NotSavable);
            Assert.True(fresh.CanApply);
            UpdateChange past = Compare(false).Changes.Single(change => change.Plot == "103");
            Assert.False(past.NewEntry);
            Assert.NotNull(past.NotSavable);
        }

        [Fact]
        public void R704_apply_writes_the_new_entry_with_the_report_as_its_model_and_no_key_and_the_json_keeps_the_mark()
        {
            UpdateChange change = Compare(true).Changes.Single(item => item.Plot == "103");
            JObject overrides = new JObject { ["overrides"] = new JArray(new JObject { ["plot"] = "101", ["field"] = "heightM", ["source"] = "parsons" }) };
            UpdateApplied applied = UpdateFromReport.Apply(new[] { change }, R402_UpdateFromReportTests.MadePlan(), overrides, R402_UpdateFromReportTests.MadeRules(), "parsons2024Rev6", "r1");
            Assert.Empty(applied.NotApplied);
            JObject entry = applied.Entries.Single();
            Assert.Equal("103", (string)entry["plot"]);
            Assert.Equal(Report, (string)entry["model"]);
            Assert.Equal(JTokenType.Null, entry["key"].Type);
            Assert.Equal("parsons", (string)entry["source"]);
            // A plot with no entry at all goes after the last entry of the list
            Assert.Same(entry, ((JArray)applied.List["overrides"]).Last);
            // Review 1 of PR 3: every reader of the list runs the check of R330 first, it takes the entry by its source and its hand mark
            var groups = new JObject { ["parsons"] = new JArray("imc"), ["native"] = new JArray("u11") };
            PlanOverrides.CheckEntries(new JArray(entry.DeepClone()), groups);
            JObject unmarked = (JObject)entry.DeepClone();
            unmarked.Remove("byHand");
            Assert.Throws<System.IO.InvalidDataException>(() => PlanOverrides.CheckEntries(new JArray(unmarked), groups));
            JObject otherJob = (JObject)entry.DeepClone();
            otherJob["source"] = "nobody";
            Assert.Throws<System.IO.InvalidDataException>(() => PlanOverrides.CheckEntries(new JArray(otherJob), groups));

            var list = new UpdateList();
            list.Changes.Add(change);
            Assert.True(UpdateListJson.Read(UpdateFromReport.ResultJson(list, null)).Changes.Single().NewEntry);

            // The same change with no mark finds no entry of the job to take its model from
            change.NewEntry = false;
            Assert.Single(UpdateFromReport.Apply(new[] { change }, R402_UpdateFromReportTests.MadePlan(), overrides, R402_UpdateFromReportTests.MadeRules(), "parsons2024Rev6", "r1").NotApplied);
        }
    }
}
