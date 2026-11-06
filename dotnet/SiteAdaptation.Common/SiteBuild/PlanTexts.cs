using System.Globalization;

namespace SiteAdaptation.Common.SiteBuild
{
    // R640. Every line a person reads from the plan, in plain CAD and Revit words. A line never holds braces, the word null or a code name of the tool
    internal static class PlanTexts
    {
        public const string NoPlots = "The plot table holds no plot, so there is nothing to build. Read the data again with button 2 and check the drawing folder.";
        public const string NoOutlines = "No plot of the table has an outline from the drawing, so there is nothing to draw in the model. Read the data again with button 2.";
        public const string CoordinatesNoOutline = "The coordinates cannot be taken from the drawing because no plot has an outline.";
        public const string PositionFileNoPoint = "The position file holds no east and north, so it is not used as a check.";
        public const string NoMassesGiven = "No masses were given, only the plot outlines are checked for overlap.";
        public const string NoPlacedFloors = "No floors placed by the tool are known, so no floor is marked to be replaced.";
        public const string NoRoadEdgePoints = "No road edge points were given, so none is checked against the cut.";

        public static string TableUnreadable(string reason)
        {
            return "The plot table cannot be read, so Build stops: " + Plain(reason) + ". Read the data again with button 2.";
        }

        public static string PlotWithoutNumber(string reason)
        {
            return "A plot row has no plot number and is left out" + (string.IsNullOrEmpty(reason) ? "." : ": " + Plain(reason) + ".");
        }

        public static string PlotTwice(string number)
        {
            return "Plot " + number + " is in the table twice. The first row is used and the second is left out.";
        }

        public static string RuleTextUnreadable(string id, string text)
        {
            return "The text of rule " + id + " is not in a form the build can read a number from, so that number stays empty and marked: " + Plain(text);
        }

        public static string RulesNotUsed(System.Collections.Generic.IEnumerable<string> ids)
        {
            return "The build takes no number from these rules yet, so they change nothing in the plan: " + string.Join(", ", ids) + ".";
        }

        public static string FileUnreadable(string role, string reason)
        {
            return "The file " + role + " cannot be read, so it is not used: " + Plain(reason);
        }

        public static string FileUnderstood(string role, string expected)
        {
            return "The file " + role + " holds no " + expected + ", so it is not used.";
        }

        public static string ItemLeftOut(string role, string what)
        {
            return "In the file " + role + " " + what + " is left out.";
        }

        public static string CoordinatesNoSettings(string message)
        {
            return "The coordinate settings of the config cannot be used, so the coordinates stay empty: " + Plain(message);
        }

        public static string PositionFileUnreadable(string message)
        {
            return "The position file cannot be read, so it is not used as a check: " + Plain(message);
        }

        public static string Overlap(string kind, string one, string other, double limitMm)
        {
            string name = kind == "masses" ? "The masses" : "The outlines";
            return name + " of plot " + one + " and plot " + other + " overlap by more than " + Mm(limitMm) + " mm.";
        }

        public static string OverlapMore(string kind, int more)
        {
            return "And " + more + " more " + (kind == "masses" ? "masses" : "plot outlines") + " overlap by more than the limit. The count is in the plan.";
        }

        public static string OverlapLine(string kind, int checkedCount, int count, double limitMm)
        {
            return (kind == "masses" ? "Masses" : "Plot outlines") + ": " + checkedCount + " checked, " + count + " pairs overlap by more than " + Mm(limitMm) + " mm.";
        }

        public static string FloorChanged(string plot)
        {
            return "The floor of plot " + plot + " placed by the tool has an outline that differs from the plot now. Button 5 replaces it.";
        }

        public static string FloorsLine(int checkedCount, int changed)
        {
            return "Floors placed by the tool: " + checkedCount + " checked, " + changed + " have a changed outline and are replaced by button 5.";
        }

        public static string FloorWithoutPlot(string plot)
        {
            return "A floor placed by the tool is for plot " + plot + ", which is not in the plot table. It is left as it is.";
        }

        public static string CutLine(int areas, double cutM2, double hullM2)
        {
            return "Toposolid: " + areas + " neighbourhoods are cut to their own boundary, " + Square(cutM2) + " m2 in all, where a hull of the plots would hold " + Square(hullM2) + " m2.";
        }

        public static string CutNoArea(System.Collections.Generic.IList<string> plots)
        {
            return plots.Count == 1 ? "Plot " + plots[0] + " has no neighbourhood, so the toposolid cut leaves it out." : plots.Count + " plots have no neighbourhood, so the toposolid cut leaves them out.";
        }

        public static string RoadEdgeLine(int checkedCount, int over, double limitMm)
        {
            return "Road edge points: " + checkedCount + " checked, " + over + " lie more than " + Mm(limitMm) + " mm from the cut.";
        }

        public static string RoadEdgePoint(string area, double mm)
        {
            return "A road edge point of neighbourhood " + area + " lies " + Square(mm) + " mm from the cut.";
        }

        public static string StepLine(int button, string name, int ready, int total)
        {
            return button + " " + name + ": " + ready + " of " + total + " plots are ready.";
        }

        public static string Progress(int total, int ready, int marked, int issues)
        {
            return "Build plan: " + total + " plots, " + ready + " ready for every step, " + marked + " marked, " + issues + " issues.";
        }

        public static string BlockedProgress(string why)
        {
            return "Build stops: " + why;
        }

        public static string FrontEdgeWhy
        {
            get { return "The front edge of the plot is not in the plot table, so the level at the front cannot be found."; }
        }

        public static string RoadLevelWhy
        {
            get { return "No road surface is given, so the road level at the front is empty."; }
        }

        public static string FflWhy
        {
            get { return "The floor level needs the road level at the front and a rule for its height above the road, and neither is there."; }
        }

        public static string NoRuleWhy(string what)
        {
            return "No rule of the project gives " + what + ", so it stays empty.";
        }

        public static string NoValueWhy(string what)
        {
            return "The plot table holds no " + what + " for this plot.";
        }

        public static string NoOutlineWhy
        {
            get { return "The plot has no usable outline in the plot table."; }
        }

        public static string Mm(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Square(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        // Braces and the word null never reach a person, a message of a library may hold either
        private static string Plain(string text)
        {
            if (text == null)
            {
                return "no reason given";
            }
            return text.Replace("{", "(").Replace("}", ")").Replace("null", "empty").Replace("Null", "Empty").Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
