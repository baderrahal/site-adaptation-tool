using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
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
        private bool surfacesEnabled;
        private string surfacesTip;
        private string surfacesLine = string.Empty;

        public SetupRowViewModel(SetupRow row)
        {
            Row = row;
            string[] words = NewProjectText.Rows[row.Id];
            File = words[0];
            HelpLine = SiteAdaptation.AddIn.Help.HelpFile.Book.Find(row.Id)?.Line;
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

        // R540. Its one line on hover, Row.Id is the key of the help file
        public string HelpLine { get; }
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

        // R492. The button Pick surfaces and the line under the row Road levels, set by the view model on every refresh
        public bool HasSurfaces { get { return Row.Id == SetupRows.RoadLevels; } }
        public bool SurfacesEnabled { get { return surfacesEnabled; } private set { Set(ref surfacesEnabled, value); } }
        public string SurfacesTip { get { return surfacesTip; } private set { Set(ref surfacesTip, value); } }
        public string SurfacesLine { get { return surfacesLine; } private set { Set(ref surfacesLine, value); } }

        internal void ShowSurfaces(bool enabled, string tip, string line)
        {
            SurfacesEnabled = enabled;
            SurfacesTip = tip;
            SurfacesLine = line;
        }

        internal void LayersFound(int? layers)
        {
            if (layers != null && Row.Id == SetupRows.Site)
            {
                Help = TextTemplate.Fill(NewProjectText.LayersFound, layers.Value) + " " + Help;
            }
        }
    }

    // A choice of Start from: Empty project, which keeps the settings as they are, or a saved project of R434. R520: the window opens on the one the setup came from
    // R542. One field of the report file name, Value is what the window shows, its edit goes back to the window through edited
    public sealed class ReportFieldRow : Bindable
    {
        private readonly Action<ReportFieldRow> edited;
        private string value = string.Empty;

        public ReportFieldRow(string key, string label, Action<ReportFieldRow> edited)
        {
            Key = key;
            Label = label;
            this.edited = edited;
        }

        public string Key { get; }
        public string Label { get; }

        public string Value
        {
            get { return value; }
            set
            {
                if (Set(ref this.value, value ?? string.Empty))
                {
                    edited(this);
                }
            }
        }

        // From the settings, no edit
        public void Show(string text)
        {
            if (value != (text ?? string.Empty))
            {
                value = text ?? string.Empty;
                Changed(nameof(Value));
            }
        }
    }

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

        // R797. The reader of the files as they are, for the shipped set itself, readText reads the shipped config as a new project will hold it
        private readonly Func<string, string> readFiles;
        private readonly Dictionary<string, string> picks = new Dictionary<string, string>(StringComparer.Ordinal);
        private StartChoice start;

        // R520. The choice of the saved project the setup came from, null for none. The settings hold it already, so Save setup does not import it over the picks
        private StartChoice cameFrom;
        private readonly string savedHint;
        private string ready = string.Empty;
        private bool stopShown;
        private string stopLine = string.Empty;
        private string todoLine = string.Empty;
        private bool checkedOk;
        private string templateLine = string.Empty;
        private string modeLine = string.Empty;

        // R486. The shipped set of the install, null where the window has none, and what Save took from it
        private readonly ShippedSet shipped;
        private ProjectSet taken;
        private ProjectSet preview;
        private bool previewRead;

        // R440, Q283. The scan the facts came from, the window What the tool read it opens, and the picks of the dropdowns and the window
        private readonly DrawingFactsResult facts;
        private readonly Func<string, ProjectSettings, ProjectSettings> viewScan;
        private readonly Dictionary<string, string> scanPicks = new Dictionary<string, string>(StringComparer.Ordinal);
        private IDictionary<string, string> conflictPicks;
        private int layerIndex;
        private int sheetIndex;
        private int columnIndex;

        // R492. The window Road surfaces behind the button Pick surfaces, and the order it gave, null until a pick
        private readonly SurfacePicker pickSurfaces;
        private List<string> surfaces;

        // R542. The report name fields as the settings give them, null when config holds no block, and the edits of the window over them
        private readonly Dictionary<string, string> reportEdits = new Dictionary<string, string>(StringComparer.Ordinal);
        private SortedDictionary<string, string> reportBase;
        private bool reportShown;
        private string reportLine = string.Empty;

        public NewProjectViewModel(ProjectSettings current, string projectName, IReadOnlyList<SavedStart> starts, DrawingFactsResult facts, Func<string, bool, bool> there,
            IPathPicker picker, Func<string, string> readText, Func<string, ProjectSettings, ProjectSettings> viewScan = null, bool modelOpen = true, SurfacePicker pickSurfaces = null, ShippedSet shipped = null)
        {
            this.shipped = shipped;
            this.pickSurfaces = pickSurfaces;
            NoModelShown = !modelOpen;
            this.viewScan = viewScan;
            this.current = current ?? new ProjectSettings();
            this.there = there;
            this.picker = picker;
            readFiles = readText;
            this.readText = shipped == null ? readText : shipped.Seen(readText);
            ProjectName = projectName ?? string.Empty;
            Starts = new[] { new StartChoice(NewProjectText.EmptyProject, null) }
                .Concat((starts ?? new SavedStart[0]).Select(saved => new StartChoice(TextTemplate.Fill(NewProjectText.SavedName, saved.Name), saved))).ToList();
            savedHint = starts == null || starts.Count == 0 ? string.Empty : TextTemplate.Fill(NewProjectText.SavedHint, starts[0].Name);
            SavedStart origin = SavedStarts.CameFrom(this.current, starts);
            cameFrom = origin == null ? null : Starts.First(choice => choice.Start == origin);
            Rows = SetupRows.All.Select(row => new SetupRowViewModel(row)).ToList();
            ReportRows = ReportFile.NameFields.Concat(new[] { ReportFile.Revision, ReportFile.Status, ReportFile.ProjectName })
                .Select((key, index) => new ReportFieldRow(key, NewProjectText.ReportLabels[index], ReportEdited)).ToList();
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
            start = cameFrom ?? Starts[0];
            Refresh();
        }

        public string ProjectName { get; }
        public IReadOnlyList<StartChoice> Starts { get; }

        // The hint speaks for Empty project, a saved project chosen needs it no more (R520)
        public string SavedHint => start?.Start != null ? string.Empty : savedHint;

        public IReadOnlyList<SetupRowViewModel> Rows { get; }
        public IReadOnlyList<ReportFieldRow> ReportRows { get; }

        // R542. Hidden when config holds no block, the report then names its file by the neighbourhood and reads no field
        public bool ReportShown { get { return reportShown; } private set { Set(ref reportShown, value); } }

        public string ReportLine { get { return reportLine; } private set { Set(ref reportLine, value); } }

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

        // R484. The one plain line under Start from, the rules the settings of Save setup would build under. Refreshed with every pick, empty where no mode is known
        public string ModeLine { get { return modeLine; } private set { Set(ref modeLine, value); } }

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
                    // R520. Only the start the window opened on is not imported again, a start a person picks is, also the same one picked back
                    cameFrom = null;
                    // R492. A pick made for the project before would hide the list of the chosen one
                    surfaces = null;
                    CheckedOk = false;
                    Changed(nameof(SavedHint));
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

        // R492. Opens the window Road surfaces on the folder of the row and the effective order, a Use keeps the ticked order for Save setup,
        // a Cancel keeps what was there
        public void PickSurfaces()
        {
            RoadPickState state = RoadPickState.Of(Next(), readText);
            if (!state.Enabled || pickSurfaces == null)
            {
                return;
            }
            IReadOnlyList<string> picked = pickSurfaces(state.InFolder(), state.Effective);
            if (picked == null)
            {
                return;
            }
            surfaces = picked.ToList();
            CheckedOk = false;
            Refresh();
        }

        // R431. Read only, the statuses and the stop box of the settings Save setup would give
        public void CheckFiles()
        {
            SetupCheckResult result = Refresh();
            CheckedOk = (result.HasStart || Takes) && !result.Stops.Any() && !Unread;
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

        // R486. Empty project of settings that name no config, with a set shipped
        private bool Wants => start?.Start == null && string.IsNullOrEmpty(current.ConfigFile) && shipped != null && shipped.There;

        private bool Takes => Wants && shipped.Refusal == ShippedSetRefusal.None;

        private string RefusedLine => !Wants ? null : shipped.Refusal == ShippedSetRefusal.NotSaved ? NewProjectText.SetNotSaved : shipped.Refusal == ShippedSetRefusal.NoSafeName ? NewProjectText.SetNoName : null;

        // R486. The set is there and its files do not read, so the window does not read as all found
        private bool Unread => Takes && taken == null && Previewed() == null;

        // R486. Before Save the window reads the shipped set as the Save will give it, so the road pick and the sheet row work, nothing is copied. Read once
        private ProjectSet Previewed()
        {
            if (!Takes)
            {
                return null;
            }
            if (!previewRead)
            {
                preview = shipped.Preview(readFiles);
                previewRead = true;
            }
            return preview;
        }

        public void Save()
        {
            // R486. The copy is made here where the work root is known, SetupSave lays the result over the settings. A refusal leaves the window open and nothing written
            if (RefusedLine != null)
            {
                return;
            }
            if (Takes)
            {
                try
                {
                    taken = shipped.Take(readFiles);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException || error is JsonException || error is ArgumentException || error is NotSupportedException || error is InvalidOperationException)
                {
                    // The reason stays out of the window, the add-in has no log to put it in
                    StopShown = true;
                    StopLine = NewProjectText.SetNotCopied;
                    TodoLine = NewProjectText.SetNotCopiedTodo;
                    return;
                }
            }
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
            var draft = new SetupDraft { ReportFields = reportEdits, StartText = start == cameFrom ? null : start?.Start?.Text, ScanPicks = new Dictionary<string, string>(scanPicks), ConflictPicks = conflictPicks, RoadSurfaces = surfaces, NewSet = taken ?? Previewed() };
            foreach (KeyValuePair<string, string> pick in picks)
            {
                draft.Picks[pick.Key] = pick.Value;
            }
            return draft;
        }

        private ProjectSettings Next()
        {
            return SetupSave.Apply(Draft(), current, readText);
        }

        // What the Save would give, by rev6 against the other, the name of the past job lives in config alone. No mode known and no set to take shows no line, so it
        // never promises a build the project can not do (R486)
        private void ShowMode()
        {
            ProjectSettings next = Next();
            string mode = next.ProjectMode ?? ProjectModes.OfConfig(ProjectModes.ReadConfig(next.ConfigFile, readText)) ?? (Takes ? shipped.NewMode(readFiles) : null);
            ModeLine = mode == null ? string.Empty : mode == Common.DesignBasis.Rev6 ? NewProjectText.ModeRev6 : NewProjectText.ModePast;
        }

        // An edit back to the value of the settings is no edit, so the project keeps following config
        private void ReportEdited(ReportFieldRow row)
        {
            string held = null;
            reportBase?.TryGetValue(row.Key, out held);
            if (row.Value == (held ?? string.Empty))
            {
                reportEdits.Remove(row.Key);
            }
            else
            {
                reportEdits[row.Key] = row.Value;
            }
            ShowReportLine();
        }

        private void ShowReport(ProjectSettings next)
        {
            string file = !string.IsNullOrEmpty(next.ConfigFile) ? next.ConfigFile : Previewed()?.ConfigFile;
            reportBase = ReportFile.Laid(ProjectModes.ReadConfig(file, readText), next.ReportFields);
            ReportShown = reportBase != null;
            foreach (ReportFieldRow row in ReportRows.Where(item => !reportEdits.ContainsKey(item.Key)))
            {
                string held = null;
                reportBase?.TryGetValue(row.Key, out held);
                row.Show(held);
            }
            ShowReportLine();
        }

        // What the Save gives: an edit over the settings, so the line reads the name 9 Report will write
        private void ShowReportLine()
        {
            if (reportBase == null)
            {
                ReportLine = string.Empty;
                return;
            }
            var fields = new SortedDictionary<string, string>(reportBase, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> edit in reportEdits.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
            {
                fields[edit.Key] = edit.Value;
            }
            ReportLine = ReportFile.Refused(fields) ? NewProjectText.ReportNameRefused : NewProjectText.ReportNameLine + ReportFile.Shown(fields);
        }

        private void ShowSurfaces()
        {
            SetupRowViewModel row = Rows.Single(item => item.Row.Id == SetupRows.RoadLevels);
            RoadPickState state = RoadPickState.Of(Next(), readText);
            string tip = state.Why == RoadPickWhy.PastJob ? RoadSurfacesText.PastJob : state.Why == RoadPickWhy.NoSetup ? RoadSurfacesText.NoSetup : null;
            string line = !state.Enabled ? string.Empty
                : state.Effective.Count == 0 ? RoadSurfacesText.NonePicked
                : TextTemplate.Fill(RoadSurfacesText.InOrder, state.Effective.Count, string.Join(", ", state.Effective.Select(RoadPickState.Short)));
            row.ShowSurfaces(state.Enabled, tip, line);
        }

        private SetupCheckResult Refresh()
        {
            SetupCheckResult result = SetupCheck.Run(Next(), there, readText);
            foreach (SetupRowViewModel row in Rows)
            {
                row.Show(result.Rows.Single(item => item.Row.Id == row.Row.Id));
            }
            ShowSurfaces();
            ShowMode();
            ShowReport(Next());
            Ready = TextTemplate.Fill(NewProjectText.Ready, result.Ready, result.Total);
            SetupRowViewModel stop = result.Stops.Select(item => Rows.Single(row => row.Row.Id == item.Row.Id)).FirstOrDefault();
            bool noStart = !result.HasStart && !Takes;
            bool unread = Unread;
            StopShown = noStart || stop != null || unread;
            StopLine = RefusedLine ?? (unread ? NewProjectText.SetNotRead : noStart ? (result.StartNamed ? NewProjectText.StartGone : NewProjectText.NoStart) : stop?.StopLine ?? string.Empty);
            TodoLine = RefusedLine != null ? string.Empty : unread ? NewProjectText.SetNotReadTodo : noStart ? NewProjectText.NoStartTodo : stop?.TodoLine ?? string.Empty;
            return result;
        }
    }
}
