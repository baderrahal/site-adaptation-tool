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

        private static string Input(WorkerRequest request, string role)
        {
            return request.Inputs.First(input => input.Role == role).Path;
        }

        private static List<string> Build(WorkerRequest request, List<string> issues, Dictionary<string, int> counts)
        {
            var written = new List<string>();
            if (request.Steps == null || request.Steps.Count == 0)
            {
                issues.Add("no step was asked, the C# build writes no plan before Phase 4h");
                return written;
            }
            AddinConfig config = AddinConfig.Load(Input(request, "config"));
            foreach (string step in request.Steps)
            {
                if (step == FaceStep.Name)
                {
                    written.Add(Faces(request, config, counts));
                }
            }
            return written;
        }

        // R345. The step faces of the plots of the scope, step_faces.json in the steps folder of the request
        private static string Faces(WorkerRequest request, AddinConfig config, Dictionary<string, int> counts)
        {
            List<string> numbers = PlotScope.Numbers(request.Scope, CsvRows.Read(Input(request, "labels")));
            JArray plots = JObject.Parse(WorkFiles.ReadText(Input(request, "baseplanPlots")))["plots"] as JArray ?? throw new InvalidDataException("the baseplan plots.json holds no plots list");
            var faces = FaceStep.Build(numbers, plots, CsvRows.Read(Input(request, "plotsCsv")));
            counts[FaceStep.Name + " plots"] = faces.Count;
            counts[FaceStep.Name + " with a face"] = faces.Values.Count(face => true.Equals(face["face"]));
            string text = StepFile.Text(FaceStep.Name, "csharp", faces, config.StepFields(FaceStep.Name));
            return StepFile.Write(request.StepsFolder, FaceStep.Name, request.RunId, text);
        }
    }
}
