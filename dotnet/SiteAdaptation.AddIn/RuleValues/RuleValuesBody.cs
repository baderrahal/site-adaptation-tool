using System;
using System.IO;
using System.Windows.Interop;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.RuleValues
{
    // R541. The button Site Adaptation Rules. It returns the rules copy a Save wrote, for the settings of the model, null when nothing was saved
    public static class RuleValuesBody
    {
        // config/rule_values.json beside the settings of the project, else the one the install ships
        public static string CatalogOf(string configFile)
        {
            string beside = string.IsNullOrEmpty(configFile) ? null : Path.Combine(Path.GetDirectoryName(configFile), Common.Settings.RuleValues.CatalogName);
            if (beside != null && File.Exists(beside))
            {
                return beside;
            }
            string shipped = Path.Combine(Path.GetDirectoryName(typeof(RuleValuesBody).Assembly.Location), ShippedSet.FolderName, "config", Common.Settings.RuleValues.CatalogName);
            return File.Exists(shipped) ? shipped : null;
        }

        // workFolder names the folder of the model, null for a model never saved, which can read and not save
        public static string Show(IntPtr owner, PaneSettings settings, string workRoot, string workFolder, Action<IntPtr> manualRules, Action<string> tell)
        {
            string catalog = CatalogOf(settings.ConfigFile);
            if (catalog == null)
            {
                tell(RuleValuesText.NoCatalog);
                return null;
            }
            string rulesText = File.ReadAllText(settings.RulesFile);
            string copy = workFolder == null ? null : Common.Settings.RuleValues.CopyPath(workRoot, Path.GetFileName(workFolder), settings.RulesFile);
            var model = new RuleValuesViewModel(Common.Settings.RuleValues.Read(File.ReadAllText(catalog), rulesText), rulesText, copy,
                WorkFolder.InputRoot(settings.ConfigFile, WorkFiles.ReadText), new DialogPicker());
            var window = new RuleValuesWindow(model, manualRules);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            if (copy == null)
            {
                tell(RuleValuesText.NoFolder);
            }
            window.ShowDialog();
            return model.Saved;
        }
    }
}
