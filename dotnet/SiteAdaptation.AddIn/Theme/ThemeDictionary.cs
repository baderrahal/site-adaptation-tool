using System;
using System.Windows;

namespace SiteAdaptation.AddIn.Theme
{
    // R371, docs/design/12-ui-spec.md. Theme.xaml is the one source of every colour, font, size and icon. Revit runs no WPF
    // Application of the tool, so the dictionary is loaded from the compiled resource of this assembly by its name
    public static class ThemeDictionary
    {
        public static readonly Uri Source = new Uri("/SiteAdaptation.AddIn;component/Theme/Theme.xaml", UriKind.Relative);

        public static ResourceDictionary Load()
        {
            return (ResourceDictionary)Application.LoadComponent(Source);
        }
    }
}
