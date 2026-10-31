using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.TestHost
{
    // Lane B 5c, the Revit run after 5c (R384). The buttons of the job run on one copy as the ribbon runs them, Buttons.Run of the loaded
    // add-in by reflection, with windows that answer by themselves: Set up site as setUp answers it, Review data marks and saves
    // nothing, the check table keeps every Use as it opens and saves nothing, and the confirm of the flow and the link types to load
    // take their first link while the step runs. 2 and 4 return once their worker started, so the session waits on its Idling clock
    // until the add-in handed the end on, then the next phase goes on. After 7 each placed solid and link is read by the Revit API
    // here, a route of its own apart from the read back of the add-in. Everything goes into buttons_result.json of the run folder
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
            swapped = true;
            Put(AddinRunNames.Buttons, AddinRunNames.State, state);
            Put(AddinRunNames.SetUpSiteBody, AddinRunNames.Dialog, (Func<IntPtr, ISetUpSiteDialog>)(owner => new SeenSetUp(this)));
            Put(AddinRunNames.ReviewDataBody, AddinRunNames.Dialog, (Func<IntPtr, IReviewDialog>)(owner => new NoReview(this)));
            Put(AddinRunNames.CheckTableBody, AddinRunNames.Dialog, (Func<IntPtr, string, ICheckTableDialog>)((owner, rules) => new KeepUses(this)));
            result["model"] = Path.GetFileName(job.Model);
            result["privateMiBAtOpen"] = PrivateMiB();
            stopped = false;
            return made + ", " + Path.GetFileName(job.Model);
        }

        private string RunStep(ButtonsStep step)
        {
            var entry = new JObject { ["step"] = step.Name, ["button"] = step.Button, ["villaMode"] = step.VillaMode, ["startedUtc"] = DateTime.UtcNow };
            entries.Add(entry);
            current = entry;
            startedUtc = DateTime.UtcNow;
            commitsBefore = state.Commits;
            dialogsBefore = Dialogs().Count;
            stopped = true;
            Apply(step);
            before = step.Button == 7 ? Inventory() : null;
            Result pressed;
            DialogAnswers.Armed = Armed(step.Button);
            try
            {
                pressed = Press(step.Button);
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
            string outcome = committed ? ButtonOutcomes.Committed : timedOut ? ButtonOutcomes.TimedOut : pressed == Result.Cancelled ? ButtonOutcomes.Cancelled : ButtonOutcomes.Stopped;
            entry["outcome"] = outcome;
            entry["record"] = committed && state.Last != null ? JObject.FromObject(state.Last) : null;
            entry["dialogs"] = new JArray(Dialogs().Skip(dialogsBefore).Select(dialog => new JObject
            {
                ["dialogId"] = dialog.DialogId, ["message"] = dialog.Message, ["answer"] = dialog.Answer, ["accepted"] = dialog.Accepted,
            }));
            entry["endedUtc"] = DateTime.UtcNow;
            entry["seconds"] = Math.Round((DateTime.UtcNow - startedUtc).TotalSeconds, 1);
            entry["privateMiB"] = PrivateMiB();
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
            Save();
            if (!ButtonsRun.GoesOn(step.Button, outcome))
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

        private string[] Armed(int button)
        {
            object spec = addIn.GetType(AddinRunNames.RibbonLayout, true).GetMethod(AddinRunNames.Button, new[] { typeof(int) }).Invoke(null, new object[] { button });
            var texts = new List<string> { (string)Member(spec, AddinRunNames.ButtonLabel), RunTexts.NothingChangedYet };
            if (button == 7)
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
            if (swapped)
            {
                Put(AddinRunNames.Buttons, AddinRunNames.State, stateBefore);
                Put(AddinRunNames.SetUpSiteBody, AddinRunNames.Dialog, setUpBefore);
                Put(AddinRunNames.ReviewDataBody, AddinRunNames.Dialog, reviewBefore);
                Put(AddinRunNames.CheckTableBody, AddinRunNames.Dialog, checkBefore);
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

        private void Save()
        {
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
            string plotGuid = ParameterSheet.FromConfig(config).Parameters.FirstOrDefault(item => item.Name == Common.Placer.PlotParameter)?.Guid;
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
                host.current["review"] = new JObject { ["nbh"] = view.Nbh, ["rows"] = view.State?.Rows?.Count, ["sources"] = view.Sources == null ? null : new JArray(view.Sources) };
                return new ReviewAnswer { Saved = false };
            }
        }

        // 4 Check with no click: every Use stays as the table opens it and Save overrides does not run
        private sealed class KeepUses : ICheckTableDialog
        {
            private readonly ButtonsRunHost host;

            public KeepUses(ButtonsRunHost host)
            {
                this.host = host;
            }

            public CheckTableAnswer Show(CheckTableView view, CheckTableCallbacks callbacks)
            {
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
