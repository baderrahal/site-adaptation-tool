using System.Windows;
using System.Windows.Input;

namespace SiteAdaptation.AddIn.Update
{
    // R402. The window of Update from report. It holds no logic: each button hands its click to the session, which a test drives with no click
    public partial class UpdateFromReportWindow : Window
    {
        private readonly UpdateFromReportSession session;

        public UpdateFromReportWindow(UpdateFromReportSession session)
        {
            this.session = session;
            InitializeComponent();
            DataContext = session.Model;
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            session.Browse();
        }

        private void OnDryRun(object sender, RoutedEventArgs e)
        {
            session.DryRun();
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            session.Apply();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            session.Cancel();
        }

        private void OnChip(object sender, MouseButtonEventArgs e)
        {
            ((ChipViewModel)((FrameworkElement)sender).DataContext).Choose();
        }
    }
}
