using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common.Review
{
    // R402. The values of a team check as review_<NBH>.json keeps them
    public static class ReviewCheckValue
    {
        public const string Ok = "OK";
        public const string Wrong = "Wrong";

        public static bool IsValid(string value)
        {
            return value == Ok || value == Wrong;
        }
    }

    public enum ReviewStatus
    {
        NotChecked,
        Ok,
        Wrong,
    }

    // R402. The chips of the window, All, Not checked, OK, Wrong
    public enum ReviewChip
    {
        All,
        NotChecked,
        Ok,
        Wrong,
    }

    // R402. The manual rule every row names. CHOSEN by Code: the data of a row decides whether the villa of its plot is detached,
    // semi-attached or attached, so a row is checked by M01 of docs/design/15-manual-rules.md, and no other rule of M01 to M15 reads the
    // data a row shows (the cluster list is M02, but a cluster is not read until button 4 builds). The id moves to the manualRules block
    // of the rules profile when 5c adds it, and this is the only place that names it
    public static class ReviewRules
    {
        public const string RowRule = "M01";
    }

    // R402. What the build of button 4 knows of a plot, null until it has built. Every field null is not read yet
    public sealed class ReviewPlanFact
    {
        public string Cluster { get; set; }

        public bool? Mirror { get; set; }

        // The index of the front edge in the ring of the plot, the edge from ring point i to point i + 1
        public int? FrontEdge { get; set; }

        public string RoadAtFront { get; set; }
    }

    // R402. One corner of a plot. X and Y are the model coordinates the survey point of site.json gives, null when the site file gives
    // none. SharedE and SharedN are the shared coordinates of CAD
    public sealed class ReviewCorner
    {
        public int Number { get; set; }

        public double? X { get; set; }

        public double? Y { get; set; }

        public double SharedE { get; set; }

        public double SharedN { get; set; }
    }

    // R402. One row of the table. A field the reads do not give yet is null
    public sealed class ReviewRow
    {
        public string PlotNo { get; set; }

        public string CadLabel { get; set; }

        public string Nbh { get; set; }

        public string VillaType { get; set; }

        public bool? Mirror { get; set; }

        public string Cluster { get; set; }

        public double AreaM2 { get; set; }

        public string RoadAtFront { get; set; }

        public string FrontEdge { get; set; }

        public IReadOnlyList<ReviewCorner> Corners { get; set; } = new ReviewCorner[0];

        public string RuleId { get; set; }
    }

    // R402. One entry of review_<NBH>.json
    public sealed class ReviewCheck
    {
        public string Plot { get; set; }

        public string Check { get; set; }

        public string Note { get; set; }

        public string RunId { get; set; }

        public string PlotTableHash { get; set; }
    }

    // R402. What the run needs to open the window
    public sealed class ReviewInput
    {
        public string Nbh { get; set; }

        public IReadOnlyList<ReviewRow> Rows { get; set; }

        public IReadOnlyList<ReviewCheck> Checks { get; set; }

        public string PlotTableHash { get; set; }

        public string ReadRunId { get; set; }

        public IReadOnlyList<string> Sources { get; set; }
    }
}
