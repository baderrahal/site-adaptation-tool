using System;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.Settings
{
    public enum SetupWriteOutcome
    {
        Written,
        Kept,
        Failed,
    }

    public sealed class SetupWriteResult
    {
        public SetupWriteResult(SetupWriteOutcome outcome, ProjectSettings settings, string reason)
        {
            Outcome = outcome;
            Settings = settings;
            Reason = reason ?? string.Empty;
        }

        public SetupWriteOutcome Outcome { get; }

        // The settings as the model holds them after the commit, null unless written
        public ProjectSettings Settings { get; }

        public string Reason { get; }
    }

    // R445, Q280 by Bader on 1 Nov 2026. Save setup puts the setup into the model at once, through SettingsStore and the entity of R367
    // the buttons write, in one transaction that rolls back unless it commits. With no model to write to the setup is kept, as before
    // R445, and the next real run of a button writes it
    public static class SetupWrite
    {
        public const string TransactionName = "Site Adaptation: Save setup";
        public const string NotCommitted = "Revit did not commit the change";

        public static SetupWriteResult Write(ProjectSettings settings, IModel model, ISettingsEntity entity)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (model == null || entity == null)
            {
                return new SetupWriteResult(SetupWriteOutcome.Kept, null, null);
            }
            IModelTransaction transaction = model.Start(TransactionName);
            bool committed = false;
            try
            {
                ProjectSettings written = new SettingsStore(entity).Save(settings, transaction);
                committed = transaction.Commit();
                return committed
                    ? new SetupWriteResult(SetupWriteOutcome.Written, written, null)
                    : new SetupWriteResult(SetupWriteOutcome.Failed, null, transaction.Errors.Count == 0 ? NotCommitted : string.Join(" ", transaction.Errors));
            }
            finally
            {
                if (!committed)
                {
                    transaction.RollBack();
                }
                transaction.Dispose();
            }
        }
    }
}
