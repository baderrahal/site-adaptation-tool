using System;
using System.Collections.Generic;
using System.IO;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Flow
{
    // R498. Writes the run of buttons 2 to 9 into the Settings entity, so a reopen shows each step as it was saved. Button 1 writes its
    // own through SetUpRun, a button with no number is no step. One recorder lives for one run of the flow
    public sealed class StepRecorder
    {
        private const int SetUp = 1;

        private readonly Func<ProjectSettings> settingsNow;
        private readonly SettingsStore store;
        private readonly Func<DateTime> clock;
        private ButtonRun before;
        private ButtonRun written;
        private DateTime? begun;
        private SettingsScope scopeBegun;

        public StepRecorder(Func<ProjectSettings> settingsNow, ISettingsEntity entity, Func<DateTime> clock)
        {
            this.settingsNow = settingsNow ?? throw new ArgumentNullException(nameof(settingsNow));
            store = new SettingsStore(entity);
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public static bool Records(int button)
        {
            return button > SetUp;
        }

        // A worker runs on with the pane live, so a pick or a press of another step in that time is no part of its run. Its time and scope are
        // those of its start
        public void Begin()
        {
            begun = clock();
            scopeBegun = Copy(settingsNow()?.Scope);
        }

        // Inside the open transaction, so a roll back takes the write back with the model
        public void Write(RunContext context, IReadOnlyList<string> issues, IModelTransaction transaction, IReadOnlyDictionary<string, string> outputs = null)
        {
            ProjectSettings settings = settingsNow();
            if (!Records(context.Button) || settings == null)
            {
                return;
            }
            before = store.LastRun(context.Button);
            written = ButtonRun.Of(begun ?? clock(), settings, scopeBegun ?? Copy(settings.Scope), context.InputHashes, issues ?? new string[0]);
            written.Outputs = Named(context, outputs);
            store.Record(settings, context.Button, written, RunKind.Real, transaction);
        }

        // What only the commit and the move of the outputs can tell asks for a second write: the warnings of Revit, an issue after the commit,
        // the outputs as moved, or a failed move, after which the step is not done and the run before it stands
        public bool NeedsSettling(int warnings, int issuesAfter, int outputs, int failedMoves)
        {
            return written != null && (warnings > 0 || issuesAfter > 0 || outputs > 0 || failedMoves > 0);
        }

        public void Settle(RunContext context, int warnings, IReadOnlyList<string> issuesAfter, IReadOnlyDictionary<string, string> outputs, int failedMoves, IModelTransaction transaction)
        {
            if (failedMoves > 0)
            {
                store.PutBack(settingsNow(), context.Button, before, transaction);
                return;
            }
            if (warnings > 0)
            {
                written.Counts[ButtonRun.WarningsCount] = warnings;
            }
            written.Issues.AddRange(issuesAfter);
            written.Outputs = Named(context, outputs);
            store.Record(settingsNow(), context.Button, written, RunKind.Real, transaction);
        }

        private static SettingsScope Copy(SettingsScope scope)
        {
            SettingsScope from = scope ?? new SettingsScope();
            return new SettingsScope { Kind = from.Kind, Nbh = from.Nbh, Plots = from.Plots };
        }

        // An output outside the work folder keeps its full name
        private static SortedDictionary<string, string> Named(RunContext context, IReadOnlyDictionary<string, string> outputs)
        {
            if (outputs == null || outputs.Count == 0)
            {
                return null;
            }
            string folder = context.WorkFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var named = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> output in outputs)
            {
                named[output.Key.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? output.Key.Substring(folder.Length) : output.Key] = output.Value;
            }
            return named;
        }
    }
}
