; ProdigyDownloader Inno Setup script
; Compile with Inno Setup after publishing to publish\win-x64

#define MyAppName "ProdigyDownloader"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "ProdigyNova"
#define MyAppURL "https://www.prodigynova.com"
#define MyAppExeName "ProdigyDownloader.exe"

[Setup]
AppId={{A7C3E9F2-4B81-4D2E-9F10-6E8C2B1A0D55}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL=https://github.com/mitribox/ProdigyDownloader
AppUpdatesURL=https://github.com/mitribox/ProdigyDownloader/releases
DefaultDirName={autopf}\ProdigyNova\{#MyAppName}
DefaultGroupName=ProdigyNova
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=ProdigyDownloader-Setup-{#MyAppVersion}
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoCopyright=Copyright (C) 2026 ProdigyNova - https://www.prodigynova.com

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Website - ProdigyNova"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
