using System.Windows;

namespace DictationApp.Views
{
    public partial class HomeWindow : Window
    {
        public HomeWindow()
        {
            InitializeComponent();
        }

        private void Speaker_Click(object sender, RoutedEventArgs e)
        {
            new SpeakerWindow().Show();
            Close();
        }

        private void Reviewer_Click(object sender, RoutedEventArgs e)
        {
            new ReviewerWindow().Show();
            Close();
        }
    }
}
