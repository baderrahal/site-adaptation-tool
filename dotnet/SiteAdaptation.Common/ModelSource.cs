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

        IEnumerable<RampRow> Ramps();
    }

    public class SourceLink
    {
        public string File { get; set; }
        public LinkTransform Transform { get; set; }

        // Gives the linked document, or null when the link is not loaded
        public Func<IModelSource> Open { get; set; }
    }
}
