using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The dialog watcher of R106 on texts built here. Nothing here starts Revit or touches a window
    public class SecurityDialogTests
    {
        private const string Ours = "0edeabc3-f57d-4177-8427-b90d0dbf73d0";
        private static readonly string[] OurIds = { Ours, "3c6f1d52-7a0e-4b8e-9c41-5e2b8d07a915", "895f6ce7-9b1d-4b94-bf39-608da3e13c01" };
        private static readonly string[] OurMarks = { "Site Adaptation test host", "SiteAdaptation.TestHost.dll", "RevitPrototypeReader.dll" };
        private static readonly string[] Buttons = { "Always Load", "Load Once", "Do Not Load" };

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
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray()));
        }

        [Fact]
        public void R106_the_name_vendor_and_guid_are_read_from_the_dialog_text()
        {
            SecurityDialogEntry entry = Dialog("Green Audit", "51C9C81C-C2E2-4D7E-8301-1CCBB187A928");

            Assert.Equal("Green Audit", entry.AddInName);
            Assert.Equal("Some Vendor", entry.Vendor);
            Assert.Equal("51c9c81c-c2e2-4d7e-8301-1ccbb187a928", entry.AddInId);
            Assert.Equal(Buttons, entry.Buttons);
        }

        [Fact]
        public void R106_another_add_in_gets_do_not_load()
        {
            Assert.Equal(WatchAction.DoNotLoad, SecurityDialogs.Decide(Dialog("Green Audit", "51c9c81c-c2e2-4d7e-8301-1ccbb187a928"), OurIds, OurMarks));
            Assert.Equal(WatchAction.DoNotLoad, SecurityDialogs.Decide(Dialog("Green Audit", null), OurIds, OurMarks));
        }

        [Fact]
        public void R106_an_add_in_of_this_tool_stops_by_guid_name_or_dll()
        {
            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(Dialog("Anything", Ours.ToUpperInvariant()), OurIds, OurMarks));
            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(Dialog("Site Adaptation test host", null), OurIds, OurMarks));
            SecurityDialogEntry byPath = SecurityDialogs.Read("Security - Unsigned Add-In", "#32770", new[] { "Name: X", @"Path: C:\Addins\RevitPrototypeReader.dll" }, Buttons);
            Assert.Equal(WatchAction.StopOurs, SecurityDialogs.Decide(byPath, OurIds, OurMarks));
        }

        [Fact]
        public void R106_a_dialog_it_cannot_read_or_with_no_do_not_load_stops_revit()
        {
            Assert.Equal(WatchAction.StopUnreadable, SecurityDialogs.Decide(Dialog(null, null), OurIds, OurMarks));
            Assert.Equal(WatchAction.StopNoButton, SecurityDialogs.Decide(Dialog("Green Audit", null, new[] { "OK" }), OurIds, OurMarks));
            Assert.Equal(WatchAction.StopNoButton, SecurityDialogs.Decide(Dialog("Green Audit", null, new[] { "Always Load", "Load Once" }), OurIds, OurMarks));
        }

        [Fact]
        public void R106_any_standard_dialog_or_security_window_counts_and_a_plain_window_does_not()
        {
            Assert.True(SecurityDialogs.IsDialog("#32770", "Autodesk Revit 2024", new string[0]));
            Assert.True(SecurityDialogs.IsDialog("HwndWrapper", "Security - Unsigned Add-In", new string[0]));
            Assert.True(SecurityDialogs.IsDialog("HwndWrapper", "", new[] { "Do Not Load" }));
            Assert.False(SecurityDialogs.IsDialog("Afx:00007FF6", "Autodesk Revit 2024.3", new[] { "Minimize" }));
        }

        [Fact]
        public void R106_only_do_not_load_counts_as_do_not_load()
        {
            Assert.True(SecurityDialogs.IsDoNotLoad(" Do Not Load "));
            Assert.True(SecurityDialogs.IsDoNotLoad("Do not load\nThe add-in is skipped"));
            Assert.False(SecurityDialogs.IsDoNotLoad("Always Load"));
            Assert.False(SecurityDialogs.IsDoNotLoad("Load Once"));
            Assert.False(SecurityDialogs.IsDoNotLoad(null));
        }

        [Fact]
        public void R106_the_watcher_clicks_only_do_not_load_and_names_no_other_button()
        {
            string program = RepoText("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs");

            Assert.DoesNotContain("Always Load", program);
            Assert.DoesNotContain("Load Once", program);
            Assert.Single(Regex.Matches(program, @"\.Invoke\(\)").Cast<Match>());
            int check = program.IndexOf("if (!SecurityDialogs.IsDoNotLoad(name))", StringComparison.Ordinal);
            int invoke = program.IndexOf(".Invoke()", StringComparison.Ordinal);
            Assert.True(check > 0 && check < invoke);
            Assert.Contains("action == WatchAction.DoNotLoad && Click(window, entry)", program);
        }

        [Fact]
        public void R106_the_watcher_stops_on_the_first_test_host_step_or_after_its_minutes()
        {
            string program = RepoText("dotnet", "SiteAdaptation.DialogWatcher", "Program.cs");

            Assert.Contains("public double Minutes = 10;", program);
            Assert.Contains("TestHostStarted(options.StopLog, report.StartedUtc)", program);
            Assert.Contains("steps != null && steps.HasValues", program);
        }

        [Fact]
        public void R106_the_run_script_takes_the_guids_of_this_tool_from_the_repo_addin_files()
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
