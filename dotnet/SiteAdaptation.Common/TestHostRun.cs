using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SiteAdaptation.Common
{
    // testhost_<code>_log.json (R104). Written again after every step, so a run that hangs still shows where
    public class TestHostLog
    {
        public string Tool { get; set; } = "SiteAdaptation.TestHost";
        public string Code { get; set; }
        public string Kind { get; set; }
        public string Model { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public string PrototypeFile { get; set; }
        public string Plan { get; set; }
        public string Guard { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? FinishedUtc { get; set; }
        public string Result { get; set; }
        public Dictionary<string, string> Assemblies { get; set; } = new Dictionary<string, string>();
        public List<TestHostStep> Steps { get; set; } = new List<TestHostStep>();
        public List<DialogEntry> Dialogs { get; set; } = new List<DialogEntry>();
        public List<string> Failures { get; set; } = new List<string>();
        public List<LinkOpening> LinkOpenings { get; set; } = new List<LinkOpening>();
        public List<string> Notes { get; set; } = new List<string>();
    }

    public class TestHostStep
    {
        public string Name { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public double? Seconds { get; set; }
        public double LimitMinutes { get; set; }
        // ok, failed, timeout or skipped
        public string Result { get; set; }
        public string Detail { get; set; }
        public string Exception { get; set; }
    }

    public class DialogEntry
    {
        public DateTime Utc { get; set; }
        public string Kind { get; set; }
        public string DialogId { get; set; }
        public string Message { get; set; }
        public int? Answer { get; set; }
        public bool Accepted { get; set; }
        public string Step { get; set; }
    }

    public class LinkOpening
    {
        public string Path { get; set; }
        public DateTime OpeningUtc { get; set; }
        public DateTime? OpenedUtc { get; set; }
        public double? Seconds { get; set; }
        public string Step { get; set; }
    }

    // One step of the job. Always steps run even after an earlier step failed, such as closing the document
    public sealed class JobStep
    {
        public string Name;
        public double LimitMinutes;
        public bool Always;
        public Func<string> Action;
    }

    // R104. Runs the steps in order and logs each one. A failed step skips the rest except the Always steps.
    // The watchdog kills the process when a step or the whole job runs over its limit, after it has written the log
    public sealed class TestHostRunner
    {
        private readonly object gate = new object();
        private readonly TestHostLog log;
        private readonly Action<TestHostLog> save;
        private readonly Func<DateTime> clock;
        private TestHostStep current;

        public TestHostRunner(TestHostLog log, Action<TestHostLog> save, Func<DateTime> clock)
        {
            this.log = log;
            this.save = save;
            this.clock = clock;
        }

        public string CurrentStep
        {
            get
            {
                lock (gate)
                {
                    return current == null ? null : current.Name;
                }
            }
        }

        // Event handlers on other threads and the watchdog add to the log through here
        public void Record(Action<TestHostLog> change)
        {
            lock (gate)
            {
                change(log);
                Save();
            }
        }

        public bool Run(IEnumerable<JobStep> steps)
        {
            bool failed = false;
            foreach (JobStep step in steps)
            {
                var entry = new TestHostStep { Name = step.Name, StartedUtc = clock(), LimitMinutes = step.LimitMinutes };
                lock (gate)
                {
                    log.Steps.Add(entry);
                    if (failed && !step.Always)
                    {
                        entry.Result = "skipped";
                        entry.Detail = "an earlier step failed";
                        entry.EndedUtc = entry.StartedUtc;
                        entry.Seconds = 0;
                        Save();
                        continue;
                    }
                    current = entry;
                    Save();
                }
                string detail = null;
                string error = null;
                try
                {
                    detail = step.Action();
                }
                catch (Exception failure)
                {
                    error = failure.ToString();
                }
                lock (gate)
                {
                    current = null;
                    entry.EndedUtc = clock();
                    entry.Seconds = Math.Round((entry.EndedUtc.Value - entry.StartedUtc).TotalSeconds, 1);
                    entry.Detail = detail;
                    if (error != null)
                    {
                        entry.Result = "failed";
                        entry.Exception = error;
                        failed = true;
                    }
                    else
                    {
                        entry.Result = "ok";
                    }
                    Save();
                }
            }
            lock (gate)
            {
                log.Result = failed ? "failed" : "ok";
                log.FinishedUtc = clock();
                Save();
            }
            return !failed;
        }

        // What the watchdog writes before it kills the process. Null when nothing ran over
        public string Overrun(DateTime nowUtc, DateTime jobDeadlineUtc)
        {
            lock (gate)
            {
                string why = null;
                if (current != null && (nowUtc - current.StartedUtc).TotalMinutes > current.LimitMinutes)
                {
                    why = "step " + current.Name + " ran over its limit of " + current.LimitMinutes + " minutes";
                }
                else if (nowUtc > jobDeadlineUtc && current == null && log.FinishedUtc.HasValue)
                {
                    // The job ended, only the exit of Revit is late, so the result of the job stays
                    log.Notes.Add("Revit did not exit in time after the job, the test host stopped Revit");
                    Save();
                    return "Revit did not exit in time";
                }
                else if (nowUtc > jobDeadlineUtc)
                {
                    why = "the job ran over its total limit" + (current == null ? "" : ", in step " + current.Name);
                }
                if (why == null)
                {
                    return null;
                }
                if (current != null)
                {
                    current.Result = "timeout";
                    current.EndedUtc = nowUtc;
                    current.Seconds = Math.Round((nowUtc - current.StartedUtc).TotalSeconds, 1);
                    current.Detail = why;
                }
                log.Result = "timeout";
                log.FinishedUtc = nowUtc;
                log.Notes.Add(why + ", the test host stopped Revit");
                Save();
                return why;
            }
        }

        private void Save()
        {
            try
            {
                save(log);
            }
            catch (Exception failure)
            {
                // A log that cannot be written must not stop the job, the next save tries again
                Trace.WriteLine("test host log not written: " + failure.Message);
            }
        }
    }

    // Looks at the runner on its own thread and kills the process on an overrun, since a Revit call cannot be interrupted
    public sealed class TestHostWatchdog : IDisposable
    {
        private readonly Timer timer;
        private readonly TestHostRunner runner;
        private readonly Func<DateTime> clock;
        private readonly Action<string> kill;
        private DateTime deadlineUtc;
        private int fired;

        public TestHostWatchdog(TestHostRunner runner, DateTime deadlineUtc, Func<DateTime> clock, Action<string> kill, TimeSpan period)
        {
            this.runner = runner;
            this.deadlineUtc = deadlineUtc;
            this.clock = clock;
            this.kill = kill;
            timer = new Timer(_ => Look(), null, period, period);
        }

        // After the job the deadline moves to the time Revit has to exit in
        public void MoveDeadline(DateTime deadline)
        {
            deadlineUtc = deadline;
        }

        public void Look()
        {
            string why = runner.Overrun(clock(), deadlineUtc);
            if (why != null && Interlocked.Exchange(ref fired, 1) == 0)
            {
                kill(why);
            }
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }

    // How the test host answers a Revit dialog it cannot leave open. The answers are tried in order until Revit takes one
    public static class DialogAnswers
    {
        public const int Ok = 1;
        public const int Cancel = 2;
        public const int Yes = 6;
        public const int No = 7;
        public const int Close = 8;
        public const int FirstLink = 1001;
        public const int SecondLink = 1002;

        private static readonly Dictionary<string, int[]> Known = new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            // Ignore and continue opening the project, so a missing link never stops the open
            { "TaskDialog_Unresolved_References", new[] { SecondLink, Close } },
            // Close the document without saving, a save is always explicit in the job
            { "TaskDialog_Save_File", new[] { No, Close } },
            { "TaskDialog_Changes_Not_Saved", new[] { No, Close } },
        };

        public static int[] For(string kind, string dialogId)
        {
            int[] answers;
            if (dialogId != null && Known.TryGetValue(dialogId, out answers))
            {
                return answers;
            }
            // Never a command link for a dialog that is not listed, a link can write a file such as the central or a link source.
            // OK comes last, Revit only takes it on a box that has nothing else, which only tells
            switch (kind)
            {
                case "taskDialog":
                    return new[] { Close, Cancel, Ok };
                case "messageBox":
                    return new[] { Cancel, No, Ok };
                default:
                    return new[] { Cancel };
            }
        }

        // R104. The first answer Revit takes, or null when it takes none
        public static int? Answer(string kind, string dialogId, Func<int, bool> offer)
        {
            return For(kind, dialogId).Select(code => (int?)code).FirstOrDefault(code => offer(code.Value));
        }
    }
}
