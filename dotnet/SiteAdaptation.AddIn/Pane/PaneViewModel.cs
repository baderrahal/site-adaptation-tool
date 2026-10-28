using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/12-ui-spec.md 7.2. The pane of the active document, top to bottom, with no Revit type, so its logic is
    // tested without Revit. Every text comes from PaneText, the ribbon labels or the settings
    public sealed class PaneViewModel : Bindable
    {
        private readonly IInputProbe probe;
        private DocumentSession document;
        private IReadOnlyList<StepStatus> statuses = StepPlan.Before5c(false);
        private bool statusesFromOutside;
        private int generation;
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

        public PaneViewModel(IInputProbe probe)
        {
            this.probe = probe;
            SettingsRow = new FileRowViewModel(PaneText.Settings, false);
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
            });
            Steps = new ReadOnlyCollection<StepRowViewModel>(RibbonLayout.Buttons.Select(button => new StepRowViewModel(button.Number, StepName(button.Number), PaneText.StepLines[button.Number - 1])).ToList());
            BoxCommand = new PaneCommand(OnBox, () => document != null);
            Load(null);
        }

        // The box button asks for the settings file on first open, else for a run of the ribbon button it names
        public event Action ChooseSettingsRequested;
        public event Action<int> RunRequested;

        public bool HasDocument { get { return document != null; } }
        public bool FirstOpen { get { return document == null || !document.Settings.Chosen; } }
        public PaneSettings Settings { get { return document == null ? null : document.Settings; } }
        public string ProjectName { get { return projectName; } private set { Set(ref projectName, value); } }
        public string BoxLabel { get { return boxLabel; } private set { Set(ref boxLabel, value); } }
        public string BoxName { get { return boxName; } private set { Set(ref boxName, value); } }
        public string BoxLine { get { return boxLine; } private set { Set(ref boxLine, value); } }
        public string BoxButton { get { return boxButton; } private set { Set(ref boxButton, value); } }
        public bool BoxButtonShown { get { return boxButtonShown; } private set { Set(ref boxButtonShown, value); } }
        public PaneCommand BoxCommand { get; }
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

        // The pane of the active document, or of none: then it shows its labels with no values and every button grey
        public void Load(DocumentSession active)
        {
            document = active;
            generation++;
            statusesFromOutside = false;
            ProjectName = active == null ? string.Empty : string.IsNullOrEmpty(active.Title) ? PaneText.NotSaved : active.Title;
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

        // The paths of one row in the order of its keys, so DLUS and sales takes two
        public void PickInput(FileRowViewModel row, IReadOnlyList<string> paths)
        {
            if (paths.Count == 0 || paths.Count > row.Keys.Count)
            {
                throw PaneErrors.Paths(row.Keys.Count, paths.Count);
            }
            for (int at = 0; at < paths.Count; at++)
            {
                Settings.Pick(row.Keys[at], paths[at]);
            }
            RefreshAll();
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

        public void SetNbhCodes(IReadOnlyList<string> codes)
        {
            NbhCodes = codes;
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
            if (!statusesFromOutside)
            {
                statuses = StepPlan.Before5c(document != null && document.Settings.Chosen);
            }
            RefreshRow(SettingsRow, document == null ? null : new[] { document.Settings.ConfigFile });
            RefreshRow(RulesRow, document == null ? null : new[] { document.Settings.RulesFile });
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
            foreach (string name in new[] { nameof(HasDocument), nameof(FirstOpen), nameof(Settings), nameof(ScopeWhole), nameof(ScopeNbh), nameof(ScopePlots), nameof(Nbh), nameof(Plots), nameof(DryRun), nameof(VillaMasses), nameof(VillaLinks) })
            {
                Changed(name);
            }
        }

        private void RefreshRow(FileRowViewModel row, string[] paths)
        {
            if (paths == null)
            {
                row.Show(false, false, string.Empty, string.Empty, null);
                return;
            }
            var names = new List<string>();
            var details = new List<string>();
            var tips = new List<string>();
            bool found = true;
            int stamp = generation;
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
                InputFile input = null;
                if (row.Keys.Count > at)
                {
                    document.Settings.Inputs.TryGetValue(row.Keys[at], out input);
                }
                HashedInput known;
                if (hashes.TryGetValue(path, out known) && known.Size == facts.Size && known.Stamp == facts.Stamp)
                {
                    details.Add(TextTemplate.Fill(PaneText.SizeAndHash, SizeText(facts.Size), Short(known.Sha256)));
                    if (input != null && input.Path == path)
                    {
                        input.Size = facts.Size;
                        input.Sha256 = known.Sha256;
                    }
                    continue;
                }
                details.Add(TextTemplate.Fill(PaneText.Size, SizeText(facts.Size)));
                if (hashing.Add(path))
                {
                    probe.Hash(path, row.Folder, hash =>
                    {
                        hashing.Remove(path);
                        if (hash != null)
                        {
                            hashes[path] = new HashedInput(facts.Size, facts.Stamp, hash);
                        }
                        if (hash != null && stamp == generation && document != null)
                        {
                            RefreshAll();
                        }
                    });
                }
            }
            string name = names.Count == 0 ? PaneText.NotChosen : string.Join(", ", names);
            string detail = string.Join(", ", details);
            // At 380 wide the grey run is cut on a long name, so the tooltip gives it again under the full path
            if (detail.Length > 0)
            {
                tips.Add(detail);
            }
            row.Show(true, found && names.Count == paths.Length, name, detail, tips.Count == 0 ? null : string.Join(Environment.NewLine, tips));
        }

        private void RefreshHelp()
        {
            if (document == null)
            {
                InputHelp = string.Empty;
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
                ChooseSettingsRequested?.Invoke();
                return;
            }
            int next = StepPlan.Next(statuses);
            if (next != 0)
            {
                RunRequested?.Invoke(next);
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
        private string detail = string.Empty;
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
        public string Detail { get { return detail; } private set { Set(ref detail, value); } }
        public string ToolTip { get { return toolTip; } private set { Set(ref toolTip, value); } }

        internal void Show(bool on, bool isFound, string name, string details, string tip)
        {
            Shown = on;
            Found = isFound;
            FileName = name;
            Detail = details;
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

        public StepRowViewModel(int number, string name, string line)
        {
            Number = number;
            Name = name;
            Line = line;
        }

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
