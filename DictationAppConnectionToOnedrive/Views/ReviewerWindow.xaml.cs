using DictationApp.Models;
using DictationApp.Services;
using Microsoft.Win32;
using NAudio.Wave;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using WpfMessageBox = System.Windows.MessageBox;

namespace DictationApp.Views
{
    public partial class ReviewerWindow : Window
    {
        private AudioFile?       _selectedFile;
        private WaveOutEvent?    _player;
        private AudioFileReader? _playerReader;
        private DispatcherTimer? _playbackTimer;

        // Files loaded in this session that are NOT in DataService (opened directly from disk)
        private readonly List<AudioFile> _sessionFiles = new();

        public ReviewerWindow()
        {
            InitializeComponent();
            FolderBox.Text = SettingsService.ReviewerWorkingFolder;
            RefreshQueue();
        }

        // ── File Queue ────────────────────────────────────────────────────────

        private void RefreshQueue()
        {
            // Merge persisted sent files with any session-opened files
            var sentFiles = DataService.GetAllRecordings()
                .Where(f => f.Status >= FileStatus.Sent)
                .ToList();

            // Session files not already in the persisted list
            foreach (var sf in _sessionFiles)
                if (!sentFiles.Any(f => f.Id == sf.Id))
                    sentFiles.Insert(0, sf);

            FileQueue.ItemsSource = sentFiles;
            QueueCountLabel.Text  = $"{sentFiles.Count} file{(sentFiles.Count != 1 ? "s" : "")}";
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshQueue();

        private void FileQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFile = FileQueue.SelectedItem as AudioFile;
            if (_selectedFile == null) return;

            FileNameLabel.Text    = _selectedFile.FileName;
            FileDurationLabel.Text = _selectedFile.DurationDisplay;
            FileStatusLabel.Text  = _selectedFile.StatusDisplay;

            TranscriptBox.Text = _selectedFile.Transcription ?? string.Empty;
            NotesBox.Text      = _selectedFile.ReviewerNotes ?? string.Empty;

            bool hasAudio = File.Exists(_selectedFile.FilePath);
            TranscribeButton.IsEnabled = hasAudio;
            PlayButton.IsEnabled        = hasAudio;
            TranscriptBox.IsEnabled     = true;
            NotesBox.IsEnabled          = true;
            SaveButton.IsEnabled        = true;
            MarkReviewedButton.IsEnabled = true;
            ExportButton.IsEnabled      = !string.IsNullOrWhiteSpace(_selectedFile.Transcription);
            SaveStatus.Text             = string.Empty;
        }

        // ── OneDrive Folder ───────────────────────────────────────────────────

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description  = "Select the shared OneDrive folder containing audio files",
                SelectedPath = SettingsService.ReviewerWorkingFolder,
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SettingsService.ReviewerWorkingFolder = dlg.SelectedPath;
                FolderBox.Text = dlg.SelectedPath;
            }
        }

        private void LoadFromFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SettingsService.ReviewerWorkingFolder;
            if (!Directory.Exists(folder))
            {
                MessageBox.Show("Folder not found. Please browse and select a valid folder.",
                    "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var audioExtensions = new[] { ".wav", ".mp3", ".m4a", ".mp4", ".webm" };
            var files = Directory.GetFiles(folder)
                .Where(f => audioExtensions.Contains(
                    Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (files.Count == 0)
            {
                MessageBox.Show("No audio files found in the selected folder.",
                    "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int added = 0;
            foreach (var filePath in files)
            {
                // Skip if already tracked
                bool exists = DataService.AudioFiles.Any(f => f.FilePath == filePath)
                           || _sessionFiles.Any(f => f.FilePath == filePath);
                if (exists) continue;

                var fi = new FileInfo(filePath);
                _sessionFiles.Add(new AudioFile
                {
                    FileName      = Path.GetFileNameWithoutExtension(filePath),
                    FilePath      = filePath,
                    FileSizeBytes = fi.Length,
                    RecordedAt    = fi.CreationTime,
                    Status        = FileStatus.Sent
                });
                added++;
            }

            RefreshQueue();
            MessageBox.Show(
                added > 0
                    ? $"{added} new file{(added != 1 ? "s" : "")} loaded from folder."
                    : "All files in this folder are already in the queue.",
                "Folder Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Open Audio File",
                Filter = "Audio Files|*.wav;*.mp3;*.m4a;*.mp4;*.webm|All Files|*.*",
                InitialDirectory = SettingsService.ReviewerWorkingFolder
            };
            if (dlg.ShowDialog() != true) return;

            bool exists = DataService.AudioFiles.Any(f => f.FilePath == dlg.FileName)
                       || _sessionFiles.Any(f => f.FilePath == dlg.FileName);
            if (exists)
            {
                MessageBox.Show("This file is already in the queue.", "Already Added",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var fi = new FileInfo(dlg.FileName);
            var audioFile = new AudioFile
            {
                FileName      = Path.GetFileNameWithoutExtension(dlg.FileName),
                FilePath      = dlg.FileName,
                FileSizeBytes = fi.Length,
                RecordedAt    = fi.CreationTime,
                Status        = FileStatus.Sent
            };
            _sessionFiles.Add(audioFile);
            RefreshQueue();

            // Auto-select the newly opened file
            FileQueue.SelectedItem = audioFile;
        }

        // ── Playback ──────────────────────────────────────────────────────────

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null || !File.Exists(_selectedFile.FilePath)) return;

            StopPlayback();

            _playerReader = new AudioFileReader(_selectedFile.FilePath);
            _player = new WaveOutEvent();
            _player.Init(_playerReader);
            _player.Play();

            SliderBar.Visibility     = Visibility.Visible;
            PlaybackSlider.Maximum   = _playerReader.TotalTime.TotalSeconds;
            StopPlayButton.IsEnabled = true;

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _playbackTimer.Tick += (_, _) =>
            {
                if (_playerReader == null) return;
                PlaybackSlider.Value    = _playerReader.CurrentTime.TotalSeconds;
                PlaybackTimeLabel.Text  =
                    $"{Fmt(_playerReader.CurrentTime)} / {Fmt(_playerReader.TotalTime)}";
            };
            _playbackTimer.Start();

            _player.PlaybackStopped += (_, _) => Dispatcher.InvokeAsync(() =>
            {
                _playbackTimer?.Stop();
                SliderBar.Visibility     = Visibility.Collapsed;
                PlaybackTimeLabel.Text   = string.Empty;
                StopPlayButton.IsEnabled = false;
                _playerReader?.Dispose();
                _playerReader = null;
            });
        }

        private void StopPlay_Click(object sender, RoutedEventArgs e) => StopPlayback();

        private void StopPlayback()
        {
            _playbackTimer?.Stop();
            _player?.Stop();
            _player?.Dispose();
            _player = null;
            _playerReader?.Dispose();
            _playerReader = null;
            SliderBar.Visibility     = Visibility.Collapsed;
            PlaybackTimeLabel.Text   = string.Empty;
            StopPlayButton.IsEnabled = false;
        }

        private static string Fmt(TimeSpan t) =>
            t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes:D2}:{t.Seconds:D2}";

        // ── Transcript ────────────────────────────────────────────────────────

        private void Transcript_TextChanged(object sender, TextChangedEventArgs e)
        {
            var words = TranscriptBox.Text
                .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
            WordCountLabel.Text = $"{words} word{(words != 1 ? "s" : "")}";
        }

        // ── Save / Mark Reviewed / Export ─────────────────────────────────────

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;
            _selectedFile.Transcription = TranscriptBox.Text;
            _selectedFile.ReviewerNotes = NotesBox.Text.Trim();

            // Persist only if this file is in DataService
            if (DataService.AudioFiles.Any(f => f.Id == _selectedFile.Id))
                DataService.UpdateAudioFile(_selectedFile);

            ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedFile.Transcription);
            SaveStatus.Text = "💾 Saved";
            FileStatusLabel.Text = _selectedFile.StatusDisplay;
        }

        private void MarkReviewed_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;
            _selectedFile.Transcription = TranscriptBox.Text;
            _selectedFile.ReviewerNotes = NotesBox.Text.Trim();
            _selectedFile.Status = FileStatus.Reviewed;

            if (DataService.AudioFiles.Any(f => f.Id == _selectedFile.Id))
                DataService.UpdateAudioFile(_selectedFile);

            FileStatusLabel.Text = _selectedFile.StatusDisplay;
            SaveStatus.Text = "✅ Marked as reviewed";
            RefreshQueue();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;

            // Commit current edits
            _selectedFile.Transcription = TranscriptBox.Text;
            _selectedFile.ReviewerNotes = NotesBox.Text.Trim();

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title       = "Export Transcription to Word",
                Filter      = "Word Document (*.docx)|*.docx",
                FileName    = $"{_selectedFile.FileName}.docx",
                DefaultExt  = ".docx",
                InitialDirectory = SettingsService.ReviewerWorkingFolder
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                WordExportService.Export(_selectedFile, dlg.FileName);

                _selectedFile.Status = FileStatus.Exported;
                if (DataService.AudioFiles.Any(f => f.Id == _selectedFile.Id))
                    DataService.UpdateAudioFile(_selectedFile);

                FileStatusLabel.Text = _selectedFile.StatusDisplay;
                SaveStatus.Text = "📄 Exported";
                RefreshQueue();

                var result = MessageBox.Show(
                    $"Exported successfully!\n\n{dlg.FileName}\n\nOpen the document now?",
                    "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dlg.FileName, UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Nav ───────────────────────────────────────────────────────────────

        private void Settings_Click(object sender, RoutedEventArgs e) =>
            new SettingsWindow().ShowDialog();

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            new HomeWindow().Show();
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopPlayback();
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

                TranscriptBox.Text = text;
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
    }
}
