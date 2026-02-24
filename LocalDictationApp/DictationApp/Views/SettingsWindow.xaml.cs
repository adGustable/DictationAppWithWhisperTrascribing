using DictationApp.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DictationApp.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApiKeyBox.Text = SettingsService.WhisperApiKey;
            UpdateApiKeyStatus(SettingsService.WhisperApiKey);

            var lang = SettingsService.DefaultLanguage;
            foreach (ComboBoxItem item in DefaultLangCombo.Items)
            {
                if (item.Content?.ToString() == lang)
                {
                    DefaultLangCombo.SelectedItem = item;
                    break;
                }
            }
            if (DefaultLangCombo.SelectedIndex < 0)
                DefaultLangCombo.SelectedIndex = 0;

            ApiKeyBox.TextChanged += (_, _) => UpdateApiKeyStatus(ApiKeyBox.Text);
        }

        private void UpdateApiKeyStatus(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                ApiKeyStatus.Text = "⚠ No API key set";
                ApiKeyStatus.Foreground = (Brush)FindResource("DangerBrush");
            }
            else if (key.StartsWith("sk-"))
            {
                ApiKeyStatus.Text = "✅ Key looks valid";
                ApiKeyStatus.Foreground = (Brush)FindResource("AccentBrush");
            }
            else
            {
                ApiKeyStatus.Text = "⚠ Key should start with sk-";
                ApiKeyStatus.Foreground = (Brush)FindResource("DangerBrush");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.WhisperApiKey = ApiKeyBox.Text.Trim();
            SettingsService.DefaultLanguage =
                (DefaultLangCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "en";

            MessageBox.Show("Settings saved successfully.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
