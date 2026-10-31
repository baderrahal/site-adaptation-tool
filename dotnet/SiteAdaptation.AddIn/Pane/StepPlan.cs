using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;

namespace SiteAdaptation.AddIn.Pane
{
    public enum StepKind
    {
        None,
        Done,
        Issues,
        Ready,
        NeedsStep,
        NeedsSettings,
    }

    public sealed class StepStatus
    {
        public StepStatus(StepKind kind, int value = 0)
        {
            Kind = kind;
            Value = value;
        }

        public StepKind Kind { get; }

        // The issues of Issues, the step of NeedsStep
        public int Value { get; }
    }

    // R372, R366. The step lines of the pane are the lines of StepStates of Common since join W9 of steps/lane-b.md, so the needs of
    // docs/design/13-workflow-changes.md Order, 9 Report needs 4 Check only (Q144), and the hash, scope and output checks are one
    // logic. This class only maps each line to the chip of the pane and finds the next step
    public static class StepPlan
    {
        public const int Count = StepStates.Count;

        public static int Needs(int number)
        {
            return StepStates.Needs(number);
        }

        // The lines of a document with no step run
        public static IReadOnlyList<StepStatus> None(bool settingsChosen)
        {
            return Of(settingsChosen, new SettingsScope(), new Dictionary<int, StepNow>());
        }

        public static IReadOnlyList<StepStatus> Of(bool settingsChosen, SettingsScope scope, IReadOnlyDictionary<int, StepNow> steps)
        {
            return StepStates.Of(settingsChosen, scope ?? new SettingsScope(), steps).Select(Status).ToList();
        }

        private static StepStatus Status(StepLine line)
        {
            switch (line.Kind)
            {
                case StepStateKind.Done: return new StepStatus(StepKind.Done);
                case StepStateKind.DoneWithIssues: return new StepStatus(StepKind.Issues, line.Value);
                case StepStateKind.Ready: return new StepStatus(StepKind.Ready);
                case StepStateKind.NeedsStep: return new StepStatus(StepKind.NeedsStep, line.Value);
                default: return new StepStatus(StepKind.NeedsSettings);
            }
        }

        // The next step is the first one that is Ready, none when no step is
        public static int Next(IReadOnlyList<StepStatus> steps)
        {
            for (int at = 0; at < steps.Count; at++)
            {
                if (steps[at].Kind == StepKind.Ready)
                {
                    return at + 1;
                }
            }
            return 0;
        }
    }
}
