using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.AddIn.ReviewData
{
    // R406, docs/design/13-workflow-changes.md Review data (5c). What the window binds to. It holds no Revit type and no rule, the state
    // of Common is shown as it is. A value only the plan of 4 Check or the site file gives reads "not read yet" until they exist
    public sealed class ReviewDataViewModel : INotifyPropertyChanged
    {
        private readonly ReviewView view;
        private ReviewChip chip = ReviewChip.All;
        private string find = "";
        private ReviewRowViewModel selected;
        private string message = "";
        private string note = "";

        public ReviewDataViewModel(ReviewView view)
        {
            this.view = view;
            Rows = new List<ReviewRowViewModel>();
            Chips = new List<ChipViewModel>();
            Corners = new List<CornerViewModel>();
            Refresh();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ReviewState State => view.State;

        public string Title => TextTemplate.Fill(ReviewDataText.Title, view.Nbh, view.State.Total);

        public string ReadFromLine => TextTemplate.Fill(ReviewDataText.ReadFrom, string.Join(", ", view.Sources));

        public string CheckedLine => TextTemplate.Fill(ReviewDataText.CheckedLine, view.State.CheckedCount, view.State.Total);

        public IReadOnlyList<ReviewRowViewModel> Rows { get; private set; }

        public IReadOnlyList<ChipViewModel> Chips { get; private set; }

        public IReadOnlyList<CornerViewModel> Corners { get; private set; }

        public ReviewChip Chip => chip;

        public string Find
        {
            get { return find; }
            set
            {
                find = value ?? "";
                Refresh();
            }
        }

        public ReviewRowViewModel Selected
        {
            get { return selected; }
            set
            {
                if (!ReferenceEquals(selected, value))
                {
                    selected = value;
                    note = selected == null ? "" : view.State.NoteOf(selected.PlotNo);
                    Corners = selected == null ? new List<CornerViewModel>() : selected.Row.Corners.Select(corner => new CornerViewModel(corner)).ToList();
                    Changed(string.Empty);
                }
            }
        }

        public bool HasSelected => selected != null;

        public bool NoneSelected => selected == null;

        public string PickText => ReviewDataText.PickAPlot;

        public string ManualRuleLine => selected == null ? "" : TextTemplate.Fill(ReviewDataText.ManualRule, selected.Row.RuleId);

        public string EarlierNoteLine
        {
            get
            {
                string earlier = selected == null ? "" : view.State.EarlierNoteOf(selected.PlotNo);
                return earlier.Length == 0 ? "" : TextTemplate.Fill(ReviewDataText.EarlierNote, earlier);
            }
        }

        public bool EarlierNoteShown => EarlierNoteLine.Length > 0;

        public string SelectedPlot => selected == null ? "" : selected.PlotNo;

        public string SideCadLabel => selected == null ? "" : selected.CadLabel;

        public string SideNbh => selected == null ? "" : selected.Row.Nbh;

        public string SideCluster => selected == null ? "" : selected.Cluster;

        public string SideMirror => selected == null ? "" : selected.Mirror;

        public string SideFrontEdge => selected == null ? "" : selected.Row.FrontEdge.HasValue ? TextTemplate.Fill(ReviewDataText.Edge, selected.Row.FrontEdge.Value) : ReviewDataText.NotReadYet;

        public string SideRoad => selected == null ? "" : selected.RoadAtFront;

        public bool IsOk => selected != null && view.State.StatusOf(selected.PlotNo) == ReviewStatus.Ok;

        public bool IsWrong => selected != null && view.State.StatusOf(selected.PlotNo) == ReviewStatus.Wrong;

        // The note of the selected plot. While the plot has a check, an edit of the note goes into that check
        public string Note
        {
            get { return note; }
            set
            {
                note = value ?? "";
                if (selected != null && view.State.StatusOf(selected.PlotNo) != ReviewStatus.NotChecked)
                {
                    Mark(view.State.StatusOf(selected.PlotNo) == ReviewStatus.Wrong ? ReviewCheckValue.Wrong : ReviewCheckValue.Ok);
                }
            }
        }

        public string Message
        {
            get { return message; }
            set
            {
                message = value ?? "";
                Changed(nameof(Message));
                Changed(nameof(MessageShown));
            }
        }

        public bool MessageShown => message.Length > 0;

        public bool NotSavedShown => view.State.Changed;

        public string NotSavedText => ReviewDataText.NotSavedYet;

        public void SetChip(ReviewChip next)
        {
            chip = next;
            Refresh();
        }

        // Marks the selected plot OK or Wrong with the note as it stands
        public void Mark(string check)
        {
            if (selected == null)
            {
                return;
            }
            view.State.Mark(selected.PlotNo, check, note);
            selected.Update(view.State);
            Changed(nameof(CheckedLine));
            Changed(nameof(IsOk));
            Changed(nameof(IsWrong));
            Changed(nameof(EarlierNoteLine));
            Changed(nameof(EarlierNoteShown));
            Changed(nameof(NotSavedShown));
            RefreshChips();
        }

        public void Saved()
        {
            Message = ReviewDataText.ReviewSaved;
            Changed(nameof(NotSavedShown));
        }

        // The rows the chip and the find box pick. The selected plot stays selected only while it is among them
        private void Refresh()
        {
            Rows = view.State.Filter(chip, find).Select(row => new ReviewRowViewModel(row, view.State)).ToList();
            ReviewRowViewModel keep = selected == null ? null : Rows.FirstOrDefault(row => row.PlotNo == selected.PlotNo);
            RefreshChips();
            selected = null;
            Selected = keep;
            Changed(nameof(Rows));
        }

        private void RefreshChips()
        {
            Chips = new[] { ReviewChip.All, ReviewChip.NotChecked, ReviewChip.Ok, ReviewChip.Wrong }.Select(item => new ChipViewModel(item, Label(item), view.State.CountOf(item), item == chip)).ToList();
            Changed(nameof(Chips));
        }

        private static string Label(ReviewChip item)
        {
            switch (item)
            {
                case ReviewChip.NotChecked: return ReviewDataText.NotChecked;
                case ReviewChip.Ok: return ReviewDataText.Ok;
                case ReviewChip.Wrong: return ReviewDataText.Wrong;
                default: return ReviewDataText.All;
            }
        }

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class ChipViewModel
    {
        public ChipViewModel(ReviewChip chip, string label, int count, bool active)
        {
            Chip = chip;
            Text = label + " " + count.ToString(CultureInfo.InvariantCulture);
            Active = active;
        }

        public ReviewChip Chip { get; }

        public string Text { get; }

        public bool Active { get; }
    }

    public sealed class ReviewRowViewModel : INotifyPropertyChanged
    {
        public ReviewRowViewModel(ReviewRow row, ReviewState state)
        {
            Row = row;
            Update(state);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ReviewRow Row { get; }

        public string PlotNo => Row.PlotNo;

        public string CadLabel => Row.CadLabel;

        public string VillaType => Row.VillaType ?? ReviewDataText.NotReadYet;

        public string Mirror => Row.Mirror.HasValue ? (Row.Mirror.Value ? ReviewDataText.Yes : ReviewDataText.No) : ReviewDataText.NotReadYet;

        public string Cluster => Row.Cluster ?? ReviewDataText.NotReadYet;

        public string Area => Row.AreaM2.ToString("0.00", CultureInfo.InvariantCulture);

        public string RoadAtFront => Row.RoadAtFront ?? ReviewDataText.NotReadYet;

        // The manual rule of the row as the tooltip of its plot number
        public string RuleTip => TextTemplate.Fill(ReviewDataText.ManualRule, Row.RuleId);

        public string Check { get; private set; }

        public bool IsOk { get; private set; }

        public bool IsWrong { get; private set; }

        public void Update(ReviewState state)
        {
            ReviewStatus status = state.StatusOf(Row.PlotNo);
            Check = status == ReviewStatus.Ok ? ReviewDataText.Ok : status == ReviewStatus.Wrong ? ReviewDataText.Wrong : ReviewDataText.NotChecked;
            IsOk = status == ReviewStatus.Ok;
            IsWrong = status == ReviewStatus.Wrong;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public sealed class CornerViewModel
    {
        public CornerViewModel(ReviewCorner corner)
        {
            Name = TextTemplate.Fill(ReviewDataText.Corner, corner.Number);
            X = corner.X.HasValue ? Format(corner.X.Value) : ReviewDataText.NotReadYet;
            Y = corner.Y.HasValue ? Format(corner.Y.Value) : ReviewDataText.NotReadYet;
            Shared = Format(corner.SharedE) + ", " + Format(corner.SharedN);
        }

        public string Name { get; }

        public string X { get; }

        public string Y { get; }

        public string Shared { get; }

        private static string Format(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
