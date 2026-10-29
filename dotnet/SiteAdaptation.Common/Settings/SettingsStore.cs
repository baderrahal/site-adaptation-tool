using System;
using System.Globalization;
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
    // with the record of a real run inside its transaction, so a dry run and Cancel write nothing and a roll back takes the write back
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
            next.LastRuns[button.ToString(CultureInfo.InvariantCulture)] = run;
            string json = SettingsJson.Write(next);
            SettingsJson.Read(json);
            entity.Write(json);
            return true;
        }
    }
}
