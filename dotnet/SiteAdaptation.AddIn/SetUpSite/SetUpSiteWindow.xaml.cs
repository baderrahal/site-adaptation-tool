using System.Windows;

namespace SiteAdaptation.AddIn.SetUpSite
{
    // R373. The window of button 1. It holds no logic: each button hands its click to the session, which a test drives with no click
    public partial class SetUpSiteWindow : Window
    {
        private readonly SetUpSiteSession session;

        public SetUpSiteWindow(SetUpSiteSession session)
        {
            this.session = session;
            InitializeComponent();
            DataContext = session.Model;
        }

        private void OnDryRun(object sender, RoutedEventArgs e)
        {
            session.DryRun();
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            session.Browse();
        }

        private void OnUseCad(object sender, RoutedEventArgs e)
        {
            session.UseCad();
        }

        private void OnPickPosition(object sender, RoutedEventArgs e)
        {
            session.PickPosition();
        }

        private void OnUseTyped(object sender, RoutedEventArgs e)
        {
            session.UseTyped();
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            session.Apply();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            session.Cancel();
        }
    }
}
