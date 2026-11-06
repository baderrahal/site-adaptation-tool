using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.AddIn.Pane
{
    // R688. The one Build runs set up site, check, floors and ramps, levels, villas and walls one after another through the step code of version 1, each step a run
    // of its own button as a press of its row is. It stops only when a step did not end well, and says at which step and why. The closing message of each step
    // that ended well, most often a summary and no issue, is said in the line at the end. No Revit type, so a test drives it step by step
    public sealed class BuildChain
    {
        public static readonly int[] Order = { 1, 4, 5, 6, 7, 8 };

        // A step may wait on an earlier one more than once in a build, never on more than this many, so a build can never loop
        public const int MostAdded = 6;

        private readonly Func<int, string> name;
        private readonly Func<int, StepStatus> status;
        private readonly List<int> steps = new List<int>();
        private readonly List<string> messages = new List<string>();
        private int at = -1;
        private int added;

        public BuildChain(Func<int, string> stepName, Func<int, StepStatus> stepStatus)
        {
            name = stepName;
            status = stepStatus;
        }

        public bool Running { get { return at >= 0; } }

        // The first step, null when a build runs already. Set up site runs only when it has not run yet, as a run of it again makes the read and the review
        // of stage 1 out of date and asks to confirm nothing new
        public int? Start()
        {
            if (Running)
            {
                return null;
            }
            messages.Clear();
            steps.Clear();
            added = 0;
            steps.AddRange(Order.Where(step => step != 1 || !Ran(step)));
            at = 0;
            return Before(steps[at]);
        }

        // The one progress line while a step runs
        public string Progress
        {
            get { return Running ? TextTemplate.Fill(SiteBuildText.StepOf, (at + 1).ToString(CultureInfo.InvariantCulture), steps.Count.ToString(CultureInfo.InvariantCulture), name(steps[at])) : null; }
        }

        // A step of the build ended. False when it is no step of this build, else the line of card 2 and the next step, null when the build ended or stopped
        public bool Ended(int number, RunEnd end, string message, out int? next, out string line)
        {
            next = null;
            line = null;
            if (!Running || number != steps[at])
            {
                return false;
            }
            // Its work goes on outside Revit, the end comes later
            if (end == RunEnd.Started)
            {
                line = Progress;
                return true;
            }
            string step = name(number);
            if (end == RunEnd.Cancelled)
            {
                at = -1;
                line = TextTemplate.Fill(SiteBuildText.StepCancelled, step);
                return true;
            }
            if (end != RunEnd.Committed)
            {
                at = -1;
                line = TextTemplate.Fill(SiteBuildText.StepStopped, step, string.IsNullOrWhiteSpace(message) ? SiteBuildText.NoReason : message.Trim());
                return true;
            }
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(step + ": " + message.Trim());
            }
            at++;
            if (at >= steps.Count)
            {
                at = -1;
                line = string.Join(" ", new[] { TextTemplate.Fill(SiteBuildText.BuildDone, steps.Count.ToString(CultureInfo.InvariantCulture)) }.Concat(messages));
                return true;
            }
            next = Before(steps[at]);
            line = Progress;
            return true;
        }

        private bool Ran(int step)
        {
            StepKind kind = status(step).Kind;
            return kind == StepKind.Done || kind == StepKind.Issues;
        }

        // The step to run now: a step that waits on an earlier step runs that one first, as a run of an earlier step can leave a later one out of date
        private int Before(int step)
        {
            StepStatus now = status(step);
            while (now.Kind == StepKind.NeedsStep && now.Value >= 1 && now.Value < step && added < MostAdded)
            {
                added++;
                steps.Insert(at, now.Value);
                step = now.Value;
                now = status(step);
            }
            return steps[at];
        }
    }
}
