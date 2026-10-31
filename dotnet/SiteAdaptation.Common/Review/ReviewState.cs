using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.Review
{
    // R406. The team checks of one NBH while the window is open. A check made on a read whose plot table hash differs from the one now
    // is shown as not checked and keeps its earlier note visible (CHOSEN by the main session). It stays in the file until the team marks
    // the plot again, so no note is lost
    public sealed class ReviewState
    {
        private readonly Dictionary<string, ReviewCheck> checks = new Dictionary<string, ReviewCheck>(StringComparer.Ordinal);
        private readonly string readRunId;
        private readonly string plotTableHash;

        public ReviewState(IReadOnlyList<ReviewRow> rows, IEnumerable<ReviewCheck> saved, string readRunId, string plotTableHash)
        {
            Rows = rows;
            this.readRunId = readRunId;
            this.plotTableHash = plotTableHash;
            foreach (ReviewCheck check in saved ?? new ReviewCheck[0])
            {
                checks[check.Plot] = check;
            }
        }

        public IReadOnlyList<ReviewRow> Rows { get; }

        public int Total => Rows.Count;

        // True from the first mark after the last save
        public bool Changed { get; private set; }

        public int CheckedCount => Rows.Count(row => StatusOf(row.PlotNo) != ReviewStatus.NotChecked);

        public ReviewStatus StatusOf(string plot)
        {
            ReviewCheck check = Current(plot);
            if (check == null)
            {
                return ReviewStatus.NotChecked;
            }
            return check.Check == ReviewCheckValue.Wrong ? ReviewStatus.Wrong : ReviewStatus.Ok;
        }

        public string NoteOf(string plot)
        {
            return Current(plot)?.Note ?? "";
        }

        // The note of a check made on an earlier read, which the plot shows as not checked now
        public string EarlierNoteOf(string plot)
        {
            return checks.TryGetValue(plot, out ReviewCheck check) && check.PlotTableHash != plotTableHash ? check.Note ?? "" : "";
        }

        public void Mark(string plot, string check, string note)
        {
            if (!Rows.Any(row => row.PlotNo == plot))
            {
                throw new ArgumentException("no plot " + plot + " in this neighbourhood", nameof(plot));
            }
            if (!ReviewCheckValue.IsValid(check))
            {
                throw new ArgumentException("a team check is " + ReviewCheckValue.Ok + " or " + ReviewCheckValue.Wrong + ", not " + check, nameof(check));
            }
            checks[plot] = new ReviewCheck { Plot = plot, Check = check, Note = note ?? "", RunId = readRunId, PlotTableHash = plotTableHash };
            Changed = true;
        }

        // Every entry to keep, the plots not checked have none. Ordered by plot so the file is the same for the same checks
        public List<ReviewCheck> ToChecks()
        {
            return checks.Values.OrderBy(check => check.Plot, StringComparer.Ordinal).ToList();
        }

        public void MarkSaved()
        {
            Changed = false;
        }

        public IEnumerable<ReviewRow> Filter(ReviewChip chip, string find)
        {
            string part = (find ?? "").Trim();
            return Rows.Where(row => InChip(row, chip) && (part.Length == 0
                || row.PlotNo.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0
                || (row.CadLabel ?? "").IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public int CountOf(ReviewChip chip)
        {
            return Rows.Count(row => InChip(row, chip));
        }

        private bool InChip(ReviewRow row, ReviewChip chip)
        {
            ReviewStatus status = StatusOf(row.PlotNo);
            switch (chip)
            {
                case ReviewChip.NotChecked: return status == ReviewStatus.NotChecked;
                case ReviewChip.Ok: return status == ReviewStatus.Ok;
                case ReviewChip.Wrong: return status == ReviewStatus.Wrong;
                default: return true;
            }
        }

        private ReviewCheck Current(string plot)
        {
            return checks.TryGetValue(plot, out ReviewCheck check) && check.PlotTableHash == plotTableHash ? check : null;
        }
    }
}
