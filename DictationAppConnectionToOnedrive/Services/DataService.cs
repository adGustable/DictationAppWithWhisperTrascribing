using DictationApp.Models;
using Newtonsoft.Json;
using System.IO;

namespace DictationApp.Services
{
    /// <summary>
    /// Manages persistence of AudioFile metadata. No user authentication — role selection
    /// is handled at app startup via the HomeWindow.
    /// </summary>
    public static class DataService
    {
        private static string _dataDir  = string.Empty;
        private static string _filesFile = string.Empty;

        public static List<AudioFile> AudioFiles { get; private set; } = new();

        public static void Initialize()
        {
            _dataDir  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DictationApp");
            _filesFile = Path.Combine(_dataDir, "audiofiles.json");

            Directory.CreateDirectory(_dataDir);

            // Ensure the Speaker output folder exists
            var speakerFolder = SettingsService.SpeakerOutputFolder;
            Directory.CreateDirectory(speakerFolder);

            LoadAudioFiles();
        }

        // ── CRUD ───────────────────────────────────────────────────────────────

        public static void AddAudioFile(AudioFile file)
        {
            AudioFiles.Add(file);
            Save();
        }

        public static void UpdateAudioFile(AudioFile file)
        {
            var idx = AudioFiles.FindIndex(f => f.Id == file.Id);
            if (idx >= 0) { AudioFiles[idx] = file; Save(); }
        }

        public static void DeleteAudioFile(string fileId)
        {
            var file = AudioFiles.FirstOrDefault(f => f.Id == fileId);
            if (file == null) return;
            AudioFiles.Remove(file);
            if (File.Exists(file.FilePath))
            {
                try { File.Delete(file.FilePath); } catch { /* ignore lock errors */ }
            }
            Save();
        }

        // ── Queries ────────────────────────────────────────────────────────────

        /// <summary>All recordings, newest first.</summary>
        public static List<AudioFile> GetAllRecordings() =>
            AudioFiles.OrderByDescending(f => f.RecordedAt).ToList();

        /// <summary>Recordings from today.</summary>
        public static List<AudioFile> GetRecordingsToday()
        {
            var today = DateTime.Today;
            return AudioFiles
                .Where(f => f.RecordedAt.Date == today)
                .OrderByDescending(f => f.RecordedAt)
                .ToList();
        }

        /// <summary>Recordings from the current calendar week (Mon–Sun).</summary>
        public static List<AudioFile> GetRecordingsThisWeek()
        {
            var today = DateTime.Today;
            // ISO week starts Monday
            int dayOfWeek = ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-dayOfWeek);
            return AudioFiles
                .Where(f => f.RecordedAt.Date >= weekStart)
                .OrderByDescending(f => f.RecordedAt)
                .ToList();
        }

        // ── Persistence ────────────────────────────────────────────────────────

        private static void LoadAudioFiles()
        {
            if (!File.Exists(_filesFile)) return;
            try
            {
                var json = File.ReadAllText(_filesFile);
                AudioFiles = JsonConvert.DeserializeObject<List<AudioFile>>(json) ?? new();
            }
            catch { AudioFiles = new(); }
        }

        public static void Save()
        {
            File.WriteAllText(_filesFile,
                JsonConvert.SerializeObject(AudioFiles, Formatting.Indented));
        }
    }
}
