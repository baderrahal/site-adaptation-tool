using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The dialog watcher of R108 on texts built here. Nothing here starts Revit or touches a window
    public class SecurityDialogTests
    {
        private const string Ours = "0edeabc3-f57d-4177-8427-b90d0dbf73d0";
        private static readonly string[] OurIds = { Ours, "3c6f1d52-7a0e-4b8e-9c41-5e2b8d07a915", "895f6ce7-9b1d-4b94-bf39-608da3e13c01" };
        private static readonly string[] OurMarks = { "Site Adaptation test host", "SiteAdaptation.TestHost.dll", "RevitPrototypeReader.dll" };
        private static readonly string[] Buttons = { "Always Load", "Load Once", "Do Not Load" };
        private const string ReaderDll = @"C:\Users\x\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\RevitPrototypeReader.dll";
        private const string HostDll = @"C:\Users\x\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationTestHost\SiteAdaptation.TestHost.dll";
        private static readonly string[] OurPaths = { ReaderDll, HostDll, @"C:\Users\x\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationPlacer\SiteAdaptation.Placer.dll" };

        private static SecurityDialogEntry Dialog(string name, string id, string[] buttons = null, string vendor = "Some Vendor")
        {
            var texts = new[]
            {
                "The publisher of this add-in could not be verified. What do you want to do?",
                name == null ? null : "Name: " + name,
                vendor == null ? null : "Vendor: " + vendor,
                id == null ? null : "AddInId: {" + id + "}",
            };
            return SecurityDialogs.Read("Security - Unsigned Add-In", "#32770", texts, buttons ?? Buttons);
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

        [Fact]
        public void R108_the_name_vendor_and_guid_are_read_from_the_dialog_text()
        {
            SecurityDialogEntry entry = Dialog("Green Audit", "51C9C81C-C2E2-4D7E-8301-1CCBB187A928");

            Assert.Equal("Green Audit", entry.AddInName);
            Assert.Equal("Some Vendor", entry.Vendor);
            Assert.Equal("51c9c81c-c2e2-4d7e-8301-1ccbb187a928", entry.AddInId);
            Assert.Equal(Buttons, entry.Buttons);
        }

        [Fact]
        public void R108_another_add_in_gets_do_not_load()
        {
            Assert.Equal(WatchAction.DoNotLoad, SecurityDialogs.Decide(Dialog("Green Audit", "51c9c81c-c2e2-4d7e-8301-1ccbb187a928"), OurIds, OurMarks, OurPaths));
            Assert.Equal(WatchAction.DoNotLoad, SecurityDialogs.Decide(Dialog("Green Audit", null), OurIds, OurMarks, OurPaths));
        }

        [Fact]
        public void R108_an_add_in_of_this_tool_stops_by_guid_name_or_dll()
        {
            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(Dialog("Anything", Ours.ToUpperInvariant()), OurIds, OurMarks, OurPaths));
            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(Dialog("Site Adaptation test host", null), OurIds, OurMarks, OurPaths));
            SecurityDialogEntry byPath = SecurityDialogs.Read("Security - Unsigned Add-In", "#32770", new[] { "Name: X", @"Path: C:\Addins\RevitPrototypeReader.dll" }, Buttons);
            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(byPath, OurIds, OurMarks, OurPaths));
        }

        [Fact]
        public void R108_a_dialog_it_cannot_read_or_with_no_do_not_load_stops_revit()
        {
            Assert.Equal(WatchAction.StopUnreadable, SecurityDialogs.Decide(Dialog(null, null), OurIds, OurMarks, OurPaths));
            Assert.Equal(WatchAction.StopNoButton, SecurityDialogs.Decide(Dialog("Green Audit", null, new[] { "OK" }), OurIds, OurMarks, OurPaths));
            Assert.Equal(WatchAction.StopNoButton, SecurityDialogs.Decide(Dialog("Green Audit", null, new[] { "Always Load", "Load Once" }), OurIds, OurMarks, OurPaths));
        }

        [Fact]
        public void R108_any_standard_dialog_or_security_window_counts_and_the_revit_main_window_does_not()
        {
            Assert.True(SecurityDialogs.IsDialog("#32770", "Autodesk Revit 2024"));
            Assert.True(SecurityDialogs.IsDialog("HwndWrapper", "Security - Unsigned Add-In"));
            Assert.False(SecurityDialogs.IsDialog("Afx:00007FF6", "Autodesk Revit 2024.3"));
            // The WPF main window of Revit, class Window and no title, holds the security box as a child and is not a dialog itself
            Assert.False(SecurityDialogs.IsDialog("Window", ""));
        }

        // The text as the Revit 2024 box shows it: one text block with padded labels and no GUID
        private static SecurityDialogEntry RevitBox(string name, string location)
        {
            var texts = new[]
            {
                "The publisher of this add-in could not be verified. What do you want to do?",
                "Name:                " + name + "\r\nPublisher:           Unknown Publisher\r\nLocation:            " + location + "\r\nIssuer:                 None\r\nDate:                   2026-09-20 13:43:03\r\n\r\nMake sure that this add-in comes from a trusted source.",
            };
            return SecurityDialogs.Read("Security - Unsigned Add-In", "#32770", texts, Buttons);
        }

        [Fact]
        public void R108_the_revit_2024_box_gives_name_publisher_and_location_and_no_guid()
        {
            SecurityDialogEntry entry = RevitBox("Other Audit", @"C:\Addins\2024\OtherAudit\Other.Audit.dll");

            Assert.Equal("Other Audit", entry.AddInName);
            Assert.Equal("Unknown Publisher", entry.Vendor);
            Assert.Equal(@"C:\Addins\2024\OtherAudit\Other.Audit.dll", entry.Location);
            Assert.Null(entry.AddInId);
            Assert.Equal(WatchAction.DoNotLoad, SecurityDialogs.Decide(entry, OurIds, OurMarks, OurPaths));
        }

        [Fact]
        public void R108_a_box_that_names_a_dll_of_this_tool_somewhere_else_stops_revit()
        {
            SecurityDialogEntry entry = RevitBox("Renamed", @"C:\Addins\2024\SiteAdaptationTestHost\SiteAdaptation.TestHost.dll");

            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(entry, OurIds, OurMarks, OurPaths));
            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(RevitBox("X", ReaderDll + ".old"), OurIds, OurMarks, OurPaths));
        }

        [Fact]
        public void R108_the_box_of_an_installed_dll_of_this_tool_gets_load_once()
        {
            SecurityDialogEntry reader = RevitBox("RevitPrototypeReader.ExportPrototypesCommand", ReaderDll);
            SecurityDialogEntry host = RevitBox("Site Adaptation test host", HostDll.ToUpperInvariant());

            Assert.Equal(WatchAction.LoadOnceOurs, SecurityDialogs.Decide(reader, OurIds, OurMarks, OurPaths));
            Assert.Equal(WatchAction.LoadOnceOurs, SecurityDialogs.Decide(host, OurIds, OurMarks, OurPaths));
            Assert.Equal(SecurityDialogs.LoadOnce, SecurityDialogs.ButtonFor(WatchAction.LoadOnceOurs));
        }

        [Fact]
        public void R108_an_installed_dll_of_this_tool_with_no_load_once_button_stops_revit()
        {
            SecurityDialogEntry entry = SecurityDialogs.Read("Security - Unsigned Add-In", "#32770", new[] { "Name: X", "Location: " + ReaderDll }, new[] { "Always Load", "Do Not Load" });

            Assert.Equal(WatchAction.StopNoButton, SecurityDialogs.Decide(entry, OurIds, OurMarks, OurPaths));
        }

        [Fact]
        public void R108_no_action_ever_clicks_the_button_that_keeps_an_answer()
        {
            foreach (WatchAction action in Enum.GetValues(typeof(WatchAction)).Cast<WatchAction>())
            {
                string button = SecurityDialogs.ButtonFor(action);
                Assert.True(button == null || button == SecurityDialogs.DoNotLoad || button == SecurityDialogs.LoadOnce);
            }
            Assert.Equal(SecurityDialogs.DoNotLoad, SecurityDialogs.ButtonFor(WatchAction.DoNotLoad));
            Assert.Null(SecurityDialogs.ButtonFor(WatchAction.StopOurs));
            Assert.False(SecurityDialogs.IsLoadOnce("Always Load"));
            Assert.True(SecurityDialogs.IsLoadOnce(" Load Once "));
        }

        [Fact]
        public void R108_only_do_not_load_counts_as_do_not_load()
        {
            Assert.True(SecurityDialogs.IsDoNotLoad(" Do Not Load "));
            Assert.True(SecurityDialogs.IsDoNotLoad("Do not load\nThe add-in is skipped"));
            Assert.False(SecurityDialogs.IsDoNotLoad("Always Load"));
            Assert.False(SecurityDialogs.IsDoNotLoad("Load Once"));
            Assert.False(SecurityDialogs.IsDoNotLoad(null));
        }

        [Fact]
        public void R108_the_watcher_clicks_only_the_button_the_decision_names_and_never_always_load()
        {
            string program = RepoText("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs");

            Assert.DoesNotContain("Always Load", program);
            Assert.DoesNotContain("Always Load", RepoText("dotnet", "SiteAdaptation.Common", "SecurityDialogs.cs"));
            Assert.Single(Regex.Matches(program, @"\.Invoke\(\)").Cast<Match>());
            Assert.Single(Regex.Matches(program, @"PostMessage\(new IntPtr").Cast<Match>());
            Assert.Contains("bool wanted = label == SecurityDialogs.DoNotLoad ? SecurityDialogs.IsDoNotLoad(part.Current.Name) : SecurityDialogs.IsLoadOnce(part.Current.Name);", program);
            int check = program.IndexOf("if (!IsButton(part) || !wanted)", StringComparison.Ordinal);
            int invoke = program.IndexOf(".Invoke()", StringComparison.Ordinal);
            int message = program.IndexOf("PostMessage(new IntPtr(buttonHandle), ButtonClick", StringComparison.Ordinal);
            Assert.True(check > 0 && check < invoke && invoke < message);
            Assert.Contains("string label = SecurityDialogs.ButtonFor(action);", program);
            Assert.Contains("if (label != null && Click(window, entry, label))", program);
            Assert.Contains("the dialog stayed open after \" + label", program);
        }

        [Fact]
        public void R108_the_watcher_looks_inside_the_main_window_and_keys_dialogs_by_runtime_id()
        {
            string program = RepoText("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs");

            Assert.Contains("foreach (AutomationElement window in Candidates(tops))", program);
            Assert.Contains("top.FindAll(TreeScope.Children, inner)", program);
            Assert.Contains("string.Join(\".\", element.GetRuntimeId())", program);
            Assert.DoesNotContain("new HashSet<int>()", program);
            Assert.Contains("part.Current.ClassName == \"CCPushButton\"", program);
        }

        [Fact]
        public void R108_the_watcher_stops_on_the_first_test_host_step_or_after_its_minutes()
        {
            string program = RepoText("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs");

            Assert.Contains("public double Minutes = 10;", program);
            Assert.Contains("TestHostStarted(options.StopLog, report.StartedUtc)", program);
            Assert.Contains("steps != null && steps.HasValues", program);
        }

        [Fact]
        public void R108_the_run_script_takes_the_guids_of_this_tool_from_the_repo_addin_files()
        {
            string script = RepoText("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");

            foreach (string manifest in new[] { "SiteAdaptation.addin", "SiteAdaptationPlacer_Revit2024.addin", "SiteAdaptationTestHost_Revit2024.addin" })
            {
                Assert.Contains(manifest, script);
            }
            Assert.Contains("SiteAdaptation.DialogWatcher.exe", script);
            Assert.Contains("securityDialogs", script);
        }
    }
}
