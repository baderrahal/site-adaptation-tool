namespace SiteAdaptation.AddIn.RuleValues
{
    // R541. Every text of the window, the text test holds each against docs/design/14-ui-labels.md
    public static class RuleValuesText
    {
        public const string Title = "Site Adaptation Rules";
        public const string Line = "Change a value here, the rule itself stays as it is. A changed value shows amber, and 4 Check goes back to Ready after Save.";
        public const string ColumnId = "Rule";
        public const string ColumnName = "Name";
        public const string ColumnManual = "Manual value";
        public const string ColumnProject = "Project value";
        public const string ColumnUnit = "Unit";
        public const string ColumnPage = "Manual page";
        public const string Save = "Save";
        public const string Reset = "Reset to manual";
        public const string Export = "Export";
        public const string Import = "Import";
        public const string ManualRules = "Manual rules";
        public const string Close = "Close";
        public const string Saved = "Saved for this project.";
        public const string NothingChanged = "No value changed, nothing was saved.";
        public const string ReadOnly = "This model is read only, so its rules can not be saved.";
        public const string NoFolder = "Save the model first, the rules of a project are kept by its file name.";
        public const string SaveFailed = "The rules were not saved: <name>";
        public const string ImportFailed = "Not imported: <name>";
        public const string InInputs = "it would lie in the input folder, which the tool never writes";
        public const string Exported = "Exported: <name>";
        public const string NoCatalog = "The list of rule values is not there. Reinstall the add-in.";
    }
}
