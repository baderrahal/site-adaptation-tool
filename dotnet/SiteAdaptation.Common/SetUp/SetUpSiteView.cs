using System.Collections.Generic;

namespace SiteAdaptation.Common.SetUp
{
    // R373, R374, 7.3 of docs/design/07-mock.md and 12-ui-spec.md. What the Set up site window shows, filled in Common by the read of
    // the document and the dry run of button 1, so the window holds no logic and the picture test fills it with sample data
    public sealed class SetUpSiteView
    {
        // True gives the badge "Existing site file", false "Empty project found"
        public bool ExistingSite { get; set; }

        // The signs the badge stands on, one text each, empty on an empty project
        public List<string> Signs { get; set; } = new List<string>();

        // "What the project has now": Survey point, Project base point, Levels, Floor types and wall types, Shared parameters, Units
        public List<SetUpRow> Now { get; set; } = new List<SetUpRow>();

        // "What the tool will set": Coordinate system, Survey point, Angle to true north, Levels, Floor types, Ramp slope, Wall types,
        // Shared parameters, Units. Differs marks a row "Will change"
        public List<SetUpRow> WillSet { get; set; } = new List<SetUpRow>();

        // The dropdown of the row Ramp slope, in percent, from the rules profile, and the one chosen, the default at first
        public List<double> RampSlopeChoices { get; set; } = new List<double>();

        public double RampSlope { get; set; }

        // The row Shared parameters, "From the parameter sheet: <name>" and its count of parameters, null when no sheet is chosen
        public string ParameterSheetName { get; set; }

        public int ParameterCount { get; set; }

        public List<CheckPointRow> CheckPoints { get; set; } = new List<CheckPointRow>();

        public double PassLimitMm { get; set; }

        // addin.position.gapDecimals, the decimals the Diff is shown and judged at (R385)
        public int GapDecimals { get; set; }

        public int Passing { get; set; }

        // False until the dry run filled the Revit column and What the tool will set
        public bool DryRunDone { get; set; }

        // A stop found before any change, such as a shared parameter of a name of the sheet with another GUID. Apply is off while set
        public List<string> Stops { get; set; } = new List<string>();

        // What is listed and not set, never a stop, such as Plot with no GUID until Q156 gives it one
        public List<string> Waiting { get; set; } = new List<string>();

        // True on a second run of a document that holds every value, so Apply says it changes nothing
        public bool NothingDiffers { get; set; }

        // R441, the section Current files and the Rules dropdown, filled by CurrentFilesDialog. Null where no such dialog wraps the
        // window, as a run of the tests, and then Apply waits for no tick. A list, even empty, holds Apply until the site drawing is ticked
        public List<CurrentFileRow> CurrentFiles { get; set; }

        public List<RulesChoice> RulesProfiles { get; set; } = new List<RulesChoice>();

        public string RulesFile { get; set; }
    }

    // The window of button 1 in the add-in, a stand-in in the test host and the tests, so the run is driven with no click
    public interface ISetUpSiteDialog
    {
        SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks);
    }

    public sealed class SetUpSiteCallbacks
    {
        // The button Dry run with the ramp slope chosen in percent, a view with the Revit column and What the tool will set filled
        public System.Func<double, SetUpSiteView> DryRun { get; set; }

        // Browse of the row Shared parameters with the path of the sheet chosen, the view read again on it
        public System.Func<string, SetUpSiteView> SheetChosen { get; set; }
    }

    public sealed class SetUpSiteAnswer
    {
        // False on Cancel or a closed window
        public bool Apply { get; set; }

        public double RampSlope { get; set; }

        // The sheet chosen by Browse, null when the pane's own stays
        public string ParameterSheet { get; set; }

        // R441. The inputs ticked Current and the rules profile chosen, null where the window gave none, so a stand-in keeps the settings
        public List<string> CurrentFiles { get; set; }

        public string RulesFile { get; set; }
    }

    public sealed class SetUpRow
    {
        public string Label { get; set; }

        public string Value { get; set; }

        public bool Differs { get; set; }
    }

    // One row of "Check points from CAD". CAD and Revit as east, north and elevation in metres, Diff in mm at gapDecimals by R385,
    // null before the dry run gives the Revit column
    public sealed class CheckPointRow
    {
        public string Point { get; set; }

        public string Cad { get; set; }

        public string Revit { get; set; }

        public double? DiffMm { get; set; }

        public bool Pass { get; set; }
    }
}
