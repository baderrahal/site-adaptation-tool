using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Update
{
    // R402. Every label of the button Update from report in one place, the tiles, chips and columns word for word from
    // docs/design/13-workflow-changes.md part Update from report, the rest CHOSEN in Round ADDIN 5f
    public static class UpdateText
    {
        public const string ChangesFound = "Changes found";
        public const string FollowTheRules = "Follow the rules";
        public const string BreakAManualRule = "Break a manual rule";
        public const string NotEditableIgnored = "Not editable ignored";
        public static readonly string[] Tiles = { ChangesFound, FollowTheRules, BreakAManualRule, NotEditableIgnored };

        public const string All = "All";
        public const string Ffl = "FFL";
        public const string Entry = "El @ Entry";
        public const string Steps = "Steps";
        public const string VillaType = "Villa type";
        public const string Mirror = "Mirror";
        public const string BreaksARule = "Breaks a rule";
        public static readonly string[] Chips = { All, Ffl, Entry, Steps, VillaType, Mirror, BreaksARule };
        // The fields a change can be of, in the order the list shows them
        public static readonly string[] Fields = { Ffl, Entry, Steps, VillaType, Mirror };

        public static readonly string[] Columns = { "Apply", "Plot", "Field", "In model", "In Excel", "Rule check" };

        public const string Decision = "Update from report";
        public const string Reason = "Update from report <run id>";
        public const string Follows = "follows the rules";
        public const string BreaksJoin = ", ";
        public const string NotSavable = "the plot has no override entry of the job <n>, so Apply can not save it (Q169)";

        private static readonly System.Text.RegularExpressions.Regex Slot = new System.Text.RegularExpressions.Regex("<[a-z]+>");

        // A text of the label list with each value in its slot, in order, as TextTemplate of the add-in fills one
        public static string Fill(string template, params object[] values)
        {
            int next = 0;
            return Slot.Replace(template, match => System.Convert.ToString(values[next++], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

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

        public string RuleCheck => Invalid ?? (Breaks.Count == 0 ? UpdateText.Follows : string.Join(UpdateText.BreaksJoin, Breaks.Concat(ManualRules)));
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
                [UpdateText.FollowTheRules] = Changes.Count(change => !change.BreaksARule),
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
