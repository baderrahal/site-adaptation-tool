using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.AddIn.Pane
{
    // R680. One stage card of the pane: its title, a status, one plain line, the i text and one main button
    public sealed class StageCard : Bindable
    {
        private string status = string.Empty;
        private string line = string.Empty;
        private bool good;

        public StageCard(int number, string title, string help, string button, PaneCommand command)
        {
            Number = number;
            Title = title;
            Help = help;
            Button = button;
            Command = command;
        }

        public int Number { get; }
        public string Title { get; }
        public string Help { get; }
        public string Button { get; }
        public PaneCommand Command { get; }
        public string Status { get { return status; } internal set { Set(ref status, value); } }
        public string Line { get { return line; } internal set { Set(ref line, value); } }

        // Done with no issue reads green, every other status in the wait colour
        public bool Good { get { return good; } internal set { Set(ref good, value); } }
    }

    // R680. The status of a stage from the status of its steps of version 1: stage 1 holds steps 1 to 3, stage 2 steps 4 to 8, stage 3 step 9
    public static class StageStatus
    {
        public static readonly int[][] Steps = { new[] { 1, 2, 3 }, new[] { 4, 5, 6, 7, 8 }, new[] { 9 } };

        // R765, F7 of run 1. readThere: the work folder of the model holds a read. A model with a read and no setup was closed without save
        // after Save setup, as the setup lives in the model and the reads in the work folder, so the card says that plainly
        public static string Of(int stage, IReadOnlyList<StepStatus> statuses, bool hasDocument, bool settingsChosen, bool readThere = false)
        {
            if (!hasDocument)
            {
                return StageText.NoDocument;
            }
            if (!settingsChosen)
            {
                return readThere ? StageText.SetupNotSaved : StageText.SetUpFirst;
            }
            List<StepStatus> own = Own(stage, statuses);
            int? done = IssuesWhenDone(stage, statuses);
            if (done != null)
            {
                return done == 0 ? StageText.Done : TextTemplate.Fill(StageText.DoneIssues, done.Value);
            }
            if (own.Any(step => step.Kind == StepKind.Ready || step.Kind == StepKind.Done || step.Kind == StepKind.Issues))
            {
                return StageText.Ready;
            }
            // A step of this stage waits on a step of an earlier stage, which names that stage
            int waited = own.Where(step => step.Kind == StepKind.NeedsStep).Select(step => StageOf(step.Value)).Where(other => other < stage).DefaultIfEmpty(0).Min();
            return waited > 0 ? TextTemplate.Fill(StageText.Waits, waited) : StageText.NotStarted;
        }

        // R816, F34 of run 2. The issues of the steps of a stage when every one of them ran, the count of "Done, n issues", else null. The
        // steps run on the scope, so card 3 counts what card 2 counts
        public static int? IssuesWhenDone(int stage, IReadOnlyList<StepStatus> statuses)
        {
            List<StepStatus> own = Own(stage, statuses);
            if (own.Count == 0 || !own.All(step => step.Kind == StepKind.Done || step.Kind == StepKind.Issues))
            {
                return null;
            }
            return own.Where(step => step.Kind == StepKind.Issues).Sum(step => step.Value);
        }

        private static List<StepStatus> Own(int stage, IReadOnlyList<StepStatus> statuses)
        {
            return Steps[stage - 1].Where(step => step <= statuses.Count).Select(step => statuses[step - 1]).ToList();
        }

        public static int StageOf(int step)
        {
            for (int at = 0; at < Steps.Length; at++)
            {
                if (Steps[at].Contains(step))
                {
                    return at + 1;
                }
            }
            return 0;
        }
    }

    // R681. The data check of the latest read of the work folder, the folder a read wrote data_check.json and project_profile.json into
    public static class StageRead
    {
        public static string Latest(string workFolder)
        {
            if (string.IsNullOrEmpty(workFolder) || !Directory.Exists(workFolder))
            {
                return null;
            }
            try
            {
                return Directory.GetDirectories(workFolder)
                    .Where(folder => File.Exists(Path.Combine(folder, DataCheckRows.FileName)) && File.Exists(Path.Combine(folder, ConfirmProfile.ConfirmProfileStep.ProfileFile)))
                    .OrderByDescending(folder => File.GetLastWriteTimeUtc(Path.Combine(folder, DataCheckRows.FileName)))
                    .FirstOrDefault();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                return null;
            }
        }

        // The data check of a read folder. The worker writes it when the check stops too, so a stopped check and one that does not read
        // both come back with Stopped set, the reason the card shows, as the window of version 1 refuses to confirm a stopped check
        public static DataCheckResult Read(string readFolder)
        {
            if (readFolder == null)
            {
                return new DataCheckResult();
            }
            try
            {
                string plots = Path.Combine(readFolder, DataCheckRows.PlotsFileName);
                return DataCheckRows.ReadResult(WorkFiles.ReadText(Path.Combine(readFolder, DataCheckRows.FileName)), File.Exists(plots) ? WorkFiles.ReadText(plots) : null);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is Newtonsoft.Json.JsonException || failure is InvalidDataException || failure is FormatException)
            {
                return new DataCheckResult { Stopped = failure.Message };
            }
        }
    }

    // R681. Choose project folder sets inputRoot of the config of a new project, the copy of the shipped set under the work root of R486. The shipped
    // set and a past job are never written. Null when written, else the plain reason
    public static class ProjectFolderPick
    {
        public static string Refusal(string configFile, string workRoot, string chosen, string workFolder)
        {
            string projects = string.IsNullOrEmpty(workRoot) ? null : Path.Combine(workRoot, Common.Settings.ShippedSet.ProjectsFolder);
            if (string.IsNullOrEmpty(configFile) || projects == null || !WorkFolder.Inside(Path.GetDirectoryName(Path.GetFullPath(configFile)), projects))
            {
                return StageText.FolderOwnCopyOnly;
            }
            if (WorkFolder.Inside(workFolder, chosen))
            {
                return StageText.FolderHoldsWork;
            }
            return null;
        }

        // R763. The project folder the config names, null with none, no config or one that does not read
        public static string Chosen(string configFile)
        {
            if (string.IsNullOrEmpty(configFile) || !File.Exists(configFile))
            {
                return null;
            }
            try
            {
                string root = (string)JObject.Parse(WorkFiles.ReadText(configFile))["inputRoot"];
                return string.IsNullOrWhiteSpace(root) ? null : root;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is Newtonsoft.Json.JsonException || failure is InvalidCastException || failure is ArgumentException)
            {
                return null;
            }
        }

        internal static bool SameFolder(string one, string other)
        {
            try
            {
                return string.Equals(Path.GetFullPath(one).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(other).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException || failure is PathTooLongException)
            {
                return false;
            }
        }

        public static string Write(string configFile, string workRoot, string chosen, string workFolder)
        {
            string refused = Refusal(configFile, workRoot, chosen, workFolder);
            if (refused != null)
            {
                return refused;
            }
            try
            {
                JObject config = JObject.Parse(WorkFiles.ReadText(configFile));
                // R769, F24 of the short check. The config is an input of step 1, so the same folder chosen again leaves the file as it is and step 1 done
                if (config["inputRoot"]?.Type == JTokenType.String && SameFolder((string)config["inputRoot"], chosen))
                {
                    return null;
                }
                config["inputRoot"] = Path.GetFullPath(chosen);
                string temporary = configFile + ".choosing";
                File.WriteAllText(temporary, config.ToString(Newtonsoft.Json.Formatting.Indented));
                File.Copy(temporary, configFile, true);
                File.Delete(temporary);
                return null;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is Newtonsoft.Json.JsonException)
            {
                return TextTemplate.Fill(StageText.FolderNotWritten, failure.Message);
            }
        }
    }

    // R680. The pane in 3 stages over the view model of the pane of version 1, which stays whole under Advanced
    public sealed class StagesViewModel : Bindable
    {
        public const int SetUpStep = 1;
        public const int ReadDataStep = 2;
        public const int ReviewStep = 3;

        // R769, F24 of the short check. The step Choose project folder runs once the folder is written: the read when step 1 is done on the config
        // as it is now, else step 1, as a new folder changes the config, an input of step 1, and the read would only meet "2 Read data needs step 1"
        public static int StepAfterFolder(IReadOnlyList<StepStatus> statuses)
        {
            StepKind first = statuses != null && statuses.Count > 0 ? statuses[0].Kind : StepKind.None;
            return first == StepKind.Done || first == StepKind.Issues ? ReadDataStep : SetUpStep;
        }

        // R769. The same with the folder the config named before the write: another folder changed the config, so step 1 runs again whatever the
        // statuses still say before the pane hashes the config again
        public static int StepAfterFolder(IReadOnlyList<StepStatus> statuses, string before, string chosen)
        {
            return before != null && chosen != null && ProjectFolderPick.SameFolder(before, chosen) ? StepAfterFolder(statuses) : SetUpStep;
        }
        public const int ReportStep = 9;
        private bool settingsOpen;
        private string rulesLine;
        private string buildLine;
        private bool previewFirst = true;
        private string stopLine;
        private string readLine = StageText.NoRead;
        private string pickLine;
        private string readFolder;
        private string fixFolder;
        private DateTime readStamp;
        // A fresh pane has no read to compare with, so the first look always fills card 1
        private bool readSeen;

        public StagesViewModel(PaneViewModel model)
        {
            Model = model;
            Cards = new ReadOnlyCollection<StageCard>(new[]
            {
                new StageCard(1, StageText.Stage1, StageText.Stage1Help, StageText.ChooseFolder, new PaneCommand(ChooseFolder, () => model.InUse)),
                new StageCard(2, StageText.Stage2, StageText.Stage2Help, StageText.Build, new PaneCommand(PressBuild, () => model.InUse && readFolder != null)),
                new StageCard(3, StageText.Stage3, StageText.Stage3Help, StageText.Report, new PaneCommand(() => model.RequestRun(ReportStep), () => model.InUse && Waits(ReportStep) == 0)),
            });
            FixItemsCommand = new PaneCommand(() => FixItemsRequested?.Invoke(fixFolder), () => fixFolder != null);
            SettingsCommand = new PaneCommand(() => SettingsOpen = !SettingsOpen, () => true);
            RulesCommand = new PaneCommand(() => RulesRequested?.Invoke(), () => model.InUse);
            UpdateCommand = new PaneCommand(() => UpdateRequested?.Invoke(), () => model.InUse);
            SetUpCommand = new PaneCommand(() =>
            {
                pickLine = null;
                model.RequestRun(SetUpStep);
            }, () => model.InUse);
            ReviewCommand = new PaneCommand(() => model.RequestRun(ReviewStep), () => model.InUse && Waits(ReviewStep) == 0);
            model.PropertyChanged += OnModelChanged;
            Refresh();
        }

        public PaneViewModel Model { get; }
        public ReadOnlyCollection<StageCard> Cards { get; }
        public StageCard Card1 { get { return Cards[0]; } }
        public StageCard Card2 { get { return Cards[1]; } }
        public StageCard Card3 { get { return Cards[2]; } }
        public ObservableCollection<DataCheckRow> CheckRows { get; } = new ObservableCollection<DataCheckRow>();
        public bool HasRows { get { return CheckRows.Count > 0; } }
        public PaneCommand FixItemsCommand { get; }
        public PaneCommand SettingsCommand { get; }
        public bool SettingsOpen { get { return settingsOpen; } set { Set(ref settingsOpen, value); } }
        public PaneCommand RulesCommand { get; }

        // R761, F1 of run 1. Card 1 holds step 1 Set up site, and Choose project folder waits for it, so an empty model never meets the box
        // "2 Read data needs step 1". The button says the label of the ribbon button, so it reads as the step row of Advanced
        public PaneCommand SetUpCommand { get; }
        public string SetUpButton { get { return RibbonLayout.Button(SetUpStep).Label; } }

        // R762, F3 of run 1. Card 1 holds step 3 Review data, so a read is reviewed from card 1 and stage 1 can end there
        public PaneCommand ReviewCommand { get; }
        public string ReviewButton { get { return RibbonLayout.Button(ReviewStep).Label; } }

        // R763, F9 of run 1. Report of card 3 is grey while step 9 waits on a step before it, as card 3 then says it waits for stage 2.
        // A step ran when it is done, with or without issues
        private bool Ran(int step)
        {
            if (step > Model.Statuses.Count)
            {
                return false;
            }
            StepKind kind = Model.Statuses[step - 1].Kind;
            return kind == StepKind.Done || kind == StepKind.Issues;
        }

        // The step a step waits on, 0 when it may run
        private int Waits(int step)
        {
            if (step > Model.Statuses.Count)
            {
                return step - 1;
            }
            StepStatus status = Model.Statuses[step - 1];
            return status.Kind == StepKind.NeedsStep ? status.Value : status.Kind == StepKind.NeedsSettings ? step : 0;
        }

        // R686. The scope of the build in the words of the project, and the Preview tick, on by default (CHOSEN)
        // R689. Card 3: the issues of the last build, the source mix of its values and Update from report
        private StageResults results = new StageResults();
        public PaneCommand UpdateCommand { get; private set; }
        public Func<StageResults> ResultsNow { get; set; }
        // R816, F34 of run 2. Once card 2 is done the count is the issues of its steps on the scope, as card 2 says it, never the issues the
        // build plan lists for the whole read. Before that the plan count stands, as R689 gave it
        public string IssuesLine
        {
            get
            {
                int? scope = Model.HasDocument && Model.InUse ? StageStatus.IssuesWhenDone(BuildStage, Model.Statuses) : null;
                if (scope != null)
                {
                    return TextTemplate.Fill(StageResultsText.IssuesCount, scope.Value);
                }
                return results.HasPlan || results.Issues.Count > 0 ? TextTemplate.Fill(StageResultsText.IssuesCount, results.Issues.Count) : StageResultsText.NoBuild;
            }
        }

        private const int BuildStage = 2;
        public string MixLine { get { return results.HasPlan ? results.Mix.Line : string.Empty; } }
        public bool RuleIssue { get { return results.RuleIssue; } }
        // R691. Q504: the plots the plan lists with no road level
        // R802, F30 of short check 2. With a plan of 4 Check the line counts plotsWithNoRoadLevel of that plan, the plots of the scope steps 5 to 8
        // build from, never the plots of the whole read the build plan lists
        public string NoRoadLevelLine
        {
            get
            {
                if (planCount != null)
                {
                    return planCount.NoRoadLevel > 0 ? TextTemplate.Fill(StageResultsText.NoRoadLevel, planCount.NoRoadLevel) : string.Empty;
                }
                return results.NoRoadLevelCount > 0 ? TextTemplate.Fill(StageResultsText.NoRoadLevel, results.NoRoadLevelCount) : string.Empty;
            }
        }

        // R801, R802. The counts of the plan of the last run of 4 Check, read again only when the plan changed
        private CheckPlanCount planCount;
        private string planStamp;

        private void RefreshPlan()
        {
            string plan = Model.HasDocument ? CheckPlanCount.LatestPlan(WorkFolderNow?.Invoke()) : null;
            string stamp = plan == null ? null : plan + "@" + File.GetLastWriteTimeUtc(plan).Ticks;
            if (stamp == planStamp)
            {
                return;
            }
            planStamp = stamp;
            planCount = CheckPlanCount.Read(plan);
        }

        // R801, F29 of short check 2. Card 2 says what the last press of Build said, then the plots the plan of 4 Check leaves out with why
        private string Card2Text()
        {
            string plan = planCount?.Line;
            return string.Join(" ", new[] { buildLine, plan }.Where(part => !string.IsNullOrEmpty(part)));
        }

        public bool HasMix { get { return results.HasPlan && results.Mix.Total > 0; } }

        // The widths of the bar, one share per source, a source with no value takes no room
        public System.Windows.GridLength MixComputed { get { return Share(results.Mix.ComputedCount); } }
        public System.Windows.GridLength MixRule { get { return Share(results.Mix.RuleCount); } }
        public System.Windows.GridLength MixPerson { get { return Share(results.Mix.PersonCount); } }
        public System.Windows.GridLength MixMissing { get { return Share(results.Mix.MissingCount); } }
        public System.Windows.GridLength MixOther { get { return Share(results.Mix.OtherCount); } }

        private static System.Windows.GridLength Share(int count)
        {
            return new System.Windows.GridLength(count, System.Windows.GridUnitType.Star);
        }

        public string ScopeLine { get { return SiteBuildPlan.ScopeLine(Model.Settings?.Scope); } }
        public bool PreviewFirst { get { return previewFirst; } set { Set(ref previewFirst, value); } }

        // R760, F12 of run 1. The box of a Build that stopped: the step and the reason as BuildChain.Ended words them, so the person
        // need not find them at the end of the long line of card 2. It stays until the next press of Build
        public string StopLine { get { return stopLine; } private set { Set(ref stopLine, value); Changed(nameof(HasStop)); } }
        public bool HasStop { get { return !string.IsNullOrEmpty(stopLine); } }

        // R760. A step of the Build ended with the line chain.Ended gave. A stop, a roll back or a cancel shows the box, a step that
        // ended well or goes on outside Revit leaves it as it is
        public void ShowStop(RunEnd end, string line)
        {
            if (end == RunEnd.Stopped || end == RunEnd.RolledBack || end == RunEnd.Cancelled)
            {
                StopLine = line;
            }
        }

        // R763, F9 of run 1. The folder dialog opens at the project folder chosen before, read from the config copy of the project
        // R768, F1 and F2. Before step 1 ran, the folder goes into the config copy of the project, an empty Baseplan row takes the Baseplan of that
        // folder, and 1 Set up site runs on it. Only a folder a person chose here fills the row, never the inputRoot a config copy came with. The read follows at the next press, once the site is placed, so an empty model never meets "2 Read data needs
        // step 1" and Set up site never waits on a folder no one could choose. A refusal stands in card 1
        private void ChooseFolder()
        {
            DialogPicker.Start = ProjectFolderPick.Chosen(Model.Settings?.ConfigFile) ?? DialogPicker.Start;
            pickLine = null;
            if (StepAfterFolder(Model.Statuses) == ReadDataStep)
            {
                ChooseFolderRequested?.Invoke();
                return;
            }
            string chosen = PickFolder?.Invoke();
            if (string.IsNullOrEmpty(chosen) || Model.Settings == null)
            {
                Card1.Line = Card1Line();
                return;
            }
            string before = ProjectFolderPick.Chosen(Model.Settings.ConfigFile);
            pickLine = ProjectFolderPick.Write(Model.Settings.ConfigFile, WorkRootNow?.Invoke(), chosen, WorkFolderNow?.Invoke());
            if (pickLine == null)
            {
                FolderChosen(before, chosen);
            }
            Card1.Line = Card1Line();
        }

        // R768, R769, F24 of the short check. After a folder is written into the config, by card 1 before step 1 or by PaneHost.ChooseProjectFolder
        // after it: a Baseplan the folder chosen before gave goes with it, an empty Baseplan row takes the one of the new folder, and the read runs
        // only when step 1 still holds on the config, else step 1 runs first, never the box "2 Read data needs step 1". before is the folder the
        // config named before the write
        public void FolderChosen(string before, string chosen)
        {
            if (Model.Settings == null)
            {
                return;
            }
            string row = Model.Settings.InputPath(InputKeys.Baseplan);
            if (WorkFolder.Inside(row, before) && !WorkFolder.Inside(row, chosen))
            {
                Model.PickInput(InputKeys.Baseplan, null);
            }
            Model.PickConfig(Model.Settings.ConfigFile);
            Fast7.SetUpBaseplan.Fill(Model.Settings, Model.PickInput);
            Model.RequestRun(StepAfterFolder(Model.Statuses, before, chosen));
        }

        // The line of card 1: a refused folder, else the ask for step 1, else the line of the read
        private string Card1Line()
        {
            return pickLine ?? (Model.InUse && !Ran(SetUpStep) ? StageText.SetUpBeforeFolder : readLine);
        }

        // The folder dialog and the work root, as the Revit side has them, so a test gives its own
        public Func<string> PickFolder { get; set; } = () => new DialogPicker().Folder();
        public Func<string> WorkRootNow { get; set; } = () => WorkFolder.Root(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), WorkFolder.ReadIfThere);

        private void PressBuild()
        {
            StopLine = null;
            BuildRequested?.Invoke();
        }

        // R686. What the last press of Build said, the plan, its stop or its coordinates
        public void ShowBuild(string line)
        {
            buildLine = line;
            RefreshPlan();
            Card2.Line = Card2Text();
        }

        // R683. The one amber line of the pane until every rule of the project is confirmed, null when they all are
        public string RulesLine { get { return rulesLine; } private set { Set(ref rulesLine, value); Changed(nameof(HasRulesLine)); } }
        public bool HasRulesLine { get { return !string.IsNullOrEmpty(rulesLine); } }

        // The Revit side opens the folder picker and the Confirm window, the pane holds no window
        public event Action ChooseFolderRequested;
        public event Action<string> FixItemsRequested;
        public event Action RulesRequested;
        public event Action BuildRequested;
        public event Action UpdateRequested;

        // The work folder of the active document, null with none
        public Func<string> WorkFolderNow { get; set; }

        // The amber line of the rules of the active project, null with none to say
        public Func<string> RulesLineNow { get; set; }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            bool open = Model.HasDocument;
            bool chosen = Model.InUse;
            RefreshRead();
            for (int stage = 1; stage <= Cards.Count; stage++)
            {
                Cards[stage - 1].Status = StageStatus.Of(stage, Model.Statuses, open, chosen, readFolder != null);
                Cards[stage - 1].Good = Cards[stage - 1].Status == StageText.Done;
                Cards[stage - 1].Command.Refresh();
            }
            RefreshPlan();
            Card2.Line = Card2Text();
            Changed(nameof(ScopeLine));
            Card3.Line = StageResultsText.ReportLine;
            UpdateCommand.Refresh();
            SetUpCommand.Refresh();
            ReviewCommand.Refresh();
            results = (Model.HasDocument ? ResultsNow?.Invoke() : null) ?? new StageResults();
            Changed(nameof(IssuesLine));
            Changed(nameof(MixLine));
            Changed(nameof(RuleIssue));
            Changed(nameof(HasMix));
            Changed(nameof(NoRoadLevelLine));
            Changed(nameof(MixComputed));
            Changed(nameof(MixRule));
            Changed(nameof(MixPerson));
            Changed(nameof(MixMissing));
            Changed(nameof(MixOther));
            RulesCommand.Refresh();
            RulesLine = Model.HasDocument && Model.InUse ? RulesLineNow?.Invoke() : null;
            Card1.Line = Card1Line();
        }

        // The rows of the latest read, read again only when its data check changed
        private void RefreshRead()
        {
            string latest = Model.HasDocument ? StageRead.Latest(WorkFolderNow?.Invoke()) : null;
            DateTime stamp = latest == null ? DateTime.MinValue : File.GetLastWriteTimeUtc(Path.Combine(latest, DataCheckRows.FileName));
            if (readSeen && latest == readFolder && stamp == readStamp)
            {
                return;
            }
            readFolder = latest;
            readStamp = stamp;
            readSeen = true;
            CheckRows.Clear();
            DataCheckResult check = StageRead.Read(latest);
            foreach (DataCheckRow row in check.Rows)
            {
                CheckRows.Add(row);
            }
            fixFolder = latest != null && check.Stopped == null ? latest : null;
            readLine = latest == null ? StageText.NoRead : check.Stopped != null ? TextTemplate.Fill(StageText.ReadStopped, check.Stopped) : string.Empty;
            Changed(nameof(HasRows));
            FixItemsCommand.Refresh();
            Card2.Command.Refresh();
        }
    }
}
