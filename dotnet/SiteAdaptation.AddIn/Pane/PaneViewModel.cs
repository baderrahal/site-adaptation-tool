using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/12-ui-spec.md 7.2. The pane of the active document, top to bottom, with no Revit type, so its logic is
    // tested without Revit. Every text comes from PaneText, the ribbon labels or the settings
    public sealed class PaneViewModel : Bindable
    {
        private readonly IInputProbe probe;
        private readonly IPathPicker picker;
        private DocumentSession document;
        private IReadOnlyList<StepStatus> statuses = StepPlan.None(false);
        private bool statusesFromOutside;
        private bool refreshing;
        private bool again;
        private readonly Dictionary<string, HashedInput> hashes = new Dictionary<string, HashedInput>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> hashing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string projectName = string.Empty;
        private string boxLabel = PaneText.StartLabel;
        private string boxName = PaneText.StartName;
        private string boxLine = PaneText.StartLine;
        private string boxButton = PaneText.StartButton;
        private bool boxButtonShown = true;
        private string inputHelp = string.Empty;
        private IReadOnlyList<string> levelProfiles = new string[0];
        private string levelProfile;
        private IReadOnlyList<string> nbhCodes = new string[0];
        private string startSavedLine = PaneText.StartSavedNone;

        public PaneViewModel(IInputProbe probe, IPathPicker picker = null)
        {
            this.probe = probe;
            this.picker = picker;
            SettingsRow = new FileRowViewModel(PaneText.Setup, false);
            RulesRow = new FileRowViewModel(PaneText.Rules, false);
            InputRows = new ReadOnlyCollection<FileRowViewModel>(new[]
            {
                new FileRowViewModel(PaneText.Baseplan, false, InputKeys.Baseplan),
                new FileRowViewModel(PaneText.VillaBlocks, false, InputKeys.VillaBlocks),
                new FileRowViewModel(PaneText.Curbs, false, InputKeys.Curbs),
                new FileRowViewModel(PaneText.RoadSurface, true, InputKeys.RoadSurface),
                new FileRowViewModel(PaneText.DlusAndSales, false, InputKeys.Dlus, InputKeys.Sales),
                new FileRowViewModel(PaneText.PlotNumbers, false, InputKeys.PlotNumbers),
                new FileRowViewModel(PaneText.Prototypes, true, InputKeys.Prototypes),
                new FileRowViewModel(PaneText.SharedParameters, false, InputKeys.ParameterSheet),
            });
            Steps = new ReadOnlyCollection<StepRowViewModel>(RibbonLayout.Buttons.Select(button => new StepRowViewModel(button.Number, StepName(button.Number), PaneText.StepLines[button.Number - 1], number => RunRequested?.Invoke(number))).ToList());
            // PaneFirst board: on first open steps 1 to 4, then one row for the steps after 4 up to the last
            FoldedStep = new StepRowViewModel(FoldFrom, TextTemplate.Fill(PaneText.FoldedSteps, StepName(FoldFrom), StepPlan.Count, StepName(StepPlan.Count)), string.Empty);
            FirstSteps = new ReadOnlyCollection<StepRowViewModel>(Steps.Take(FoldFrom - 1).Concat(new[] { FoldedStep }).ToList());
            BoxCommand = new PaneCommand(OnBox, () => document != null);
            ImportCommand = new PaneCommand(ImportPreSetting, () => document != null);
            ExportCommand = new PaneCommand(ExportPreSetting, () => document != null);
            NewProjectCommand = new PaneCommand(() => NewProjectRequested?.Invoke(), () => document != null);
            HowItWorksCommand = new PaneCommand(() => HowItWorksRequested?.Invoke(), () => true);
            Load(null);
        }

        private const int FoldFrom = 5;

        // The box button opens New project on first open, else asks for a run of the ribbon button it names. A click on a step row
        // that can run asks the same (R444)
        public event Action<int> RunRequested;

        // NEWPROJ-UI. The Revit side opens the window of each, the pane holds no window
        public event Action NewProjectRequested;
        public event Action HowItWorksRequested;

        // A message for the user, which the Revit side shows in a TaskDialog
        public event Action<string> Told;

        public bool HasDocument { get { return document != null; } }
        public bool FirstOpen { get { return document == null || !document.Settings.Chosen; } }
        public bool InUse { get { return !FirstOpen; } }
        public string StartSavedLine { get { return startSavedLine; } private set { Set(ref startSavedLine, value); } }
        public PaneCommand NewProjectCommand { get; }
        public PaneCommand HowItWorksCommand { get; }
        public StepRowViewModel FoldedStep { get; }
        public ReadOnlyCollection<StepRowViewModel> FirstSteps { get; }

        // The steps the pane shows: all of them in use, steps 1 to 4 and the folded row on first open
        public ReadOnlyCollection<StepRowViewModel> ShownSteps { get { return FirstOpen ? FirstSteps : Steps; } }
        public PaneSettings Settings { get { return document == null ? null : document.Settings; } }
        public string ProjectName { get { return projectName; } private set { Set(ref projectName, value); } }
        public string BoxLabel { get { return boxLabel; } private set { Set(ref boxLabel, value); } }
        public string BoxName { get { return boxName; } private set { Set(ref boxName, value); } }
        public string BoxLine { get { return boxLine; } private set { Set(ref boxLine, value); } }
        public string BoxButton { get { return boxButton; } private set { Set(ref boxButton, value); } }
        public bool BoxButtonShown { get { return boxButtonShown; } private set { Set(ref boxButtonShown, value); } }
        public PaneCommand BoxCommand { get; }

        // Import from past job and Export of the row Pre-setting. Import pre-setting of the first open box runs Import too
        public PaneCommand ImportCommand { get; }
        public PaneCommand ExportCommand { get; }
        public string InputHelp { get { return inputHelp; } private set { Set(ref inputHelp, value); } }
        public FileRowViewModel SettingsRow { get; }
        public FileRowViewModel RulesRow { get; }
        public ReadOnlyCollection<FileRowViewModel> InputRows { get; }
        public ReadOnlyCollection<StepRowViewModel> Steps { get; }
        public IReadOnlyList<string> LevelProfiles { get { return levelProfiles; } private set { Set(ref levelProfiles, value); } }
        public IReadOnlyList<string> NbhCodes { get { return nbhCodes; } private set { Set(ref nbhCodes, value); } }

        public string LevelProfile
        {
            get { return levelProfile; }
            set
            {
                if (Set(ref levelProfile, value) && document != null && value != null)
                {
                    document.Settings.LevelProfile = value;
                }
            }
        }

        public bool ScopeWhole { get { return ScopeIs(ScopeKinds.Project); } set { SetScope(ScopeKinds.Project, value); } }
        public bool ScopeNbh { get { return ScopeIs(ScopeKinds.Nbh); } set { SetScope(ScopeKinds.Nbh, value); } }
        public bool ScopePlots { get { return ScopeIs(ScopeKinds.Plots); } set { SetScope(ScopeKinds.Plots, value); } }

        public string Nbh
        {
            get { return document == null ? null : document.Settings.Scope.Nbh; }
            set { if (document != null) { document.Settings.Scope.Nbh = value; Changed(); } }
        }

        public string Plots
        {
            get { return document == null ? null : document.Settings.Scope.Plots; }
            set { if (document != null) { document.Settings.Scope.Plots = value; Changed(); } }
        }

        public bool DryRun
        {
            get { return document != null && document.Settings.DryRunFirst; }
            set
            {
                if (document != null && document.Settings.DryRunFirst != value)
                {
                    document.Settings.DryRunFirst = value;
                    Changed();
                    RefreshBox();
                }
            }
        }

        public bool VillaMasses { get { return VillaModeIs(VillaModes.Masses); } set { SetVillaMode(VillaModes.Masses, value); } }
        public bool VillaLinks { get { return VillaModeIs(VillaModes.Links); } set { SetVillaMode(VillaModes.Links, value); } }

        public IReadOnlyList<StepStatus> Statuses { get { return statuses; } }

        // How often the pane loaded a document. The addinRun job of the test host reads it around each step, to see the pane load again
        // on each event of docs/design/09-risks.md open item 1
        public int Loads { get; private set; }

        // The pane of the active document, or of none: then it shows its labels with no values and every button grey
        public void Load(DocumentSession active)
        {
            Loads++;
            document = active;
            statusesFromOutside = false;
            // Revit run 3: a detached document has a Title but no saved path, so the Title alone would name a project with no work folder
            ProjectName = active == null ? string.Empty : !active.HasPath || string.IsNullOrEmpty(active.Title) ? PaneText.NotSaved : active.Title;
            RefreshAll();
        }

        public void LoadSettings(PaneSettings settings)
        {
            if (document == null)
            {
                throw PaneErrors.NoDocument();
            }
            document.Settings = settings;
            RefreshAll();
        }

        public void PickConfig(string path)
        {
            Settings.ConfigFile = path;
            RefreshAll();
        }

        public void PickRules(string path)
        {
            Settings.RulesFile = path;
            RefreshAll();
        }

        public void PickInput(string key, string path)
        {
            Settings.Pick(key, path);
            RefreshAll();
        }

        // R434. The names of the saved projects of Start from, the first named in the line of the first open box
        public void SetSavedStarts(IReadOnlyList<string> names)
        {
            StartSavedLine = names == null || names.Count == 0 ? PaneText.StartSavedNone : TextTemplate.Fill(PaneText.StartSaved, names[0]);
        }

        // Browse asks the user for a path by the row. DLUS and sales asks for each workbook by its role, one dialog after the
        // other, and a Cancel keeps the workbook the row had. The Setup row opens New project, so no settings file is ever picked by
        // its path in the pane (NEWPROJ-UI)
        public void Browse(FileRowViewModel row)
        {
            if (document == null)
            {
                return;
            }
            if (row == SettingsRow)
            {
                NewProjectRequested?.Invoke();
                return;
            }
            if (picker == null)
            {
                return;
            }
            if (row == RulesRow)
            {
                string rules = picker.File(null);
                if (rules != null)
                {
                    PickRules(rules);
                }
                return;
            }
            bool picked = false;
            foreach (string key in row.Keys)
            {
                string path = row.Folder ? picker.Folder() : picker.File(row.Keys.Count > 1 ? RoleTitle(key) : null);
                if (path != null)
                {
                    Settings.Pick(key, path);
                    picked = true;
                }
            }
            if (picked)
            {
                RefreshAll();
            }
        }

        private static string RoleTitle(string key)
        {
            return key == InputKeys.Dlus ? PaneText.DlusTitle : PaneText.SalesTitle;
        }

        // R366 of 4h gives the step state at 5c, the picture test gives the mock's until then
        public void SetSteps(IReadOnlyList<StepStatus> given)
        {
            if (given.Count != StepPlan.Count)
            {
                throw PaneErrors.Steps(given.Count);
            }
            statuses = given;
            statusesFromOutside = true;
            RefreshSteps();
            RefreshBox();
        }

        public DocumentSession Document
        {
            get { return document; }
        }

        // A real run of a button that committed is kept with the document, its step line and the lines after it follow by R366
        public void Committed(int button, StepRun run)
        {
            if (document == null)
            {
                return;
            }
            document.Runs[button] = run;
            statusesFromOutside = false;
            RefreshAll();
        }

        // The code the settings hold stays on the list, so a scope saved with the model is never shown empty
        public void SetNbhCodes(IReadOnlyList<string> codes)
        {
            string saved = document?.Settings.Scope.Nbh;
            IReadOnlyList<string> next = string.IsNullOrEmpty(saved) || codes.Contains(saved) ? codes : codes.Concat(new[] { saved }).ToList();
            if (next.SequenceEqual(NbhCodes))
            {
                return;
            }
            NbhCodes = next;

            // A combo given a new ItemsSource may write a null SelectedItem back, so the code saved with the model is put back
            if (!string.IsNullOrEmpty(saved) && Nbh != saved)
            {
                Nbh = saved;
            }
        }

        public static string StepName(int number)
        {
            string label = RibbonLayout.Button(number).Label;
            return label.Substring(label.IndexOf(' ') + 1);
        }

        public static string SizeText(long bytes)
        {
            return (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture);
        }

        // A hash that comes back while the rows are filled, as a test probe does at once, asks for one more pass
        private void RefreshAll()
        {
            if (refreshing)
            {
                again = true;
                return;
            }
            refreshing = true;
            try
            {
                do
                {
                    again = false;
                    RefreshOnce();
                }
                while (again);
            }
            finally
            {
                refreshing = false;
            }
        }

        private void RefreshOnce()
        {
            if (document != null)
            {
                document.Settings.VillaModeDefault = probe.VillaModeDefault(document.Settings.ConfigFile);
            }
            if (!statusesFromOutside)
            {
                statuses = document == null ? StepPlan.None(false) : StepPlan.Of(document.Settings.Chosen, document.Settings.Scope, StepsNow());
            }
            RefreshRow(SettingsRow, document == null ? null : new[] { document.Settings.ConfigFile });
            RefreshRow(RulesRow, document == null ? null : new[] { document.Settings.RulesFile });
            RefreshNames();
            foreach (FileRowViewModel row in InputRows)
            {
                RefreshRow(row, document == null ? null : row.Keys.Select(key => document.Settings.InputPath(key)).ToArray());
            }
            RefreshHelp();
            RefreshProfiles();
            if (document != null && !string.IsNullOrEmpty(document.Settings.Scope.Nbh) && !NbhCodes.Contains(document.Settings.Scope.Nbh))
            {
                NbhCodes = NbhCodes.Concat(new[] { document.Settings.Scope.Nbh }).ToList();
            }
            RefreshSteps();
            RefreshBox();
            BoxCommand.Refresh();
            ImportCommand.Refresh();
            ExportCommand.Refresh();
            NewProjectCommand.Refresh();
            foreach (string name in new[] { nameof(HasDocument), nameof(FirstOpen), nameof(InUse), nameof(ShownSteps), nameof(Settings), nameof(ScopeWhole), nameof(ScopeNbh), nameof(ScopePlots), nameof(Nbh), nameof(Plots), nameof(DryRun), nameof(VillaMasses), nameof(VillaLinks) })
            {
                Changed(name);
            }
        }

        private void RefreshRow(FileRowViewModel row, string[] paths)
        {
            if (paths == null)
            {
                row.Show(false, false, string.Empty, null);
                return;
            }
            var names = new List<string>();
            var tips = new List<string>();
            bool found = true;
            for (int at = 0; at < paths.Length; at++)
            {
                string path = paths[at];
                if (string.IsNullOrEmpty(path))
                {
                    found = false;
                    continue;
                }
                names.Add(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                tips.Add(path);
                InputFacts facts = probe.Look(path, row.Folder);
                found &= facts.Found;
                if (!facts.Found)
                {
                    continue;
                }
                SettingsInput input = null;
                if (row.Keys.Count > at)
                {
                    document.Settings.Inputs.TryGetValue(row.Keys[at], out input);
                }
                HashedInput known;
                if (hashes.TryGetValue(path, out known) && known.Size == facts.Size && known.Stamp == facts.Stamp)
                {
                    tips.Add(TextTemplate.Fill(PaneText.SizeAndHash, SizeText(facts.Size), Short(known.Sha256)));
                    if (input != null && input.Path == path)
                    {
                        input.Size = facts.Size;
                        input.Sha256 = known.Sha256;
                    }
                    continue;
                }
                tips.Add(TextTemplate.Fill(PaneText.Size, SizeText(facts.Size)));
                // The size and hash the settings held belong to the file before it changed
                if (input != null && input.Path == path)
                {
                    input.Size = null;
                    input.Sha256 = null;
                }
                Ask(path, row.Folder, facts);
            }
            string name = names.Count == 0 ? PaneText.NotChosen : string.Join(", ", names);
            // QL15. The row shows the file name alone, at 380 wide the size and hash showed on no row, so the tooltip gives each
            // under its full path
            row.Show(true, found && names.Count == paths.Length, name, tips.Count == 0 ? null : string.Join(Environment.NewLine, tips));
        }

        // NEWPROJ-UI, no file name ending in .json in the pane. The Setup row says whether the project has its setup, the Rules row
        // shows the project its rules file names, else that file's name with no extension. Neither shows a path in its tooltip
        private void RefreshNames()
        {
            if (document == null)
            {
                return;
            }
            SettingsRow.Show(true, SettingsRow.Found, document.Settings.Chosen ? PaneText.SetupSaved : PaneText.NotDone, null);
            string rules = document.Settings.RulesFile;
            string name = string.IsNullOrEmpty(rules) ? PaneText.NotChosen : probe.RulesName(rules) ?? Path.GetFileNameWithoutExtension(rules);
            RulesRow.Show(true, RulesRow.Found, name, null);
        }

        // The hash is kept by path, so it shows on whatever document is shown when it ends, the one of the request again after the
        // user came back to it
        private void Ask(string path, bool folder, InputFacts facts)
        {
            if (!hashing.Add(path))
            {
                return;
            }
            probe.Hash(path, folder, hash =>
            {
                hashing.Remove(path);
                if (hash != null)
                {
                    hashes[path] = new HashedInput(facts.Size, facts.Stamp, hash);
                }
                if (hash != null && document != null)
                {
                    RefreshAll();
                }
            });
        }

        // The SHA-256 of a path as the disk holds it now, null while it is not on disk or not hashed yet, and then its hash is asked
        private string KnownHash(string path, bool folder)
        {
            InputFacts facts = probe.Look(path, folder);
            if (!facts.Found)
            {
                return null;
            }
            if (hashes.TryGetValue(path, out HashedInput known) && known.Size == facts.Size && known.Stamp == facts.Stamp)
            {
                return known.Sha256;
            }
            Ask(path, folder, facts);
            return null;
        }

        // R366, W9. Each step with a last run as StepStates reads it now: the run of this session, else the last run of the settings,
        // the one hash over the inputs of its button by the hashes of the pane, none while one is not on disk or not hashed, and the
        // SHA-256 of each output of its record on disk
        private Dictionary<int, StepNow> StepsNow()
        {
            var steps = new Dictionary<int, StepNow>();
            for (int step = 1; step <= StepPlan.Count; step++)
            {
                StepRun run = LastRun(step);
                if (run == null)
                {
                    continue;
                }
                var now = new Dictionary<string, string>(StringComparer.Ordinal);
                bool missing = false;
                foreach (RunInput input in PaneRunState.InputsOf(document.Settings, step))
                {
                    string hash = string.IsNullOrEmpty(input.Path) ? null : KnownHash(input.Path, input.IsFolder);
                    missing |= hash == null;
                    now[input.Name] = hash;
                }
                var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string output in run.Outputs.Keys)
                {
                    string hash = KnownHash(output, false);
                    if (hash != null)
                    {
                        outputs[output] = hash;
                    }
                }
                steps[step] = new StepNow(run, missing ? null : StepHash.Combine(now), outputs);
            }
            return steps;
        }

        // A last run of the settings that does not read is no run, R367 refused it already when the time is no ISO text
        private StepRun LastRun(int step)
        {
            if (document.Runs.TryGetValue(step, out StepRun run))
            {
                return run;
            }
            if (!document.Settings.Project.LastRuns.TryGetValue(step.ToString(CultureInfo.InvariantCulture), out ButtonRun last) || last == null)
            {
                return null;
            }
            try
            {
                return StepRun.From(last, null);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private void RefreshHelp()
        {
            if (document == null)
            {
                InputHelp = string.Empty;
                return;
            }
            if (FirstOpen)
            {
                InputHelp = PaneText.InputHelpFirst;
                return;
            }
            int found = InputRows.Count(row => row.Found);
            InputHelp = found == InputRows.Count
                ? TextTemplate.Fill(PaneText.InputHelpAll, InputRows.Count)
                : TextTemplate.Fill(PaneText.InputHelpSome, found, InputRows.Count);
        }

        private void RefreshProfiles()
        {
            string fallback = null;
            LevelProfiles = document == null ? new string[0] : probe.LevelProfiles(document.Settings.RulesFile, out fallback);
            string chosen = document == null ? null : document.Settings.LevelProfile ?? fallback;
            if (chosen != null && !LevelProfiles.Contains(chosen))
            {
                LevelProfiles = LevelProfiles.Concat(new[] { chosen }).ToList();
            }
            Set(ref levelProfile, chosen, nameof(LevelProfile));
        }

        private void RefreshSteps()
        {
            int next = StepPlan.Next(statuses);
            bool shown = document != null && document.HasPath;
            foreach (StepRowViewModel step in Steps)
            {
                step.Show(shown ? statuses[step.Number - 1] : new StepStatus(StepKind.None), step.Number == next && shown);
            }
            FoldedStep.Show(shown ? statuses[FoldFrom - 1] : new StepStatus(StepKind.None), false);
        }

        private void RefreshBox()
        {
            int next = StepPlan.Next(statuses);
            if (FirstOpen)
            {
                BoxLabel = PaneText.StartLabel;
                BoxName = PaneText.StartName;
                BoxLine = PaneText.StartLine;
                BoxButton = PaneText.StartButton;
                BoxButtonShown = true;
                return;
            }
            BoxLabel = PaneText.NextLabel;
            BoxName = next == 0 ? string.Empty : StepName(next);
            BoxLine = next == 0 ? string.Empty : PaneText.StepLines[next - 1] + ". " + (DryRun ? PaneText.DryRunOn : PaneText.DryRunOff);
            BoxButton = next == 0 ? string.Empty : TextTemplate.Fill(PaneText.RunButton, next, StepName(next));
            BoxButtonShown = next != 0;
        }

        private void OnBox()
        {
            if (FirstOpen)
            {
                NewProjectRequested?.Invoke();
                return;
            }
            int next = StepPlan.Next(statuses);
            if (next != 0)
            {
                RunRequested?.Invoke(next);
            }
        }

        // W4, R367. Import reads a pre-setting into the settings in memory, a pick as Browse is, so it reaches the document with the
        // next real run. A file the reader refuses or cannot read is told and changes nothing
        private void ImportPreSetting()
        {
            string path = document == null || picker == null ? null : picker.File(PaneText.UseSaved);
            if (path == null)
            {
                return;
            }
            try
            {
                PreSetting.Import(File.ReadAllText(path), document.Settings.Project);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException)
            {
                Told?.Invoke(error.Message);
                return;
            }
            RefreshAll();
        }

        // Export writes the pre-setting of the settings in memory to the file the user names, outside any work folder or input root
        // only as the user chooses it, since the pre-setting holds no drawing path and no coordinate
        private void ExportPreSetting()
        {
            string path = document == null || picker == null ? null : picker.Save(PaneText.Export);
            if (path == null)
            {
                return;
            }
            try
            {
                File.WriteAllText(path, PreSetting.Export(document.Settings.Project), new System.Text.UTF8Encoding(false));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Told?.Invoke(error.Message);
            }
        }

        private bool ScopeIs(string kind)
        {
            return document != null && document.Settings.Scope.Kind == kind;
        }

        private void SetScope(string kind, bool on)
        {
            if (on && document != null && document.Settings.Scope.Kind != kind)
            {
                document.Settings.Scope.Kind = kind;
                Changed(nameof(ScopeWhole));
                Changed(nameof(ScopeNbh));
                Changed(nameof(ScopePlots));
            }
        }

        private bool VillaModeIs(string mode)
        {
            return document != null && document.Settings.VillaMode == mode;
        }

        private void SetVillaMode(string mode, bool on)
        {
            if (on && document != null && document.Settings.VillaMode != mode)
            {
                document.Settings.VillaMode = mode;
                Changed(nameof(VillaMasses));
                Changed(nameof(VillaLinks));
            }
        }

        private static string Short(string sha256)
        {
            return sha256.Length > 8 ? sha256.Substring(0, 8) : sha256;
        }
    }

    public sealed class FileRowViewModel : Bindable
    {
        private bool shown;
        private bool found;
        private string fileName = string.Empty;
        private string toolTip;

        public FileRowViewModel(string label, bool folder, params string[] keys)
        {
            Label = label;
            Folder = folder;
            Keys = keys;
        }

        public string Label { get; }
        public bool Folder { get; }
        public IReadOnlyList<string> Keys { get; }
        public bool Shown { get { return shown; } private set { Set(ref shown, value); } }
        public bool Found { get { return found; } private set { Set(ref found, value); } }
        public string FileName { get { return fileName; } private set { Set(ref fileName, value); } }
        public string ToolTip { get { return toolTip; } private set { Set(ref toolTip, value); } }

        internal void Show(bool on, bool isFound, string name, string tip)
        {
            Shown = on;
            Found = isFound;
            FileName = name;
            ToolTip = tip;
        }
    }

    public enum MarkKind
    {
        Waiting,
        Next,
        Done,
        Issues,
    }

    public sealed class StepRowViewModel : Bindable
    {
        private StepKind chip;
        private string chipText = string.Empty;
        private string chipTip;
        private MarkKind mark;
        private bool runnable;
        private string rowTip;

        public StepRowViewModel(int number, string name, string line, Action<int> run = null)
        {
            Number = number;
            Name = name;
            Line = line;
            // R444. A done step runs again, the next step runs, a step that waits does nothing and its chip says what it needs
            RunCommand = new PaneCommand(() =>
            {
                if (Runnable)
                {
                    run?.Invoke(Number);
                }
            }, () => true);
            this.run = run;
        }

        private readonly Action<int> run;

        public PaneCommand RunCommand { get; }
        public bool Runnable { get { return runnable; } private set { Set(ref runnable, value); } }
        public string RowTip { get { return rowTip; } private set { Set(ref rowTip, value); } }

        public int Number { get; }
        public string Name { get; }
        public string Line { get; }
        public StepKind Chip { get { return chip; } private set { Set(ref chip, value); } }
        public string ChipText { get { return chipText; } private set { Set(ref chipText, value); } }
        public string ChipTip { get { return chipTip; } private set { Set(ref chipTip, value); } }
        public MarkKind Mark { get { return mark; } private set { Set(ref mark, value); } }

        internal void Show(StepStatus status, bool next)
        {
            Chip = status.Kind;
            ChipTip = status.Kind == StepKind.Issues ? TextTemplate.Fill(PaneText.DoneWithIssues, TextTemplate.Count(status.Value)) : null;
            switch (status.Kind)
            {
                case StepKind.Done: ChipText = PaneText.Done; break;
                case StepKind.Issues: ChipText = TextTemplate.Fill(PaneText.Issues, TextTemplate.Count(status.Value)); break;
                case StepKind.Ready: ChipText = PaneText.Ready; break;
                case StepKind.NeedsStep: ChipText = TextTemplate.Fill(PaneText.NeedsStep, status.Value); break;
                case StepKind.NeedsSettings: ChipText = PaneText.NeedsSettings; break;
                default: ChipText = string.Empty; break;
            }
            Mark = status.Kind == StepKind.Done ? MarkKind.Done : status.Kind == StepKind.Issues ? MarkKind.Issues : next ? MarkKind.Next : MarkKind.Waiting;
            Runnable = run != null && (status.Kind == StepKind.Done || status.Kind == StepKind.Issues || status.Kind == StepKind.Ready);
            RowTip = Runnable ? TextTemplate.Fill(PaneText.RunButton, Number, Name) : ChipText.Length == 0 ? null : ChipText;
        }
    }

    public abstract class Bindable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            Changed(name);
            return true;
        }

        protected void Changed([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class PaneCommand : ICommand
    {
        private readonly Action run;
        private readonly Func<bool> can;

        public PaneCommand(Action run, Func<bool> can)
        {
            this.run = run;
            this.can = can;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return can();
        }

        public void Execute(object parameter)
        {
            run();
        }

        public void Refresh()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
