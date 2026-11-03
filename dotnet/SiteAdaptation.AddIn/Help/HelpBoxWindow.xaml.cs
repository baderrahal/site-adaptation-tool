using System.Windows;
using SiteAdaptation.Common.Help;

namespace SiteAdaptation.AddIn.Help
{
    // R540. The side box of an i, every word from the help file but the headings of HelpText
    public partial class HelpBoxWindow : Window
    {
        public HelpBoxWindow(HelpEntry entry)
        {
            InitializeComponent();
            DataContext = entry;
            NoLimits.Visibility = entry.Limits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
