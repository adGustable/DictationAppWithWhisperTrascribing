using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace DictationApp.Services
{
    /// <summary>
    /// Transcribes audio using the OpenAI Whisper API.
    /// Requires an API key set in Settings or environment variable OPENAI_API_KEY.
    /// </summary>
    public class WhisperService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public WhisperService(string apiKey)
        {
            _apiKey = apiKey;
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Transcribes the given audio file using OpenAI Whisper.
        /// Supported formats: mp3, mp4, mpeg, mpga, m4a, wav, webm.
        /// </summary>
        public async Task<string> TranscribeAsync(
            string audioFilePath,
            string language = "en",
            IProgress<string>? progress = null)
        {
            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException("Audio file not found.", audioFilePath);

            progress?.Report("Uploading audio to Whisper API...");

            using var form = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(audioFilePath);
            using var fileContent = new StreamContent(fileStream);

            var ext = Path.GetExtension(audioFilePath).TrimStart('.').ToLower();
            fileContent.Headers.ContentType = new MediaTypeHeaderValue($"audio/{ext}");
            form.Add(fileContent, "file", Path.GetFileName(audioFilePath));
            form.Add(new StringContent("whisper-1"), "model");
            form.Add(new StringContent(language), "language");
            form.Add(new StringContent("verbose_json"), "response_format");

            progress?.Report("Transcribing — this may take a moment...");

            var response = await _http.PostAsync(
                "https://api.openai.com/v1/audio/transcriptions", form);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Whisper API error ({(int)response.StatusCode}): {json}");

            progress?.Report("Processing results...");

            var obj = JObject.Parse(json);
            return obj["text"]?.ToString() ?? string.Empty;
        }

        public static bool IsApiKeySet(string key) =>
            !string.IsNullOrWhiteSpace(key) && key.StartsWith("sk-");
    }
}
