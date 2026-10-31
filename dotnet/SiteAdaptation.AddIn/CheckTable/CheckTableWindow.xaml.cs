using System.Windows;
using System.Windows.Controls;
using SiteAdaptation.Common.CheckTable;

namespace SiteAdaptation.AddIn.CheckTable
{
    // R376. The window of button 4. It holds no logic: each button hands its click to the session, which a test drives with no click
    public partial class CheckTableWindow : Window
    {
        private readonly CheckTableSession session;

        public CheckTableWindow(CheckTableSession session)
        {
            this.session = session;
            InitializeComponent();
            DataContext = session.Model;
        }

        private void OnZoom(object sender, RoutedEventArgs e)
        {
            session.Zoom();
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            session.Export();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            session.Save();
        }

        private void OnOpenRule(object sender, RoutedEventArgs e)
        {
            session.OpenRule();
        }

        private void OnChooseRule(object sender, RoutedEventArgs e)
        {
            session.Model.Choose(CheckUse.Rule);
        }

        private void OnChoosePast(object sender, RoutedEventArgs e)
        {
            session.Model.Choose(CheckUse.Past);
        }

        private void OnChooseSkip(object sender, RoutedEventArgs e)
        {
            session.Model.Choose(CheckUse.Skip);
        }

        private void OnChip(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is CheckChipViewModel chip)
            {
                session.Model.SetChip(chip.Chip);
            }
        }
    }
}
