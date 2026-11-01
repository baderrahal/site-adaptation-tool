using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R377, R404. The thin Revit side of button 7, which only reads and carries out what PlaceVillasRun decides. Points are in metres in
    // internal coordinates, a shape in the internal plan coordinates of its prototype
    public interface IVillaDocument
    {
        string Title { get; }

        SurveyReading Survey();

        // Every RevitLinkType of the document with the absolute path of GetExternalFileReference
        IReadOnlyList<VillaLinkType> LinkTypes();

        // Every link instance and every solid of the schema of R368, each with its tag or none and its plot number parameter, read by
        // its GUID, or none
        IReadOnlyList<VillaElement> Villas(string plotNumberGuid);

        bool FileExists(string path);

        // Outside any transaction, since RevitLinkType.Load and Reload may not run while one is open and clear the Undo list. Null
        // when it loaded, else the load result
        string LoadType(string key);

        string ReloadType(string key);

        // The rest inside the one transaction of the button. CreateType gives the key of the new type, null when it did not load
        string CreateType(string fullPath);

        string PlaceLink(string typeKey, PlacementSteps steps);

        // R404. One DirectShape of Generic Models whose base is the shape, extruded up by heightM, carried by the steps of the link
        string PlaceSolid(IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps);

        // The element keeps its UniqueId and its tag: a link is brought back to origin to origin and given the steps again, a solid
        // takes its new geometry
        void MoveLink(string uniqueId, PlacementSteps steps);

        void ReshapeSolid(string uniqueId, IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps);

        void Delete(string uniqueId);

        PlacedTransform ReadBackLink(string uniqueId);

        // The outer loop of the lowest face of a solid in internal coordinates, x, y and z, read from its geometry
        IReadOnlyList<double[]> ReadBackBase(string uniqueId);

        // 03a Shared parameters: by its GUID, Element.get_Parameter(Guid), never by name, since LookupParameter gives one of two
        // parameters of one name at random. A null GUID is Missing
        ParameterWrite Write(string uniqueId, string guid, string value);

        // R107. The instance Name of a link, the name of a DirectShape
        ParameterWrite WriteName(string uniqueId, string value);

        void Tag(string uniqueId, PlacedTag tag);
    }

    public sealed class VillaLinkType
    {
        public string FullPath { get; set; }
        public string Key { get; set; }
        public bool Loaded { get; set; } = true;

        // False for a type a new instance cannot be made from, a nested link, and Why says which
        public bool Usable { get; set; } = true;
        public string Why { get; set; }
    }

    public sealed class VillaElement
    {
        public string UniqueId { get; set; }
        public bool IsSolid { get; set; }

        // Null for an element with no entity of the schema
        public PlacedTag Tag { get; set; }

        // RRE_ZZ_SP_Plot-Number, null where it has none
        public string PlotNumber { get; set; }

        // A link, else null
        public PlacedTransform Transform { get; set; }

        // A solid, else null
        public IReadOnlyList<double[]> Base { get; set; }
    }

    // R377, 03b 7 Inputs. What one run of button 7 reads, given by the loader the add-in sets
    public sealed class VillaInputs
    {
        public PlanFile Plan { get; set; }
        public string PlanPath { get; set; }
        public LightModeConfig Config { get; set; }

        // One of VillaModes, by VillaModeChoice
        public string Mode { get; set; }

        // The level profile of the settings, written in each tag
        public string Profile { get; set; }

        // R366, null for every plot of the plan
        public Func<string, bool> InScope { get; set; }

        // R102, null for every prototype file
        public string PrototypeFilter { get; set; }

        // R300, the villa outlines of the footprint read of button 2 in shared plan coordinates
        public IReadOnlyList<double[][]> Footprint { get; set; } = new List<double[][]>();

        // The SHA-256 of each prototype file by its full path, from the record of the last real run of 7
        public IReadOnlyDictionary<string, string> LastHashes { get; set; } = new Dictionary<string, string>();

        // The plots the last real run of 7 placed, so a villa deleted by hand is told from one never placed (R368)
        public ICollection<string> LastPlaced { get; set; } = new HashSet<string>();

        // The UniqueIds the user set to the plan
        public ICollection<string> SetToPlan { get; set; } = new HashSet<string>();

        // R465. The GUID of each shared parameter button 7 fills by the Filled by of the parameter sheet, by its name
        public IReadOnlyDictionary<string, string> ParameterGuids { get; set; } = new Dictionary<string, string>();

        // R465. The names of the parameter sheet button 7 does not fill, another button's, none's or left out, never written by it
        public ICollection<string> FilledByOthers { get; set; } = new HashSet<string>();

        // R377. The inputs from the files of the work folder: the plan held by the SHA-256 the record of the last real run of 4 names,
        // addin.lightMode of the Settings file, the Villa mode of the pane row, the footprint read of button 2 and the record of the last
        // real run of 7, place_<NBH>.json, null before the first
        public static VillaInputs Of(string planPath, string planSha256, string configPath, string pickedMode, string profile, Func<string, bool> inScope,
            Newtonsoft.Json.Linq.JObject villaBlocksRead, Newtonsoft.Json.Linq.JObject lastRecord)
        {
            LightModeConfig config = LightModeConfig.Load(configPath);
            Newtonsoft.Json.Linq.JObject settings = Newtonsoft.Json.Linq.JObject.Parse(WorkFiles.ReadText(configPath));
            DesignValues.UseConfigOf(settings);
            SetUp.ParameterSheet sheet = SetUp.ParameterSheet.FromConfig(configPath, settings);
            Dictionary<string, string> guids = sheet.GuidsFilledBy(SetUp.ParameterSheet.PlaceVillasButton);
            var others = new HashSet<string>(sheet.Parameters.Where(item => item.FilledBy != SetUp.ParameterSheet.PlaceVillasButton).Select(item => item.Name).Concat(sheet.LeftOut),
                StringComparer.Ordinal);
            return new VillaInputs
            {
                Plan = PlanSource.Open(planPath, planSha256), PlanPath = planPath, Config = config, Mode = VillaModeChoice.Resolve(pickedMode, config), Profile = profile,
                InScope = inScope, Footprint = FootprintCheck.FromRead(villaBlocksRead), LastHashes = PlaceVillasRun.Hashes(lastRecord), LastPlaced = PlaceVillasRun.Placed(lastRecord),
                ParameterGuids = guids, FilledByOthers = others,
            };
        }
    }

    // R377, 03b 7 Inputs. plan_<NBH>.json of the last real run of button 4, held against the SHA-256 its run record names, never a plan
    // of a dry folder
    public static class PlanSource
    {
        public static PlanFile Open(string path, string recordedSha256)
        {
            if (string.IsNullOrEmpty(recordedSha256))
            {
                throw new InvalidDataException("no real run of 4 Check names a plan, run 4 Check first (R377)");
            }
            string full = Path.GetFullPath(path);
            if (full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "dry", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(Path.GetFileName(path) + " lies in a dry folder, Place villas reads only the plan of a real run of 4 Check (R377)");
            }
            string now = FileHash.Sha256(full);
            if (!string.Equals(now, recordedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(Path.GetFileName(path) + " is not the plan the last real run of 4 Check wrote, its SHA-256 differs, run 4 Check again (R377)");
            }
            return PlanFile.Parse(WorkFiles.ReadText(full));
        }
    }
}
