using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common
{
    public static class KeyParameters
    {
        public const string FileName = "key-parameters.txt";

        public static readonly string[] Defaults =
        {
            "Width", "Height", "Depth", "Length", "Area", "Mark", "Type Mark", "Description",
        };

        // One parameter name per line. Blank lines and lines starting with # are skipped
        public static IList<string> Load(string folder)
        {
            string path = Path.Combine(folder ?? "", FileName);
            if (!File.Exists(path))
            {
                return Defaults;
            }
            var names = File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                .ToList();
            return names.Count == 0 ? Defaults : names;
        }
    }
}
