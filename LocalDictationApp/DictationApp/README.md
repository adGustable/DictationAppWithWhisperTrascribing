# 🎙 DictationApp — WPF C# Dictation System

A professional voice dictation application with Speaker and Reviewer roles, Whisper AI transcription, and Word document export.

---

## Features

| Feature | Details |
|---------|---------|
| **Multi-Account System** | Speaker and Reviewer roles with login/registration |
| **Audio Recording** | High-quality 44.1kHz WAV recording via NAudio |
| **Live Waveform** | Real-time volume visualiser while recording |
| **Whisper Transcription** | OpenAI Whisper API integration (state-of-the-art accuracy) |
| **Transcription Editor** | Full rich text editor — reviewers can correct errors |
| **Word Export** | Professional `.docx` export with metadata table |
| **File Queue** | Reviewers see all files sent from speakers |
| **Local Storage** | All data stored in `%AppData%\DictationApp` |

---

## Project Structure

```
DictationApp/
├── Models/
│   ├── User.cs             # User model (Speaker / Reviewer roles)
│   └── AudioFile.cs        # Recording model with status tracking
├── Services/
│   ├── DataService.cs      # JSON-based data persistence
│   ├── AudioRecordingService.cs  # NAudio recording wrapper
│   ├── WhisperService.cs   # OpenAI Whisper API client
│   ├── WordExportService.cs # DocumentFormat.OpenXml export
│   └── SettingsService.cs  # API key & preference storage
├── Views/
│   ├── LoginWindow.xaml    # Login + Registration
│   ├── SpeakerWindow.xaml  # Speaker dashboard (record + send)
│   ├── ReviewerWindow.xaml # Reviewer dashboard (transcribe + edit + export)
│   └── SettingsWindow.xaml # API key configuration
├── Themes/
│   └── AppTheme.xaml       # Global styles, colors, control templates
└── DictationApp.csproj
```

---

## Prerequisites

- **Visual Studio 2022** (with .NET Desktop Workload) or **Rider**
- **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **OpenAI API Key** — https://platform.openai.com (for Whisper transcription)
- A working **microphone** for recording

---

## Setup & Run

### 1. Open the project
```
File → Open → Project/Solution → DictationApp.csproj
```

### 2. Restore NuGet packages
Visual Studio restores automatically. Or in Terminal:
```bash
dotnet restore
```

### 3. Build & Run
```
F5  or  Ctrl+F5
```

### 4. Configure Whisper API Key
1. Sign in with a **Reviewer** account
2. Click **⚙ Settings** in the top bar
3. Paste your OpenAI API key (`sk-...`)
4. Click **Save Settings**

---

## Default Accounts

| Username | Password | Role |
|----------|----------|------|
| `speaker1` | `password` | Speaker (Dr. Sarah Mitchell) |
| `speaker2` | `password` | Speaker (Dr. James Carter) |
| `reviewer1` | `password` | Reviewer (Emma Thompson) |
| `reviewer2` | `password` | Reviewer (Oliver Bennett) |

---

## Whisper API Integration

The app uses **OpenAI's Whisper API** (`whisper-1` model):

```
POST https://api.openai.com/v1/audio/transcriptions
```

### How it works
1. Speaker records audio → saved as `.wav` in `%AppData%\DictationApp\Audio\`
2. Speaker clicks **Send for Review** → file status set to "Sent"
3. Reviewer selects the file → clicks **Transcribe with Whisper**
4. App uploads the WAV file to OpenAI (multipart/form-data)
5. Whisper returns the full transcript text
6. Reviewer edits the text in the editor
7. Reviewer clicks **Export to Word** → `.docx` generated

### Supported audio formats
WAV (default), MP3, MP4, M4A, MPEG, WEBM

### Pricing
~$0.006 per minute of audio. A 10-minute recording ≈ $0.06.

---

## NuGet Packages

| Package | Version | Use |
|---------|---------|-----|
| `NAudio` | 2.2.1 | Audio recording & playback |
| `DocumentFormat.OpenXml` | 3.0.2 | Word `.docx` export |
| `Newtonsoft.Json` | 13.0.3 | JSON data persistence |

---

## Workflow

```
SPEAKER                         REVIEWER
   │                               │
   ├─ Record audio                 │
   ├─ Review recording             │
   ├─ Send for Review ────────────►│
   │                               ├─ See file in queue
   │                               ├─ Play audio
   │                               ├─ Click "Transcribe with Whisper"
   │                               ├─ Edit transcription
   │                               ├─ Add reviewer notes
   │                               ├─ Mark as Reviewed
   │                               └─ Export to Word (.docx)
```

---

## Data Storage

All data is stored locally in JSON files:

```
%AppData%\DictationApp\
├── users.json          # User accounts (passwords SHA-256 hashed)
├── audiofiles.json     # Recording metadata & transcriptions
├── settings.ini        # API key & preferences
└── Audio\              # WAV recordings
    ├── Recording_20240101_120000.wav
    └── ...
```

---

## Extending the App

- **Add Azure Speech / Google STT**: Implement an `ISpeechService` interface alongside `WhisperService`
- **Email notifications**: Add `System.Net.Mail` to notify reviewers when files are sent
- **Cloud sync**: Replace `DataService` JSON storage with a SQLite or REST API backend
- **Audio compression**: Use NAudio to convert WAV → MP3 before upload to reduce API costs
