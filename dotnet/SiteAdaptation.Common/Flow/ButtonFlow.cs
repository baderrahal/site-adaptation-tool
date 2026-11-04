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
        private readonly RunTiming timing;
        private readonly StepRecorder recorder;

        public ButtonFlow(IRunState state, IModel model, IConfirm confirm, Func<RunInput, string> hash, Func<string> newRunId, RunTiming timing = null, StepRecorder recorder = null)
        {
            this.recorder = recorder;
            this.state = state;
            this.model = model;
            this.confirm = confirm;
            this.hash = hash;
            this.newRunId = newRunId;
            this.timing = timing;
        }

        // The label is the one of the ribbon, such as "7 Place villas", given by the add-in
        public RunOutcome Run(int number, string label, ButtonBody body, string documentPath)
        {
            return Run(number, label, body, documentPath, null);
        }

        // R375. ended takes the end of a body with Start, which comes after this returns Started, or before when the work ends at
        // once. The flow does not tell the state of that end, the caller does, on the document of the run, which may no longer be
        // the active one when the end comes (docs/design/02-folders.md, Revit does not wait)
        public RunOutcome Run(int number, string label, ButtonBody body, string documentPath, Action<RunOutcome> ended)
        {
            using (timing?.Enter())
            {
                try
                {
                    RunOutcome outcome = Flowed(number, label, body, documentPath, ended);
                    if (outcome.End == RunEnd.Started)
                    {
                        timing?.WriteSoFar();
                    }
                    else
                    {
                        timing?.Finish(outcome.End.ToString());
                    }
                    return outcome;
                }
                catch
                {
                    timing?.Finish(RunTiming.Threw);
                    throw;
                }
            }
        }

        private RunOutcome Flowed(int number, string label, ButtonBody body, string documentPath, Action<RunOutcome> ended)
        {
            if (body.NotBuilt != null)
            {
                return Stop(body.NotBuilt);
            }
            if (body.Start != null && ended == null)
            {
                return Stop(RunTexts.ChangedNothing(label, RunTexts.Failed("the run has no way to take the end of its worker")));
            }
            if (!SavedPath.Is(documentPath))
            {
                return Stop(RunTexts.SaveFirst);
            }
            // A button with no number, Update from report, is no step of the pane and waits for none
            int missing = number > 0 ? state.MissingStep(number) : 0;
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
            timing?.Begin(number, label, runId, work);
            timing?.NextStep(RunTiming.HashesStep, RunTiming.Reading);
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
            timing?.NextStep(RunTiming.PreviewStep, RunTiming.Reading);
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
            bool apply;
            timing?.NextStep(RunTiming.ConfirmStep);
            try
            {
                apply = body.Confirm != null ? body.Confirm(preview, summary) : confirm.Apply(label, summary, dryRun);
            }
            catch (Exception failure)
            {
                return Stop(RunTexts.ChangedNothing(label, RunTexts.Failed(failure.Message)));
            }
            if (!apply)
            {
                return new RunOutcome(RunEnd.Cancelled, null, null);
            }
            if (dryRun)
            {
                timing?.NextStep(RunTiming.HashesAgainStep, RunTiming.Reading);
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
            var real = new RunContext(number, label, runId, work, null, true, inputs, hashes) { Timing = timing };
            return body.Start != null ? Later(body, real, ended) : Real(body, real);
        }

        // R375. A body with no model element opens no transaction. Its end is taken once, a failure is the whole message of the stop
        // and records nothing, a done end records the run with the outputs the work wrote on their names
        private RunOutcome Later(ButtonBody body, RunContext context, Action<RunOutcome> ended)
        {
            int taken = 0;
            timing?.NextStep(RunTiming.WorkerStartStep);
            Action<RunLater> end = later =>
            {
                if (System.Threading.Interlocked.Exchange(ref taken, 1) == 1)
                {
                    return;
                }
                RunOutcome outcome = RecordedAlone(context, Ended(context, later));
                // Before the end is told, since the caller shows its dialog there and the wait of a user is no time of the run
                timing?.Finish(outcome.End.ToString());
                ended(outcome);
            };
            try
            {
                body.Start(context, end);
            }
            catch (Exception failure)
            {
                if (System.Threading.Interlocked.Exchange(ref taken, 1) == 1)
                {
                    return new RunOutcome(RunEnd.Started, null, null);
                }
                return Stop(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message)));
            }
            timing?.NextStep(RunTiming.WorkerStep, RunTiming.WorkerWait);
            return new RunOutcome(RunEnd.Started, null, null);
        }

        private static RunOutcome Ended(RunContext context, RunLater later)
        {
            if (later == null || later.Failure != null)
            {
                return Stop(later == null ? RunTexts.LaterFailed(context.Label, null) : later.Failure);
            }
            var record = new RunRecord(context.RunId, context.InputHashes, later.Issues, later.Outputs, new string[0]);
            return new RunOutcome(RunEnd.Committed, later.Message, record);
        }

        // R498. A body with no transaction records its run in a small one of its own once its work ended. The work stands when this write
        // fails, so the step is done for this session, a reopen asks for it again and the message says so
        private RunOutcome RecordedAlone(RunContext context, RunOutcome outcome)
        {
            if (recorder == null || !StepRecorder.Records(context.Button) || outcome.Record == null)
            {
                return outcome;
            }
            string reason = WrittenAlone(context, transaction => recorder.Write(context, outcome.Record.Issues, transaction, outcome.Record.Outputs));
            if (reason == null)
            {
                return outcome;
            }
            string told = RunTexts.RecordNotWritten(reason);
            return new RunOutcome(outcome.End, outcome.Message == null ? told : outcome.Message + " " + told, outcome.Record);
        }

        // Null when the transaction committed, else why not
        private string WrittenAlone(RunContext context, Action<IModelTransaction> write)
        {
            try
            {
                using (IModelTransaction transaction = model.Start(RunTexts.TransactionName(context.Label)))
                {
                    write(transaction);
                    if (transaction.Commit())
                    {
                        return null;
                    }
                    transaction.RollBack();
                    return transaction.Errors.Count > 0 ? RunTexts.Refused(transaction.Errors) : RunTexts.NotCommitted;
                }
            }
            catch (Exception failure)
            {
                return failure.Message;
            }
        }

        private static RunOutcome Stop(string message)
        {
            return new RunOutcome(RunEnd.Stopped, message, null);
        }

        // A button never guesses an input: one not chosen or not on disk stops it, by the first such input
        private static string Absent(string label, IReadOnlyList<RunInput> inputs)
        {
            foreach (RunInput input in inputs.Where(item => item.Text == null))
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
            IDisposable outside;
            if (body.Outside != null)
            {
                timing?.NextStep(RunTiming.OutsideStep);
            }
            try
            {
                outside = body.Outside == null ? null : body.Outside(context);
            }
            catch (Exception failure)
            {
                context.DeleteOutputs();
                return new RunOutcome(RunEnd.RolledBack, RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message)), null);
            }
            try
            {
                return Transacted(body, context);
            }
            finally
            {
                if (outside != null)
                {
                    timing?.NextStep(RunTiming.OutsideEndStep);
                }
                outside?.Dispose();
            }
        }

        // R498. A group around the run, so the write a commit still asks for after it falls into one undo entry with the step
        private RunOutcome Transacted(ButtonBody body, RunContext context)
        {
            StepRecorder recording = StepRecorder.Records(context.Button) ? recorder : null;
            IModelGroup group;
            try
            {
                group = recording == null ? null : (model as IModelGroups)?.StartGroup(RunTexts.TransactionName(context.Label));
            }
            catch (Exception failure)
            {
                return new RunOutcome(RunEnd.RolledBack, RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message)), null);
            }
            using (group)
            {
                return Transacted(body, context, recording, group);
            }
        }

        private RunOutcome Transacted(ButtonBody body, RunContext context, StepRecorder recording, IModelGroup group)
        {
            var issues = new List<string>();
            int warnings;
            int beforeCommit;
            timing?.NextStep(RunTiming.ApplyStep);
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
                    context.Transaction = transaction;
                    context.Model = model;
                    issues.AddRange(body.Apply(context) ?? new string[0]);
                    timing?.NextStep(RunTiming.ReadBackStep, RunTiming.Reading);
                    string difference = body.ReadBack == null ? null : body.ReadBack(context);
                    if (difference != null)
                    {
                        reason = RunTexts.ReadBackDiffers(difference);
                    }
                    else
                    {
                        recording?.Write(context, issues, transaction);
                    }
                }
                catch (Exception failure)
                {
                    reason = RunTexts.Failed(failure.Message);
                }
                timing?.NextStep(RunTiming.CommitStep);
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
                    group?.RollBack();
                    context.DeleteOutputs();
                    return new RunOutcome(RunEnd.RolledBack, RunTexts.ChangedNothing(context.Label, reason), null);
                }
                warnings = transaction.Warnings.Count;
                beforeCommit = issues.Count;
                issues.AddRange(transaction.Warnings);
                try
                {
                    body.AfterCommit?.Invoke(context);
                }
                catch (Exception failure)
                {
                    issues.Add("after the commit: " + failure.Message);
                }
                issues.AddRange(context.IssuesAfterCommit);
            }

            timing?.NextStep(RunTiming.OutputsStep);
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
            List<string> issuesAfter = issues.Skip(beforeCommit + warnings).ToList();
            if (recording != null && recording.NeedsSettling(warnings, issuesAfter.Count, moved.Count, failed.Count))
            {
                string notSettled = WrittenAlone(context, transaction => recording.Settle(context, warnings, issuesAfter, moved, failed.Count, transaction));
                if (notSettled != null)
                {
                    issues.Add(RunTexts.RecordNotWritten(notSettled));
                }
            }
            group?.Keep();
            var record = new RunRecord(context.RunId, context.InputHashes, issues, moved, failed);
            if (context.Button > 0 && !context.KeepsLastRun)
            {
                state.Committed(context.Button, record);
            }
            string message = failed.Count > 0 ? RunTexts.HeldOpen(context.Label, RunTexts.Names(failed)) : body.Done?.Invoke(context);
            return new RunOutcome(RunEnd.Committed, message, record);
        }
    }
}
