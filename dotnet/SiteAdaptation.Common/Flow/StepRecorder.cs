using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        // Inside the open transaction, so a roll back takes the write back with the model
        public void Write(RunContext context, IReadOnlyList<string> issues, IModelTransaction transaction)
        {
            ProjectSettings settings = settingsNow();
            if (!Records(context.Button) || settings == null)
            {
                return;
            }
            before = store.LastRun(context.Button);
            SettingsScope scope = settings.Scope ?? new SettingsScope();
            written = new ButtonRun
            {
                Time = clock().ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                Mode = settings.DryRunFirst ? RunModes.AfterDryRun : RunModes.Direct,
                Scope = new SettingsScope { Kind = scope.Kind, Nbh = scope.Nbh, Plots = scope.Plots },
                InputHashes = new SortedDictionary<string, string>(context.InputHashes.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal),
                Issues = (issues ?? new string[0]).ToList(),
            };
            store.Record(settings, context.Button, written, RunKind.Real, transaction);
        }

        // What only the commit and the move of the outputs can tell asks for a second write: the warnings of Revit, or a failed move, after
        // which the step is not done and the run before it stands
        public bool NeedsSettling(int warnings, int failedMoves)
        {
            return written != null && (warnings > 0 || failedMoves > 0);
        }

        public void Settle(int button, int warnings, int failedMoves, IModelTransaction transaction)
        {
            if (failedMoves > 0)
            {
                store.PutBack(settingsNow(), button, before, transaction);
                return;
            }
            written.Counts[ButtonRun.WarningsCount] = warnings;
            store.Record(settingsNow(), button, written, RunKind.Real, transaction);
        }
    }
}
