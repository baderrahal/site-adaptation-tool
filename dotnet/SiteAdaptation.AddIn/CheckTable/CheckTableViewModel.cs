using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.CheckTable;

namespace SiteAdaptation.AddIn.CheckTable
{
    // R376, docs/design/12-ui-spec.md 7.4. What the window binds to. It holds no Revit type and no rule, the state of Common is shown as
    // it is
    public sealed class CheckTableViewModel : INotifyPropertyChanged
    {
        private readonly CheckTableView view;
        private readonly CheckTableState state;
        private readonly Func<string, string> ruleLine;
        private readonly CheckTableCallbacks callbacks;
        private string chip = CheckTableState.AllChip;
        private string find = "";
        private CheckRowViewModel selected;
        private string message = "";

        public CheckTableViewModel(CheckTableView view, CheckTableState state, Func<string, string> ruleLine, CheckTableCallbacks callbacks)
        {
            this.view = view;
            this.state = state;
            this.ruleLine = ruleLine ?? (id => id);
            this.callbacks = callbacks;
            Rows = new List<CheckRowViewModel>();
            Chips = new List<CheckChipViewModel>();
            Refresh();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title => view.Title;

        public bool ProfileShown => view.Info != null;

        public string ProfileLine => view.Info == null ? "" : TextTemplate.Fill(CheckTableText.ProfileLine, view.Info.LevelProfile, view.Info.OverrideFile,
            TextTemplate.Count(view.Info.Overrides), TextTemplate.Count(view.Info.OnNbh), view.Nbh);

        public string FollowLine => view.FollowLine;

        public string NoPastJobLine => view.NoPastJobLine ?? "";

        public bool NoPastJobShown => view.NoPastJobLine != null;

        public bool SaveAllowed => view.SaveAllowed && callbacks.Save != null;

        public bool ZoomAllowed => callbacks.Zoom != null;

        public bool ExportAllowed => callbacks.Export != null;

        public IReadOnlyList<CheckRowViewModel> Rows { get; private set; }

        public IReadOnlyList<CheckChipViewModel> Chips { get; private set; }

        public string Find
        {
            get { return find; }
            set
            {
                find = value ?? "";
                Refresh();
            }
        }

        public CheckRowViewModel Selected
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

        public string PickText => CheckTableText.PickARow;

        public string SelectedLine => selected == null ? "" : TextTemplate.Fill(CheckTableText.SelectedPlot, selected.Plot, selected.Unit, selected.Check);

        public string SideTool => selected == null ? "" : selected.ToolText;

        public string SidePast => selected == null ? "" : selected.PastText;

        public string SideWhy => selected == null ? "" : CheckTableState.Text(selected.Row.Why);

        public bool SideWhyShown => SideWhy.Length > 0;

        // A row with no manual rule shows no id and no button to open it
        public bool ManualRuleShown => selected != null && selected.Row.ManualRuleId != null;

        public string ManualRuleLine => ManualRuleShown ? ruleLine(selected.Row.ManualRuleId) : "";

        public string GiveRule => Give(CheckUse.Rule);

        public string GivePast => Give(CheckUse.Past);

        public string GiveSkip => Give(CheckUse.Skip);

        public bool PastChoosable => selected != null && selected.Row.PastOffered;

        public string PastGreyTip => selected == null ? "" : state.PastTip(selected.Row) ?? "";

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

        public bool NotSavedShown => state.Changed;

        public string NotSavedText => CheckTableText.NotSavedYet;

        public void SetChip(string next)
        {
            chip = next;
            Refresh();
        }

        // The side box sets the same Use as the dropdown of the row
        public void Choose(string use)
        {
            if (selected != null)
            {
                selected.Use = use;
            }
        }

        public void Done(string text)
        {
            Message = text;
            Changed(nameof(NotSavedShown));
        }

        private string Give(string use)
        {
            return selected == null ? "" : CheckTableState.Gives(selected.Row, use, callbacks.Gives);
        }

        private void Refresh()
        {
            Rows = state.Filter(chip, find).Select(row => new CheckRowViewModel(row, state, ruleLine, UseChanged)).ToList();
            CheckRowViewModel keep = selected == null ? null : Rows.FirstOrDefault(row => row.Row.Index == selected.Row.Index);
            Chips = view.Counts.Select(item => new CheckChipViewModel(item.Key, item.Value, item.Key == chip)).ToList();
            selected = null;
            Selected = keep;
            Changed(nameof(Rows));
            Changed(nameof(Chips));
        }

        private void UseChanged()
        {
            Changed(nameof(NotSavedShown));
            Changed(nameof(GiveRule));
        }

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class CheckChipViewModel
    {
        public CheckChipViewModel(string chip, int count, bool active)
        {
            Chip = chip;
            Text = chip + " " + count.ToString(CultureInfo.InvariantCulture);
            Active = active;
            IsAssumed = chip == CheckTableState.AssumedChip;
        }

        public string Chip { get; }

        public string Text { get; }

        public bool Active { get; }

        // The chip Assumed has a dashed amber border
        public bool IsAssumed { get; }
    }

    // One entry of the Use dropdown. Past is grey where the row offers none, with its reason as the tooltip
    public sealed class UseOptionViewModel
    {
        public UseOptionViewModel(string name, bool enabled, string tip)
        {
            Name = name;
            Enabled = enabled;
            Tip = tip;
        }

        public string Name { get; }

        public bool Enabled { get; }

        public string Tip { get; }
    }

    public sealed class CheckRowViewModel : INotifyPropertyChanged
    {
        private readonly CheckTableState state;
        private readonly Action changed;

        public CheckRowViewModel(CheckRow row, CheckTableState state, Func<string, string> ruleLine, Action changed)
        {
            Row = row;
            this.state = state;
            this.changed = changed;
            ManualRuleTip = row.ManualRuleId == null ? "" : ruleLine(row.ManualRuleId);
            UseOptions = new[]
            {
                new UseOptionViewModel(CheckUse.Rule, true, null),
                new UseOptionViewModel(CheckUse.Past, row.PastOffered, state.PastTip(row)),
                new UseOptionViewModel(CheckUse.Skip, true, null),
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public CheckRow Row { get; }

        public string Plot => Row.Plot;

        public string Nbh => Row.Nbh;

        public string Unit => Row.Unit ?? "";

        public string Check => Row.Check;

        public string ToolText => Value(CheckTableState.Text(Row.ToolValue));

        // The value Past would write, else the reason there is none
        public string PastText => Value(Row.PastValue != null ? CheckShown.Past(Row.PastValue) : CheckTableState.Text(Row.PastOffered || Row.PastWhy == null ? null : (JToken)Row.PastWhy));

        public string RuleText => CheckShown.RuleName(CheckTableState.Text(Row.Rule));

        public string ManualRule => Row.ManualRuleId ?? "";

        public string ManualRuleTip { get; }

        // The rule id of the Rule column, the plain name is the cell. The manual rule has its own tooltip, ManualRuleTip
        public string RuleTip => CheckTableState.Text(Row.Rule);

        public IReadOnlyList<UseOptionViewModel> UseOptions { get; }

        public string Use
        {
            get { return state.UseOf(Row.Index); }
            set
            {
                if (value == null || value == Use)
                {
                    return;
                }
                state.Set(Row.Index, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Use)));
                changed();
            }
        }

        private static string Value(string text)
        {
            return text.Length == 0 ? CheckTableText.None : text;
        }
    }
}
