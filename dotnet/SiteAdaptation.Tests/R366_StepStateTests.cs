using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R366, docs/design/03a-buttons-shared-and-1.md Steps and docs/design/13-workflow-changes.md Order. The step line of each of the 9
    // buttons from its last real run, the one hash over its inputs now and its outputs on disk, all given, so no Revit and no file
    public sealed class R366_StepStateTests : IDisposable
    {
        private const string Out = "out-hash";
        private static readonly DateTime Start = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-r366-" + Guid.NewGuid().ToString("N"));
        private readonly SettingsScope scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "002" };

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        // A step whose last real run committed at the minute of its number on the scope of the test, its inputs as they are now
        private StepNow Ran(int step, int issues = 0, string hashThen = null, string hashNow = null, int minute = 0, SettingsScope scopeThen = null)
        {
            var outputs = new Dictionary<string, string> { { "step" + step + ".json", Out } };
            var run = new StepRun(Start.AddMinutes(minute == 0 ? step : minute), hashThen ?? "in" + step, scopeThen ?? scope, issues, outputs);
            return new StepNow(run, hashNow ?? hashThen ?? "in" + step, outputs);
        }

        private static Dictionary<int, StepNow> Steps(params StepNow[] steps)
        {
            var found = new Dictionary<int, StepNow>();
            for (int at = 0; at < steps.Length; at++)
            {
                found[at + 1] = steps[at];
            }
            return found;
        }

        private IReadOnlyList<StepLine> Lines(Dictionary<int, StepNow> steps, bool settingsChosen = true)
        {
            return StepStates.Of(settingsChosen, scope, steps);
        }

        private static string Text(IReadOnlyList<StepLine> lines)
        {
            return string.Join(" ", lines.Select(line => line.Kind + (line.Value == 0 ? "" : ":" + line.Value)));
        }

        [Fact]
        public void R366_each_step_needs_the_one_before_9_needs_4_and_1_needs_only_the_settings()
        {
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 4 }, Enumerable.Range(1, StepStates.Count).Select(StepStates.Needs).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => StepStates.Needs(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StepStates.Needs(10));
        }

        [Fact]
        public void R366_with_no_run_and_no_settings_1_needs_the_settings_and_each_other_step_the_step_it_needs()
        {
            Assert.Equal("NeedsSettings NeedsStep:1 NeedsStep:2 NeedsStep:3 NeedsStep:4 NeedsStep:5 NeedsStep:6 NeedsStep:7 NeedsStep:4",
                Text(Lines(new Dictionary<int, StepNow>(), false)));
        }

        [Fact]
        public void R366_with_the_settings_chosen_1_is_ready()
        {
            Assert.Equal("Ready NeedsStep:1 NeedsStep:2 NeedsStep:3 NeedsStep:4 NeedsStep:5 NeedsStep:6 NeedsStep:7 NeedsStep:4", Text(Lines(new Dictionary<int, StepNow>())));
        }

        [Fact]
        public void R366_a_step_whose_last_run_committed_with_no_issue_on_the_same_inputs_scope_and_outputs_is_done()
        {
            Assert.Equal("Done Ready NeedsStep:2 NeedsStep:3 NeedsStep:4 NeedsStep:5 NeedsStep:6 NeedsStep:7 NeedsStep:4", Text(Lines(Steps(Ran(1)))));
        }

        [Fact]
        public void R366_9_is_ready_once_4_is_done_and_5_is_ready_with_it()
        {
            Assert.Equal("Done Done Done Done Ready NeedsStep:5 NeedsStep:6 NeedsStep:7 Ready", Text(Lines(Steps(Ran(1), Ran(2), Ran(3), Ran(4)))));
        }

        [Fact]
        public void R366_a_run_with_issues_is_done_with_its_issues_and_the_step_after_it_is_ready()
        {
            Assert.Equal("DoneWithIssues:2 Ready NeedsStep:2", string.Join(" ", Text(Lines(Steps(Ran(1, issues: 2)))).Split(' ').Take(3)));
        }

        [Fact]
        public void R366_an_input_changed_since_a_step_makes_that_step_and_every_step_after_it_need_running_again()
        {
            var steps = Steps(Ran(1), Ran(2), Ran(3, hashThen: "old", hashNow: "new"), Ran(4), Ran(5), Ran(6), Ran(7), Ran(8), Ran(9));

            Assert.Equal("Done Done Ready NeedsStep:3 NeedsStep:4 NeedsStep:5 NeedsStep:6 NeedsStep:7 NeedsStep:4", Text(Lines(steps)));
        }

        [Fact]
        public void R366_an_input_of_5_changed_makes_9_run_again_though_9_needs_only_4()
        {
            var steps = Steps(Ran(1), Ran(2), Ran(3), Ran(4), Ran(5, hashThen: "old", hashNow: "new"), Ran(6), Ran(7), Ran(8), Ran(9));

            Assert.Equal("Done Done Done Done Ready NeedsStep:5 NeedsStep:6 NeedsStep:7 Ready", Text(Lines(steps)));
        }

        [Fact]
        public void R366_an_input_not_chosen_or_not_on_disk_now_leaves_the_step_not_done()
        {
            var steps = Steps(Ran(1), new StepNow(Ran(2).LastRun, null, Ran(2).OutputsOnDisk));

            Assert.Equal("Done Ready NeedsStep:2", string.Join(" ", Text(Lines(steps)).Split(' ').Take(3)));
        }

        [Fact]
        public void R366_without_the_settings_chosen_1_needs_the_settings_though_it_ran()
        {
            var steps = Steps(new StepNow(Ran(1).LastRun, null, Ran(1).OutputsOnDisk), Ran(2));

            Assert.Equal("NeedsSettings NeedsStep:1 NeedsStep:2", string.Join(" ", Text(Lines(steps, false)).Split(' ').Take(3)));
        }

        [Fact]
        public void R366_a_step_run_on_another_scope_is_not_done()
        {
            var other = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "2290-2299" };

            Assert.Equal("Done Done Ready NeedsStep:3", string.Join(" ", Text(Lines(Steps(Ran(1), Ran(2), Ran(3, scopeThen: other), Ran(4)))).Split(' ').Take(4)));
        }

        [Fact]
        public void R366_the_same_plots_typed_another_way_are_the_same_scope()
        {
            var now = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "2290-2299, 42" };
            var then = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "042\n02290-2299" };
            var steps = Steps(Ran(1), Ran(2, scopeThen: then));

            Assert.Equal(StepStateKind.Done, StepStates.Of(true, now, steps)[1].Kind);
        }

        [Fact]
        public void R366_button_1_always_works_on_the_whole_project_so_its_scope_never_makes_it_run_again()
        {
            var steps = Steps(Ran(1, scopeThen: new SettingsScope { Kind = ScopeKinds.Plots, Plots = "43" }));

            Assert.Equal(StepStateKind.Done, Lines(steps)[0].Kind);
        }

        [Fact]
        public void R366_an_output_the_record_lists_that_is_gone_leaves_the_step_not_done()
        {
            StepNow two = Ran(2);
            var steps = Steps(Ran(1), new StepNow(two.LastRun, "in2", new Dictionary<string, string>()));

            Assert.Equal(StepStateKind.Ready, Lines(steps)[1].Kind);
        }

        [Fact]
        public void R366_an_output_whose_hash_on_disk_is_not_the_one_of_the_record_leaves_the_step_not_done()
        {
            StepNow two = Ran(2);
            var steps = Steps(Ran(1), new StepNow(two.LastRun, "in2", new Dictionary<string, string> { { "step2.json", "other" } }));

            Assert.Equal(StepStateKind.Ready, Lines(steps)[1].Kind);
        }

        [Fact]
        public void R366_a_file_on_disk_the_record_does_not_list_changes_nothing()
        {
            StepNow two = Ran(2);
            var disk = new Dictionary<string, string>(two.OutputsOnDisk.ToDictionary(item => item.Key, item => item.Value)) { { "extra.json", "x" } };
            var steps = Steps(Ran(1), new StepNow(two.LastRun, "in2", disk));

            Assert.Equal(StepStateKind.Done, Lines(steps)[1].Kind);
        }

        [Fact]
        public void R366_a_step_run_again_after_a_later_step_makes_the_later_steps_run_again()
        {
            var steps = Steps(Ran(1), Ran(2), Ran(3, minute: 30), Ran(4));

            Assert.Equal("Done Done Done Ready NeedsStep:4", string.Join(" ", Text(Lines(steps)).Split(' ').Take(5)));
        }

        [Fact]
        public void R366_5_run_after_9_makes_9_run_again()
        {
            var steps = Steps(Ran(1), Ran(2), Ran(3), Ran(4), Ran(5, minute: 30), null, null, null, Ran(9));

            Assert.Equal("Done Done Done Done Done Ready NeedsStep:6 NeedsStep:7 Ready", Text(Lines(steps)));
        }

        [Fact]
        public void R366_a_step_whose_step_before_never_ran_is_not_done_and_needs_it()
        {
            var steps = Steps(Ran(1), null, Ran(3));

            Assert.Equal("Done Ready NeedsStep:2 NeedsStep:3", string.Join(" ", Text(Lines(steps)).Split(' ').Take(4)));
        }

        [Fact]
        public void R366_a_step_not_of_1_to_9_or_a_run_with_issues_below_0_is_refused()
        {
            var steps = new Dictionary<int, StepNow> { { 10, Ran(1) } };

            Assert.Throws<ArgumentOutOfRangeException>(() => StepStates.Of(true, scope, steps));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StepRun(Start, "in", scope, -1, new Dictionary<string, string>()));
        }

        [Fact]
        public void R366_a_last_run_of_the_settings_json_gives_its_time_hash_scope_and_issues()
        {
            var button = new ButtonRun { Time = "2026-10-29T08:05:00Z", Mode = RunModes.Direct, Scope = scope };
            button.InputHashes["Curbs"] = "c1";
            button.InputHashes["Settings"] = "s1";
            button.Issues.Add("Plot 43: the ramp is short");
            var outputs = new Dictionary<string, string> { { "plan.json", Out } };

            StepRun run = StepRun.From(button, outputs);

            Assert.Equal(Start.AddMinutes(5), run.TimeUtc);
            Assert.Equal(DateTimeKind.Utc, run.TimeUtc.Kind);
            Assert.Equal(StepHash.Combine(button.InputHashes), run.InputHash);
            Assert.Same(scope, run.Scope);
            Assert.Equal(1, run.Issues);
            Assert.Equal(Out, run.Outputs["plan.json"]);
        }

        [Fact]
        public void R366_the_one_hash_over_the_inputs_holds_every_name_and_hash_in_any_order()
        {
            string one = StepHash.Combine(new Dictionary<string, string> { { "Curbs", "c1" }, { "Settings", "s1" } });
            var reversed = new SortedDictionary<string, string>(StringComparer.Ordinal) { { "Settings", "s1" }, { "Curbs", "c1" } };

            Assert.Matches("^[0-9a-f]{64}$", one);
            Assert.Equal(one, StepHash.Combine(reversed));
            Assert.NotEqual(one, StepHash.Combine(new Dictionary<string, string> { { "Curbs", "c2" }, { "Settings", "s1" } }));
            Assert.NotEqual(one, StepHash.Combine(new Dictionary<string, string> { { "Kerbs", "c1" }, { "Settings", "s1" } }));
            Assert.NotEqual(one, StepHash.Combine(new Dictionary<string, string> { { "Curbs", "c1" } }));
            Assert.NotEqual(StepHash.Combine(new Dictionary<string, string> { { "a|b", "c" } }), StepHash.Combine(new Dictionary<string, string> { { "a", "b|c" } }));
        }

        [Fact]
        public void R366_the_hash_over_the_inputs_hashes_files_by_content_and_folders_by_their_list()
        {
            Directory.CreateDirectory(Path.Combine(root, "roads"));
            string curbs = Path.Combine(root, "curbs.dwg");
            string roads = Path.Combine(root, "roads");
            File.WriteAllText(curbs, "one");
            File.WriteAllText(Path.Combine(roads, "a.xml"), "a");
            var inputs = new[] { new RunInput("Curbs", curbs, false), new RunInput("Road surface", roads, true) };

            string first = StepHash.Over(inputs);
            var each = new Dictionary<string, string> { { "Curbs", FileHash.Sha256(curbs) }, { "Road surface", InputHash.OfFolder(roads) } };
            Assert.Equal(StepHash.Combine(each), first);

            File.WriteAllText(curbs, "two");
            string second = StepHash.Over(inputs);
            Assert.NotEqual(first, second);

            File.WriteAllText(Path.Combine(roads, "b.xml"), "b");
            Assert.NotEqual(second, StepHash.Over(inputs));
        }

        [Fact]
        public void R366_the_hash_over_the_inputs_is_none_when_an_input_is_not_chosen_or_not_on_disk()
        {
            Directory.CreateDirectory(root);
            string curbs = Path.Combine(root, "curbs.dwg");
            File.WriteAllText(curbs, "one");

            Assert.Null(StepHash.Over(new[] { new RunInput("Curbs", curbs, false), new RunInput("Sales", Path.Combine(root, "sales.xlsx"), false) }));
            Assert.Null(StepHash.Over(new[] { new RunInput("Road surface", Path.Combine(root, "roads"), true) }));
            Assert.Null(StepHash.Over(new[] { new RunInput("Curbs", "", false) }));
            Assert.Null(StepHash.Over(new[] { new RunInput("Curbs", curbs, true) }));
            Assert.Throws<ArgumentException>(() => StepHash.Over(new[] { new RunInput("Curbs", curbs, false), new RunInput("Curbs", curbs, false) }));
        }

        [Fact]
        public void R366_the_outputs_on_disk_are_hashed_by_content_and_a_missing_one_is_left_out()
        {
            Directory.CreateDirectory(root);
            string plan = Path.Combine(root, "plan.json");
            File.WriteAllText(plan, "{}");

            Dictionary<string, string> disk = StepHash.OnDisk(new[] { plan, Path.Combine(root, "gone.json") });

            Assert.Equal(new[] { plan }, disk.Keys.ToArray());
            Assert.Equal(FileHash.Sha256(plan), disk[plan]);
        }
    }
}
