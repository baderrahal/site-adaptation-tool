using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.ManualRules;

namespace SiteAdaptation.AddIn.RulesWindow
{
    // R403, docs/design/13-workflow-changes.md Site Adaptation Rules (5c). What the window binds to. It holds no Revit type and no rule
    // text: the rows, the chips and the manual come from the block of the rules profile through ManualRuleTable of Common
    public sealed class RulesWindowViewModel : INotifyPropertyChanged
    {
        private readonly ManualRuleTable table;
        private ManualChip chip = ManualChip.All;
        private RuleRowViewModel selected;
        private string message = "";

        public RulesWindowViewModel(ManualRuleTable table, string selectedId = null)
        {
            this.table = table;
            Refresh();
            if (selectedId != null)
            {
                Selected = Rows.FirstOrDefault(row => row.Id == selectedId);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ManualRuleTable Table => table;

        public string Title => RulesWindowText.Title;

        public string ManualLine => TextTemplate.Fill(RulesWindowText.ManualLine, table.Block.Manual.Name, table.Block.Manual.Revision, table.Block.Manual.Number);

        public string PrevailsLine => TextTemplate.Fill(RulesWindowText.Prevails, table.Block.Manual.Revision, table.Block.Manual.OlderRevision);

        public IReadOnlyList<RuleRowViewModel> Rows { get; private set; }

        public IReadOnlyList<RuleChipViewModel> Chips { get; private set; }

        public ManualChip Chip => chip;

        public RuleRowViewModel Selected
        {
            get { return selected; }
            set
            {
                if (!ReferenceEquals(selected, value))
                {
                    selected = value;
                    Changed(string.Empty);
                }
            }
        }

        public bool HasSelected => selected != null;

        public bool NoneSelected => selected == null;

        public string PickText => RulesWindowText.PickARule;

        public string SideId => selected == null ? "" : selected.Id;

        public string SideText => selected == null ? "" : selected.Text;

        public string SideManual => selected == null ? "" : selected.ManualRef;

        public string SideSection => selected == null ? "" : selected.SectionPage;

        public IReadOnlyList<string> SideValues => selected == null ? new string[0] : selected.Values;

        public string SideChecked => selected == null ? "" : selected.CheckedIn;

        public string SidePastJob => selected == null ? "" : selected.PastJob;

        // The text of what the tool does is not in the block, so it reads not read yet (CHOSEN by Code, question to Bader)
        public string SideToolDoes => selected == null ? "" : RulesWindowText.NotReadYet;

        public string Message
        {
            get { return message; }
            set
            {
                message = value ?? "";
                Changed(nameof(Message));
                Changed(nameof(MessageShown));
            }
        }

        public bool MessageShown => message.Length > 0;

        public void SetChip(ManualChip next)
        {
            chip = next;
            Refresh();
        }

        // The selected rule stays selected only while the chip still holds it
        private void Refresh()
        {
            Rows = table.Rows(chip).Select(rule => new RuleRowViewModel(rule, table.PastJob(rule))).ToList();
            RuleRowViewModel keep = selected == null ? null : Rows.FirstOrDefault(row => row.Id == selected.Id);
            Chips = table.Chips.Select(item => new RuleChipViewModel(item, Label(item), table.CountOf(item), item == chip)).ToList();
            Changed(nameof(Chips));
            selected = null;
            Selected = keep;
            Changed(nameof(Rows));
        }

        private static string Label(ManualChip item)
        {
            switch (item.Kind)
            {
                case ManualChipKind.Group: return item.Group;
                case ManualChipKind.PastJobDiffers: return RulesWindowText.PastJobDiffers;
                default: return RulesWindowText.All;
            }
        }

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class RuleChipViewModel
    {
        public RuleChipViewModel(ManualChip chip, string label, int count, bool active)
        {
            Chip = chip;
            Text = label + " " + count.ToString(CultureInfo.InvariantCulture);
            Active = active;
        }

        public ManualChip Chip { get; }

        public string Text { get; }

        public bool Active { get; }
    }

    public sealed class RuleRowViewModel
    {
        public RuleRowViewModel(ManualRule rule, PastJobView pastJob)
        {
            Rule = rule;
            PastJob = pastJob.Text ?? RulesWindowText.NotReadYet;
            Differs = pastJob.State == PastJobState.Differs;
        }

        public ManualRule Rule { get; }

        public string Id => Rule.Id;

        public string Text => Rule.Text;

        public string ManualRef => Rule.ManualRef;

        public string SectionPage => Rule.SectionPage;

        public IReadOnlyList<string> Values => Rule.Values.Count == 0 ? new[] { RulesWindowText.NoValues } : Rule.Values;

        public string CheckedIn => string.Join(", ", Rule.CheckedIn);

        public string PastJob { get; }

        public bool Differs { get; }
    }
}
