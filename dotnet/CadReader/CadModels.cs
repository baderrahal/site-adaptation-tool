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

    public class LayersFile
    {
        public string Source { get; set; }
        public string Units { get; set; }
        public TotalsEntry Totals { get; set; }
        public ExtentsEntry Extents { get; set; }
        public List<XrefEntry> Xrefs { get; set; }
        public List<NoteEntry> Notes { get; set; }
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

    public class CadExtract
    {
        public LayersFile Layers { get; set; }
        public PlotsFile Plots { get; set; }
        public OtherFacesFile OtherFaces { get; set; }
        public TextsFile Texts { get; set; }
        public PiecesFile Pieces { get; set; }
    }
}
