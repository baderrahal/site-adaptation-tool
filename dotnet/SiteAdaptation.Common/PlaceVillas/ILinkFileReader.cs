namespace SiteAdaptation.Common.PlaceVillas
{
    // R452. The file a placed link shows, read apart from IVillaDocument so a link whose transform holds but whose file is not the one
    // the run links, a mass where the run links prototypes or the prototype the plan gave before, is swapped and not kept as placed
    public interface ILinkFileReader
    {
        // The absolute path of the file of the type of the link instance, null for a solid, an element that is gone or a type with no
        // file reference, which the run then keeps by its transform alone
        string LinkFileOf(string uniqueId);
    }
}
