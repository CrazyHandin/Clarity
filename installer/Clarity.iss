#ifndef PayloadDir
  #error PayloadDir must point to the verified portable payload.
#endif
#ifndef ReleaseTag
  #error ReleaseTag is required.
#endif
#ifndef NumericVersion
  #error NumericVersion is required.
#endif
#ifndef ReleaseOutput
  #error ReleaseOutput is required.
#endif

[Setup]
AppId={{A58446CF-E0B8-4D66-97CA-71AE049410A8}
AppName=Clarity
AppVersion={#ReleaseTag}
AppVerName=Clarity {#ReleaseTag}
AppPublisher=CraZyX
AppPublisherURL=https://github.com/CrazyHandin/Clarity
AppSupportURL=https://github.com/CrazyHandin/Clarity/issues
AppUpdatesURL=https://github.com/CrazyHandin/Clarity/releases/latest
DefaultDirName={localappdata}\Programs\Clarity
DefaultGroupName=Clarity
AllowNoIcons=yes
DisableProgramGroupPage=no
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir={#ReleaseOutput}
OutputBaseFilename=Clarity-{#ReleaseTag}-setup
SetupIconFile=..\assets\Clarity.ico
UninstallDisplayIcon={app}\Clarity.exe
VersionInfoVersion={#NumericVersion}
VersionInfoProductName=Clarity
VersionInfoProductVersion={#NumericVersion}
VersionInfoProductTextVersion={#ReleaseTag}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dynamic windows11
WizardSizePercent=110
CloseApplications=yes
RestartApplications=no
DisableStartupPrompt=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
#ifdef IncludeChinese
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\Clarity.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Clarity.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\使用说明.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Clarity"; Filename: "{app}\Clarity.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Clarity"; Filename: "{app}\Clarity.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Clarity.exe"; Description: "{cm:LaunchProgram,Clarity}"; Flags: nowait postinstall skipifsilent unchecked

[CustomMessages]
english.NetRequired=Clarity requires .NET Framework 4.8 or later. Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48, then run this installer again.
#ifdef IncludeChinese
chinesesimplified.NetRequired=Clarity 需要 .NET Framework 4.8 或更高版本。请从 https://dotnet.microsoft.com/download/dotnet-framework/net48 安装后重试。
#endif

[Code]
function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox(CustomMessage('NetRequired'), mbCriticalError, MB_OK, IDOK);
end;

// No custom uninstall deletion: only installed files and shortcuts are removed.
// Application settings and ClarityBackups are kept for the user.
