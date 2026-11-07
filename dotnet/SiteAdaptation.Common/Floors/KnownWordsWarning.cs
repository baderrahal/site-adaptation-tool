using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.Floors
{
    // R782, F27 of the short check of 7 Nov. A warning whose words the list of the step names, floors.knownWarnings for 5 Floors and ramps,
    // but whose floors are no pair of one plot the list accepts, Highlighted floors overlap between two plots for one. Revit showed it in its
    // own box with OK and Cancel, so an unwatched run waited there. The step takes it without a box and lists the floors in its issues, so a
    // person still sees every such overlap. Words the list does not name still show as Revit gives them
    public static class KnownWordsWarning
    {
        public const string Taken = " Taken without a box, its words are known but its floors are no pair the list accepts: ";

        // True when the words are known and the list did not accept this warning, so the step takes it and lists it
        public static bool TakeAndList(KnownWarningList known, string text, AcceptedWarning accepted)
        {
            return known != null && accepted == null && known.Knows(text);
        }

        // The issue line, each element by its plot value, type and id, as the Revit side read them
        public static string Line(string text, IEnumerable<WarningElement> elements)
        {
            List<string> named = (elements ?? Enumerable.Empty<WarningElement>()).Select(element => "plot " + (element?.Plot ?? "none") + " type " + (element?.TypeName ?? "none") + " id " + (element?.Id ?? "none")).ToList();
            return (text ?? "").Trim() + Taken + (named.Count == 0 ? "no element named" : string.Join(", ", named)) + " (R782)";
        }
    }
}
