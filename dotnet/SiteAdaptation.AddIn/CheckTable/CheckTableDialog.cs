using System;
using System.Windows.Interop;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.ManualRules;

namespace SiteAdaptation.AddIn.CheckTable
{
    // R376. The window of button 4 in Revit: modal like Review data, its owner the Revit main window, so the worker callback that
    // opens it gets the Use of each row when it returns (7.4 of the mock asks for a modeless window, the seam of ICheckTableDialog
    // returns the answer, so it stays modal, CHOSEN). rulesFile is the rules profile of the pane, which holds the manual rules
    public sealed class CheckTableDialog : ICheckTableDialog
    {
        private readonly IntPtr owner;
        private readonly string rulesFile;

        public CheckTableDialog(IntPtr owner, string rulesFile)
        {
            this.owner = owner;
            this.rulesFile = rulesFile;
        }

        public CheckTableAnswer Show(CheckTableView view, CheckTableCallbacks callbacks)
        {
            var session = new CheckTableSession(view, callbacks, RuleLine(rulesFile), id => RulesWindowBody.Show(owner, rulesFile, id));
            var window = new CheckTableWindow(session);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            window.ShowDialog();
            return session.Answer();
        }

        // The id and text of a manual rule, the id alone when the profile holds no block or the file is unreadable
        public static Func<string, string> RuleLine(string rulesFile)
        {
            ManualRuleBlock block = null;
            try
            {
                block = string.IsNullOrEmpty(rulesFile) ? null : ManualRuleBlock.FromFile(rulesFile);
            }
            catch (Exception error) when (error is System.IO.IOException || error is System.IO.InvalidDataException || error is Newtonsoft.Json.JsonException)
            {
                block = null;
            }
            return id => block == null ? id : block.Line(id);
        }
    }
}
