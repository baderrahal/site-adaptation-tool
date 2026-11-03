using System.Collections.Generic;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R450, R451. The thin Revit side of the mass files. It reads what a prototype holds and writes the file Common decides, never
    // choosing the base itself
    public interface IMassFileMaker
    {
        // The prototype opened detached from a copy, never written: every wall as WallRingBuilder takes it, and the outer loop of its
        // largest ground slab, both in internal plan metres
        MassBaseRead ReadBase(string prototypePath, LightModeConfig config);

        // A new Revit project holding one DirectShape of Generic Models, the ring extruded up by heightM from Z 0, an arc kept as an arc
        // (R503), saved as massPath
        void Write(string massPath, IReadOnlyList<RingEdge> edges, double heightM);
    }

    public sealed class MassBaseRead
    {
        public IReadOnlyList<RingWall> Walls { get; set; } = new List<RingWall>();
        public IReadOnlyList<double[]> Slab { get; set; }
    }
}
