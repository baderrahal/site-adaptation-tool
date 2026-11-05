using System;
using System.IO;
using System.Linq;
using System.Windows.Interop;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;

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

        public static ButtonBody Wrap(ButtonBody read, IntPtr owner)
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
                Start = (context, done) => read.Start(context, later => Ended(owner, context, later, done)),
            };
        }

        // In the API context of Revit, by the hand of the pane, as the end of the read
        private static void Ended(IntPtr owner, RunContext context, RunLater later, Action<RunLater> done)
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
                    done(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed("the read wrote no " + (check == null ? DataCheckRows.FileName : ProfileFile)))));
                    return;
                }
                string plots = Path.Combine(Path.GetDirectoryName(check), DataCheckRows.PlotsFileName);
                ShowCheck(owner, DataCheckRows.ReadResult(WorkFiles.ReadText(check), File.Exists(plots) ? WorkFiles.ReadText(plots) : null));
                var session = new ConfirmProfileSession(ConfirmProfileModel.Read(WorkFiles.ReadText(profile)), context.WorkFolder, context.RunId, 0);
                ShowConfirm(owner, session);
                // A close by the X is a Cancel, the read stays done and says nothing was saved
                string line = session.Result ?? ConfirmProfileText.CancelledLine;
                done(RunLater.Done((later.Message ?? "") + " " + line, later.Issues, later.Outputs));
            }
            catch (Exception failure)
            {
                done(RunLater.Failed(RunTexts.ChangedNothing(context.Label, RunTexts.Failed(failure.Message))));
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
