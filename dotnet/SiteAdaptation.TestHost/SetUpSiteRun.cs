using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.TestHost
{
    // Lane B 5b, docs/design/09-risks.md open items 3 to 8 (R373, R374). Button 1 on the four copies of 3.1, each run as the ribbon
    // runs it, Buttons.Run of the loaded add-in by reflection, with a window that answers by itself: Dry run with the default slope,
    // then Apply unless the view holds a stop. Then a second run, which must find nothing to set. The site facts of R311 are read
    // before and after by the Revit API here, a route of its own apart from the Revit side of the add-in. Everything the
    // run saw goes into setup_result.json of the run folder, outside the repo, since it holds coordinates
    internal sealed class SetUpSiteRun
    {
        public const string ResultFile = "setup_result.json";
        public const string ImdTail = "-SETUP-IMD.rvt";
        public const string NoTemplateTail = "-SETUP-NOTEMPLATE.rvt";
        public const string R17bTail = "-SETUP-R17B.rvt";
        public const double Slope = 0.5;

        private readonly TestHostJob job;
        private readonly TestHostRunner runner;
        private readonly string folder;
        private readonly JObject result = new JObject();
        private readonly JArray copies = new JArray();
        private UIApplication ui;
        // Revit refuses to close the active document from the API, so each copy is closed once the next one is active, the last at exit
        private Document previous;

        public SetUpSiteRun(TestHostJob job, TestHostRunner runner)
        {
            this.job = job;
            this.runner = runner;
            folder = Path.GetDirectoryName(job.Model);
            result["code"] = job.Code;
            result["startedUtc"] = DateTime.UtcNow;
            result["copies"] = copies;
        }

        public string Settings
        {
            get { return Path.Combine(folder, "config", "project.json"); }
        }

        private string Copy(string tail)
        {
            return System.IO.Path.Combine(folder, job.Code + tail);
        }

        public IEnumerable<JobStep> Steps(UIApplication app)
        {
            ui = app;
            yield return Step("makeTemplate", 5, () => Make(job.Model, () => ui.Application.NewProjectDocument(job.Template), null));
            yield return Step("makeNoTemplate", 5, () => Make(Copy(NoTemplateTail), () => ui.Application.NewProjectDocument(UnitSystem.Metric), null));
            yield return Step("makeR17b", 5, () => Make(Copy(R17bTail), () => ui.Application.NewProjectDocument(job.Template), OtherGuids));
            foreach (var copy in new[] { Tuple.Create("template", job.Model), Tuple.Create("noTemplate", Copy(NoTemplateTail)), Tuple.Create("imd", Copy(ImdTail)), Tuple.Create("r17b", Copy(R17bTail)) })
            {
                yield return Step("setUp_" + copy.Item1, 4, () => Run(copy.Item1, copy.Item2), true);
            }
            yield return Step("write", 1, () =>
            {
                result["finishedUtc"] = DateTime.UtcNow;
                JsonOut.Write(System.IO.Path.Combine(folder, ResultFile), result);
                return "written " + ResultFile + " of " + copies.Count + " copies";
            }, true);
        }

        private JobStep Step(string name, double minutes, Func<string> action, bool always = false)
        {
            return new JobStep { Name = name, LimitMinutes = minutes, Always = always, Action = action };
        }

        private string Make(string path, Func<Document> make, Action<Document> then)
        {
            Document document = make();
            then?.Invoke(document);
            document.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = false });
            document.Close(false);
            return "made and saved as " + System.IO.Path.GetFileName(path);
        }

        // The sign of 01101-PSN-IMC-R17B-TEST-NEW, made as R212 made it: the names of the sheet bound from a new shared parameter file,
        // so with GUIDs of their own, never those of config. The setting of Revit is put back
        private void OtherGuids(Document document)
        {
            var app = ui.Application;
            string before = app.SharedParametersFilename;
            string file = System.IO.Path.Combine(folder, job.Code + "_r17b_shared.txt");
            File.WriteAllText(file, "");
            try
            {
                app.SharedParametersFilename = file;
                DefinitionGroup group = app.OpenSharedParameterFile().Groups.Create("SiteAdaptation");
                ParameterSheet sheet = ParameterSheet.FromConfig(JObject.Parse(File.ReadAllText(Settings)));
                using (var transaction = new Transaction(document, "Parameters of R212"))
                {
                    transaction.Start();
                    foreach (ParameterSpec spec in sheet.Parameters)
                    {
                        Definition definition = group.Definitions.Create(new ExternalDefinitionCreationOptions(spec.Name, SpecTypeId.String.Text));
                        CategorySet categories = app.Create.NewCategorySet();
                        categories.Insert(Category.GetCategory(document, BuiltInCategory.OST_RvtLinks));
                        document.ParameterBindings.Insert(definition, app.Create.NewInstanceBinding(categories), GroupTypeId.Data);
                    }
                    transaction.Commit();
                }
            }
            finally
            {
                app.SharedParametersFilename = before;
            }
        }

        private string Run(string label, string path)
        {
            var entry = new JObject { ["label"] = label, ["file"] = System.IO.Path.GetFileName(path) };
            copies.Add(entry);
            Document document = ui.OpenAndActivateDocument(path).Document;
            if (previous != null && previous.IsValidObject)
            {
                previous.Close(false);
            }
            previous = document;
            entry["before"] = Facts(document);
            var first = new AnswerWindow();
            var state = new RunState(folder, label, Settings);
            entry["firstResult"] = Button(state, first);
            entry["first"] = first.Seen;
            entry["firstCommitted"] = state.Commits;
            entry["after"] = Facts(document);
            var second = new AnswerWindow();
            var again = new RunState(folder, label, Settings);
            entry["secondResult"] = Button(again, second);
            entry["second"] = second.Seen;
            entry["secondCommitted"] = again.Commits;
            entry["afterSecond"] = Facts(document);
            return label + ": first " + (state.Commits > 0 ? "committed" : "not committed") + ", second " + (again.Commits > 0 ? "committed" : "not committed")
                + ", stops " + first.Stops;
        }

        // Buttons.Run(1) of the add-in with the run state and the window of this run, both put back after it
        private string Button(IRunState state, ISetUpSiteDialog dialog)
        {
            Assembly addIn = AppDomain.CurrentDomain.GetAssemblies().First(each => each.GetName().Name == AddinRunNames.Assembly);
            Type buttons = addIn.GetType(AddinRunNames.Buttons, true);
            Type body = addIn.GetType(AddinRunNames.SetUpSiteBody, true);
            PropertyInfo stateProperty = buttons.GetProperty(AddinRunNames.State, BindingFlags.Public | BindingFlags.Static);
            PropertyInfo dialogProperty = body.GetProperty(AddinRunNames.Dialog, BindingFlags.Public | BindingFlags.Static);
            object stateBefore = stateProperty.GetValue(null);
            object dialogBefore = dialogProperty.GetValue(null);
            try
            {
                stateProperty.SetValue(null, state);
                dialogProperty.SetValue(null, (Func<IntPtr, ISetUpSiteDialog>)(owner => dialog));
                MethodInfo run = buttons.GetMethod(AddinRunNames.Run, new[] { typeof(int), typeof(UIApplication) });
                return run.Invoke(null, new object[] { 1, ui }).ToString();
            }
            finally
            {
                stateProperty.SetValue(null, stateBefore);
                dialogProperty.SetValue(null, dialogBefore);
            }
        }

        // The facts of R311 that button 1 sets, read here by the Revit API, a route of its own apart from RevitSiteDocument of the
        // add-in: the position, the levels and their base, the units, the two points, the types and the shared parameters by GUID
        private static JObject Facts(Document document)
        {
            var facts = new JObject();
            ProjectPosition position = document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            facts["positionM"] = new JArray(Metre(position.EastWest), Metre(position.NorthSouth), Metre(position.Elevation));
            facts["angleDeg"] = position.Angle * 180 / Math.PI;
            facts["levels"] = new JArray(new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().OrderBy(level => level.Name).Select(level => new JObject
            {
                ["name"] = level.Name,
                ["elevationM"] = Metre(level.Elevation),
                ["projectElevationM"] = Metre(level.ProjectElevation),
                ["base"] = document.GetElement(level.GetTypeId())?.get_Parameter(BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE)?.AsInteger(),
            }));
            FormatOptions length = document.GetUnits().GetFormatOptions(SpecTypeId.Length);
            facts["lengthUnit"] = length.GetUnitTypeId().TypeId;
            facts["lengthAccuracy"] = length.Accuracy;
            facts["displayUnitSystem"] = document.DisplayUnitSystem.ToString();
            facts["sharedParameters"] = new JArray(new FilteredElementCollector(document).OfClass(typeof(SharedParameterElement)).Cast<SharedParameterElement>()
                .Select(each => new JObject { ["name"] = each.Name, ["guid"] = each.GuidValue.ToString() }));
            var survey = BasePoint.GetSurveyPoint(document);
            var project = BasePoint.GetProjectBasePoint(document);
            facts["surveyClipped"] = survey.Clipped;
            facts["surveyInternalM"] = Metres(survey.Position);
            facts["surveySharedM"] = Metres(survey.SharedPosition);
            facts["baseInternalM"] = Metres(project.Position);
            facts["baseSharedM"] = Metres(project.SharedPosition);
            facts["floorTypes"] = new JArray(new FilteredElementCollector(document).OfClass(typeof(FloorType)).Select(each => each.Name).OrderBy(name => name));
            facts["wallTypes"] = new JArray(new FilteredElementCollector(document).OfClass(typeof(WallType)).Cast<WallType>().Where(each => each.Kind == WallKind.Basic).Select(each => each.Name).OrderBy(name => name));
            facts["modified"] = document.IsModified;
            return facts;
        }

        private static double Metre(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
        }

        private static JArray Metres(XYZ point)
        {
            return new JArray(UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Meters), UnitUtils.ConvertFromInternalUnits(point.Y, UnitTypeId.Meters),
                UnitUtils.ConvertFromInternalUnits(point.Z, UnitTypeId.Meters));
        }

        // The window of button 1 with no click: it keeps what it was shown, runs Dry run with the default slope and answers Apply
        // unless a view holds a stop
        internal sealed class AnswerWindow : ISetUpSiteDialog
        {
            public JObject Seen { get; } = new JObject();

            public int Stops { get; private set; }

            public SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks)
            {
                Seen["shown"] = JObject.FromObject(view);
                Stops = view.Stops.Count;
                if (Stops > 0)
                {
                    return new SetUpSiteAnswer { Apply = false };
                }
                SetUpSiteView dry = callbacks.DryRun(Slope);
                Seen["dryRun"] = JObject.FromObject(dry);
                Stops = dry.Stops.Count;
                return new SetUpSiteAnswer { Apply = Stops == 0, RampSlope = Slope };
            }
        }

        // The pane side of the flow for one copy: its own work folder, the dry run first, no step missing, the settings of the run
        private sealed class RunState : IRunState
        {
            private readonly string work;
            private readonly string settings;

            public RunState(string folder, string label, string settings)
            {
                work = System.IO.Path.Combine(folder, "work", label);
                Directory.CreateDirectory(work);
                this.settings = settings;
            }

            public int Commits { get; private set; }

            public string WorkFolder(string documentPath)
            {
                return work;
            }

            public bool DryRunFirst
            {
                get { return true; }
            }

            public int MissingStep(int button)
            {
                return 0;
            }

            public IReadOnlyList<RunInput> Inputs(int button)
            {
                return new[] { new RunInput("Settings", settings, false) };
            }

            public void Committed(int button, RunRecord record)
            {
                Commits++;
            }
        }
    }
}
