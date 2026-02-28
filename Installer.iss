[Setup]
AppName=Network Speed Toggle
AppVersion=1.1
AppPublisher=FoggyPunk
DefaultDirName={autopf}\NetworkSpeedToggle
DefaultGroupName=Network Speed Toggle
InfoBeforeFile=C:\Users\marce\OneDrive\Documents\Streaming\Progetto_GitHub_Network\changelog.txt

; --- ICONS ---
SetupIconFile=C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\Resources\25g.ico
WizardSmallImageFile=C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\Resources\25g.bmp
UninstallDisplayIcon={app}\Resources\25g.ico

; --- UI TWEAKS ---
AllowNoIcons=yes
DirExistsWarning=no
CloseApplications=yes

Compression=lzma2
SolidCompression=yes
OutputDir=userdocs:Inno Setup Output
OutputBaseFilename=NetworkSpeedToggle_1.1_Installer
PrivilegesRequired=admin
WizardStyle=modern

[Tasks]
Name: "autostart"; Description: "Start Network Speed Toggle automatically when Windows starts"; GroupDescription: "Auto-start Options:"; Flags: checkedonce

[Files]
Source: "C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\NetworkSpeedToggle.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "C:\Users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Network Speed Toggle"; Filename: "{app}\NetworkSpeedToggle.exe"; IconFilename: "{app}\Resources\25g.ico"
Name: "{commonstartup}\Network Speed Toggle"; Filename: "{app}\NetworkSpeedToggle.exe"; IconFilename: "{app}\Resources\25g.ico"; Tasks: autostart

[Run]
Filename: "{app}\NetworkSpeedToggle.exe"; Description: "Launch Network Speed Toggle now"; Flags: nowait postinstall runascurrentuser

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM NetworkSpeedToggle.exe /T"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: files; Name: "{app}\config.json"
Type: dirifempty; Name: "{app}\Resources"
Type: dirifempty; Name: "{app}\runtimes"
Type: dirifempty; Name: "{app}"