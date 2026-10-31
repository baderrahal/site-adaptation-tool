using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SiteAdaptation.Common.Settings
{
    public sealed class SavedStart
    {
        public SavedStart(string name, string path, string text)
        {
            Name = name;
            Path = path;
            Text = text;
        }

        // The file name without its extension, the name the window shows
        public string Name { get; }
        public string Path { get; }
        public string Text { get; }
    }

    // R434. The saved projects Start from lists after Empty project: each pre-setting of W4 in the folder Saved projects of the work
    // root, so a saved project such as the one of this job is a file a user picks and never a value of the code. A file the
    // pre-setting reader refuses is left out by its name in skipped, never shown half read
    public static class SavedStarts
    {
        public const string FolderName = "Saved projects";
        public const string Extension = ".json";

        public static string Folder(string workRoot)
        {
            return System.IO.Path.Combine(workRoot, FolderName);
        }

        public static IReadOnlyList<SavedStart> List(string folder, List<string> skipped = null)
        {
            var starts = new List<SavedStart>();
            if (folder == null || !Directory.Exists(folder))
            {
                return starts;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*" + Extension);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                skipped?.Add(FolderName);
                return starts;
            }
            foreach (string path in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    starts.Add(Read(path));
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException || error is JsonException)
                {
                    skipped?.Add(System.IO.Path.GetFileName(path));
                }
            }
            return starts;
        }

        // A file Import of a pre-setting takes, tried on a scratch copy so nothing of a project changes
        public static SavedStart Read(string path)
        {
            string text = File.ReadAllText(path);
            PreSetting.Import(text, new ProjectSettings());
            return new SavedStart(System.IO.Path.GetFileNameWithoutExtension(path), path, text);
        }
    }
}
