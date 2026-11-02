using System.Windows;
using System.Windows.Input;

namespace SiteAdaptation.AddIn.NewProject
{
    // R492. The window Road surfaces. It holds no logic: each click goes to the view model, which a test drives with no click
    public partial class RoadSurfacesWindow : Window
    {
        private readonly RoadSurfacesViewModel model;

        public RoadSurfacesWindow(RoadSurfacesViewModel model)
        {
            this.model = model;
            InitializeComponent();
            DataContext = model;
        }

        private void OnRow(object sender, MouseButtonEventArgs e)
        {
            model.Select((sender as FrameworkElement)?.DataContext as RoadSurfaceRowViewModel);
        }

        private void OnUp(object sender, RoutedEventArgs e)
        {
            model.MoveUp();
        }

        private void OnDown(object sender, RoutedEventArgs e)
        {
            model.MoveDown();
        }

        private void OnUse(object sender, RoutedEventArgs e)
        {
            model.Use();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            model.Cancel();
        }
    }
}
