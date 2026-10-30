using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteAdaptation.Common.Scope;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Steps
{
    public enum StepStateKind
    {
        Done,
        DoneWithIssues,
        Ready,
        NeedsStep,
        NeedsSettings,
    }

    public sealed class StepLine
    {
        public StepLine(StepStateKind kind, int value = 0)
        {
            Kind = kind;
            Value = value;
        }

        public StepStateKind Kind { get; }

        // The issues of DoneWithIssues, the step of NeedsStep, else 0
        public int Value { get; }
    }

    // R366. The last real run of a step that committed: its time, the one hash over its inputs then, its scope, its issue count and
    // each output its run record lists with its SHA-256
    public sealed class StepRun
    {
        public StepRun(DateTime timeUtc, string inputHash, SettingsScope scope, int issues, IReadOnlyDictionary<string, string> outputs)
        {
            if (issues < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(issues), issues, "a run has 0 issues or more");
            }
            TimeUtc = timeUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(timeUtc, DateTimeKind.Utc) : timeUtc.ToUniversalTime();
            InputHash = inputHash;
            Scope = scope;
            Issues = issues;
            Outputs = outputs ?? new Dictionary<string, string>();
        }

        public DateTime TimeUtc { get; }
        public string InputHash { get; }
        public SettingsScope Scope { get; }
        public int Issues { get; }
        public IReadOnlyDictionary<string, string> Outputs { get; }

        // The last run of a button as the settings JSON of R367 keeps it, with the outputs of its run record, which lastRuns does not hold
        public static StepRun From(ButtonRun run, IReadOnlyDictionary<string, string> outputs)
        {
            DateTime time = DateTime.Parse(run.Time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return new StepRun(time, StepHash.Combine(run.InputHashes), run.Scope, run.Issues.Count, outputs);
        }
    }

    // R366. A step as it is now: its last real run or none, the one hash over its inputs now, none when an input is not chosen or
    // not on disk, and the SHA-256 of each output of the record found on disk
    public sealed class StepNow
    {
        public StepNow(StepRun lastRun, string inputHash, IReadOnlyDictionary<string, string> outputsOnDisk)
        {
            LastRun = lastRun;
            InputHash = inputHash;
            OutputsOnDisk = outputsOnDisk ?? new Dictionary<string, string>();
        }

        public StepRun LastRun { get; }
        public string InputHash { get; }
        public IReadOnlyDictionary<string, string> OutputsOnDisk { get; }
    }

    // R366, docs/design/03a-buttons-shared-and-1.md Steps and docs/design/13-workflow-changes.md Order. The line of each of the 9 buttons
    public static class StepStates
    {
        public const int Count = 9;
        private const int SetUp = 1;
        private const int Check = 4;
        private const int Report = 9;

        // Each step needs the one before, 9 Report needs 4 Check only (Q144) and 1 Set up site only the settings, given as 0
        public static int Needs(int step)
        {
            if (step < 1 || step > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, "the steps are 1 to " + Count);
            }
            return step == SetUp ? 0 : step == Report ? Check : step - 1;
        }

        // A step is done when its own run holds now, the step it needs is done and no step before it by number changed its inputs or
        // ran again since, so 5 run again or changed makes 9 run again though 9 needs only 4. Done with issues counts as done
        public static IReadOnlyList<StepLine> Of(bool settingsChosen, SettingsScope scopeNow, IReadOnlyDictionary<int, StepNow> steps)
        {
            foreach (int step in steps.Keys)
            {
                Needs(step);
            }
            var done = new bool[Count + 1];
            var lines = new List<StepLine>();
            for (int step = 1; step <= Count; step++)
            {
                StepNow now = At(steps, step);
                int need = Needs(step);
                done[step] = Holds(step, now, scopeNow) && (need == 0 || done[need]) && !LaterOrChangedBefore(step, now.LastRun, steps);
                if (done[step])
                {
                    int issues = now.LastRun.Issues;
                    lines.Add(issues == 0 ? new StepLine(StepStateKind.Done) : new StepLine(StepStateKind.DoneWithIssues, issues));
                }
                else if (need == 0)
                {
                    lines.Add(new StepLine(settingsChosen ? StepStateKind.Ready : StepStateKind.NeedsSettings));
                }
                else
                {
                    lines.Add(done[need] ? new StepLine(StepStateKind.Ready) : new StepLine(StepStateKind.NeedsStep, need));
                }
            }
            return lines;
        }

        private static StepNow At(IReadOnlyDictionary<int, StepNow> steps, int step)
        {
            return steps.TryGetValue(step, out StepNow now) && now != null ? now : new StepNow(null, null, null);
        }

        // The run of the step on the inputs, scope and outputs it has now
        private static bool Holds(int step, StepNow now, SettingsScope scopeNow)
        {
            StepRun run = now.LastRun;
            return run != null && !Changed(now)
                && ButtonScope.Key(step, run.Scope) == ButtonScope.Key(step, scopeNow)
                && run.Outputs.All(output => now.OutputsOnDisk.TryGetValue(output.Key, out string hash) && hash == output.Value);
        }

        private static bool Changed(StepNow now)
        {
            return now.LastRun != null && (now.InputHash == null || now.InputHash != now.LastRun.InputHash);
        }

        private static bool LaterOrChangedBefore(int step, StepRun run, IReadOnlyDictionary<int, StepNow> steps)
        {
            for (int before = 1; before < step; before++)
            {
                StepNow now = At(steps, before);
                if (now.LastRun != null && (Changed(now) || now.LastRun.TimeUtc > run.TimeUtc))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
