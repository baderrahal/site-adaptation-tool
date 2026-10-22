using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common
{
    // The slice of one Revit document the reader needs. RevitPrototypeReader fills it from the Revit API and tests fill it by hand
    public interface IModelSource
    {
        // Problems met on single elements, one line each. The reader copies them into the output
        IList<string> Errors { get; }

        IEnumerable<SourceLink> Links();

        // Family instances placed in the document, with every instance and type parameter
        IEnumerable<PrototypeRow> Instances();

        IEnumerable<WallRow> Walls();

        // Floors placed in this document itself, not the ones inside its links
        IEnumerable<FloorRow> Floors();

        IEnumerable<RampRow> Ramps();

        // Spot elevation annotations of this document
        IEnumerable<SpotRow> SpotElevations();
    }

    public class SourceLink
    {
        public string File { get; set; }
        public LinkTransform Transform { get; set; }

        // Name and parameters of the link instance. They do not depend on the link being loaded
        public string Name { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        // Problems met while reading the name or the parameters, one line each
        public IList<string> Problems { get; set; }

        // Gives the linked document, or null when the link is not loaded
        public Func<IModelSource> Open { get; set; }
    }
}
