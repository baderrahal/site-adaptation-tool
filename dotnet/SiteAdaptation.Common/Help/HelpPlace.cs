using System;

namespace SiteAdaptation.Common.Help
{
    // R558. Where the help box stands. In Revit at 133 percent every box of the 9 steps ran 150 to 670 px under the screen with Close out of
    // reach, and stood over the rows of the pane, as it was placed by the i and never held to the work area
    public struct HelpBoxSpot
    {
        public HelpBoxSpot(double left, double top, double maxHeight)
        {
            Left = left;
            Top = top;
            MaxHeight = maxHeight;
        }

        public double Left { get; }
        public double Top { get; }
        public double MaxHeight { get; }
    }

    public static class HelpPlace
    {
        // Left of the host when it fits, else right of it, else the work area edge, where only a screen too small for both overlaps it.
        // The top follows the i until the box would pass the bottom of the work area
        public static HelpBoxSpot Of(double hostLeft, double hostRight, double iconTop, double width, double height, double workLeft, double workTop, double workRight, double workBottom, double gap)
        {
            double room = Math.Max(0, workBottom - workTop);
            double tall = Math.Min(height, room);
            double left = hostLeft - gap - width >= workLeft ? hostLeft - gap - width
                : hostRight + gap + width <= workRight ? hostRight + gap
                : workLeft;
            double top = Math.Max(workTop, Math.Min(iconTop, workBottom - tall));
            return new HelpBoxSpot(left, top, room);
        }
    }
}
