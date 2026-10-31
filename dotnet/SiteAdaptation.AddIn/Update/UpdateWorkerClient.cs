using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Update;

namespace SiteAdaptation.AddIn.Update
{
    // R347, R402. Asks the worker CadReader.exe, installed beside the add-in, to read the Excel files of a request and waits for its answer. The
    // wait is inside the modal window of Update from report, which holds the user at that button anyway, and WorkerLaunch ends the worker at
    // the time limit of the request, addin.workerTimeLimitS.updateFromReport, and hands on a timedOut answer, so the wait ends
    public static class UpdateWorkerClient
    {
        public static JObject Read(WorkerRequest request, string workFolder)
        {
            // The installer puts the worker in its own folder, where button 2 starts it (Revit run of 5d)
            string exe = LaunchedWorker.BesideAddIn();
            if (!File.Exists(exe))
            {
                throw new FileNotFoundException(TextTemplate.Fill(UpdateFromReportText.WorkerMissing, exe));
            }
            Directory.CreateDirectory(workFolder);
            string requestFile = request.Write(workFolder);
            WorkerAnswer answer = null;
            using (var done = new ManualResetEventSlim())
            {
                using (WorkerLaunch.Start(WorkerLaunch.WorkerStart(exe, requestFile), workFolder, request, ended => { answer = ended; done.Set(); }))
                {
                    done.Wait();
                }
            }
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                throw new InvalidOperationException(TextTemplate.Fill(UpdateFromReportText.WorkerFailed, answer == null ? UpdateFromReportText.WorkerNoAnswer : answer.Status + ", " + answer.Message));
            }
            return PyJson.Parse(WorkFiles.ReadText(Path.Combine(request.OutputFolder, UpdateFromReportOutput))) as JObject
                ?? throw new InvalidDataException(UpdateFromReportText.WorkerNoJson);
        }

        // The name the worker command writes, CadReader.UpdateFromReportWorker.OutputName, the add-in loads no CadReader type
        private const string UpdateFromReportOutput = "update_from_report.json";
    }
}
