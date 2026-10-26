namespace SiteAdaptation.Common
{
    // R347. The exit codes of the worker, defined once so the worker and the add-in read them the same way
    public static class WorkerExit
    {
        public const int Done = 0;
        public const int Failed = 1;
        public const int Refused = 2;
        public const int Memory = 3;
        public const int InputChanged = 4;

        public static string Status(int code)
        {
            switch (code)
            {
                case Done:
                    return "done";
                case Refused:
                case Memory:
                    return "refused";
                default:
                    return "failed";
            }
        }

        public static string Meaning(int code)
        {
            switch (code)
            {
                case Done:
                    return "done";
                case Failed:
                    return "failed, an exception, its text in the answer";
                case Refused:
                    return "refused before any read: an input missing or not chosen, a later formatVersion, a referenceOnly drawing or an input unit that is not metres";
                case Memory:
                    return "refused for memory: free memory under the measured peak of an input plus the floor";
                case InputChanged:
                    return "an input changed while it was read, its SHA-256 before and after differ (R1)";
                default:
                    return "an exit code no rule names";
            }
        }

        // An exit with no answer file is a failure that names the code
        public static string NoAnswer(int code)
        {
            return "the worker ended with code " + code + ", " + Meaning(code) + ", and wrote no answer";
        }
    }
}
