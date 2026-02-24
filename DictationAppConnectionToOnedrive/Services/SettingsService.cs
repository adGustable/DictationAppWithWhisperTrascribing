using System.IO;

namespace DictationApp.Services
{
    public static class SettingsService
    {
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DictationApp", "settings.ini");

        /// <summary>The folder where the Speaker saves recorded WAV files.</summary>
        public static string SpeakerOutputFolder
        {
            get
            {
                var v = ReadKey("SpeakerOutputFolder");
                return string.IsNullOrEmpty(v) ? DefaultAudioFolder : v;
            }
            set => WriteKey("SpeakerOutputFolder", value);
        }

        /// <summary>The folder the Reviewer opens to find audio files (e.g. the same OneDrive folder).</summary>
        public static string ReviewerWorkingFolder
        {
            get
            {
                var v = ReadKey("ReviewerWorkingFolder");
                return string.IsNullOrEmpty(v) ? DefaultAudioFolder : v;
            }
            set => WriteKey("ReviewerWorkingFolder", value);
        }

        private static string DefaultAudioFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DictationApp");

        // ── IO helpers ─────────────────────────────────────────────────────────

        private static string ReadKey(string key)
        {
            if (!File.Exists(SettingsPath)) return string.Empty;
            foreach (var line in File.ReadAllLines(SettingsPath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                    return parts[1].Trim();
            }
            return string.Empty;
        }

        private static void WriteKey(string key, string value)
        {
            var lines = File.Exists(SettingsPath)
                ? File.ReadAllLines(SettingsPath).ToList()
                : new List<string>();

            var idx = lines.FindIndex(l => l.StartsWith(key + "="));
            var entry = $"{key}={value}";
            if (idx >= 0) lines[idx] = entry;
            else lines.Add(entry);

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllLines(SettingsPath, lines);
        }
    }
}
