using System.Collections.Generic;

namespace SiteAdaptation.Common
{
    // R347. The commands the worker runs so far: build, which writes the step files of R345 for the steps the C# gives. The
    // other commands of the design come with the phase that builds them, and until then a request of one is refused
    public static class WorkerCommands
    {
        public static Dictionary<string, WorkerCommand> Default()
        {
            return new Dictionary<string, WorkerCommand> { ["build"] = Build };
        }

        private static List<string> Build(WorkerRequest request, List<string> issues, Dictionary<string, int> counts)
        {
            var written = new List<string>();
            if (request.Steps == null || request.Steps.Count == 0)
            {
                issues.Add("no step was asked, the C# build writes no plan before Phase 4h");
            }
            return written;
        }
    }
}
