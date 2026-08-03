#define MyAppName "像素蛋挞"
#define MyAppVersion "2.0.4"
#define MyAppPublisher "像素蛋挞"
#define MyAppExeName "KitaoPhotoSelector.exe"
#define MyPublishDir "..\artifacts\releases\2.0.4\publish\win-x64"
#ifdef TestBuild
  #undef MyAppName
  #define MyAppName "像素蛋挞 验收测试"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\publish\acceptance-win-x64"
#endif

[Setup]
#ifdef TestBuild
AppId={{8D3538F8-CB93-4812-8722-A21D9B3204B2}
#else
AppId={{72CA568E-8C7C-4DB6-A8E4-AEC68008D19B}
#endif
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
#ifdef TestBuild
DefaultDirName={autopf}\像素蛋挞_验收测试
#else
DefaultDirName={autopf}\像素蛋挞
#endif
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\artifacts\releases\2.0.4\installer
#ifdef TestBuild
OutputBaseFilename=像素蛋挞_Test_Setup_2.0.4_x64
#else
OutputBaseFilename=像素蛋挞_Setup_2.0.4_x64
#endif
SetupIconFile=..\src\RAWSelectionAssistant\Assets\AppIcon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
CloseApplications=force
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no
AllowNoIcons=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: checkedonce

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DeleteUserDataCheckBox: TNewCheckBox;

function FindWindow(lpClassName, lpWindowName: string): HWND;
  external 'FindWindowW@user32.dll stdcall';
function PostMessage(hWnd: HWND; Msg: LongWord; wParam, lParam: Longint): Boolean;
  external 'PostMessageW@user32.dll stdcall';

function InitializeUninstall(): Boolean;
var
  AppWindow: HWND;
  WaitCount: Integer;
begin
  AppWindow := FindWindow('', '像素蛋挞');
  if AppWindow <> 0 then
  begin
    PostMessage(AppWindow, $0010, 0, 0);
    for WaitCount := 1 to 50 do
    begin
      Sleep(100);
      if FindWindow('', '像素蛋挞') = 0 then
        Break;
    end;
  end;
  Result := True;
end;

function InitializeSetup(): Boolean;
begin
  Result := IsWin64;
  if not Result then
    MsgBox('像素蛋挞仅支持 Windows 10 或 Windows 11 的 64 位版本。', mbError, MB_OK);
end;

procedure InitializeUninstallProgressForm();
begin
  DeleteUserDataCheckBox := TNewCheckBox.Create(UninstallProgressForm);
  DeleteUserDataCheckBox.Parent := UninstallProgressForm;
  DeleteUserDataCheckBox.Left := UninstallProgressForm.StatusLabel.Left;
  DeleteUserDataCheckBox.Top := UninstallProgressForm.StatusLabel.Top + UninstallProgressForm.StatusLabel.Height + ScaleY(28);
  DeleteUserDataCheckBox.Width := UninstallProgressForm.ClientWidth - (DeleteUserDataCheckBox.Left * 2);
  DeleteUserDataCheckBox.Caption := '同时删除用户设置、索引和历史日志';
  DeleteUserDataCheckBox.Checked := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteUserDataCheckBox.Checked then
    DelTree(ExpandConstant('{localappdata}\KitaoPhotoSelector'), True, True, True);
end;
