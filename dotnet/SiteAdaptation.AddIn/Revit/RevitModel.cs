using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.AddIn.Revit
{
    // R382. The thin Revit side of the flow: the one transaction of a real run with its failure preprocessor
    internal sealed class RevitModel : IModel
    {
        private readonly Document document;

        public RevitModel(Document document)
        {
            this.document = document;
        }

        public IModelTransaction Start(string name)
        {
            return new RevitTransaction(document, name);
        }
    }

    internal sealed class RevitTransaction : IModelTransaction
    {
        private readonly Transaction transaction;
        private readonly WarningCollector collector = new WarningCollector();
        private readonly List<string> errors = new List<string>();

        public RevitTransaction(Document document, string name)
        {
            transaction = new Transaction(document, name);
            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(collector);
            // The TaskDialog of the flow gives the reason, so Revit shows no second dialog of its own after the roll back
            options.SetClearAfterRollback(true);
            transaction.SetFailureHandlingOptions(options);
            if (transaction.Start() != TransactionStatus.Started)
            {
                transaction.Dispose();
                throw new InvalidOperationException("Revit did not start the transaction " + name);
            }
        }

        public IReadOnlyList<string> Warnings
        {
            get { return collector.Warnings; }
        }

        public IReadOnlyList<string> Errors
        {
            get
            {
                var all = new List<string>(collector.Errors);
                all.AddRange(errors);
                return all;
            }
        }

        public bool Commit()
        {
            try
            {
                return transaction.Commit() == TransactionStatus.Committed;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException failure)
            {
                errors.Add(failure.Message);
                return false;
            }
        }

        public void RollBack()
        {
            if (transaction.HasStarted() && !transaction.HasEnded())
            {
                transaction.RollBack();
            }
        }

        public void Dispose()
        {
            transaction.Dispose();
        }
    }

    // R382. Apply or Cancel. Cancel is the default button, so Enter changes nothing. The list per plot shows after a dry run only
    internal sealed class TaskDialogConfirm : IConfirm
    {
        public bool Apply(string label, RunSummary summary, bool dryRun)
        {
            var dialog = new TaskDialog(RibbonLayout.Tab)
            {
                MainInstruction = label,
                MainContent = RunTexts.ConfirmContent(summary, dryRun),
                CommonButtons = TaskDialogCommonButtons.Cancel,
                DefaultButton = TaskDialogResult.Cancel,
            };
            string details = RunTexts.ConfirmDetails(summary, dryRun);
            if (details != null)
            {
                dialog.ExpandedContent = details;
            }
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, RunTexts.Apply);
            return dialog.Show() == TaskDialogResult.CommandLink1;
        }
    }
}
