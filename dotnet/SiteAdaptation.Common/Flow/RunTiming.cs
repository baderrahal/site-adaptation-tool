using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Flow
{
    // R447. Where one button run spends its time, so a speed fix rests on numbers. A step is a phase of the flow of R382, one clock runs
    // through them, and a measure inside a step takes its time out of whatever it sits in, so a commit does not also count the
    // preprocessor Revit calls inside it and each step adds up to its total. Nothing here may fail or change a button: every use of the
    // clock and every write is guarded, and a failure only leaves the timing short
    public sealed class RunTiming
    {
        public const string RevitStart = "revitStart";
        public const string ModelOpen = "modelOpen";
        public const string Reading = "reading";
        public const string Transactions = "transactions";
        public const string ModelRefresh = "modelRefresh";
        public const string Warnings = "warnings";
        public const string Saving = "saving";
        public const string WorkerWait = "workerWait";
        public const string Other = "other";
        public const string Absent = "absent";
        public const string StartStep = "start";
        public const int FormatVersion = 1;

        // Before the run id of the flow nothing is written, so this first step is the time up to the first step with a file to go in
        internal const string PrepareStep = "prepare";
        internal const string HashesStep = "hashes";
        internal const string PreviewStep = "preview";
        internal const string ConfirmStep = "confirm";
        internal const string HashesAgainStep = "hashesAgain";
        internal const string WorkerStartStep = "workerStart";
        internal const string WorkerStep = "worker";
        internal const string WorkerEndStep = "workerEnd";
        internal const string WindowStep = "window";
        internal const string OutsideStep = "outside";
        // R447. The parts of the outside step of 7 Place villas, which is most of its time
        internal const string VillaDecideStep = "villaDecide";
        internal const string VillaMassFilesStep = "villaMassFiles";
        internal const string VillaLoadTypesStep = "villaLoadTypes";
        internal const string ApplyStep = "apply";
        internal const string ReadBackStep = "readBack";
        internal const string CommitStep = "commit";
        internal const string OutputsStep = "outputs";
        internal const string OutsideEndStep = "outsideEnd";
        internal const string Threw = "Threw";
        internal const string StartedEnd = "Started";

        private static readonly string[] Categories = { RevitStart, ModelOpen, Reading, Transactions, ModelRefresh, Warnings, Saving, WorkerWait, Other };
        private static readonly object pendingGate = new object();
        private static double? pendingRevitStart;
        private static double? pendingModelOpen;

        // One thread per run while the flow holds it, the API thread of Revit. A second button may run while the worker of the first
        // goes on, so a Regenerate must reach the run on the stack, never the run whose worker waits
        [ThreadStatic]
        private static RunTiming current;

        private readonly object gate = new object();
        private readonly Func<double> clock;
        private readonly List<Step> steps = new List<Step>();
        private readonly Stack<string> open = new Stack<string>();
        private double mark;
        private double? revitStart;
        private double? modelOpen;
        private int button;
        private string label;
        private string runId;
        private string workFolder;
        private bool finished;

        public RunTiming()
            : this(Stopwatch.StartNew())
        {
        }

        private RunTiming(Stopwatch watch)
            : this(() => watch.Elapsed.TotalSeconds)
        {
        }

        // seconds gives the time on one clock, the tests give their own
        public RunTiming(Func<double> seconds)
        {
            clock = seconds;
            Safely(() =>
            {
                mark = clock();
                steps.Add(new Step(PrepareStep, null));
            });
        }

        public static string JsonName(string runId)
        {
            return "run_timing_" + runId + ".json";
        }

        public static string LogName(string runId)
        {
            return "run_" + runId + ".log";
        }

        // What a caller measured before the run, null where nothing measured it. Held for the next run that has a file to write, so a
        // sum over the runs of a job counts one open once. A later call keeps what an earlier call gave
        public static void BeforeRun(double? revitStartSeconds, double? modelOpenSeconds)
        {
            lock (pendingGate)
            {
                pendingRevitStart = revitStartSeconds ?? pendingRevitStart;
                pendingModelOpen = modelOpenSeconds ?? pendingModelOpen;
            }
        }

        internal static void TakeBeforeRun(out double? revitStartSeconds, out double? modelOpenSeconds)
        {
            lock (pendingGate)
            {
                revitStartSeconds = pendingRevitStart;
                modelOpenSeconds = pendingModelOpen;
                pendingRevitStart = null;
                pendingModelOpen = null;
            }
        }

        // The Revit side reports here with no argument passed down through the documents. Outside a run it only runs the work, and a
        // failure of the work goes on to its caller as it would with no measure
        public static void Measure(string category, Action work)
        {
            RunTiming timing = current;
            timing?.Open(category);
            try
            {
                work();
            }
            finally
            {
                timing?.Close();
            }
        }

        public static T Measure<T>(string category, Func<T> work)
        {
            RunTiming timing = current;
            timing?.Open(category);
            try
            {
                return work();
            }
            finally
            {
                timing?.Close();
            }
        }

        // Sets this run as the one a Measure reports to until the scope ends, then the one before it
        internal IDisposable Enter()
        {
            RunTiming before = current;
            current = this;
            return new Scope(before);
        }

        // The run id and the work folder of the flow. A run stopped before them has nowhere to write and writes nothing
        internal void Begin(int number, string buttonLabel, string id, string folder)
        {
            Safely(() =>
            {
                button = number;
                label = buttonLabel;
                runId = id;
                workFolder = folder;
                TakeBeforeRun(out revitStart, out modelOpen);
            });
        }

        // Ends the step that runs and starts the next. What the new step spends outside any measure goes to category, or with none
        // to the remainder other. A name may come again later in a run, as the worker of the scan and then of the read of button 2.
        // The step that runs under the name asked goes on, so no step of no time is written for a body that said so first
        internal void NextStep(string name, string category = null)
        {
            Safely(() =>
            {
                if (!finished && steps[steps.Count - 1].Name != name)
                {
                    Charge();
                    steps.Add(new Step(name, category));
                }
            });
        }

        // The run so far, for a worker that may never end: Revit closed or the process killed still leaves the steps before it
        internal void WriteSoFar()
        {
            Safely(() =>
            {
                if (!finished)
                {
                    Charge();
                    Write(StartedEnd);
                }
            });
        }

        // The end of the run, written once. A later call from the same run changes nothing
        internal void Finish(string end)
        {
            Safely(() =>
            {
                if (!finished)
                {
                    Charge();
                    finished = true;
                    Write(end);
                }
            });
        }

        private void Open(string category)
        {
            Safely(() =>
            {
                if (!finished)
                {
                    Charge();
                    open.Push(category);
                }
            });
        }

        private void Close()
        {
            Safely(() =>
            {
                if (!finished && open.Count > 0)
                {
                    Charge();
                    open.Pop();
                }
            });
        }

        private void Charge()
        {
            double now = clock();
            double spent = now - mark;
            mark = now;
            Step step = steps[steps.Count - 1];
            step.Total += spent;
            string category = open.Count > 0 ? open.Peek() : step.Category;
            if (category != null)
            {
                step.Seconds.TryGetValue(category, out double before);
                step.Seconds[category] = before + spent;
            }
        }

        // No write of a step report may stop or change a button, whatever throws: the flow goes on as if it had no timing
        private void Safely(Action action)
        {
            try
            {
                lock (gate)
                {
                    action();
                }
            }
            catch (Exception failure)
            {
                Trace.WriteLine("run timing: " + failure.Message);
            }
        }

        private void Write(string end)
        {
            if (runId == null)
            {
                return;
            }
            List<Row> rows = Rows();
            Safely(() => WorkFiles.WriteText(Path.Combine(workFolder, JsonName(runId)), runId, Json(end, rows).ToString(Formatting.Indented)));
            Safely(() => WorkFiles.WriteText(Path.Combine(workFolder, LogName(runId)), runId, string.Concat(rows.Select(row => Line(runId, row) + "\n"))));
        }

        // Whole milliseconds, so what the file says adds up exactly: other is what the rounded categories leave of the rounded total
        private List<Row> Rows()
        {
            var rows = new List<Row> { StartRow() };
            foreach (Step step in steps)
            {
                var row = new Row(step.Name);
                foreach (KeyValuePair<string, double> seconds in step.Seconds)
                {
                    row.Milliseconds[seconds.Key] = Milliseconds(seconds.Value);
                }
                long categories = row.Milliseconds.Values.Sum(value => value);
                row.Milliseconds[Other] = Math.Max(0, Milliseconds(step.Total) - categories);
                row.Total = categories + row.Milliseconds[Other];
                rows.Add(row);
            }
            return rows;
        }

        // Never 0 for what no caller measured: with neither given the whole row says absent
        private Row StartRow()
        {
            var row = new Row(StartStep);
            if (revitStart.HasValue)
            {
                row.Milliseconds[RevitStart] = Milliseconds(revitStart.Value);
            }
            if (modelOpen.HasValue)
            {
                row.Milliseconds[ModelOpen] = Milliseconds(modelOpen.Value);
            }
            if (row.Milliseconds.Count > 0)
            {
                row.Total = row.Milliseconds.Values.Sum(value => value);
                row.Milliseconds[Other] = 0;
            }
            return row;
        }

        private JObject Json(string end, List<Row> rows)
        {
            var categories = new JObject();
            foreach (string category in Categories)
            {
                List<long> given = rows.Where(row => row.Milliseconds.ContainsKey(category)).Select(row => row.Milliseconds[category]).ToList();
                categories[category] = Value(given.Count > 0 ? given.Sum() : (long?)null);
            }
            return new JObject
            {
                ["formatVersion"] = FormatVersion,
                ["runId"] = runId,
                ["button"] = button,
                ["label"] = label,
                ["end"] = end,
                ["totalSeconds"] = Value(rows.Sum(row => row.Total ?? 0)),
                ["categories"] = categories,
                ["steps"] = new JArray(rows.Select(RowJson)),
            };
        }

        private static JObject RowJson(Row row)
        {
            var json = new JObject { ["step"] = row.Name, ["totalSeconds"] = Value(row.Total) };
            foreach (string category in Categories)
            {
                json[category] = Value(row.Of(category));
            }
            return json;
        }

        private static string Line(string runId, Row row)
        {
            IEnumerable<string> parts = Categories.Select(category => category + "=" + Text(row.Of(category)));
            return "timing " + runId + " " + row.Name + " total=" + Text(row.Total) + " " + string.Join(" ", parts);
        }

        private static JToken Value(long? milliseconds)
        {
            return milliseconds.HasValue ? new JValue(milliseconds.Value / 1000.0) : new JValue(Absent);
        }

        private static string Text(long? milliseconds)
        {
            return milliseconds.HasValue ? (milliseconds.Value / 1000.0).ToString("0.000", CultureInfo.InvariantCulture) : Absent;
        }

        private static long Milliseconds(double seconds)
        {
            return (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
        }

        private sealed class Step
        {
            public Step(string name, string category)
            {
                Name = name;
                Category = category;
            }

            public string Name { get; }
            public string Category { get; }
            public double Total { get; set; }
            public Dictionary<string, double> Seconds { get; } = new Dictionary<string, double>();
        }

        private sealed class Row
        {
            public Row(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public long? Total { get; set; }
            public Dictionary<string, long> Milliseconds { get; } = new Dictionary<string, long>();

            public long? Of(string category)
            {
                return Milliseconds.TryGetValue(category, out long value) ? value : (long?)null;
            }
        }

        private sealed class Scope : IDisposable
        {
            private readonly RunTiming before;

            public Scope(RunTiming before)
            {
                this.before = before;
            }

            public void Dispose()
            {
                current = before;
            }
        }
    }
}
