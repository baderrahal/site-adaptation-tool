using System;
using System.IO;
using System.Linq;
using System.Windows.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;

namespace SiteAdaptation.AddIn.ConfirmProfile
{
    // R636, R635. Button 2 Read data on a folder with no past files: the read runs the data check itself in the worker (no second check,
    // no scan), and once it ended done this step shows data_check.json of the read folder and then the window Confirm what was found
    // over project_profile.json of the same folder. Confirm writes confirmed_profile.json in the work folder, a new file is a new read.
    // Cancel saves nothing and the read stays done. CHOSEN by Lead: two windows one after the other, the check then the confirm
    public static class ConfirmProfileStep
    {
        public const string ProfileFile = "project_profile.json";

        // The check alone and the confirm window, stand-ins in the tests. Confirm returns the session once the window closed
        public static Action<IntPtr, DataCheckResult> ShowCheck { get; set; } = (owner, result) => ScanResultsDialog.ShowCheck(owner, result);
        public static Action<IntPtr, ConfirmProfileSession> ShowConfirm { get; set; } = (owner, session) => ConfirmProfileDialog.Show(owner, session);

        // The Apply or Cancel prompt the flow already uses, a stand-in in the tests. True when the person says to discard
        public static Func<string, string, bool> AskDiscard { get; set; } = (label, why) => new TaskDialogConfirm().Apply(label, new RunSummary(ConfirmProfileText.DiscardAsk.Replace("<why>", why), null), false);

        // R792. The line a Confirm that saved adds when the check ran again on the read just made, and the one when it could not
        public const string AppliedLine = "They are applied to this read already, so that press reads no drawing again.";
        public const string NotAppliedLine = "The confirmed values count from the next read, <why>.";

        // apply is ReadDataRun.ApplyConfirmed of the same read, null keeps a Confirm that saved for the next read only, as before R791
        public static ButtonBody Wrap(ButtonBody read, PaneSettings settings, IntPtr owner, Func<RunContext, RunLater, RunLater> apply = null)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }
            if (read.NotBuilt != null || read.Start == null)
            {
                return read;
            }
            return new ButtonBody
            {
                Preview = read.Preview, Apply = read.Apply, ReadBack = read.ReadBack, Done = read.Done, Confirm = read.Confirm, Outside = read.Outside,
                Start = (context, done) =>
                {
                    string refused = RefusedProfile(settings.ConfigFile, context.WorkFolder);
                    if (refused != null)
                    {
                        Refused(context, refused, done);
                        return;
                    }
                    read.Start(context, later => Ended(owner, context, later, done, apply));
                },
            };
        }

        // Why the worker would refuse the confirmed file of the work folder, as a person cannot edit it by hand, null when it would take it or
        // there is none. It holds the file as DataCheckCommand.Confirmed does, a settings file that does not read is said by the check itself
        private static string RefusedProfile(string configFile, string workFolder)
        {
            string path = Path.Combine(workFolder, ConfirmedProfile.FileName);
            if (!File.Exists(path))
            {
                return null;
            }
            DataCheckSettings settings;
            try
            {
                settings = DataCheckSettings.From(JObject.Parse(WorkFiles.ReadText(configFile)));
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is JsonException)
            {
                return null;
            }
            try
            {
                ConfirmedProfile.From(JObject.Parse(WorkFiles.ReadText(path)), settings.GroupGapsM.Count);
                return null;
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is JsonException)
            {
                return failure.Message;
            }
        }

        // The press cannot read again here: the flow hashed the file before its dry run and the read holds its plan to it, so the next press does
        private static void Refused(RunContext context, string why, Action<RunLater> done)
        {
            if (!AskDiscard(context.Label, why))
            {
                done(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(why))));
                return;
            }
            File.Delete(Path.Combine(context.WorkFolder, ConfirmedProfile.FileName));
            done(RunLater.Failed(ConfirmProfileText.Discarded));
        }

        // In the API context of Revit, by the hand of the pane, as the end of the read
        private static void Ended(IntPtr owner, RunContext context, RunLater later, Action<RunLater> done, Func<RunContext, RunLater, RunLater> apply)
        {
            if (later.Failure != null)
            {
                done(later);
                return;
            }
            try
            {
                string check = Find(later, DataCheckRows.FileName);
                string profile = Find(later, ProfileFile);
                if (check == null || profile == null)
                {
                    done(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(check == null ? ConfirmProfileText.NoCheckFile : ConfirmProfileText.NoProposalFile))));
                    return;
                }
                string plots = Path.Combine(Path.GetDirectoryName(check), DataCheckRows.PlotsFileName);
                DataCheckResult result = DataCheckRows.ReadResult(WorkFiles.ReadText(check), File.Exists(plots) ? WorkFiles.ReadText(plots) : null);
                // R771, F6. A step 2 the Build put back shows no check window, a check that stopped still says why in the line below
                bool quiet = Fast7.QuietRead.On;
                if (!quiet)
                {
                    ShowCheck(owner, result);
                }
                // A check that stopped holds nothing to confirm, the window above told why and the press says so too
                if (result.Stopped != null)
                {
                    done(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(ConfirmProfileText.CheckStopped.Replace("<why>", result.Stopped)))));
                    return;
                }
                // R771. The read took the profile the person confirmed last for this project, so the Build keeps it and opens no Confirm. With none
                // confirmed yet the Confirm window opens, as a Build never takes a fresh proposal in place of a person's choice
                // Lead's point of 18:20: only a profile confirmed for the folder this read asks, so a second folder of the project gets its own Confirm
                string scanFolder = Fast7.ConfirmedFor.ScanFolderOf(Path.GetDirectoryName(check));
                if (quiet && Fast7.ConfirmedFor.Holds(context.WorkFolder, scanFolder))
                {
                    done(RunLater.Done((later.Message ?? "") + " " + ScanResultsText.QuietKept, later.Issues, later.Outputs));
                    return;
                }
                var session = new ConfirmProfileSession(ConfirmProfileModel.Read(WorkFiles.ReadText(profile)), context.WorkFolder, context.RunId);
                ShowConfirm(owner, session);
                // A close by the X is a Cancel, the read stays done and says nothing was saved
                string line = session.Result ?? ConfirmProfileText.CancelledLine;
                if (session.Result == ConfirmProfileText.Saved)
                {
                    Fast7.ConfirmedFor.Write(context.WorkFolder, scanFolder);
                    if (apply != null)
                    {
                        RunLater applied = Applied(apply, context, later);
                        if (applied.Failure == null)
                        {
                            done(RunLater.Done((later.Message ?? "") + " " + line + " " + AppliedLine, applied.Issues, applied.Outputs));
                            return;
                        }
                        line += " " + NotAppliedLine.Replace("<why>", applied.Failure);
                    }
                }
                done(RunLater.Done((later.Message ?? "") + " " + line, later.Issues, later.Outputs));
            }
            catch (Exception failure)
            {
                done(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message))));
            }
        }

        // R792. A fault of the check run again is no fault of the read: the read stays done and the line says why the values wait for the next read
        private static RunLater Applied(Func<RunContext, RunLater, RunLater> apply, RunContext context, RunLater later)
        {
            try
            {
                return apply(context, later) ?? RunLater.Failed(ReadDataRun.ApplyNoCheck);
            }
            catch (Exception failure)
            {
                return RunLater.Failed(failure.GetType().Name + ": " + failure.Message);
            }
        }

        private static string Find(RunLater later, string name)
        {
            return later.Outputs.Keys.FirstOrDefault(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));
        }
    }

    // R636. The window over the Revit main window, modal. It opens no transaction and writes nothing but what the session saves
    public static class ConfirmProfileDialog
    {
        public static void Show(IntPtr owner, ConfirmProfileSession session)
        {
            var window = new ConfirmProfileWindow(session);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            session.Finished += () => window.Close();
            window.ShowDialog();
        }
    }
}
