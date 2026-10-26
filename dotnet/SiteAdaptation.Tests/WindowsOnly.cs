using System.Runtime.InteropServices;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R346. A test of a Windows tool of the test host or the reader, its Windows paths, kernel32, cmd, PowerShell or CIM, runs on
    // net48 and on the net8.0 leg of Windows, and the net8.0 leg of Linux skips it with this reason, so a skip is never silent
    internal static class WindowsOnly
    {
        public const string Reason = "Windows only: Windows paths, kernel32, cmd, PowerShell or CIM of a tool that runs on Windows (R346)";

        public static string SkipOffWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? null : Reason;
        }
    }

    public sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            Skip = WindowsOnly.SkipOffWindows();
        }
    }

    public sealed class WindowsTheoryAttribute : TheoryAttribute
    {
        public WindowsTheoryAttribute()
        {
            Skip = WindowsOnly.SkipOffWindows();
        }
    }
}
