using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.BoundaryWalls
{
    // R815, F32 of run 2 of 8 Nov. Revit stopped step 8 on its own box with OK and Cancel for this warning, 7.4 minutes in run 2 with no
    // one at the PC. Step 8 gives its transaction these words as known, so the collector takes the warning without a box as R782 does for
    // floors, and lists each wall Revit named in the issues of the step. A warning of other words still shows as Revit gives it
    public static class WallOverlapWarning
    {
        // The words of BuiltInFailures.OverlapFailures.WallsOverlap in RevitAPI.xml of Revit 2024, the same words as the box of run 2
        public const string Words = "Highlighted walls overlap. One of them may be ignored when Revit finds room boundaries. Use Cut Geometry to embed one wall within the other.";

        public const string Taken = "Walls overlap, taken without a box (R815): ";

        // An entry with no role pair, so KnownWarningList.Decide never accepts it apart and the collector lists it as an issue. Walls hold no
        // plot parameter, so none is read
        public static KnownWarningList List()
        {
            return new KnownWarningList(new[] { new KnownWarning { Text = Words, SamePlot = false } }, null, new Dictionary<string, IReadOnlyList<string>>());
        }

        public static bool Owns(string text)
        {
            return string.Equals((text ?? "").Trim(), Words, StringComparison.Ordinal);
        }

        // The issue line, each wall Revit named by its type and id
        public static string Line(IEnumerable<WarningElement> elements)
        {
            List<string> named = (elements ?? Enumerable.Empty<WarningElement>()).Select(element => "wall type " + (element?.TypeName ?? "none") + " id " + (element?.Id ?? "none")).ToList();
            return Taken + (named.Count == 0 ? "no wall named" : string.Join(", ", named)) + ". Revit says: " + Words;
        }
    }
}
