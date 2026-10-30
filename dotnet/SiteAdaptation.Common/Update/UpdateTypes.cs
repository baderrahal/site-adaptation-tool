using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Update
{
    // One sheet as read back: its rows from row 1, each cell a string, a double, a long, a bool or null, as XlsxSheet of CadReader reads it
    public sealed class UpdateSheet
    {
        public string Name { get; set; }
        public List<object[]> Rows { get; set; } = new List<object[]>();
    }

    // One edited Excel file as read back, the level Excel or the report Excel of one NBH
    public sealed class UpdateBook
    {
        public string FileName { get; set; }
        public List<UpdateSheet> Sheets { get; } = new List<UpdateSheet>();

        public UpdateSheet Sheet(string name)
        {
            return Sheets.FirstOrDefault(sheet => sheet.Name == name);
        }
    }

    // One change of an editable column against the plan of the model, a row of the list of the window
    public sealed class UpdateChange
    {
        public string Plot { get; set; }
        // A label of UpdateText.Fields
        public string Field { get; set; }
        public string FileName { get; set; }
        public string Sheet { get; set; }
        public int Row { get; set; }
        public object InModel { get; set; }
        public object InExcel { get; set; }
        // The level rule checks of R402 the change breaks, by their names
        public List<string> Breaks { get; } = new List<string>();
        // The ids of the manual rules of the block manualRules whose checks the change breaks
        public List<string> ManualRules { get; } = new List<string>();
        // Why the cell can not be applied, null when it can
        public string Invalid { get; set; }
        // The Apply tick the window starts with
        public bool Ticked { get; set; }

        public bool BreaksARule => Invalid != null || Breaks.Count > 0;

        // The level rule checks of R402 that ran on the change, by their ids, none when no rule checks its field
        public List<string> Checks { get; } = new List<string>();

        // The plain names of the rules the change breaks and the ids of the manual rules, else whether a rule checked it at all (12-ui-spec.md)
        public string RuleCheck => Invalid ?? (Breaks.Count > 0 ? string.Join(UpdateText.BreaksJoin, Breaks.Select(UpdateText.RuleName).Concat(ManualRules))
            : Checks.Count == 0 ? UpdateText.NoRuleChecks : UpdateText.Follows);

        // The ids of the rules the rule column names, or of the checks the change follows, for the tooltip
        public string RuleIds => Invalid != null ? "" : string.Join(UpdateText.BreaksJoin, Breaks.Count > 0 ? Breaks.Concat(ManualRules) : Checks);

        public bool FollowsTheRules => Invalid == null && Breaks.Count == 0 && Checks.Count > 0;
    }

    // A cell of a column that is not editable whose value differs from the plan, counted and never applied
    public sealed class IgnoredCell
    {
        public string Plot { get; set; }
        public string FileName { get; set; }
        public string Sheet { get; set; }
        public string Column { get; set; }
        public int Row { get; set; }
        public object InModel { get; set; }
        public object InExcel { get; set; }
        public string Why { get; set; }
    }

    // What the window shows: the refusal or the changes, the ignored cells, the tiles and chips as counts, and what the rules profile
    // lacks for a check
    public sealed class UpdateList
    {
        public string Refusal { get; set; }
        public List<UpdateChange> Changes { get; } = new List<UpdateChange>();
        public List<IgnoredCell> Ignored { get; } = new List<IgnoredCell>();
        public List<string> Lacks { get; } = new List<string>();

        public Dictionary<string, int> Tiles()
        {
            return new Dictionary<string, int>
            {
                [UpdateText.ChangesFound] = Changes.Count,
                [UpdateText.FollowTheRules] = Changes.Count(change => change.FollowsTheRules),
                [UpdateText.BreakAManualRule] = Changes.Count(change => change.ManualRules.Count > 0),
                [UpdateText.NotEditableIgnored] = Ignored.Count,
            };
        }

        public IEnumerable<UpdateChange> Chip(string chip)
        {
            if (chip == UpdateText.All)
            {
                return Changes;
            }
            if (chip == UpdateText.BreaksARule)
            {
                return Changes.Where(change => change.BreaksARule);
            }
            return Changes.Where(change => change.Field == chip);
        }

        public Dictionary<string, int> Chips()
        {
            return UpdateText.Chips.ToDictionary(chip => chip, chip => Chip(chip).Count());
        }
    }

    // What Apply saved: the override list after it, the entries it wrote, the entries they replaced, the plots changed in the order of the
    // changes, so the buttons update only those, and each ticked change it could not save with why
    public sealed class UpdateApplied
    {
        public JObject List { get; set; }
        public List<JObject> Entries { get; } = new List<JObject>();
        public List<JObject> Replaced { get; } = new List<JObject>();
        public List<string> Plots { get; } = new List<string>();
        public List<string> NotApplied { get; } = new List<string>();
    }
}
