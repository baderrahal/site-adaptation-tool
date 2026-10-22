using System.Collections.Generic;

namespace CadReader
{
    public class LayerEntry
    {
        public string Name { get; set; }
        public int Entities { get; set; }
        public Dictionary<string, int> ByType { get; set; }
    }

    public class LayersFile
    {
        public string Source { get; set; }
        public string Units { get; set; }
        public List<LayerEntry> Layers { get; set; }
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
    }

    public class PlotsFile
    {
        public string Source { get; set; }
        public List<PlotEntry> Plots { get; set; }
    }

    public class TextEntry
    {
        public string Kind { get; set; }
        public string Layer { get; set; }
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
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
        public TextsFile Texts { get; set; }
    }
}
