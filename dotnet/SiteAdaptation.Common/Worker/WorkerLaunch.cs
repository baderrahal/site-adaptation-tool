using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace SiteAdaptation.Common
{
    // R347. The add-in starts the worker and returns at once, so Revit does not wait. The end of the process hands the answer on,
    // the one the worker wrote or a failure that names the exit code. Past the time limit of the request the worker is killed (it
    // starts no child process), the temporary files of its run id are deleted and the answer written here with status timedOut
    public sealed class WorkerLaunch : IDisposable
    {
        private readonly object gate = new object();
        private readonly Process process;
        private readonly string workFolder;
        private readonly WorkerRequest request;
        private readonly Action<WorkerAnswer> ended;
        private readonly StreamWriter log;
        private Timer timer;

        public bool Ended { get; private set; }

        private WorkerLaunch(ProcessStartInfo start, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
        {
            this.workFolder = workFolder;
            this.request = request;
            this.ended = ended;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardError = true;
            log = new StreamWriter(new FileStream(WorkerFiles.LogPath(workFolder, request.RunId), FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.ErrorDataReceived += (sender, line) =>
            {
                if (line.Data != null)
                {
                    lock (log)
                    {
                        log.WriteLine(line.Data);
                    }
                }
            };
            process.Exited += (sender, args) => Exit();
        }

        // CadReader.exe --request <file>, the command of the worker
        public static ProcessStartInfo WorkerStart(string exe, string requestPath)
        {
            return new ProcessStartInfo(exe, "--request \"" + requestPath + "\"");
        }

        public static WorkerLaunch Start(ProcessStartInfo start, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
        {
            var launch = new WorkerLaunch(start, workFolder, request, ended);
            launch.process.Start();
            launch.process.BeginErrorReadLine();
            launch.timer = new Timer(state => launch.TimeUp(), null, TimeSpan.FromSeconds(request.TimeLimitS), Timeout.InfiniteTimeSpan);
            return launch;
        }

        private bool Finish()
        {
            lock (gate)
            {
                if (Ended)
                {
                    return false;
                }
                Ended = true;
                timer?.Dispose();
                return true;
            }
        }

        private void Exit()
        {
            if (!Finish())
            {
                return;
            }
            // The wait with no limit returns once the standard error is read to its end
            process.WaitForExit();
            int code = process.ExitCode;
            string path = WorkerFiles.AnswerPath(workFolder, request.RunId);
            WorkerAnswer answer = File.Exists(path) ? WorkerAnswer.Read(path) : new WorkerAnswer
            {
                RunId = request.RunId,
                Document = request.Document,
                Status = WorkerExit.Status(WorkerExit.Failed),
                ExitCode = code,
                Message = WorkerExit.NoAnswer(code),
            };
            Close();
            ended(answer);
        }

        private void TimeUp()
        {
            if (!Finish())
            {
                return;
            }
            try
            {
                process.Kill();
                process.WaitForExit(10000);
            }
            catch (InvalidOperationException)
            {
            }
            foreach (string folder in new[] { workFolder, request.OutputFolder, request.StepsFolder })
            {
                WorkFiles.DeleteRun(folder, request.RunId);
            }
            var answer = new WorkerAnswer
            {
                RunId = request.RunId,
                Document = request.Document,
                Status = "timedOut",
                Message = "the worker ran past its time limit of " + request.TimeLimitS.ToString("0.###", CultureInfo.InvariantCulture) + " s and was ended, nothing of the run is used",
            };
            WorkFiles.WriteText(WorkerFiles.AnswerPath(workFolder, request.RunId), request.RunId, answer.ToJson());
            Close();
            ended(answer);
        }

        private void Close()
        {
            lock (log)
            {
                log.Dispose();
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
            process.Dispose();
        }
    }
}
