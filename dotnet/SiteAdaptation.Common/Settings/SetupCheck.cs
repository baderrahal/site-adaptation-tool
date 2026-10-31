using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.Settings
{
    public enum SetupState
    {
        Found,
        Missing,
        Later,
    }

    public sealed class SetupRowState
    {
        public SetupRowState(SetupRow row, SetupState state, IReadOnlyList<string> paths)
        {
            Row = row;
            State = state;
            Paths = paths;
        }

        public SetupRow Row { get; }
        public SetupState State { get; }

        // The path of each input of the row as the settings hold it, null where none is chosen
        public IReadOnlyList<string> Paths { get; }
    }

    public sealed class SetupCheckResult
    {
        public SetupCheckResult(IReadOnlyList<SetupRowState> rows, bool hasStart, bool startNamed)
        {
            Rows = rows;
            HasStart = hasStart;
            StartNamed = startNamed;
        }

        // True when the settings name a config file, found on disk or not
        public bool StartNamed { get; }

        public IReadOnlyList<SetupRowState> Rows { get; }

        // False when the settings name no config file or one the disk does not hold, so no button can run, whatever the files
        public bool HasStart { get; }

        // The rows a button needs that are missing, in the order of the board, those the tool stops on
        public IEnumerable<SetupRowState> Stops
        {
            get { return Rows.Where(row => row.State == SetupState.Missing && !row.Row.Optional); }
        }

        public int Ready
        {
            get { return Rows.Count(row => row.State == SetupState.Found); }
        }

        // The rows that can be ready, a row that waits on Lane A counts in neither
        public int Total
        {
            get { return Rows.Count(row => row.State != SetupState.Later); }
        }

        public bool Ok
        {
            get { return HasStart && !Stops.Any(); }
        }
    }

    // R431, Check files of the New project window. Read only: it asks the disk through there and changes neither the settings nor
    // the model. A row is found when every input it fills names a path the disk holds, as a file or, for a folder row, a folder
    public static class SetupCheck
    {
        public static SetupCheckResult Run(ProjectSettings settings, Func<string, bool, bool> there)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (there == null)
            {
                throw new ArgumentNullException(nameof(there));
            }
            var rows = new List<SetupRowState>();
            foreach (SetupRow row in SetupRows.All)
            {
                if (row.Later)
                {
                    rows.Add(new SetupRowState(row, SetupState.Later, new string[0]));
                    continue;
                }
                string[] paths = row.Inputs.Select(name => settings.Inputs.TryGetValue(name, out SettingsInput input) && input != null && !string.IsNullOrEmpty(input.Path) ? input.Path : null).ToArray();
                bool found = paths.All(path => path != null && there(path, row.Folder));
                rows.Add(new SetupRowState(row, found ? SetupState.Found : SetupState.Missing, paths));
            }
            bool named = !string.IsNullOrEmpty(settings.ConfigFile);
            return new SetupCheckResult(rows, named && there(settings.ConfigFile, false), named);
        }
    }
}
