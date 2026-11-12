using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.AddIn
{
    // R371, R846. The tab, the panels and the buttons of the ribbon, and apart from them the steps the cards run. Since Bader's decision of
    // 12 Nov 2026 the ribbon holds no step. No Revit type, so a test holds it and App builds the ribbon from Panels alone
    public static class RibbonLayout
    {
        public const string Tab = "Site Adaptation";
        public const string AvailabilityClass = "SiteAdaptation.AddIn.Commands.ProjectAvailability";
        // R494. Show pane works with no project open, so Revit does not grey it
        public const string AlwaysAvailableClass = "SiteAdaptation.AddIn.Commands.AlwaysAvailable";

        public static readonly IReadOnlyList<RibbonPanelSpec> Panels = new[]
        {
            new RibbonPanelSpec("Rules", new[]
            {
                new RibbonButtonSpec("Site Adaptation Rules", "SiteAdaptationRulesCommand"),
            }),
            new RibbonPanelSpec("Pane", new[]
            {
                new RibbonButtonSpec("Show pane", "ShowPaneCommand", false),
            }),
        };

        // R846. The 9 steps and Update from report, the engine behind the 3 cards. None is on the ribbon and none has a command class, the name
        // of each is the one Buttons.Run and a job of the test host find it by
        public static readonly IReadOnlyList<RibbonButtonSpec> Steps = new[]
        {
            new RibbonButtonSpec(1, "1 Set up site", "SetUpSiteCommand"),
            new RibbonButtonSpec(2, "2 Read data", "ReadDataCommand"),
            new RibbonButtonSpec(3, "3 Review data", "ReviewDataCommand"),
            new RibbonButtonSpec(4, "4 Check", "CheckCommand"),
            new RibbonButtonSpec(5, "5 Floors and ramps", "FloorsAndRampsCommand"),
            new RibbonButtonSpec(6, "6 Levels", "LevelsCommand"),
            new RibbonButtonSpec(7, "7 Place villas", "PlaceVillasCommand"),
            new RibbonButtonSpec(8, "8 Walls", "WallsCommand"),
            new RibbonButtonSpec(9, "9 Report", "ReportCommand"),
            new RibbonButtonSpec("Update from report", Common.ButtonsRun.UpdateCommand),
        };

        // Every button of the ribbon in its order
        public static IEnumerable<RibbonButtonSpec> AllButtons
        {
            get { return Panels.SelectMany(panel => panel.Buttons); }
        }

        // The numbered steps in the order of their numbers
        public static IEnumerable<RibbonButtonSpec> Buttons
        {
            get { return Steps.Where(button => button.Number > 0).OrderBy(button => button.Number); }
        }

        public static RibbonButtonSpec Button(int number)
        {
            RibbonButtonSpec found = Buttons.FirstOrDefault(button => button.Number == number);
            if (found == null)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "the steps are numbered " + Buttons.First().Number + " to " + Buttons.Last().Number + " only");
            }
            return found;
        }

        // A button of the ribbon or a step by the short name of its command, the way one with no number is found
        public static RibbonButtonSpec ButtonOf(string commandName)
        {
            RibbonButtonSpec found = AllButtons.Concat(Steps).FirstOrDefault(button => button.CommandClass == RibbonButtonSpec.CommandNamespace + commandName);
            if (found == null)
            {
                throw new ArgumentOutOfRangeException(nameof(commandName), commandName, "no button of the ribbon and no step has that command");
            }
            return found;
        }

        // A family document is no project, Site Adaptation Rules works on the site model only
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

        // R494. A button with no number that needs no project open
        public RibbonButtonSpec(string label, string commandName, bool needsProject) : this(label, commandName)
        {
            NeedsProject = needsProject;
        }

        // False only for Show pane
        public bool NeedsProject { get; } = true;

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
