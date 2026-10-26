using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SiteAdaptation.Common
{
    // R347. The add-in starts the worker and returns at once, so Revit does not wait. The end of the process hands the answer on,
    // the one the worker wrote for this request or a failure that names the exit code. Past the time limit of the request the
    // worker is killed (it starts no child process), a commit it cut short is put back, the temporary files of its run id are
    // deleted and the answer written here with status timedOut, unless the worker had ended on its own, whose answer stands. The
    // step files of a run that did not pass are deleted (R345). The end and the time limit run on threads of the pool, where an
    // exception would end the host process, Revit from Phase 5a, so each one becomes a failed answer with its reason and goes no
    // further
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
        private readonly Action beforeExit;
        private bool logClosed;
        private Timer timer;

        public bool Ended { get; private set; }

        // An exception the hand off of the add-in threw, kept here since nothing may carry it further on a thread of the pool
        public Exception HandOffFailure { get; private set; }

        private WorkerLaunch(ProcessStartInfo start, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended, Action beforeExit)
        {
            this.workFolder = workFolder;
            this.request = request;
            this.ended = ended;
            this.beforeExit = beforeExit;
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
        // 0 a timer holds or under addin.workerTimeLimitMinS of the config the request names, and a run id this work folder ran
        // already, whose log is there. An answer of the run id left on disk with no log is deleted, so only an answer the new worker
        // writes is handed on
        public static WorkerLaunch Start(ProcessStartInfo start, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
        {
            return Start(start, workFolder, request, ended, null);
        }

        // beforeExit runs on the thread of the end before it is handled, the tests hold it there so the time limit fires first
        internal static WorkerLaunch Start(ProcessStartInfo start, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended, Action beforeExit)
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
            WorkerFile config = request.Inputs?.FirstOrDefault(input => input.Role == "config");
            if (config == null || string.IsNullOrEmpty(config.Path))
            {
                throw new ArgumentException("the request of run " + request.RunId + " names no config, which holds the shortest time limit and the settings of the worker");
            }
            double minimum = AddinConfig.Load(config.Path).WorkerTimeLimitMinS;
            if (limit < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "the time limit of run " + request.RunId + " is " + limit.ToString(CultureInfo.InvariantCulture)
                    + " s, under the minimum of " + minimum.ToString(CultureInfo.InvariantCulture) + " s, addin.workerTimeLimitMinS of config");
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
            var launch = new WorkerLaunch(start, workFolder, request, ended, beforeExit);
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
                beforeExit?.Invoke();
                if (!Finish())
                {
                    return;
                }
                answer = Ending();
            }
            catch (Exception failure)
            {
                answer = Failed(null, "the end of the worker could not be handled, " + failure.GetType().Name + ": " + failure.Message);
            }
            answer = WithoutSteps(answer);
            Close();
            Hand(answer);
        }

        // The answer of a worker that ended
        private WorkerAnswer Ending()
        {
            int? code = null;
            try
            {
                // The wait with no limit returns once the standard error is read to its end
                process.WaitForExit();
                code = process.ExitCode;
                return Answered(code.Value);
            }
            catch (Exception failure)
            {
                return Failed(code, "the end of the worker could not be read, " + failure.GetType().Name + ": " + failure.Message);
            }
        }

        // The answer of the worker when it is this request's: the same run id, the same document and the exit code the process ended
        // with, else a failure that names the code and why
        private WorkerAnswer Answered(int code)
        {
            string why;
            return Own(code, out why) ?? Failed(code, why);
        }

        // The answer the worker wrote for this request and the exit code it ended with, else null and why not
        private WorkerAnswer Own(int code, out string why)
        {
            why = null;
            string path = WorkerFiles.AnswerPath(workFolder, request.RunId);
            if (!File.Exists(path))
            {
                why = WorkerExit.NoAnswer(code);
                return null;
            }
            WorkerAnswer answer;
            try
            {
                answer = WorkerAnswer.Read(path);
            }
            catch (Exception failure)
            {
                why = "the worker ended with code " + code + " and its answer could not be read, " + failure.GetType().Name + ": " + failure.Message;
                return null;
            }
            if (answer == null || answer.RunId != request.RunId || !SameDocument(answer.Document, request.Document) || answer.ExitCode != code)
            {
                why = "the worker ended with code " + code + ", its answer names run " + answer?.RunId + " of " + Named(answer?.Document) + " with exit code " + answer?.ExitCode
                    + ", the request is run " + request.RunId + " of " + Named(request.Document) + ", the answer is not used";
                return null;
            }
            return answer;
        }

        private bool HasExited()
        {
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        // R345. Nothing of a run that did not pass is used, so the step files of its steps go and the parity finds them missing
        private WorkerAnswer WithoutSteps(WorkerAnswer answer)
        {
            if (answer.Status == WorkerExit.Status(WorkerExit.Done))
            {
                return answer;
            }
            try
            {
                StepFile.DeleteSteps(request);
            }
            catch (Exception failure)
            {
                answer.Message += ", and its step files could not be deleted, " + failure.GetType().Name + ": " + failure.Message;
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
                // The worker ended within its time and the end was not handled yet, so its answer stands as on a plain end
                answer = HasExited() ? Ending() : Killed();
            }
            catch (Exception failure)
            {
                answer = Failed(null, "the time limit of the worker could not be handled, " + failure.GetType().Name + ": " + failure.Message);
            }
            answer = WithoutSteps(answer);
            Close();
            Hand(answer);
        }

        // The worker past its time limit is killed. When it ended on its own between the look and the kill, the answer of this run
        // that names the code it ended with is its own and stands: the code of a kill, -1 on Windows and 137 on Linux, is no code of
        // an answer. Else a commit the kill cut short goes back, its temporary files go and the answer timedOut is written here
        private WorkerAnswer Killed()
        {
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
            if (HasExited())
            {
                string why;
                WorkerAnswer own = Own(process.ExitCode, out why);
                if (own != null)
                {
                    return own;
                }
            }
            string folders = null;
            try
            {
                folders = WorkFiles.RestoreRun(new[] { workFolder, request.OutputFolder, request.StepsFolder }, request.RunId);
            }
            catch (Exception failure)
            {
                problems.Add(failure.Message);
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
            string ended = limit + " and was ended, " + (folders == null ? "" : folders + ", ") + "nothing of the run is used";
            WorkerAnswer answer = problems.Count == 0
                ? new WorkerAnswer { RunId = request.RunId, Document = request.Document, Status = "timedOut", Message = ended }
                : Failed(null, ended + ", and " + string.Join(", ", problems));
            try
            {
                WorkFiles.WriteText(WorkerFiles.AnswerPath(workFolder, request.RunId), request.RunId, answer.ToJson());
            }
            catch (Exception failure)
            {
                answer = Failed(null, answer.Message + ", and the answer could not be written, " + failure.GetType().Name + ": " + failure.Message);
            }
            return answer;
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
