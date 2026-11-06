using System;
using System.Collections.Generic;
using System.Globalization;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.AddIn.Pane
{
    // R688. The one Build runs set up site, check, floors and ramps, levels, villas and walls one after another through the step code of version 1, each step a run
    // of its own button as a press of its row is. It stops only when a step did not end well, and says at which step and why. The message of a step that ended well
    // goes to the issues of the build. No Revit type, so a test drives it step by step
    public sealed class BuildChain
    {
        public static readonly int[] Order = { 1, 4, 5, 6, 7, 8 };

        private readonly Func<int, string> name;
        private readonly List<string> issues = new List<string>();
        private int at = -1;

        public BuildChain(Func<int, string> stepName)
        {
            name = stepName;
        }

        public bool Running { get { return at >= 0; } }

        // The step that runs now, null when none
        public int? Current { get { return Running ? Order[at] : (int?)null; } }

        public IReadOnlyList<string> Issues { get { return issues; } }

        // The first step. A second Build while one runs starts nothing
        public int? Start()
        {
            if (Running)
            {
                return null;
            }
            issues.Clear();
            at = 0;
            return Order[at];
        }

        // The one progress line while a step runs
        public string Progress
        {
            get { return Running ? TextTemplate.Fill(SiteBuildText.StepOf, (at + 1).ToString(CultureInfo.InvariantCulture), Order.Length.ToString(CultureInfo.InvariantCulture), name(Order[at])) : null; }
        }

        // A step of the chain ended. False when it is no step of this chain, else the line of card 2 and the next step, null when the build ended or stopped
        public bool Ended(int number, RunEnd end, string message, out int? next, out string line)
        {
            next = null;
            line = null;
            if (!Running || number != Order[at])
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
                issues.Add(step + ": " + message.Trim());
            }
            at++;
            if (at >= Order.Length)
            {
                at = -1;
                line = TextTemplate.Fill(SiteBuildText.BuildDone, Order.Length.ToString(CultureInfo.InvariantCulture), issues.Count.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            next = Order[at];
            line = Progress;
            return true;
        }
    }
}
