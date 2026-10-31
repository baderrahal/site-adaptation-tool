using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.Review
{
    // R406. What the window shows and holds
    public sealed class ReviewView
    {
        public string Nbh { get; set; }

        // The file names of the source drawings, for the band line "Read from"
        public IReadOnlyList<string> Sources { get; set; }

        public ReviewState State { get; set; }
    }

    // R406. The buttons of the window that reach outside it. Save writes review_<NBH>.json and gives its path. Zoom is set by the Revit
    // side, Common holds no Revit type. Export is null while no writer of the xlsx is reachable from the add-in, as R350 has the worker
    // write them
    public sealed class ReviewCallbacks
    {
        public Func<IReadOnlyList<ReviewCheck>, string> Save { get; set; }

        public Action<ReviewRow> Zoom { get; set; }

        public Func<string> Export { get; set; }

        // Null when the zoom ran, else what the window says
        public string ZoomMessage(ReviewRow row)
        {
            if (Zoom == null)
            {
                return RunTexts.NotBuilt("Zoom to plot");
            }
            try
            {
                Zoom(row);
                return null;
            }
            catch (Exception error)
            {
                return error.Message;
            }
        }

        public string ExportMessage()
        {
            return Export == null ? RunTexts.NotBuilt("Export to Excel") : Export();
        }
    }

    public sealed class ReviewAnswer
    {
        // True when Save review ran, so the step has something to commit
        public bool Saved { get; set; }
    }

    public interface IReviewDialog
    {
        ReviewAnswer Show(ReviewView view, ReviewCallbacks callbacks);
    }

    // R406, button 3 Review data. Preview reads the rows, Confirm opens the window and is true when the team saved a review, Apply sets
    // nothing in the model. load gives the rows, the checks kept and the hash of the plot table of the read, from the files of button 2
    public sealed class ReviewDataRun
    {
        private readonly Func<RunContext, ReviewInput> load;
        private readonly IReviewDialog dialog;
        private readonly Action<ReviewRow> zoom;
        private ReviewInput input;

        public ReviewDataRun(Func<RunContext, ReviewInput> load, IReviewDialog dialog, Action<ReviewRow> zoom)
        {
            this.load = load;
            this.dialog = dialog;
            this.zoom = zoom;
        }

        public ButtonBody Body()
        {
            return new ButtonBody
            {
                Preview = Preview,
                Confirm = (context, summary) => Show(input, context.RunId, context.WorkFolder),
                Apply = context => new string[0],
                ReadBack = context => null,
            };
        }

        private RunSummary Preview(RunContext context)
        {
            input = load(context);
            var state = new ReviewState(input.Rows, input.Checks, input.ReadRunId, input.PlotTableHash);
            List<string> wrong = state.Rows.Where(row => state.StatusOf(row.PlotNo) == ReviewStatus.Wrong).Select(row => "Plot " + row.PlotNo + ": " + state.NoteOf(row.PlotNo)).ToList();
            return new RunSummary(state.Total + " plots, " + state.CheckedCount + " of " + state.Total + " checked", wrong);
        }

        // The window on the input, true when Save review ran in it
        public bool Show(ReviewInput shown, string runId, string workFolder)
        {
            if (dialog == null)
            {
                throw new InvalidOperationException("the Review data window is not set");
            }
            var state = new ReviewState(shown.Rows, shown.Checks, shown.ReadRunId, shown.PlotTableHash);
            var saved = false;
            var callbacks = new ReviewCallbacks
            {
                Save = checks =>
                {
                    string path = ReviewFile.Write(workFolder, shown.Nbh, checks, runId);
                    state.MarkSaved();
                    saved = true;
                    return path;
                },
                Zoom = zoom,
            };
            dialog.Show(new ReviewView { Nbh = shown.Nbh, Sources = shown.Sources ?? new string[0], State = state }, callbacks);
            return saved;
        }
    }
}
