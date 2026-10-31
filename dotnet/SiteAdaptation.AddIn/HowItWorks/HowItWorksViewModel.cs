using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.AddIn.Pane;

namespace SiteAdaptation.AddIn.HowItWorks
{
    public sealed class HowRow
    {
        public HowRow(int number, string what, string needs, string gives, string button)
        {
            Number = number;
            What = what;
            Needs = needs;
            Gives = gives;
            Button = button;
        }

        public int Number { get; }
        public string What { get; }
        public string Needs { get; }
        public string Gives { get; }
        public string Button { get; }
    }

    // Lane C NEWPROJ-UI. The page How it works: one row per step of StepPlan in the order of the ribbon. It reads nothing and writes nothing,
    // Open New project ends it with OpenNewProject set, which the pane host takes to open New project
    public sealed class HowItWorksViewModel
    {
        // NewProjectTests holds the three columns of HowItWorksText at one text per step
        // canOpenNew is false with no document open, since New project sets up the document shown
        public HowItWorksViewModel(bool canOpenNew = true)
        {
            CanOpenNew = canOpenNew;
            Rows = RibbonLayout.Buttons.Select(button => new HowRow(button.Number, HowItWorksText.What[button.Number - 1], HowItWorksText.Needs[button.Number - 1],
                HowItWorksText.Gives[button.Number - 1], button.Label)).ToList();
            Count = TextTemplate.Fill(HowItWorksText.Count, StepPlan.Count);
            CountLine = TextTemplate.Fill(HowItWorksText.CountLine, StepPlan.Count);
        }

        public IReadOnlyList<HowRow> Rows { get; }
        public string Count { get; }
        public string CountLine { get; }
        public bool CanOpenNew { get; }
        public bool OpenNewProject { get; private set; }

        public event Action Finished;

        public void Close()
        {
            Finished?.Invoke();
        }

        public void OpenNew()
        {
            if (!CanOpenNew)
            {
                return;
            }
            OpenNewProject = true;
            Finished?.Invoke();
        }
    }
}
