using System.Collections.Generic;

namespace SiteAdaptation.Common.Update
{
    // R402. Every text of the button Update from report the window shows, in one place for the text test of R372, which reads this file as
    // a file of the window: the tiles, chips and columns word for word from docs/design/13-workflow-changes.md part Update from report, the
    // rule column, the refusal, the lacks and the lines of a change not saved CHOSEN in Round ADDIN 5f. A value is a slot of the label list,
    // filled by UpdateFromReport.Fill. No other text goes here, the text test fails on any this file holds that the label list does not
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

        public const string Follows = "follows the rules";
        public const string BreaksJoin = ", ";
        public const string NoRuleChecks = "no rule checks this field";

        // The plain name of each level rule check by its id, at most 6 words (12-ui-spec.md), CHOSEN in the 5f fix for Bader's check
        public static readonly Dictionary<string, string> RuleNames = new Dictionary<string, string>
        {
            [UpdateFromReport.FflOverEntry] = "FFL over entry by the base",
            [UpdateFromReport.StepsRule] = "Steps by the steps rule",
            [UpdateFromReport.StepsAtMost] = "Front steps within their range",
            [UpdateFromReport.StepHeights] = "Height in the step legend",
            [UpdateFromReport.Riser] = "Riser height within its limits",
            [UpdateFromReport.OneStepUnits] = "One step for one step units",
            [UpdateFromReport.OneFflPerPiece] = "One FFL per piece",
            [UpdateFromReport.VillaTypeCheck] = "Villa type of the profile",
            [UpdateFromReport.HandRule] = "Mirror by the hand rule",
        };

        public static string RuleName(string id)
        {
            return id != null && RuleNames.TryGetValue(id, out string name) ? name : id;
        }

        // Why a change is never applied, its rule column
        public const string NotSavable = "the plot has no override entry of the job <n>, so Apply can not save it (Q169)";
        public const string NoNumber = "the cell holds no number";
        public const string NoWhole = "the cell holds no whole number";
        public const string Empty = "the cell is empty";
        public const string NoType = "the cell holds no villa type";
        public const string NoFlag = "the cell holds no TRUE or FALSE";

        // The one line of a refusal, nothing listed
        public const string NoSettings = "<n> holds no sheet Settings with the Plan build id and the Plan SHA-256, so its build is not known (R402)";
        public const string OtherBuildId = "<n> is of another build, its Plan build id is <n>, the plan of the model <n> (R402)";
        public const string OtherSha = "<n> is of another build, its Plan SHA-256 is <n>, the plan of the model <n> (R402)";
        public const string NoSheets = "<n> holds no sheet Levels or Placed, so it is no level Excel or report Excel (R402)";
        public const string NoLevelProfile = "the rules profile holds no level profile <n> (R402)";
        public const string NoHeader = "<n> holds no header <n> in its sheet <n> (R402)";

        // What the rules profile lacks for a check
        public const string Lacks = "the rules profile holds no <n>, so the check <n> is not run (R402)";
        public const string NoManualBlock = "the rules profile holds no block manualRules, so no change breaks a manual rule (R402)";
        public const string BareManual = "<n> manual rules of the block name no updateChecks, so no change is held against them: <n> (R402)";

        // A ticked change Apply could not save, in the dry run line and the message after Apply
        public const string NotApplied = "plot <n> <n>: <n> (R402)";
        public const string NoOverrideField = "no field of the override list";
        public const string NoLevelRule = "the plan holds no levelRule of the plot, so no gap to the rule";
        public const string NoOrigin = "no entry of source <n> of the plot names its key and model";

        // Why a dry run or Apply stops, in the problem line of the window or the message of the run
        public const string NoJob = "the level profile <n> names no job, so an override has no source (R402)";
        public const string NoOverrides = "the override list holds no overrides (R402)";
        public const string OtherKey = "the override list holds <n>, which the form of majority.overrides_text does not write (R402)";

        // Why the picked files are not read, in the problem line. NoNbh holds no value, its slot is a part of the file name it shows
        public const string NoNbh = "the file name of a report Excel starts with its NBH and an underscore, as 012_Levels_<profile>.xlsx does (R402)";
        public const string TwoNbhs = "the files are of NBH <n>, Update from report takes the files of one NBH (R402)";
        public const string TwoLevelExcels = "<n> and <n> are both level Excels, Update from report takes one level Excel and one report Excel of one NBH (R402)";
        public const string TwoReportExcels = "<n> and <n> are both report Excels, Update from report takes one level Excel and one report Excel of one NBH (R402)";
    }
}
