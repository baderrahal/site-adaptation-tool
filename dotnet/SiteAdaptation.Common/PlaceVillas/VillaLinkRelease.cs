using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R848, Bader 13 Nov 2026. A Build reads the drawings and makes the plan outside Revit, and on a PC of 16 GB the villa links Revit holds
    // loaded can leave the worker short of free memory. So before the first step of a Build, when the free memory is under the largest whole
    // command of the worker, the links of the villas the tool placed are unloaded, and Place villas loads back the ones its scope needs with
    // no box. A link the tool did not place is never touched
    public static class VillaLinkRelease
    {
        public sealed class Result
        {
            // The file of each link type unloaded
            public List<string> Unloaded { get; } = new List<string>();

            // The file name and why, for each link type that did not unload
            public List<string> Failed { get; } = new List<string>();
        }

        // True when both numbers are known and the free memory is under the need, so a PC that cannot tell its free memory unloads nothing
        public static bool Needed(double? freeMiB, double? needMiB)
        {
            return freeMiB.HasValue && needMiB.HasValue && freeMiB.Value < needMiB.Value;
        }

        // Outside any transaction, as RevitLinkType.Unload may not run while one is open
        public static Result Unload(IVillaDocument document, ILinkFileReader reader)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VillaElement villa in document.Villas(null).Where(item => !item.IsSolid && item.Tag != null))
            {
                string file = Full(reader.LinkFileOf(villa.UniqueId));
                if (file != null)
                {
                    files.Add(file);
                }
            }
            var result = new Result();
            foreach (VillaLinkType type in document.LinkTypes().Where(item => item.Loaded && Full(item.FullPath) != null && files.Contains(Full(item.FullPath))))
            {
                string failure = document.UnloadType(type.Key);
                if (failure == null)
                {
                    result.Unloaded.Add(type.FullPath);
                }
                else
                {
                    result.Failed.Add(Path.GetFileName(type.FullPath) + " " + failure);
                }
            }
            return result;
        }

        // True when every type the box of Place villas would ask to load is one a release unloaded, and none asks a reload, a file that changed,
        // which the person still sees
        public static bool AllReleased(IReadOnlyList<string> lines, IEnumerable<string> released)
        {
            var held = new HashSet<string>((released ?? Enumerable.Empty<string>()).Select(Full).Where(file => file != null), StringComparer.OrdinalIgnoreCase);
            List<string> loads = lines.Where(line => line.StartsWith(PlaceVillasRun.LoadLine, StringComparison.Ordinal)).Select(line => line.Substring(PlaceVillasRun.LoadLine.Length)).ToList();
            return held.Count > 0 && loads.Count > 0 && !lines.Any(line => line.StartsWith(PlaceVillasRun.ReloadLine, StringComparison.Ordinal))
                && loads.All(file => Full(file) != null && held.Contains(Full(file)));
        }

        // The full path, null for none and for a path that does not parse, as a cloud link of Revit gives one with a scheme and a colon
        private static string Full(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException || failure is PathTooLongException || failure is System.Security.SecurityException)
            {
                return null;
            }
        }
    }
}
