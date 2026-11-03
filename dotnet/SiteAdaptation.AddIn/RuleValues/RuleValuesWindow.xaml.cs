using System;
using System.Windows;
using System.Windows.Interop;

namespace SiteAdaptation.AddIn.RuleValues
{
    // R541. The table of the button Site Adaptation Rules, the manual rules one press away
    public partial class RuleValuesWindow : Window
    {
        private readonly RuleValuesViewModel model;
        private readonly Action<IntPtr> manualRules;

        public RuleValuesWindow(RuleValuesViewModel model, Action<IntPtr> manualRules)
        {
            InitializeComponent();
            this.model = model;
            this.manualRules = manualRules;
            DataContext = model;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            model.Save();
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            model.Reset();
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            model.Export();
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            model.Import();
        }

        private void OnManualRules(object sender, RoutedEventArgs e)
        {
            manualRules?.Invoke(new WindowInteropHelper(this).Handle);
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
