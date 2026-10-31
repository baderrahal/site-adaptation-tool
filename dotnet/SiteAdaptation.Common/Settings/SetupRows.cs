using System.Collections.Generic;

namespace SiteAdaptation.Common.Settings
{
    // R430, docs/design/boards/NewProject.dc.html. One row of "Files the tool needs" of the New project window: the inputs of the
    // settings JSON it fills, the buttons that use it, whether a button runs without it and the input whose template Get template
    // writes. The words of each row are the window's, NewProjectText of the add-in
    public sealed class SetupRow
    {
        public SetupRow(string id, string[] inputs, int[] buttons, bool optional, bool folder, string template)
        {
            Id = id;
            Inputs = inputs;
            Buttons = buttons;
            Optional = optional;
            Folder = folder;
            Template = template;
        }

        public string Id { get; }

        // The names of InputNames the row fills, none for a row whose input does not exist yet
        public IReadOnlyList<string> Inputs { get; }

        public IReadOnlyList<int> Buttons { get; }
        public bool Optional { get; }
        public bool Folder { get; }

        // The input name TemplateBook writes a template for, null when the row has no Get template
        public string Template { get; }

        // A row of the board whose input waits on a decision of Lane A, shown and never saved (steps/waiting-on-others.md)
        public bool Later
        {
            get { return Inputs.Count == 0; }
        }
    }

    // R430. The five rows of the board in its order. The board's site drawing is the baseplan and its plot list the workbook of the
    // sales role, which holds the sales and the DLUS setback sheets (addin.workbookRoles of config). The villa blocks, curbs, plot
    // numbers, the DLUS workbook and the prototypes keep their rows in the pane (CHOSEN by Code, Q281). Villa levels has no input of
    // the settings JSON, its door level source waits on Lane A
    public static class SetupRows
    {
        public const string Site = "site";
        public const string PlotList = "plotList";
        public const string VillaLevels = "villaLevels";
        public const string RoadLevels = "roadLevels";
        public const string ParameterSheet = "parameterSheet";

        public static readonly IReadOnlyList<SetupRow> All = new[]
        {
            new SetupRow(Site, new[] { InputNames.Baseplan }, new[] { 1, 2, 3, 4 }, false, false, null),
            new SetupRow(PlotList, new[] { InputNames.Sales }, new[] { 3 }, false, false, InputNames.Sales),
            new SetupRow(VillaLevels, new string[0], new[] { 4, 5 }, false, false, null),
            new SetupRow(RoadLevels, new[] { InputNames.RoadSurface }, new[] { 4 }, true, true, null),
            new SetupRow(ParameterSheet, new[] { InputNames.ParameterSheet }, new[] { 1 }, false, false, InputNames.ParameterSheet),
        };
    }
}
