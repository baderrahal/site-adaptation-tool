using System.Collections.Generic;

namespace SiteAdaptation.Common.ProjectRules
{
    // R660, R661. The commands of the rules per project, the worker calls Add so this lane needs no line in the worker
    public static class ProjectRulesCommands
    {
        public static void Add(IDictionary<string, WorkerCommand> commands)
        {
            commands["rulesSetup"] = RulesSetup.Run;
            commands["rulesChange"] = RulesChange.Run;
        }
    }
}
