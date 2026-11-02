using System.Windows;

namespace SiteAdaptation.AddIn.ReviewData
{
    public enum ReviewCloseChoice
    {
        Save,
        DontSave,
        Cancel,
    }

    // R446. The ask of 3 Review data. It holds no logic: ReviewDataSession.CanClose takes the choice. A close by Esc or the X is Cancel
    public partial class ReviewCloseWindow : Window
    {
        public ReviewCloseWindow()
        {
            InitializeComponent();
        }

        public ReviewCloseChoice Choice { get; private set; } = ReviewCloseChoice.Cancel;

        public static ReviewCloseChoice Ask(Window owner)
        {
            var window = new ReviewCloseWindow { Owner = owner };
            window.ShowDialog();
            return window.Choice;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            Choice = ReviewCloseChoice.Save;
            Close();
        }

        private void OnDontSave(object sender, RoutedEventArgs e)
        {
            Choice = ReviewCloseChoice.DontSave;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Choice = ReviewCloseChoice.Cancel;
            Close();
        }
    }
}
