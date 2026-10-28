using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/02-folders.md Revit does not wait. The Exited event of a worker comes on a thread of the pool, so its answer
    // waits here and raise asks Revit for its API context, an ExternalEvent in Revit. In that context Route hands each answer to
    // the document of its request by AnswerRouting of Common, whatever document is active, or drops it with its issue
    public sealed class AnswerRelay
    {
        private readonly ConcurrentQueue<WorkerAnswer> waiting = new ConcurrentQueue<WorkerAnswer>();
        private readonly Action raise;

        public AnswerRelay(Action raise)
        {
            this.raise = raise;
        }

        public int Waiting
        {
            get { return waiting.Count; }
        }

        public void Post(WorkerAnswer answer)
        {
            waiting.Enqueue(answer);
            raise();
        }

        public List<RoutedAnswer> Route(IEnumerable<OpenDocument> open)
        {
            var routed = new List<RoutedAnswer>();
            List<OpenDocument> documents = new List<OpenDocument>(open);
            while (waiting.TryDequeue(out WorkerAnswer answer))
            {
                OpenDocument document = AnswerRouting.Find(answer, documents, out string issue);
                routed.Add(new RoutedAnswer(answer, document, issue));
            }
            return routed;
        }
    }

    public sealed class RoutedAnswer
    {
        public RoutedAnswer(WorkerAnswer answer, OpenDocument document, string issue)
        {
            Answer = answer;
            Document = document;
            Issue = issue;
        }

        public WorkerAnswer Answer { get; }

        // Null when the document of the request closed, then Issue names the run
        public OpenDocument Document { get; }
        public string Issue { get; }
    }
}
