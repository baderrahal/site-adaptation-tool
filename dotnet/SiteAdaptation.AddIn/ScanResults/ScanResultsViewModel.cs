using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Scan;

namespace SiteAdaptation.AddIn.ScanResults
{
    // One row of "Where each value comes from". Changing the source from the list counts as a pick, so the status turns Clear (R435)
    public sealed class ScanValueViewModel : Bindable
    {
        private readonly ScanValueRow row;
        private readonly Action changed;

        public ScanValueViewModel(ScanValueRow row, Action changed)
        {
            this.row = row;
            this.changed = changed;
        }

        public string Label { get { return row.Label; } }
        public string Kind { get { return row.Kind; } }
        public IReadOnlyList<ScanSource> Sources { get { return row.Sources; } }
        public string Covers { get { return row.Covers; } }
        public string Checks { get { return row.Checks; } }
        public string Status { get { return row.Status; } }
        public bool IsPick { get { return row.IsPick; } }
        public bool IsClear { get { return !row.IsPick; } }

        // True once the user changed the source, only those picks are saved with the setup
        public bool Touched { get; private set; }
        public string SelectedRaw { get { return row.Selected; } }

        public ScanSource Selected
        {
            get { return row.Sources.FirstOrDefault(source => source.Raw == row.Selected); }
            set
            {
                if (value == null || value.Raw == row.Selected)
                {
                    return;
                }
                row.Selected = value.Raw;
                Touched = true;
                Changed(nameof(Selected));
                Changed(nameof(Covers));
                Changed(nameof(Checks));
                Changed(nameof(Status));
                Changed(nameof(IsPick));
                Changed(nameof(IsClear));
                changed?.Invoke();
            }
        }
    }

    // One side of a conflict. Its text is the choice as the list shows it
    public sealed class ScanChoice
    {
        public string Text { get; set; }
        public string Source { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }

    public sealed class ScanConflictViewModel : Bindable
    {
        private readonly ScanConflictRow row;

        public ScanConflictViewModel(ScanConflictRow row)
        {
            this.row = row;
            Choices = new[] { new ScanChoice { Text = row.ChoiceA, Source = row.SourceA }, new ScanChoice { Text = row.ChoiceB, Source = row.SourceB } };
        }

        public string Plot { get { return row.Plot; } }
        public string Value { get { return row.Value; } }
        public string Key { get { return row.Key; } }
        public bool Picked { get { return row.Picked; } }
        public IReadOnlyList<ScanChoice> Choices { get; }

        public ScanChoice Selected
        {
            get { return row.Selected == row.SourceB && row.SourceB != row.SourceA ? Choices[1] : Choices[0]; }
            set
            {
                if (value == null)
                {
                    return;
                }
                row.Selected = value.Source;
                row.Picked = true;
                Changed(nameof(Selected));
            }
        }

        // The proposed side, the plot drawing or the lead source, for Use the proposed for all
        internal void UseProposed()
        {
            row.Selected = row.SourceA;
            row.Picked = true;
            Changed(nameof(Selected));
        }
    }

    // Lane C SCAN-UI, the window What the tool read with no Revit type. The rows are the objects of ScanView, so a pick changes them and
    // Export writes what the window shows. 2808 conflicts are listed by a virtualizing list, the first FirstShown until Show all
    public sealed class ScanResultsViewModel : Bindable
    {
        public const int FirstShown = 20;

        private bool showingAll;
        private IReadOnlyList<ScanUnclear> unclear;
        private string footLine = ScanResultsText.Foot;

        public ScanResultsViewModel(ScanViewData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Values = data.Values.Select(row => new ScanValueViewModel(row, Refresh)).ToList();
            Files = data.Files;
            AllConflicts = data.Conflicts.Select(row => new ScanConflictViewModel(row)).ToList();
            unclear = data.Unclear.ToList();
        }

        public ScanViewData Data { get; }
        public IReadOnlyList<ScanValueViewModel> Values { get; }
        public IReadOnlyList<ScanFileRow> Files { get; }
        public IReadOnlyList<ScanConflictViewModel> AllConflicts { get; }

        public string FilesRead { get { return ScanView.Count(Data.FilesRead); } }
        public string PlotsFound { get { return ScanView.Count(Data.PlotsFound); } }
        public string ConflictCount { get { return ScanView.Count(Data.ConflictCount); } }

        public IReadOnlyList<ScanUnclear> Unclear { get { return unclear; } }
        public bool HasUnclear { get { return unclear.Count > 0; } }

        public bool HasConflicts { get { return AllConflicts.Count > 0; } }
        public IReadOnlyList<ScanConflictViewModel> Conflicts { get { return showingAll ? AllConflicts : AllConflicts.Take(FirstShown).ToList(); } }
        public bool CanShowAll { get { return !showingAll && AllConflicts.Count > FirstShown; } }
        public string ShowAllText { get { return ScanView.Fill(ScanResultsText.ShowAll, ScanView.Count(AllConflicts.Count)); } }

        // The foot line, which says the file saved or what stopped an export
        public string FootLine { get { return footLine; } set { Set(ref footLine, value); } }

        public void ShowAll()
        {
            if (showingAll)
            {
                return;
            }
            showingAll = true;
            Changed(nameof(Conflicts));
            Changed(nameof(CanShowAll));
        }

        public void UseProposedForAll()
        {
            foreach (ScanConflictViewModel conflict in AllConflicts)
            {
                conflict.UseProposed();
            }
        }

        private void Refresh()
        {
            unclear = Data.Unclear.ToList();
            Changed(nameof(Unclear));
            Changed(nameof(HasUnclear));
        }
    }
}
