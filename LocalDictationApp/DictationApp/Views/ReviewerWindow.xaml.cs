using DictationApp.Models;
using DictationApp.Services;
using Microsoft.Win32;
using NAudio.Wave;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DictationApp.Views
{
    public partial class ReviewerWindow : Window
    {
        private readonly User _currentUser;
        private AudioFile? _selectedFile;
        private WaveOutEvent? _player;
        private AudioFileReader? _reader;
        private bool _isLoaded;

        public ReviewerWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            UserNameLabel.Text = user.DisplayName;
            Loaded += ReviewerWindow_Loaded; ;
        }

        private void ReviewerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            FilterCombo.SelectedIndex = 0;
            RefreshQueue(); ;
        }

        // ── Queue ──────────────────────────────────────────────────────────────

        private void RefreshQueue()
        {
            var allFiles = DataService.GetFilesForReview() ?? new List<AudioFile>();

            var filtered = ApplyFilter(allFiles) ?? new List<AudioFile>();

            FileQueue.ItemsSource = filtered;
            QueueCountLabel.Text = $"{filtered.Count} file{(filtered.Count != 1 ? "s" : "")}";
        }

        private List<AudioFile> ApplyFilter(List<AudioFile>? files)
        {
            files ??= new List<AudioFile>();

            if (FilterCombo == null)
                return files;

            return FilterCombo.SelectedIndex switch
            {
                1 => files.Where(f => f.Status == FileStatus.Sent).ToList(),
                2 => files.Where(f => f.Status == FileStatus.Transcribed).ToList(),
                3 => files.Where(f => f.Status >= FileStatus.Reviewed).ToList(),
                _ => files
            };
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            RefreshQueue();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshQueue();

        // ── File Selection ─────────────────────────────────────────────────────

        private void FileQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFile = FileQueue.SelectedItem as AudioFile;
            if (_selectedFile == null) return;

            FileNameLabel.Text = _selectedFile.FileName;
            SpeakerLabel.Text = $"Speaker: {_selectedFile.SpeakerName}";
            DurationLabel.Text = _selectedFile.DurationDisplay;
            StatusLabel.Text = _selectedFile.StatusDisplay;

            TranscriptionEditor.Text = _selectedFile.Transcription ?? string.Empty;
            ReviewerNotesBox.Text = _selectedFile.ReviewerNotes ?? string.Empty;

            bool hasAudio = File.Exists(_selectedFile.FilePath);
            PlayFileButton.IsEnabled = hasAudio;
            TranscribeButton.IsEnabled = hasAudio;
            TranscriptionEditor.IsEnabled = true;
            ReviewerNotesBox.IsEnabled = true;
            SaveButton.IsEnabled = true;
            MarkReviewedButton.IsEnabled = _selectedFile.Status == FileStatus.Transcribed
                                        || _selectedFile.Status == FileStatus.Reviewed;
            ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedFile.Transcription);

            TranscribeStatus.Text = string.Empty;
        }

        // ── Playback ──────────────────────────────────────────────────────────

        private void PlayFile_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null || !File.Exists(_selectedFile.FilePath)) return;

            _player?.Stop();
            _player?.Dispose();
            _reader?.Dispose();

            _reader = new AudioFileReader(_selectedFile.FilePath);
            _player = new WaveOutEvent();
            _player.Init(_reader);
            _player.Play();
            StopPlayButton.IsEnabled = true;
            _player.PlaybackStopped += (_, _) =>
            {
                Dispatcher.InvokeAsync(() => StopPlayButton.IsEnabled = false);
                _reader?.Dispose();
            };
        }

        private void StopPlay_Click(object sender, RoutedEventArgs e)
        {
            _player?.Stop();
            StopPlayButton.IsEnabled = false;
        }

        // ── Transcription ─────────────────────────────────────────────────────

        private async void Transcribe_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;

            // For OpenAI SK
            //var apiKey = SettingsService.WhisperApiKey;
            //if (!WhisperService.IsApiKeySet(apiKey))
            //{
            //    var result = MessageBox.Show(
            //        "No OpenAI API key configured.\n\nOpen Settings to enter your key now?",
            //        "API Key Required", MessageBoxButton.YesNo, MessageBoxImage.Question);
            //    if (result == MessageBoxResult.Yes)
            //    {
            //        new SettingsWindow().ShowDialog();
            //        apiKey = SettingsService.WhisperApiKey;
            //        if (!WhisperService.IsApiKeySet(apiKey)) return;
            //    }
            //    else return;
            //}

            TranscribeButton.IsEnabled = false;
            TranscribeStatus.Text = "⏳ Connecting to Whisper...";

            var language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "en";
            //var whisper = new WhisperService(apiKey); used for OpenAI

            var progress = new Progress<string>(msg =>
                Dispatcher.InvokeAsync(() => TranscribeStatus.Text = msg));

            // Mark as Transcribing
            _selectedFile.Status = FileStatus.Transcribing;
            DataService.UpdateAudioFile(_selectedFile);
            StatusLabel.Text = _selectedFile.StatusDisplay;

            try
            {
                var whisper = new LocalWhisperService();

                var text = await whisper.TranscribeAsync(
                    _selectedFile.FilePath,
                    language,
                    progress);

                _selectedFile.Transcription = text;
                _selectedFile.Status = FileStatus.Transcribed;
                DataService.UpdateAudioFile(_selectedFile);

                TranscriptionEditor.Text = text;
                StatusLabel.Text = _selectedFile.StatusDisplay;
                TranscribeStatus.Text = "✅ Transcription complete!";
                ExportButton.IsEnabled = true;
                MarkReviewedButton.IsEnabled = true;
                RefreshQueue();
            }
            catch (Exception ex)
            {
                _selectedFile.Status = FileStatus.Sent;
                DataService.UpdateAudioFile(_selectedFile);
                TranscribeStatus.Text = $"❌ Error: {ex.Message}";
                MessageBox.Show($"Transcription failed:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TranscribeButton.IsEnabled = true;
            }
        }

        private void TranscriptionEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            var words = TranscriptionEditor.Text
                .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
            WordCountLabel.Text = $"{words} words";
        }

        // ── Save & Review ─────────────────────────────────────────────────────

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;
            _selectedFile.Transcription = TranscriptionEditor.Text;
            _selectedFile.ReviewerNotes = ReviewerNotesBox.Text.Trim();
            DataService.UpdateAudioFile(_selectedFile);
            TranscribeStatus.Text = "💾 Saved.";
            ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedFile.Transcription);
        }

        private void MarkReviewed_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;
            _selectedFile.Transcription = TranscriptionEditor.Text;
            _selectedFile.ReviewerNotes = ReviewerNotesBox.Text.Trim();
            _selectedFile.Status = FileStatus.Reviewed;
            DataService.UpdateAudioFile(_selectedFile);
            StatusLabel.Text = _selectedFile.StatusDisplay;
            TranscribeStatus.Text = "✅ Marked as reviewed.";
            RefreshQueue();
        }

        // ── Export ────────────────────────────────────────────────────────────

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;

            // Save current edits first
            _selectedFile.Transcription = TranscriptionEditor.Text;
            _selectedFile.ReviewerNotes = ReviewerNotesBox.Text.Trim();
            DataService.UpdateAudioFile(_selectedFile);

            var dlg = new SaveFileDialog
            {
                Title = "Export Transcription",
                Filter = "Word Document (*.docx)|*.docx",
                FileName = $"{_selectedFile.FileName}.docx",
                DefaultExt = ".docx"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                WordExportService.Export(_selectedFile, dlg.FileName);

                _selectedFile.Status = FileStatus.Exported;
                DataService.UpdateAudioFile(_selectedFile);
                StatusLabel.Text = _selectedFile.StatusDisplay;
                RefreshQueue();

                var result = MessageBox.Show(
                    $"Exported successfully!\n\n{dlg.FileName}\n\nOpen the file now?",
                    "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dlg.FileName,
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Nav ───────────────────────────────────────────────────────────────

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow().ShowDialog();
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _player?.Stop();
            _player?.Dispose();
            _reader?.Dispose();
        }
    }
}
