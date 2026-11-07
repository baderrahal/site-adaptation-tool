using System;
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

        // R540. The i of a step row
        private void OnStepHelp(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement source && source.DataContext is StepRowViewModel row)
            {
                Help.HelpBox.Show(row.HelpKey, source);
            }
        }

        private void OnPickSurfaces(object sender, RoutedEventArgs e)
        {
            model.PickSurfaces();
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

        // R763, F9 of run 1. The folder a folder dialog opens at: the folder picked before in this Revit session, or the project folder
        // Choose project folder sets before it opens the dialog
        public static string Start { get; set; }

        // R763. The folder dialog of Windows with a path field, opened at Start. The old tree of Windows Forms only when the shell refuses it
        public string Folder()
        {
            string picked;
            try
            {
                picked = Fast7.FolderDialog.Pick(Start, System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);
            }
            catch (Exception failure) when (failure is System.Runtime.InteropServices.COMException || failure is InvalidCastException)
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog { ShowNewFolderButton = false, SelectedPath = Start ?? string.Empty })
                {
                    picked = dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
                }
            }
            if (!string.IsNullOrEmpty(picked))
            {
                Start = picked;
            }
            return picked;
        }
    }
}
