using System.IO;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R348. The units of a drawing output the worker reads, as python/cad_units.py holds them for the Python reference (R388): a read of
    // the worker names the InsUnits of its drawing in units, a CadReader output carries none and takes the units of the layers.json of its
    // read beside it. A drawing is read only in Meters and never converted, any other unit or none known is refused with the file named
    public static class CadUnits
    {
        public const string Metres = "Meters";
        public const string Layers = "layers.json";

        // Null when the output is of a drawing in Meters, else why it is refused
        public static string Refusal(string path, JObject content)
        {
            JToken units = content?["units"];
            if (units != null)
            {
                return (string)units == Metres ? null : path + " is of a drawing in " + units + ", a drawing is read only in " + Metres + " and never converted (R348)";
            }
            string beside = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "", Layers);
            if (!File.Exists(beside))
            {
                return path + " names no units and has no " + Layers + " of its read beside it, so the units of its drawing are not known, a drawing is read only in " + Metres + " (R348)";
            }
            JToken found = JObject.Parse(WorkFiles.ReadText(beside))["units"];
            return found != null && (string)found == Metres ? null : path + " is of a drawing in " + (found?.ToString() ?? "no units") + ", a drawing is read only in " + Metres + " and never converted (R348)";
        }

        // R348, R347. The units of a drawing header, Meters, else the refusal of a read before anything of it is used
        public static string HeaderRefusal(string file, string insUnits)
        {
            return insUnits == Metres ? null : file + " is in " + insUnits + " by its header InsUnits, a drawing is read only in " + Metres + " and never converted (R348)";
        }
    }
}
