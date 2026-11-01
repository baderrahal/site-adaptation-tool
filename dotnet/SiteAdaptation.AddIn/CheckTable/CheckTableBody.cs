using System;
using System.Collections.Generic;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.CheckTable
{
    // R376. Button 4 Check on the document of the run: CheckRun of Common with the inputs of the pane by their roles, the worker of
    // button 2 and the window. Like ReviewDataBody it names no Revit type. While Dialog is null button 4 is not built (Bodies.Of)
    public static class CheckTableBody
    {
        // The pane names each input of button 4 by its row, the request by its role
        public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PaneText.Settings] = CheckRun.Config, [PaneText.Rules] = CheckRun.Rules, [PaneText.Prototypes] = CheckRun.Prototypes,
        };

        // The window for its owner and the rules file of the pane, set by App at start
        public static Func<IntPtr, string, ICheckTableDialog> Dialog { get; set; }

        // The manual rule id of a check type and field, for the rules file of the pane. PaneHost wires it to ManualRuleBlock.IdOfCheck
        public static Func<string, Func<string, string, string>> ManualRule { get; set; }

        public static bool IsSet => Dialog != null;

        // The Rules file and the Prototypes folder of the pane, beside the Settings file every button reads. The past job (key join and
        // key reads) has no row in the pane, so a run has none until a row gives it (Q117)
        public static IReadOnlyList<RunInput> Inputs(PaneSettings settings)
        {
            return new[]
            {
                new RunInput(PaneText.Rules, settings.RulesFile, false),
                new RunInput(PaneText.Prototypes, settings.InputPath(InputKeys.Prototypes), true),
            };
        }

        public static ButtonBody For(DocumentSession session, IntPtr mainWindow)
        {
            PaneSettings settings = session.Settings;
            Func<SettingsScope> scope = () => new SettingsScope { Kind = settings.Scope.Kind, Nbh = settings.Scope.Nbh, Plots = settings.Scope.Plots };
            Func<string, string, string> manualRule = ManualRule?.Invoke(settings.RulesFile);
            var run = new CheckRun(Roles, ReadDataBody.Worker, null, new WorkerDocument { Path = session.Path, SessionId = session.SessionId }, scope,
                Dialog(mainWindow, settings.RulesFile), manualRule, settings.LevelProfile);
            return DoorWired(run, settings).Body();
        }

        // R443. The pick of Door level from rides in the pane settings, written to the model with the record of the next real run (R367)
        public static CheckRun DoorWired(CheckRun run, PaneSettings settings)
        {
            run.DoorPick = () => settings.Project.DoorLevelSource;
            run.DoorPicked = source => settings.Project.DoorLevelSource = source;
            return run;
        }
    }
}
