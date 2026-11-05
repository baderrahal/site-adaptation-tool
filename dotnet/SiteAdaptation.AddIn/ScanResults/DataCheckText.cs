namespace SiteAdaptation.AddIn.ScanResults
{
    // R620. The words of the section Data check of the window What the tool read: the plain name of each need and each mark. They stand in
    // the order of DataCheckRows.NeedIds and DataCheckRows.MarkIds. Every text is in docs/design/14-ui-labels.md under What the tool read
    public static class DataCheckText
    {
        public static readonly string[] Needs =
        {
            "Plot outlines", "Plot numbers", "Group levels", "Villa types", "Villa blocks", "Road edges", "Road levels", "Coordinates", "Prototypes", "Rule sets", "Shared parameters",
        };

        public static readonly string[] Marks = { "Strong", "Usable", "Weak", "Missing" };
    }
}
