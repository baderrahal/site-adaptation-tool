using System.Windows;

namespace SiteAdaptation.AddIn.HowItWorks
{
    // Lane C NEWPROJ-UI. The window of How it works. It holds no logic: each button hands its click to the view model
    public partial class HowItWorksWindow : Window
    {
        private readonly HowItWorksViewModel model;

        public HowItWorksWindow(HowItWorksViewModel model)
        {
            this.model = model;
            InitializeComponent();
            DataContext = model;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            model.Close();
        }

        private void OnOpenNew(object sender, RoutedEventArgs e)
        {
            model.OpenNew();
        }
    }
}
