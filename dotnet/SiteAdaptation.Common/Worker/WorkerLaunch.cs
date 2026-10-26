using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace SiteAdaptation.Common
{
    // R347. The add-in starts the worker and returns at once, so Revit does not wait. The end of the process hands the answer on,
    // the one the worker wrote for this request or a failure that names the exit code. Past the time limit of the request the
    // worker is killed (it starts no child process), the temporary files of its run id are deleted and the answer written here with
    // status timedOut. The end and the time limit run on threads of the pool, where an exception would end the host process, Revit
    // from Phase 5a, so each one becomes a failed answer with its reason and goes no further
    public sealed class WorkerLaunch : IDisposable
    {
        // The longest due time System.Threading.Timer holds, 2^32 - 2 ms
        public const double LongestTimeLimitS = 4294967.294;

        private readonly object gate = new object();
        private readonly Process process;
        private readonly string workFolder;
        private readonly WorkerRequest request;
        private readonly Action<WorkerAnswer> ended;
        private readonly StreamWriter log;
        private bool logClosed;
        private Timer timer;

        public bool Ended { get; private set; }

        // An exception the hand off of the add-in threw, kept here since nothing may carry it further on a thread of the pool
        public Exception HandOffFailure { get; private set; }

        private WorkerLaunch(ProcessStartInfo start, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
        {
            this.workFolder = workFolder;
            this.request = request;
            this.ended = ended;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardError = true;
            log = new StreamWriter(new FileStream(WorkerFiles.LogPath(workFolder, request.RunId), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.ErrorDataReceived += (sender, line) => LogLine(line.Data);
            process.Exited += (sender, args) => Exit();
        }

        // CadReader.exe --request <file>, the command of the worker
        public static ProcessStartInfo WorkerStart(string exe, string requestPath)
        {
            return new ProcessStartInfo(exe, "--request \"" + requestPath + "\"");
        }

        // Refuses before anything is started or written: a run id of another form, a time limit that is no number of seconds above
        // 0 a timer holds, and a run id this work folder ran already, whose log is there. An answer of the run id left on disk with
        // no log is deleted, so only an answer the new worker writes is handed on
        public static WorkerLaunch Start(ProcessStartInfo start, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
        {
            if (request == null || !WorkerFiles.IsRunId(request.RunId))
            {
                throw new ArgumentException("the request names no run id of letters, digits, dash and underscore");
            }
            double limit = request.TimeLimitS;
            if (double.IsNaN(limit) || double.IsInfinity(limit) || limit <= 0 || limit > LongestTimeLimitS)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "the time limit of run " + request.RunId + " is " + limit.ToString(CultureInfo.InvariantCulture)
                    + " s, it must be a number of seconds above 0 and at most " + LongestTimeLimitS.ToString(CultureInfo.InvariantCulture) + ", addin.workerTimeLimitS of config");
            }
            if (File.Exists(WorkerFiles.LogPath(workFolder, request.RunId)))
            {
                throw new InvalidOperationException("the run id " + request.RunId + " was used already, its log " + WorkerFiles.LogPath(workFolder, request.RunId) + " is in the work folder, a run takes a new run id");
            }
            string answer = WorkerFiles.AnswerPath(workFolder, request.RunId);
            if (File.Exists(answer))
            {
                File.Delete(answer);
            }
            var launch = new WorkerLaunch(start, workFolder, request, ended);
            try
            {
                launch.process.Start();
            }
            catch (Exception)
            {
                launch.Close();
                launch.process.Dispose();
                throw;
            }
            launch.process.BeginErrorReadLine();
            lock (launch.gate)
            {
                if (!launch.Ended)
                {
                    launch.timer = new Timer(state => launch.TimeUp(), null, TimeSpan.FromSeconds(limit), Timeout.InfiniteTimeSpan);
                }
            }
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
            WorkerAnswer answer;
            try
            {
                if (!Finish())
                {
                    return;
                }
                int? code = null;
                try
                {
                    // The wait with no limit returns once the standard error is read to its end
                    process.WaitForExit();
                    code = process.ExitCode;
                    answer = Answered(code.Value);
                }
                catch (Exception failure)
                {
                    answer = Failed(code, "the end of the worker could not be read, " + failure.GetType().Name + ": " + failure.Message);
                }
            }
            catch (Exception failure)
            {
                answer = Failed(null, "the end of the worker could not be handled, " + failure.GetType().Name + ": " + failure.Message);
            }
            Close();
            Hand(answer);
        }

        // The answer of the worker when it is this request's: the same run id, the same document and the exit code the process ended
        // with, else a failure that names the code and why
        private WorkerAnswer Answered(int code)
        {
            string path = WorkerFiles.AnswerPath(workFolder, request.RunId);
            if (!File.Exists(path))
            {
                return Failed(code, WorkerExit.NoAnswer(code));
            }
            WorkerAnswer answer;
            try
            {
                answer = WorkerAnswer.Read(path);
            }
            catch (Exception failure)
            {
                return Failed(code, "the worker ended with code " + code + " and its answer could not be read, " + failure.GetType().Name + ": " + failure.Message);
            }
            if (answer == null || answer.RunId != request.RunId || !SameDocument(answer.Document, request.Document) || answer.ExitCode != code)
            {
                return Failed(code, "the worker ended with code " + code + ", its answer names run " + answer?.RunId + " of " + Named(answer?.Document) + " with exit code " + answer?.ExitCode
                    + ", the request is run " + request.RunId + " of " + Named(request.Document) + ", the answer is not used");
            }
            return answer;
        }

        private static bool SameDocument(WorkerDocument one, WorkerDocument other)
        {
            return one != null && other != null && one.SessionId == other.SessionId && AnswerRouting.SamePath(one.Path, other.Path);
        }

        private static string Named(WorkerDocument document)
        {
            return document == null ? "no document" : document.Path + " session " + document.SessionId;
        }

        private WorkerAnswer Failed(int? code, string message)
        {
            return new WorkerAnswer { RunId = request.RunId, Document = request.Document, Status = WorkerExit.Status(WorkerExit.Failed), ExitCode = code, Message = message };
        }

        private void TimeUp()
        {
            WorkerAnswer answer;
            try
            {
                if (!Finish())
                {
                    return;
                }
                string limit = "the worker ran past its time limit of " + request.TimeLimitS.ToString("0.###", CultureInfo.InvariantCulture) + " s";
                var problems = new System.Collections.Generic.List<string>();
                try
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
                catch (InvalidOperationException)
                {
                    // The process had ended already
                }
                catch (Exception failure)
                {
                    problems.Add("it could not be ended, " + failure.GetType().Name + ": " + failure.Message);
                }
                foreach (string folder in new[] { workFolder, request.OutputFolder, request.StepsFolder })
                {
                    try
                    {
                        WorkFiles.DeleteRun(folder, request.RunId);
                    }
                    catch (Exception failure)
                    {
                        problems.Add(failure.Message);
                    }
                }
                answer = problems.Count == 0
                    ? new WorkerAnswer { RunId = request.RunId, Document = request.Document, Status = "timedOut", Message = limit + " and was ended, nothing of the run is used" }
                    : Failed(null, limit + ", nothing of the run is used, and " + string.Join(", ", problems));
                try
                {
                    WorkFiles.WriteText(WorkerFiles.AnswerPath(workFolder, request.RunId), request.RunId, answer.ToJson());
                }
                catch (Exception failure)
                {
                    answer = Failed(null, answer.Message + ", and the answer could not be written, " + failure.GetType().Name + ": " + failure.Message);
                }
            }
            catch (Exception failure)
            {
                answer = Failed(null, "the time limit of the worker could not be handled, " + failure.GetType().Name + ": " + failure.Message);
            }
            Close();
            Hand(answer);
        }

        private void Hand(WorkerAnswer answer)
        {
            try
            {
                ended(answer);
            }
            catch (Exception failure)
            {
                HandOffFailure = failure;
                try
                {
                    File.AppendAllText(WorkerFiles.LogPath(workFolder, request.RunId), "the hand off of the answer threw " + failure.GetType().Name + ": " + failure.Message + Environment.NewLine);
                }
                catch (Exception)
                {
                    // The failure stays on HandOffFailure
                }
            }
        }

        private void LogLine(string line)
        {
            if (line == null)
            {
                return;
            }
            try
            {
                lock (log)
                {
                    if (!logClosed)
                    {
                        log.WriteLine(line);
                    }
                }
            }
            catch (Exception)
            {
                // A log line that cannot be written is lost, never the host
            }
        }

        private void Close()
        {
            try
            {
                lock (log)
                {
                    logClosed = true;
                    log.Dispose();
                }
            }
            catch (Exception)
            {
                // The log is closed as far as it can be
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
            process.Dispose();
        }
    }
}
