using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common
{
    // An open document of Revit as the add-in sees it: its full path and the session id the add-in gave it when it opened
    public sealed class OpenDocument
    {
        public string Path { get; set; }
        public string SessionId { get; set; }
        public bool Active { get; set; }
    }

    // R347. An answer belongs to the document of its request, found among the open documents by its path and session id whatever
    // document is active. A document that closed, or closed and opened again with a new session id, gets nothing of it
    public static class AnswerRouting
    {
        public static OpenDocument Find(WorkerAnswer answer, IEnumerable<OpenDocument> open, out string issue)
        {
            WorkerDocument wanted = answer.Document;
            OpenDocument found = wanted == null ? null : open.FirstOrDefault(document => SamePath(document.Path, wanted.Path) && document.SessionId == wanted.SessionId);
            issue = found == null ? "run " + answer.RunId + " ended after its document closed, nothing applied" : null;
            return found;
        }

        // Windows paths ignore case, a path is compared in full
        internal static bool SamePath(string one, string other)
        {
            if (one == null || other == null)
            {
                return false;
            }
            return string.Equals(Full(one), Full(other), StringComparison.OrdinalIgnoreCase);
        }

        private static string Full(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path;
            }
        }
    }
}
