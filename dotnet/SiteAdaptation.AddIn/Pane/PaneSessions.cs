using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/07-mock.md 7.2. Each open document gets a session id, a new GUID on DocumentOpened and on
    // DocumentCreated held in memory beside it, with its settings of the pane. The key is the Revit Document, which compares by the
    // document it wraps, so this class needs no Revit type and a test gives any object
    public sealed class PaneSessions
    {
        private readonly Dictionary<object, DocumentSession> open = new Dictionary<object, DocumentSession>();
        private readonly Dictionary<int, object> closing = new Dictionary<int, object>();

        public int Count
        {
            get { return open.Count; }
        }

        // Opened again after a close, the document gets a new id, so an answer of its earlier session finds nothing
        public DocumentSession Opened(object key, string path, string title)
        {
            var session = new DocumentSession(Guid.NewGuid().ToString("D"), path, title);
            open[key] = session;
            return session;
        }

        public DocumentSession Find(object key)
        {
            if (key == null)
            {
                return null;
            }
            return open.TryGetValue(key, out DocumentSession session) ? session : null;
        }

        // A document open before the add-in saw it, as one Revit opened from its command line, gets its session on first sight
        public DocumentSession Seen(object key, string path, string title)
        {
            DocumentSession session = Find(key);
            if (session == null)
            {
                return Opened(key, path, title);
            }
            session.Path = path;
            session.Title = title;
            return session;
        }

        // DocumentClosed gives only the id of DocumentClosing, the close may still be cancelled between the two
        public void Closing(int documentId, object key)
        {
            closing[documentId] = key;
        }

        public void Closed(int documentId, bool closed)
        {
            if (closing.TryGetValue(documentId, out object key))
            {
                closing.Remove(documentId);
                if (closed)
                {
                    open.Remove(key);
                }
            }
        }

        // The open documents as AnswerRouting of Common reads them, the one of active marked
        public List<OpenDocument> Documents(object active)
        {
            return open.Select(pair => new OpenDocument { Path = pair.Value.Path, SessionId = pair.Value.SessionId, Active = Equals(pair.Key, active) }).ToList();
        }

        public object KeyOf(string sessionId)
        {
            return open.Where(pair => pair.Value.SessionId == sessionId).Select(pair => pair.Key).FirstOrDefault();
        }
    }

    public sealed class DocumentSession
    {
        public DocumentSession(string sessionId, string path, string title)
        {
            SessionId = sessionId;
            Path = path;
            Title = title;
            Settings = new PaneSettings();
        }

        public string SessionId { get; }
        public string Path { get; set; }
        public string Title { get; set; }
        public PaneSettings Settings { get; set; }

        public bool HasPath
        {
            get { return !string.IsNullOrEmpty(Path); }
        }
    }
}
