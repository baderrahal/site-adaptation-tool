using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn
{
    // R375. Button 2 Read data on the document of the run: ReadDataRun of Common with the inputs of the pane by their roles and the
    // worker CadReader.exe beside the add-in. No Revit type, so the tests load it with no Revit
    public static class ReadDataBody
    {
        // The pane names each input of button 2 by its row, the request by its role (R348 to R352)
        public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PaneText.Settings] = ReadInputs.Config, [PaneText.Baseplan] = "baseplan", [PaneText.VillaBlocks] = "villaBlocks", [PaneText.Curbs] = "curbs",
            [PaneText.RoadSurface] = ReadInputs.RoadSurface, [PaneText.DlusTitle] = "dlus", [PaneText.SalesTitle] = "sales", [PaneText.PlotNumbers] = "plotNumbers",
        };

        // The worker the add-in starts, set by App at start. Null keeps button 2 not built, as in the tests
        public static IWorkerStart Worker { get; set; }

        // The inputs of button 2 the pane holds, beside the Settings file every button reads. Prototypes are no input of the command
        // read, no read step takes them yet
        public static IReadOnlyList<RunInput> Inputs(PaneSettings settings)
        {
            return new[]
            {
                new RunInput(PaneText.Baseplan, settings.InputPath(InputKeys.Baseplan), false),
                new RunInput(PaneText.VillaBlocks, settings.InputPath(InputKeys.VillaBlocks), false),
                new RunInput(PaneText.Curbs, settings.InputPath(InputKeys.Curbs), false),
                new RunInput(PaneText.RoadSurface, settings.InputPath(InputKeys.RoadSurface), true),
                new RunInput(PaneText.DlusTitle, settings.InputPath(InputKeys.Dlus), false),
                new RunInput(PaneText.SalesTitle, settings.InputPath(InputKeys.Sales), false),
                new RunInput(PaneText.PlotNumbers, settings.InputPath(InputKeys.PlotNumbers), false),
            };
        }

        // R438, Q246. The read runs after the scan of the same files, reused when they did not change, and the gate of the window What
        // the tool read. owner is the Revit main window the window opens over
        public static ButtonBody For(DocumentSession session, IntPtr owner)
        {
            Func<SettingsScope> scope = () => new SettingsScope { Kind = session.Settings.Scope.Kind, Nbh = session.Settings.Scope.Nbh, Plots = session.Settings.Scope.Plots };
            var document = new WorkerDocument { Path = session.Path, SessionId = session.SessionId };
            ButtonBody read = new ReadDataRun(Roles, Worker, document, scope, () => session.Settings.Project).Body();
            // A run of the test host has no one to pick in the window and runs its read as before, so the runs of Lane B keep their
            // time and never wait on a window (CHOSEN, SCAN-UI)
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestHostGuard.JobVariable)))
            {
                return read;
            }
            return ScanStep.Wrap(read, Worker, document, () => ScanInputs(session.Settings),
                path => ScanResults.ScanGate.Ask(path, session.Settings, owner, WorkFiles.ReadText),
                () => new[] { session.Settings.RulesFile, LaunchedWorker.BesideAddIn() });
        }

        // The inputs of the read with the Settings file, whose role config the scan needs, and the Prototypes folder
        public static IReadOnlyList<WorkerFile> ScanInputs(PaneSettings settings)
        {
            var pane = new List<RunInput>(Inputs(settings)) { new RunInput(PaneText.Settings, settings.ConfigFile, false) };
            return ScanStep.Inputs(pane, Roles, settings.InputPath(InputKeys.Prototypes));
        }
    }

    // R347, R375. The worker started by WorkerLaunch, its answer posted to the relay of the pane, which raises the ExternalEvent, and in
    // the API context of Revit handed to the end of its run by Hand
    public sealed class LaunchedWorker : IWorkerStart
    {
        private static readonly ConcurrentDictionary<string, Action<WorkerAnswer>> Ends = new ConcurrentDictionary<string, Action<WorkerAnswer>>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, WorkerLaunch> Launches = new ConcurrentDictionary<string, WorkerLaunch>(StringComparer.Ordinal);
        private readonly string exe;
        private readonly Action<WorkerAnswer> post;

        public LaunchedWorker(string exe, Action<WorkerAnswer> post)
        {
            this.exe = exe;
            this.post = post;
        }

        // CadReader.exe in the Worker folder under the add-in, where Install-SiteAdaptation.ps1 puts it with its own redirects
        public static string BesideAddIn()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(LaunchedWorker).Assembly.Location), "Worker", "CadReader.exe");
        }

        public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
        {
            Ends[request.RunId] = ended;
            try
            {
                Launches[request.RunId] = WorkerLaunch.Start(WorkerLaunch.WorkerStart(exe, requestPath), workFolder, request, post);
            }
            catch
            {
                Ends.TryRemove(request.RunId, out _);
                throw;
            }
        }

        // In the API context: each answer goes on with its run, which writes the record of the read in the work folder and frees the
        // button. CHOSEN by Code in Lane B 5c: Buttons applies nothing to a document that closed, the issue of its route stays in the
        // routes of the pane, and the read is there for the next run of that file
        public static void Hand(IEnumerable<RoutedAnswer> routed)
        {
            foreach (RoutedAnswer item in routed)
            {
                string runId = item.Answer.RunId ?? "";
                if (Launches.TryRemove(runId, out WorkerLaunch launch))
                {
                    launch.Dispose();
                }
                if (Ends.TryRemove(runId, out Action<WorkerAnswer> end))
                {
                    end(item.Answer);
                }
            }
        }
    }
}
