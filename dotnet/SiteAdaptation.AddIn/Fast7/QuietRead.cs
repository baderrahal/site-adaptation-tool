using System;

namespace SiteAdaptation.AddIn.Fast7
{
    // R771, F6 of run 1 of FAST-3. While the Build runs a step 2 it put back, the read shows no check window and no second Confirm: the gate of
    // the scan goes on with the picks made before, and the Confirm takes the profile the person confirmed last for this project. With no confirmed
    // profile the Confirm window still opens, so a Build never swaps the typology or area column of a person. Lead's version of 7 Nov 17:15 and 17:38
    public static class QuietRead
    {
        // Set by PaneHost to the state of its Build chain, a stand-in in the tests
        public static Func<bool> Now { get; set; } = () => false;

        public static bool On
        {
            get { return Now?.Invoke() == true; }
        }
    }
}
