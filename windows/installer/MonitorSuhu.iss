#define MyAppName "MonitorSuhu"
#define MyAppVersion "1.0.13"
#define MyAppPublisher "MonitorSuhu"
#define MyAppURL "https://github.com/BadryansahBangsawan/MonitorSuhu"
#define MyAppExeName "MonitorSuhu.exe"

[Setup]
AppId={{A7E3C1B4-9F62-4D18-B8C5-0E2A91F47D33}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=MonitorSuhu-{#MyAppVersion}-windows-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\MonitorSuhu.App\Assets\MonitorSuhu.ico
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes
MinVersion=10.0
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""MonitorSuhu"" /F"; Flags: runhidden; RunOnceId: "RemoveAutostartTask"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\MonitorSuhu"
