namespace SiteAdaptation.AddIn.ConfirmProfile
{
    // R636. The words of the window Confirm what was found, listed in docs/design/14-ui-labels.md
    public static class ConfirmProfileText
    {
        public const string Title = "Confirm what was found";
        public const string Intro = "The tool read the folder and proposes the values below. Nothing here changes the model. What you confirm is saved for the next Read data, which then reads the folder again.";
        public const string Typology = "Villa type of each plot";
        public const string TypologyHelp = "The sheet column that gives each plot its villa type. Leave it on none to keep what the drawings give.";
        public const string Area = "Area of each plot";
        public const string AreaHelp = "The sheet column that gives each plot its area. Leave it on none to group the plots by distance.";
        public const string NoneChoice = "none";
        public const string CodeMap = "Code map";
        public const string CodeMapHelp = "One pair per line, the old code then the new code. Type it when the tool found no table.";
        public const string CodeMapFound = "Proposed from {0}.";
        public const string CodeMapNone = "No table of old and new codes was found.";
        public const string Hands = "Hand of each prototype";
        public const string HandsHelp = "Right when its blocks are drawn unmirrored, left when mirrored. The guess comes from how the blocks are drawn, no prototype file was opened.";
        public const string HandsNone = "No prototype code was found in the villa blocks.";
        public const string Levels = "Group levels";
        public const string LevelsHelp = "The word you call each level of plot groups. Leave it empty to give none.";
        public const string LevelsNone = "No group levels were found.";
        public const string Confirm = "Confirm";
        public const string Cancel = "Cancel";
        public const string CancelledLine = "Nothing was saved. The read stays as it is.";
        public const string NothingGiven = "Nothing was confirmed, so no file was written.";
        public const string Saved = "Saved {0}. Press Read data again to read with it.";
        public const string NotSaved = "Nothing was saved: {0}";
    }
}
