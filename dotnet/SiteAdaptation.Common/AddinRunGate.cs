using System;

namespace SiteAdaptation.Common
{
    // R372, QL20. Run 2 of 5a stopped on "Can not subscribe to an event during execution of that event!" when Start added its Idling
    // handler from inside the first Idling. So the test host adds every handler once, from OnStartup, and never adds or removes one
    // inside a Revit event. This flag is what starts and ends the job instead, and each handler returns at once when it is not live
    public sealed class RunGate
    {
        public bool Started { get; private set; }
        public bool Finished { get; private set; }

        public bool Live
        {
            get { return Started && !Finished; }
        }

        // True the one time the job starts, never again and never after it ended
        public bool Begin()
        {
            if (Started || Finished)
            {
                return false;
            }
            Started = true;
            return true;
        }

        public void End()
        {
            Finished = true;
        }
    }

    // R372, QL20 point 4. Whether an ExternalEvent the test host raises on the Revit Home screen, before any document is made, runs
    // there. Run 3 records it first, since the whole job hangs on it
    public sealed class RunHomeEvent
    {
        // Raised is true when Revit took the raise
        public bool Raised { get; set; }
        public DateTime? RaisedUtc { get; set; }
        public int? DocumentsAtRaise { get; set; }
        // Null until the event runs or the job gives up on it, false when it gave up
        public bool? Ran { get; set; }
        public double? Seconds { get; set; }
        public bool? NoDocument { get; set; }
        public int? DocumentsAtRun { get; set; }

        public void Raise(DateTime nowUtc, int documents, bool accepted)
        {
            Raised = accepted;
            RaisedUtc = nowUtc;
            DocumentsAtRaise = documents;
        }

        // True the first time the raised event runs. The phases after it run from the same event and leave the record as it is
        public bool Run(DateTime nowUtc, bool noDocument, int documents)
        {
            if (!Raised || Ran.HasValue)
            {
                return false;
            }
            Ran = true;
            Seconds = RaisedUtc.HasValue ? Math.Round((nowUtc - RaisedUtc.Value).TotalSeconds, 1) : (double?)null;
            NoDocument = noDocument;
            DocumentsAtRun = documents;
            return true;
        }

        // The job gave up on a raised event, so one that has not run never ran
        public void NotRun()
        {
            if (Raised && !Ran.HasValue)
            {
                Ran = false;
            }
        }
    }
}
