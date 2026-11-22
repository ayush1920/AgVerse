#ifndef Arch
  #define Arch "x64"
#endif

#ifndef Standalone
  #define Standalone "1"
#endif

#define MyAppName "AgVerse"
#define MyAppVersion "2.0.4"
#define MyAppPublisher "Ayush"
#define MyAppURL "https://github.com/ayush1920/AgVerse"
#define MyAppExeName "AgVerse.exe"

#if Standalone == "1"
  #define SourceDir "..\publish\" + Arch + "_selfcontained"
  #define OutputSuffix ""
#else
  #define SourceDir "..\publish\" + Arch
  #define OutputSuffix "-lightweight"
#endif

[Setup]
AppId={{5E28B594-F28B-4DC4-9128-B4C131804EFA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=Output
OutputBaseFilename=AgVerse-Setup-v{#MyAppVersion}-{#Arch}{#OutputSuffix}
SetupIconFile=..\icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes

#if Arch == "x64"
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\github_16.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\github_20.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\github_24.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\github_32.png"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
