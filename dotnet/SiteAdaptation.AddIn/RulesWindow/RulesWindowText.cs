namespace SiteAdaptation.AddIn.RulesWindow
{
    // R403, docs/design/13-workflow-changes.md Site Adaptation Rules (5c). Every text the window shows apart from the data of the block
    // manualRules, which the profile holds. The text test holds each against docs/design/14-ui-labels.md word for word, a value
    // written <n> as the list writes it and filled by TextTemplate
    public static class RulesWindowText
    {
        public const string Title = "Site Adaptation Rules";
        public const string ManualLine = "<n>, <n>, document <n>";
        public const string Prevails = "<n> prevails over <n>";
        public const string OpenManual = "Open the manual PDF";
        public const string ExportToExcel = "Export to Excel";

        public const string All = "All";
        public const string PastJobDiffers = "Past job differs";

        public const string Id = "ID";
        public const string Rule = "Rule";
        public const string Manual = "Manual";
        public const string CheckedIn = "Checked in";
        public const string PastJob = "Past job";

        public const string SelectedRule = "Selected rule";
        public const string FullText = "Full text";
        public const string SectionAndPage = "Section and page";
        public const string Values = "Values";
        public const string ToolDoes = "What the tool does";

        public const string NotReadYet = "not read yet";
        public const string NoValues = "none";
        public const string PickARule = "Pick a rule from the table.";
        public const string PdfNotFound = "The manual PDF was not found. Looked for <n>";
        public const string PdfNotOpened = "The manual PDF did not open: <n>";
    }
}
