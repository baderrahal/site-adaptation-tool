using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.AddIn
{
    // R371. The tab, the panels and the buttons of 7.1 of docs/design/12-ui-spec.md, Bader's ribbon of 28 Oct 2026, in its order
    // and its words. No Revit type, so a test holds it against the spec and App builds the ribbon from it
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
                new RibbonButtonSpec(3, "3 Review data", "ReviewDataCommand"),
                new RibbonButtonSpec(4, "4 Check", "CheckCommand"),
            }),
            new RibbonPanelSpec("Rules", new[]
            {
                new RibbonButtonSpec("Site Adaptation Rules", "SiteAdaptationRulesCommand"),
            }),
            new RibbonPanelSpec("Build", new[]
            {
                new RibbonButtonSpec(5, "5 Floors and ramps", "FloorsAndRampsCommand"),
                new RibbonButtonSpec(6, "6 Levels", "LevelsCommand"),
                new RibbonButtonSpec(7, "7 Place villas", "PlaceVillasCommand"),
                new RibbonButtonSpec(8, "8 Walls", "WallsCommand"),
            }),
            new RibbonPanelSpec("Output", new[]
            {
                new RibbonButtonSpec(9, "9 Report", "ReportCommand"),
                new RibbonButtonSpec("Update from report", "UpdateFromReportCommand"),
            }),
        };

        // Every button of the ribbon in its order, the two with no number among them
        public static IEnumerable<RibbonButtonSpec> AllButtons
        {
            get { return Panels.SelectMany(panel => panel.Buttons); }
        }

        // The numbered buttons, the steps of the pane, in the order of their numbers
        public static IEnumerable<RibbonButtonSpec> Buttons
        {
            get { return AllButtons.Where(button => button.Number > 0).OrderBy(button => button.Number); }
        }

        public static RibbonButtonSpec Button(int number)
        {
            RibbonButtonSpec found = Buttons.FirstOrDefault(button => button.Number == number);
            if (found == null)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "the ribbon has numbered buttons " + Buttons.First().Number + " to " + Buttons.Last().Number + " only");
            }
            return found;
        }

        // A button by the short name of its command class, the way a button with no number is found
        public static RibbonButtonSpec ButtonOf(string commandName)
        {
            RibbonButtonSpec found = AllButtons.FirstOrDefault(button => button.CommandClass == RibbonButtonSpec.CommandNamespace + commandName);
            if (found == null)
            {
                throw new ArgumentOutOfRangeException(nameof(commandName), commandName, "the ribbon has no button of that command");
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
        public const string CommandNamespace = "SiteAdaptation.AddIn.Commands.";

        public RibbonButtonSpec(int number, string label, string commandName)
        {
            Number = number;
            Label = label;
            CommandClass = CommandNamespace + commandName;
            Key = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // A button with no number, as Site Adaptation Rules. Its key is its label with no spaces, each word capitalised
        public RibbonButtonSpec(string label, string commandName)
        {
            Label = label;
            CommandClass = CommandNamespace + commandName;
            Key = string.Concat(label.Split(' ').Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
        }

        // 0 for a button with no number
        public int Number { get; }
        public string Label { get; }
        public string CommandClass { get; }

        // The name of the button in its icon keys and its ribbon item, unique on the ribbon
        public string Key { get; }
        public string IconKey32 { get { return "SA.Icon." + Key + ".32"; } }
        public string IconKey16 { get { return "SA.Icon." + Key + ".16"; } }
    }
}
