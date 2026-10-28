using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Icons;
using SiteAdaptation.AddIn.Theme;

namespace SiteAdaptation.AddIn
{
    // R371. The manifest names this class. The resolver comes first, before any type of SiteAdaptation.Common can load
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            OwnFolderResolver.Register(Path.GetDirectoryName(typeof(App).Assembly.Location));
            BuildRibbon(application);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BuildRibbon(UIControlledApplication application)
        {
            ResourceDictionary theme = ThemeDictionary.Load();
            string assembly = typeof(App).Assembly.Location;
            application.CreateRibbonTab(RibbonLayout.Tab);
            foreach (RibbonPanelSpec panel in RibbonLayout.Panels)
            {
                RibbonPanel ribbonPanel = application.CreateRibbonPanel(RibbonLayout.Tab, panel.Name);
                foreach (RibbonButtonSpec button in panel.Buttons)
                {
                    var data = new PushButtonData("SiteAdaptation.Button" + button.Number, button.Label, assembly, button.CommandClass)
                    {
                        LargeImage = IconRenderer.Render(theme, button.IconKey32, 32),
                        Image = IconRenderer.Render(theme, button.IconKey16, 16),
                        AvailabilityClassName = RibbonLayout.AvailabilityClass,
                    };
                    ribbonPanel.AddItem(data);
                }
            }
        }
    }
}
