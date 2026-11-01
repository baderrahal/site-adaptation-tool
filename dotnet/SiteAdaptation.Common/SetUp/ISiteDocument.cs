using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SiteAdaptation.Common.SetUp
{
    // R373, R374. The thin Revit side of button 1, which only reads and carries out what SetUpRun decides. Every step but the
    // parameter file runs inside the one transaction of the flow, in the order of 03a steps 1 to 5
    public interface ISiteDocument
    {
        SiteState Read();

        // The shared point of an internal point by a Revit Transform made in memory of the position: the turn by its angle about the
        // internal origin, then the move by its east, north and elevation. The document is not changed (3.1 Checks, dry run)
        double[] SharedOf(SitePosition position, double[] internalM);

        // The shared point the document gives now for an internal point, the inverse of ProjectLocation.GetTotalTransform (after Apply)
        double[] SharedNow(double[] internalM);

        // Step 1, the length, area, angle and slope units on a copy of Document.GetUnits written back by Document.SetUnits
        void SetUnits(UnitState units);

        // Step 2, SetProjectPosition, then the survey point and the project base point moved to their internal points, the survey
        // point unclipped for the move and its clip state set back
        void SetPosition(SetUpTarget target, SetUpChanges changes);

        // R455, before step 2: the named position of the CAD site made the active one, by its name or by a rename of the active one,
        // then the latitude and longitude of the site location and its place name when config gives one
        void SetLocation(SetUpTarget target);

        // Step 3, the levels made by name with a floor plan each, the Elevation Base of their type set, then Level.Elevation
        void SetLevels(SetUpTarget target, SetUpChanges changes);

        // Step 4, the floor types as copies of a generic type, else of the first floor type, the wall types of the first basic one
        void MakeTypes(SetUpTarget target, SetUpChanges changes);

        // Step 5, each parameter bound by its GUID from the parameter file of UseParameterFile, as an instance binding on its categories
        void BindParameters(SetUpTarget target, IReadOnlyList<ParameterSpec> parameters);

        // Outside the transaction, the setting belongs to the application: Application.SharedParametersFilename set to the file and
        // the user's own setting put back when the scope is disposed
        IDisposable UseParameterFile(string path);
    }

    // R383. The element tags of a run. Button 1 calls one tag step that does nothing yet, 5c fills it from R368 of 4h
    public interface ITagStep
    {
        IReadOnlyList<string> Tag(string runId, int button);
    }

    public sealed class NoTags : ITagStep
    {
        public IReadOnlyList<string> Tag(string runId, int button)
        {
            return new string[0];
        }
    }

    // The shared parameter file Revit reads the definitions of button 1 from, one group, each parameter with the GUID of the sheet
    public static class SharedParameterText
    {
        // CHOSEN: UTF-16 with a byte order mark, so a name with a letter such as the c cedilla of Facade reads back as written
        public static readonly Encoding FileEncoding = new UnicodeEncoding(false, true);

        public static string Of(string group, IEnumerable<ParameterSpec> parameters)
        {
            var text = new StringBuilder();
            text.Append("# This is a Revit shared parameter file.\r\n# Do not edit manually.\r\n");
            text.Append("*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\n");
            text.Append("*GROUP\tID\tNAME\r\nGROUP\t1\t").Append(group).Append("\r\n");
            text.Append("*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE\r\n");
            foreach (ParameterSpec item in parameters.Where(item => item.Guid != null))
            {
                text.Append("PARAM\t").Append(item.Guid).Append('\t').Append(item.Name).Append('\t').Append(item.DataType ?? ParameterSheet.TextType).Append("\t\t1\t1\t\t1\t0\r\n");
            }
            return text.ToString();
        }

        public static string FileName(string runId)
        {
            return "shared_" + runId + ".txt";
        }
    }
}
