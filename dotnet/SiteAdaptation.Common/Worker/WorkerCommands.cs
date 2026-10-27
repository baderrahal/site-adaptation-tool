using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R347. The commands the worker runs so far: build, which writes the step files of R345 for the steps the C# gives, the faces and the
    // build steps of R354 to R357. The
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
            // R354, Q089. plan_from_book.py refuses --analysis before any step, so the worker refuses a request that names one
            if ((request.Inputs ?? new List<WorkerFile>()).Any(input => input.Role == "analysis"))
            {
                throw new WorkerStop(WorkerExit.Refused, BuildSteps.AnalysisRefused);
            }
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
            // R354 to R357. The build steps on the inputs read once, after the faces
            List<string> built = request.Steps.Where(BuildSteps.Names.Contains).ToList();
            if (built.Count > 0)
            {
                BuildSteps.Run(run, config, built);
            }
        }

        // R345. The step faces of the plots of the scope, step_faces_csharp.json in the steps folder of the request
        private static void Faces(WorkerContext run, AddinConfig config)
        {
            WorkerRequest request = run.Request;
            List<string> numbers = PlotScope.Of(run);
            string role = request.Inputs.Any(input => input.Role == "baseplanRead") ? "baseplanRead" : "baseplanPlots";
            if (request.Inputs.Count(input => input.Role == "baseplanRead" || input.Role == "baseplanPlots") > 1)
            {
                throw new WorkerStop(WorkerExit.Refused, "the request names two baseplans, baseplanPlots and baseplanRead, a build reads one");
            }
            string path = run.Input(role);
            JObject baseplan = JObject.Parse(WorkFiles.ReadText(path));
            // R348. A drawing output is read only of a drawing in Meters, by its own units or the layers.json of its read
            string refusal = CadUnits.Refusal(path, baseplan);
            if (refusal != null)
            {
                throw new WorkerStop(WorkerExit.Refused, refusal);
            }
            JArray plots = baseplan["plots"] as JArray ?? throw new InvalidDataException("the baseplan plots.json holds no plots list");
            var faces = FaceStep.Build(numbers, plots, CsvRows.Read(run.Input("plotsCsv"), CsvRows.PlotsColumns), config.ParityTolerance("areas") ?? throw new InvalidDataException("addin.parity.kinds.areas is null, the tolerance of the baseplan faces (R345)"));
            run.Counts[FaceStep.Name + " plots"] = faces.Count;
            run.Counts[FaceStep.Name + " with a face"] = faces.Values.Count(face => true.Equals(face["face"]));
            run.WriteJson(request.StepsFolder, StepFile.FileName(FaceStep.Name, "csharp"), StepFile.Build(FaceStep.Name, "csharp", StepFile.ScopeText(request.Scope), faces, config.StepFields(FaceStep.Name)));
        }
    }
}
