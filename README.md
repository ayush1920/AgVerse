# AgVerse - Multi-Profile Manager & Telemetry for Google Antigravity

[![GitHub Release](https://img.shields.io/github/v/release/ayush1920/AgVerse?color=blue&label=AgVerse%20Release)](https://github.com/ayush1920/AgVerse/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue)](https://github.com/ayush1920/AgVerse)

**AgVerse** is a fast, lightweight profile manager, workspace synchronizer, and credential vault bridge for **Google Antigravity IDE** on Windows.

AgVerse allows you to run multiple instances of Antigravity simultaneously, each logged into an independent Google account, without session conflicts, credential overwrites, or forced logouts. It also bridges authentication and quota telemetry seamlessly with external tools like Antigravity Rainmeter widgets.

---

## 🚀 Downloads & Installers

Official standalone installers and portable single-file binaries are available under [Releases](https://github.com/ayush1920/AgVerse/releases):

| Architecture | Setup Installer | Portable (Lightweight, ~450 KB) | Portable (Self-Contained, ~50 MB) |
| :--- | :--- | :--- | :--- |
| **x64 (64-bit Windows)** | [AgVerse-Setup-v2.0.0-x64.exe](https://github.com/ayush1920/AgVerse/releases) | `AgVerse-v2.0.0-win-x64-portable.exe` | `AgVerse-v2.0.0-win-x64-portable-selfcontained.exe` |
| **x86 (32-bit Windows)** | [AgVerse-Setup-v2.0.0-x86.exe](https://github.com/ayush1920/AgVerse/releases) | `AgVerse-v2.0.0-win-x86-portable.exe` | `AgVerse-v2.0.0-win-x86-portable-selfcontained.exe` |

> **Note**:
> - **Lightweight Portable (~450 KB)**: Requires Windows 10/11 with .NET Desktop Runtime installed. Instant download with zero runtime overhead.
> - **Self-Contained Portable (~50 MB)**: Bundles the entire .NET runtime engine. Runs on any clean Windows machine out-of-the-box with zero prerequisites.

### Installer Features
- **Zero Admin Elevation Needed**: Installs cleanly per-user into `Local Programs` or system-wide without UAC permission friction.
- **Desktop & Start Menu Shortcuts**: Option to create instant desktop shortcuts.
- **Auto Launch**: Optional checkbox to launch AgVerse immediately upon installation completion.

---

## ⚡ Key Capabilities

- **Simultaneous Multi-Account Instances**: Run as many concurrent Antigravity windows as needed, each bound to a separate Google account.
- **Windows Credential Ring Bypass**: Automatically stores OAuth credentials safely in isolated profile homes via environment sandboxing, preventing global `wincred` overwriting.
- **Default-Shared Project Model**: Workspaces created in one profile are instantly discoverable across all profiles, with granular exclusion and global archiving capabilities.
- **Bulk Project Operations**: Multi-select projects using checkboxes for instant batch sharing, exclusion, or archiving.
- **Token & Quota Synchronization**:
  - Live account display (masked email and token validity) right on the main screen.
  - One-click token refresh and interactive browser sign-in fallback.
  - Seamless credential sharing with the Antigravity Limits Rainmeter desktop skin.
- **Verbose Mode Toggle**: One-click toggle in the UI to launch Antigravity with full diagnostic conversation logging (`chat.conversationSteps: verbose`).
- **One-Click Desktop Shortcuts**: Generates custom `.lnk` shortcuts to launch any profile directly from your desktop.
- **Multilingual Support**: Real-time interface translation for English, Portuguese, Spanish, and Russian.
- **Native .NET 9 Performance**: Fast startup, crisp vector UI rendering, embedded assets, and minimal memory footprint.

---

## 🛠️ How It Works

### 1. Isolated User Data and Profile Homes
Each profile is allocated an isolated directory structure inside `profiles/<ProfileName>/`:
- `userData/`: Houses Electron/VS Code state, caches, cookies, and window settings.
- `home/`: Serves as a virtual `%USERPROFILE%`. AgVerse sets `USERPROFILE` to point here, guaranteeing that `.gemini` configurations remain isolated.

### 2. Credential Ring Sandboxing
During authentication, `language_server.exe` checks for remote or headless development markers. By setting:
```bash
SSH_CONNECTION=127.0.0.1 1234 127.0.0.1 22
```
in the child process environment, the language server bypasses the Windows Credential Manager (`gemini:antigravity`) and stores OAuth tokens locally inside `<profile>/home/.gemini`.

### 3. Unified Token Vault & Rainmeter Bridge
AgVerse links token state across profiles into a unified vault directory (`%USERPROFILE%/.gemini/token_vault`), ensuring that Rainmeter widgets, background quota trackers, and IDE sessions always have valid authorization without needing duplicate logins.

---

## 💻 Building from Source

### Prerequisites
- Windows 10 / 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (for building the installers)

### Build Standalone Single-File Binaries
```powershell
# Publish x64
dotnet publish src/AgVerse/AgVerse.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/x64

# Publish x86
dotnet publish src/AgVerse/AgVerse.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/x86
```

### Build Installers
```powershell
iscc.exe /DArch=x64 installer/AgVerse_Setup.iss
iscc.exe /DArch=x86 installer/AgVerse_Setup.iss
```

The compiled setup files will be placed into `installer/Output/`.

---

## 📜 License

Distributed under the MIT License. See [LICENSE](LICENSE) for details.
