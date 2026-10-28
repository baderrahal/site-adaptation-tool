using System;
using System.Collections.Generic;
using System.Linq;

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

    // R372. The step lines of the pane until R366, the step state of 4h, comes in. CHOSEN for 5a, joined with R366 at 5c: no step
    // is ever done, so button 1 is Ready once a settings file is chosen and Needs settings before, and every other button needs the
    // step before it by the order of docs/design/03a-buttons-shared-and-1.md, 8 needs 3
    public static class StepPlan
    {
        public const int Count = 8;

        public static int Needs(int number)
        {
            if (number < 1 || number > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "the steps are 1 to 8");
            }
            return number == 1 ? 0 : number == 8 ? 3 : number - 1;
        }

        public static IReadOnlyList<StepStatus> Before5c(bool settingsChosen)
        {
            return Enumerable.Range(1, Count)
                .Select(number => number == 1
                    ? new StepStatus(settingsChosen ? StepKind.Ready : StepKind.NeedsSettings)
                    : new StepStatus(StepKind.NeedsStep, Needs(number)))
                .ToList();
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
