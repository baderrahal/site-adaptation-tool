using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.PlaceVillas
{
    // R805 to R807, F31. What button 7 decides about a link file of an older Revit, with no Revit type, so the tests reach it. The Revit
    // side is UpgradingVillaDocument
    public static class LinkUpgrade
    {
        // R805. The id Revit 2024 gives its box "Structural Analytical Model Upgrade", which has one button, Close. Measured, not guessed:
        // the test host logged it with Close taken in step 7 of the buttons run LB6E on 1 Nov and in the prototype upgrade jobs
        public const string BoxId = "TaskDialog_Views_Related_To_Analytical_Changed";
        public const string BoxTitle = "Structural Analytical Model Upgrade";

        // TaskDialogResult.Close, the one button of the box
        public const int Close = 8;

        // R805. Only the box of this id, any other box Revit shows while a link loads stays for the person as before
        public static bool Knows(string dialogId)
        {
            return string.Equals(dialogId, BoxId, StringComparison.Ordinal);
        }

        // A version Revit gives as a year, "2021", is older than the running one. A version that did not read is not called older
        public static bool Older(string version, string current)
        {
            return int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out int year)
                && int.TryParse(current, NumberStyles.None, CultureInfo.InvariantCulture, out int now) && year < now;
        }

        // R807. The prototype files of the plan in the filter and the scope of the run that no usable loaded link type of the model
        // stands for, each a type the run loads or makes, matched by the full path without case as PlaceVillasRun matches them. None in
        // Masses, which links no prototype file
        public static IReadOnlyList<string> NewFiles(VillaInputs inputs, IEnumerable<VillaLinkType> types)
        {
            PlanFile plan = inputs?.Plan;
            if (plan?.PrototypeFolder == null || plan.Plots == null || inputs.Config == null || VillaModeChoice.Resolve(inputs.Mode, inputs.Config) != VillaModes.Links)
            {
                return new List<string>();
            }
            var loaded = new HashSet<string>((types ?? Enumerable.Empty<VillaLinkType>()).Where(type => type.Usable && type.Loaded && type.FullPath != null)
                .Select(type => Full(type.FullPath)), StringComparer.OrdinalIgnoreCase);
            var files = new List<string>();
            foreach (PlanPlot plot in plan.Plots)
            {
                string number = plot?.Plot?.Trim();
                string name = plot?.PrototypeFile?.Value?.Trim();
                if (string.IsNullOrEmpty(number) || string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || !Placer.InFilter(plot, inputs.PrototypeFilter)
                    || (inputs.InScope != null && !inputs.InScope(number)))
                {
                    continue;
                }
                string file = inputs.PrototypeFile(name);
                if (!loaded.Contains(Full(file)) && !files.Any(other => string.Equals(Full(other), Full(file), StringComparison.OrdinalIgnoreCase)))
                {
                    files.Add(file);
                }
            }
            return files;
        }

        // R807. The box of step 7 counts the link types it loads that the model holds no loaded type for, and names each file of an
        // older Revit, which Revit upgrades as it loads, first in the list so the box shows it. versionOf gives null when it did not read
        public static RunSummary Add(RunSummary summary, IReadOnlyList<string> newFiles, Func<string, string> versionOf, string current)
        {
            if (summary == null || newFiles == null || newFiles.Count == 0)
            {
                return summary;
            }
            var read = newFiles.Select(file => new { file, version = versionOf(file) }).ToList();
            int older = read.Count(item => Older(item.version, current));
            var lines = new List<string>();
            foreach (var item in read)
            {
                if (Older(item.version, current))
                {
                    lines.Add("link " + Path.GetFileName(item.file) + " is a Revit " + item.version + " file, Revit upgrades it to " + current + " as it loads, which takes minutes");
                }
                else if (item.version == null)
                {
                    lines.Add("link " + Path.GetFileName(item.file) + ": its Revit version did not read, it may be upgraded as it loads");
                }
            }
            string counts = summary.Counts + ", link types new to the model " + read.Count + ", of an older Revit " + older;
            return new RunSummary(counts, lines.Concat(summary.PerPlot).ToList());
        }

        // R811. The box of step 7 says how many files it links from the copies beside the prototype folder, first, and names each file
        // with no copy. No line when no folder of copies was found
        public static RunSummary WithCopies(RunSummary summary, PrototypeCopies copies)
        {
            if (summary == null || copies?.CopiesFolder == null)
            {
                return summary;
            }
            var lines = new List<string> { "links the Revit " + copies.Running + " copies in " + Path.GetFileName(copies.CopiesFolder) + " for " + copies.CopyCount + " of " + copies.Codes.Count + " prototype files (R811)" };
            lines.AddRange(copies.Codes.Where(code => code.Copy == null).Select(code => "prototype " + code.Name + " has no Revit " + copies.Running + " copy, it is linked from " + Path.GetFileName(copies.Folder) + " as before"));
            return new RunSummary(summary.Counts, lines.Concat(summary.PerPlot).ToList());
        }

        // R806. The issues of the step: each file Revit upgraded as it loaded, and how many times the tool closed the upgrade box
        public static IReadOnlyList<string> IssueLines(IReadOnlyList<KeyValuePair<string, string>> upgraded, string current, int closed)
        {
            var lines = (upgraded ?? new List<KeyValuePair<string, string>>()).Select(item => "link " + Path.GetFileName(item.Key) + " was a Revit " + item.Value
                + " file and was upgraded to " + current + " as it loaded (R806)").ToList();
            if (closed > 0)
            {
                lines.Add("the tool closed the Revit box " + BoxTitle + " " + closed + (closed == 1 ? " time" : " times") + " while the links loaded (R805)");
            }
            return lines;
        }

        private static string Full(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException || failure is PathTooLongException)
            {
                return path;
            }
        }
    }
}
