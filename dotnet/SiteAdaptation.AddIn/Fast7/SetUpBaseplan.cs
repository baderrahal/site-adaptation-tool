using System;
using System.Collections.Generic;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.Fast7
{
    // R768, F2 of run 1 of FAST-3. Set up site takes the Baseplan of the chosen project folder, so no person browses the Baseplan row of Advanced
    // before step 1. The file is the one FolderRoles.Of of R740 finds by the name config gives, the same the Build fills its rows by. Only the
    // Baseplan row, and only when it is empty or its file is gone: a row a person set stays
    public static class SetUpBaseplan
    {
        // The path taken into the Baseplan row, null when the row stays as it was
        public static string Fill(PaneSettings settings, Action<string, string> pick)
        {
            if (settings == null || pick == null)
            {
                return null;
            }
            string taken = null;
            Fast5.BuildRead.FillRows(settings, (key, path) =>
            {
                if (key == InputKeys.Baseplan)
                {
                    pick(key, path);
                    taken = path;
                }
            });
            return taken;
        }
    }
}
