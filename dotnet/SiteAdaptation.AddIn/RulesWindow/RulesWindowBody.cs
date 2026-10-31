using System;
using System.IO;
using System.Windows.Interop;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.ManualRules;

namespace SiteAdaptation.AddIn.RulesWindow
{
    // R403. The window Site Adaptation Rules for the ribbon button of command SiteAdaptationRulesCommand. It changes no model element,
    // so it needs no transaction, no saved project and no step. Like ReviewDataBody it names no Revit type: the Revit side passes the
    // handle of its main window and the rules file the pane holds
    public static class RulesWindowBody
    {
        // The line to show in place of the window, null when the file can be opened. A chosen file that is gone names its path, none chosen
        // says where to choose it, so the line never ends on an empty path
        public static string Problem(string rulesFile)
        {
            if (string.IsNullOrEmpty(rulesFile))
            {
                return RulesWindowText.NoRulesFile;
            }
            return File.Exists(rulesFile) ? null : TextTemplate.Fill(RulesWindowText.RulesFileMissing, rulesFile);
        }

        // Opens the window modal on the block manualRules of the rules file. selectedId picks a rule at once, the "Open in Site Adaptation
        // Rules" of 4 Check and Review data. facts gives the past job of a rule from the override list or the build, null for none.
        // Throws InvalidDataException when the file holds no usable block, the caller says it
        public static void Show(IntPtr mainWindow, string rulesFile, string selectedId = null, Func<string, PastJobFact> facts = null)
        {
            if (string.IsNullOrEmpty(rulesFile) || !File.Exists(rulesFile))
            {
                throw new FileNotFoundException("the rules file was not found", rulesFile);
            }
            var table = new ManualRuleTable(ManualRuleBlock.FromFile(rulesFile), facts);
            var window = new RulesWindow(new RulesWindowSession(table, selectedId, null, null));
            if (mainWindow != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = mainWindow;
            }
            window.ShowDialog();
        }
    }
}
