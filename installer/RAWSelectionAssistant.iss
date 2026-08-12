#define MyAppName "像素蛋挞"
#define MyAppVersion "2.3.0"
#define MyAppPublisher "像素蛋挞"
#define MyAppExeName "KitaoPhotoSelector.exe"
#define MyPublishDir "..\artifacts\releases\2.3.0\publish\win-x64"
#ifdef CandidateBuild
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\win-x64"
#endif
#ifdef CandidateCoreHotfix2
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\corehotfix2-win-x64"
#endif
#ifdef ProductRedesignCandidate
  #undef MyAppName
  #define MyAppName "像素蛋挞 产品重构候选"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\product-redesign-rc1-win-x64"
#endif
#ifdef CoreReliabilityInteractionHotfix
  #undef MyAppName
  #define MyAppName "Pixel Tart CoreReliability Interaction Hotfix DevValidation"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\core-reliability-interaction-hotfix-devvalidation-win-x64"
#endif
#ifdef CoreReliabilityDevValidation
  #undef MyAppName
  #define MyAppName "像素蛋挞 核心可靠性验收"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\core-reliability-devvalidation-win-x64-2"
#endif
#ifdef TestBuild
  #undef MyAppName
  #define MyAppName "像素蛋挞 验收测试"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\win-x64"
#endif

[Setup]
#ifdef CoreReliabilityInteractionHotfix
AppId={{A2D0D68C-0B3F-4B8F-9C0A-7C0D27E4C2F1}
#else
#ifdef CoreReliabilityDevValidation
AppId={{4FBAC285-72B5-4560-A249-EBF70EFD7B3F}
#else
#ifdef ProductRedesignCandidate
AppId={{9E737D34-58DB-4B4D-91DF-C7B8A96D5F20}
#else
#ifdef TestBuild
AppId={{8D3538F8-CB93-4812-8722-A21D9B3204B2}
#else
#ifdef IsolatedRuntimeTest
AppId={{F2A566DF-83FD-4D9E-B6B6-3A11F0B77B5A}
#else
AppId={{72CA568E-8C7C-4DB6-A8E4-AEC68008D19B}
#endif
#endif
#endif
#endif
#endif
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
#ifdef CoreReliabilityInteractionHotfix
DefaultDirName={autopf}\PixelTart_CoreReliability_InteractionHotfix_DevValidation
#else
#ifdef CoreReliabilityDevValidation
DefaultDirName={autopf}\像素蛋挞_CoreReliability_DevValidation
#else
#ifdef ProductRedesignCandidate
DefaultDirName={autopf}\像素蛋挞_ProductRedesign_RC1
#else
#ifdef TestBuild
DefaultDirName={autopf}\像素蛋挞_验收测试
#else
#ifdef IsolatedRuntimeTest
DefaultDirName={autopf}\像素蛋挞_RC5_隔离验收
#else
DefaultDirName={autopf}\像素蛋挞
#endif
#endif
#endif
#endif
#endif
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
#ifdef CoreReliabilityInteractionHotfix
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=PixelTart_2.3.0_CoreReliability_InteractionHotfix_DevValidation_x64
#else
#ifdef CoreReliabilityDevValidation
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=像素蛋挞_2.3.0_CoreReliability_DevValidation_x64
#else
#ifdef ProductRedesignCandidate
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=像素蛋挞_Setup_2.3.0_ProductRedesign_RC1_x64
#else
#ifdef IsolatedRuntimeTest
OutputDir=..\artifacts\ui-review\2.3.0-rc5\runtime-installer
OutputBaseFilename=像素蛋挞_Setup_2.3.0_RC5_IsolatedAcceptance_x64
#else
#ifdef TestBuild
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=像素蛋挞_Test_Setup_2.3.0_x64
#else
#ifdef CandidateBuild
OutputDir=..\artifacts\releases\2.3.0\installer
#ifdef CandidateRc5
#ifdef CandidateCoreHotfix2
OutputBaseFilename=像素蛋挞_Setup_2.3.0_RC5_CoreHotfix2_x64
#else
OutputBaseFilename=像素蛋挞_Setup_2.3.0_RC5_x64
#endif
#else
#ifdef CandidateRc4
OutputBaseFilename=像素蛋挞_Setup_2.3.0_RC4_x64
#else
#ifdef CandidateRc3
OutputBaseFilename=像素蛋挞_Setup_2.3.0_RC3_x64
#else
#ifdef CandidateRc2
OutputBaseFilename=像素蛋挞_Setup_2.3.0_RC2_x64
#else
OutputBaseFilename=像素蛋挞_Setup_2.3.0_RC1_x64
#endif
#endif
#endif
#endif
#else
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=像素蛋挞_Setup_2.3.0_x64
#endif
#endif
#endif
#endif
#endif
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
  DeleteUserDataCheckBox.Caption := '同时删除用户设置、项目数据库、索引和历史日志';
  DeleteUserDataCheckBox.Checked := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteUserDataCheckBox.Checked then
    DelTree(ExpandConstant('{localappdata}\KitaoPhotoSelector'), True, True, True);
end;
