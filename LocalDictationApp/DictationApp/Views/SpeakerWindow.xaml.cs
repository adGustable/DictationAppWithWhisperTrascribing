using DictationApp.Models;
using DictationApp.Services;
using NAudio.Wave;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DictationApp.Views
{
    public partial class SpeakerWindow : Window
    {
        private readonly User _currentUser;
        private readonly AudioRecordingService _recorder = new();
        private WaveOutEvent? _player;
        private string? _lastRecordedPath;
        private AudioFile? _selectedFile;

        // Waveform bars
        private readonly List<Rectangle> _bars = new();
        private const int BarCount = 40;

        public SpeakerWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            UserNameLabel.Text = user.DisplayName;

            InitWaveform();
            _recorder.VolumeChanged += OnVolumeChanged;
            _recorder.DurationUpdated += OnDurationUpdated;

            RefreshList();
        }

        // ── Waveform ──────────────────────────────────────────────────────────

        private void InitWaveform()
        {
            WaveformCanvas.Loaded += (_, _) => BuildBars();
        }

        private void BuildBars()
        {
            _bars.Clear();
            WaveformCanvas.Children.Clear();
            double canvasW = WaveformCanvas.ActualWidth;
            double canvasH = WaveformCanvas.ActualHeight;
            double barW = canvasW / BarCount - 1;
            double centerY = canvasH / 2;

            for (int i = 0; i < BarCount; i++)
            {
                var rect = new Rectangle
                {
                    Width = barW,
                    Height = 4,
                    RadiusX = 2, RadiusY = 2,
                    Fill = new SolidColorBrush(Color.FromRgb(0xDA, 0xDC, 0xE0))
                };
                Canvas.SetLeft(rect, i * (barW + 1));
                Canvas.SetTop(rect, centerY - 2);
                WaveformCanvas.Children.Add(rect);
                _bars.Add(rect);
            }
        }

        private int _barIndex = 0;
        private readonly Queue<double> _volumeHistory = new(Enumerable.Repeat(0.0, 40));

        private void OnVolumeChanged(object? sender, double rms)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_bars.Count == 0) return;
                _volumeHistory.Dequeue();
                _volumeHistory.Enqueue(rms);

                double canvasH = WaveformCanvas.ActualHeight;
                double centerY = canvasH / 2;
                int i = 0;
                foreach (var vol in _volumeHistory)
                {
                    if (i >= _bars.Count) break;
                    var bar = _bars[i];
                    double h = Math.Max(4, vol * canvasH * 3.5);
                    bar.Height = h;
                    Canvas.SetTop(bar, centerY - h / 2);
                    bar.Fill = new SolidColorBrush(Color.FromRgb(0xEA, 0x43, 0x35));
                    i++;
                }
            });
        }

        private void ResetWaveform()
        {
            if (_bars.Count == 0) return;
            double canvasH = WaveformCanvas.ActualHeight;
            double centerY = canvasH / 2;
            foreach (var bar in _bars)
            {
                bar.Height = 4;
                bar.Fill = new SolidColorBrush(Color.FromRgb(0xDA, 0xDC, 0xE0));
                Canvas.SetTop(bar, centerY - 2);
            }
        }

        // ── Recording ─────────────────────────────────────────────────────────

        private void OnDurationUpdated(object? sender, TimeSpan duration)
        {
            Dispatcher.InvokeAsync(() =>
                TimerLabel.Text = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                    : $"{duration.Minutes:D2}:{duration.Seconds:D2}");
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            _lastRecordedPath = _recorder.StartRecording(DataService.AudioDirectory);
            RecordButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            RecordingStatusLabel.Text = "● Recording...";
            RecordingStatusLabel.Foreground = (Brush)FindResource("DangerBrush");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            var (path, duration, size) = _recorder.StopRecording();
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            TimerLabel.Text = "00:00";
            RecordingStatusLabel.Text = "Recording saved";
            RecordingStatusLabel.Foreground = (Brush)FindResource("AccentBrush");
            ResetWaveform();

            if (string.IsNullOrEmpty(path) || duration < 0.5) return;

            // Determine file name
            var customName = FileNameBox.Text.Trim();
            var displayName = string.IsNullOrEmpty(customName)
                ? $"Recording {DateTime.Now:yyyy-MM-dd HH-mm-ss}"
                : customName;

            // Rename file if custom name provided
            if (!string.IsNullOrEmpty(customName))
            {
                var newPath = System.IO.Path.Combine(DataService.AudioDirectory,
                    $"{customName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                try { File.Move(path, newPath); path = newPath; }
                catch { /* keep original name */ }
            }

            var audioFile = new AudioFile
            {
                SpeakerId  = _currentUser.Id,
                SpeakerName = _currentUser.DisplayName,
                FileName   = displayName,
                FilePath   = path,
                DurationSeconds = duration,
                FileSizeBytes = size,
                ReviewerNotes = NotesBox.Text.Trim()
            };

            DataService.AddAudioFile(audioFile);
            FileNameBox.Text = string.Empty;
            NotesBox.Text = string.Empty;
            RefreshList();
        }

        // ── List Actions ──────────────────────────────────────────────────────

        private void RecordingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFile = RecordingsList.SelectedItem as AudioFile;
            bool hasSelection = _selectedFile != null;
            SendButton.IsEnabled = hasSelection && _selectedFile!.Status == FileStatus.Recorded;
            PlayButton.IsEnabled = hasSelection && File.Exists(_selectedFile!.FilePath);
            DeleteButton.IsEnabled = hasSelection && _selectedFile!.Status == FileStatus.Recorded;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;
            _selectedFile.Status = FileStatus.Sent;
            _selectedFile.SentAt = DateTime.UtcNow;
            DataService.UpdateAudioFile(_selectedFile);
            RefreshList();
            MessageBox.Show(
                $"{_selectedFile.FileName} has been sent for review.",
                "Sent",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null || !File.Exists(_selectedFile.FilePath)) return;

            _player?.Stop();
            _player?.Dispose();

            var reader = new AudioFileReader(_selectedFile.FilePath);
            _player = new WaveOutEvent();
            _player.Init(reader);
            _player.Play();
            _player.PlaybackStopped += (_, _) => reader.Dispose();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;
            var result = MessageBox.Show(
                $"Delete \"{_selectedFile.FileName}\"?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            DataService.DeleteAudioFile(_selectedFile.Id);
            RefreshList();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

        private void RefreshList()
        {
            var files = DataService.GetFilesForSpeaker(_currentUser.Id);
            RecordingsList.ItemsSource = files;
            RecordingCountLabel.Text = $"{files.Count} recording{(files.Count != 1 ? "s" : "")}";
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
            if (_recorder.IsRecording) _recorder.StopRecording();
            _player?.Stop();
            _player?.Dispose();
            _recorder.Dispose();
        }
    }
}
