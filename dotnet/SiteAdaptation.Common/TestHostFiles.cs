using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SiteAdaptation.Common
{
    // R117. A save keeps no Revit backup of a test copy. Revit leaves <name>.0001.rvt style files next to a plain model and a
    // <name>_backup folder next to a central, and these are found here by name so the test host can delete them after its save
    public static class TestHostFiles
    {
        // A test copy is a plain .rvt full path whose name holds _TEST- once, and never the site model it came from
        public static bool IsTestCopy(string model)
        {
            if (!TestHostGuard.IsPlainFullPath(model) || !model.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string name = Path.GetFileName(model);
            int at = name.IndexOf(TestHostGuard.TestTag, StringComparison.OrdinalIgnoreCase);
            return at > 0 && at == name.LastIndexOf(TestHostGuard.TestTag, StringComparison.OrdinalIgnoreCase);
        }

        // The backup files of the copy among the file names of its folder, <name>.<4 digits>.rvt and nothing else
        public static List<string> BackupFiles(string model, IEnumerable<string> fileNames)
        {
            if (!IsTestCopy(model))
            {
                return new List<string>();
            }
            string stem = Path.GetFileNameWithoutExtension(model);
            var pattern = new Regex(@"\A" + Regex.Escape(stem) + @"\.[0-9]{4}\.rvt\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            string folder = Path.GetDirectoryName(model);
            return (fileNames ?? Enumerable.Empty<string>()).Where(name => name != null && pattern.IsMatch(name)).Select(name => Path.Combine(folder, name)).ToList();
        }

        // The backup folder of the copy, <name>_backup next to it, or null for a file that is not a test copy
        public static string BackupFolder(string model)
        {
            return IsTestCopy(model) ? Path.Combine(Path.GetDirectoryName(model), Path.GetFileNameWithoutExtension(model) + "_backup") : null;
        }

        // Deletes what a save of the test copy left behind and says what went. Only names built from the copy are ever touched
        public static string DeleteBackups(string model)
        {
            if (!IsTestCopy(model))
            {
                throw new InvalidOperationException("not a test copy, no backup of it is deleted: " + model);
            }
            string folder = Path.GetDirectoryName(model);
            List<string> files = BackupFiles(model, Directory.GetFiles(folder).Select(Path.GetFileName));
            foreach (string file in files)
            {
                File.Delete(file);
            }
            string backup = BackupFolder(model);
            bool folderGone = false;
            if (Directory.Exists(backup))
            {
                if ((File.GetAttributes(backup) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("the backup folder is a link or junction and is left alone: " + backup);
                }
                Directory.Delete(backup, true);
                folderGone = true;
            }
            return "backup files deleted " + files.Count + (folderGone ? ", backup folder deleted" : ", no backup folder");
        }
    }
}
