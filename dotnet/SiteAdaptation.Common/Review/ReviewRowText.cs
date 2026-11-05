namespace SiteAdaptation.Common.Review
{
    // R639. What a row of the Review data window says of a value a finished read did not find, held against docs/design/14-ui-labels.md word for
    // word by UiLabelTests, a value written <n> as the list writes it. Not read yet is false of such a plot and hides the reason the read holds.
    // No cluster here: only the plan of 4 Check gives one, and no read holds a reason for it
    public static class ReviewRowText
    {
        public const string NoType = "no type: <n>";
        public const string NoMirror = "no mirror: <n>";
        public const string NoReason = "the read gives no reason";
    }
}
