using NAudio.Wave;
using System.IO;

namespace DictationApp.Services
{
    public enum RecordingState { Idle, Recording, Paused }

    public class AudioRecordingService : IDisposable
    {
        private WaveInEvent?    _waveIn;
        private WaveFileWriter? _writer;
        private string          _currentFilePath = string.Empty;
        private DateTime        _recordingStartTime;
        private TimeSpan        _accumulatedTime = TimeSpan.Zero;
        private DateTime        _segmentStartTime;

        private System.Timers.Timer? _durationTimer;

        public RecordingState State { get; private set; } = RecordingState.Idle;
        public bool IsRecording => State == RecordingState.Recording;
        public bool IsPaused    => State == RecordingState.Paused;
        public bool IsActive    => State != RecordingState.Idle;

        /// <summary>Current wall-clock duration of the recording (excluding paused gaps).</summary>
        public TimeSpan Duration =>
            State == RecordingState.Recording
                ? _accumulatedTime + (DateTime.UtcNow - _segmentStartTime)
                : _accumulatedTime;

        public event EventHandler<double>?   VolumeChanged;
        public event EventHandler<TimeSpan>? DurationUpdated;

        // ── Start ──────────────────────────────────────────────────────────────

        /// <summary>Begin a new recording. Returns the file path.</summary>
        public string Start(string outputDir)
        {
            if (State != RecordingState.Idle)
                throw new InvalidOperationException("Already recording.");

            Directory.CreateDirectory(outputDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentFilePath = Path.Combine(outputDir, $"dictation_{timestamp}.wav");

            _accumulatedTime = TimeSpan.Zero;
            _recordingStartTime = DateTime.UtcNow;
            _segmentStartTime   = DateTime.UtcNow;

            OpenWaveIn();
            State = RecordingState.Recording;
            StartTimer();

            return _currentFilePath;
        }

        // ── Pause ──────────────────────────────────────────────────────────────

        /// <summary>Pause recording — audio already captured is kept in the same file.</summary>
        public void Pause()
        {
            if (State != RecordingState.Recording) return;

            _accumulatedTime += DateTime.UtcNow - _segmentStartTime;
            _waveIn?.StopRecording();
            // Keep _writer open so we can append on Resume
            State = RecordingState.Paused;
            _durationTimer?.Stop();
        }

        // ── Resume ─────────────────────────────────────────────────────────────

        /// <summary>Resume after a pause — audio is appended to the same WAV file.</summary>
        public void Resume()
        {
            if (State != RecordingState.Paused) return;

            _segmentStartTime = DateTime.UtcNow;
            // Re-create WaveInEvent and attach to the still-open writer
            _waveIn?.Dispose();
            _waveIn = null;
            OpenWaveIn();
            State = RecordingState.Recording;
            _durationTimer?.Start();
        }

        // ── Stop ───────────────────────────────────────────────────────────────

        /// <summary>Stop and finalise the recording. Returns (path, duration, fileSize).</summary>
        public (string filePath, TimeSpan duration, long fileSize) Stop()
        {
            if (State == RecordingState.Idle)
                return (_currentFilePath, TimeSpan.Zero, 0);

            if (State == RecordingState.Recording)
                _accumulatedTime += DateTime.UtcNow - _segmentStartTime;

            _durationTimer?.Stop();
            _durationTimer?.Dispose();
            _durationTimer = null;

            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;

            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            var duration = _accumulatedTime;
            var fileSize = File.Exists(_currentFilePath)
                ? new FileInfo(_currentFilePath).Length
                : 0;

            State = RecordingState.Idle;
            _accumulatedTime = TimeSpan.Zero;

            return (_currentFilePath, duration, fileSize);
        }

        // ── Input devices ──────────────────────────────────────────────────────

        public static List<string> GetInputDevices()
        {
            var list = new List<string>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
                list.Add(WaveIn.GetCapabilities(i).ProductName);
            return list;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void OpenWaveIn()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 1),
                BufferMilliseconds = 50
            };

            // If writer already exists (resume case) just keep using it.
            // On first start, create the writer.
            _writer ??= new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }

        private void StartTimer()
        {
            _durationTimer = new System.Timers.Timer(250);
            _durationTimer.Elapsed += (_, _) =>
                DurationUpdated?.Invoke(this, Duration);
            _durationTimer.Start();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_writer == null) return;
            _writer.Write(e.Buffer, 0, e.BytesRecorded);

            // RMS volume for waveform visualisation
            float sum = 0;
            int sampleCount = e.BytesRecorded / 2;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short s = BitConverter.ToInt16(e.Buffer, i);
                float n = s / 32768f;
                sum += n * n;
            }
            var rms = sampleCount > 0 ? Math.Sqrt(sum / sampleCount) : 0;
            VolumeChanged?.Invoke(this, rms);
        }

        public void Dispose()
        {
            if (IsActive) Stop();
            _durationTimer?.Dispose();
            _waveIn?.Dispose();
            _writer?.Dispose();
        }
    }
}
