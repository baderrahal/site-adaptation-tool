using System.Collections.Generic;

namespace CadReader
{
    public class LayerEntry
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public int Entities { get; set; }
        public Dictionary<string, int> ByType { get; set; }
        public int After { get; set; }
        public Dictionary<string, int> AfterByType { get; set; }
    }

    public class XrefEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool Found { get; set; }
        public int Inserts { get; set; }
    }

    public class NoteEntry
    {
        public string Text { get; set; }
        public int Count { get; set; }
    }

    // The DWG class list says which application objects a drawing holds, Civil 3D ones among them, which ACadSharp drops as entities (R189).
    public class ClassEntry
    {
        public string DxfName { get; set; }
        public string CppClassName { get; set; }
        public string ApplicationName { get; set; }
        public int InstanceCount { get; set; }
        public bool IsAnEntity { get; set; }
    }

    public class TotalsEntry
    {
        public int Before { get; set; }
        public int After { get; set; }
        public int Inserts { get; set; }
        public int MaxDepth { get; set; }
        public int Written { get; set; }
        public int Dropped { get; set; }
        public int DepthLimitHits { get; set; }
        public int Cycles { get; set; }
        public int ArrayInserts { get; set; }
        public int NoBlock { get; set; }
    }

    public class ExtentsEntry
    {
        public double[] Kept { get; set; }
        public double[] Core { get; set; }
        public int Points { get; set; }
    }

    public class HeaderFile
    {
        public string Source { get; set; }
        public string Units { get; set; }
        public string Created { get; set; }
        public string Updated { get; set; }
        public int ModelSpaceEntities { get; set; }
        public List<ClassEntry> Classes { get; set; }
    }

    public class LayersFile
    {
        public string Source { get; set; }
        public string Units { get; set; }
        // R196. The created and last saved times of the drawing header, so two exports of one sheet can be told apart by age
        public string Created { get; set; }
        public string Updated { get; set; }
        public TotalsEntry Totals { get; set; }
        public ExtentsEntry Extents { get; set; }
        public List<XrefEntry> Xrefs { get; set; }
        public List<NoteEntry> Notes { get; set; }
        public List<ClassEntry> Classes { get; set; }
        public List<LayerEntry> Layers { get; set; }
    }

    public class ArcEntry
    {
        public int Segment { get; set; }
        public double[] Center { get; set; }
        public double Radius { get; set; }
        public bool Ccw { get; set; }
    }

    public class PlotEntry
    {
        public string Kind { get; set; }
        public string Layer { get; set; }
        public bool? Closed { get; set; }
        public List<double[]> Vertices { get; set; }
        public string Block { get; set; }
        public double[] InsertPoint { get; set; }
        public double? RotationDeg { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
        public double? Area { get; set; }
        public List<ArcEntry> Arcs { get; set; }
        public List<double[]> Segments { get; set; }
        public string Plot { get; set; }
        public string Type { get; set; }
        public double[] Center { get; set; }
        public string Reason { get; set; }
        public List<string> Labels { get; set; }
        // R353. The merge of each vertex of a plot shape, written by --exact alone, so plots.json stays as it was
        [Newtonsoft.Json.JsonIgnore]
        public List<double> VertexMergeM { get; set; }
    }

    public class PlotsSummary
    {
        public int Shapes { get; set; }
        public int Plots { get; set; }
        public int OtherFaces { get; set; }
        public int NoLabel { get; set; }
        public int ManyLabels { get; set; }
        public int Labels { get; set; }
        public int LabelsWithoutNumber { get; set; }
        public int LabelsOutsideShapes { get; set; }
        public List<string> DuplicateNumbers { get; set; }
    }

    public class PlotsFile
    {
        public string Source { get; set; }
        public PlotsSummary Summary { get; set; }
        public List<PlotEntry> Plots { get; set; }
    }

    public class OtherFacesFile
    {
        public string Source { get; set; }
        public List<PlotEntry> Faces { get; set; }
    }

    public class XDataEntry
    {
        public string App { get; set; }
        public int? Count { get; set; }
        public List<string> Values { get; set; }
    }

    // One piece of geometry after exploding. Points are [x, y] or [x, y, z] in world coordinates.
    // Polyline vertices are [x, y, bulge]. Path lists the handles of the insert chain, outermost first.
    public class Piece
    {
        public string Kind { get; set; }
        public string Layer { get; set; }
        public string Handle { get; set; }
        public string Path { get; set; }
        public List<double[]> Pts { get; set; }
        public double[] Center { get; set; }
        public double? Radius { get; set; }
        public double[] Start { get; set; }
        public double[] End { get; set; }
        public bool? Ccw { get; set; }
        public bool? Closed { get; set; }
        public double? Elev { get; set; }
        public double? Ratio { get; set; }
        public List<List<double[]>> Loops { get; set; }
        public string Name { get; set; }
        public bool? Solid { get; set; }
        public Dictionary<string, string> Attrs { get; set; }
        public List<XDataEntry> Xdata { get; set; }
        public string Note { get; set; }
    }

    public class PiecesFile
    {
        public string Source { get; set; }
        public List<Piece> Pieces { get; set; }
    }

    public class TextEntry
    {
        public string Kind { get; set; }
        public string Layer { get; set; }
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Handle { get; set; }
        public string Path { get; set; }
        public string Tag { get; set; }
        public double? Height { get; set; }
    }

    public class TextsFile
    {
        public string Source { get; set; }
        public List<TextEntry> Texts { get; set; }
    }

    // One block insert at any depth. InsertPoint and Outline are in world coordinates. Scale and RotationDeg are the
    // values stored on the insert itself. Mirrored is scale X times scale Y below zero, taken before rounding so a
    // small scale keeps its sign. ChainMirrored is set when the inserts that hold this one flip it again.
    public class BlockEntry
    {
        public string Name { get; set; }
        public string Layer { get; set; }
        public string Handle { get; set; }
        public string Path { get; set; }
        public double[] InsertPoint { get; set; }
        public double[] Scale { get; set; }
        public double RotationDeg { get; set; }
        public bool Mirrored { get; set; }
        public bool? ChainMirrored { get; set; }
        public List<double[]> Outline { get; set; }
        public string Note { get; set; }
    }

    public class BlocksFile
    {
        public string Source { get; set; }
        public List<BlockEntry> Blocks { get; set; }
    }

    public class CadExtract
    {
        public LayersFile Layers { get; set; }
        public PlotsFile Plots { get; set; }
        public OtherFacesFile OtherFaces { get; set; }
        public TextsFile Texts { get; set; }
        public BlocksFile Blocks { get; set; }
        public PiecesFile Pieces { get; set; }
    }
}
