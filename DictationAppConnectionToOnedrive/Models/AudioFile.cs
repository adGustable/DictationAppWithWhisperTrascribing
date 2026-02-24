namespace DictationApp.Models
{
    public enum FileStatus
    {
        Recorded,
        Sent,
        Transcribing,
        Transcribed,
        Reviewed,
        Exported
    }

    public class AudioFile
    {
        public string   Id              { get; set; } = Guid.NewGuid().ToString();
        public string   FileName        { get; set; } = string.Empty;
        public string   FilePath        { get; set; } = string.Empty;
        public double   DurationSeconds { get; set; }
        public long     FileSizeBytes   { get; set; }
        public DateTime RecordedAt      { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt         { get; set; }
        public FileStatus Status        { get; set; } = FileStatus.Recorded;
        public string?  Transcription   { get; set; }
        public string?  ReviewerNotes   { get; set; }

        // ── Computed display properties ────────────────────────────────────────

        public string StatusDisplay => Status switch
        {
            FileStatus.Recorded    => "🎙 Recorded",
            FileStatus.Sent        => "📤 Sent",
            FileStatus.Transcribing => "⚙ Transcribing...",
            FileStatus.Transcribed => "📝 Transcribed",
            FileStatus.Reviewed    => "✅ Reviewed",
            FileStatus.Exported    => "📄 Exported",
            _ => Status.ToString()
        };

        public string DurationDisplay
        {
            get
            {
                var ts = TimeSpan.FromSeconds(DurationSeconds);
                return ts.TotalHours >= 1
                    ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                    : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
        }

        public string FileSizeDisplay
        {
            get
            {
                if (FileSizeBytes < 1024)                return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024)         return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }
    }
}
