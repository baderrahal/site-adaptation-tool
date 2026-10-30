using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.CheckTable
{
    // R376. Save overrides of one check table window. The window sends the rows whose Use differs from the opening value at each Save,
    // so each Save writes those changes over the list as it stood at the first Save: a second Save writes what one Save of the same
    // choices writes and never an entry twice, and rows set back after a Save leave the list byte for byte as the table opened on it.
    // A list changed on disk since the last Save of the window is refused, never written over
    public sealed class OverrideSession
    {
        private readonly string path;
        private string opening;
        private string written;

        public OverrideSession(string path)
        {
            this.path = path;
        }

        // True when the list on disk differs from the one the table opened on, so the plan is built again
        public bool Changed => opening != null && !string.Equals(written, opening, StringComparison.Ordinal);

        // The number of changes of the session written
        public int Save(IReadOnlyList<CheckRow> rows, IReadOnlyList<CheckChoice> uses, JObject config, string runId)
        {
            string now = WorkFiles.ReadText(path);
            if (opening == null)
            {
                opening = now;
                written = now;
            }
            else if (!string.Equals(now, written, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(Path.GetFileName(path) + " changed on disk since the last Save overrides of this table, so it is not written over. Close the table and run 4 Check again (R376)");
            }
            OverrideEdits.Edited edit = OverrideEdits.Apply(opening, path, rows, uses, config);
            if (!string.Equals(edit.Text, now, StringComparison.Ordinal))
            {
                WorkFiles.WriteText(path, runId, edit.Text);
            }
            written = edit.Text;
            return edit.Count;
        }
    }
}
