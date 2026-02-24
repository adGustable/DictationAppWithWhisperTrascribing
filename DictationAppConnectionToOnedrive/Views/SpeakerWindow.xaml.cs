using DictationApp.Models;
using DictationApp.Services;
using NAudio.Wave;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace DictationApp.Views
{
    public partial class SpeakerWindow : Window
    {
        private readonly AudioRecordingService _recorder = new();
        private WaveOutEvent?    _player;
        private AudioFileReader? _playerReader;
        private DispatcherTimer? _playbackTimer;
        private AudioFile?       _selectedFile;

        // Waveform bars
        private readonly List<Rectangle> _bars = new();
        private const int BarCount = 44;
        private readonly Queue<double> _volumeHistory = new(Enumerable.Repeat(0.0, BarCount));

        public SpeakerWindow()
        {
            InitializeComponent();
            OutputFolderBox.Text = SettingsService.SpeakerOutputFolder;
            _recorder.VolumeChanged += OnVolumeChanged;
            _recorder.DurationUpdated += OnDurationUpdated;
            WaveformCanvas.Loaded += (_, _) => BuildBars();

            Loaded += (_, _) => RefreshList();
        }

        // ── Waveform ──────────────────────────────────────────────────────────

        private void BuildBars()
        {
            _bars.Clear();
            WaveformCanvas.Children.Clear();
            double w  = WaveformCanvas.ActualWidth;
            double h  = WaveformCanvas.ActualHeight;
            double bw = w / BarCount - 1;
            double cy = h / 2;
            for (int i = 0; i < BarCount; i++)
            {
                var r = new System.Windows.Shapes.Rectangle
                {
                    Width = bw, Height = 4, RadiusX = 2, RadiusY = 2,
                    Fill = new SolidColorBrush(Color.FromRgb(0xDA, 0xDC, 0xE0))
                };
                Canvas.SetLeft(r, i * (bw + 1));
                Canvas.SetTop(r, cy - 2);
                WaveformCanvas.Children.Add(r);
                _bars.Add(r);
            }
        }

        private void OnVolumeChanged(object? sender, double rms)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_bars.Count == 0) return;
                _volumeHistory.Dequeue();
                _volumeHistory.Enqueue(rms);
                double h  = WaveformCanvas.ActualHeight;
                double cy = h / 2;
                int i = 0;
                foreach (var vol in _volumeHistory)
                {
                    if (i >= _bars.Count) break;
                    var bar = _bars[i++];
                    double barH = Math.Max(4, vol * h * 3.5);
                    bar.Height = barH;
                    Canvas.SetTop(bar, cy - barH / 2);
                    bar.Fill = new SolidColorBrush(Color.FromRgb(0xEA, 0x43, 0x35));
                }
            });
        }

        private void ResetWaveform()
        {
            if (_bars.Count == 0) return;
            double cy = WaveformCanvas.ActualHeight / 2;
            foreach (var bar in _bars)
            {
                bar.Height = 4;
                Canvas.SetTop(bar, cy - 2);
                bar.Fill = new SolidColorBrush(Color.FromRgb(0xDA, 0xDC, 0xE0));
            }
        }

        private void OnDurationUpdated(object? sender, TimeSpan duration)
        {
            Dispatcher.InvokeAsync(() =>
            {
                TimerLabel.Text = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                    : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
            });
        }

        // ── Record / Pause / Stop ─────────────────────────────────────────────

        private void RecordPause_Click(object sender, RoutedEventArgs e)
        {
            if (_recorder.State == RecordingState.Idle)
            {
                // Validate file name
                if (string.IsNullOrWhiteSpace(FileNameBox.Text))
                {
                    FileNameError.Visibility = Visibility.Visible;
                    FileNameBox.Focus();
                    return;
                }
                FileNameError.Visibility = Visibility.Collapsed;

                // Ensure output folder exists
                var folder = SettingsService.SpeakerOutputFolder;
                Directory.CreateDirectory(folder);

                _recorder.Start(folder);
                RecordPauseButton.Content = "⏸  Pause";
                StopButton.IsEnabled = true;
                RecordingStatusLabel.Text = "● Recording…";
                RecordingStatusLabel.Foreground = (Brush)FindResource("DangerBrush");
                FileNameBox.IsEnabled = false;
            }
            else if (_recorder.State == RecordingState.Recording)
            {
                _recorder.Pause();
                RecordPauseButton.Content = "⏺  Resume";
                RecordingStatusLabel.Text = "⏸ Paused";
                RecordingStatusLabel.Foreground = (Brush)FindResource("TextSecondaryBrush");
            }
            else if (_recorder.State == RecordingState.Paused)
            {
                _recorder.Resume();
                RecordPauseButton.Content = "⏸  Pause";
                RecordingStatusLabel.Text = "● Recording…";
                RecordingStatusLabel.Foreground = (Brush)FindResource("DangerBrush");
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (!_recorder.IsActive) return;

            var (path, duration, size) = _recorder.Stop();

            RecordPauseButton.Content = "⏺  Record";
            StopButton.IsEnabled = false;
            TimerLabel.Text = "00:00";
            RecordingStatusLabel.Text = "Recording saved ✓";
            RecordingStatusLabel.Foreground = (Brush)FindResource("AccentBrush");
            ResetWaveform();
            FileNameBox.IsEnabled = true;

            if (string.IsNullOrEmpty(path) || duration.TotalSeconds < 0.5) return;

            // Build a clean file name from what the user typed
            var safeName = string.Concat(
                FileNameBox.Text.Trim()
                    .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var finalPath = Path.Combine(
                Path.GetDirectoryName(path)!,
                $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
            try { File.Move(path, finalPath); path = finalPath; } catch { /* keep original */ }

            var audioFile = new AudioFile
            {
                FileName        = FileNameBox.Text.Trim(),
                FilePath        = path,
                DurationSeconds = duration.TotalSeconds,
                FileSizeBytes   = size,
                Status          = FileStatus.Recorded,
                ReviewerNotes = NotesBox.Text.Trim()
            };

            DataService.AddAudioFile(audioFile);
            FileNameBox.Text = string.Empty;
            NotesBox.Text = string.Empty;
            RefreshList();
        }

        // ── Folder selection ──────────────────────────────────────────────────

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description  = "Select the shared OneDrive folder for audio files",
                SelectedPath = SettingsService.SpeakerOutputFolder,
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SettingsService.SpeakerOutputFolder = dlg.SelectedPath;
                OutputFolderBox.Text = dlg.SelectedPath;
            }
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

            PlaybackBar.Visibility = Visibility.Visible;
            PlaybackFileName.Text  = _selectedFile.FileName;
            PlaybackSlider.Maximum = _playerReader.TotalTime.TotalSeconds;
            StopPlayButton.IsEnabled = true;

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _playbackTimer.Tick += (_, _) =>
            {
                if (_playerReader == null) return;
                PlaybackSlider.Value  = _playerReader.CurrentTime.TotalSeconds;
                PlaybackTimeLabel.Text =
                    $"{FormatTime(_playerReader.CurrentTime)} / {FormatTime(_playerReader.TotalTime)}";
            };
            _playbackTimer.Start();

            _player.PlaybackStopped += (_, _) => Dispatcher.InvokeAsync(() =>
            {
                _playbackTimer?.Stop();
                PlaybackBar.Visibility = Visibility.Collapsed;
                StopPlayButton.IsEnabled = false;
                _playerReader?.Dispose();
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
            PlaybackBar.Visibility   = Visibility.Collapsed;
            StopPlayButton.IsEnabled = false;
        }

        private static string FormatTime(TimeSpan t) =>
            t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes:D2}:{t.Seconds:D2}";

        // ── List & Filter ─────────────────────────────────────────────────────

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            RefreshList();

        private void RecordingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFile = RecordingsList.SelectedItem as AudioFile;
            bool has = _selectedFile != null;
            bool hasFile = has && File.Exists(_selectedFile!.FilePath);

            PlayButton.IsEnabled   = hasFile;
            SendButton.IsEnabled   = has && _selectedFile!.Status == FileStatus.Recorded;
            DeleteButton.IsEnabled = has && _selectedFile!.Status == FileStatus.Recorded;
        }

        private void RefreshList()
        {
            if (FilterCombo == null ||
                RecordingsList == null ||
                EmptyLabel == null ||
                RecordingCountLabel == null)
            {
                return;
            }

            List<AudioFile> files = FilterCombo.SelectedIndex switch
            {
                0 => DataService.GetRecordingsToday(),
                1 => DataService.GetRecordingsThisWeek(),
                2 => DataService.GetAllRecordings(),
                _ => new List<AudioFile>()   // "No files shown"
            };

            RecordingsList.ItemsSource = files;

            bool noFiles = files.Count == 0;
            EmptyLabel.Visibility = noFiles ? Visibility.Visible : Visibility.Collapsed;

            RecordingCountLabel.Text = FilterCombo.SelectedIndex == 3
                ? "List hidden"
                : $"{files.Count} recording{(files.Count != 1 ? "s" : "")}";
        }

        // ── Send / Delete ─────────────────────────────────────────────────────

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;

            _selectedFile.Status = FileStatus.Sent;
            _selectedFile.SentAt = DateTime.UtcNow;

            DataService.UpdateAudioFile(_selectedFile);
            RefreshList();

            MessageBox.Show(
                $"\"{_selectedFile.FileName}\" has been marked as sent.\n\n" +
                "Ensure the file is in your shared OneDrive folder so your reviewer can access it.",
                "Sent for Review",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;

            var r = MessageBox.Show(
                $"Delete \"{_selectedFile.FileName}\"?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (r != MessageBoxResult.Yes) return;

            StopPlayback();
            DataService.DeleteAudioFile(_selectedFile.Id);
            RefreshList();
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
            if (_recorder.IsActive) _recorder.Stop();
            StopPlayback();
            _recorder.Dispose();
        }
    }
}
