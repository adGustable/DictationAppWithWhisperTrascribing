using NAudio.Wave;
using System.IO;

namespace DictationApp.Services
{
    public class AudioRecordingService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string _currentFilePath = string.Empty;
        private DateTime _recordingStartTime;
        private bool _isRecording;

        public event EventHandler<double>? VolumeChanged;
        public event EventHandler<TimeSpan>? DurationUpdated;

        public bool IsRecording => _isRecording;
        public double DurationSeconds => _isRecording
            ? (DateTime.UtcNow - _recordingStartTime).TotalSeconds
            : 0;

        private System.Timers.Timer? _durationTimer;

        public string StartRecording(string outputDir)
        {
            if (_isRecording) return _currentFilePath;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentFilePath = Path.Combine(outputDir, $"Recording_{timestamp}.wav");

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 1), // 44.1kHz mono
                BufferMilliseconds = 50
            };

            _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();

            _recordingStartTime = DateTime.UtcNow;
            _isRecording = true;

            _durationTimer = new System.Timers.Timer(500);
            _durationTimer.Elapsed += (_, _) =>
                DurationUpdated?.Invoke(this, DateTime.UtcNow - _recordingStartTime);
            _durationTimer.Start();

            return _currentFilePath;
        }

        public (string filePath, double durationSeconds, long fileSize) StopRecording()
        {
            if (!_isRecording) return (_currentFilePath, 0, 0);

            _durationTimer?.Stop();
            _durationTimer?.Dispose();
            _durationTimer = null;

            var duration = (DateTime.UtcNow - _recordingStartTime).TotalSeconds;

            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;

            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            _isRecording = false;

            var fileSize = File.Exists(_currentFilePath) ? new FileInfo(_currentFilePath).Length : 0;
            return (_currentFilePath, duration, fileSize);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // Calculate RMS volume for visualisation
            float sum = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i);
                float normalized = sample / 32768f;
                sum += normalized * normalized;
            }
            var rms = Math.Sqrt(sum / (e.BytesRecorded / 2));
            VolumeChanged?.Invoke(this, rms);
        }

        public static List<string> GetInputDevices()
        {
            var devices = new List<string>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
                devices.Add(WaveIn.GetCapabilities(i).ProductName);
            return devices;
        }

        public void Dispose()
        {
            if (_isRecording) StopRecording();
            _durationTimer?.Dispose();
            _waveIn?.Dispose();
            _writer?.Dispose();
        }
    }
}
