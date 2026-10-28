using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.TestHost
{
    // Lane B 5a (R372). The add-in as Revit loaded it, read by reflection, so the test host holds no reference to it, loads no second
    // copy and changes none of its files. Each member it cannot find is a note of the result and reads as not measured
    internal sealed class AddinProbe
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private readonly Assembly assembly;
        private readonly List<string> notes;

        private AddinProbe(Assembly assembly, List<string> notes)
        {
            this.assembly = assembly;
            this.notes = notes;
        }

        public static AddinProbe Find(List<string> notes)
        {
            Assembly found = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(each => each.GetName().Name == AddinRunNames.Assembly);
            if (found == null)
            {
                notes.Add("the add-in assembly " + AddinRunNames.Assembly + " is not loaded in this Revit");
            }
            return new AddinProbe(found, notes);
        }

        private object Host
        {
            get { return Read("PaneHost.Current", () => Member(TypeOf(AddinRunNames.PaneHost), AddinRunNames.Current, null)); }
        }

        private object Model
        {
            get { return Read("PaneHost.model", () => Member(Host, AddinRunNames.Model)); }
        }

        public string SessionOf(Document document)
        {
            if (document == null)
            {
                return null;
            }
            return Read("PaneSessions.Find", () =>
            {
                object sessions = Member(Host, AddinRunNames.Sessions);
                return Member(Call(sessions.GetType(), AddinRunNames.Find, sessions, document), AddinRunNames.SessionId);
            }) as string;
        }

        public int? Loads()
        {
            return Read("PaneViewModel.Loads", () => Member(Model, AddinRunNames.Loads)) as int?;
        }

        public string PaneSession()
        {
            return Read("PaneViewModel.document", () => Member(Member(Model, AddinRunNames.Document), AddinRunNames.SessionId)) as string;
        }

        public string PaneProject()
        {
            return Read("PaneViewModel.ProjectName", () => Member(Model, AddinRunNames.ProjectName)) as string;
        }

        public int? Waiting()
        {
            return Read("AnswerRelay.Waiting", () => Member(Member(Host, AddinRunNames.Answers), AddinRunNames.Waiting)) as int?;
        }

        public int? RoutedCount()
        {
            return Read("PaneHost.Routed", () => ((ICollection)Member(Host, AddinRunNames.Routed)).Count) as int?;
        }

        // The answer at index of PaneHost.Routed into the record: the session and path of the document it went to, or its issue
        public void ReadRouted(int index, RunAnswer into)
        {
            Read("RoutedAnswer", () =>
            {
                object routed = ((IList)Member(Host, AddinRunNames.Routed))[index];
                object document = Member(routed, AddinRunNames.RoutedDocument);
                into.Routed = true;
                into.RoutedSession = Member(document, AddinRunNames.SessionId) as string;
                into.RoutedPath = Member(document, AddinRunNames.RoutedPath) as string;
                into.Issue = Member(routed, AddinRunNames.Issue) as string;
                return null;
            });
        }

        // A worker answer for the document of path and session, posted to the relay of the add-in as a worker would post it, so the
        // ExternalEvent of the add-in routes it. The answer is built as the type Post takes, whichever Common that type came from
        public bool Post(string runId, string path, string sessionId)
        {
            return Read("AnswerRelay.Post", () =>
            {
                object answers = Member(Host, AddinRunNames.Answers);
                MethodInfo post = answers.GetType().GetMethod(AddinRunNames.Post);
                var payload = new JObject
                {
                    ["runId"] = runId,
                    ["document"] = new JObject { ["path"] = path, ["sessionId"] = sessionId },
                    ["status"] = "ok",
                    ["message"] = "Lane B 5a test answer, nothing to apply",
                };
                post.Invoke(answers, new[] { payload.ToObject(post.GetParameters()[0].ParameterType) });
                return true;
            }) as bool? == true;
        }

        // The work root as the add-in names it, through WorkFolder.Root, the one the pane side gives the flow
        public string Root()
        {
            return Read("WorkFolder.Root", () =>
            {
                Type type = TypeOf(AddinRunNames.WorkFolder);
                var read = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), type.GetMethod(AddinRunNames.ReadIfThere, Any));
                return Call(type, AddinRunNames.Root, null, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), read);
            }) as string;
        }

        // The pane side of the R382 flow as the add-in sets it at start, the one that names the work folder of a button
        public IRunState State()
        {
            return Read("Buttons.State", () =>
            {
                object state = Member(TypeOf(AddinRunNames.Buttons), AddinRunNames.State, null);
                if (state != null && !(state is IRunState))
                {
                    throw new InvalidCastException("Buttons.State is no IRunState of the Common this test host loaded");
                }
                return state;
            }) as IRunState;
        }

        public string Tab()
        {
            return Read("RibbonLayout.Tab", () => Member(TypeOf(AddinRunNames.RibbonLayout), AddinRunNames.Tab, null)) as string;
        }

        public object PaneId()
        {
            return Read("PaneHost.PaneId", () => Member(TypeOf(AddinRunNames.PaneHost), AddinRunNames.PaneId, null));
        }

        public Type PageType()
        {
            return Read("SitePanePage", () => TypeOf(AddinRunNames.Page)) as Type;
        }

        private Type TypeOf(string name)
        {
            if (assembly == null)
            {
                throw new InvalidOperationException("the add-in is not loaded");
            }
            return assembly.GetType(name, true);
        }

        private object Read(string what, Func<object> read)
        {
            try
            {
                return read();
            }
            catch (Exception failure)
            {
                Exception inner = failure is TargetInvocationException && failure.InnerException != null ? failure.InnerException : failure;
                string note = what + " not read: " + inner.GetType().Name + " " + inner.Message;
                if (!notes.Contains(note))
                {
                    notes.Add(note);
                }
                return null;
            }
        }

        private static object Member(object target, string name)
        {
            return target == null ? null : Member(target.GetType(), name, target);
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
                return field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(target);
            }
            throw new MissingMemberException(type.FullName, name);
        }

        private static object Call(Type type, string name, object target, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(name, Any);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method.Invoke(target, arguments);
        }
    }
}
