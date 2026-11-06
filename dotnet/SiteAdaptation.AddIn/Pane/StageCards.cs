using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;

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

        public static string Of(int stage, IReadOnlyList<StepStatus> statuses, bool hasDocument, bool settingsChosen)
        {
            if (!hasDocument)
            {
                return StageText.NoDocument;
            }
            if (!settingsChosen)
            {
                return StageText.SetUpFirst;
            }
            List<StepStatus> own = Steps[stage - 1].Where(step => step <= statuses.Count).Select(step => statuses[step - 1]).ToList();
            if (own.Count > 0 && own.All(step => step.Kind == StepKind.Done || step.Kind == StepKind.Issues))
            {
                int issues = own.Where(step => step.Kind == StepKind.Issues).Sum(step => step.Value);
                return issues == 0 ? StageText.Done : TextTemplate.Fill(StageText.DoneIssues, issues);
            }
            if (own.Any(step => step.Kind == StepKind.Ready || step.Kind == StepKind.Done || step.Kind == StepKind.Issues))
            {
                return StageText.Ready;
            }
            // A step of this stage waits on a step of an earlier stage, which names that stage
            int waited = own.Where(step => step.Kind == StepKind.NeedsStep).Select(step => StageOf(step.Value)).Where(other => other < stage).DefaultIfEmpty(0).Min();
            return waited > 0 ? TextTemplate.Fill(StageText.Waits, waited) : StageText.NotStarted;
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

        // The rows of a read folder, none when it holds no data check that reads
        public static IReadOnlyList<DataCheckRow> Rows(string readFolder)
        {
            if (readFolder == null)
            {
                return new DataCheckRow[0];
            }
            try
            {
                string plots = Path.Combine(readFolder, DataCheckRows.PlotsFileName);
                return DataCheckRows.ReadResult(WorkFiles.ReadText(Path.Combine(readFolder, DataCheckRows.FileName)), File.Exists(plots) ? WorkFiles.ReadText(plots) : null).Rows;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is Newtonsoft.Json.JsonException || failure is InvalidDataException)
            {
                return new DataCheckRow[0];
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
        public const int ReadDataStep = 2;
        public const int ReportStep = 9;
        private bool settingsOpen;
        private string readFolder;
        private DateTime readStamp;

        public StagesViewModel(PaneViewModel model)
        {
            Model = model;
            Cards = new ReadOnlyCollection<StageCard>(new[]
            {
                new StageCard(1, StageText.Stage1, StageText.Stage1Help, StageText.ChooseFolder, new PaneCommand(() => ChooseFolderRequested?.Invoke(), () => model.InUse)),
                new StageCard(2, StageText.Stage2, StageText.Stage2Help, StageText.Build, new PaneCommand(() => { }, () => false)),
                new StageCard(3, StageText.Stage3, StageText.Stage3Help, StageText.Report, new PaneCommand(() => model.RequestRun(ReportStep), () => model.InUse)),
            });
            FixItemsCommand = new PaneCommand(() => FixItemsRequested?.Invoke(readFolder), () => readFolder != null);
            SettingsCommand = new PaneCommand(() => SettingsOpen = !SettingsOpen, () => true);
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

        // The Revit side opens the folder picker and the Confirm window, the pane holds no window
        public event Action ChooseFolderRequested;
        public event Action<string> FixItemsRequested;

        // The work folder of the active document, null with none
        public Func<string> WorkFolderNow { get; set; }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            bool open = Model.HasDocument;
            bool chosen = Model.InUse;
            for (int stage = 1; stage <= Cards.Count; stage++)
            {
                Cards[stage - 1].Status = StageStatus.Of(stage, Model.Statuses, open, chosen);
                Cards[stage - 1].Good = Cards[stage - 1].Status == StageText.Done;
                Cards[stage - 1].Command.Refresh();
            }
            Card2.Line = StageText.BuildNotWired;
            Card3.Line = StageText.ReportLine;
            RefreshRead();
        }

        // The rows of the latest read, read again only when its data check changed
        private void RefreshRead()
        {
            string latest = Model.HasDocument ? StageRead.Latest(WorkFolderNow?.Invoke()) : null;
            DateTime stamp = latest == null ? DateTime.MinValue : File.GetLastWriteTimeUtc(Path.Combine(latest, DataCheckRows.FileName));
            if (latest == readFolder && stamp == readStamp)
            {
                return;
            }
            readFolder = latest;
            readStamp = stamp;
            CheckRows.Clear();
            foreach (DataCheckRow row in StageRead.Rows(latest))
            {
                CheckRows.Add(row);
            }
            Card1.Line = latest == null ? StageText.NoRead : string.Empty;
            Changed(nameof(HasRows));
            FixItemsCommand.Refresh();
        }
    }
}
