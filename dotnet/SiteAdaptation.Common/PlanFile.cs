using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SiteAdaptation.Common
{
    // plan.json as R80 and R112 write it. Only the parts the placer uses are read
    public class PlanFile
    {
        public const string FormatName = "site-adaptation-plan";
        public const int Version = 1;

        public string Format { get; set; }
        public int FormatVersion { get; set; }
        public string Neighbourhood { get; set; }
        public PlanSurveyPoint SurveyPoint { get; set; }
        public double? AngleToTrueNorthDeg { get; set; }
        public string PrototypeFolder { get; set; }
        public PlanFloorSettings FloorSettings { get; set; }
        // R206. A plan of a past model to compare with. The placer never places from it
        public bool? AnswerKeyOnly { get; set; }
        public List<PlanPlot> Plots { get; set; }

        public static PlanFile Parse(string json)
        {
            return JsonConvert.DeserializeObject<PlanFile>(json);
        }

        public static PlanFile Read(string path)
        {
            return Parse(File.ReadAllText(path));
        }
    }

    public class PlanSurveyPoint
    {
        public double[] Position { get; set; }
        public double[] SharedPosition { get; set; }
    }

    // A value with where it came from, pastJob or computed. A value the plan lacks is a null Sourced
    public class Sourced<T>
    {
        public T Value { get; set; }
        public string Source { get; set; }
    }

    public class PlanPlot
    {
        public string Plot { get; set; }
        public Sourced<string> Unit { get; set; }
        public Sourced<string> PrototypeFile { get; set; }
        public Sourced<string> Case { get; set; }
        // The link instance Name, built from the same values as RRE_ZZ_SP_RVT-Name
        public Sourced<string> LinkName { get; set; }
        public Sourced<double[]> Insert { get; set; }
        public Sourced<double?> RotationDeg { get; set; }
        public Sourced<bool?> Mirrored { get; set; }
        public PlanLevels Levels { get; set; }
        public Dictionary<string, Sourced<string>> Parameters { get; set; }
        public PlanFloors Floors { get; set; }
    }

    public class PlanLevels
    {
        public Sourced<double?> Ffl { get; set; }
        public Sourced<double?> Ssl { get; set; }
        public Sourced<double?> Entry { get; set; }
        public Sourced<double?> Height { get; set; }
        public Sourced<int?> Steps { get; set; }
    }

    // R112. The floor type names of each kind, the level of each kind and the parameter that holds unit-plot, all from config/project.json
    public class PlanFloorSettings
    {
        public Dictionary<string, List<string>> Types { get; set; }
        public Dictionary<string, string> Levels { get; set; }
        public string PlotParameter { get; set; }
    }

    public class PlanFloors
    {
        public PlanFloor Generic { get; set; }
        public PlanFloor Yard { get; set; }
        public PlanFloor Ramp { get; set; }
    }

    // One floor of a plot in shared coordinates. Z is the top of a flat floor, a ramp has its four Corners with their own z instead
    public class PlanFloor
    {
        public Sourced<string> Type { get; set; }
        public Sourced<string> Level { get; set; }
        public Sourced<double?> LevelElevation { get; set; }
        public Sourced<double?> HeightOffset { get; set; }
        public Sourced<double?> Z { get; set; }
        public Sourced<PlanOutline> Outline { get; set; }
        public Sourced<string> PlotValue { get; set; }
        public Sourced<List<double[]>> Corners { get; set; }
    }

    public class PlanOutline
    {
        public List<PlanLoop> Loops { get; set; }
    }

    // Points in order. An arc replaces the straight edge from point Edge to the next one
    public class PlanLoop
    {
        public List<double[]> Points { get; set; }
        public List<PlanArc> Arcs { get; set; }
    }

    public class PlanArc
    {
        public int Edge { get; set; }
        public double[] Center { get; set; }
        public double Radius { get; set; }
        public bool Ccw { get; set; }
    }
}
