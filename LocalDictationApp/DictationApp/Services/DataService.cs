using DictationApp.Models;
using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DictationApp.Services
{
    public static class DataService
    {
        private static string _dataDir = string.Empty;
        private static string _audioDir = string.Empty;
        private static string _usersFile = string.Empty;
        private static string _filesFile = string.Empty;

        public static List<User> Users { get; private set; } = new();
        public static List<AudioFile> AudioFiles { get; private set; } = new();

        public static void Initialize()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DictationApp");
            _audioDir = Path.Combine(_dataDir, "Audio");
            _usersFile = Path.Combine(_dataDir, "users.json");
            _filesFile = Path.Combine(_dataDir, "audiofiles.json");

            Directory.CreateDirectory(_dataDir);
            Directory.CreateDirectory(_audioDir);

            LoadUsers();
            LoadAudioFiles();

            // Seed default accounts if none exist
            if (!Users.Any())
                SeedDefaultUsers();
        }

        public static string AudioDirectory => _audioDir;

        private static void SeedDefaultUsers()
        {
            Users.Add(new User
            {
                Username = "speaker1",
                PasswordHash = HashPassword("password"),
                DisplayName = "Dr. Sarah Mitchell",
                Role = UserRole.Speaker
            });
            Users.Add(new User
            {
                Username = "speaker2",
                PasswordHash = HashPassword("password"),
                DisplayName = "Dr. James Carter",
                Role = UserRole.Speaker
            });
            Users.Add(new User
            {
                Username = "reviewer1",
                PasswordHash = HashPassword("password"),
                DisplayName = "Emma Thompson",
                Role = UserRole.Reviewer
            });
            Users.Add(new User
            {
                Username = "reviewer2",
                PasswordHash = HashPassword("password"),
                DisplayName = "Oliver Bennett",
                Role = UserRole.Reviewer
            });
            SaveUsers();
        }

        public static User? Authenticate(string username, string password)
        {
            var hash = HashPassword(password);
            return Users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == hash);
        }

        public static bool RegisterUser(string username, string password, string displayName, UserRole role)
        {
            if (Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return false;

            Users.Add(new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                DisplayName = displayName,
                Role = role
            });
            SaveUsers();
            return true;
        }

        public static void AddAudioFile(AudioFile file)
        {
            AudioFiles.Add(file);
            SaveAudioFiles();
        }

        public static void UpdateAudioFile(AudioFile file)
        {
            var index = AudioFiles.FindIndex(f => f.Id == file.Id);
            if (index >= 0)
            {
                AudioFiles[index] = file;
                SaveAudioFiles();
            }
        }

        public static void DeleteAudioFile(string fileId)
        {
            var file = AudioFiles.FirstOrDefault(f => f.Id == fileId);
            if (file != null)
            {
                AudioFiles.Remove(file);
                if (File.Exists(file.FilePath))
                    File.Delete(file.FilePath);
                SaveAudioFiles();
            }
        }

        public static List<AudioFile> GetFilesForSpeaker(string speakerId) =>
            AudioFiles.Where(f => f.SpeakerId == speakerId)
                      .OrderByDescending(f => f.RecordedAt)
                      .ToList();

        public static List<AudioFile> GetFilesForReview() =>
            AudioFiles.Where(f => f.Status >= FileStatus.Sent)
                      .OrderByDescending(f => f.SentAt ?? f.RecordedAt)
                      .ToList();

        public static List<User> GetSpeakers() =>
            Users.Where(u => u.Role == UserRole.Speaker).ToList();

        public static List<User> GetReviewers() =>
            Users.Where(u => u.Role == UserRole.Reviewer).ToList();

        private static void LoadUsers()
        {
            if (File.Exists(_usersFile))
            {
                var json = File.ReadAllText(_usersFile);
                Users = JsonConvert.DeserializeObject<List<User>>(json) ?? new();
            }
        }

        private static void LoadAudioFiles()
        {
            if (File.Exists(_filesFile))
            {
                var json = File.ReadAllText(_filesFile);
                AudioFiles = JsonConvert.DeserializeObject<List<AudioFile>>(json) ?? new();
            }
        }

        public static void SaveUsers()
        {
            File.WriteAllText(_usersFile, JsonConvert.SerializeObject(Users, Formatting.Indented));
        }

        public static void SaveAudioFiles()
        {
            File.WriteAllText(_filesFile, JsonConvert.SerializeObject(AudioFiles, Formatting.Indented));
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "DictationAppSalt2024"));
            return Convert.ToBase64String(bytes);
        }
    }
}
