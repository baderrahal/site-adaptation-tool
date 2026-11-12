using System;
using System.Linq;
using SiteAdaptation.Common.CheckTable;

namespace SiteAdaptation.AddIn.CheckTable
{
    // R376. What the buttons of the window do, apart from the window, so a test drives them with no click. A callback that throws
    // leaves the window open with its reason shown
    public sealed class CheckTableSession
    {
        private readonly CheckTableView view;
        private readonly CheckTableCallbacks callbacks;
        private readonly Action<string> openRule;

        // ruleLine gives the text of a manual rule by its id for the side box, openRule opens Site Adaptation Rules on it
        public CheckTableSession(CheckTableView view, CheckTableCallbacks callbacks, Func<string, string> ruleLine, Action<string> openRule)
        {
            this.view = view;
            this.callbacks = callbacks ?? new CheckTableCallbacks();
            this.openRule = openRule;
            State = new CheckTableState(view);
            Model = new CheckTableViewModel(view, State, ruleLine, this.callbacks);
        }

        public CheckTableState State { get; }

        public CheckTableViewModel Model { get; }

        // True once Save overrides ran
        public bool Saved { get; private set; }

        public CheckTableAnswer Answer()
        {
            return new CheckTableAnswer { Uses = view.Rows.Select(row => new CheckChoice(row.Index, State.UseOf(row.Index))).ToList(), Saved = Saved };
        }

        public void Save()
        {
            if (!Model.SaveAllowed)
            {
                return;
            }
            try
            {
                callbacks.Save(State.Choices());
                Saved = true;
                State.MarkSaved();
                Model.Done(CheckTableText.OverridesSaved);
            }
            catch (Exception error)
            {
                Model.Message = error.Message;
            }
        }

        public void Zoom()
        {
            if (Model.Selected == null || callbacks.Zoom == null)
            {
                return;
            }
            Model.Message = callbacks.Zoom(Model.Selected.Row) ?? "";
        }

        public void Export()
        {
            if (callbacks.Export != null)
            {
                Model.Message = callbacks.Export() ?? "";
            }
        }

        public void OpenRule()
        {
            string id = Model.Selected?.Row.ManualRuleId;
            if (id == null || openRule == null)
            {
                return;
            }
            try
            {
                openRule(id);
            }
            catch (Exception error)
            {
                Model.Message = error.Message;
            }
        }
    }
}
