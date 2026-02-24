using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DictationApp.Services
{
    public class LocalWhisperService
    {
        private readonly string _exePath;
        private readonly string _modelPath;

        public LocalWhisperService()
        {
            _exePath = @"D:\Whisper\whisper.cpp-master\build\bin\Release\whisper-cli.exe";
            _modelPath = @"D:\Whisper\whisper.cpp-master\models\ggml-base.en.bin";

            if (!File.Exists(_exePath))
                throw new FileNotFoundException("Whisper executable not found.", _exePath);

            if (!File.Exists(_modelPath))
                throw new FileNotFoundException("Whisper model not found.", _modelPath);
        }

        public async Task<string> TranscribeAsync(
            string audioFilePath,
            string language = "en",
            IProgress<string>? progress = null)
        {
            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException("Audio file not found.", audioFilePath);

            progress?.Report("Starting local Whisper...");

            var outputBuilder = new StringBuilder();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _exePath,
                    Arguments = $"-m \"{_modelPath}\" -f \"{audioFilePath}\" -l {language} --no-timestamps",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"Local Whisper error: {stderr}");

            return stdout;
        }
    }
}