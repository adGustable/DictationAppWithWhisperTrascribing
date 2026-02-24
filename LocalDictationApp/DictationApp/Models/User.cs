namespace DictationApp.Models
{
    public enum UserRole
    {
        Speaker,
        Reviewer
    }

    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
