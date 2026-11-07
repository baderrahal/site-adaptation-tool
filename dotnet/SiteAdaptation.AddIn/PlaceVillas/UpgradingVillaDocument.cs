using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using SiteAdaptation.Common;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.AddIn.PlaceVillas
{
    // R805, R806, F31. The document of button 7 with the upgrade box of Revit closed while a link type loads, so an unwatched Build does
    // not wait on it, and each file Revit upgraded kept for the issues of the step. The handler is on only around a load, so a box at
    // any other time, and any box of another id, stays for the person as before. Everything else goes to the document it wraps
    internal sealed class UpgradingVillaDocument : IVillaDocument, ILinkFileReader
    {
        private readonly IVillaDocument inner;
        private readonly UIApplication application;
        private readonly List<KeyValuePair<string, string>> upgraded = new List<KeyValuePair<string, string>>();
        private int closed;

        public UpgradingVillaDocument(IVillaDocument inner, UIApplication application)
        {
            this.inner = inner;
            this.application = application;
        }

        public string Current => application.Application.VersionNumber;

        // R806. The issue lines of what loaded since the last call, then cleared, so a second run of the same body starts empty
        public IReadOnlyList<string> TakeIssues()
        {
            IReadOnlyList<string> lines = LinkUpgrade.IssueLines(upgraded.ToList(), Current, closed);
            upgraded.Clear();
            closed = 0;
            return lines;
        }

        // R807. The year of the Revit that saved the file, null when it does not read
        public static string VersionOf(string path)
        {
            try
            {
                return BasicFileInfo.Extract(path)?.Format;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return null;
            }
        }

        public string LoadType(string key) => Watch(PathOf(key), () => inner.LoadType(key));

        public string ReloadType(string key) => Watch(PathOf(key), () => inner.ReloadType(key));

        public string CreateType(string fullPath) => Watch(fullPath, () => inner.CreateType(fullPath));

        private string Watch(string path, Func<string> load)
        {
            string version = path == null ? null : VersionOf(path);
            application.DialogBoxShowing += OnDialog;
            try
            {
                return load();
            }
            finally
            {
                application.DialogBoxShowing -= OnDialog;
                if (LinkUpgrade.Older(version, Current))
                {
                    upgraded.Add(new KeyValuePair<string, string>(path, version));
                }
            }
        }

        private void OnDialog(object sender, DialogBoxShowingEventArgs args)
        {
            if (LinkUpgrade.Knows(args.DialogId) && args.OverrideResult(LinkUpgrade.Close))
            {
                closed++;
            }
        }

        private string PathOf(string key) => inner.LinkTypes().FirstOrDefault(type => type.Key == key)?.FullPath;

        public string Title => inner.Title;

        public SurveyReading Survey() => inner.Survey();

        public IReadOnlyList<VillaLinkType> LinkTypes() => inner.LinkTypes();

        public IReadOnlyList<VillaElement> Villas(string plotNumberGuid) => inner.Villas(plotNumberGuid);

        public bool FileExists(string path) => inner.FileExists(path);

        public string UnloadType(string key) => inner.UnloadType(key);

        public string PlaceLink(string typeKey, PlacementSteps steps) => inner.PlaceLink(typeKey, steps);

        public string PlaceSolid(IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps) => inner.PlaceSolid(shape, heightM, steps);

        public void MoveLink(string uniqueId, PlacementSteps steps) => inner.MoveLink(uniqueId, steps);

        public void ReshapeSolid(string uniqueId, IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps) => inner.ReshapeSolid(uniqueId, shape, heightM, steps);

        public void Delete(string uniqueId) => inner.Delete(uniqueId);

        public PlacedTransform ReadBackLink(string uniqueId) => inner.ReadBackLink(uniqueId);

        public IReadOnlyList<double[]> ReadBackBase(string uniqueId) => inner.ReadBackBase(uniqueId);

        public ParameterWrite Write(string uniqueId, string guid, string value) => inner.Write(uniqueId, guid, value);

        public ParameterWrite WriteName(string uniqueId, string value) => inner.WriteName(uniqueId, value);

        public void Tag(string uniqueId, PlacedTag tag) => inner.Tag(uniqueId, tag);

        public string LinkFileOf(string uniqueId) => (inner as ILinkFileReader)?.LinkFileOf(uniqueId);
    }
}
