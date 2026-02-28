#define MyAppName "Network Speed Toggle"
#define MyAppVersion "1.1"
#define MyAppPublisher "FoggyPunk"
#define MyAppExeName "NetworkSpeedToggle.exe"

[Setup]
AppId={{D37D0ED6-5E8D-4131-B2C1-30A5840AC97B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes

InfoBeforeFile="C:\Users\marce\OneDrive\Documents\Streaming\Progetto_GitHub_Network\changelog.txt"
OutputDir=.\InstallerOutput
OutputBaseFilename=NetworkSpeedToggle_1.1_Setup

SetupIconFile="C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\Resources\25g.ico"

; CHANGED: We now point EXACTLY to the physical .ico file inside the installation folder
UninstallDisplayIcon="{app}\Resources\25g.ico"

Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start automatically with Windows (Hidden Task)"; GroupDescription: "Windows Startup:"

[Files]
Source: "C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Excludes: "config.json"; Flags: ignoreversion recursesubdirs
; NEW: Explicitly force the copy of the icon to the Resources folder just to be absolutely certain it is there
Source: "C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\Resources\25g.ico"; DestDir: "{app}\Resources"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\25g.ico"; Tasks: startmenuicon

[Run]
Filename: "schtasks.exe"; Parameters: "/create /tn ""{#MyAppName}"" /tr ""'""{app}\{#MyAppExeName}""'"" /sc onlogon /rl highest /f"; Flags: runhidden; Tasks: startup
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/delete /tn ""{#MyAppName}"" /f"; Flags: runhidden