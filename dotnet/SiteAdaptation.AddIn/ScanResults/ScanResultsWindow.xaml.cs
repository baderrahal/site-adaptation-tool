using System.Windows;

namespace SiteAdaptation.AddIn.ScanResults
{
    // Lane C SCAN-UI. The window What the tool read. It holds no logic: each button hands its click to the session, which a test drives
    // with no click
    public partial class ScanResultsWindow : Window
    {
        private readonly ScanResultsSession session;

        public ScanResultsWindow(ScanResultsSession session)
        {
            this.session = session;
            InitializeComponent();
            DataContext = session.Model;
        }

        private void OnShowAll(object sender, RoutedEventArgs e)
        {
            session.ShowAll();
        }

        private void OnUseProposed(object sender, RoutedEventArgs e)
        {
            session.UseProposedForAll();
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            session.Export();
        }

        private void OnUse(object sender, RoutedEventArgs e)
        {
            session.UseThesePicks();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            session.Cancel();
        }
    }
}
