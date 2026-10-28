using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SiteAdaptation.AddIn.Pane
{
    // R372. The page of the dockable pane. It holds no Revit type and no logic of its own: a Browse asks the user for a path and
    // hands it to the view model, which changes the settings in memory only
    public partial class SitePanePage : Page
    {
        private readonly PaneViewModel model;

        public SitePanePage(PaneViewModel model)
        {
            this.model = model;
            InitializeComponent();
            DataContext = model;
            model.ChooseSettingsRequested += ChooseSettings;
        }

        private void ChooseSettings()
        {
            var dialog = new OpenFileDialog { CheckFileExists = true };
            if (dialog.ShowDialog() != true)
            {
                return;
            }
            try
            {
                model.LoadSettings(SettingsFile.Read(File.ReadAllText(dialog.FileName)));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException)
            {
                MessageBox.Show(error.Message, PaneText.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is FileRowViewModel row) || model.Settings == null)
            {
                return;
            }
            if (row.Folder)
            {
                using (var folder = new System.Windows.Forms.FolderBrowserDialog { ShowNewFolderButton = false })
                {
                    if (folder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        model.PickInput(row, new[] { folder.SelectedPath });
                    }
                }
                return;
            }
            var dialog = new OpenFileDialog { CheckFileExists = true, Multiselect = row.Keys.Count > 1 };
            if (dialog.ShowDialog() != true)
            {
                return;
            }
            if (row == model.SettingsRow)
            {
                model.PickConfig(dialog.FileName);
            }
            else if (row == model.RulesRow)
            {
                model.PickRules(dialog.FileName);
            }
            else
            {
                model.PickInput(row, dialog.FileNames.Take(row.Keys.Count).ToList());
            }
        }
    }
}
