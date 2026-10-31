using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R442. The cluster confirm of 3 Review data in Common, on net48 and net8.0, on a small plan file of a made up 4 Check built here.
    // Made up plot numbers only, no coordinate, level or file name of a real model
    public sealed class R442_ClusterConfirmTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "cluster-confirm-" + Guid.NewGuid().ToString("N"));

        public R442_ClusterConfirmTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        // Plots 12 and 13 one cluster of two opposite hands, 14 a detached unit with no Rev 6 hand, 15 a reason with no cluster and 16
        // with no proposal at all, as the plan of a build under rev6 writes plots[].proposal
        private static JObject Plan(bool withProposals)
        {
            var plots = new JArray(
                new JObject { ["plot"] = "12", ["proposal"] = new JObject { ["cluster"] = new JArray("12", "13"), ["mirrored"] = false, ["reason"] = "pair with 13" } },
                new JObject { ["plot"] = "13", ["proposal"] = new JObject { ["cluster"] = new JArray("12", "13"), ["mirrored"] = true, ["reason"] = "pair with 12" } },
                new JObject { ["plot"] = "14", ["proposal"] = new JObject { ["cluster"] = new JArray("14"), ["mirrored"] = null, ["reason"] = "detached, no Rev 6 hand (Q226)" } },
                new JObject { ["plot"] = "15", ["proposal"] = new JObject { ["cluster"] = null, ["mirrored"] = null, ["reason"] = "no single partner (Q229)" } },
                new JObject { ["plot"] = "16" });
            if (!withProposals)
            {
                foreach (JObject entry in plots)
                {
                    entry.Remove("proposal");
                }
            }
            return new JObject { ["format"] = "site-adaptation-plan", ["neighbourhood"] = "012", ["plots"] = plots };
        }

        // A done run of 4 Check: its folder with the plan of NBH 012 and check.json naming the plan by its SHA-256
        private string Check(string runId, JObject plan)
        {
            string folder = Path.Combine(work, CheckRun.FolderPrefix + runId);
            Directory.CreateDirectory(folder);
            string planPath = Path.Combine(folder, CheckRun.PlanName("012"));
            File.WriteAllText(planPath, plan.ToString());
            var record = new JObject
            {
                ["format"] = CheckRun.RecordFormat,
                ["runId"] = runId,
                ["outputs"] = new JArray(new JObject { ["path"] = planPath, ["sha256"] = FileHash.Sha256(planPath) }),
            };
            File.WriteAllText(Path.Combine(folder, CheckRun.RecordFile), record.ToString());
            return planPath;
        }

        private ClusterConfirm Loaded(ClusterSaved saved)
        {
            Tuple<List<ClusterMark>, string> found = ClusterConfirm.Load(work, "012");
            return new ClusterConfirm(found.Item1, found.Item2, saved);
        }

        [Fact]
        public void R442_the_proposals_are_read_from_the_plan_of_the_last_done_4_check_and_shown_with_their_mirror()
        {
            Check("R4-20261101-0100", Plan(false));
            string planPath = Check("R4-20261101-0200", Plan(true));

            Tuple<List<ClusterMark>, string> found = ClusterConfirm.Load(work, "012");

            Assert.Equal(FileHash.Sha256(planPath), found.Item2);
            Assert.Equal(new[] { "12", "13", "14", "15" }, found.Item1.Select(mark => mark.Plot));
            var clusters = new ClusterConfirm(found.Item1, found.Item2, null);
            Assert.Equal("12+13 mirror no", ClusterConfirm.Shown(clusters.MarkOf("12")));
            Assert.Equal("12+13 mirror yes", ClusterConfirm.Shown(clusters.MarkOf("13")));
            Assert.Equal("14 mirror none", ClusterConfirm.Shown(clusters.MarkOf("14")));
            Assert.Equal("No proposal", ClusterConfirm.Shown(clusters.MarkOf("15")));
            Assert.Null(ClusterConfirm.Shown(clusters.MarkOf("16")));
            Assert.Equal("no single partner", clusters.MarkOf("15").Reason);
            Assert.Equal(new[] { true, true, true, false, false }, new[] { "12", "13", "14", "15", "16" }.Select(clusters.HasProposal));
        }

        [Fact]
        public void R442_a_plan_changed_after_its_record_gives_nothing_to_confirm_as_buttons_5_to_8_stop_on_it()
        {
            string planPath = Check("R4-20261101-0200", Plan(true));
            File.AppendAllText(planPath, " ");

            Tuple<List<ClusterMark>, string> found = ClusterConfirm.Load(work, "012");

            Assert.Empty(found.Item1);
            Assert.Null(found.Item2);
        }

        [Fact]
        public void R442_a_tick_confirms_a_plot_with_a_proposal_and_only_such_a_plot()
        {
            Check("R4-20261101-0200", Plan(true));
            ClusterConfirm clusters = Loaded(null);

            clusters.Confirm("12", true);

            Assert.True(clusters.IsConfirmed("12"));
            Assert.True(clusters.Changed);
            Assert.Equal(new[] { "12" }, clusters.Confirmed());
            Assert.Equal(new[] { "13", "14" }, clusters.Unconfirmed());
            Assert.Throws<ArgumentException>(() => clusters.Confirm("15", true));
            Assert.Throws<ArgumentException>(() => clusters.Confirm("16", true));
            clusters.Confirm("12", false);
            Assert.Empty(clusters.Confirmed());
            Assert.Equal(new[] { "12", "13", "14" }, clusters.Unconfirmed());
        }

        [Fact]
        public void R442_confirm_all_proposed_ticks_every_plot_with_a_proposal()
        {
            Check("R4-20261101-0200", Plan(true));
            ClusterConfirm clusters = Loaded(null);
            clusters.Confirm("13", true);
            clusters.MarkSaved();

            Assert.True(clusters.AnyToConfirm);
            Assert.Equal(2, clusters.ConfirmAll());

            Assert.Equal(new[] { "12", "13", "14" }, clusters.Confirmed());
            Assert.Empty(clusters.Unconfirmed());
            Assert.False(clusters.AnyToConfirm);
            Assert.True(clusters.Changed);
            Assert.Equal(0, clusters.ConfirmAll());
            Assert.False(clusters.IsConfirmed("15"));
        }

        [Fact]
        public void R442_with_no_plan_of_4_check_or_a_plan_with_no_proposal_there_is_nothing_to_confirm_and_no_plot_is_marked()
        {
            Tuple<List<ClusterMark>, string> before = ClusterConfirm.Load(work, "012");
            Assert.Empty(before.Item1);
            Assert.Null(before.Item2);
            Assert.Empty(ClusterConfirm.Load(Path.Combine(work, "no such folder"), "012").Item1);

            Check("R4-20261101-0200", Plan(false));
            foreach (ClusterConfirm clusters in new[] { new ClusterConfirm(before.Item1, before.Item2, null), Loaded(null), ClusterConfirm.None })
            {
                Assert.False(clusters.AnyProposed);
                Assert.False(clusters.AnyToConfirm);
                Assert.Equal(0, clusters.ConfirmAll());
                Assert.Empty(clusters.Confirmed());
                Assert.Empty(clusters.Unconfirmed());
                Assert.False(clusters.Changed);
            }
            Assert.NotNull(ClusterConfirm.Load(work, "012").Item2);
        }

        [Fact]
        public void R442_the_review_file_keeps_the_confirmed_and_the_unconfirmed_plots_and_reads_them_back()
        {
            Check("R4-20261101-0200", Plan(true));
            ClusterConfirm clusters = Loaded(null);
            clusters.Confirm("13", true);
            var checks = new[] { new ReviewCheck { Plot = "12", Check = ReviewCheckValue.Ok, Note = "", RunId = "r", PlotTableHash = "h1" } };

            string path = ReviewFile.Write(work, "012", checks, "R3-1", clusters);

            JObject root = JObject.Parse(File.ReadAllText(path));
            Assert.Equal(new[] { "13" }, root["clusterConfirmed"].Select(item => (string)item));
            Assert.Equal(new[] { "12", "14" }, root["clusterUnconfirmed"].Select(item => (string)item));
            Assert.Equal(clusters.PlanSha256, (string)root["clusterPlanSha256"]);
            Assert.Equal(1, (int)root["formatVersion"]);
            ClusterSaved saved = ReviewFile.ReadClusters(path);
            Assert.Equal(new[] { "13" }, saved.Confirmed);
            Assert.Equal(new[] { "12", "14" }, saved.Unconfirmed);
            Assert.Equal(new[] { "12", "14" }, ReviewFile.ClusterUnconfirmed(work, "012"));
            Assert.Equal("12", ReviewFile.Read(path).Single().Plot);
            // The window opened again on the same plan shows the tick saved
            Assert.True(Loaded(saved).IsConfirmed("13"));
            Assert.False(Loaded(saved).IsConfirmed("12"));
        }

        [Fact]
        public void R442_an_old_review_file_without_the_two_fields_reads_as_none_confirmed_and_none_to_skip()
        {
            string path = ReviewFile.Write(work, "012", new[] { new ReviewCheck { Plot = "12", Check = ReviewCheckValue.Wrong, Note = "turned", RunId = "r", PlotTableHash = "h1" } }, "R3-1");

            Assert.Null(JObject.Parse(File.ReadAllText(path))["clusterUnconfirmed"]);
            ClusterSaved saved = ReviewFile.ReadClusters(path);
            Assert.Empty(saved.Confirmed);
            Assert.Empty(saved.Unconfirmed);
            Assert.Null(saved.PlanSha256);
            Assert.Empty(ReviewFile.ClusterUnconfirmed(work, "012"));
            Assert.Empty(ReviewFile.ClusterUnconfirmed(work, "013"));
            Assert.Equal("12", ReviewFile.WrongPlots(work, "012", "h1").Single().Plot);

            JObject root = JObject.Parse(File.ReadAllText(path));
            root["clusterUnconfirmed"] = "12";
            File.WriteAllText(path, root.ToString());
            Assert.Contains("clusterUnconfirmed", Assert.Throws<InvalidDataException>(() => ReviewFile.ReadClusters(path)).Message);
        }

        [Fact]
        public void R442_a_confirm_holds_only_against_the_plan_it_was_made_on_and_with_no_plan_the_saved_confirm_is_kept()
        {
            Check("R4-20261101-0200", Plan(true));
            var saved = new ClusterSaved { Confirmed = new[] { "12", "13" }, Unconfirmed = new[] { "14" }, PlanSha256 = "an earlier plan" };

            ClusterConfirm again = Loaded(saved);
            Assert.False(again.IsConfirmed("12"));
            Assert.Equal(new[] { "12", "13", "14" }, again.Unconfirmed());

            ClusterConfirm none = new ClusterConfirm(null, null, saved);
            Assert.False(none.AnyProposed);
            Assert.Equal(new[] { "12", "13" }, none.Confirmed());
            Assert.Equal(new[] { "14" }, none.Unconfirmed());
            Assert.Equal("an earlier plan", none.PlanSha256);
        }

        [Fact]
        public void R442_button_3_loads_the_proposals_and_a_save_in_the_window_writes_the_step_state()
        {
            Check("R4-20261101-0200", Plan(true));
            string read = Path.Combine(work, ReadDataRun.FolderPrefix + "one");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, ReadDataRun.RecordFile), new JObject { ["format"] = ReadDataRun.RecordFormat, ["runId"] = "R2-20261101-0000", ["inputs"] = new JArray() }.ToString());
            var plot = new JObject { ["baseplanLabel"] = "1012", ["nbhId"] = "012", ["locationPlanNumber"] = "12", ["officialNumber"] = "1012", ["typologyRev9"] = "VL4", ["faceAreaM2"] = 300.0 };
            File.WriteAllText(Path.Combine(read, ReadDataRun.PlotTableFile), new JObject { ["plots"] = new JArray(plot) }.ToString());

            ReviewInput input = ReviewLoad.Load(work, "012", null);

            Assert.Equal(new[] { "12", "13", "14", "15" }, input.Proposals.Select(mark => mark.Plot));
            Assert.NotNull(input.PlanSha256);
            Assert.Empty(input.ClusterSaved.Confirmed);
            ReviewView opened = null;
            var dialog = new Dialog((view, callbacks) =>
            {
                opened = view;
                view.Clusters.Confirm("12", true);
                callbacks.Save(view.State.ToChecks());
                return new ReviewAnswer { Saved = true };
            });

            Assert.True(new ReviewDataRun(context => input, dialog, null).Show(input, "R3-1", work));

            Assert.False(opened.Clusters.Changed);
            Assert.Equal(new[] { "13", "14" }, ReviewFile.ClusterUnconfirmed(work, "012"));
            Assert.Equal(new[] { "12" }, ReviewLoad.Load(work, "012", null).ClusterSaved.Confirmed);
            Assert.Equal("cluster not confirmed in 3 Review data", ClusterConfirm.NotConfirmedReason);
        }

        private sealed class Dialog : IReviewDialog
        {
            private readonly Func<ReviewView, ReviewCallbacks, ReviewAnswer> act;

            public Dialog(Func<ReviewView, ReviewCallbacks, ReviewAnswer> act)
            {
                this.act = act;
            }

            public ReviewAnswer Show(ReviewView shown, ReviewCallbacks callbacks)
            {
                return act(shown, callbacks);
            }
        }

        [Fact]
        public void R442_the_reason_shown_names_no_question_and_no_rule()
        {
            Assert.Equal("unit VL4 is detached, Rev 06 2.1.A sets a detached unit alone and gives it no hand, so no mirror flag",
                ClusterConfirm.Plain("unit VL4 is detached, Rev 06 2.1.A sets a detached unit alone and gives it no hand, so no mirror flag, Q226 (R411)"));
            Assert.Equal("the row 1 of units A, B splits into no clusters of the rules profile, Rev 06 2.1.E, so no proposal",
                ClusterConfirm.Plain("the row 1 of units A, B splits into no clusters of the rules profile, Rev 06 2.1.E, so no proposal, Q229 (R411)"));
            Assert.Null(ClusterConfirm.Plain(null));
        }
    }
}
