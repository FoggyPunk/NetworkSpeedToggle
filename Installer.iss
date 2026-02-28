[Setup]
AppName=Network Speed Toggle
AppVersion=1.0
AppPublisher=FoggyPunk
DefaultDirName={autopf}\NetworkSpeedToggle
DefaultGroupName=Network Speed Toggle

; --- ICONS ---
SetupIconFile=C:\users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\Resources\25g.ico
WizardSmallImageFile=C:\users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\Resources\25g.bmp
UninstallDisplayIcon={app}\Resources\25g.ico

; --- UI TWEAKS ---
AllowNoIcons=yes
; Disabilita il fastidioso avviso "La cartella esiste gia'" quando reinstalli/aggiorni
DirExistsWarning=no
CloseApplications=yes

Compression=lzma2
SolidCompression=yes
OutputDir=userdocs:Inno Setup Output
OutputBaseFilename=NetworkSpeedToggle_Installer
PrivilegesRequired=admin
WizardStyle=modern

[Tasks]
Name: "autostart"; Description: "Start Network Speed Toggle automatically when Windows starts"; GroupDescription: "Auto-start Options:"; Flags: checkedonce

[Files]
Source: "C:\users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\NetworkSpeedToggle.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "C:\users\marce\source\repos\NetworkSpeedToggle\NetworkSpeedToggle\bin\Release\net10.0-windows\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Network Speed Toggle"; Filename: "{app}\NetworkSpeedToggle.exe"; IconFilename: "{app}\Resources\25g.ico"
Name: "{commonstartup}\Network Speed Toggle"; Filename: "{app}\NetworkSpeedToggle.exe"; IconFilename: "{app}\Resources\25g.ico"; Tasks: autostart

[Run]
Filename: "{app}\NetworkSpeedToggle.exe"; Description: "Launch Network Speed Toggle now"; Flags: nowait postinstall runascurrentuser

[UninstallRun]
; Uccide il processo per sbloccare i file e rimuovere la tray icon
Filename: "taskkill.exe"; Parameters: "/F /IM NetworkSpeedToggle.exe /T"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; ELIMINAZIONE PROFONDA: Rimuove file generati dinamicamente (come config.json) e forza la cancellazione dell'intera cartella
Type: files; Name: "{app}\config.json"
Type: dirifempty; Name: "{app}\Resources"
Type: dirifempty; Name: "{app}\runtimes"
Type: dirifempty; Name: "{app}"

[Code]
var
  AdapterPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  AdapterPage := CreateInputQueryPage(wpSelectDir,
    'Network Adapter Configuration',
    'Specify the network adapter you want to control',
    'To find your exact adapter name in Windows:' + #13#10 +
    '1. Press the Windows Key + R' + #13#10 +
    '2. Type "ncpa.cpl" and press Enter' + #13#10 +
    '3. A window will open showing your network connections.' + #13#10 +
    '4. Look for your active cable connection (usually named "Ethernet" or "Ethernet 2").' + #13#10 + #13#10 +
    'Please type that exact name below (it is case-sensitive):');

  AdapterPage.Add('Adapter Name:', False);
  AdapterPage.Values[0] := 'Ethernet';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  JsonContent: String;
begin
  if CurStep = ssPostInstall then
  begin
    JsonContent := '{' + #13#10 + '  "NetworkAdapterName": "' + AdapterPage.Values[0] + '"' + #13#10 + '}';
    SaveStringToFile(ExpandConstant('{app}\config.json'), JsonContent, False);
  end;
end;