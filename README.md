# MultiGravity - Multi-Profile Manager for Antigravity

A lightweight, sandboxed profile manager and multi-instance launcher for Google Antigravity IDE on Windows.

MultiGravity allows you to run multiple instances of Antigravity simultaneously, each logged into a completely independent Google account, without session conflicts, credential overwrites, or forced logouts.

---

## Downloads

Pre-built binaries and clean source archives for v1.0.1 are available directly under the [Releases](https://github.com/rodolford1112/MultiGravity-Multi-Profile-Manager-for-Antigravity/releases/tag/v1.0.1) page:

- `MultiGravity.exe`: Standalone Windows executable (portable, no Python installation required).
- `MultiGravity-Source.zip`: Source package containing editable code and assets.

### Changes in v1.0.1
- Fixed project chat history visibility: Automatically links `.gemini/antigravity` via native NTFS directory junction, allowing shared projects to seamlessly display all previous chats, conversation summaries, and artifacts in the Antigravity sidebar while keeping Google authentication tokens completely segregated.

---

## The Problem

Google Antigravity is built on the VS Code / Electron architecture. While launching multiple instances with `--user-data-dir` allows separate window states, Antigravity relies on an internal background process called `language_server.exe` to manage Google authentication, AI communication, and telemetry.

Under standard conditions:
1. `language_server.exe` stores Google OAuth tokens in the Windows Credential Manager (`wincred`) under a single global target: `gemini:antigravity`.
2. It resolves its `.gemini` home directory strictly relative to the system `%USERPROFILE%`.
3. When you open a second instance and log into Account B, `language_server.exe` overwrites the credentials in Windows Credential Manager. As soon as Account A attempts a token refresh, its session is invalidated and logged out.

---

## How It Works

MultiGravity solves this problem at the operating system and environment level:

### 1. Isolated User Data and Home Directories
Each profile is allocated an isolated directory structure inside the `profiles/<ProfileName>/` folder:
- `userData/`: Houses Electron/VS Code state, caches, cookies, and window settings.
- `home/`: Serves as a virtual `%USERPROFILE%`. MultiGravity overrides the process environment variable `USERPROFILE` to point directly here, ensuring that `.gemini` configuration files remain entirely segregated.

### 2. Windows Credential Ring Bypass
During authentication, `language_server.exe` inspects its environment for remote or headless development markers. By injecting:
```
SSH_CONNECTION=127.0.0.1 1234 127.0.0.1 22
```
into the child process environment, the internal `sshDetector` triggers `shouldBypassKeyring()`. This instructs the language server to bypass the Windows Credential Manager entirely and store OAuth tokens in local file-based storage inside the isolated `<profile>/home/.gemini` directory.

As a result, each instance retains full autonomy over its authentication lifecycle.

### 3. Default-Shared Projects with Exclusion Lists
Antigravity stores workspace descriptors as JSON files inside `.gemini/config/projects/`. 

MultiGravity adopts a **Default-Shared** model:
- **Shared by Default**: Whenever you create a new project in any profile, it is automatically available across all your profiles.
- **Exclusion Lists**: If you have sensitive, client-specific, or personal projects you wish to hide from a particular profile, simply add them to that profile's **Exclusion List**.
- **Sidecar Filtering**: Antigravity's internal sidecars (e.g., `swarm/swarm_board`) are automatically filtered out so they never clutter your project list.

---

## Features

- Multi-Instance Support: Open as many concurrent Antigravity windows as your system resources allow.
- Complete Account Isolation: Log into distinct Google accounts per window with no cross-account token invalidation.
- Default-Shared Projects: All projects are automatically synchronized across accounts unless placed on a profile's Exclusion List.
- Sidecar & Swarm Isolation: Automatically ignores internal panels and sidecars like `swarm/swarm_board`.
- High Performance C# & Python Implementations: 
  - **Native C# (.NET 9 WinForms)**: Super lightweight (~260 KB single-file binary), instant startup, zero Python/Tkinter dependencies.
  - **Python (Tkinter)**: Full source code in `main.py`.
- Headless CLI Mode: Desktop shortcuts invoke `MultiGravity.exe --launch <Profile>` to auto-synchronize projects in 1ms before launching Antigravity detached.
- Desktop Shortcuts: Generate one-click shortcuts on the Windows Desktop for any specific profile.
- Multilingual: Dynamic interface translation supporting English, Portuguese, Spanish, and Russian.

---

## How to Use

### Running the Application

You can run the pre-compiled executable directly:
```
MultiGravity.exe
```

Or run from source using Python:
```
python main.py
```

### Creating and Launching Profiles

1. Enter a profile name in the text input (e.g., `Work`, `Personal`, `ClientA`).
2. Click **Add Profile**.
3. Select the profile from the list and click **Launch Selected Profile**.
4. In the newly opened Antigravity window, sign into your designated Google account.
5. Repeat for any additional profiles. All instances can run at the same time.

### Managing Projects (Shared, Excluded & Archived)

1. Click **Manage Projects** in the main window.
2. Select your target profile from the dropdown at the top.
3. Inspect your projects in the visual table with real-time status indicators:
   - 🟢 **Shared**: Active and available in this profile's Antigravity sidebar.
   - 🟡 **Excluded**: Blocked from this profile. Click **Toggle Exclude** or use right-click to change.
   - ⚪ **Archived**: Archived globally across all accounts.
4. Click **Refresh** to re-scan your disk and profiles for newly added projects at any time.
5. Double-click any project (or click **Open Folder**) to immediately view its codebase in Windows Explorer.
6. Click **Save & Apply** to update your profiles.

### Creating Direct Desktop Shortcuts

1. Select a profile from the list.
2. Click **Create Desktop Shortcut**.
3. A shortcut named `Antigravity - <ProfileName>.lnk` will be created on your Desktop, allowing you to launch that profile directly without opening the manager first.

---

## Directory Structure

```
MultiGravity/
|-- MultiGravity.exe               # Standalone executable
|-- main.py                        # Source code
|-- icon.ico                       # Application icon
|-- README.md                      # Documentation
|-- profiles/                      # Sandboxed profiles directory
    |-- Profile_A/
    |   |-- home/                  # Virtual %USERPROFILE% (.gemini, auth tokens)
    |   `-- userData/              # Electron / VS Code data
    `-- Profile_B/
        |-- home/
        `-- userData/
```

---

## Building from Source

To compile the application into a single standalone executable using PyInstaller:

```bash
pip install pyinstaller
pyinstaller --onefile --noconsole --icon=icon.ico --name=MultiGravity main.py
```

The resulting binary will be generated as `dist/MultiGravity.exe`.

---

## Requirements

- Operating System: Windows 10 or Windows 11 (64-bit)
- Target Application: Google Antigravity IDE installed in standard paths (`%LOCALAPPDATA%\Programs\antigravity` or `Program Files`)
- Python (only if running from source): Python 3.8 or newer (Tkinter included by default in standard Windows Python installations)

---

## License

This utility is distributed under the MIT License.
