using System;
using System.Threading;
using System.Threading.Tasks;

namespace SiteAdaptation.Common
{
    // R303. The decisions of the dialog watcher of R108 that need no window, so they are tested without Revit
    public static class WatcherRules
    {
        public const int Answered = 0;
        public const int BadArguments = 2;
        public const int StoppedRevit = 3;
        // The start phase worker faulted or never ended, so no one is sure the start dialogs were answered
        public const int WorkerFailed = 4;
        // Not an exit code: the worker or the loop has nothing to end the watch with
        public const int KeepWatching = -1;

        // R303. A window of the job phase is logged once per handle, class, title and visible state, so a window is logged again
        // only when one of them changes
        public static string JobWindowKey(long handle, string windowClass, string title, bool visible)
        {
            return handle + "|" + windowClass + "|" + visible + "|" + title;
        }

        // R303. What the start phase worker hands the main loop, waiting at most wait for it to end: KeepWatching when there is
        // none or it ended with nothing to say, its own exit code, or WorkerFailed when it faulted or did not end in time, which
        // goes to failed with the exception
        public static int AfterWorker(Task<int> worker, TimeSpan wait, Action<string> failed)
        {
            if (worker == null)
            {
                return KeepWatching;
            }
            // The wait handle never throws, where Task.Wait throws on a faulted task
            if (!((IAsyncResult)worker).AsyncWaitHandle.WaitOne(wait))
            {
                failed("the start phase worker did not end in " + wait.TotalSeconds + " s");
                return WorkerFailed;
            }
            if (worker.IsFaulted || worker.IsCanceled)
            {
                failed("the start phase worker failed: " + (worker.Exception != null ? worker.Exception.ToString() : "cancelled"));
                return WorkerFailed;
            }
            return worker.Result;
        }

        // R303. The end of the watch once StopReason gives a reason. The start phase worker ends first and its code wins, so a
        // Revit the worker stopped returns StoppedRevit and never reads as Revit exited. Only a worker with nothing to say lets the
        // reason be written as the result
        public static int End(Task<int> worker, TimeSpan wait, string why, Action<string> failed, Action<string> result)
        {
            int code = AfterWorker(worker, wait, failed);
            if (code != KeepWatching)
            {
                return code;
            }
            result(why);
            return Answered;
        }

        // R303. One report write with one short retry. A write that fails twice goes to error and the watcher keeps watching
        public static bool WriteWithRetry(Action write, TimeSpan pause, Action<string> error)
        {
            try
            {
                write();
                return true;
            }
            catch (Exception)
            {
                // A reader may hold the file for a moment, the retry below takes it
            }
            Thread.Sleep(pause);
            try
            {
                write();
                return true;
            }
            catch (Exception failure)
            {
                error("dialog watcher: the report was not written: " + failure.Message);
                return false;
            }
        }
    }
}
