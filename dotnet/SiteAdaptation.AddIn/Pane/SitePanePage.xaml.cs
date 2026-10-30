using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SiteAdaptation.AddIn.Pane
{
    // R372. The page of the dockable pane. It holds no Revit type and no logic of its own: a Browse hands its row to the view model,
    // which asks the dialogs below for a path and changes the settings in memory only
    public partial class SitePanePage : Page
    {
        private readonly PaneViewModel model;

        public SitePanePage(PaneViewModel model)
        {
            this.model = model;
            InitializeComponent();
            DataContext = model;
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is FileRowViewModel row)
            {
                model.Browse(row);
            }
        }
    }

    // The dialogs of Windows behind Browse. net48 WPF has no folder dialog, the one of Windows Forms picks a folder
    public sealed class DialogPicker : IPathPicker
    {
        public string File(string title)
        {
            var dialog = new OpenFileDialog { CheckFileExists = true };
            if (title != null)
            {
                dialog.Title = title;
            }
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string Save(string title)
        {
            var dialog = new SaveFileDialog { Title = title, OverwritePrompt = true };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string Folder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog { ShowNewFolderButton = false })
            {
                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }
    }
}
