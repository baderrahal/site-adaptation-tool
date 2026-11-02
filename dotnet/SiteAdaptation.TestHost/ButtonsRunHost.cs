using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ManualRules;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Update;

namespace SiteAdaptation.TestHost
{
    // Lane B 5c, the Revit run after 5c (R384). The buttons of the job run on one copy as the ribbon runs them, Buttons.Run of the loaded
    // add-in by reflection, with windows that answer by themselves: Set up site as setUp answers it, Review data marks and saves
    // nothing, the check table keeps every Use as it opens and saves nothing, and the confirm of the flow and the link types to load
    // take their first link while the step runs. 2 and 4 return once their worker started, so the session waits on its Idling clock
    // until the add-in handed the end on, then the next phase goes on. After 7 each placed solid and link is read by the Revit API
    // here, a route of its own apart from the read back of the add-in. The named commands of the job run after the buttons by Buttons.Run
    // of their names, Site Adaptation Rules opens and closes, Update from report reads the two Excel files of the run and applies nothing.
    // Everything goes into buttons_result.json of the run folder. With shots true (6a) each window is drawn to PNG under shots of the run
    // folder before its answer, the pane after 1 and at the end, the ribbon, New project and How it works at the end (ButtonShots)
    internal sealed class ButtonsRunHost
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        // What a worker end may take past the time limit of its worker, for the exit and the ExternalEvent. A tool timing
        private const double WaitMarginSeconds = 60;
        // A face of a solid whose normal points this far down is its base. A tool tolerance
        private const double DownNormal = 0.999;
        // The texts of LinkLoadDialog of the add-in and of the lines PlaceVillasRun gives it
        private static readonly string[] LinkLoadTexts = { "Link types to load", "Load clears the Undo list of Revit." };

        private readonly TestHostJob job;
        private readonly TestHostRunner runner;
        private readonly string folder;
        private readonly string work;
        private readonly List<ButtonsStep> steps;
        private readonly HostState state;
        private readonly JObject result = new JObject();
        private readonly JArray entries = new JArray();
        private readonly JArray shots = new JArray();
        private readonly List<string> shotNotes = new List<string>();
        private UIApplication ui;
        private AddinProbe probe;
        private Assembly addIn;
        private Document document;
        private JObject config;
        private object session;
        private int next;
        private bool stopped;
        private bool swapped;
        private object stateBefore;
        private object setUpBefore;
        private object reviewBefore;
        private object checkBefore;
        private object updateBefore;
        private object rulesBefore;
        private bool answered;
        private JObject current;
        private JObject waiting;
        private ButtonsStep waitingStep;
        private int commitsBefore;
        private int dialogsBefore;
        private DateTime startedUtc;
        private Dictionary<string, bool> before;
        private HostLoopCache hostLoops;

        public ButtonsRunHost(TestHostJob job, TestHostRunner runner)
        {
            this.job = job;
            this.runner = runner;
            folder = Path.GetDirectoryName(job.Model);
            work = Path.Combine(folder, "work", "buttons");
            steps = ButtonsRun.Steps(job);
            state = new HostState(this);
            result["code"] = job.Code;
            result["nbh"] = job.Nbh;
            result["dryRunFirst"] = job.DryRunFirst != false;
            result["startedUtc"] = DateTime.UtcNow;
            result["steps"] = entries;
            if (job.Shots == true)
            {
                result["shots"] = shots;
            }
        }

        // Set when the last phase ended on a worker start, the most the wait for its end may last in seconds
        public double? WaitSeconds { get; private set; }

        public bool Done { get; private set; }

        private string Settings
        {
            get { return Path.Combine(folder, "config", "project.json"); }
        }

        private string LoopsFile
        {
            get { return job.PrototypeLoops ?? Path.Combine(work, HostLoopCache.DefaultFile); }
        }

        // What the wait waits for: 1 while the add-in holds the end of a worker it started and did not hand on
        public int Pending(AddinProbe from)
        {
            return waiting != null && EndsWaiting() > 0 ? 1 : 0;
        }

        public void WaitEnded(WaitState ended, double seconds)
        {
            if (waiting != null)
            {
                waiting["wait"] = ended.ToString();
                waiting["waitedSeconds"] = seconds;
            }
        }

        public IEnumerable<JobStep> Phase(UIApplication app, AddinProbe from)
        {
            ui = app;
            probe = from;
            WaitSeconds = null;
            if (document == null && !stopped)
            {
                yield return Step("open", 10, Open);
            }
            if (waiting != null)
            {
                yield return Step("answer_" + waitingStep.Name, 20, Answered);
            }
            while (!stopped && next < steps.Count)
            {
                ButtonsStep step = steps[next++];
                yield return Step(step.Name, step.Button == 7 ? 20 : 10, () => RunStep(step));
                if (WaitSeconds.HasValue)
                {
                    yield break;
                }
            }
            yield return Step("write", 2, Write, true);
            Done = true;
        }

        private static JobStep Step(string name, double minutes, Func<string> action, bool always = false)
        {
            return new JobStep { Name = name, LimitMinutes = minutes, Always = always, Action = action };
        }

        // The copy of the run folder, the key copy the run script put there or a new project from the template, saved as the copy
        private string Open()
        {
            stopped = true;
            addIn = AppDomain.CurrentDomain.GetAssemblies().First(each => each.GetName().Name == AddinRunNames.Assembly);
            Directory.CreateDirectory(work);
            string made = "opened the key copy";
            if (!File.Exists(job.Model))
            {
                Document fresh = ui.Application.NewProjectDocument(job.Template);
                fresh.SaveAs(job.Model, new SaveAsOptions { OverwriteExistingFile = false });
                fresh.Close(false);
                made = "made from the template and opened";
            }
            document = ui.OpenAndActivateDocument(job.Model).Document;
            config = JObject.Parse(File.ReadAllText(Settings));
            DesignValues.UseConfigOf(config);
            stateBefore = Get(AddinRunNames.Buttons, AddinRunNames.State);
            setUpBefore = Get(AddinRunNames.SetUpSiteBody, AddinRunNames.Dialog);
            reviewBefore = Get(AddinRunNames.ReviewDataBody, AddinRunNames.Dialog);
            checkBefore = Get(AddinRunNames.CheckTableBody, AddinRunNames.Dialog);
            updateBefore = Get(AddinRunNames.UpdateFromReportBody, AddinRunNames.Dialog);
            rulesBefore = Get(AddinRunNames.RulesWindowBody, AddinRunNames.Dialog);
            swapped = true;
            Put(AddinRunNames.Buttons, AddinRunNames.State, state);
            Put(AddinRunNames.SetUpSiteBody, AddinRunNames.Dialog, (Func<IntPtr, ISetUpSiteDialog>)(owner => new SeenSetUp(this)));
            Put(AddinRunNames.ReviewDataBody, AddinRunNames.Dialog, (Func<IntPtr, IReviewDialog>)(owner => new NoReview(this)));
            Put(AddinRunNames.CheckTableBody, AddinRunNames.Dialog, (Func<IntPtr, string, ICheckTableDialog>)((owner, rules) => new KeepUses(this, rules)));
            Put(AddinRunNames.UpdateFromReportBody, AddinRunNames.Dialog, (Func<IntPtr, IUpdateFromReportDialog>)(owner => new ReadNoApply(this)));
            Put(AddinRunNames.RulesWindowBody, AddinRunNames.Dialog, (Action<IntPtr, ManualRuleTable, string>)((owner, rules, selected) => Opened(rules, selected)));
            if (job.Shots == true)
            {
                Noted("the pane was not shown", ShowPane);
            }
            result["model"] = Path.GetFileName(job.Model);
            result["privateMiBAtOpen"] = PrivateMiB();
            stopped = false;
            return made + ", " + Path.GetFileName(job.Model);
        }

        private string RunStep(ButtonsStep step)
        {
            var entry = new JObject { ["step"] = step.Name, ["button"] = step.Button, ["command"] = step.Command, ["villaMode"] = step.VillaMode, ["startedUtc"] = DateTime.UtcNow };
            entries.Add(entry);
            current = entry;
            startedUtc = DateTime.UtcNow;
            commitsBefore = state.Commits;
            dialogsBefore = Dialogs().Count;
            answered = false;
            stopped = true;
            Apply(step);
            before = step.Button == 7 ? Inventory() : null;
            Result pressed;
            DialogAnswers.Armed = Armed(step);
            try
            {
                pressed = step.Command == null ? Press(step.Button) : Press(step.Command);
            }
            finally
            {
                DialogAnswers.Armed = null;
            }
            stopped = false;
            entry["result"] = pressed.ToString();
            if (ButtonsRun.Waits(step.Button) && pressed == Result.Succeeded && state.Commits == commitsBefore && EndsWaiting() > 0)
            {
                waiting = entry;
                waitingStep = step;
                WaitSeconds = AddinConfig.Load(Settings).WorkerTimeLimitS(ButtonsRun.WorkerCommand(step.Button)) + WaitMarginSeconds;
                entry["workerStartedUtc"] = DateTime.UtcNow;
                entry["waitLimitSeconds"] = WaitSeconds.Value;
                Save();
                return step.Name + ": the worker started, the job waits up to " + WaitSeconds.Value + " s for its end";
            }
            return Finish(step, entry, pressed, false);
        }

        private string Answered()
        {
            JObject entry = waiting;
            ButtonsStep step = waitingStep;
            waiting = null;
            current = entry;
            entry["answerSeenUtc"] = DateTime.UtcNow;
            entry["routed"] = probe?.RoutedCount();
            return Finish(step, entry, Result.Succeeded, EndsWaiting() > 0);
        }

        private string Finish(ButtonsStep step, JObject entry, Result pressed, bool timedOut)
        {
            bool committed = state.Commits > commitsBefore;
            string outcome = step.Command != null ? ButtonsRun.NamedOutcome(answered, committed)
                : committed ? ButtonOutcomes.Committed : timedOut ? ButtonOutcomes.TimedOut : pressed == Result.Cancelled ? ButtonOutcomes.Cancelled : ButtonOutcomes.Stopped;
            entry["outcome"] = outcome;
            entry["record"] = committed && state.Last != null ? JObject.FromObject(state.Last) : null;
            entry["dialogs"] = new JArray(Dialogs().Skip(dialogsBefore).Select(dialog => new JObject
            {
                ["dialogId"] = dialog.DialogId, ["message"] = dialog.Message, ["answer"] = dialog.Answer, ["accepted"] = dialog.Accepted,
            }));
            entry["endedUtc"] = DateTime.UtcNow;
            entry["seconds"] = Math.Round((DateTime.UtcNow - startedUtc).TotalSeconds, 1);
            entry["privateMiB"] = PrivateMiB();
            if (job.Shots == true && ButtonShots.PaneAfter(step))
            {
                Pane(step.Name);
            }
            if (step.Button == 7 && committed)
            {
                try
                {
                    Villas(entry, step);
                }
                catch (Exception failure)
                {
                    entry["villasNotRead"] = failure.GetType().Name + " " + failure.Message;
                }
            }
            // R468. Bader's checkpoint of 1 Nov: a restart loses nothing a button committed. A save that fails is noted and the run goes on
            if (committed && document != null && document.IsValidObject && document.IsModified)
            {
                try
                {
                    document.Save();
                    entry["savedAfter"] = true;
                }
                catch (Exception failure)
                {
                    entry["savedAfter"] = failure.GetType().Name + " " + failure.Message;
                }
            }
            Save();
            if (!ButtonsRun.GoesOn(step, outcome))
            {
                stopped = true;
                throw new InvalidOperationException(step.Name + " ended " + outcome + ", the steps after it need its commit");
            }
            return step.Name + ": " + outcome;
        }

        // The settings of the pane for the copy: the JSON the run script wrote, with the NBH, the dry run and the Villa mode of the
        // step, given to the session of the document and loaded into the pane, so every body reads them as a pick would give them
        private void Apply(ButtonsStep step)
        {
            ProjectSettings project = SettingsJson.Read(File.ReadAllText(job.PaneSettings));
            project.Scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = job.Nbh };
            project.DryRunFirst = job.DryRunFirst != false;
            project.VillaMode = step.VillaMode ?? job.VillaMode;
            object settings = Activator.CreateInstance(addIn.GetType(AddinRunNames.PaneSettings, true), project);
            object host = Get(AddinRunNames.PaneHost, AddinRunNames.Current);
            object sessions = Member(host, AddinRunNames.Sessions);
            session = sessions.GetType().GetMethod(AddinRunNames.Seen).Invoke(sessions, new object[] { document, document.PathName, document.Title });
            session.GetType().GetProperty(AddinRunNames.Settings).SetValue(session, settings);
            object model = Member(host, AddinRunNames.Model);
            model.GetType().GetMethod(AddinRunNames.Load, new[] { session.GetType() }).Invoke(model, new[] { session });
        }

        private Result Press(int button)
        {
            Type buttons = addIn.GetType(AddinRunNames.Buttons, true);
            MethodInfo run = buttons.GetMethod(AddinRunNames.Run, new[] { typeof(int), typeof(UIApplication) });
            try
            {
                return (Result)run.Invoke(null, new object[] { button, ui });
            }
            catch (TargetInvocationException failure) when (failure.InnerException != null)
            {
                throw failure.InnerException;
            }
        }

        // A named command, by Buttons.Run(string, UIApplication) as its ribbon command runs it
        private Result Press(string command)
        {
            Type buttons = addIn.GetType(AddinRunNames.Buttons, true);
            MethodInfo run = buttons.GetMethod(AddinRunNames.Run, new[] { typeof(string), typeof(UIApplication) });
            try
            {
                return (Result)run.Invoke(null, new object[] { command, ui });
            }
            catch (TargetInvocationException failure) when (failure.InnerException != null)
            {
                throw failure.InnerException;
            }
        }

        private string[] Armed(ButtonsStep step)
        {
            Type layout = addIn.GetType(AddinRunNames.RibbonLayout, true);
            object spec = step.Command == null ? layout.GetMethod(AddinRunNames.Button, new[] { typeof(int) }).Invoke(null, new object[] { step.Button })
                : layout.GetMethod(AddinRunNames.ButtonOf, new[] { typeof(string) }).Invoke(null, new object[] { step.Command });
            var texts = new List<string> { (string)Member(spec, AddinRunNames.ButtonLabel), RunTexts.NothingChangedYet };
            if (step.Button == 7)
            {
                texts.AddRange(LinkLoadTexts);
            }
            return texts.ToArray();
        }

        // The ends LaunchedWorker of the add-in still holds, each a worker that has not been handed on
        private int EndsWaiting()
        {
            object ends = addIn?.GetType(AddinRunNames.LaunchedWorker, true).GetField(AddinRunNames.Ends, Any).GetValue(null);
            return ends == null ? 0 : ((ICollection)ends).Count;
        }

        private List<DialogEntry> Dialogs()
        {
            List<DialogEntry> seen = null;
            runner.Record(log => seen = log.Dialogs.ToList());
            return seen;
        }

        private string Write()
        {
            DialogAnswers.Armed = null;
            if (job.Shots == true && document != null)
            {
                Noted("the end shots stopped", EndShots);
            }
            if (swapped)
            {
                Put(AddinRunNames.Buttons, AddinRunNames.State, stateBefore);
                Put(AddinRunNames.SetUpSiteBody, AddinRunNames.Dialog, setUpBefore);
                Put(AddinRunNames.ReviewDataBody, AddinRunNames.Dialog, reviewBefore);
                Put(AddinRunNames.CheckTableBody, AddinRunNames.Dialog, checkBefore);
                Put(AddinRunNames.UpdateFromReportBody, AddinRunNames.Dialog, updateBefore);
                Put(AddinRunNames.RulesWindowBody, AddinRunNames.Dialog, rulesBefore);
                swapped = false;
            }
            // The copy keeps what the buttons did, for a look after the run. It lies in the run folder under the work root
            if (document != null && document.IsValidObject && document.IsModified)
            {
                document.Save();
                result["saved"] = true;
            }
            result["finishedUtc"] = DateTime.UtcNow;
            result["privateMiB"] = PrivateMiB();
            Save();
            return "written " + ButtonsRun.ResultFile + " of " + entries.Count + " steps";
        }

        // The pane shown and the tab of the add-in made active at the open, so the pane page is laid out and the ribbon shows the tab
        private void ShowPane()
        {
            Directory.CreateDirectory(Path.Combine(folder, ButtonShots.Folder));
            if (probe.PaneId() is DockablePaneId id)
            {
                ui.GetDockablePane(id).Show();
            }
            string tab = probe.Tab();
            if (tab != null)
            {
                RevitShots.ActivateTab(tab, shotNotes);
            }
        }

        // The pane page as RevitShots.Pane draws it in the addinRun job, with no least width to hold
        private void Pane(string step)
        {
            Kept(step, ButtonShots.Pane, () => new[] { RevitShots.Pane(probe.PageType(), ButtonShots.File(folder, step, ButtonShots.Pane), PaneShown(), null) });
        }

        // A shot never stops the open or the write of the result, what stopped it goes into shotNotes
        private void Noted(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception failure)
            {
                shotNotes.Add(what + ": " + Why(failure));
            }
        }

        private static string Why(Exception failure)
        {
            Exception inner = failure is TargetInvocationException && failure.InnerException != null ? failure.InnerException : failure;
            return inner.GetType().Name + " " + inner.Message;
        }

        private bool? PaneShown()
        {
            return probe.PaneId() is DockablePaneId id ? ui.GetDockablePane(id).IsShown() : (bool?)null;
        }

        // After the last step: the pane, the main window by PrintWindow and the ribbon cut from it, then New project and How it works
        // built on the pane state as the pane opens them, drawn and closed with nothing saved
        private void EndShots()
        {
            Pane(ButtonShots.End);
            Kept(ButtonShots.End, ButtonShots.Ribbon, () =>
            {
                List<RunShot> window = RevitShots.Window(ui.MainWindowHandle, RevitShots.RibbonBounds(shotNotes),
                    ButtonShots.File(folder, ButtonShots.End, ButtonShots.Window), ButtonShots.File(folder, ButtonShots.End, ButtonShots.Ribbon));
                RunShot ribbon = window.FirstOrDefault(shot => shot.Name == ButtonShots.Ribbon);
                if (ribbon != null && probe.Tab() != null)
                {
                    RevitShots.ReadTab(ribbon, probe.Tab(), shotNotes);
                }
                return window;
            });
            object model = Member(Get(AddinRunNames.PaneHost, AddinRunNames.Current), AddinRunNames.Model);
            object open = Member(model, AddinRunNames.PaneDocument);
            if (open != null)
            {
                Shoot(ButtonShots.End, ButtonShots.NewProject, "NewProject", open, probe.Root());
            }
            Shoot(ButtonShots.End, ButtonShots.HowItWorks, "HowItWorks", open != null);
        }

        // One window of the add-in built by WindowShots from the live view its button gave the Dialog, drawn offscreen and closed by
        // WindowShots.Draw before the host answers the Dialog. A shot that fails is kept with its error and the step goes on
        private void Shoot(string window, string build, params object[] args)
        {
            Shoot((string)current["step"], window, build, args);
        }

        private void Shoot(string step, string window, string build, params object[] args)
        {
            if (job.Shots != true)
            {
                return;
            }
            Kept(step, window, () =>
            {
                Type made = addIn.GetType(AddinRunNames.WindowShots, true);
                object built = made.GetMethod(build).Invoke(null, args);
                var drawn = (System.Windows.Media.Imaging.BitmapSource)made.GetMethod(AddinRunNames.Draw).Invoke(null, new[] { built });
                return new[] { RevitShots.Saved(window, ButtonShots.File(folder, step, window), drawn, "drawn offscreen by WindowShots." + build) };
            });
        }

        // Each shot with its file, size, colours and black share, or the error that stopped it, and the run goes on
        private void Kept(string step, string window, Func<IEnumerable<RunShot>> take)
        {
            try
            {
                foreach (RunShot shot in take().ToList())
                {
                    JObject kept = JObject.FromObject(shot, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                    kept.AddFirst(new JProperty("step", step));
                    shots.Add(kept);
                }
            }
            catch (Exception failure)
            {
                shots.Add(new JObject { ["step"] = step, ["name"] = window, ["failed"] = Why(failure) });
            }
        }

        private void Save()
        {
            if (job.Shots == true)
            {
                result["shotNotes"] = new JArray(shotNotes);
            }
            JsonOut.Write(Path.Combine(folder, ButtonsRun.ResultFile), result);
        }

        private static double PrivateMiB()
        {
            using (Process revit = Process.GetCurrentProcess())
            {
                revit.Refresh();
                return Math.Round(revit.PrivateMemorySize64 / 1048576.0, 1);
            }
        }

        // Every solid and link of the copy by UniqueId, true for a solid
        private Dictionary<string, bool> Inventory()
        {
            var filter = new LogicalOrFilter(new ElementClassFilter(typeof(DirectShape)), new ElementClassFilter(typeof(RevitLinkInstance)));
            return new FilteredElementCollector(document).WherePasses(filter).ToElements().ToDictionary(element => element.UniqueId, element => element is DirectShape);
        }

        // R404, R377. After a real run of 7: the counts of solids and links, then each solid placed against the lowest floor of its
        // prototype corner to corner, or each link against the plan, by the Revit API of the test host. The plan insert is taken to
        // internal coordinates by the transform of the active project location, the survey route of the add-in is kept beside it
        private void Villas(JObject entry, ButtonsStep step)
        {
            Dictionary<string, bool> after = Inventory();
            List<string> solids = after.Where(pair => pair.Value && !before.ContainsKey(pair.Key)).Select(pair => pair.Key).ToList();
            entry["solids"] = after.Count(pair => pair.Value);
            entry["links"] = after.Count(pair => !pair.Value);
            entry["placedSolids"] = solids.Count;
            entry["placedLinks"] = after.Count(pair => !pair.Value && !before.ContainsKey(pair.Key));
            entry["removed"] = before.Keys.Count(key => !after.ContainsKey(key));
            string planPath = LatestPlan();
            entry["plan"] = planPath == null ? null : Path.GetFileName(planPath);
            if (planPath == null)
            {
                return;
            }
            PlanFile plan = PlanFile.Read(planPath);
            List<PlanPlot> plots = plan.Plots.Where(plot => plot.Insert?.Value != null && plot.RotationDeg?.Value != null && plot.Mirrored?.Value != null
                && plot.PrototypeFile?.Value != null).GroupBy(plot => plot.Plot).Select(group => group.First()).ToList();
            double holdMm = Common.Placer.ReadBackOrigin * 1000;
            double holdDeg = Common.Placer.ReadBackAngle;
            string plotGuid = ParameterSheet.FromConfig(Settings, config).Parameters.FirstOrDefault(item => item.Name == Common.Placer.PlotParameter)?.Guid;
            Transform toInternal = document.ActiveProjectLocation.GetTotalTransform();
            BasePoint survey = BasePoint.GetSurveyPoint(document);
            XYZ offset = survey.SharedPosition - survey.Position;
            var villas = new JArray();
            if (step.VillaMode == VillaModes.Masses)
            {
                hostLoops = HostLoopCache.Load(LoopsFile, FileHash.Sha256);
                entry["prototypeLoops"] = LoopsFile;
                entry["loopsAtStart"] = hostLoops.AtStart;
                foreach (string id in solids)
                {
                    Element solid = document.GetElement(id);
                    List<double[]> baseLoop = BaseOf(solid);
                    PlanPlot plot = PlotOf(solid, plotGuid, plots, out string matchedBy, candidate => Centre(Expected(plan, candidate, toInternal)), Centre(baseLoop));
                    var villa = new JObject { ["element"] = solid.Id.ToString(), ["plot"] = plot?.Plot, ["matchedBy"] = matchedBy };
                    if (plot != null)
                    {
                        CornerMatch match = BaseCorners.Compare(baseLoop, Expected(plan, plot, toInternal), holdMm);
                        villa["corners"] = match.Placed;
                        villa["prototypeCorners"] = match.Expected;
                        villa["maxMm"] = Finite(match.MaxMm);
                        villa["within"] = match.Within;
                        villa["held"] = match.Held;
                        villa["surveyRouteOffMm"] = RouteGap(plot, toInternal, offset);
                    }
                    villas.Add(villa);
                }
                entry["loopsRead"] = hostLoops.Read;
            }
            else
            {
                // Only the links of this run, so a link a key copy held from its past job is not held against the plan
                foreach (RevitLinkInstance link in new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
                    .Where(each => !before.ContainsKey(each.UniqueId)))
                {
                    Transform total = link.GetTotalTransform();
                    double[] origin = Metres(total.Origin);
                    PlanPlot plot = PlotOf(link, plotGuid, plots, out string matchedBy, candidate => Internal(candidate.Insert.Value, toInternal), origin);
                    var villa = new JObject { ["element"] = link.Id.ToString(), ["plot"] = plot?.Plot, ["matchedBy"] = matchedBy };
                    if (plot != null)
                    {
                        LinkMatch match = LinkCheck.Compare(origin, Vector(total.BasisX), Vector(total.BasisY), Vector(total.BasisZ), Internal(plot.Insert.Value, toInternal),
                            plot.RotationDeg.Value.Value, plot.Mirrored.Value.Value, holdMm, holdDeg);
                        villa["originOffMm"] = Math.Round(match.OriginOffMm, 3);
                        villa["rotationOffDeg"] = Math.Round(match.RotationOffDeg, 4);
                        villa["mirrored"] = match.Mirrored;
                        villa["held"] = match.Held;
                        villa["surveyRouteOffMm"] = RouteGap(plot, toInternal, offset);
                    }
                    villas.Add(villa);
                }
            }
            entry["holdMm"] = holdMm;
            entry["holdDeg"] = holdDeg;
            entry["villas"] = villas;
            entry["held"] = villas.Count(villa => villa["held"]?.Value<bool>() == true);
            entry["notHeld"] = villas.Count - entry["held"].Value<int>();
        }

        // The newest plan of the NBH a real run of 4 Check wrote in its check folder, never one of a dry folder
        private string LatestPlan()
        {
            return Directory.GetDirectories(work, CheckRun.FolderPrefix + "*").OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Select(path => Path.Combine(path, "plan_" + job.Nbh + ".json")).FirstOrDefault(File.Exists);
        }

        // By the plot number parameter of config by its GUID, else the plot whose expected place lies nearest
        private static PlanPlot PlotOf(Element element, string plotGuid, List<PlanPlot> plots, out string matchedBy, Func<PlanPlot, double[]> place, double[] at)
        {
            string number = plotGuid == null ? null : element.get_Parameter(new Guid(plotGuid))?.AsString();
            PlanPlot found = number == null ? null : plots.FirstOrDefault(plot => plot.Plot == number);
            if (found != null)
            {
                matchedBy = "parameter";
                return found;
            }
            matchedBy = "nearest";
            return at == null ? null : plots.OrderBy(plot => Distance(place(plot), at)).FirstOrDefault();
        }

        private List<double[]> Expected(PlanFile plan, PlanPlot plot, Transform toInternal)
        {
            List<double[]> loop = PrototypeLoop(Path.Combine(plan.PrototypeFolder, plot.PrototypeFile.Value.Trim()));
            return BaseCorners.Carry(loop, Internal(plot.Insert.Value, toInternal), plot.RotationDeg.Value.Value, plot.Mirrored.Value.Value);
        }

        private static double[] Internal(double[] shared, Transform toInternal)
        {
            return Metres(toInternal.OfPoint(new XYZ(Feet(shared[0]), Feet(shared[1]), 0)));
        }

        // The gap between the insert by the transform and by the survey point offset the add-in takes, in mm
        private static double RouteGap(PlanPlot plot, Transform toInternal, XYZ offset)
        {
            double[] bySurvey = Metres(new XYZ(Feet(plot.Insert.Value[0]), Feet(plot.Insert.Value[1]), 0) - offset);
            return Math.Round(Distance(Internal(plot.Insert.Value, toInternal), bySurvey) * 1000, 3);
        }

        // The outer loop of the sketch of the lowest floor of the prototype, in its internal plan coordinates, read once per file by
        // the loop file of the job
        private List<double[]> PrototypeLoop(string path)
        {
            return hostLoops.Get(path, ReadCopy);
        }

        // D3, D19. The prototype is copied into the run, the copy opened detached and closed with no save and deleted, never the
        // original. The dialogs and warnings of the open are answered by the session
        private List<double[]> ReadCopy(string path)
        {
            string copies = Path.Combine(work, "copies");
            Directory.CreateDirectory(copies);
            string copy = Path.Combine(copies, Guid.NewGuid().ToString("N") + Path.GetExtension(path));
            File.Copy(path, copy);
            Document prototype = null;
            try
            {
                prototype = ui.Application.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(copy), new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets });
                Floor lowest = new FilteredElementCollector(prototype).OfClass(typeof(Floor)).Cast<Floor>().Where(floor => floor.get_BoundingBox(null) != null)
                    .OrderBy(floor => floor.get_BoundingBox(null).Min.Z).FirstOrDefault();
                if (lowest == null || !(prototype.GetElement(lowest.SketchId) is Sketch sketch))
                {
                    throw new InvalidDataException(Path.GetFileName(path) + " holds no floor with a sketch");
                }
                return sketch.Profile.Cast<CurveArray>().Select(array => array.Cast<Curve>().SelectMany(Corners).ToList()).OrderByDescending(Area).First();
            }
            finally
            {
                prototype?.Close(false);
                File.Delete(copy);
            }
        }

        // The outer loop of the lowest face of the solid that points down, from its own geometry
        private static List<double[]> BaseOf(Element solid)
        {
            PlanarFace bottom = Solids(solid.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })).SelectMany(each => each.Faces.Cast<Face>()).OfType<PlanarFace>()
                .Where(face => face.FaceNormal.Z < -DownNormal).OrderBy(face => face.Origin.Z).FirstOrDefault();
            if (bottom == null)
            {
                return new List<double[]>();
            }
            return bottom.GetEdgesAsCurveLoops().Select(loop => loop.SelectMany(Corners).ToList()).OrderByDescending(Area).First();
        }

        private static IEnumerable<Solid> Solids(GeometryElement geometry)
        {
            if (geometry == null)
            {
                yield break;
            }
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Faces.Size > 0)
                {
                    yield return solid;
                }
                else if (item is GeometryInstance instance)
                {
                    foreach (Solid inner in Solids(instance.GetInstanceGeometry()))
                    {
                        yield return inner;
                    }
                }
            }
        }

        // A line gives its start, a curve the points of its tessellation but its end, which the next curve starts at
        private static IEnumerable<double[]> Corners(Curve curve)
        {
            if (curve is Line)
            {
                return new[] { Metres(curve.GetEndPoint(0)) };
            }
            IList<XYZ> points = curve.Tessellate();
            return points.Take(points.Count - 1).Select(Metres);
        }

        private static double Area(List<double[]> loop)
        {
            double twice = 0;
            for (int index = 0; index < loop.Count; index++)
            {
                double[] a = loop[index];
                double[] b = loop[(index + 1) % loop.Count];
                twice += a[0] * b[1] - b[0] * a[1];
            }
            return Math.Abs(twice) / 2;
        }

        private static double[] Centre(List<double[]> loop)
        {
            return loop.Count == 0 ? null : new[] { loop.Average(point => point[0]), loop.Average(point => point[1]) };
        }

        private static double Distance(double[] a, double[] b)
        {
            return a == null || b == null ? double.MaxValue : Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));
        }

        private static double? Finite(double value)
        {
            return double.IsInfinity(value) || double.IsNaN(value) ? (double?)null : Math.Round(value, 3);
        }

        private static double Feet(double metres)
        {
            return UnitUtils.ConvertToInternalUnits(metres, UnitTypeId.Meters);
        }

        private static double[] Metres(XYZ point)
        {
            return new[] { UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Meters), UnitUtils.ConvertFromInternalUnits(point.Y, UnitTypeId.Meters),
                UnitUtils.ConvertFromInternalUnits(point.Z, UnitTypeId.Meters) };
        }

        private static double[] Vector(XYZ axis)
        {
            return new[] { axis.X, axis.Y, axis.Z };
        }

        private object Get(string type, string member)
        {
            return Member(addIn.GetType(type, true), member, null);
        }

        private void Put(string type, string member, object value)
        {
            addIn.GetType(type, true).GetProperty(member, Any).SetValue(null, value);
        }

        private static object Member(object target, string name)
        {
            return Member(target.GetType(), name, target);
        }

        private static object Member(Type type, string name, object target)
        {
            PropertyInfo property = type.GetProperty(name, Any);
            if (property != null)
            {
                return property.GetValue(target);
            }
            FieldInfo field = type.GetField(name, Any);
            if (field != null)
            {
                return field.GetValue(target);
            }
            throw new MissingMemberException(type.FullName, name);
        }

        // The pane side of the flow for the copy: the work folder of the run, the dry run of the job, no step missing, the inputs of the
        // pane by PaneRunState.InputsOf of the add-in and each commit kept with its session as the pane keeps it
        private sealed class HostState : IRunState
        {
            private readonly ButtonsRunHost host;

            public HostState(ButtonsRunHost host)
            {
                this.host = host;
            }

            public int Commits { get; private set; }

            public RunRecord Last { get; private set; }

            public string WorkFolder(string documentPath)
            {
                return host.work;
            }

            public bool DryRunFirst
            {
                get { return host.job.DryRunFirst != false; }
            }

            public int MissingStep(int button)
            {
                return 0;
            }

            public IReadOnlyList<RunInput> Inputs(int button)
            {
                object settings = Member(host.session, AddinRunNames.Settings);
                Type paneRunState = host.addIn.GetType(AddinRunNames.PaneRunState, true);
                return (IReadOnlyList<RunInput>)paneRunState.GetMethod(AddinRunNames.InputsOf, Any).Invoke(null, new[] { settings, button });
            }

            public void Committed(int button, RunRecord record)
            {
                Commits++;
                Last = record;
                Type paneRunState = host.addIn.GetType(AddinRunNames.PaneRunState, true);
                paneRunState.GetMethod(AddinRunNames.Record, Any).Invoke(null, new[] { host.session, button, record });
            }
        }

        // Button 1 as setUp answers it, what it was shown kept with the step
        private sealed class SeenSetUp : ISetUpSiteDialog
        {
            private readonly ButtonsRunHost host;

            public SeenSetUp(ButtonsRunHost host)
            {
                this.host = host;
            }

            public SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks)
            {
                host.Shoot(ButtonShots.SetUpSite, "SetUp", view, callbacks);
                var window = new SetUpSiteRun.AnswerWindow();
                SetUpSiteAnswer answer = window.Show(view, callbacks);
                host.current["setUpStops"] = window.Stops;
                return answer;
            }
        }

        // 3 Review data with no click: it marks nothing and saves nothing, so the button ends Cancelled
        private sealed class NoReview : IReviewDialog
        {
            private readonly ButtonsRunHost host;

            public NoReview(ButtonsRunHost host)
            {
                this.host = host;
            }

            public ReviewAnswer Show(ReviewView view, ReviewCallbacks callbacks)
            {
                host.Shoot(ButtonShots.ReviewData, "Review", view, callbacks);
                host.current["review"] = new JObject { ["nbh"] = view.Nbh, ["rows"] = view.State?.Rows?.Count, ["sources"] = view.Sources == null ? null : new JArray(view.Sources) };
                return new ReviewAnswer { Saved = false };
            }
        }

        // Site Adaptation Rules with no click: the window opens on the table of the rules file of the pane and closes, it changes nothing.
        // A rules file with no usable block shows its line in place of the window, so the step is not answered
        private void Opened(ManualRuleTable rules, string selected)
        {
            Shoot(ButtonShots.Rules, "Rules", rules, selected);
            current["rulesWindow"] = new JObject { ["rules"] = rules.Block.Rules.Count, ["revision"] = rules.Block.Manual?.Revision };
            answered = true;
        }

        // Update from report with no click: it reads the level Excel of 6 and the report Excel of 9 of the run through the worker as a
        // pick of both would, keeps the counts of the list and closes with no change, so the override list is not written
        private sealed class ReadNoApply : IUpdateFromReportDialog
        {
            private readonly ButtonsRunHost host;

            public ReadNoApply(ButtonsRunHost host)
            {
                this.host = host;
            }

            public UpdateFromReportAnswer Show(UpdateFromReportCallbacks callbacks)
            {
                var window = new JObject();
                host.current["updateFromReport"] = window;
                string missing;
                List<string> files = ButtonsRun.UpdateFiles(host.work, host.job.Nbh, out missing);
                if (files == null)
                {
                    window["notRead"] = missing;
                    return new UpdateFromReportAnswer { Apply = false };
                }
                window["files"] = new JArray(files.Select(Path.GetFileName));
                try
                {
                    UpdateList list = callbacks.Read(files);
                    window["refusal"] = list.Refusal;
                    window["changes"] = list.Changes.Count;
                    window["ignored"] = list.Ignored.Count;
                    window["lacks"] = new JArray(list.Lacks);
                    window["tiles"] = new JObject(list.Tiles().Select(pair => new JProperty(pair.Key, pair.Value)));
                    host.Shoot(ButtonShots.UpdateFromReport, "Update", callbacks, list, files);
                    host.answered = list.Refusal == null;
                }
                catch (Exception failure)
                {
                    window["notRead"] = failure.GetType().Name + " " + failure.Message;
                }
                return new UpdateFromReportAnswer { Apply = false };
            }
        }

        // 4 Check with no click: every Use stays as the table opens it and Save overrides does not run
        private sealed class KeepUses : ICheckTableDialog
        {
            private readonly ButtonsRunHost host;
            private readonly string rules;

            public KeepUses(ButtonsRunHost host, string rules)
            {
                this.host = host;
                this.rules = rules;
            }

            public CheckTableAnswer Show(CheckTableView view, CheckTableCallbacks callbacks)
            {
                host.Shoot(ButtonShots.CheckTable, "Check", view, callbacks, rules);
                host.current["checkTable"] = new JObject
                {
                    ["title"] = view.Title, ["rows"] = view.Rows.Count, ["followEveryRule"] = view.FollowEveryRule, ["saveAllowed"] = view.SaveAllowed,
                    ["counts"] = new JObject(view.Counts.Select(pair => new JProperty(pair.Key, pair.Value))),
                };
                return new CheckTableAnswer { Uses = view.Rows.Select((row, index) => new CheckChoice(index, row.OpenUse)).ToList(), Saved = false };
            }
        }
    }
}
