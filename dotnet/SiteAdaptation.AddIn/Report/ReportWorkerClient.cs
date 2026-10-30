using System;
using System.IO;
using System.Reflection;
using System.Threading;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.Report
{
    // R347, R381. Asks the worker CadReader.exe, installed beside the add-in, for the report Excel of one request and waits for its answer, as
    // UpdateWorkerClient does for Update from report. WorkerLaunch ends the worker at addin.workerTimeLimitS.reportExcel of the request and hands
    // on a timedOut answer, so the wait ends. A run that did not end Done throws with its status and message
    public static class ReportWorkerClient
    {
        public static WorkerAnswer Run(WorkerRequest request, string workFolder)
        {
            string exe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CadReader.exe");
            if (!File.Exists(exe))
            {
                throw new FileNotFoundException("the worker CadReader.exe is not beside the add-in: " + exe);
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
                throw new InvalidOperationException("the worker ended " + (answer == null ? "with no answer" : answer.Status + ", " + answer.Message));
            }
            return answer;
        }
    }
}
