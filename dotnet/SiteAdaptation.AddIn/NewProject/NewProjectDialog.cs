using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Interop;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.NewProject
{
    // Lane C NEWPROJ-UI. New project in Revit: modal, its owner the Revit main window. Show returns the settings Save setup gave, null on
    // Cancel or a close of the window. It opens no transaction and writes no file but a template the user names
    public static class NewProjectDialog
    {
        // The hook of the scanner of a later Lane B round, which sets its own reader here at start (steps/waiting-on-others.md)
        public static IDrawingFacts Facts { get; set; } = new LastReadFacts();

        public static ProjectSettings Show(IntPtr owner, ProjectSettings current, string projectName, string workRoot, string workFolder, IPathPicker picker)
        {
            IReadOnlyList<SavedStart> starts = SavedStarts.List(SavedStarts.Folder(workRoot));
            DrawingFactsResult facts = Facts.Read(current?.ConfigFile, workFolder);
            var model = new NewProjectViewModel(current, projectName, starts, facts, There, picker, WorkFolder.ReadIfThere);
            var window = new NewProjectWindow(model);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            model.Finished += () => window.Close();
            window.ShowDialog();
            return model.Result;
        }

        public static bool There(string path, bool folder)
        {
            return folder ? Directory.Exists(path) : File.Exists(path);
        }
    }
}
