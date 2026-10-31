using System.Windows;

namespace SiteAdaptation.AddIn.NewProject
{
    // Lane C NEWPROJ-UI. The window of New project. It holds no logic: each button hands its click to the view model, which a test drives
    // with no click
    public partial class NewProjectWindow : Window
    {
        private readonly NewProjectViewModel model;

        public NewProjectWindow(NewProjectViewModel model)
        {
            this.model = model;
            InitializeComponent();
            DataContext = model;
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SetupRowViewModel row)
            {
                model.Browse(row);
            }
        }

        private void OnTemplate(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SetupRowViewModel row)
            {
                model.GetTemplate(row);
            }
        }

        private void OnCheck(object sender, RoutedEventArgs e)
        {
            model.CheckFiles();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            model.Save();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            model.Cancel();
        }
    }
}
