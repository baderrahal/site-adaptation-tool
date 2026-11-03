using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R448. The job folder of the Revit listener with no Revit type. A lane drops a job file, one background thread of the listener
    // claims it, checks it and marks its inputs, the Revit side takes it and only carries it out, the same thread then writes its
    // result. So no file work ever sits on the Revit thread and a Revit that hangs still leaves a beat
    public sealed class RevitListenerSetting
    {
        public const string RootVariable = "SITEADAPT_LISTENER_ROOT";
        public const string SlotVariable = "SITEADAPT_LISTENER_SLOT";
        public const string InputRootsVariable = "SITEADAPT_LISTENER_INPUT_ROOTS";
        public const string MinFreeVariable = "SITEADAPT_LISTENER_MIN_FREE_GB";
        private const char RootSeparator = '|';
        private static readonly Regex SlotText = new Regex(@"\A[1-9][0-9]?\z", RegexOptions.CultureInvariant);

        private RevitListenerSetting(string root, string slot, IReadOnlyList<string> inputRoots, double? minFreeGb)
        {
            Root = root;
            Slot = slot;
            InputRoots = inputRoots;
            MinFreeGb = minFreeGb;
        }

        // siteBuild.minFreeGB of the config as the start script gives it, the floor of R200 before each job
        public double? MinFreeGb { get; }

        public string Root { get; }

        public string Slot { get; }

        public IReadOnlyList<string> InputRoots { get; }

        public string InFolder
        {
            get { return Path.Combine(Root, "in-" + Slot); }
        }

        public string OutFolder
        {
            get { return Path.Combine(Root, "out-" + Slot); }
        }

        public string ClaimedFolder
        {
            get { return Path.Combine(InFolder, "claimed"); }
        }

        // Null with no root or no slot variable, the listener is off and nothing of it runs
        public static RevitListenerSetting Read(Func<string, string> variable)
        {
            string root = variable(RootVariable);
            string slot = variable(SlotVariable);
            if (string.IsNullOrWhiteSpace(root) || slot == null)
            {
                return null;
            }
            string[] inputRoots = (variable(InputRootsVariable) ?? "").Split(new[] { RootSeparator }, StringSplitOptions.RemoveEmptyEntries);
            double? minFree = double.TryParse(variable(MinFreeVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out double floor) ? floor : (double?)null;
            return new RevitListenerSetting(root, slot, inputRoots, minFree);
        }

        public string Refusal()
        {
            if (!SlotText.IsMatch(Slot))
            {
                return "the slot of the listener is not a number from 1 to 99: " + Slot;
            }
            if (!MinFreeGb.HasValue)
            {
                return "the listener has no memory floor in " + MinFreeVariable;
            }
            return InsideRoot(Root, InputRoots);
        }

        // D19. Null when the path lies in no root. Windows matches names in any case, and a folder that only starts with the same letters
        // is a folder of its own
        public static string InsideRoot(string path, IEnumerable<string> roots)
        {
            string full = path.TrimEnd('\\', '/');
            foreach (string root in roots)
            {
                string inputRoot = root.TrimEnd('\\', '/');
                if (string.Equals(full, inputRoot, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(inputRoot + "\\", StringComparison.OrdinalIgnoreCase) || full.StartsWith(inputRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return "the path lies under the input root " + inputRoot + ", a run only reads there (D19): " + path;
                }
            }
            return null;
        }
    }

    public static class RevitListenerFiles
    {
        public const string WritingTail = ".writing";

        // A reader never sees half a file: the text goes to a name no reader looks at, and one move makes it the file
        public static void WriteWhole(string path, string text)
        {
            string writing = path + WritingTail;
            File.WriteAllText(writing, text, new UTF8Encoding(false));
            File.Move(writing, path);
        }

        // S5. The same over a file that is there, so the one result of a job says its final marks. Replace is one step of the file system
        public static void WriteOver(string path, string text)
        {
            string writing = path + WritingTail;
            File.WriteAllText(writing, text, new UTF8Encoding(false));
            File.Replace(writing, path, null);
        }
    }

    // R448. The add-in runs a job of the test host with no window of a person when this variable is set, as a Revit of Run-TestHost.ps1
    // has it from its start. A listener gets its jobs later, so it stands in Revit while a job runs
    public static class RevitListenerJobVariable
    {
        public static IDisposable Set(string job)
        {
            string before = Environment.GetEnvironmentVariable(TestHostGuard.JobVariable);
            Environment.SetEnvironmentVariable(TestHostGuard.JobVariable, job);
            return new Scope(before);
        }

        private sealed class Scope : IDisposable
        {
            private readonly string before;

            public Scope(string before)
            {
                this.before = before;
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(TestHostGuard.JobVariable, before);
            }
        }
    }

    public sealed class RevitListenerClaim
    {
        public string Stem { get; set; }
        public string Text { get; set; }
        // The write time of the job file, which one move keeps: when the send made it appear
        public DateTime ArrivedUtc { get; set; }
        // Why the claimed file could not be read, null when it could
        public string Error { get; set; }
        public DateTime ClaimedUtc { get; set; }
    }

    public sealed class RevitListenerQueue
    {
        public const string JobTail = ".job.json";
        public const string ResultTail = ".result.json";
        public const string LogTail = ".log.json";
        public const string HeartbeatFile = "heartbeat.json";
        public const string ReadyFile = "ready.json";
        public const string ListenerLogFile = "listener_log.json";

        private readonly RevitListenerSetting setting;

        public RevitListenerQueue(RevitListenerSetting setting)
        {
            this.setting = setting;
        }

        public RevitListenerSetting Setting
        {
            get { return setting; }
        }

        public void Prepare()
        {
            Directory.CreateDirectory(setting.ClaimedFolder);
            Directory.CreateDirectory(setting.OutFolder);
        }

        // The oldest job file that can be taken, by its write time and then its name, moved to the claimed folder so that no job runs twice.
        // The move keeps the write time. A file its writer still holds, or another claim took first, is left and the next is tried
        public RevitListenerClaim Next()
        {
            foreach (FileInfo file in Waiting())
            {
                string stem = file.Name.Substring(0, file.Name.Length - JobTail.Length);
                string target = FreeName(setting.ClaimedFolder, stem, JobTail);
                DateTime arrived = file.LastWriteTimeUtc;
                bool link = false;
                try
                {
                    link = (file.Attributes & FileAttributes.ReparsePoint) != 0;
                    File.Move(file.FullName, target);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                RevitListenerClaim claim = Read(stem, target, link);
                claim.ArrivedUtc = arrived;
                return claim;
            }
            return null;
        }

        private IEnumerable<FileInfo> Waiting()
        {
            return new DirectoryInfo(setting.InFolder).GetFiles("*" + JobTail)
                .Where(file => file.Name.EndsWith(JobTail, StringComparison.OrdinalIgnoreCase) && file.Name.Length > JobTail.Length)
                .OrderBy(file => file.LastWriteTimeUtc).ThenBy(file => file.Name, StringComparer.Ordinal).ToList();
        }

        private static RevitListenerClaim Read(string stem, string path, bool link)
        {
            var claim = new RevitListenerClaim { Stem = stem };
            if (link)
            {
                claim.Error = "the job file is a link, a job file is a plain file of the job folder";
                return claim;
            }
            try
            {
                claim.Text = File.ReadAllText(path);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                claim.Error = "the job file could not be read: " + failure.Message;
            }
            return claim;
        }

        // Never over a file of an earlier job: <stem><tail>, then <stem>.2<tail> and on
        private static string FreeName(string folder, string stem, string tail)
        {
            string path = Path.Combine(folder, stem + tail);
            for (int count = 2; File.Exists(path); count++)
            {
                path = Path.Combine(folder, stem + "." + count + tail);
            }
            return path;
        }

        public string ResultPath(string stem)
        {
            return FreeName(setting.OutFolder, stem, ResultTail);
        }
    }

    // R448. The fields of a listener job on top of those of a run job. Run id, button list, model copy and config are what a lane sends,
    // the rest of TestHostJob is what the run needs
    public sealed class RevitListenerJob : TestHostJob
    {
        public string RunId { get; set; }
        public string Config { get; set; }
        // The real files the job reads in place, marked before and after it (D19)
        public List<string> Inputs { get; set; }
    }

    public sealed class RevitListenerCheck
    {
        private static readonly Regex RunIdText = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\z", RegexOptions.CultureInvariant);

        public bool Act { get; private set; }
        public string Reason { get; private set; }
        public RevitListenerJob Job { get; private set; }

        private static RevitListenerCheck Refuse(string reason)
        {
            return new RevitListenerCheck { Reason = reason };
        }

        // The job file reads and names itself, nothing on disk is asked
        public static RevitListenerCheck Read(RevitListenerClaim claim)
        {
            if (claim.Error != null)
            {
                return Refuse(claim.Error);
            }
            RevitListenerJob job;
            try
            {
                job = JsonConvert.DeserializeObject<RevitListenerJob>(claim.Text ?? "", new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc });
            }
            catch (JsonException failure)
            {
                return Refuse("the job file does not read: " + failure.Message);
            }
            if (job == null)
            {
                return Refuse("the job file holds no job");
            }
            if (job.RunId == null || !RunIdText.IsMatch(job.RunId))
            {
                return Refuse("the run id is not 1 to 64 letters, digits, _ or -");
            }
            if (!string.Equals(job.RunId, claim.Stem, StringComparison.Ordinal))
            {
                return Refuse("the job file is named " + claim.Stem + " and the job names the run id " + job.RunId);
            }
            return new RevitListenerCheck { Act = true, Job = job };
        }

        // Every check a run job passes in JobSession.Start, then what a listener job adds. A model copy may be there already, the other
        // files of the run may not. The age of the guard is taken at the arrival of the job file and not now: the send made the run folder
        // and the copy already, so a job that waited in the queue must run. What keeps an old job out is the token of this Revit session
        public static RevitListenerCheck Of(RevitListenerClaim claim, string token, string documents, IReadOnlyList<string> inputRoots,
            Func<string, bool> fileExists, Func<string, bool> folderExists)
        {
            RevitListenerCheck read = Read(claim);
            if (!read.Act)
            {
                return read;
            }
            RevitListenerJob job = read.Job;
            if (job.Kind != TestHostGuard.AddinRunKind)
            {
                return Refuse("a listener job is an " + TestHostGuard.AddinRunKind + " job, not " + (job.Kind ?? TestHostGuard.PlaceKind));
            }
            if (job.Buttons == null)
            {
                return Refuse("a listener job names its buttons");
            }
            GuardResult guard = TestHostGuard.Check(claim.Text, token, claim.ArrivedUtc,
                file => !string.Equals(file, job.Model, StringComparison.OrdinalIgnoreCase) && fileExists(file), folderExists);
            if (!guard.Act)
            {
                return Refuse(guard.Reason);
            }
            string reason = AddinRunFiles.CheckPlace(job.Model, documents) ?? CheckFiles(job, inputRoots, fileExists) ?? CheckInputs(job) ?? FileIdentity.CheckTargetFolder(Path.GetDirectoryName(job.Model));
            if (reason == null && fileExists(job.Model))
            {
                reason = FileIdentity.CheckCopy(job.Model, TestHostGuard.OriginalOf(job.Model));
            }
            return reason == null ? read : Refuse(reason);
        }

        private static string CheckFiles(RevitListenerJob job, IReadOnlyList<string> inputRoots, Func<string, bool> fileExists)
        {
            // ButtonsRunHost reads the config of the run folder, so a config anywhere else would be named here and not be the one that runs
            string wanted = Path.Combine(Path.GetDirectoryName(job.Model), "config", "project.json");
            if (!string.Equals(job.Config, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return "the config of a listener job is the copy of its run folder, " + wanted;
            }
            foreach (string written in new[] { job.Model, job.Config, job.PaneSettings })
            {
                string inside = RevitListenerSetting.InsideRoot(written, inputRoots);
                if (inside != null)
                {
                    return inside;
                }
            }
            return !fileExists(job.Config) ? "the config does not exist: " + job.Config
                : !fileExists(job.PaneSettings) ? "the pane settings do not exist: " + job.PaneSettings : null;
        }

        private static string CheckInputs(RevitListenerJob job)
        {
            return job.Inputs != null && job.Inputs.Any(input => !TestHostGuard.IsPlainFullPath(input)) ? "an input is not a plain full path" : null;
        }
    }

    // One job from its claim to its result. Only the loop writes these, under its lock
    public sealed class RevitListenerRun
    {
        internal RevitListenerRun(RevitListenerClaim claim, RevitListenerJob job)
        {
            Claim = claim;
            Job = job;
        }

        public RevitListenerClaim Claim { get; }
        public RevitListenerJob Job { get; }
        internal List<InputMark> Before { get; set; }
        internal double? FreeGb { get; set; }
        internal DateTime? StartedUtc { get; set; }
        internal DateTime? EndedUtc { get; set; }
        internal string Outcome { get; set; }
        internal string Error { get; set; }
        // Revit is out of the job. Past its limit the outcome comes first and this later, so the marks of that first result are not final
        internal bool RevitDone { get; set; }
        internal DateTime? RevitDoneUtc { get; set; }
        internal RevitListenerResult Written { get; set; }
        internal string WrittenPath { get; set; }
    }

    // S3. What buttons_result.json of a run says that the steps of the job do not: the run went to its end and these did not hold. A
    // result with any of them is findings and never ok, the job did not fail and its outputs are there, but a lane must see them
    public sealed class RevitListenerFindings
    {
        public int NamedStopped { get; set; }
        public int VillasNotRead { get; set; }
        public int NotHeld { get; set; }
        public int SaveErrors { get; set; }
        public int Unreadable { get; set; }

        [JsonIgnore]
        public bool Any
        {
            get { return NamedStopped + VillasNotRead + NotHeld + SaveErrors + Unreadable > 0; }
        }

        // Null when the run folder holds no result file, a job that stopped before its write step has failed already
        public static RevitListenerFindings Read(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var findings = new RevitListenerFindings();
            try
            {
                JToken steps = JObject.Parse(File.ReadAllText(path))["steps"];
                foreach (JToken step in steps == null ? Enumerable.Empty<JToken>() : steps.Children())
                {
                    findings.NamedStopped += step["command"]?.Type == JTokenType.String && (string)step["outcome"] == ButtonOutcomes.Stopped ? 1 : 0;
                    findings.VillasNotRead += step["villasNotRead"] != null ? 1 : 0;
                    findings.NotHeld += step["notHeld"]?.Value<int>() ?? 0;
                    findings.SaveErrors += step["savedAfter"]?.Type == JTokenType.String ? 1 : 0;
                }
            }
            catch (Exception failure) when (failure is JsonException || failure is IOException || failure is UnauthorizedAccessException || failure is FormatException || failure is InvalidCastException)
            {
                findings.Unreadable = 1;
            }
            return findings;
        }
    }

    // What the out folder holds of one job, counts and seconds and file names, no value of a model
    public sealed class RevitListenerResult
    {
        public const string Ok = "ok";
        public const string Failed = "failed";
        public const string Timeout = "timeout";
        public const string Refused = "refused";
        public const string WithFindings = "findings";

        public string RunId { get; set; }
        public string Result { get; set; }
        public string Error { get; set; }
        public DateTime? ArrivedUtc { get; set; }
        public DateTime? ClaimedUtc { get; set; }
        public DateTime? StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        // From the arrival of the job file to its start, or to its refusal for a job that never starts
        public double? QueueSeconds { get; set; }
        public double? Seconds { get; set; }
        public DateTime? RevitDoneUtc { get; set; }
        public RevitListenerFindings Findings { get; set; }
        public double? FreeGbAtStart { get; set; }
        // False while Revit may still be in a job past its limit, the marks after are taken again when it comes out and this file written over
        public bool? MarksFinal { get; set; }
        public string RunFolder { get; set; }
        public string Timing { get; set; }
        public string TimingNote { get; set; }
        public List<InputMark> InputsBefore { get; set; }
        public List<InputMark> InputsAfter { get; set; }
        public List<string> InputsChanged { get; set; }

        // The failed steps of a job log, each by its name and the first line of what it threw. The rest stays in the log of the job
        public static string ErrorOf(TestHostLog log)
        {
            var lines = new List<string>();
            foreach (TestHostStep step in log.Steps.Where(each => each.Result == Failed || each.Result == Timeout))
            {
                string text = step.Exception ?? step.Detail ?? step.Result;
                lines.Add(step.Name + ": " + text.Split('\r', '\n')[0]);
            }
            return lines.Count == 0 ? null : string.Join(" | ", lines);
        }
    }

    public static class RevitListenerSteps
    {
        // R448. Past its limit a job runs only the steps that always run, so the write step still puts the add-in back. Each step is asked
        // for by the enumerator and only run by the caller, so a step left out here does nothing
        public static IEnumerable<JobStep> Until(IEnumerable<JobStep> steps, Func<bool> stop)
        {
            foreach (JobStep step in steps)
            {
                if (step.Always || !stop())
                {
                    yield return step;
                }
            }
        }
    }

    // R448. The queue as one background thread runs it. Look is that thread and only that thread, Take and Waiting the Revit thread, End
    // either the Revit thread or the watchdog, and the first End of a run counts. A job past its limit gets its result at once while
    // Revit may still be in the call, the next job waits until Revit is back and takes it
    public sealed class RevitListenerLoop
    {
        public const double BeatSeconds = 10;
        private const string TimingPattern = "run_timing_*.json";

        private readonly object gate = new object();
        private readonly RevitListenerQueue queue;
        private readonly Func<RevitListenerClaim, RevitListenerCheck> check;
        private readonly Func<DateTime> clock;
        private readonly Func<double?> freeGb;
        private readonly Func<string, InputMark> mark;
        private readonly object beatGate = new object();
        // Results written while Revit was still in the job, each to be marked again when it comes out. The next job waits for that, so what
        // it does is never counted for the one before
        private readonly List<RevitListenerRun> notFinal = new List<RevitListenerRun>();
        private RevitListenerRun current;
        private RevitListenerRun waiting;
        private string marking;
        private DateTime? lastBeat;
        private int done;

        public RevitListenerLoop(RevitListenerQueue queue, Func<RevitListenerClaim, RevitListenerCheck> check, Func<DateTime> clock, Func<double?> freeGb = null, Func<string, InputMark> mark = null)
        {
            this.queue = queue;
            this.check = check;
            this.clock = clock;
            this.freeGb = freeGb;
            this.mark = mark ?? InputGuard.Mark;
        }

        // The step Revit is in now, for the heartbeat
        public Func<string> Step { get; set; }

        public RevitListenerRun Waiting
        {
            get
            {
                lock (gate)
                {
                    return notFinal.Count > 0 ? null : waiting;
                }
            }
        }

        public RevitListenerRun Take()
        {
            lock (gate)
            {
                if (notFinal.Count > 0)
                {
                    return null;
                }
                RevitListenerRun run = waiting;
                waiting = null;
                if (run != null)
                {
                    run.StartedUtc = clock();
                }
                return run;
            }
        }

        // Revit is out of the job. The first outcome of a run counts, so a watchdog and a Revit that came back never give two
        public void End(RevitListenerRun run, string outcome, string error)
        {
            lock (gate)
            {
                Outcome(run, outcome, error);
                run.RevitDone = true;
                run.RevitDoneUtc = clock();
            }
        }

        // On the thread of the watchdog while Revit may still be in a call, the lane gets its answer now and Revit is not done
        public void PastLimit(RevitListenerRun run, string why)
        {
            lock (gate)
            {
                Outcome(run, RevitListenerResult.Timeout, why);
            }
        }

        private void Outcome(RevitListenerRun run, string outcome, string error)
        {
            if (run.Outcome == null)
            {
                run.Outcome = outcome;
                run.Error = error;
                run.EndedUtc = clock();
            }
        }

        public void Look()
        {
            Beat();
            MarkAgain();
            RevitListenerRun run;
            lock (gate)
            {
                run = current;
                if (run != null && run.Outcome == null)
                {
                    return;
                }
            }
            if (run != null)
            {
                bool final = WriteResult(run);
                lock (gate)
                {
                    current = null;
                    done++;
                    if (!final)
                    {
                        notFinal.Add(run);
                    }
                }
            }
            ClaimNext();
        }

        // S5, D19. Revit came out of a job past its limit: the marks again, what it wrote while it ran over is reported in the one result
        private void MarkAgain()
        {
            List<RevitListenerRun> back;
            lock (gate)
            {
                back = notFinal.Where(run => run.RevitDone).ToList();
            }
            foreach (RevitListenerRun run in back)
            {
                RevitListenerResult result = run.Written;
                string outcome = result.Result;
                string error = result.Error;
                lock (gate)
                {
                    result.RevitDoneUtc = run.RevitDoneUtc;
                }
                AfterMarks(run, result, ref outcome, ref error);
                Finals(result, ref outcome);
                result.Result = outcome;
                result.Error = error;
                result.MarksFinal = true;
                RevitListenerFiles.WriteOver(run.WrittenPath, JsonOut.ToJsonUnrounded(result));
                lock (gate)
                {
                    notFinal.Remove(run);
                }
            }
        }

        // Read only when Revit is out of the job, before that the file is not at its end
        private static void Finals(RevitListenerResult result, ref string outcome)
        {
            result.Findings = RevitListenerFindings.Read(Path.Combine(result.RunFolder ?? "", ButtonsRun.ResultFile));
            if (result.Findings != null && result.Findings.Any && outcome == RevitListenerResult.Ok)
            {
                outcome = RevitListenerResult.WithFindings;
            }
        }

        private void ClaimNext()
        {
            RevitListenerClaim claim;
            while ((claim = queue.Next()) != null)
            {
                claim.ClaimedUtc = clock();
                RevitListenerCheck checkedJob = Checked(claim);
                if (!checkedJob.Act)
                {
                    WriteRefusal(claim, checkedJob.Reason);
                    continue;
                }
                var run = new RevitListenerRun(claim, checkedJob.Job);
                lock (beatGate)
                {
                    marking = claim.Stem;
                }
                try
                {
                    run.Before = Marks(run.Job);
                }
                catch (Exception failure)
                {
                    WriteRefusal(claim, "an input could not be marked (D19): " + failure.Message);
                    continue;
                }
                finally
                {
                    lock (beatGate)
                    {
                        marking = null;
                    }
                }
                // L5. The floor of R200 is looked at last, nearest the start of the job, and only here since a window lock looks once
                double? free = freeGb?.Invoke();
                if (free.HasValue && queue.Setting.MinFreeGb.HasValue && free.Value < queue.Setting.MinFreeGb.Value)
                {
                    WriteRefusal(claim, "free memory " + free.Value.ToString("0.0", CultureInfo.InvariantCulture) + " GB is under siteBuild.minFreeGB " + queue.Setting.MinFreeGb.Value.ToString("0.0", CultureInfo.InvariantCulture) + " GB, the job did not start");
                    continue;
                }
                run.FreeGb = free;
                lock (gate)
                {
                    current = run;
                    waiting = run;
                }
                return;
            }
        }

        // A check can refuse and never throw, so one job file never stops the queue
        private RevitListenerCheck Checked(RevitListenerClaim claim)
        {
            try
            {
                return check(claim);
            }
            catch (Exception failure)
            {
                return RevitListenerCheck.Read(new RevitListenerClaim { Error = "the job could not be checked: " + failure.GetType().Name + ": " + failure.Message });
            }
        }

        private List<InputMark> Marks(RevitListenerJob job)
        {
            return (job.Inputs ?? new List<string>()).Select(mark).ToList();
        }

        private void WriteRefusal(RevitListenerClaim claim, string reason)
        {
            WriteJson(claim.Stem, new RevitListenerResult
            {
                RunId = claim.Stem,
                Result = RevitListenerResult.Refused,
                Error = reason,
                ArrivedUtc = claim.ArrivedUtc,
                ClaimedUtc = claim.ClaimedUtc,
                FinishedUtc = clock(),
                QueueSeconds = Waited(claim, clock()),
            });
        }

        // From the send, one meaning on every path. A clock a little behind the file time is no wait
        private static double Waited(RevitListenerClaim claim, DateTime until)
        {
            return Rounding.Fixed(Math.Max(0, (until - claim.ArrivedUtc).TotalSeconds), 1);
        }

        // True when the marks of this result are final
        private bool WriteResult(RevitListenerRun run)
        {
            string outcome;
            string error;
            DateTime? ended;
            bool final;
            lock (gate)
            {
                outcome = run.Outcome;
                error = run.Error;
                ended = run.EndedUtc;
                final = run.RevitDone;
            }
            var result = new RevitListenerResult
            {
                RunId = run.Job.RunId,
                ArrivedUtc = run.Claim.ArrivedUtc,
                ClaimedUtc = run.Claim.ClaimedUtc,
                RevitDoneUtc = run.RevitDoneUtc,
                FreeGbAtStart = run.FreeGb,
                MarksFinal = final,
                StartedUtc = run.StartedUtc,
                FinishedUtc = clock(),
                QueueSeconds = run.StartedUtc.HasValue ? Waited(run.Claim, run.StartedUtc.Value) : (double?)null,
                Seconds = run.StartedUtc.HasValue && ended.HasValue ? Rounding.Fixed((ended.Value - run.StartedUtc.Value).TotalSeconds, 1) : (double?)null,
                RunFolder = Path.GetDirectoryName(run.Job.Model),
                InputsBefore = run.Before,
            };
            AfterMarks(run, result, ref outcome, ref error);
            if (final)
            {
                Finals(result, ref outcome);
            }
            Timing(result);
            result.Result = outcome;
            result.Error = error;
            run.Written = result;
            run.WrittenPath = WriteJson(run.Claim.Stem, result);
            return final;
        }

        // D19. An input that changed fails the job whatever it did, as the run script does at the end of a run
        private void AfterMarks(RevitListenerRun run, RevitListenerResult result, ref string outcome, ref string error)
        {
            string changedText = null;
            try
            {
                result.InputsAfter = Marks(run.Job);
                result.InputsChanged = run.Before.Zip(result.InputsAfter, (before, after) => InputGuard.Change(before, after) == null ? null : before.Name)
                    .Where(name => name != null).ToList();
                if (result.InputsChanged.Count > 0)
                {
                    changedText = "a real input of the job changed (D19): " + string.Join(", ", result.InputsChanged);
                }
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                changedText = "an input could not be marked after the job (D19): " + failure.Message;
            }
            if (changedText != null)
            {
                error = error == null ? changedText : error + " | " + changedText;
                if (outcome == RevitListenerResult.Ok)
                {
                    outcome = RevitListenerResult.Failed;
                }
            }
        }

        // Copied whatever its shape, the run folder of a job is its own. A copy that fails is a note, the result still goes out
        private void Timing(RevitListenerResult result)
        {
            try
            {
                string[] files = Directory.Exists(result.RunFolder) ? Directory.GetFiles(result.RunFolder, TimingPattern, SearchOption.AllDirectories) : new string[0];
                if (files.Length == 0)
                {
                    result.TimingNote = "no run_timing file in the run folder";
                    return;
                }
                foreach (string file in files)
                {
                    File.Copy(file, Path.Combine(queue.Setting.OutFolder, Path.GetFileName(file)), true);
                }
                result.Timing = string.Join(", ", files.Select(Path.GetFileName));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is ArgumentException)
            {
                result.TimingNote = "the run_timing file was not copied: " + failure.Message;
            }
        }

        // Unrounded, a bad job has no config and so no design values, and a result holds only counts and seconds
        private string WriteJson(string stem, RevitListenerResult result)
        {
            string path = queue.ResultPath(stem);
            RevitListenerFiles.WriteWhole(path, JsonOut.ToJsonUnrounded(result));
            return path;
        }

        // The write time is what the freeze guard of the send script reads, a new file each time as JobSession.Beat writes it. L6. Look
        // beats, and the thread of RevitListener.Beats too, since hashing the inputs takes as long as they are large and sits in one Look.
        // No lock of the queue is held while a file is hashed, so a beat never waits for that
        public void Beat()
        {
            lock (beatGate)
            {
                DateTime now = clock();
                if (lastBeat.HasValue && (now - lastBeat.Value).TotalSeconds < BeatSeconds)
                {
                    return;
                }
                lastBeat = now;
                RevitListenerRun run;
                int finished;
                lock (gate)
                {
                    run = current;
                    finished = done;
                }
                bool running = run != null && run.StartedUtc.HasValue;
                string text = new JObject
                {
                    { "utc", now },
                    { "pid", Process.GetCurrentProcess().Id },
                    { "slot", queue.Setting.Slot },
                    { "state", marking != null ? "marking" : running ? "running" : run != null ? "queued" : "idle" },
                    { "runId", marking ?? run?.Job.RunId },
                    { "step", running ? Step?.Invoke() : null },
                    { "jobsDone", finished },
                }.ToString();
                string beat = Path.Combine(queue.Setting.OutFolder, RevitListenerQueue.HeartbeatFile);
                string next = beat + ".next";
                File.WriteAllText(next, text);
                File.Copy(next, beat, true);
                File.Delete(next);
            }
        }
    }
}
