using System.IO;
using System.Linq;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R494. The pane returns when a document opens and the ribbon button Show pane opens it by hand. net48 only, the add-in
    public sealed class R494_PaneReturnTests
    {
        // Item 4 of Bader's list. A save dialog with no owner leaves What the tool read open to a second Export, the window owns it
        [Fact]
        public void R495_the_save_as_of_what_the_tool_read_is_owned_by_its_window()
        {
            Assert.Contains("dialog.ShowDialog(ExportOwner)", Source("ScanResults", "ScanGate.cs"));
            string show = Source("ScanResults", "ScanResultsDialog.cs");
            Assert.Contains("ScanGate.ExportOwner = window", show);
            Assert.Contains("ScanGate.ExportOwner = null", show);
        }

        private static string Source(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(new[] { ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn" }.Concat(parts).ToArray()));
        }

        [Fact]
        public void R494_a_document_open_arms_the_first_show_again_so_a_pane_hidden_in_the_session_returns()
        {
            var first = new PaneFirstShow();
            int asked = 0;
            Assert.True(first.Due(true, () => { asked++; return false; }));
            Assert.False(first.Due(true, () => { asked++; return false; }));

            first.Rearm();
            Assert.True(first.Due(true, () => { asked++; return false; }));
            Assert.Equal(2, asked);

            first.Rearm();
            Assert.False(first.Due(true, () => true));
            Assert.False(first.Due(true, () => false));
        }

        [Fact]
        public void R494_a_family_view_does_not_use_up_the_armed_show()
        {
            var first = new PaneFirstShow();
            first.Rearm();
            Assert.False(first.Due(false, () => false));
            Assert.True(first.Due(true, () => false));
        }

        [Fact]
        public void R494_the_host_arms_the_show_when_a_document_opens_or_is_created()
        {
            string host = Source("App", "PaneHost.cs");
            string opened = host.Substring(host.IndexOf("private void OnOpened", System.StringComparison.Ordinal));
            opened = opened.Substring(0, opened.IndexOf("private void OnClosing", System.StringComparison.Ordinal));

            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(opened, @"firstShow\.Rearm\(\);").Count);
        }

        [Fact]
        public void R494_the_button_says_what_happened_when_revit_shows_the_pane_or_refuses()
        {
            Assert.Null(PaneFirstShow.ByHand(() => true));
            Assert.Equal(PaneText.ShowRefused, PaneFirstShow.ByHand(() => false));
            Assert.Equal("Revit would not show the pane now. Open a project and try again.", PaneText.ShowRefused);
        }

        [Fact]
        public void R494_Show_pane_is_a_button_of_its_own_panel_that_is_available_with_no_project()
        {
            RibbonPanelSpec panel = RibbonLayout.Panels.Last();
            Assert.Equal("Pane", panel.Name);
            Assert.Equal(new[] { "Show pane" }, panel.Buttons.Select(button => button.Label));
            RibbonButtonSpec show = RibbonLayout.ButtonOf("ShowPaneCommand");
            Assert.False(show.NeedsProject);
            Assert.Equal("SA.Icon.ShowPane.32", show.IconKey32);
            Assert.All(RibbonLayout.AllButtons.Where(button => button != show), button => Assert.True(button.NeedsProject));
            Assert.Equal("SiteAdaptation.AddIn.Commands.AlwaysAvailable", RibbonLayout.AlwaysAvailableClass);

            string commands = Source("Commands", "ButtonCommands.cs");
            Assert.Contains("public sealed class AlwaysAvailable : IExternalCommandAvailability", commands);
            Assert.Contains("Buttons.Run(nameof(ShowPaneCommand), commandData.Application)", commands);
            Assert.Contains("if (commandName == nameof(Commands.ShowPaneCommand))", Source("Commands", "Buttons.cs"));
            Assert.Contains("button.NeedsProject ? RibbonLayout.AvailabilityClass : RibbonLayout.AlwaysAvailableClass", Source("App", "App.cs"));
        }
    }
}
