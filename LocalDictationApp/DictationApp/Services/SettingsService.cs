using System.IO;

namespace DictationApp.Services
{
    public static class SettingsService
    {
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DictationApp", "settings.ini");

        public static string WhisperApiKey
        {
            get => ReadKey("WhisperApiKey");
            set => WriteKey("WhisperApiKey", value);
        }

        public static string DefaultLanguage
        {
            get
            {
                var v = ReadKey("DefaultLanguage");
                return string.IsNullOrEmpty(v) ? "en" : v;
            }
            set => WriteKey("DefaultLanguage", value);
        }

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
