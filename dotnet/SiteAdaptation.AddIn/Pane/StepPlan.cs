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
    // step before it by the order of docs/design/13-workflow-changes.md, 9 Report needs 4 Check
    public static class StepPlan
    {
        public const int Count = 9;
        private const int Report = 9;
        private const int Check = 4;

        public static int Needs(int number)
        {
            if (number < 1 || number > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "the steps are 1 to " + Count);
            }
            return number == 1 ? 0 : number == Report ? Check : number - 1;
        }

        public static IReadOnlyList<StepStatus> Before5c(bool settingsChosen)
        {
            return From(settingsChosen, new Dictionary<int, StepStatus>());
        }

        // The step lines with the real runs of this session that committed, held in memory until R366 keeps them. A step done
        // stays Done or Done with its issues, one whose step before is done is Ready, else it needs the step before it, as the
        // mock shows 5 Needs step 4 while 4 is Ready
        public static IReadOnlyList<StepStatus> From(bool settingsChosen, IReadOnlyDictionary<int, StepStatus> done)
        {
            return Enumerable.Range(1, Count).Select(number => Line(number, settingsChosen, done)).ToList();
        }

        private static StepStatus Line(int number, bool settingsChosen, IReadOnlyDictionary<int, StepStatus> done)
        {
            if (done.TryGetValue(number, out StepStatus status))
            {
                return status;
            }
            if (number == 1)
            {
                return new StepStatus(settingsChosen ? StepKind.Ready : StepKind.NeedsSettings);
            }
            return done.ContainsKey(Needs(number)) ? new StepStatus(StepKind.Ready) : new StepStatus(StepKind.NeedsStep, Needs(number));
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
