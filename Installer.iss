; =====================================================
; Network Speed Toggle v1.2 - GitHub Release Installer
; =====================================================

#define MyAppName "Network Speed Toggle"
#define MyAppVersion "1.2"
#define MyAppPublisher "FoggyPunk"
#define MyAppExeName "NetworkSpeedToggle.exe"

[Setup]
AppId={{D37D0ED6-5E8D-4131-B2C1-30A5840AC97B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
InfoBeforeFile=changelog.txt

; --- ICONS ---
SetupIconFile=Resources\25g.ico
WizardSmallImageFile=Resources\25g.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}

; --- UI TWEAKS ---
AllowNoIcons=yes
DirExistsWarning=no
CloseApplications=yes
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=NetworkSpeedToggle_{#MyAppVersion}_Installer
PrivilegesRequired=admin
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Start {#MyAppName} automatically when Windows starts"; GroupDescription: "Auto-start Options:"; Flags: checkedonce

[Files]
Source: "bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "changelog.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commonstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: postinstall skipifsilent nowait

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName} /T"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "schtasks.exe"; Parameters: "/delete /tn ""{#MyAppName}"" /f"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\NetworkSpeedToggle\config.json"
Type: dirifempty; Name: "{localappdata}\NetworkSpeedToggle"
Type: dirifempty; Name: "{app}\Resources"
Type: dirifempty; Name: "{app}"
