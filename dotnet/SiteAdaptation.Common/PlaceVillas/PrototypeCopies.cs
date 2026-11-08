using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R810, F33. The Revit copies of the prototype files in a folder beside the given one. Run 2 of 01A linked 13 villa files of Revit
    // 2021, so step 7 took 28 minutes as each upgraded on load and the reopen of the model 27.5 minutes with 13 upgrade boxes. A
    // folder beside the given one counts when its name holds the year of the running Revit, CHOSEN by Lead in FAST-13, and a file of
    // the same name in it counts as the copy of a code only when the R496 look reads that year, so a copy that is still old is no copy
    public sealed class PrototypeCopies
    {
        // Revit keeps a backup of a saved file as name.0001.rvt, which is no prototype
        private static readonly Regex Backup = new Regex(@"\.\d{4}\.rvt$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private PrototypeCopies(string folder, string copiesFolder, int running, IReadOnlyList<PrototypeCode> codes)
        {
            Folder = folder;
            CopiesFolder = copiesFolder;
            Running = running;
            Codes = codes;
        }

        // The folder of the Prototypes row
        public string Folder { get; }

        // The folder beside it the copies come from, null when none counts
        public string CopiesFolder { get; }

        public int Running { get; }

        // One per prototype file of the given folder, in name order
        public IReadOnlyList<PrototypeCode> Codes { get; }

        public int CopyCount => Codes.Count(code => code.Copy != null);

        // R811. The file step 7 links for a prototype file name: the copy when the code has one, else the file of the given folder as before
        public string FileFor(string name)
        {
            PrototypeCode code = Codes.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            return code?.Copy ?? Path.Combine(Folder, name);
        }

        public static PrototypeCopies Find(string folder, int running)
        {
            return Find(folder, running, RevitFormat.Of);
        }

        // yearOf is the reader of one file, a test gives its own. Null for a folder that is not there or holds no prototype file. A file
        // already of the running year takes no copy. Among the folders beside the given one the folder with the most copies wins, by
        // name on a tie, so the choice does not hang on the order the disk lists them
        public static PrototypeCopies Find(string folder, int running, Func<string, int?> yearOf)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    return null;
                }
                string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                List<string> names = Directory.EnumerateFiles(full, "*.rvt", SearchOption.TopDirectoryOnly).Where(file => !Backup.IsMatch(file))
                    .Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
                if (names.Count == 0)
                {
                    return null;
                }
                var years = names.ToDictionary(name => name, name => yearOf(Path.Combine(full, name)), StringComparer.OrdinalIgnoreCase);
                List<string> old = names.Where(name => years[name] != running).ToList();
                string parent = Path.GetDirectoryName(full);
                string year = running.ToString(System.Globalization.CultureInfo.InvariantCulture);
                IEnumerable<string> beside = parent == null || old.Count == 0 ? Enumerable.Empty<string>()
                    : Directory.EnumerateDirectories(parent).Where(other => Path.GetFileName(other).Contains(year)
                        && !string.Equals(other.TrimEnd(Path.DirectorySeparatorChar), full, StringComparison.OrdinalIgnoreCase));
                string best = null;
                var bestCopies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string other in beside.OrderBy(name => name, StringComparer.Ordinal))
                {
                    var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string name in old)
                    {
                        string copy = Path.Combine(other, name);
                        if (File.Exists(copy) && yearOf(copy) == running)
                        {
                            copies[name] = copy;
                        }
                    }
                    if (copies.Count > bestCopies.Count)
                    {
                        best = other;
                        bestCopies = copies;
                    }
                }
                var codes = names.Select(name => bestCopies.TryGetValue(name, out string copy)
                    ? new PrototypeCode(name, running, copy) : new PrototypeCode(name, years[name], null)).ToList();
                return new PrototypeCopies(full, best, running, codes);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return null;
            }
        }
    }

    // R810. One prototype file of the given folder: the year found, the copy's when it has one, null when the year did not read
    public sealed class PrototypeCode
    {
        public PrototypeCode(string name, int? year, string copy)
        {
            Name = name;
            Year = year;
            Copy = copy;
        }

        public string Name { get; }

        public string Code => Path.GetFileNameWithoutExtension(Name);

        public int? Year { get; }

        public string Copy { get; }
    }
}
