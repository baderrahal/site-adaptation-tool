using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SiteAdaptation.AddIn.Revit
{
    // R382, R86. The add-in's own copy, docs/design/02-folders.md: the placer keeps its WarningCollector until the project is removed.
    // Warnings are kept as issues and stay in the model. An error makes Revit roll the transaction back
    internal sealed class WarningCollector : IFailuresPreprocessor
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            bool error = false;
            foreach (FailureMessageAccessor message in accessor.GetFailureMessages())
            {
                if (message.GetSeverity() == FailureSeverity.Warning)
                {
                    Warnings.Add(message.GetDescriptionText());
                }
                else
                {
                    Errors.Add(message.GetDescriptionText());
                    error = true;
                }
            }
            return error ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }
}
