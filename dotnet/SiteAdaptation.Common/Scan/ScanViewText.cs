namespace SiteAdaptation.Common.Scan
{
    // R435. The texts of the window What the tool read that Common gives it, in one file for the text test of R372, which reads this file as a
    // file of the window. A value is a slot of the label list, filled by ScanView.Fill. No other text goes here
    public static class ScanViewText
    {
        public const string PlotOutline = "Plot outline";
        public const string PlotNumber = "Plot number";
        public const string VillaBlock = "Villa block";
        public const string VillaType = "Villa type";
        public const string Location = "Location";
        public const string Label = "Label";

        public const string Clear = "Clear";
        public const string Pick = "Pick";
        public const string Read = "Read";
        public const string NotRead = "Not read";
        public const string Cad = "CAD";
        public const string Excel = "Excel";
        public const string Pdf = "PDF";
        public const string Prototype = "Prototype";

        public const string OnLayer = "<name>, layer <name>";
        public const string BlocksOn = "<name>, blocks on <name>";
        public const string TextsOn = "<name>, texts on <name>";
        public const string OnSheet = "<name>, sheet <name>, column <name>";
        public const string OnPage = "<name>, page <n>";

        public const string CoversPlots = "<n> plots";
        public const string CoversOf = "<n> of <n>";
        public const string ChecksFiles = "<n> files";

        public const string FoundCad = "<n> layers, <n> closed outlines, <n> texts, <n> blocks";
        public const string FoundSources = "<n> villa type sources";

        public const string ChoiceFrom = "<name> from <name>";

        public const string NotClear = "<name> is not clear";
        public const string NoPrototypeMatch = "No source matches your prototype names on enough plots. The best covers <n> of <n>.";
        public const string NoSourceCovers = "No source covers enough plots. The best covers <n> of <n>.";
        public const string DoVillaType = "What to do: pick the source in the row Villa type, or ask the master planner which file is current.";
        public const string DoPick = "What to do: pick the source in the row <name>.";
    }
}
