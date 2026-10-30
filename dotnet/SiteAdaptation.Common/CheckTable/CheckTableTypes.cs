using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.CheckTable
{
    // R376. The Use of a row, as the dropdown of the check table names it
    public static class CheckUse
    {
        public const string Rule = "Rule";
        public const string Past = "Past";
        public const string Skip = "Skip";

        public static bool IsValid(string use)
        {
            return use == Rule || use == Past || use == Skip;
        }
    }

    // R376. One row of the check table, one plot and check
    public sealed class CheckRow
    {
        // The place of the row in checks_<NBH>.json, the key a Use choice names it by
        public int Index { get; set; }

        public string Plot { get; set; }

        public string Unit { get; set; }

        public string Nbh { get; set; }

        public string Check { get; set; }

        // The field of the override list, or the key of the missing list, null for a row of none
        public string Field { get; set; }

        // override, question, cannot, missing of R363, team for a plot marked Wrong in 3 Review data, join or key of Q117
        public string From { get; set; }

        // Past when the build applies an override of the list for the row, else Rule, kept as the opening value
        public string OpenUse { get; set; }

        public JToken Rule { get; set; }

        public JToken ToolValue { get; set; }

        // The form Past writes in the list, such as gapToRuleM, null where the 03b table offers none
        public string PastForm { get; set; }

        // The value Past writes in that form, null when no source gives one
        public JToken PastValue { get; set; }

        // True when Past can be chosen, else PastWhy says why it is grey
        public bool PastOffered { get; set; }

        public string PastWhy { get; set; }

        public JToken Why { get; set; }

        // The answer key and its model file that hold the plot, null when none does
        public string Key { get; set; }

        public string Model { get; set; }

        // The manual rule of the row, M01 to M15 of the rules profile, null until the lookup is wired
        public string ManualRuleId { get; set; }
    }

    // R376. The chosen Use of one row, by its index
    public sealed class CheckChoice
    {
        public CheckChoice(int index, string use)
        {
            Index = index;
            Use = use;
        }

        public int Index { get; }

        public string Use { get; }
    }

    // R376, docs/design/07-mock.md 7.4. What the check table window shows
    public sealed class CheckTableView
    {
        public string Nbh { get; set; }

        // "4 Check, NBH <n>, <n> plots"
        public string Title { get; set; }

        public IReadOnlyList<CheckRow> Rows { get; set; } = new CheckRow[0];

        // All, each check type in order, then Assumed
        public IReadOnlyList<KeyValuePair<string, int>> Counts { get; set; } = new KeyValuePair<string, int>[0];

        public int FollowEveryRule { get; set; }

        // "[n] plots follow every rule"
        public string FollowLine { get; set; }

        // Q117. The one line that no past job was given, null when one was
        public string NoPastJobLine { get; set; }

        // The assumed groups of the chip Assumed, as the build gave them
        public JObject Assumed { get; set; } = new JObject();

        // False in a dry run, Save overrides is greyed
        public bool SaveAllowed { get; set; }

        // The line under the title, null where the run did not give it
        public CheckTableInfo Info { get; set; }
    }

    // R376. What the window reaches outside it. Save writes the override list with the choices that differ from the opening value
    // and gives the path, or throws with the reason, such as a list a build refuses
    public sealed class CheckTableCallbacks
    {
        public Func<IReadOnlyList<CheckChoice>, string> Save { get; set; }

        // The text of the Use a row would get, for the side box, null for none
        public Func<CheckRow, string, string> Gives { get; set; }

        // Zoom to plot and Export to Excel of the Revit side. Each gives the message to show, or null for none. While one is null its
        // button is grey, as the Revit side of 4 Check does not wire them yet
        public Func<CheckRow, string> Zoom { get; set; }

        public Func<string> Export { get; set; }
    }

    // R376. The Use of each row when the window closed and whether Save overrides ran
    public sealed class CheckTableAnswer
    {
        public IReadOnlyList<CheckChoice> Uses { get; set; } = new CheckChoice[0];

        public bool Saved { get; set; }
    }

    // R376. The check table window as Common knows it, like ISetUpSiteDialog and IReviewDialog, so the tests load no WPF window
    public interface ICheckTableDialog
    {
        CheckTableAnswer Show(CheckTableView view, CheckTableCallbacks callbacks);
    }

    // Q117. The link a key read holds for a plot, of the past model prototype reads
    public sealed class KeyLink
    {
        public string Code { get; set; }

        public string Model { get; set; }

        public string File { get; set; }

        public bool? Mirrored { get; set; }

        public JObject Parameters { get; set; } = new JObject();
    }

    // Q117. The past job, both parts optional as a new project has none: key_join.json of python/key_join.py and the links of the
    // past model prototype reads by plot
    public sealed class PastJob
    {
        public JObject KeyJoin { get; set; }

        public IReadOnlyDictionary<string, KeyLink> Links { get; set; }

        public bool Given => KeyJoin != null || Links != null;
    }
}
