using Autodesk.Revit.UI;

namespace SiteAdaptation.AddIn
{
    // R371, docs/design/08-install.md. The one entry of a button, the ribbon command and later the test host call it, so no run
    // logic is copied. The transaction wrapper of R382 comes behind it in 5a PR 4
    public static class Buttons
    {
        public static string NotBuiltText(int number)
        {
            return RibbonLayout.Button(number).Label + " is not built yet.";
        }

        public static Result Run(int number, UIApplication application)
        {
            TaskDialog.Show(RibbonLayout.Tab, NotBuiltText(number));
            return Result.Succeeded;
        }
    }
}
