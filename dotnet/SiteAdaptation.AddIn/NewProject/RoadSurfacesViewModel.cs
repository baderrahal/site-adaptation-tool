using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SiteAdaptation.AddIn.Pane;

namespace SiteAdaptation.AddIn.NewProject
{
    // The function a window Road surfaces stands behind: the stems the folder holds and the stems in the effective order go in, the
    // ticked stems in their order come out, null on Cancel
    public delegate IReadOnlyList<string> SurfacePicker(IReadOnlyList<string> inFolder, IReadOnlyList<string> effective);

    public sealed class RoadSurfaceRowViewModel : Bindable
    {
        private readonly Action<RoadSurfaceRowViewModel> ticked;
        private bool isTicked;
        private bool isSelected;

        internal RoadSurfaceRowViewModel(string stem, bool missing, bool isTicked, Action<RoadSurfaceRowViewModel> ticked)
        {
            Stem = stem;
            Missing = missing;
            this.isTicked = isTicked;
            this.ticked = ticked;
        }

        public string Stem { get; }
        public string Short { get { return RoadPickState.Short(Stem); } }
        public bool Missing { get; }
        public bool CanTick { get { return !Missing; } }
        public bool IsSelected { get { return isSelected; } internal set { Set(ref isSelected, value); } }

        public bool IsTicked
        {
            get { return isTicked; }
            set
            {
                if (!Missing && Set(ref isTicked, value))
                {
                    ticked(this);
                }
            }
        }
    }

    // R492, the window Road surfaces. One list, ticked rows first in their order, the rest in sorted order, a name the folder no longer
    // holds last, flagged and unticked. It holds no logic of the build and no Revit type, a test drives it with no click
    public sealed class RoadSurfacesViewModel : Bindable
    {
        private readonly ObservableCollection<RoadSurfaceRowViewModel> rows = new ObservableCollection<RoadSurfaceRowViewModel>();
        private RoadSurfaceRowViewModel selected;

        public RoadSurfacesViewModel(IReadOnlyList<string> inFolder, IReadOnlyList<string> effective)
        {
            var held = (inFolder ?? new string[0]).Select(RoadPickState.Stem).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Rows = new ReadOnlyObservableCollection<RoadSurfaceRowViewModel>(rows);
            HasFiles = held.Count > 0;
            var wanted = (effective ?? new string[0]).Select(RoadPickState.Stem).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var order = new List<string>();
            foreach (string name in wanted)
            {
                string found = held.FirstOrDefault(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    order.Add(found);
                }
            }
            foreach (string name in order)
            {
                rows.Add(new RoadSurfaceRowViewModel(name, false, true, OnTicked));
            }
            foreach (string name in held.Where(item => !order.Contains(item)).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new RoadSurfaceRowViewModel(name, false, false, OnTicked));
            }
            var gone = wanted.Where(name => !held.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (string name in gone)
            {
                rows.Add(new RoadSurfaceRowViewModel(name, true, false, OnTicked));
            }
            MissingLine = gone.Count == 0 ? string.Empty : TextTemplate.Fill(RoadSurfacesText.NotInFolder, string.Join(", ", gone.Select(RoadPickState.Short)));
            selected = rows.FirstOrDefault(row => row.IsTicked);
            if (selected != null)
            {
                selected.IsSelected = true;
            }
        }

        public ReadOnlyObservableCollection<RoadSurfaceRowViewModel> Rows { get; }
        public bool HasFiles { get; }
        public bool NoFiles { get { return !HasFiles; } }
        public string MissingLine { get; }
        public bool MissingShown { get { return MissingLine.Length > 0; } }

        // The ticked stems in their order, null after Cancel or before Use
        public IReadOnlyList<string> Result { get; private set; }

        public event Action Finished;

        public IReadOnlyList<string> Ticked { get { return rows.Where(row => row.IsTicked).Select(row => row.Stem).ToList(); } }
        public bool CanUse { get { return rows.Any(row => row.IsTicked); } }
        public bool NeedTickShown { get { return !CanUse; } }

        public RoadSurfaceRowViewModel Selected { get { return selected; } }

        public void Select(RoadSurfaceRowViewModel row)
        {
            if (row == null || !rows.Contains(row) || row == selected)
            {
                return;
            }
            if (selected != null)
            {
                selected.IsSelected = false;
            }
            selected = row;
            row.IsSelected = true;
            MovesChanged();
        }

        public bool CanMoveUp { get { return selected != null && selected.IsTicked && rows.IndexOf(selected) > 0; } }

        public bool CanMoveDown
        {
            get
            {
                int at = selected == null ? -1 : rows.IndexOf(selected);
                return selected != null && selected.IsTicked && at < rows.Count - 1 && rows[at + 1].IsTicked;
            }
        }

        public void MoveUp()
        {
            if (CanMoveUp)
            {
                rows.Move(rows.IndexOf(selected), rows.IndexOf(selected) - 1);
                MovesChanged();
            }
        }

        public void MoveDown()
        {
            if (CanMoveDown)
            {
                rows.Move(rows.IndexOf(selected), rows.IndexOf(selected) + 1);
                MovesChanged();
            }
        }

        // Use these picks. With no tick it does nothing and the line says why
        public void Use()
        {
            if (!CanUse)
            {
                return;
            }
            Result = Ticked;
            Finished?.Invoke();
        }

        public void Cancel()
        {
            Result = null;
            Finished?.Invoke();
        }

        // A tick goes to the end of the ticked rows, an untick back to its sorted place among the rest
        private void OnTicked(RoadSurfaceRowViewModel row)
        {
            int from = rows.IndexOf(row);
            int ticks = rows.Count(item => item != row && item.IsTicked);
            int to;
            if (row.IsTicked)
            {
                to = ticks;
            }
            else
            {
                var rest = rows.Where(item => item != row && !item.IsTicked && !item.Missing).ToList();
                to = ticks + rest.Count(item => StringComparer.OrdinalIgnoreCase.Compare(item.Stem, row.Stem) < 0);
            }
            if (to != from)
            {
                rows.Move(from, to);
            }
            Changed(nameof(CanUse));
            Changed(nameof(NeedTickShown));
            MovesChanged();
        }

        private void MovesChanged()
        {
            Changed(nameof(CanMoveUp));
            Changed(nameof(CanMoveDown));
        }
    }
}
