using System.Windows;

namespace SiteAdaptation.AddIn.RulesWindow
{
    // R682. The rules table of a project. It holds no logic, each button hands its click to the view model, which a test drives with no click
    public partial class ProjectRulesWindow : Window
    {
        public ProjectRulesWindow(ProjectRulesViewModel model)
        {
            InitializeComponent();
            DataContext = model;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
