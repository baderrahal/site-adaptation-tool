#if NETFRAMEWORK
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.PlaceVillas;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests.FAST12
{
    // R805 to R807, F31. Step 7 links prototype files of an older Revit: the box counts the new link types and says they upgrade, the
    // upgrade box of Revit is closed by its id alone and the issues list each file upgraded. The files and the folder are made up
    public sealed class LinkUpgradeTests
    {
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "fast12-prototypes");
        private readonly LightModeConfig config = LightModeConfig.Load(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json"));

        private static PlanPlot Plot(string number, string file)
        {
            return new PlanPlot { Plot = number, PrototypeFile = new Sourced<string> { Value = file } };
        }

        private VillaInputs Inputs(string mode, params PlanPlot[] plots)
        {
            return new VillaInputs { Plan = new PlanFile { PrototypeFolder = Folder, Plots = plots.ToList() }, Config = config, Mode = mode };
        }

        [Fact]
        public void R805_only_the_upgrade_box_is_known()
        {
            Assert.True(LinkUpgrade.Knows("TaskDialog_Views_Related_To_Analytical_Changed"));
            Assert.False(LinkUpgrade.Knows("TaskDialog_Unresolved_References"));
            Assert.False(LinkUpgrade.Knows("taskdialog_views_related_to_analytical_changed"));
            Assert.False(LinkUpgrade.Knows(null));
            Assert.Equal(8, LinkUpgrade.Close);
        }

        [Fact]
        public void R805_a_file_is_older_by_its_year_and_a_version_not_read_is_not()
        {
            Assert.True(LinkUpgrade.Older("2021", "2024"));
            Assert.False(LinkUpgrade.Older("2024", "2024"));
            Assert.False(LinkUpgrade.Older("2025", "2024"));
            Assert.False(LinkUpgrade.Older(null, "2024"));
            Assert.False(LinkUpgrade.Older("Autodesk Revit 2021", "2024"));
        }

        [Fact]
        public void R807_new_files_are_those_with_no_loaded_type_in_the_scope_once_each()
        {
            VillaInputs inputs = Inputs(VillaModes.Links, Plot("1", "A.rvt"), Plot("2", "A.rvt"), Plot("3", "B.rvt"), Plot("4", "C.rvt"), Plot("5", "D.rvt"), Plot("6", "E.rvt"));
            inputs.InScope = number => number != "6";
            var types = new List<VillaLinkType>
            {
                new VillaLinkType { FullPath = Path.Combine(Folder, "b.RVT"), Key = "1", Loaded = true },
                new VillaLinkType { FullPath = Path.Combine(Folder, "C.rvt"), Key = "2", Loaded = false },
                new VillaLinkType { FullPath = Path.Combine(Folder, "D.rvt"), Key = "3", Loaded = true, Usable = false },
            };

            IReadOnlyList<string> files = LinkUpgrade.NewFiles(inputs, types);

            Assert.Equal(new[] { "A.rvt", "C.rvt", "D.rvt" }, files.Select(Path.GetFileName).ToArray());
        }

        [Fact]
        public void R807_masses_and_a_filter_are_kept()
        {
            Assert.Empty(LinkUpgrade.NewFiles(Inputs(VillaModes.Masses, Plot("1", "A.rvt")), new List<VillaLinkType>()));
            VillaInputs filtered = Inputs(VillaModes.Links, Plot("1", "A.rvt"), Plot("2", "B.rvt"));
            filtered.PrototypeFilter = "B.rvt";
            Assert.Equal(new[] { "B.rvt" }, LinkUpgrade.NewFiles(filtered, new List<VillaLinkType>()).Select(Path.GetFileName).ToArray());
            Assert.Empty(LinkUpgrade.NewFiles(null, null));
        }

        [Fact]
        public void R807_the_box_counts_the_new_types_and_says_the_older_ones_upgrade()
        {
            var summary = new RunSummary("Links: place 62, link types to load 0, to reload 0", new[] { "plot 1: place" });
            var files = new[] { "A.rvt", "B.rvt", "C.rvt", "D.rvt" }.Select(name => Path.Combine(Folder, name)).ToList();
            var versions = new Dictionary<string, string> { [files[0]] = "2021", [files[1]] = "2021", [files[2]] = "2024", [files[3]] = null };

            RunSummary shown = LinkUpgrade.Add(summary, files, file => versions[file], "2024");

            Assert.Equal("Links: place 62, link types to load 0, to reload 0, link types new to the model 4, of an older Revit 2", shown.Counts);
            Assert.Equal("link A.rvt is a Revit 2021 file, Revit upgrades it to 2024 as it loads, which takes minutes", shown.PerPlot[0]);
            Assert.Equal("link B.rvt is a Revit 2021 file, Revit upgrades it to 2024 as it loads, which takes minutes", shown.PerPlot[1]);
            Assert.Equal("link D.rvt: its Revit version did not read, it may be upgraded as it loads", shown.PerPlot[2]);
            Assert.Equal("plot 1: place", shown.PerPlot[3]);
            Assert.Equal(4, shown.PerPlot.Count);
        }

        [Fact]
        public void R807_no_new_type_leaves_the_box_as_it_was()
        {
            var summary = new RunSummary("Links: place 0", new[] { "plot 1: already placed" });
            Assert.Same(summary, LinkUpgrade.Add(summary, new List<string>(), file => "2021", "2024"));
        }

        [Fact]
        public void R806_the_issues_list_each_upgraded_file_and_the_boxes_closed()
        {
            var upgraded = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Path.Combine(Folder, "A.rvt"), "2021"),
                new KeyValuePair<string, string>(Path.Combine(Folder, "B.rvt"), "2021"),
            };

            IReadOnlyList<string> lines = LinkUpgrade.IssueLines(upgraded, "2024", 4);

            Assert.Equal(new[]
            {
                "link A.rvt was a Revit 2021 file and was upgraded to 2024 as it loaded (R806)",
                "link B.rvt was a Revit 2021 file and was upgraded to 2024 as it loaded (R806)",
                "the tool closed the Revit box Structural Analytical Model Upgrade 4 times while the links loaded (R805)",
            }, lines);
            Assert.Equal("the tool closed the Revit box Structural Analytical Model Upgrade 1 time while the links loaded (R805)", LinkUpgrade.IssueLines(null, "2024", 1).Single());
            Assert.Empty(LinkUpgrade.IssueLines(null, "2024", 0));
        }

        // The Revit side cannot run here, so its wiring is read from the source: the run of button 7 takes the wrapped document, the
        // body takes the count and the issues, and the handler is on only around a load
        [Fact]
        public void R805_to_R807_are_wired_into_button_7()
        {
            string folder = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "PlaceVillas");
            string body = File.ReadAllText(Path.Combine(folder, "PlaceVillasBody.cs"));
            string document = File.ReadAllText(Path.Combine(folder, "UpgradingVillaDocument.cs"));

            Assert.Contains("var document = new UpgradingVillaDocument(new RevitVillaDocument(application.ActiveUIDocument.Document), application);", body);
            Assert.Contains("return Upgrades(run.Body(), document);", body);
            Assert.Contains("LinkUpgrade.Add(preview(context), LinkUpgrade.NewFiles(Load(context), document.LinkTypes())", body);
            Assert.Contains("apply(context).Concat(document.TakeIssues())", body);
            Assert.Contains("public string CreateType(string fullPath) => Watch(fullPath, () => inner.CreateType(fullPath));", document);
            Assert.Contains("public string LoadType(string key) => Watch(PathOf(key), () => inner.LoadType(key));", document);
            Assert.Contains("public string ReloadType(string key) => Watch(PathOf(key), () => inner.ReloadType(key));", document);
            Assert.Contains("LinkUpgrade.Knows(args.DialogId) && args.OverrideResult(LinkUpgrade.Close)", document);
            int on = document.IndexOf("application.DialogBoxShowing += OnDialog;");
            int off = document.IndexOf("application.DialogBoxShowing -= OnDialog;");
            Assert.True(on > 0 && off > on && document.IndexOf("finally", on) < off);
        }
    }
}
#endif
