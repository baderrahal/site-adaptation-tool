using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Update;

namespace SiteAdaptation.AddIn.Update
{
    // R402, 5f. What the window Update from report binds to. It holds no Revit type and no rule: the list is the one the worker gave, the ticks,
    // the counts and the chips are answered here so a test needs no click. A change that can not be applied is never ticked
    public sealed class UpdateFromReportViewModel : INotifyPropertyChanged
    {
        private UpdateList list = new UpdateList();
        private readonly Dictionary<UpdateChange, UpdateRowViewModel> rows = new Dictionary<UpdateChange, UpdateRowViewModel>();
        private string chip = UpdateText.All;
        private string problem = "";
        private string dry = "";

        public event PropertyChangedEventHandler PropertyChanged;

        public string FilesLine { get; private set; } = "";

        public IReadOnlyList<TileViewModel> Tiles { get; private set; } = new TileViewModel[0];

        public IReadOnlyList<ChipViewModel> Chips { get; private set; } = new ChipViewModel[0];

        // The rows of the chip chosen, in the order of the list
        public IReadOnlyList<UpdateRowViewModel> Rows { get; private set; } = new UpdateRowViewModel[0];

        // Every row of the list, whatever chip shows
        public IEnumerable<UpdateRowViewModel> AllRows => list.Changes.Select(change => rows[change]);

        public bool ShowsRefusal => !string.IsNullOrEmpty(list.Refusal);

        public string RefusalLine => list.Refusal ?? "";

        public bool ShowsLacks => LacksLine.Length > 0;

        public string LacksLine => string.Join(Environment.NewLine, list.Lacks);

        public string ProblemLine => problem;

        public bool ProblemShown => problem.Length > 0;

        public string DryLine => dry;

        public bool DryShown => dry.Length > 0;

        // Every ticked change of the list, whatever chip shows, in the order of the list
        public IReadOnlyList<UpdateChange> TickedChanges => list.Changes.Where(change => rows[change].Apply).ToList();

        public int ApplyCount => TickedChanges.Count;

        public string ApplyLabel => TextTemplate.Fill(UpdateFromReportText.ApplyN, ApplyCount);

        private bool Loaded => FilesLine.Length > 0 && !ShowsRefusal;

        public bool CanApply => Loaded && ApplyCount > 0;

        public bool CanDryRun => Loaded && list.Changes.Count > 0;

        public void Load(UpdateList next, IReadOnlyList<string> files)
        {
            list = next ?? new UpdateList();
            FilesLine = string.Join(", ", files.Select(System.IO.Path.GetFileName));
            problem = "";
            dry = "";
            rows.Clear();
            foreach (UpdateChange change in list.Changes)
            {
                rows[change] = new UpdateRowViewModel(this, change);
            }
            chip = UpdateText.All;
            Refresh();
        }

        public void SelectChip(string label)
        {
            chip = label;
            dry = "";
            Refresh();
        }

        public void Fail(string reason)
        {
            problem = reason ?? "";
            Changed(nameof(ProblemLine));
            Changed(nameof(ProblemShown));
        }

        public void ClearProblem()
        {
            problem = "";
            Changed(nameof(ProblemLine));
            Changed(nameof(ProblemShown));
        }

        public void ShowDry(UpdateApplied applied)
        {
            var lines = new List<string> { TextTemplate.Fill(UpdateFromReportText.DryDone, applied.Entries.Count, applied.Replaced.Count) };
            lines.AddRange(applied.NotApplied);
            dry = string.Join(Environment.NewLine, lines);
            Changed(nameof(DryLine));
            Changed(nameof(DryShown));
        }

        // A tick or a choice of a row changes the count and the dry run line no longer holds
        internal void Ticked()
        {
            dry = "";
            Changed(nameof(ApplyLabel));
            Changed(nameof(CanApply));
            Changed(nameof(CanDryRun));
            Changed(nameof(DryLine));
            Changed(nameof(DryShown));
        }

        private void Refresh()
        {
            Dictionary<string, int> tiles = list.Tiles();
            Dictionary<string, int> chips = list.Chips();
            Tiles = UpdateText.Tiles.Select(label => new TileViewModel(label, tiles[label])).ToList();
            Chips = UpdateText.Chips.Select(label => new ChipViewModel(this, label, chips[label], label == chip)).ToList();
            Rows = list.Chip(chip).Select(change => rows[change]).ToList();
            Changed(string.Empty);
        }

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class TileViewModel
    {
        public TileViewModel(string label, int count)
        {
            Label = label;
            Count = count;
        }

        public string Label { get; }

        public int Count { get; }
    }

    public sealed class ChipViewModel
    {
        private readonly UpdateFromReportViewModel owner;

        public ChipViewModel(UpdateFromReportViewModel owner, string label, int count, bool selected)
        {
            this.owner = owner;
            Label = label;
            Count = count;
            Selected = selected;
        }

        public string Label { get; }

        public int Count { get; }

        public bool Selected { get; }

        public string Text => Label + " " + Count.ToString(CultureInfo.InvariantCulture);

        public void Choose()
        {
            owner.SelectChip(Label);
        }
    }

    // One change of the list. Keep the model and Apply anyway are the two sides of its tick, for a change that breaks a manual rule
    public sealed class UpdateRowViewModel : INotifyPropertyChanged
    {
        private readonly UpdateFromReportViewModel owner;
        private bool apply;
        private string note = "";

        public UpdateRowViewModel(UpdateFromReportViewModel owner, UpdateChange change)
        {
            this.owner = owner;
            Change = change;
            apply = change.Ticked && change.Invalid == null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public UpdateChange Change { get; }

        public string Plot => Change.Plot;

        public string Field => Change.Field;

        public string InModel => Show(Change.InModel);

        public string InExcel => Show(Change.InExcel);

        public string RuleCheck => Change.RuleCheck;

        // The ids of the rules the rule column names, for its tooltip (12-ui-spec.md)
        public string RuleIds => Change.RuleIds;

        public bool BreaksManual => Change.ManualRules.Count > 0;

        public bool BreaksRule => Change.BreaksARule;

        public bool CanTick => Change.Invalid == null;

        public bool Apply
        {
            get { return apply; }
            set
            {
                bool next = value && CanTick;
                if (next == apply)
                {
                    return;
                }
                apply = next;
                Changed(nameof(Apply));
                Changed(nameof(KeepModel));
                Changed(nameof(ApplyAnyway));
                owner.Ticked();
            }
        }

        public bool KeepModel
        {
            get { return !apply; }
            set
            {
                if (value)
                {
                    Apply = false;
                }
            }
        }

        public bool ApplyAnyway
        {
            get { return apply; }
            set
            {
                if (value)
                {
                    Apply = true;
                }
            }
        }

        public string Note
        {
            get { return note; }
            set
            {
                note = value ?? "";
                Changed(nameof(Note));
            }
        }

        // The columns show El @ Entry at 4 decimals, FFL at 3 and Steps whole as R402 gives them, a flag as Excel shows it
        private string Show(object value)
        {
            switch (value)
            {
                case null:
                    return "";
                case double number:
                    return Field == UpdateText.Entry ? number.ToString("0.0000", CultureInfo.InvariantCulture) : Field == UpdateText.Ffl ? number.ToString("0.000", CultureInfo.InvariantCulture) : number.ToString(CultureInfo.InvariantCulture);
                case bool flag:
                    return flag.ToString().ToUpperInvariant();
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
