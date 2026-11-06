using System.Windows;

namespace SiteAdaptation.AddIn.Pane
{
    // R687. The 2D preview of a plan. Go on lets the build go on, Cancel or the X stops it with nothing written
    public partial class SiteBuildPreviewWindow : Window
    {
        public SiteBuildPreviewWindow(SiteBuildResult plan)
        {
            InitializeComponent();
            LineText.Text = SiteBuildPreview.Line(plan);
            Plots.ItemsSource = SiteBuildPreview.Outlines(plan.Preview);
        }

        private void OnGoOn(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
