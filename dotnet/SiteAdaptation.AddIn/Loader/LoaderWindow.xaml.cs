using System;
using System.Windows;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.AddIn.Loader
{
    // R444. The window of the loader, on the thread of RunLoader. It holds no logic, RunLoader decides when it stands
    public partial class LoaderWindow : Window
    {
        public LoaderWindow(string step)
        {
            InitializeComponent();
            StepName.Text = step;
            Tick(TimeSpan.Zero);
        }

        // Hide on the run outside Revit only, the loader then closes for good
        public event Action HideRequested;

        public void Tick(TimeSpan elapsed)
        {
            Clock.Text = TextTemplate.Fill(LoaderText.Elapsed, LoaderRule.Clock(elapsed));
        }

        public void Outside()
        {
            Line.Text = LoaderText.Outside;
            HideButton.Visibility = Visibility.Visible;
        }

        public bool IsOutside => HideButton.Visibility == Visibility.Visible;

        private void OnHide(object sender, RoutedEventArgs e)
        {
            HideRequested?.Invoke();
        }
    }
}
