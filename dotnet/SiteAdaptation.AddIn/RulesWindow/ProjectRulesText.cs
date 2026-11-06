namespace SiteAdaptation.AddIn.RulesWindow
{
    // R682, R683. Every word of the rules table of a project and of its line on the pane, in docs/design/14-ui-labels.md under Site Adaptation Rules
    public static class ProjectRulesText
    {
        public const string Title = "Rules of this project";
        public const string Intro = "Each rule this project builds by. Confirm a value as it stands, or type another one and save it.";
        public const string Rule = "Rule";
        public const string Value = "Value";
        public const string Source = "Source";
        public const string Status = "Status";
        public const string Confirm = "Confirm";
        public const string Save = "Save";
        public const string Close = "Close";
        public const string BeforeBuild = "Before Build";
        public const string Warn = "Warn and build";
        public const string Block = "Stop the build";
        public const string BeforeBuildHelp = "What a Build does while a rule is not confirmed.";
        public const string UseManual = "Use the manual template";
        public const string NoRules = "No rules chosen for this project yet.";

        public const string AmberNone = "No rules chosen for this project yet. Open Rules in Settings.";
        public const string AmberSome = "<n> of <n> rules are not confirmed. Open Rules in Settings.";
        public const string OpenRules = "Rules";
        public const string Open = "Open";

        public const string OwnCopyOnly = "This project uses the shipped settings, so its rules cannot be set here. Start a new project in Settings, Project setup, then choose its rules.";
        public const string NoTemplate = "The rules templates are not installed with the add-in. Install the add-in again.";
        public const string NotSaved = "Save the model first, the rules table keeps its record beside the work of the model.";
        public const string NotRead = "The value of <name> is not a list or a set the tool can read: <name>";
        public const string Refused = "Nothing changed: <name>";
        public const string Saved = "Saved <name>.";
        public const string Chosen = "The rules of the manual template are now the rules of this project. Each is not confirmed until a person confirms it.";
    }
}
