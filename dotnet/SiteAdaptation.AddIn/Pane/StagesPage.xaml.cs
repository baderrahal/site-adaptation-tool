using System.Windows.Controls;

namespace SiteAdaptation.AddIn.Pane
{
    // R680. The page Revit docks: the 3 stage cards over the pane of version 1, which Advanced holds whole
    public partial class StagesPage : Page
    {
        public StagesPage(StagesViewModel model, SitePanePage oldPane)
        {
            InitializeComponent();
            DataContext = model;
            OldPane.Content = oldPane;
        }

        public StagesViewModel Model { get { return (StagesViewModel)DataContext; } }

        public SitePanePage OldPage { get { return (SitePanePage)OldPane.Content; } }
    }
}
