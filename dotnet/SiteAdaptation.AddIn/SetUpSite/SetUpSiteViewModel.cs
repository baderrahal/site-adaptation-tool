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

        // R441. The ticks of Current by input and the rules profile chosen, kept across the views a dry run or a sheet gives back
        private readonly Dictionary<string, bool> ticks = new Dictionary<string, bool>(StringComparer.Ordinal);
        private int rulesIndex = -1;

        public SetUpSiteViewModel(SetUpSiteView view)
        {
            Load(view);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Badge { get; private set; }

        // R510. The line of a model with no site coordinates, as Common gives it
        public string NoCoordinatesLine => view.NoCoordinatesLine ?? "";

        public bool NoCoordinatesShown => NoCoordinatesLine.Length > 0;

        // R510. The place of the template, named as that
        public string TemplatePlaceLine => view.TemplatePlaceLine ?? "";

        public bool TemplatePlaceShown => TemplatePlaceLine.Length > 0;

        // R513. Why CAD gives no usable coordinates, as Common gives it
        public string NoSourceLine => view.NoSourceLine ?? "";

        public bool NoSourceShown => NoSourceLine.Length > 0;

        // R441, R513. The help says what the gate does, it waits for the site drawing only while CAD is or may be the source
        public string CurrentHelpLine => DrawingAsked ? SetUpSiteText.CurrentHelp : SetUpSiteText.CurrentHelpOther;

        // R512. Why the check against the position file did not run, as Common gives it
        public string CheckLine => view.CheckLine ?? "";

        public bool CheckLineShown => CheckLine.Length > 0;

        // Q422. The words of the window follow the source, a head that says CAD over a position file would say where the numbers do not come from
        public string IntroLine => BySource(SetUpSiteText.Intro, SetUpSiteText.IntroFile, SetUpSiteText.IntroTyped, SetUpSiteText.IntroNone);

        public string CheckTitle => BySource(SetUpSiteText.CheckTitleCad, SetUpSiteText.CheckTitleFile, SetUpSiteText.CheckTitleTyped, SetUpSiteText.CheckTitleNone);

        public string CheckColumn => BySource(SetUpSiteText.CompareCad, SetUpSiteText.CompareFile, SetUpSiteText.CheckColumnTyped, SetUpSiteText.CompareCad);

        private string BySource(string cad, string file, string typed, string none)
        {
            switch (view.PositionSource)
            {
                case SiteSources.Cad:
                    return cad;
                case SiteSources.PositionFile:
                    return file;
                case SiteSources.Typed:
                    return typed;
                default:
                    return none;
            }
        }

        // R512
        public IReadOnlyList<SourceRow> SourceRows => view.SourceRows;

        public bool SourceRowsShown => view.SourceRows.Count > 0;

        // R513. The three boxes as typed, kept across the views the buttons give back
        public string TypedEast { get; set; } = "";

        public string TypedNorth { get; set; } = "";

        public string TypedAngle { get; set; } = "";

        public bool TypedValues(out double east, out double north, out double angle)
        {
            north = 0.0;
            angle = 0.0;
            return Number(TypedEast, out east) && Number(TypedNorth, out north) && Number(TypedAngle, out angle);
        }

        private static bool Number(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public string SignsLine { get; private set; }

        public bool SignsShown => SignsLine.Length > 0;

        public string CountLine { get; private set; }

        // Before a dry run nothing is measured, so the count and the pass line wait for it
        public bool CountShown => view.DryRunDone;

        // 03a Second run, Q158: a view where nothing differs says so, and Apply marks the step done. It holds for the slope the view was made with
        public bool NothingShown => view.NothingDiffers && SameSlope;

        // What waits on a question, Plot on Q156, beside the line, so nothing differs never reads as a model complete
        public bool WaitingShown => view.Waiting.Count > 0;

        public string WaitingLine => string.Join(Environment.NewLine, view.Waiting);

        // The view holds for the slope it was made with. Another slope asks for a dry run, which Common makes for that slope
        private bool SameSlope => Math.Abs(view.RampSlope - SelectedSlope) < 1e-9;

        // Reviewer of Q158: the promise of Apply shows only beside an Apply that is on
        public string NothingLine => !NothingShown ? "" : CanApply ? SetUpSiteText.NothingDiffers : SetUpSiteText.NothingDiffersPlain;

        public string PassLine { get; private set; }

        public IReadOnlyList<SetUpRowViewModel> Now { get; private set; }

        public IReadOnlyList<SetUpRowViewModel> WillSet { get; private set; }

        public IReadOnlyList<CheckPointViewModel> CheckPoints { get; private set; }

        public IReadOnlyList<string> RampChoices { get; private set; }

        public string StopsLine { get; private set; }

        public bool StopsShown => StopsLine.Length > 0;

        // No stop is found, the slope is the one the view was made with, and nothing differs (Q158) or the dry run has filled the view
        public bool CanApply => SiteTicked && view.Stops.Count == 0 && SameSlope && (view.NothingDiffers || (view.DryRunDone && (!dryRunSlope.HasValue || Math.Abs(dryRunSlope.Value - SelectedSlope) < 1e-9)));

        // R766, F2 of run 1. Why a grey Apply waits, beside it in the foot: a dry run first, then the tick of the site drawing. A stop says
        // its own words in the red box above, so the line is empty then, as it is while Apply is on
        public string ApplyWaitLine
        {
            get
            {
                // Review 1 of PR 2: a stop of a callback stands in StopsLine too, and another slope asks for a dry run even where nothing differed
                if (CanApply || view.Stops.Count > 0 || StopsShown)
                {
                    return string.Empty;
                }
                bool dryDone = SameSlope && (view.NothingDiffers || (view.DryRunDone && (!dryRunSlope.HasValue || Math.Abs(dryRunSlope.Value - SelectedSlope) < 1e-9)));
                return !dryDone ? SetUpSiteText.DryRunFirst : !SiteTicked ? SetUpSiteText.TickSiteFirst : string.Empty;
            }
        }

        public IReadOnlyList<CurrentFileViewModel> CurrentFiles { get; private set; }

        public bool CurrentShown => view.CurrentFiles != null;

        // Breaker of SCAN-UI: with no site drawing to tick Apply can never turn on, so the section says why
        public bool NoSiteShown => view.CurrentFiles != null && DrawingAsked && !CurrentFiles.Any(row => row.Site);

        // R441, R513. Only CAD reads the site drawing, so a position file or typed values never wait for its tick, else no drawing in the setup would be a dead end
        private bool DrawingAsked => view.PositionSource != SiteSources.PositionFile && view.PositionSource != SiteSources.Typed;

        // Apply stays grey until the site drawing is ticked Current (R441)
        public bool SiteTicked => view.CurrentFiles == null || !DrawingAsked || Common.SetUp.CurrentFiles.CanApply(CurrentFiles.Select(row => new CurrentFileRow { Site = row.Site, Current = row.Current }));

        public IReadOnlyList<string> RulesChoices { get; private set; }

        public int RulesIndex
        {
            get { return rulesIndex; }
            set
            {
                if (value >= 0 && value < RulesChoices.Count && value != rulesIndex)
                {
                    rulesIndex = value;
                    Changed(nameof(RulesIndex));
                }
            }
        }

        // The ticked inputs and the rules file chosen, what Apply hands on
        public List<string> TickedInputs => CurrentFiles.Where(row => row.Current).Select(row => row.Input).ToList();

        public string ChosenRules => rulesIndex >= 0 && rulesIndex < view.RulesProfiles.Count ? view.RulesProfiles[rulesIndex].Path : null;

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
            List<CurrentFileRow> files = view.CurrentFiles ?? new List<CurrentFileRow>();
            foreach (CurrentFileRow row in files.Where(row => !ticks.ContainsKey(row.Input)))
            {
                ticks[row.Input] = row.Current;
            }
            CurrentFiles = files.Select(row => new CurrentFileViewModel(this, row, ticks[row.Input])).ToList();
            RulesChoices = view.RulesProfiles.Select(choice => choice.Name).ToList();
            if (rulesIndex < 0 || rulesIndex >= RulesChoices.Count)
            {
                rulesIndex = view.RulesProfiles.FindIndex(choice => string.Equals(choice.Path, view.RulesFile, StringComparison.OrdinalIgnoreCase));
            }
            Changed(string.Empty);
        }

        // A tick of Current changed, so Apply may turn on or off
        public void Ticked(string input, bool current)
        {
            ticks[input] = current;
            Changed(nameof(SiteTicked));
            Changed(nameof(CanApply));
            Changed(nameof(NothingLine));
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
            Changed(nameof(NothingLine));
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
            // R513. Why no source works stands in the section Position source beside the ways out of it, so it is not said again here. It still keeps Apply off through view.Stops
            IEnumerable<string> others = view.Stops.Where(stop => string.IsNullOrEmpty(view.NoSourceLine) || !view.NoSourceLine.Contains(stop));
            return string.Join(Environment.NewLine, others.Concat(problem.Length > 0 ? new[] { problem } : new string[0]));
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
            if (name == nameof(CanApply))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ApplyWaitLine)));
            }
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

    // R441. One file of the section Current files, its tick handed back to the window model
    public sealed class CurrentFileViewModel : INotifyPropertyChanged
    {
        private readonly SetUpSiteViewModel model;
        private bool current;

        public CurrentFileViewModel(SetUpSiteViewModel model, CurrentFileRow row, bool current)
        {
            this.model = model;
            Input = row.Input;
            Label = SetUpSiteText.InputLabels.TryGetValue(row.Input, out string label) ? label : row.Input;
            FileName = row.FileName;
            Site = row.Site;
            this.current = current;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Input { get; }

        public string Label { get; }

        public string FileName { get; }

        public bool Site { get; }

        public string SiteLine => Site ? SetUpSiteText.SiteDrawing : "";

        public bool Current
        {
            get { return current; }
            set
            {
                if (value != current)
                {
                    current = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
                    model.Ticked(Input, value);
                }
            }
        }
    }
}
