using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.Settings
{
    public enum RunKind
    {
        Dry,
        Real,
    }

    // R367. The one string field of the Settings entity on ProjectInformation. The Revit side is AddIn/Revit/SettingsEntity.cs
    public interface ISettingsEntity
    {
        // Null when the project holds no Settings entity yet
        string Read();

        void Write(string json);
    }

    // R367, 06-settings.md When. A pick changes the settings in memory only and this store has no write for it. The entity is written
    // with the record of a real run inside its transaction, so a dry run and Cancel write nothing and a roll back takes the write back.
    // Save setup writes it through Save in a transaction of its own (R445)
    public sealed class SettingsStore
    {
        private readonly ISettingsEntity entity;

        public SettingsStore(ISettingsEntity entity)
        {
            this.entity = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        public ProjectSettings Load()
        {
            string text = entity.Read();
            return string.IsNullOrEmpty(text) ? null : SettingsJson.Read(text);
        }

        // True when written. The settings of the pane are not changed, since the transaction may still roll back, the pane loads them
        // again after the commit
        public bool Record(ProjectSettings settings, int button, ButtonRun run, RunKind kind, IModelTransaction transaction)
        {
            if (kind != RunKind.Real)
            {
                return false;
            }
            if (transaction == null)
            {
                throw new InvalidOperationException("the settings are written only inside the transaction of a real run (R367)");
            }
            if (settings == null || run == null)
            {
                throw new ArgumentNullException(settings == null ? nameof(settings) : nameof(run));
            }
            // Read back from its own text, so a pick or a record the reader refuses never reaches the document
            ProjectSettings next = SettingsJson.Read(SettingsJson.Write(settings));
            next.FormatVersion = SettingsJson.Version;
            next.LastRuns = RunsHeld(next);
            next.LastRuns[button.ToString(CultureInfo.InvariantCulture)] = run;
            string json = SettingsJson.Write(next);
            SettingsJson.Read(json);
            entity.Write(json);
            return true;
        }

        // R445, Q280. Save setup writes the setup with no run record, in a transaction of its own, so a roll back takes it back as it
        // takes back the write of a run. The settings as written come back, read from their own text
        public ProjectSettings Save(ProjectSettings settings, IModelTransaction transaction)
        {
            if (transaction == null)
            {
                throw new InvalidOperationException("the settings are written only inside a transaction (R367)");
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            ProjectSettings next = SettingsJson.Read(SettingsJson.Write(settings));
            next.FormatVersion = SettingsJson.Version;
            next.LastRuns = RunsHeld(next);
            string json = SettingsJson.Write(next);
            ProjectSettings written = SettingsJson.Read(json);
            entity.Write(json);
            return written;
        }

        // R498. The last run of a button as the model holds it, null with none or with settings the reader refuses
        public ButtonRun LastRun(int button)
        {
            SortedDictionary<string, ButtonRun> held = RunsHeld(new ProjectSettings());
            return held.TryGetValue(button.ToString(CultureInfo.InvariantCulture), out ButtonRun run) ? run : null;
        }

        // R498. Puts back the last run a step had, or none, for a run that committed and whose outputs did not move (03a Steps)
        public void PutBack(ProjectSettings settings, int button, ButtonRun before, IModelTransaction transaction)
        {
            if (transaction == null)
            {
                throw new InvalidOperationException("the settings are written only inside a transaction (R367)");
            }
            ProjectSettings next = SettingsJson.Read(SettingsJson.Write(settings));
            next.FormatVersion = SettingsJson.Version;
            next.LastRuns = RunsHeld(next);
            string key = button.ToString(CultureInfo.InvariantCulture);
            if (before == null)
            {
                next.LastRuns.Remove(key);
            }
            else
            {
                next.LastRuns[key] = before;
            }
            string json = SettingsJson.Write(next);
            SettingsJson.Read(json);
            entity.Write(json);
        }

        // R498. The settings of a session keep only the runs of when it loaded them, so a write from them alone would take back what a later
        // button recorded. The run of the model wins for its button, a run only the settings give stays, and settings the reader refuses
        // in the model count as none
        private SortedDictionary<string, ButtonRun> RunsHeld(ProjectSettings given)
        {
            var runs = new SortedDictionary<string, ButtonRun>(given.LastRuns, StringComparer.Ordinal);
            try
            {
                foreach (KeyValuePair<string, ButtonRun> held in Load()?.LastRuns ?? new SortedDictionary<string, ButtonRun>(StringComparer.Ordinal))
                {
                    runs[held.Key] = held.Value;
                }
            }
            catch (InvalidDataException)
            {
                return runs;
            }
            return runs;
        }
    }
}
