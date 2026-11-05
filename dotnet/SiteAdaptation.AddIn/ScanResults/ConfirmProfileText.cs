namespace SiteAdaptation.AddIn.ConfirmProfile
{
    // R636. The words of the window Confirm what was found and of its end, listed in docs/design/14-ui-labels.md under What the tool read,
    // since the label gate files a window by its folder. A word in angle brackets is filled when it is shown
    public static class ConfirmProfileText
    {
        public const string Title = "Confirm what was found";
        public const string IntroFound = "The tool read the folder. Below is what it found and what was confirmed before. Nothing here changes the model.";
        public const string IntroNothing = "The tool read the folder and has nothing to propose here. Fill in what you know. Nothing here changes the model.";
        public const string ConfirmMeans = "Confirm takes both columns and the code map as they stand here, as your own values, with each hand you picked and each word you typed. They are saved for the next Read data, which then reads the folder again.";
        public const string Typology = "Villa type of each plot";
        public const string TypologyHelp = "The sheet column that gives each plot its villa type. Leave it on none to keep what the drawings give.";
        public const string TypologyName = "Villa type column";
        public const string Area = "Area of each plot";
        public const string AreaHelp = "The sheet column that gives each plot its area. Leave it on none to group the plots by distance.";
        public const string AreaName = "Area column";
        public const string NoneChoice = "none";
        public const string ColumnLabel = "<column> in <sheet> of <workbook>";
        public const string ColumnPlots = ", <n> plots";
        public const string ColumnProposed = " (proposed)";
        public const string ColumnConfirmed = " (confirmed)";
        public const string CodeMap = "Code map";
        public const string CodeMapHelp = "One pair per line, the old code, an arrow -> and the new code. Type it when the tool found no table.";
        public const string CodeMapFound = "Proposed from <source>.";
        public const string CodeMapConfirmed = "Confirmed on an earlier read.";
        public const string CodeMapNone = "No table of old and new codes was found.";
        public const string CodeMapGuess = "Which side is old is only a guess, read left to right. Check the arrows before you confirm.";
        public const string CodeMapUnsure = "Unsure, the drawings hold none of its old codes. Check the table before you confirm.";
        public const string CodeLine = "Line <n> of the code map needs the old code, -> and the new code, like A1 -> B1.";
        public const string CodeEdge = "Line <n> of the code map holds a code that starts or ends with - or >.";
        public const string Hands = "Hand of each prototype";
        public const string HandsHelp = "Right when its blocks are drawn unmirrored, left when mirrored. A row left empty confirms no hand.";
        public const string HandsGuessHelp = "A guess comes from how the blocks are drawn, no prototype file was opened. It changes nothing until you pick a hand.";
        public const string HandsNone = "No prototype code was found in the villa blocks.";
        public const string NoGuess = "no guess";
        public const string GuessLine = "guess: <hand>";
        public const string Levels = "Group levels";
        public const string LevelsHelp = "Type the word you call a level of plot groups. A word the tool found stays as found until you change it.";
        public const string LevelRow = "Level <n>";
        public const string LevelBox = "Word of level <n>";
        public const string LevelFound = "found by the tool";
        public const string LevelConfirmed = "confirmed on an earlier read";
        public const string LevelFromArea = "the header of the area column";
        public const string LevelsNone = "No group levels were found.";
        public const string Confirm = "Confirm";
        public const string Cancel = "Cancel";
        public const string CancelledLine = "Nothing was saved. The read stays as it is.";
        public const string NothingGiven = "Nothing was confirmed, so no file was written.";
        public const string Saved = "Your values are saved. Press Read data again to read with them.";
        public const string Removed = "Every block is empty, so the values confirmed earlier were removed. Press Read data again to read without them.";
        public const string NotSaved = "Nothing was saved: <why>";
        public const string NoCheckFile = "the read wrote no check result";
        public const string NoProposalFile = "the read wrote no proposals";
        public const string CheckStopped = "the data check stopped before it finished: <why>";
        public const string DiscardAsk = "The values you confirmed cannot be used: <why> Apply discards them, then press Read data again to read the folder without them. Cancel keeps them and the read does not run.";
        public const string Discarded = "The values you confirmed were discarded. Press Read data again to read the folder without them.";
    }
}
