using System;
using System.IO;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn.ConfirmProfile;

namespace SiteAdaptation.UiShots
{
    // R636. The window Confirm what was found with made up workbook names, codes and words, no real value. A draft for Bader, CHOSEN by Lead
    public static partial class Shots
    {
        private const string SampleProfile = "{\"levels\":[{\"level\":1,\"word\":null,\"source\":\"missing\"},{\"level\":2,\"word\":\"Block\",\"source\":\"computed\"}],"
            + "\"codeMap\":{\"proposed\":[{\"old\":\"A1\",\"new\":\"B1\"},{\"old\":\"A2\",\"new\":\"B2\"}],\"foundIn\":\"codes.xlsx, Sheet 1\"},"
            + "\"typologyColumn\":{\"proposed\":{\"workbook\":\"plots.xlsx\",\"sheet\":\"Plots\",\"column\":\"Type\",\"plots\":300},\"candidates\":[{\"workbook\":\"plots.xlsx\",\"sheet\":\"Plots\",\"column\":\"Type\",\"plots\":300},{\"workbook\":\"plots.xlsx\",\"sheet\":\"Plots\",\"column\":\"Model\",\"plots\":280}]},"
            + "\"areaColumn\":{\"proposed\":null,\"candidates\":[{\"workbook\":\"plots.xlsx\",\"sheet\":\"Plots\",\"column\":\"Zone\",\"plots\":300}]},"
            + "\"hands\":{\"proposed\":{\"A1\":\"right\",\"B1\":\"left\",\"C3\":null}}}";

        private static BitmapSource ConfirmProfilePage()
        {
            var window = new ConfirmProfileWindow(new ConfirmProfileSession(ConfirmProfileModel.Read(SampleProfile), Path.GetTempPath(), "shot", 2));
            LayOut(window);
            return Draw(Body(window));
        }
    }
}
