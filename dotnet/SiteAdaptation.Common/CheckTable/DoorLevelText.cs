namespace SiteAdaptation.Common.CheckTable
{
    // R443, docs/design/14-ui-labels.md Check table. The texts of the row Door level from. They live in Common so the net8.0 tests
    // reach them, and the text test of the window reads this file with the files of the window
    public static class DoorLevelText
    {
        public const string From = "Door level from";
        public const string Civil = "Civil entry file (<n> plots)";
        public const string Road = "Road level plus slope (<n> plots)";
        public const string NoCivil = "No civil entry file in the setup, so the door level comes from road level plus slope.";
    }
}
