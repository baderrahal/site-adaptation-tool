using System;
using System.IO;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ManualRules;

namespace SiteAdaptation.AddIn.RulesWindow
{
    // R403. What the buttons of the window do, apart from the window, so a test drives them with no click. exists and open are the
    // file test and the shell of Windows in Revit and stubs in the tests. A shell that throws leaves the window open with its reason
    public sealed class RulesWindowSession
    {
        private readonly Func<string, bool> exists;
        private readonly Action<string> open;

        public RulesWindowSession(ManualRuleTable table, string selectedId, Func<string, bool> exists, Action<string> open)
        {
            this.exists = exists ?? File.Exists;
            this.open = open ?? OpenByShell;
            Model = new RulesWindowViewModel(table, selectedId);
        }

        public RulesWindowViewModel Model { get; }

        public void OpenManual()
        {
            ManualPdfLookup lookup = ManualPdf.Find(Model.Table.Block.Manual, exists);
            if (lookup.Path == null)
            {
                Model.Message = TextTemplate.Fill(RulesWindowText.PdfNotFound, string.Join(", ", lookup.Looked));
                return;
            }
            try
            {
                open(lookup.Path);
                Model.Message = "";
            }
            catch (Exception error)
            {
                Model.Message = TextTemplate.Fill(RulesWindowText.PdfNotOpened, error.Message);
            }
        }

        // No xlsx is written inside Revit (R350), so the button says it is not built until the worker writes it
        public void Export()
        {
            Model.Message = RunTexts.NotBuilt(RulesWindowText.ExportToExcel);
        }

        private static void OpenByShell(string path)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
