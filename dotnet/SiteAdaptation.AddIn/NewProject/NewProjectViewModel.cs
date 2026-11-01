using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.NewProject
{
    // One row of "Files the tool needs", its words from NewProjectText by the id of its SetupRow
    public sealed class SetupRowViewModel : Bindable
    {
        private string fileName = NewProjectText.NotChosen;
        private string toolTip;
        private SetupState state;

        public SetupRowViewModel(SetupRow row)
        {
            Row = row;
            string[] words = NewProjectText.Rows[row.Id];
            File = words[0];
            Who = words[1];
            UsedBy = words[2];
            Help = words[3];
            StopLine = words[4];
            TodoLine = words[5];
        }

        public SetupRow Row { get; }
        public string File { get; }
        public string Who { get; }
        public string UsedBy { get; }
        public string Help { get; private set; }
        public string StopLine { get; }
        public string TodoLine { get; }
        public bool CanBrowse { get { return !Row.Later; } }
        public bool HasTemplate { get { return Row.Template != null; } }
        public string FileName { get { return fileName; } private set { Set(ref fileName, value); } }
        public string ToolTip { get { return toolTip; } private set { Set(ref toolTip, value); } }

        public SetupState State
        {
            get { return state; }
            private set
            {
                if (Set(ref state, value))
                {
                    Changed(nameof(StatusText));
                    Changed(nameof(IsMissing));
                    Changed(nameof(IsFound));
                }
            }
        }

        public bool IsMissing { get { return State == SetupState.Missing; } }
        public bool IsFound { get { return State == SetupState.Found; } }

        public string StatusText
        {
            get { return State == SetupState.Found ? NewProjectText.Found : State == SetupState.Missing ? NewProjectText.Missing : NewProjectText.Later; }
        }

        internal void Show(SetupRowState shown)
        {
            State = shown.State;
            string[] chosen = shown.Paths.Where(path => !string.IsNullOrEmpty(path)).ToArray();
            FileName = chosen.Length == 0 ? NewProjectText.NotChosen : string.Join(", ", chosen.Select(path => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))));
            ToolTip = chosen.Length == 0 ? null : string.Join(Environment.NewLine, chosen);
        }

        internal void LayersFound(int? layers)
        {
            if (layers != null && Row.Id == SetupRows.Site)
            {
                Help = TextTemplate.Fill(NewProjectText.LayersFound, layers.Value) + " " + Help;
            }
        }
    }

    // A choice of Start from: Empty project, which keeps the settings as they are, or a saved project of R434
    public sealed class StartChoice
    {
        public StartChoice(string name, SavedStart start)
        {
            Name = name;
            Start = start;
        }

        public string Name { get; }
        public SavedStart Start { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    // Lane C NEWPROJ-UI, the New project window with no Revit type. Every pick stays in the window until Save setup, which lays them over the
    // settings of the pane by SetupSave (R432) and hands the result back: the pane host writes them into the model at once (R445), or with
    // no model to write to the pane keeps them and the next real run writes them (R367). Check files is SetupCheck (R431), read only. Get template writes TemplateBook (R433) where the user names,
    // never inside the input folder of config. Nothing here writes the model or a settings file
    public sealed class NewProjectViewModel : Bindable
    {
        private readonly ProjectSettings current;
        private readonly Func<string, bool, bool> there;
        private readonly IPathPicker picker;
        private readonly Func<string, string> readText;
        private readonly Dictionary<string, string> picks = new Dictionary<string, string>(StringComparer.Ordinal);
        private StartChoice start;
        private string ready = string.Empty;
        private bool stopShown;
        private string stopLine = string.Empty;
        private string todoLine = string.Empty;
        private bool checkedOk;
        private string templateLine = string.Empty;

        // R440, Q283. The scan the facts came from, the window What the tool read it opens, and the picks of the dropdowns and the window
        private readonly DrawingFactsResult facts;
        private readonly Func<string, ProjectSettings, ProjectSettings> viewScan;
        private readonly Dictionary<string, string> scanPicks = new Dictionary<string, string>(StringComparer.Ordinal);
        private IDictionary<string, string> conflictPicks;
        private int layerIndex;
        private int sheetIndex;
        private int columnIndex;

        public NewProjectViewModel(ProjectSettings current, string projectName, IReadOnlyList<SavedStart> starts, DrawingFactsResult facts, Func<string, bool, bool> there,
            IPathPicker picker, Func<string, string> readText, Func<string, ProjectSettings, ProjectSettings> viewScan = null, bool modelOpen = true)
        {
            NoModelShown = !modelOpen;
            this.viewScan = viewScan;
            this.current = current ?? new ProjectSettings();
            this.there = there;
            this.picker = picker;
            this.readText = readText;
            ProjectName = projectName ?? string.Empty;
            Starts = new[] { new StartChoice(NewProjectText.EmptyProject, null) }
                .Concat((starts ?? new SavedStart[0]).Select(saved => new StartChoice(TextTemplate.Fill(NewProjectText.SavedName, saved.Name), saved))).ToList();
            SavedHint = starts == null || starts.Count == 0 ? string.Empty : TextTemplate.Fill(NewProjectText.SavedHint, starts[0].Name);
            Rows = SetupRows.All.Select(row => new SetupRowViewModel(row)).ToList();
            facts = facts ?? new DrawingFactsResult();
            this.facts = facts;
            ScanFactsData scan = facts.Scan;
            LayerChoices = scan == null ? new List<string>() : scan.LayerChoices.Select(source => source.Shown).ToList();
            // Breaker of SCAN-UI: a source no dropdown holds leaves it empty, never its first line as if it were the source
            layerIndex = scan == null ? -1 : scan.LayerChoices.FindIndex(source => source.Raw == (Pick(ScanView.PlotOutlineKind) ?? scan.LayerProposed));
            SheetChoices = scan == null ? new List<string>() : scan.Sheets.Select(sheet => sheet.Shown).ToList();
            string villa = Pick(ScanView.VillaTypeKind) ?? scan?.VillaTypeProposed;
            sheetIndex = scan == null ? -1 : scan.Sheets.FindIndex(sheet => sheet.Columns.Any(column => column.Raw == villa));
            columnIndex = sheetIndex < 0 ? -1 : scan.Sheets[sheetIndex].Columns.FindIndex(column => column.Raw == villa);
            SurveyChoices = new[] { facts.SurveyPoint ?? (scan == null ? NewProjectText.FromScan : NewProjectText.NoSurveyPoint) };
            Rows.Single(row => row.Row.Id == SetupRows.Site).LayersFound(facts.Layers);
            SurveyPoint = facts.SurveyPoint ?? NewProjectText.FromScan;
            PlotsFound = facts.Plots == null ? NewProjectText.NotRead : TextTemplate.Fill(NewProjectText.PlotsOnLayer, TextTemplate.Count(facts.Plots.Value), facts.PlotLayer ?? NewProjectText.FromScan);
            VillaTypes = facts.VillaTypes == null ? NewProjectText.NotRead : TextTemplate.Fill(NewProjectText.Types, TextTemplate.Count(facts.VillaTypes.Value));
            start = Starts[0];
            Refresh();
        }

        public string ProjectName { get; }
        public IReadOnlyList<StartChoice> Starts { get; }
        public string SavedHint { get; }
        public IReadOnlyList<SetupRowViewModel> Rows { get; }
        public string SurveyPoint { get; }
        public string PlotsFound { get; }
        public string VillaTypes { get; }

        // Change waits for a later round, View opens What the tool read on the scan the facts came from
        public bool CanChange { get { return false; } }

        public bool CanView => facts.ScanPath != null && viewScan != null;

        public IReadOnlyList<string> SurveyChoices { get; }

        // The scan reads no survey point, so its dropdown holds one line and stays grey (Q283)
        public bool HasSurvey => facts.SurveyPoint != null;

        public IReadOnlyList<string> LayerChoices { get; }

        public bool HasLayers => LayerChoices.Count > 0;

        public int LayerIndex
        {
            get { return layerIndex; }
            set
            {
                if (value >= 0 && value < LayerChoices.Count && Set(ref layerIndex, value))
                {
                    scanPicks[ScanView.PlotOutlineKind] = facts.Scan.LayerChoices[value].Raw;
                }
            }
        }

        public IReadOnlyList<string> SheetChoices { get; }

        public bool HasSheets => SheetChoices.Count > 0;

        public int SheetIndex
        {
            get { return sheetIndex; }
            set
            {
                if (value >= 0 && value < SheetChoices.Count && Set(ref sheetIndex, value))
                {
                    columnIndex = 0;
                    Changed(nameof(ColumnChoices));
                    Changed(nameof(ColumnIndex));
                    scanPicks[ScanView.VillaTypeKind] = facts.Scan.Sheets[value].Columns[0].Raw;
                }
            }
        }

        public IReadOnlyList<string> ColumnChoices => sheetIndex < 0 ? new List<string>() : facts.Scan.Sheets[sheetIndex].Columns.Select(column => column.Column).ToList();

        public int ColumnIndex
        {
            get { return columnIndex; }
            set
            {
                if (sheetIndex >= 0 && value >= 0 && value < facts.Scan.Sheets[sheetIndex].Columns.Count && Set(ref columnIndex, value))
                {
                    scanPicks[ScanView.VillaTypeKind] = facts.Scan.Sheets[sheetIndex].Columns[value].Raw;
                }
            }
        }

        // View: What the tool read on the scan with the picks so far, its picks kept for Save setup, Cancel there keeps those before
        public void ViewScan()
        {
            if (!CanView)
            {
                return;
            }
            ProjectSettings picked = viewScan(facts.ScanPath, Next());
            if (picked == null)
            {
                return;
            }
            foreach (KeyValuePair<string, string> pick in picked.ScanPicks ?? new SortedDictionary<string, string>())
            {
                scanPicks[pick.Key] = pick.Value;
            }
            conflictPicks = picked.ConflictPicks;
        }

        private string Pick(string kind)
        {
            return current.ScanPicks != null && current.ScanPicks.TryGetValue(kind, out string raw) ? raw : null;
        }

        public string Ready { get { return ready; } private set { Set(ref ready, value); } }
        public bool StopShown { get { return stopShown; } private set { Set(ref stopShown, value); } }
        public string StopLine { get { return stopLine; } private set { Set(ref stopLine, value); } }
        public string TodoLine { get { return todoLine; } private set { Set(ref todoLine, value); } }
        public bool CheckedOk { get { return checkedOk; } private set { Set(ref checkedOk, value); } }
        public string TemplateLine { get { return templateLine; } private set { Set(ref templateLine, value); } }

        // R445. True when Save setup has no model to write to, so the foot says the next button run writes the setup
        public bool NoModelShown { get; }

        // The settings Save setup gave, null after Cancel
        public ProjectSettings Result { get; private set; }

        public event Action Finished;

        public StartChoice Start
        {
            get { return start; }
            set
            {
                if (value != null && Set(ref start, value))
                {
                    CheckedOk = false;
                    Refresh();
                }
            }
        }

        public void Browse(SetupRowViewModel row)
        {
            if (!row.CanBrowse || picker == null)
            {
                return;
            }
            string path = row.Row.Folder ? picker.Folder() : picker.File(row.File);
            if (path == null)
            {
                return;
            }
            picks[row.Row.Inputs[0]] = path;
            CheckedOk = false;
            Refresh();
        }

        // R431. Read only, the statuses and the stop box of the settings Save setup would give
        public void CheckFiles()
        {
            CheckedOk = Refresh().Ok;
        }

        public void GetTemplate(SetupRowViewModel row)
        {
            if (!row.HasTemplate || picker == null)
            {
                return;
            }
            ProjectSettings next = Next();
            try
            {
                string config = string.IsNullOrEmpty(next.ConfigFile) ? null : readText(next.ConfigFile);
                IReadOnlyList<TemplateSheet> sheets = TemplateBook.SheetsFor(row.Row.Template, config == null ? null : JObject.Parse(config));
                string path = picker.Save(NewProjectText.GetTemplate);
                if (path == null)
                {
                    return;
                }
                if (!path.EndsWith(TemplateBook.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    path += TemplateBook.Extension;
                }
                // Review of NEWPROJ-UI: with no config there is no input folder to hold the path against, so nothing is written (R1)
                string inputRoot = WorkFolder.InputRoot(next.ConfigFile, readText);
                if (string.IsNullOrEmpty(inputRoot))
                {
                    TemplateLine = TextTemplate.Fill(NewProjectText.TemplateNot, NewProjectText.NoInputRoot);
                    return;
                }
                if (WorkFolder.Inside(Path.GetDirectoryName(Path.GetFullPath(path)), inputRoot))
                {
                    TemplateLine = TextTemplate.Fill(NewProjectText.TemplateNot, NewProjectText.InsideInputs);
                    return;
                }
                // Review of NEWPROJ-UI: the dialog asked about the name before the extension was added, so a file there is never replaced
                if (File.Exists(path))
                {
                    TemplateLine = TextTemplate.Fill(NewProjectText.TemplateNot, NewProjectText.AlreadyThere);
                    return;
                }
                TemplateBook.Write(path, sheets);
                TemplateLine = TextTemplate.Fill(NewProjectText.TemplateSaved, Path.GetFileName(path));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException || error is JsonException || error is ArgumentException)
            {
                TemplateLine = TextTemplate.Fill(NewProjectText.TemplateNot, error.Message);
            }
        }

        public void Save()
        {
            Result = Next();
            Finished?.Invoke();
        }

        public void Cancel()
        {
            Result = null;
            Finished?.Invoke();
        }

        private SetupDraft Draft()
        {
            var draft = new SetupDraft { StartText = start?.Start?.Text, ScanPicks = new Dictionary<string, string>(scanPicks), ConflictPicks = conflictPicks };
            foreach (KeyValuePair<string, string> pick in picks)
            {
                draft.Picks[pick.Key] = pick.Value;
            }
            return draft;
        }

        private ProjectSettings Next()
        {
            return SetupSave.Apply(Draft(), current);
        }

        private SetupCheckResult Refresh()
        {
            SetupCheckResult result = SetupCheck.Run(Next(), there);
            foreach (SetupRowViewModel row in Rows)
            {
                row.Show(result.Rows.Single(item => item.Row.Id == row.Row.Id));
            }
            Ready = TextTemplate.Fill(NewProjectText.Ready, result.Ready, result.Total);
            SetupRowViewModel stop = result.Stops.Select(item => Rows.Single(row => row.Row.Id == item.Row.Id)).FirstOrDefault();
            StopShown = !result.HasStart || stop != null;
            StopLine = !result.HasStart ? (result.StartNamed ? NewProjectText.StartGone : NewProjectText.NoStart) : stop?.StopLine ?? string.Empty;
            TodoLine = !result.HasStart ? NewProjectText.NoStartTodo : stop?.TodoLine ?? string.Empty;
            return result;
        }
    }
}
