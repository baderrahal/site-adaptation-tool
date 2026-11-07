using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Floors;

namespace SiteAdaptation.AddIn.Revit
{
    // R382, R86. The add-in's own copy, docs/design/02-folders.md: the placer keeps its WarningCollector until the project is removed.
    // Warnings are kept as issues and stay in the model. An error makes Revit roll the transaction back. R459: with a list of known
    // warnings the one the list accepts is deleted and listed apart, so Revit does not show it. Every other warning is left as it was
    internal sealed class WarningCollector : IFailuresPreprocessor
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<AcceptedWarning> Accepted = new List<AcceptedWarning>();

        public KnownWarningList Known { get; set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            return RunTiming.Measure(RunTiming.Warnings, () => Collect(accessor));
        }

        private FailureProcessingResult Collect(FailuresAccessor accessor)
        {
            bool error = false;
            foreach (FailureMessageAccessor message in accessor.GetFailureMessages())
            {
                if (message.GetSeverity() == FailureSeverity.Warning)
                {
                    AcceptedWarning accepted = Decide(accessor, message, out List<WarningElement> elements);
                    string text = message.GetDescriptionText();
                    if (accepted != null)
                    {
                        accessor.DeleteWarning(message);
                        Accepted.Add(accepted);
                    }
                    else if (KnownWordsWarning.TakeAndList(Known, text, accepted))
                    {
                        // R782. Known words on floors the list does not pair: no box of Revit holds an unwatched run, the issue lists the floors
                        accessor.DeleteWarning(message);
                        Warnings.Add(KnownWordsWarning.Line(text, elements));
                    }
                    else
                    {
                        Warnings.Add(text);
                    }
                }
                else
                {
                    Errors.Add(message.GetDescriptionText());
                    error = true;
                }
            }
            return error ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }

        private AcceptedWarning Decide(FailuresAccessor accessor, FailureMessageAccessor message, out List<WarningElement> elements)
        {
            elements = null;
            string text = message.GetDescriptionText();
            if (Known == null || !Known.Knows(text))
            {
                return null;
            }
            // The API parts failing from additional elements, and which of them an overlap names is unknown, so both are read
            Document document = accessor.GetDocument();
            elements = message.GetFailingElementIds().Concat(message.GetAdditionalElementIds()).Distinct()
                .Select(id => Read(document, id)).ToList();
            return Known.Decide(text, elements);
        }

        private WarningElement Read(Document document, ElementId id)
        {
            Element element = document.GetElement(id);
            Parameter plot = element?.LookupParameter(Known.PlotParameter);
            return new WarningElement
            {
                Id = element?.UniqueId, TypeName = element == null ? null : document.GetElement(element.GetTypeId())?.Name,
                Plot = plot == null || !plot.HasValue || plot.StorageType != StorageType.String ? null : plot.AsString(),
            };
        }
    }
}
