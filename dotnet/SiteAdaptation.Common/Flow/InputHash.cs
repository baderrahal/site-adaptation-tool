using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SiteAdaptation.Common.Flow
{
    // R382, docs/design/03a-buttons-shared-and-1.md Steps. A file input is hashed by its content. A folder input, Road surface and
    // Prototypes, by the list of its files with name, size and last write time, since their content is over a gigabyte
    public static class InputHash
    {
        public static string Of(RunInput input)
        {
            return input.Text != null ? OfText(input.Text) : input.IsFolder ? OfFolder(input.Path) : FileHash.Sha256(input.Path);
        }

        private static string OfText(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        public static string OfFolder(string folder)
        {
            string root = Path.GetFullPath(folder);
            var lines = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !IsRevitWorksharingLog(path))
                .Select(path => new FileInfo(path))
                .Select(file => file.FullName.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant()
                    + "|" + file.Length.ToString(CultureInfo.InvariantCulture)
                    + "|" + file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture))
                .OrderBy(line => line, StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        // R458. Revit writes <model>_backup\<model>.slog when it opens a workshared prototype to read it, so making the masses would
        // make every later step look stale though no input changed. Any other file of that folder still counts
        private static bool IsRevitWorksharingLog(string path)
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            return string.Equals(Path.GetExtension(path), ".slog", StringComparison.OrdinalIgnoreCase)
                && folder != null
                && folder.EndsWith("_backup", StringComparison.OrdinalIgnoreCase);
        }
    }

    // A run id names files, R347: the button, then the time to the millisecond
    public static class RunIds
    {
        public static string New(int button, DateTime time)
        {
            return button.ToString(CultureInfo.InvariantCulture) + "_" + time.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        }
    }
}
