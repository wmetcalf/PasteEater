;PasteEater Installer Script
#define MyAppName "PasteEater"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company"
#define MyAppExeName "PasteEater.exe"

[Setup]
AppId={{6D887246-2197-4BD4-9E54-3C13DE66A927}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=PasteEaterSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; 
Name: "startup"; Description: "Start when Windows starts"; GroupDescription: "Windows startup";

[Files]
Source: "bin\Release\net6.0-windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "rules.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Add run at startup option if selected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PasteEater"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup
; Set the rules file path in registry
Root: HKCU; Subkey: "Software\PasteEater"; ValueType: string; ValueName: "RulesPath"; ValueData: "{app}\rules.json"; Flags: uninsdeletevalue
; Set default preferences
Root: HKCU; Subkey: "Software\PasteEater"; ValueType: dword; ValueName: "DebugMode"; ValueData: "0"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\PasteEater"; ValueType: dword; ValueName: "WarnOnly"; ValueData: "0"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\PasteEater"; ValueType: dword; ValueName: "StartWithWindows"; ValueData: "0"; Flags: uninsdeletevalue; Tasks: startup; Check: not IsTaskSelected('startup')
Root: HKCU; Subkey: "Software\PasteEater"; ValueType: dword; ValueName: "StartWithWindows"; ValueData: "1"; Flags: uninsdeletevalue; Tasks: startup