using System.IO;

namespace SiteAdaptation.Common
{
    // R382, docs/design/09-risks.md open item 2. Whether a Document.PathName Revit gives is a saved path, the one test the flow, the
    // work folder and the pane use. Revit run 3 of Lane B 5a measured a document opened detached and never saved with the bare
    // PathName SA5A-CENTRAL-TEST-NEW_detached.rvt, so an empty check alone takes such a name as saved
    public static class SavedPath
    {
        // True only for a full path on a drive, X:\ or X:/, or on a share, \\server\share\... A bare name, a relative path, a path
        // on the current drive as \x.rvt and one relative to a drive as C:x.rvt are not saved. CHOSEN: a cloud path, the
        // "BIM 360://" or "Autodesk Docs://" form of Revit, is no local saved path either, so a button on a cloud model stops with
        // RunTexts.SaveFirst, since the work folder is named by a local file
        public static bool Is(string pathName)
        {
            if (string.IsNullOrWhiteSpace(pathName) || pathName.Contains("://") || pathName.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }
            if (!Path.IsPathRooted(pathName))
            {
                return false;
            }
            return OnDrive(pathName) || OnShare(pathName);
        }

        private static bool Separator(char letter)
        {
            return letter == '\\' || letter == '/';
        }

        private static bool OnDrive(string path)
        {
            char drive = char.ToUpperInvariant(path[0]);
            return path.Length > 3 && drive >= 'A' && drive <= 'Z' && path[1] == ':' && Separator(path[2]);
        }

        // \\server\share and a name below it, each part not empty
        private static bool OnShare(string path)
        {
            if (path.Length < 5 || !Separator(path[0]) || !Separator(path[1]))
            {
                return false;
            }
            string[] parts = path.Substring(2).Split('\\', '/');
            return parts.Length >= 3 && parts[0].Length > 0 && parts[1].Length > 0 && parts[parts.Length - 1].Length > 0;
        }
    }
}
