using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common.Settings
{
    // R367, docs/design/06-settings.md. The settings of one project as the settings JSON holds them. No field holds a number with
    // decimals, so no coordinate or level value can be kept in it, and the reader refuses a field it does not know
    public sealed class ProjectSettings
    {
        public int FormatVersion { get; set; } = SettingsJson.Version;
        public string ConfigFile { get; set; }
        public string RulesFile { get; set; }
        public string LevelProfile { get; set; }
        public SortedDictionary<string, SettingsInput> Inputs { get; set; } = new SortedDictionary<string, SettingsInput>(StringComparer.Ordinal);
        public SettingsScope Scope { get; set; } = new SettingsScope();
        public bool DryRunFirst { get; set; } = true;

        // The row Villa mode of docs/design/11-light-mode.md, which the pane of Lane B 5a already keeps, null until a pick
        public string VillaMode { get; set; }

        // The last real run of each button by its number
        public SortedDictionary<string, ButtonRun> LastRuns { get; set; } = new SortedDictionary<string, ButtonRun>(StringComparer.Ordinal);

        // SCAN-UI, Lane C. Each field below is null in a file written before it, which reads as before, so formatVersion stays 1
        // (Q288). The source picked in the window What the tool read, by the kind of its scan row (R436)
        public SortedDictionary<string, string> ScanPicks { get; set; }

        // The source picked for each conflict of the scan, keyed "<plot>|<field>" (R436)
        public SortedDictionary<string, string> ConflictPicks { get; set; }

        // The inputs ticked Current in 1 Set up site, each "<input name>|<file name>", so a new file of the input is not ticked (R441)
        public List<string> CurrentFiles { get; set; }

        // The pick of Door level from in the window of 4 Check, civilFile or roadPlusSlope, null until a pick (R443). The build of 4 Check
        // still takes designBasis.doorLevel.source of config, so the pick is only kept here for the next build once Lane A reads it
        public string DoorLevelSource { get; set; }
    }

    public sealed class SettingsInput
    {
        // As the user chose it, the pane shows the file or folder name
        public string Path { get; set; }
        public long? Size { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class SettingsScope
    {
        public string Kind { get; set; } = ScopeKinds.Project;
        public string Nbh { get; set; }

        // The plot list as typed, the scope parser of R366 reads it
        public string Plots { get; set; }
    }

    public sealed class ButtonRun
    {
        // ISO 8601 in UTC
        public string Time { get; set; }
        public string Mode { get; set; }
        public SettingsScope Scope { get; set; }
        public SortedDictionary<string, string> InputHashes { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public SortedDictionary<string, int> Counts { get; set; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public List<string> Issues { get; set; } = new List<string>();
    }

    public static class ScopeKinds
    {
        public const string Project = "project";
        public const string Nbh = "nbh";
        public const string Plots = "plots";
        public static readonly string[] All = { Project, Nbh, Plots };
    }

    // How the real run was confirmed: Apply after the dry run of "Dry run first, change nothing", or Apply on the counts alone
    public static class RunModes
    {
        public const string AfterDryRun = "afterDryRun";
        public const string Direct = "direct";
        public static readonly string[] All = { AfterDryRun, Direct };
    }

    public static class VillaModes
    {
        public const string Masses = "masses";
        public const string Links = "links";
        public static readonly string[] All = { Masses, Links };
    }

    // R443. The sources of El @ Entry the picker of 4 Check offers, the two of designBasis.doorLevel.sources of R408
    public static class DoorLevelSources
    {
        public const string CivilFile = DesignBasis.CivilFile;
        public const string RoadPlusSlope = DesignBasis.RoadPlusSlope;
        public static readonly string[] All = { CivilFile, RoadPlusSlope };
    }

    // The inputs of section 6 by their names in the settings JSON, with parameterSheet, which the pane of Lane B 5a named
    public static class InputNames
    {
        public const string Baseplan = "baseplan";
        public const string VillaBlocks = "villaBlocks";
        public const string Curbs = "curbs";
        public const string RoadSurface = "roadSurface";
        public const string Dlus = "dlus";
        public const string Sales = "sales";
        public const string PlotNumbers = "plotNumbers";
        public const string Prototypes = "prototypes";
        public const string ParameterSheet = "parameterSheet";
        public static readonly string[] All = { Baseplan, VillaBlocks, Curbs, RoadSurface, Dlus, Sales, PlotNumbers, Prototypes, ParameterSheet };
    }

    // The Extensible Storage schema of section 6 on ProjectInformation. The vendor is the VendorId of the manifests, since a schema
    // whose write access is vendor takes writes only from an add-in of that vendor
    public static class SettingsSchema
    {
        public const string Name = "SiteAdaptation_Settings";
        public const string Field = "SettingsJson";
        public const string VendorId = "SITEADAPT";
        public static readonly Guid Guid = new Guid("8faba002-4d7b-46ad-8f25-8d4c81ec65eb");
    }
}
