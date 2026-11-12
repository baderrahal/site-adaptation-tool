using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.AddIn.Pane;

namespace SiteAdaptation.AddIn.HowItWorks
{
    public sealed class HowRow
    {
        public HowRow(int number, string what, string needs, string gives, string button, string card = "")
        {
            Card = card ?? string.Empty;
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

        // R764. The head of the card the row opens, empty on a row that is not the first step of its card
        public string Card { get; }
        public bool OpensCard { get { return Card.Length > 0; } }
    }

    // Lane C NEWPROJ-UI. The page How it works: one row per step of StepPlan in its order. It reads nothing and writes nothing,
    // Open New project ends it with OpenNewProject set, which the pane host takes to open New project
    public sealed class HowItWorksViewModel
    {
        // NewProjectTests holds the three columns of HowItWorksText at one text per step
        // canOpenNew is false with no document open, since New project sets up the document shown
        public HowItWorksViewModel(bool canOpenNew = true)
        {
            CanOpenNew = canOpenNew;
            Rows = RibbonLayout.Buttons.Select(button => new HowRow(button.Number, HowItWorksText.What[button.Number - 1], HowItWorksText.Needs[button.Number - 1],
                HowItWorksText.Gives[button.Number - 1], ButtonOf(button.Number), CardOf(button.Number))).ToList();
            Count = TextTemplate.Fill(HowItWorksText.Count, StepPlan.Count);
            CountLine = TextTemplate.Fill(HowItWorksText.CountLine, Rows.Select(row => row.Button).Distinct().Count());
        }

        // R845. The button of the card that runs a step, in the words of the pane: steps 1 and 3 have their own on card 1, step 2 runs after
        // Choose project folder, steps 4 to 8 by Build, step 9 by Report
        public static string ButtonOf(int step)
        {
            if (step == StagesViewModel.SetUpStep || step == StagesViewModel.ReviewStep)
            {
                return RibbonLayout.Button(step).Label;
            }
            return CardButtons[StageStatus.StageOf(step) - 1];
        }

        // R764. The cards of the pane in 3 stages, their titles and main buttons in the words of the pane
        private static readonly string[] CardTitles = { StageText.Stage1, StageText.Stage2, StageText.Stage3 };
        private static readonly string[] CardButtons = { StageText.ChooseFolder, StageText.Build, StageText.Report };

        // The head of the card a step opens, empty when the step is not the first of its card
        public static string CardOf(int step)
        {
            int card = StageStatus.StageOf(step);
            if (card == 0 || StageStatus.Steps[card - 1][0] != step)
            {
                return string.Empty;
            }
            return TextTemplate.Fill(HowItWorksText.CardHead, card, CardTitles[card - 1], CardButtons[card - 1]);
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
