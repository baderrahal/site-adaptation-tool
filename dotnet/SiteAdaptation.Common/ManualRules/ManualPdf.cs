using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common.ManualRules
{
    // R403. Where the manual PDF is. Path is null when it is in none of the folders of the profile, Looked then says where it was looked
    public sealed class ManualPdfLookup
    {
        public string Path { get; set; }
        public IReadOnlyList<string> Looked { get; set; }
    }

    public static class ManualPdf
    {
        // exists is File.Exists in Revit and a stub in the tests, so no real path is read
        public static ManualPdfLookup Find(ManualInfo manual, Func<string, bool> exists)
        {
            var looked = new List<string>();
            foreach (string folder in manual.Folders)
            {
                string candidate = System.IO.Path.Combine(folder, manual.File);
                looked.Add(candidate);
                if (exists(candidate))
                {
                    return new ManualPdfLookup { Path = candidate, Looked = looked };
                }
            }
            return new ManualPdfLookup { Path = null, Looked = looked };
        }
    }
}
