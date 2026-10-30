using System.Windows;
using System.Windows.Controls;

namespace SiteAdaptation.AddIn.RulesWindow
{
    // R403. The window Site Adaptation Rules. It holds no logic: each button hands its click to the session, which a test drives with no click
    public partial class RulesWindow : Window
    {
        private readonly RulesWindowSession session;

        public RulesWindow(RulesWindowSession session)
        {
            this.session = session;
            InitializeComponent();
            DataContext = session.Model;
        }

        private void OnOpenManual(object sender, RoutedEventArgs e)
        {
            session.OpenManual();
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            session.Export();
        }

        private void OnChip(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is RuleChipViewModel chip)
            {
                session.Model.SetChip(chip.Chip);
            }
        }
    }
}
