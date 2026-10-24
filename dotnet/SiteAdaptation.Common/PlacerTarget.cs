using System.Collections.Generic;

namespace SiteAdaptation.Common
{
    // The slice of the open Revit document the placer needs. SiteAdaptation.Placer fills it from the Revit API and tests fill it by hand.
    // Points are in metres in internal coordinates, angles in degrees unless a name says radians
    public interface IPlacerTarget
    {
        string DocumentTitle { get; }

        SurveyReading Survey();

        // The RRE_ZZ_SP_Plot-Number of every link instance of the document, null where it has none
        IList<string> LinkPlotNumbers();

        // The Revit link types of the document by the file name of their path
        IList<LinkTypeEntry> LinkTypes();

        bool FileExists(string path);

        // The rest is called in a real run only, between Begin and Commit or RollBack
        void Begin();

        string LoadLinkType(string fullPath);

        long Place(string linkTypeKey, PlacementSteps steps);

        PlacedTransform ReadBack(long instanceId);

        ParameterWrite Write(long instanceId, string name, string value);

        CommitResult Commit();

        void RollBack();

        // Revit warnings met in the transaction, one line each
        IList<string> Warnings { get; }
    }

    public class SurveyReading
    {
        public double[] Position { get; set; }
        public double[] SharedPosition { get; set; }
        public double AngleToTrueNorthDeg { get; set; }
    }

    public class LinkTypeEntry
    {
        public string FileName { get; set; }
        public string Key { get; set; }
    }

    // What the Revit side does to one new link instance, in this order: place it origin to origin, move it by Move,
    // turn it about the vertical axis through Move by RotationRad and, when MirrorNormal is set, mirror it without a copy
    // across the vertical plane through Move with that normal
    public class PlacementSteps
    {
        public double[] Move { get; set; }
        public double RotationRad { get; set; }
        public double[] MirrorNormal { get; set; }
    }

    public class PlacedTransform
    {
        public double[] Origin { get; set; }
        public double[] AxisX { get; set; }
        public double[] AxisY { get; set; }
        public double[] AxisZ { get; set; }
    }

    public enum ParameterWrite
    {
        Written,
        Missing,
        ReadOnly,
        NotText,
        Refused,
    }

    public class CommitResult
    {
        public bool Committed { get; set; }
        public string Reason { get; set; }
    }
}
