using DictationApp.Services;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace DictationApp.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                SpeakerFolderBox.Text  = SettingsService.SpeakerOutputFolder;
                ReviewerFolderBox.Text = SettingsService.ReviewerWorkingFolder;
            };
        }

        private void BrowseSpeaker_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description         = "Select the Speaker audio output folder (e.g. OneDrive shared folder)",
                SelectedPath        = SettingsService.SpeakerOutputFolder,
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                SpeakerFolderBox.Text = dlg.SelectedPath;
        }

        private void BrowseReviewer_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description         = "Select the Reviewer working folder (e.g. OneDrive shared folder)",
                SelectedPath        = SettingsService.ReviewerWorkingFolder,
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ReviewerFolderBox.Text = dlg.SelectedPath;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.SpeakerOutputFolder  = SpeakerFolderBox.Text.Trim();
            SettingsService.ReviewerWorkingFolder = ReviewerFolderBox.Text.Trim();

            MessageBox.Show("Settings saved.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
