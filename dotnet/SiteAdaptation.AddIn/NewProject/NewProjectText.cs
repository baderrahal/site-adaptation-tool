using System.Collections.Generic;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.NewProject
{
    // Lane C NEWPROJ-UI, docs/design/boards/NewProject.dc.html word for word, the texts the board gives no words for marked CHOSEN in
    // docs/design/14-ui-labels.md. Every text the window shows, in the files of the window so the text test of R372 finds it there
    public static class NewProjectText
    {
        public const string Title = "New project";
        public const string Badge = "Start here";
        public const string Line = "Tell the tool where your files are. It reads them, shows what it found, and saves your settings. No file editing.";
        public const string StartHint = "Using another project's settings? Pick them under Start from.";
        public const string Ready = "<n> of <n>";
        public const string ReadyLine = "files ready";

        public const string ProjectName = "Project name";
        public const string StartFrom = "Start from";
        public const string EmptyProject = "Empty project";
        public const string SavedName = "<name> (saved)";
        public const string SavedHint = "<name> is kept as a saved start, not built in.";

        public const string FilesTitle = "Files the tool needs";
        public const string FilesHelp = "Each file comes from a team. Send them the template if it is missing.";
        public const string ColumnFile = "File";
        public const string ColumnWho = "Who gives it";
        public const string ColumnUsed = "Used by";
        public const string ColumnPick = "Pick and check";
        public const string ColumnStatus = "Status";

        public const string Browse = "Browse";
        public const string GetTemplate = "Get template";
        public const string Found = "Found";
        public const string Missing = "Missing";
        public const string Later = "Comes later";
        public const string NotChosen = "Not chosen";
        public const string LayersFound = "Layers found: <n>.";

        public const string ReadTitle = "What the tool read from the drawing";
        public const string ReadHelp = "Coordinates are read from the CAD. You only confirm them.";
        public const string SurveyPoint = "Survey point";
        public const string PlotsFound = "Plots found";
        public const string VillaTypes = "Villa types";
        public const string Change = "Change";
        public const string View = "View";
        public const string PlotsOnLayer = "<n> plots on layer <name>";
        public const string Types = "<n> types";
        public const string NotRead = "Not read yet. Run 2 Read data.";
        public const string FromScan = "Comes with the drawing scan";
        public const string NoSurveyPoint = "The scan reads no survey point yet";
        public const string SheetColumn = "Villa type sheet and column";

        public const string StopTitle = "The tool stops here";
        public const string NoStart = "No saved project is chosen, so no button has its rules.";
        public const string StartGone = "The saved project names settings this computer does not hold, so no button has its rules.";
        public const string NoStartTodo = "What to do: pick a saved project under Start from.";
        public const string AllFound = "All the files a button needs are found.";
        public const string TemplateSaved = "Template saved: <name>";
        public const string TemplateNot = "The template was not written: <name>";
        public const string InsideInputs = "it would lie in the input folder, which the tool never writes";
        public const string AlreadyThere = "a file of that name is there, pick another name";
        public const string NoInputRoot = "pick a saved project first, it names the input folder the tool never writes";

        public const string Foot = "Save keeps your settings in the project folder. The next person who opens it sees the same setup.";
        public const string Cancel = "Cancel";
        public const string CheckFiles = "Check files";
        public const string SaveSetup = "Save setup";
        public const string NoModel = "No model is open to write to, so the setup is saved with the next button run.";
        public const string NotWritten = "Save setup did not reach the model: <name>. The pane keeps the setup and the next button run writes it.";

        // The words of each row of SetupRows by its id: file, who gives it, used by, the help line, and when it stops a button what is
        // missing and what to do
        public static readonly IReadOnlyDictionary<string, string[]> Rows = new Dictionary<string, string[]>
        {
            [SetupRows.Site] = new[]
            {
                "Site drawing (CAD)", "Civil team", "Buttons 1 to 4", "Pick plots, roads, curbs below.",
                "The site drawing is missing, so buttons 1 to 4 cannot read the plots.", "What to do: ask the civil team for the drawing, then press Browse.",
            },
            [SetupRows.PlotList] = new[]
            {
                "Plot list (Excel)", "Sales team", "Button 3", "Pick the sheet and the columns for plot, code and cluster.",
                "The plot list is missing, so button 3 cannot check the plots.", "What to do: ask the sales team for the list, or press Get template and fill it.",
            },
            [SetupRows.VillaLevels] = new[]
            {
                "Villa levels (Excel)", "Architect", "Buttons 4 and 5", "Floor level and door level for each villa type.", "", "",
            },
            [SetupRows.RoadLevels] = new[]
            {
                "Road levels (civil file)", "Civil team", "Button 4", "Optional. Without it the tool uses road level plus slope.", "", "",
            },
            [SetupRows.ParameterSheet] = new[]
            {
                "Parameter sheet (Excel)", "BIM team", "Button 1", "The shared parameters the tool adds to the model.",
                "The parameter sheet is missing, so button 1 cannot add the shared parameters.", "What to do: ask the BIM team for the sheet, or press Get template and fill it.",
            },
        };
    }
}
