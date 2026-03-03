; =====================================================
; StreamTweak v2.0 - GitHub Release Installer
; =====================================================
#define MyAppName "StreamTweak"
#define MyAppVersion "2.0"
#define MyAppPublisher "FoggyPunk"
#define MyAppExeName "StreamTweak.exe"
#define MyAppURL "https://github.com/FoggyPunk/StreamTweak"

#include "CodeDependencies.iss"

[Setup]
AppId={{D37D0ED6-5E8D-4131-B2C1-30A5840AC97B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
InfoBeforeFile=changelog.txt
SetupIconFile=Resources\streamtweak.ico
WizardSmallImageFile=Resources\streamtweak.bmp
WizardImageFile=Resources\streamtweakinstaller.bmp
UninstallDisplayIcon={app}\Resources\streamtweak.ico
AllowNoIcons=yes
DirExistsWarning=no
CloseApplications=yes
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=StreamTweak_{#MyAppVersion}_Installer
PrivilegesRequired=admin
WizardStyle=modern
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to the StreamTweak Setup Wizard
WelcomeLabel2=

[Tasks]
Name: "autostart"; Description: "Start {#MyAppName} automatically when Windows starts"; GroupDescription: "Auto-start Options:"; Flags: checkedonce

[Files]
Source: "bin\Release\net8.0-windows7.0\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Resources\streamtweak.bmp"; Flags: dontcopy
Source: "changelog.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commonstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: postinstall skipifsilent nowait

[Code]
var
  LogoImage: TBitmapImage;
  DevelopedByLabel: TNewStaticText;
  GitHubLinkLabel: TNewStaticText;

procedure GitHubLinkClick(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', '{#MyAppURL}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard;
var
  TmpFileName: String;
begin
  ExtractTemporaryFile('streamtweak.bmp');
  TmpFileName := ExpandConstant('{tmp}\streamtweak.bmp');

  LogoImage := TBitmapImage.Create(WizardForm);
  LogoImage.Parent := WizardForm.WelcomePage;
  LogoImage.Bitmap.LoadFromFile(TmpFileName);
  LogoImage.Left := WizardForm.WelcomeLabel1.Left;
  LogoImage.Top := WizardForm.WelcomeLabel1.Top + WizardForm.WelcomeLabel1.Height + ScaleY(25);
  LogoImage.AutoSize := True;

  DevelopedByLabel := TNewStaticText.Create(WizardForm);
  DevelopedByLabel.Parent := WizardForm.WelcomePage;
  DevelopedByLabel.Left := LogoImage.Left;
  DevelopedByLabel.Top := LogoImage.Top + LogoImage.Height + ScaleY(30);
  DevelopedByLabel.Caption := 'Developed by FoggyPunk © 2026';
  DevelopedByLabel.Font.Size := 10;
  DevelopedByLabel.AutoSize := True;

  GitHubLinkLabel := TNewStaticText.Create(WizardForm);
  GitHubLinkLabel.Parent := WizardForm.WelcomePage;
  GitHubLinkLabel.Left := DevelopedByLabel.Left;
  GitHubLinkLabel.Top := DevelopedByLabel.Top + DevelopedByLabel.Height + ScaleY(15);
  GitHubLinkLabel.Caption := '{#MyAppURL}';
  GitHubLinkLabel.Cursor := crHand;
  GitHubLinkLabel.Font.Color := clHighlight;
  GitHubLinkLabel.Font.Style := [fsUnderline];
  GitHubLinkLabel.OnClick := @GitHubLinkClick;
end;

function InitializeSetup: Boolean;
begin
  Dependency_AddDotNet80Desktop;
  Result := True;
end;