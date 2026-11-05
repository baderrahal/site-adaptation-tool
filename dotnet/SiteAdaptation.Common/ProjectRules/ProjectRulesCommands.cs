using System.Collections.Generic;

namespace SiteAdaptation.Common.ProjectRules
{
    // R660. R2-C3-build-1 adds rulesSetup and rulesChange here. Until it does, the worker refuses them as not built yet, in plain words
    public static class ProjectRulesCommands
    {
        public static void Add(IDictionary<string, WorkerCommand> commands)
        {
        }
    }
}
