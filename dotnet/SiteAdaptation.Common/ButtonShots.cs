using System.IO;

namespace SiteAdaptation.Common
{
    // Lane B 6a (R384). The pictures of a buttons job with shots true: each window the host answers, drawn before its answer, the pane
    // after button 1 and at the end, the ribbon and the main window, New project and How it works at the end. The window names are the
    // names of the picture test of R372, so a shot of a run and its baseline pair up by name
    public static class ButtonShots
    {
        public const string Folder = "shots";
        public const string End = "end";
        public const string SetUpSite = "setup-site";
        public const string ReviewData = "review-data";
        public const string CheckTable = "check-table";
        public const string Rules = "rules";
        public const string UpdateFromReport = "update-from-report";
        public const string NewProject = "new-project";
        public const string HowItWorks = "how-it-works";
        public const string Pane = "pane";
        public const string Ribbon = "ribbon";
        public const string Window = "window";

        // The PNG of one window at one step of the run, step the name of a ButtonsStep or End
        public static string File(string runFolder, string step, string window)
        {
            return Path.Combine(runFolder, Folder, step + "-" + window + ".png");
        }

        // Button 1 sets the site up, so the pane shows the state the later buttons start from. The pane at the end is taken apart
        public static bool PaneAfter(ButtonsStep step)
        {
            return step.Command == null && step.Button == 1;
        }
    }
}
