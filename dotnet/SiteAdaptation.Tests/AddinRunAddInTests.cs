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
            // Lane B 5b. The setUp mode runs button 1 by Buttons.Run(int, UIApplication) with its own window in SetUpSiteBody.Dialog
            // GetParameters would load RevitAPIUI, which the tests do not have, so the entry is read from its source
            Assert.Contains("public static Result " + AddinRunNames.Run + "(int number, UIApplication application)", File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Commands", "Buttons.cs")));
            Assert.Equal(typeof(Func<IntPtr, SiteAdaptation.Common.SetUp.ISetUpSiteDialog>), MemberType(TypeOf(AddinRunNames.SetUpSiteBody), AddinRunNames.Dialog));
            Assert.Equal(RibbonLayout.Tab, TypeOf(AddinRunNames.RibbonLayout).GetField(AddinRunNames.Tab).GetRawConstantValue());
            Assert.Equal(typeof(SitePanePage), TypeOf(AddinRunNames.Page));
            // The panels and buttons of the ribbon and the least width of the pane, read at run time
            Type layout = TypeOf(AddinRunNames.RibbonLayout);
            Assert.Equal(typeof(string), MemberType(MemberType(layout, AddinRunNames.Panels).GetGenericArguments()[0], AddinRunNames.PanelName));
            Assert.Equal(typeof(string), MemberType(MemberType(layout, AddinRunNames.RibbonButtons).GetGenericArguments()[0], AddinRunNames.ButtonLabel));
            var theme = (System.Windows.ResourceDictionary)Method(TypeOf(AddinRunNames.Theme), AddinRunNames.ThemeLoad).Invoke(null, null);
            Assert.True((double)theme[AddinRunNames.PaneSize] >= 1);
        }

        // R384. The named commands of a buttons job: Buttons.Run(string, UIApplication) takes them as the ribbon does, each name is a
        // command of RibbonLayout, and the host answers the window of each through the Dialog of its body
        [Fact]
        public void R384_the_host_finds_the_named_commands_and_the_dialogs_of_their_windows_in_the_add_in()
        {
            Assert.Contains("public static Result " + AddinRunNames.Run + "(string commandName, UIApplication application)",
                File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Commands", "Buttons.cs")));
            foreach (string command in ButtonsRun.Named)
            {
                Assert.Equal(0, RibbonLayout.ButtonOf(command).Number);
            }
            Assert.Equal(typeof(Func<IntPtr, SiteAdaptation.Common.Update.IUpdateFromReportDialog>), MemberType(TypeOf(AddinRunNames.UpdateFromReportBody), AddinRunNames.Dialog));
            Assert.Equal(typeof(Action<IntPtr, SiteAdaptation.Common.ManualRules.ManualRuleTable, string>), MemberType(TypeOf(AddinRunNames.RulesWindowBody), AddinRunNames.Dialog));
        }

        // R403, R384. Site Adaptation Rules opens on the block of the rules file through RulesWindowBody.Dialog, so the test host opens and
        // closes it with no click. The ribbon keeps the modal window
        [Fact]
        public void R403_R384_the_rules_window_opens_through_its_dialog_on_the_rules_of_the_file()
        {
            string rules = Path.Combine(ThemeGateTests.RepoRoot(), "rules", "roshn-makkah.json");
            Action<IntPtr, SiteAdaptation.Common.ManualRules.ManualRuleTable, string> before = SiteAdaptation.AddIn.RulesWindow.RulesWindowBody.Dialog;
            SiteAdaptation.Common.ManualRules.ManualRuleTable seen = null;
            try
            {
                SiteAdaptation.AddIn.RulesWindow.RulesWindowBody.Dialog = (owner, table, selected) => seen = table;
                SiteAdaptation.AddIn.RulesWindow.RulesWindowBody.Show(IntPtr.Zero, rules);
            }
            finally
            {
                SiteAdaptation.AddIn.RulesWindow.RulesWindowBody.Dialog = before;
            }
            Assert.NotNull(seen);
            Assert.Equal(SiteAdaptation.Common.ManualRules.ManualRuleBlock.FromFile(rules).Rules.Count, seen.Block.Rules.Count);
            Assert.True(seen.Block.Rules.Count > 0);
        }

        // The probe walks the panels and the buttons of RibbonLayout the way AddinProbe.Labels does, and gets every button of the ribbon,
        // the ones with no number among them, so the judge holds the whole ribbon of the build that runs
        [Fact]
        public void R372_the_probe_reads_the_panels_and_every_button_of_the_ribbon_from_RibbonLayout()
        {
            Type layout = TypeOf(AddinRunNames.RibbonLayout);
            Assert.Equal(RibbonLayout.Panels.Select(panel => panel.Name), Walk(layout, AddinRunNames.Panels, AddinRunNames.PanelName));
            List<string> buttons = Walk(layout, AddinRunNames.RibbonButtons, AddinRunNames.ButtonLabel);
            Assert.Equal(RibbonLayout.AllButtons.Select(button => button.Label), buttons);
            Assert.Contains(RibbonLayout.AllButtons, button => button.Number == 0);
            Assert.Equal(RibbonLayout.Panels.Sum(panel => panel.Buttons.Count), buttons.Count);
        }

        private static List<string> Walk(Type layout, string list, string name)
        {
            MemberInfo member = (MemberInfo)layout.GetProperty(list, Any) ?? layout.GetField(list, Any);
            Assert.True(member != null, layout.FullName + " has no member " + list);
            object items = member is PropertyInfo property ? property.GetValue(null) : ((FieldInfo)member).GetValue(null);
            var labels = new List<string>();
            foreach (object item in (System.Collections.IEnumerable)items)
            {
                labels.Add((string)item.GetType().GetProperty(name, Any).GetValue(item));
            }
            return labels;
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

        // Run 2 of 5a: Revit kept the pane hidden, since VisibleByDefault counts only the first time a pane is ever loaded. The add-in
        // shows it once each session, at the first project view, when Revit has it hidden
        [Fact]
        public void R372_the_pane_is_shown_once_a_session_at_the_first_project_view_when_revit_keeps_it_hidden()
        {
            var hidden = new PaneFirstShow();
            int asked = 0;
            Assert.False(hidden.Due(false, () => { asked++; return false; }));
            Assert.True(hidden.Due(true, () => { asked++; return false; }));
            Assert.False(hidden.Due(true, () => { asked++; return false; }));
            Assert.Equal(1, asked);

            var shown = new PaneFirstShow();
            Assert.False(shown.Due(true, () => true));
            Assert.False(shown.Due(true, () => false));

            string host = RepoText("dotnet", "SiteAdaptation.AddIn", "App", "PaneHost.cs");
            Assert.Contains("ShowOnce(sender as UIApplication, now);", host);
            Assert.Contains("firstShow.Due(now != null && now.IsValidObject && !now.IsFamilyDocument, () => (pane = application.GetDockablePane(PaneId)).IsShown())", host);
            Assert.Contains("data.VisibleByDefault = true;", host);
            Assert.Contains("DockPosition = DockPosition.Right,", host);
        }
    }
}