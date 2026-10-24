using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SiteAdaptation.Common
{
    // plan.json as R80 writes it. Only the parts the placer uses are read, the floors wait for a later round
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
    }

    public class PlanLevels
    {
        public Sourced<double?> Ffl { get; set; }
        public Sourced<double?> Ssl { get; set; }
        public Sourced<double?> Entry { get; set; }
        public Sourced<double?> Height { get; set; }
        public Sourced<int?> Steps { get; set; }
    }
}
