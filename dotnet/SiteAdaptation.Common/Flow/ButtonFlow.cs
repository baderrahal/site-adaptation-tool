using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.Flow
{
    // R382, docs/design/03a-buttons-shared-and-1.md What every button shares. The run of a button with no Revit type: the stops, the
    // confirm after a dry run or a count, the one transaction of the real run and the moves after its commit
    public sealed class ButtonFlow
    {
        private readonly IRunState state;
        private readonly IModel model;
        private readonly IConfirm confirm;
        private readonly Func<RunInput, string> hash;
        private readonly Func<string> newRunId;

        public ButtonFlow(IRunState state, IModel model, IConfirm confirm, Func<RunInput, string> hash, Func<string> newRunId)
        {
            this.state = state;
            this.model = model;
            this.confirm = confirm;
            this.hash = hash;
            this.newRunId = newRunId;
        }

        // The label is the one of the ribbon, such as "7 Place villas", given by the add-in
        public RunOutcome Run(int number, string label, ButtonBody body, string documentPath)
        {
            if (body.NotBuilt != null)
            {
                return Stop(body.NotBuilt);
            }
            if (!SavedPath.Is(documentPath))
            {
                return Stop(RunTexts.SaveFirst);
            }
            int missing = state.MissingStep(number);
            if (missing > 0)
            {
                return Stop(RunTexts.NeedsStep(label, missing));
            }
            IReadOnlyList<RunInput> inputs = state.Inputs(number) ?? new RunInput[0];
            string absent = Absent(label, inputs);
            if (absent != null)
            {
                return Stop(absent);
            }

            string work = state.WorkFolder(documentPath);
            string runId = newRunId();
            Dictionary<string, string> hashes;
            try
            {
                hashes = Hashes(inputs);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                return Stop(RunTexts.ChangedNothing(label, RunTexts.Failed(failure.Message)));
            }
            bool dryRun = state.DryRunFirst;
            var preview = new RunContext(number, label, runId, work, dryRun ? Path.Combine(work, "dry", runId) : null, dryRun, inputs, hashes);
            RunSummary summary;
            try
            {
                summary = body.Preview(preview) ?? new RunSummary(null, null);
            }
            catch (Exception failure)
            {
                preview.DeleteOutputs();
                return Stop(RunTexts.ChangedNothing(label, RunTexts.Failed(failure.Message)));
            }
            if (dryRun)
            {
                summary = MoveDryOutputs(preview, summary);
            }
            if (!confirm.Apply(label, summary, dryRun))
            {
                return new RunOutcome(RunEnd.Cancelled, null, null);
            }
            if (dryRun)
            {
                absent = Absent(label, inputs);
                if (absent != null)
                {
                    return Stop(absent);
                }
                Dictionary<string, string> again;
                try
                {
                    again = Hashes(inputs);
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    return Stop(RunTexts.ChangedNothing(label, RunTexts.Failed(failure.Message)));
                }
                List<string> changed = inputs.Where(input => again[input.Name] != hashes[input.Name]).Select(input => input.Name).ToList();
                if (changed.Count > 0)
                {
                    return Stop(RunTexts.ChangedSinceDryRun(label, changed));
                }
            }
            return Real(body, new RunContext(number, label, runId, work, null, true, inputs, hashes));
        }

        private static RunOutcome Stop(string message)
        {
            return new RunOutcome(RunEnd.Stopped, message, null);
        }

        // A button never guesses an input: one not chosen or not on disk stops it, by the first such input
        private static string Absent(string label, IReadOnlyList<RunInput> inputs)
        {
            foreach (RunInput input in inputs)
            {
                if (string.IsNullOrEmpty(input.Path))
                {
                    return RunTexts.NotChosen(label, input);
                }
                if (input.IsFolder ? !Directory.Exists(input.Path) : !File.Exists(input.Path))
                {
                    return RunTexts.NotOnDisk(label, input);
                }
            }
            return null;
        }

        private Dictionary<string, string> Hashes(IReadOnlyList<RunInput> inputs)
        {
            return inputs.ToDictionary(input => input.Name, hash, StringComparer.Ordinal);
        }

        // The dry outputs move onto their names under the dry folder. One another program holds is told in the list of the summary
        private static RunSummary MoveDryOutputs(RunContext context, RunSummary summary)
        {
            var lines = summary.PerPlot.ToList();
            foreach (KeyValuePair<string, string> output in context.Outputs)
            {
                try
                {
                    WorkFiles.Commit(output.Key, output.Value);
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    lines.Add(RunTexts.DryHeldOpen(Path.GetFileName(output.Value)));
                }
            }
            return new RunSummary(summary.Counts, lines);
        }

        private RunOutcome Real(ButtonBody body, RunContext context)
        {
            var issues = new List<string>();
            IModelTransaction started;
            try
            {
                started = model.Start(RunTexts.TransactionName(context.Label));
            }
            catch (Exception failure)
            {
                return new RunOutcome(RunEnd.RolledBack, RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message)), null);
            }
            using (IModelTransaction transaction = started)
            {
                string reason = null;
                try
                {
                    issues.AddRange(body.Apply(context) ?? new string[0]);
                    string difference = body.ReadBack == null ? null : body.ReadBack(context);
                    if (difference != null)
                    {
                        reason = RunTexts.ReadBackDiffers(difference);
                    }
                }
                catch (Exception failure)
                {
                    reason = RunTexts.Failed(failure.Message);
                }
                if (reason == null)
                {
                    try
                    {
                        if (!transaction.Commit())
                        {
                            reason = transaction.Errors.Count > 0 ? RunTexts.Refused(transaction.Errors) : RunTexts.NotCommitted;
                        }
                    }
                    catch (Exception failure)
                    {
                        reason = RunTexts.Failed(failure.Message);
                    }
                }
                if (reason != null)
                {
                    transaction.RollBack();
                    context.DeleteOutputs();
                    return new RunOutcome(RunEnd.RolledBack, RunTexts.ChangedNothing(context.Label, reason), null);
                }
                issues.AddRange(transaction.Warnings);
            }

            var moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var failed = new List<string>();
            foreach (KeyValuePair<string, string> output in context.Outputs)
            {
                try
                {
                    WorkFiles.Commit(output.Key, output.Value);
                    moved[output.Value] = FileHash.Sha256(output.Value);
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    failed.Add(output.Value);
                }
            }
            var record = new RunRecord(context.RunId, context.InputHashes, issues, moved, failed);
            state.Committed(context.Button, record);
            string message = failed.Count == 0 ? null : RunTexts.HeldOpen(context.Label, RunTexts.Names(failed));
            return new RunOutcome(RunEnd.Committed, message, record);
        }
    }
}
