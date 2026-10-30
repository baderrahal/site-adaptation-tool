using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.CheckTable
{
    // R376. Save overrides of one check table window. The window sends the rows whose Use differs from the opening value at each Save,
    // so each Save writes those changes over the list as it stood at the first Save: a second Save writes what one Save of the same
    // choices writes and never an entry twice, and rows set back after a Save leave the list byte for byte as the table opened on it.
    // The first Save holds the list against the SHA-256 the plan was built on, so an edit on disk between the build and that Save is
    // refused and never merged in. A list changed on disk since the last Save of the window is refused, never written over
    public sealed class OverrideSession
    {
        private readonly string path;
        private readonly string builtSha256;
        private string opening;
        private string written;

        // builtSha256 is the SHA-256 of the list the plan of the run was built on, of the inputs of the build
        public OverrideSession(string path, string builtSha256)
        {
            this.path = path;
            this.builtSha256 = builtSha256;
        }

        // True when the list on disk differs from the one the table opened on, so the plan is built again
        public bool Changed => opening != null && !string.Equals(written, opening, StringComparison.Ordinal);

        public static string ChangedSinceBuild(string path)
        {
            return Path.GetFileName(path) + " changed on disk since 4 Check built the plan on it, so Save overrides does not write over it. Close the table and run 4 Check again (R376)";
        }

        public static string NotAsBuilt(string path)
        {
            return Path.GetFileName(path) + " on disk is not the list this run of 4 Check built the plan on or saved, so the run does not end done and writes no check.json. Run 4 Check again (R376)";
        }

        // The number of changes of the session written
        public int Save(IReadOnlyList<CheckRow> rows, IReadOnlyList<CheckChoice> uses, JObject config, string runId)
        {
            string now;
            if (opening == null)
            {
                // The hash before and after the read, so an edit while it reads is not taken for the list of the build
                bool before = Built();
                now = WorkFiles.ReadText(path);
                if (!before || !Built())
                {
                    throw new InvalidOperationException(ChangedSinceBuild(path));
                }
                opening = now;
                written = now;
            }
            else
            {
                now = WorkFiles.ReadText(path);
                if (!string.Equals(now, written, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(Path.GetFileName(path) + " changed on disk since the last Save overrides of this table, so it is not written over. Close the table and run 4 Check again (R376)");
                }
            }
            OverrideEdits.Edited edit = OverrideEdits.Apply(opening, path, rows, uses, config);
            if (!string.Equals(edit.Text, now, StringComparison.Ordinal))
            {
                WorkFiles.WriteText(path, runId, edit.Text);
            }
            written = edit.Text;
            return edit.Count;
        }

        // True when the list on disk is the one the run stands on at the close of the window: the list of the build when no Save ran,
        // else the list the last Save wrote, which a build again then takes
        public bool OnDisk()
        {
            if (!File.Exists(path))
            {
                return false;
            }
            return opening == null ? Built() : string.Equals(WorkFiles.ReadText(path), written, StringComparison.Ordinal);
        }

        private bool Built()
        {
            return File.Exists(path) && string.Equals(FileHash.Sha256(path), builtSha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
