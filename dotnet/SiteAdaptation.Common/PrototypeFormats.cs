using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SiteAdaptation.Common
{
    // R496. The oldest Revit format in a Prototypes folder, from at most Limit files read by RevitFormat. A folder that holds files of
    // more than one format gives the oldest, since one old file is enough to make a remake upgrade it
    public static class PrototypeFormats
    {
        // The most files one look opens. Each read is a few small seeks, so a slow disk still answers in seconds
        public const int Limit = 20;

        // Revit keeps a backup of a saved file as name.0001.rvt, which is no prototype
        private static readonly Regex Backup = new Regex(@"\.\d{4}\.rvt$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static int? Oldest(string folder, int limit = Limit)
        {
            return Oldest(folder, limit, RevitFormat.Of);
        }

        // read is the reader of one file, a test counts its calls. A file whose read gives null counts for nothing
        public static int? Oldest(string folder, int limit, Func<string, int?> read)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    return null;
                }
                string[] files = Directory.EnumerateFiles(folder, "*.rvt", SearchOption.TopDirectoryOnly)
                    .Where(file => !Backup.IsMatch(file)).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).Take(limit).ToArray();
                int? oldest = null;
                foreach (string file in files)
                {
                    int? year = read(file);
                    if (year != null && (oldest == null || year < oldest))
                    {
                        oldest = year;
                    }
                }
                return oldest;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return null;
            }
        }

        // The year to name in the warning, null when the oldest format is not older than the Revit the add-in runs on
        public static int? OldYear(int? oldest, int running)
        {
            return oldest != null && oldest < running ? oldest : null;
        }
    }
}
