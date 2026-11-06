using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.ProjectRules;

namespace SiteAdaptation.AddIn.RulesWindow
{
    // R682. One rule of the table. The button confirms the value as it stands, or saves what the person typed
    public sealed class ProjectRuleRow : Bindable
    {
        private readonly ProjectRulesViewModel owner;
        private string text;

        public ProjectRuleRow(ProjectRulesViewModel owner, RuleForBuild rule)
        {
            this.owner = owner;
            Id = rule.Id;
            Name = string.IsNullOrEmpty(rule.Name) ? rule.Id : rule.Name;
            Value = rule.Value;
            Source = rule.Source;
            Status = rule.Status;
            Marked = rule.Marked;
            Fixed = ProjectRulesFile.IsFixed(rule.Id);
            text = ProjectRulesTable.ValueText(rule.Value);
            SaveCommand = new PaneCommand(() => owner.Save(this), () => !Fixed && owner.HasRules);
        }

        public string Id { get; }
        public string Name { get; }
        public JToken Value { get; }
        public string Source { get; }
        public string Status { get; }
        public string Marked { get; }
        public bool HasMark { get { return !string.IsNullOrEmpty(Marked); } }
        public bool Fixed { get; }
        public bool Editable { get { return !Fixed && owner.HasRules; } }
        public PaneCommand SaveCommand { get; }

        public string Text
        {
            get { return text; }
            set
            {
                Set(ref text, value);
                Changed(nameof(ButtonText));
            }
        }

        // Confirm while the text is the value, Save once the person typed another
        public string ButtonText { get { return text == ProjectRulesTable.ValueText(Value) ? ProjectRulesText.Confirm : ProjectRulesText.Save; } }
    }

    // R682. The rules table of a project: its rows, the Before Build setting and the line of the last change
    public sealed class ProjectRulesViewModel : Bindable
    {
        private readonly ProjectRulesPlace place;
        private string line = string.Empty;
        private bool hasRules;
        private int beforeBuild;

        public ProjectRulesViewModel(ProjectRulesPlace place)
        {
            this.place = place;
            UseManualCommand = new PaneCommand(() => Said(ProjectRulesTable.UseManual(place), ProjectRulesText.Chosen), () => !hasRules);
            Load();
        }

        public string Title { get { return ProjectRulesText.Title; } }
        public ObservableCollection<ProjectRuleRow> Rows { get; } = new ObservableCollection<ProjectRuleRow>();
        public bool HasRules { get { return hasRules; } }
        public bool NoRules { get { return !hasRules; } }
        public PaneCommand UseManualCommand { get; }
        public string[] BeforeBuildChoices { get; } = { ProjectRulesText.Warn, ProjectRulesText.Block };
        public string Line { get { return line; } private set { Set(ref line, value); } }

        // 0 warn, 1 block. A pick runs rulesChange on the setting, which logs it as any change (R662)
        public int BeforeBuildIndex
        {
            get { return beforeBuild; }
            set
            {
                if (value == beforeBuild || value < 0 || !hasRules)
                {
                    return;
                }
                Said(ProjectRulesTable.Change(place, ProjectRulesFile.BeforeBuild, value == 1 ? ProjectRulesFile.Block : ProjectRulesFile.Warn), TextTemplate.Fill(ProjectRulesText.Saved, ProjectRulesText.BeforeBuild));
            }
        }

        public void Save(ProjectRuleRow row)
        {
            JToken value = ProjectRulesTable.Parse(row.Text, row.Value, out string why);
            if (value == null)
            {
                Line = TextTemplate.Fill(ProjectRulesText.NotRead, row.Name, why);
                return;
            }
            Said(ProjectRulesTable.Change(place, row.Id, value), TextTemplate.Fill(ProjectRulesText.Saved, row.Name));
        }

        // The file is read again after every change, so the table shows what a build would take and never what was typed
        private void Said(string refusal, string done)
        {
            Load();
            Line = refusal ?? done;
        }

        public void Load()
        {
            hasRules = ProjectRulesTable.HasRules(place);
            RulesForBuildAnswer answer = ProjectRulesTable.Read(place);
            Rows.Clear();
            if (answer != null)
            {
                foreach (RuleForBuild rule in answer.Rules)
                {
                    Rows.Add(new ProjectRuleRow(this, rule));
                }
            }
            beforeBuild = ProjectRulesTable.BeforeBuild(place) == ProjectRulesFile.Block ? 1 : 0;
            Line = ProjectRulesTable.Refusal(place) ?? string.Empty;
            Changed(nameof(HasRules));
            Changed(nameof(NoRules));
            Changed(nameof(BeforeBuildIndex));
            UseManualCommand.Refresh();
        }
    }
}
