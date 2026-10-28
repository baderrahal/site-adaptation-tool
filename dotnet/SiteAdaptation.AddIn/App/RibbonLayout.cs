using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.AddIn
{
    // R371. The tab, the panels and the buttons of item 1 of docs/addin-mock.md, in its order and its words. No Revit type, so a
    // test holds it against the mock and App builds the ribbon from it
    public static class RibbonLayout
    {
        public const string Tab = "Site Adaptation";
        public const string AvailabilityClass = "SiteAdaptation.AddIn.Commands.ProjectAvailability";

        public static readonly IReadOnlyList<RibbonPanelSpec> Panels = new[]
        {
            new RibbonPanelSpec("Setup", new[]
            {
                new RibbonButtonSpec(1, "1 Set up site", "SetUpSiteCommand"),
                new RibbonButtonSpec(2, "2 Read data", "ReadDataCommand"),
                new RibbonButtonSpec(3, "3 Check", "CheckCommand"),
            }),
            new RibbonPanelSpec("Build", new[]
            {
                new RibbonButtonSpec(4, "4 Place villas", "PlaceVillasCommand"),
                new RibbonButtonSpec(5, "5 Floors and ramps", "FloorsAndRampsCommand"),
                new RibbonButtonSpec(6, "6 Levels", "LevelsCommand"),
                new RibbonButtonSpec(7, "7 Walls", "WallsCommand"),
            }),
            new RibbonPanelSpec("Output", new[]
            {
                new RibbonButtonSpec(8, "8 Report", "ReportCommand"),
            }),
        };

        public static IEnumerable<RibbonButtonSpec> Buttons
        {
            get { return Panels.SelectMany(panel => panel.Buttons); }
        }

        public static RibbonButtonSpec Button(int number)
        {
            RibbonButtonSpec found = Buttons.FirstOrDefault(button => button.Number == number);
            if (found == null)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "the ribbon has buttons 1 to 8 only");
            }
            return found;
        }

        // A family document is no project, the buttons work on the site model only
        public static bool IsAvailable(bool documentOpen, bool familyDocument)
        {
            return documentOpen && !familyDocument;
        }
    }

    public sealed class RibbonPanelSpec
    {
        public RibbonPanelSpec(string name, IReadOnlyList<RibbonButtonSpec> buttons)
        {
            Name = name;
            Buttons = buttons;
        }

        public string Name { get; }
        public IReadOnlyList<RibbonButtonSpec> Buttons { get; }
    }

    public sealed class RibbonButtonSpec
    {
        public RibbonButtonSpec(int number, string label, string commandName)
        {
            Number = number;
            Label = label;
            CommandClass = "SiteAdaptation.AddIn.Commands." + commandName;
        }

        public int Number { get; }
        public string Label { get; }
        public string CommandClass { get; }
        public string IconKey32 { get { return "SA.Icon." + Number + ".32"; } }
        public string IconKey16 { get { return "SA.Icon." + Number + ".16"; } }
    }
}
