using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.AddIn.SetUpSite
{
    // R373, 7.3 of docs/design/12-ui-spec.md. What the window binds to. It holds no Revit type and no rule, the view it is given
    // by Common is shown as it is, in the order of the mock, with the three rows the changes of Bader of 28 Oct 2026 set
    public sealed class SetUpSiteViewModel : INotifyPropertyChanged
    {
        private SetUpSiteView view = new SetUpSiteView();
        private int rampIndex;
        private string problem = "";
        private double? dryRunSlope;

        public SetUpSiteViewModel(SetUpSiteView view)
        {
            Load(view);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Badge { get; private set; }

        public string SignsLine { get; private set; }

        public bool SignsShown => SignsLine.Length > 0;

        public string CountLine { get; private set; }

        // Before a dry run nothing is measured, so the count and the pass line wait for it
        public bool CountShown => view.DryRunDone;

        // 03a Second run: a view where nothing differs says so, and Apply stays off. It holds for the slope the view was made with
        public bool NothingShown => view.NothingDiffers && SameSlope;

        // What waits on a question, Plot on Q156, beside the line, so nothing differs never reads as a model complete
        public bool WaitingShown => view.Waiting.Count > 0;

        public string WaitingLine => string.Join(Environment.NewLine, view.Waiting);

        // The view holds for the slope it was made with. Another slope asks for a dry run, which Common makes for that slope
        private bool SameSlope => Math.Abs(view.RampSlope - SelectedSlope) < 1e-9;

        public string NothingLine => NothingShown ? SetUpSiteText.NothingDiffers : "";

        public string PassLine { get; private set; }

        public IReadOnlyList<SetUpRowViewModel> Now { get; private set; }

        public IReadOnlyList<SetUpRowViewModel> WillSet { get; private set; }

        public IReadOnlyList<CheckPointViewModel> CheckPoints { get; private set; }

        public IReadOnlyList<string> RampChoices { get; private set; }

        public string StopsLine { get; private set; }

        public bool StopsShown => StopsLine.Length > 0;

        // The dry run has filled the view, no stop is found, something differs, and the slope is the one the view was made with
        public bool CanApply => view.Stops.Count == 0 && view.DryRunDone && !view.NothingDiffers && SameSlope && (!dryRunSlope.HasValue || Math.Abs(dryRunSlope.Value - SelectedSlope) < 1e-9);

        public int RampIndex
        {
            get { return rampIndex; }
            set
            {
                if (value >= 0 && value < RampChoices.Count && value != rampIndex)
                {
                    rampIndex = value;
                    Changed(nameof(RampIndex));
                    Changed(nameof(CanApply));
                    Changed(nameof(NothingShown));
                    Changed(nameof(NothingLine));
                }
            }
        }

        // The slope chosen in percent, the one of the view while it has no choices
        public double SelectedSlope => view.RampSlopeChoices.Count == 0 ? view.RampSlope : view.RampSlopeChoices[rampIndex];

        // The sheet chosen by Browse, null while the pane's own stays
        public string ChosenSheet { get; set; }

        public void Load(SetUpSiteView next)
        {
            view = next ?? new SetUpSiteView();
            Badge = view.ExistingSite ? SetUpSiteText.BadgeExisting : SetUpSiteText.BadgeEmpty;
            SignsLine = string.Join(", ", view.Signs.Where(sign => !string.IsNullOrEmpty(sign)));
            CountLine = view.DryRunDone ? TextTemplate.Fill(SetUpSiteText.CountOf, view.Passing, view.CheckPoints.Count) : "";
            PassLine = view.DryRunDone ? TextTemplate.Fill(SetUpSiteText.PassLimit, view.PassLimitMm, view.Passing, view.CheckPoints.Count) : "";
            RampChoices = view.RampSlopeChoices.Select(Percent).ToList();
            rampIndex = Math.Max(0, view.RampSlopeChoices.FindIndex(choice => Math.Abs(choice - view.RampSlope) < 1e-9));
            Now = Ordered(view.Now, SetUpSiteText.NowOrder, false);
            WillSet = Ordered(view.WillSet, SetUpSiteText.WillOrder, true);
            CheckPoints = view.CheckPoints.Select(point => new CheckPointViewModel(point)).ToList();
            StopsLine = StopsText();
            Changed(string.Empty);
        }

        // Keeps the slope the user chose across the view that a dry run or a sheet gives back
        public void Keep(double slope)
        {
            int at = view.RampSlopeChoices.FindIndex(choice => Math.Abs(choice - slope) < 1e-9);
            if (at >= 0)
            {
                rampIndex = at;
                Changed(nameof(RampIndex));
            }
        }

        // A callback that threw leaves the window open with its reason under the stops
        public void Fail(string reason)
        {
            problem = reason ?? "";
            StopsLine = StopsText();
            Changed(nameof(StopsLine));
            Changed(nameof(StopsShown));
        }

        // The slope a view was filled with, so a change of the slope after it asks for a new dry run
        public void MarkDry(double slope)
        {
            dryRunSlope = slope;
            Changed(nameof(CanApply));
        }

        public void ClearProblem()
        {
            problem = "";
        }

        public static string Percent(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private string StopsText()
        {
            return string.Join(Environment.NewLine, view.Stops.Concat(problem.Length > 0 ? new[] { problem } : new string[0]));
        }

        private List<SetUpRowViewModel> Ordered(List<SetUpRow> rows, string[] order, bool will)
        {
            var made = rows.Select(row => new SetUpRowViewModel(this, row)).ToList();
            if (will)
            {
                // The values are Common's as given, the Floor types text and the sheet line with its count among them (7.3). The
                // window only adds the Browse of the sheet row and the dropdown of the Ramp slope row
                foreach (SetUpRowViewModel row in made)
                {
                    row.IsSheet = row.Label == SetUpSiteText.SharedParameters;
                    row.IsRamp = row.Label == SetUpSiteText.RampSlope;
                }
            }
            return made.Select((row, at) => new { row, at })
                .OrderBy(item => Rank(item.row.Label, order)).ThenBy(item => item.at)
                .Select(item => item.row).ToList();
        }

        private static int Rank(string label, string[] order)
        {
            int at = Array.IndexOf(order, label);
            return at < 0 ? order.Length : at;
        }

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class SetUpRowViewModel
    {
        public SetUpRowViewModel(SetUpSiteViewModel model, SetUpRow row)
        {
            Model = model;
            Label = row.Label;
            Value = row.Value;
            Differs = row.Differs;
        }

        // The window model, for the dropdown of the row Ramp slope
        public SetUpSiteViewModel Model { get; }

        public string Label { get; }

        public string Value { get; set; }

        public bool Differs { get; }

        public bool IsRamp { get; set; }

        // The row Shared parameters, which gets Browse beside Common's sheet line
        public bool IsSheet { get; set; }
    }

    public sealed class CheckPointViewModel
    {
        public CheckPointViewModel(CheckPointRow row)
        {
            Point = row.Point;
            Cad = row.Cad;
            Revit = row.Revit ?? "";
            Diff = row.DiffMm.HasValue ? row.DiffMm.Value.ToString("0.00", CultureInfo.InvariantCulture) : "";
            Passes = row.DiffMm.HasValue && row.Pass;
            Fails = row.DiffMm.HasValue && !row.Pass;
        }

        public string Point { get; }

        public string Cad { get; }

        public string Revit { get; }

        public string Diff { get; }

        public bool Passes { get; }

        public bool Fails { get; }
    }
}
