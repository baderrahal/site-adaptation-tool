using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

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

        private static void Build(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (request.Steps == null || request.Steps.Count == 0)
            {
                run.Issues.Add("no step was asked, the C# build writes no plan before Phase 4h");
                return;
            }
            AddinConfig config = AddinConfig.Load(run.Input("config"));
            foreach (string step in request.Steps)
            {
                if (step == FaceStep.Name)
                {
                    Faces(run, config);
                }
            }
        }

        // R345. The step faces of the plots of the scope, step_faces_csharp.json in the steps folder of the request
        private static void Faces(WorkerContext run, AddinConfig config)
        {
            WorkerRequest request = run.Request;
            List<string> numbers = PlotScope.Numbers(request.Scope, CsvRows.Read(run.Input("labels")));
            JArray plots = JObject.Parse(WorkFiles.ReadText(run.Input("baseplanPlots")))["plots"] as JArray ?? throw new InvalidDataException("the baseplan plots.json holds no plots list");
            var faces = FaceStep.Build(numbers, plots, CsvRows.Read(run.Input("plotsCsv")));
            run.Counts[FaceStep.Name + " plots"] = faces.Count;
            run.Counts[FaceStep.Name + " with a face"] = faces.Values.Count(face => true.Equals(face["face"]));
            run.WriteJson(request.StepsFolder, StepFile.FileName(FaceStep.Name, "csharp"), StepFile.Build(FaceStep.Name, "csharp", StepFile.ScopeText(request.Scope), faces, config.StepFields(FaceStep.Name)));
        }
    }
}
