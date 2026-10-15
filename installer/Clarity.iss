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
#ifndef ArtifactTag
  #define ArtifactTag ReleaseTag
#endif
#ifndef InformationalVersion
  #define InformationalVersion ReleaseTag
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
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName=Clarity
AllowNoIcons=yes
DisableProgramGroupPage=no
DisableDirPage=no
PrivilegesRequired=admin
MinVersion=10.0
OutputDir={#ReleaseOutput}
OutputBaseFilename=Clarity-{#ArtifactTag}-setup
SetupIconFile=..\assets\Clarity.ico
UninstallDisplayIcon={app}\Clarity.exe
VersionInfoVersion={#NumericVersion}
VersionInfoProductName=Clarity
VersionInfoProductVersion={#NumericVersion}
VersionInfoProductTextVersion={#InformationalVersion}
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

[Dirs]
Name: "{app}\Logs"; Permissions: users-modify

[Files]
Source: "{#PayloadDir}\Clarity.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Clarity.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\使用说明.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Clarity"; Filename: "{app}\Clarity.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Clarity"; Filename: "{app}\Clarity.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Clarity.exe"; Description: "{cm:LaunchProgram,Clarity}"; Flags: nowait postinstall skipifsilent unchecked
Filename: "{app}\Clarity.exe"; Flags: nowait runasoriginaluser; Check: RestartAfterUpdate

[CustomMessages]
english.NetRequired=Clarity requires .NET Framework 4.8 or later. Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48, then run this installer again.
english.SelectInstallFolder=Choose an installation location. Clarity will be installed in a Clarity subfolder.
#ifdef IncludeChinese
chinesesimplified.NetRequired=Clarity 需要 .NET Framework 4.8 或更高版本。请从 https://dotnet.microsoft.com/download/dotnet-framework/net48 安装后重试。
chinesesimplified.SelectInstallFolder=请选择安装位置。程序将安装到该位置下的 Clarity 文件夹。
#endif

[Code]
function RestartAfterUpdate: Boolean;
begin
  Result := ExpandConstant('{param:CLARITYUPDATE|0}') = '1';
end;

function DefaultInstallDir(Param: String): String;
begin
  // Use the native Program Files folder, including on 64-bit Windows.
  if IsWin64 then
    Result := ExpandConstant('{commonpf64}\Clarity')
  else
    Result := ExpandConstant('{commonpf32}\Clarity');
end;

procedure BrowseInstallFolder(Sender: TObject);
var
  SelectedDir, ParentDir: String;
begin
  SelectedDir := Trim(WizardForm.DirEdit.Text);
  // The application subfolder may not exist yet. Open the nearest existing parent.
  while (SelectedDir <> '') and not DirExists(SelectedDir) do begin
    ParentDir := ExtractFileDir(RemoveBackslashUnlessRoot(SelectedDir));
    if CompareText(ParentDir, SelectedDir) = 0 then
      Break;
    SelectedDir := ParentDir;
  end;
  if SelectedDir = '' then
    SelectedDir := ExpandConstant('{userdesktop}');

  // Windows provides Desktop, Downloads, This PC and other shell locations.
  // Cancel leaves the installation path untouched.
  if BrowseForFolder(CustomMessage('SelectInstallFolder'), SelectedDir, False) then begin
    SelectedDir := RemoveBackslashUnlessRoot(SelectedDir);
    if CompareText(ExtractFileName(SelectedDir), 'Clarity') <> 0 then
      SelectedDir := AddBackslash(SelectedDir) + 'Clarity';
    WizardForm.DirEdit.Text := SelectedDir;
  end;
end;

procedure InitializeWizard;
begin
  WizardForm.DirBrowseButton.OnClick := @BrowseInstallFolder;
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox(CustomMessage('NetRequired'), mbCriticalError, MB_OK, IDOK);
end;

// No custom uninstall deletion: only installed files and shortcuts are removed.
// Application settings and ClarityBackups are kept for the user.
