using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Lane B 5a PR 6 on net48, where the add-in builds: the names the test host reads from it by reflection, and the one work root
    // the add-in and the test host take from the same machine files
    public class AddinRunAddInTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static Type TypeOf(string name)
        {
            Type type = typeof(WorkFolder).Assembly.GetType(name);
            Assert.True(type != null, "the add-in has no type " + name);
            return type;
        }

        private static string RepoText(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray()));
        }
        private static Type MemberType(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, Any);
            if (property != null)
            {
                return property.PropertyType;
            }
            FieldInfo field = type.GetField(name, Any);
            Assert.True(field != null, type.FullName + " has no member " + name);
            return field.FieldType;
        }

        private static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, Any);
            Assert.True(method != null, type.FullName + " has no method " + name);
            return method;
        }

        // Each name of AddinRunNames, walked the way AddinProbe walks it, ends on a member of the type it expects
        [Fact]
        public void R372_every_name_the_test_host_reads_by_reflection_is_a_member_of_the_add_in()
        {
            Assert.Equal(AddinRunNames.Assembly, typeof(WorkFolder).Assembly.GetName().Name);
            // PaneHost implements a Revit interface, which the tests cannot load, so its members are held by their declarations
            string host = RepoText("dotnet", "SiteAdaptation.AddIn", "App", "PaneHost.cs");
            Assert.Contains("namespace " + AddinRunNames.PaneHost.Substring(0, AddinRunNames.PaneHost.LastIndexOf('.')), host);
            Assert.Contains("public sealed class " + AddinRunNames.PaneHost.Substring(AddinRunNames.PaneHost.LastIndexOf('.') + 1) + " : IDockablePaneProvider", host);
            Assert.Contains("public static PaneHost " + AddinRunNames.Current + " { get; private set; }", host);
            Assert.Contains("public static readonly DockablePaneId " + AddinRunNames.PaneId + " = ", host);
            Assert.Contains("private readonly PaneViewModel " + AddinRunNames.Model + ";", host);
            Assert.Contains("public PaneSessions " + AddinRunNames.Sessions + "\n", host.Replace("\r\n", "\n"));
            Assert.Contains("public AnswerRelay " + AddinRunNames.Answers + " { get; private set; }", host);
            Assert.Contains("public List<RoutedAnswer> " + AddinRunNames.Routed + " { get; }", host);

            Type model = typeof(PaneViewModel);
            Assert.Equal(typeof(int), MemberType(model, AddinRunNames.Loads));
            Assert.Equal(typeof(string), MemberType(model, AddinRunNames.ProjectName));
            Type session = MemberType(model, AddinRunNames.Document);
            Assert.Equal(typeof(DocumentSession), session);
            Assert.Equal(typeof(string), MemberType(session, AddinRunNames.SessionId));

            MethodInfo find = Method(typeof(PaneSessions), AddinRunNames.Find);
            Assert.Equal(new[] { typeof(object) }, Array.ConvertAll(find.GetParameters(), parameter => parameter.ParameterType));
            Assert.Equal(session, find.ReturnType);
            Type answers = typeof(AnswerRelay);
            Assert.Equal(typeof(int), MemberType(answers, AddinRunNames.Waiting));
            MethodInfo post = answers.GetMethod(AddinRunNames.Post);
            Assert.Equal(new[] { typeof(WorkerAnswer) }, Array.ConvertAll(post.GetParameters(), parameter => parameter.ParameterType));

            Type entry = typeof(RoutedAnswer);
            Type document = MemberType(entry, AddinRunNames.RoutedDocument);
            Assert.Equal(typeof(OpenDocument), document);
            Assert.Equal(typeof(string), MemberType(document, AddinRunNames.SessionId));
            Assert.Equal(typeof(string), MemberType(document, AddinRunNames.RoutedPath));
            Assert.Equal(typeof(string), MemberType(entry, AddinRunNames.Issue));

            Type workFolder = TypeOf(AddinRunNames.WorkFolder);
            Assert.Equal(new[] { typeof(string), typeof(string), typeof(Func<string, string>) },
                Array.ConvertAll(Method(workFolder, AddinRunNames.Root).GetParameters(), parameter => parameter.ParameterType));
            Assert.NotNull(Delegate.CreateDelegate(typeof(Func<string, string>), Method(workFolder, AddinRunNames.ReadIfThere)));
            Assert.Equal(typeof(SiteAdaptation.Common.Flow.IRunState), MemberType(TypeOf(AddinRunNames.Buttons), AddinRunNames.State));
            Assert.Equal(RibbonLayout.Tab, TypeOf(AddinRunNames.RibbonLayout).GetField(AddinRunNames.Tab).GetRawConstantValue());
            Assert.Equal(typeof(SitePanePage), TypeOf(AddinRunNames.Page));
        }

        // One rule of the work root: WorkFolder.Root of the add-in and WorkRoot.Of of Common give the same root, or refuse with the same
        // text, on the same machine files
        [Fact]
        public void R372_the_add_in_and_the_test_host_take_one_work_root_from_the_same_machine_files()
        {
            const string appData = @"C:\Users\u\AppData\Roaming";
            const string documents = @"C:\Users\u\OneDrive - Org\Documents";
            string machine = WorkRoot.MachinePath(appData);
            var cases = new List<string>
            {
                null,
                "{\"workRoot\": \"C:\\\\Users\\\\u\\\\Documents\\\\SiteAdaptation\"}",
                "{\"workRoot\": \"D:\\\\Work\\\\SiteAdaptation\"}",
                "{\"workRoot\": \"C:\\\\Users\\\\u\\\\Documents\\\\Work\"}",
                "{\"workRoot\": \"Documents\\\\SiteAdaptation\"}",
                "{\"workRoot\": 3}",
                "{}",
                "not json",
            };
            int roots = 0;
            int refusals = 0;
            foreach (string text in cases)
            {
                Func<string, string> read = path => path == machine ? text : null;
                string fromAddIn = Outcome(() => WorkFolder.Root(appData, documents, read));
                string fromCommon = Outcome(() => WorkRoot.Of(appData, documents, read));
                Assert.Equal(fromCommon, fromAddIn);
                if (fromAddIn.StartsWith("refused", StringComparison.Ordinal))
                {
                    refusals++;
                }
                else
                {
                    roots++;
                    Assert.Equal(WorkRoot.Folder, Path.GetFileName(fromAddIn));
                    // The test host takes the same root as the folder of its files
                    Assert.Equal(fromAddIn, TestHostGuard.SiteAdaptationFolder(WorkRoot.TestHostDocuments(fromAddIn)));
                }
            }
            Assert.Equal(3, roots);
            Assert.Equal(5, refusals);
        }

        private static string Outcome(Func<string> root)
        {
            try
            {
                return root();
            }
            catch (InvalidDataException refused)
            {
                return "refused: " + refused.Message;
            }
        }
    }
}
