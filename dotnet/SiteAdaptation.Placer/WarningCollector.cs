using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SiteAdaptation.Placer
{
    // Warnings are kept for the report and stay in the model. An error makes Revit roll the transaction back
    internal sealed class WarningCollector : IFailuresPreprocessor
    {
        public readonly List<string> Lines = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            bool error = false;
            foreach (FailureMessageAccessor message in accessor.GetFailureMessages())
            {
                if (message.GetSeverity() == FailureSeverity.Warning)
                {
                    Lines.Add(message.GetDescriptionText());
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
