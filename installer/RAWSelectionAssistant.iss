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
#ifdef GlobalSurfaceCloseDevValidation
  #undef MyAppName
  #define MyAppName "Pixel Tart Global Surface Close DevValidation"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\global-surface-close-devvalidation-win-x64"
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
#ifdef InputRoutingHotfixDevValidation
  #undef MyAppName
  #define MyAppName "Pixel Tart Input Routing Hotfix DevValidation"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\input-routing-hotfix-devvalidation-win-x64"
#endif
#ifdef PhysicalPointerDiagnosticDevValidation
  #undef MyAppName
  #define MyAppName "Pixel Tart Physical Pointer Diagnostic DevValidation"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\physical-pointer-diagnostic-devvalidation-win-x64"
#endif
#ifdef PhysicalPointerDiagnosticDevValidation2
  #undef MyAppName
  #define MyAppName "像素蛋挞 - 输入诊断版"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\physical-pointer-diagnostic-devvalidation2-win-x64"
#endif
#ifdef ClickRoutingFixDevValidation
  #undef MyAppName
  #define MyAppName "Pixel Tart Click Routing Fix DevValidation"
  #undef MyAppExeName
  #define MyAppExeName "KitaoPhotoSelector.Acceptance.exe"
  #undef MyPublishDir
  #define MyPublishDir "..\artifacts\releases\2.3.0\publish\click-routing-fix-devvalidation-win-x64"
#endif

[Setup]
#ifdef ClickRoutingFixDevValidation
AppId={{C4B8A06B-2812-4C3E-9FA7-EA97E755F99B}
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
AppId={{A6D55B25-EE0C-4BC4-B983-83D777F2A4B8}
#else
#ifdef PhysicalPointerDiagnosticDevValidation
AppId={{85F41320-9C4A-4AC1-AB91-302477C8E93F}
#else
#ifdef InputRoutingHotfixDevValidation
AppId={{F26197CF-E765-4CB5-8063-A5BE6C9AB5E4}
#else
#ifdef GlobalSurfaceCloseDevValidation
AppId={{6D33EA10-A934-48ED-BE4F-B2E20106D7CE}
#else
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
#endif
#endif
#endif
#endif
#endif
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
#ifdef ClickRoutingFixDevValidation
DefaultDirName={autopf}\PixelTart_ClickRoutingFix_DevValidation
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
DefaultDirName={autopf}\PixelTart_PhysicalPointerDiagnostic_DevValidation2
#else
#ifdef PhysicalPointerDiagnosticDevValidation
DefaultDirName={autopf}\PixelTart_PhysicalPointerDiagnostic_DevValidation
#else
#ifdef InputRoutingHotfixDevValidation
DefaultDirName={autopf}\PixelTart_InputRoutingHotfix_DevValidation
#else
#ifdef GlobalSurfaceCloseDevValidation
DefaultDirName={autopf}\PixelTart_GlobalSurfaceClose_DevValidation
#else
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
#endif
#endif
#endif
#endif
#ifdef PhysicalPointerDiagnosticDevValidation2
DefaultGroupName=像素蛋挞
#else
DefaultGroupName={#MyAppName}
#endif
#endif
DisableProgramGroupPage=yes
#ifdef PhysicalPointerDiagnosticDevValidation2
UninstallDisplayName=像素蛋挞 - Physical Pointer Diagnostic DevValidation
#else
UninstallDisplayName={#MyAppName}
#endif
UninstallDisplayIcon={app}\{#MyAppExeName}
#ifdef ClickRoutingFixDevValidation
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=PixelTart_2.3.0_ClickRoutingFix_DevValidation_x64
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=PixelTart_2.3.0_PhysicalPointerDiagnostic_DevValidation2_x64
#else
#ifdef PhysicalPointerDiagnosticDevValidation
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=PixelTart_2.3.0_PhysicalPointerDiagnostic_DevValidation_x64
#else
#ifdef InputRoutingHotfixDevValidation
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=PixelTart_2.3.0_InputRoutingHotfix_DevValidation_x64
#else
#ifdef GlobalSurfaceCloseDevValidation
OutputDir=..\artifacts\releases\2.3.0\installer
OutputBaseFilename=PixelTart_2.3.0_GlobalSurfaceClose_DevValidation_x64
#else
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
#ifdef ClickRoutingFixDevValidation
CloseApplications=no
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
CloseApplications=no
#else
#ifdef PhysicalPointerDiagnosticDevValidation
CloseApplications=no
#else
CloseApplications=force
#endif
#endif
#endif
RestartApplications=no
ChangesAssociations=no
#ifdef ClickRoutingFixDevValidation
ChangesEnvironment=yes
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
ChangesEnvironment=yes
#else
ChangesEnvironment=no
#endif
#endif
AllowNoIcons=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
#ifdef ClickRoutingFixDevValidation
Name: "desktopicon"; Description: "Create desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: checkedonce
#else
#ifndef PhysicalPointerDiagnosticDevValidation
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: checkedonce
#endif
#endif
#endif

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"

[Icons]
#ifdef ClickRoutingFixDevValidation
Name: "{group}\Pixel Tart Click Routing Fix DevValidation"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Pixel Tart Click Routing Fix DevValidation"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
Name: "{group}\像素蛋挞 - 输入诊断版"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\像素蛋挞 - 输入诊断版"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
#else
#ifndef PhysicalPointerDiagnosticDevValidation
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
#endif
#endif
#endif

[Run]
#ifdef ClickRoutingFixDevValidation
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Pixel Tart Click Routing Fix DevValidation"; Flags: nowait postinstall skipifsilent
#else
#ifdef PhysicalPointerDiagnosticDevValidation2
Filename: "{app}\{#MyAppExeName}"; Description: "启动 像素蛋挞 - 输入诊断版"; Flags: nowait postinstall skipifsilent
#else
#ifndef InputRoutingHotfixDevValidation
#ifndef PhysicalPointerDiagnosticDevValidation
Filename: "{app}\{#MyAppExeName}"; Description: "运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent
#endif
#endif
#endif
#endif

[Code]
var
  DeleteUserDataCheckBox: TNewCheckBox;

#if defined PhysicalPointerDiagnosticDevValidation2 || defined ClickRoutingFixDevValidation
const
  PhysicalPointerDiagnosticEnvironmentKey = 'Environment';
#ifdef ClickRoutingFixDevValidation
  PhysicalPointerDiagnosticMarkerKey = 'Software\PixelTart\ClickRoutingFixDevValidation';
#else
  PhysicalPointerDiagnosticMarkerKey = 'Software\PixelTart\PhysicalPointerDiagnosticDevValidation2';
#endif
  PhysicalPointerDiagnosticRootValue = 'PIXEL_TART_ACCEPTANCE_ROOT';
  PhysicalPointerHwndBroadcast = $FFFF;
  PhysicalPointerWmSettingChange = $001A;
  PhysicalPointerSmtoAbortIfHung = $0002;

function SetEnvironmentVariable(lpName, lpValue: string): Boolean;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';
function SendMessageTimeout(hWnd: HWND; Msg, wParam: LongWord; lParam: string; fuFlags, uTimeout: LongWord; var lpdwResult: LongWord): LongWord;
  external 'SendMessageTimeoutW@user32.dll stdcall';

function PhysicalPointerDiagnosticAcceptanceRoot(): string;
begin
#ifdef ClickRoutingFixDevValidation
  Result := ExpandConstant('{localappdata}\PixelTart_Validation\ClickRoutingFixDevValidation');
#else
  Result := ExpandConstant('{localappdata}\PixelTart_Validation\PhysicalPointerDiagnosticDevValidation2');
#endif
end;

procedure NotifyEnvironmentChanged();
var
  MessageResult: LongWord;
begin
  SendMessageTimeout(PhysicalPointerHwndBroadcast, PhysicalPointerWmSettingChange, 0, 'Environment', PhysicalPointerSmtoAbortIfHung, 5000, MessageResult);
end;

procedure ConfigurePhysicalPointerDiagnosticEnvironment();
var
  PreviousRoot: string;
  Root: string;
begin
  Root := PhysicalPointerDiagnosticAcceptanceRoot();
  if not RegValueExists(HKCU, PhysicalPointerDiagnosticMarkerKey, 'PreviousAcceptanceRootExisted') then
  begin
    if RegQueryStringValue(HKCU, PhysicalPointerDiagnosticEnvironmentKey, PhysicalPointerDiagnosticRootValue, PreviousRoot) then
    begin
      RegWriteStringValue(HKCU, PhysicalPointerDiagnosticMarkerKey, 'PreviousAcceptanceRootExisted', '1');
      RegWriteStringValue(HKCU, PhysicalPointerDiagnosticMarkerKey, 'PreviousAcceptanceRoot', PreviousRoot);
    end
    else
      RegWriteStringValue(HKCU, PhysicalPointerDiagnosticMarkerKey, 'PreviousAcceptanceRootExisted', '0');
  end;

  RegWriteStringValue(HKCU, PhysicalPointerDiagnosticEnvironmentKey, PhysicalPointerDiagnosticRootValue, Root);
  SetEnvironmentVariable(PhysicalPointerDiagnosticRootValue, Root);
  NotifyEnvironmentChanged();
end;

procedure RestorePhysicalPointerDiagnosticEnvironment();
var
  CurrentRoot: string;
  PreviousRoot: string;
  PreviousRootExisted: string;
  Root: string;
begin
  Root := PhysicalPointerDiagnosticAcceptanceRoot();
  if RegQueryStringValue(HKCU, PhysicalPointerDiagnosticEnvironmentKey, PhysicalPointerDiagnosticRootValue, CurrentRoot) and
     (CompareText(CurrentRoot, Root) = 0) then
  begin
    if RegQueryStringValue(HKCU, PhysicalPointerDiagnosticMarkerKey, 'PreviousAcceptanceRootExisted', PreviousRootExisted) and
       (PreviousRootExisted = '1') and
       RegQueryStringValue(HKCU, PhysicalPointerDiagnosticMarkerKey, 'PreviousAcceptanceRoot', PreviousRoot) then
    begin
      RegWriteStringValue(HKCU, PhysicalPointerDiagnosticEnvironmentKey, PhysicalPointerDiagnosticRootValue, PreviousRoot);
      SetEnvironmentVariable(PhysicalPointerDiagnosticRootValue, PreviousRoot);
    end
    else
    begin
      RegDeleteValue(HKCU, PhysicalPointerDiagnosticEnvironmentKey, PhysicalPointerDiagnosticRootValue);
      SetEnvironmentVariable(PhysicalPointerDiagnosticRootValue, '');
    end;
    NotifyEnvironmentChanged();
  end;
  RegDeleteKeyIncludingSubkeys(HKCU, PhysicalPointerDiagnosticMarkerKey);
end;
#endif

function FindWindow(lpClassName, lpWindowName: string): HWND;
  external 'FindWindowW@user32.dll stdcall';
function PostMessage(hWnd: HWND; Msg: LongWord; wParam, lParam: Longint): Boolean;
  external 'PostMessageW@user32.dll stdcall';

function InitializeUninstall(): Boolean;
#if !defined PhysicalPointerDiagnosticDevValidation2 && !defined ClickRoutingFixDevValidation
#ifndef InputRoutingHotfixDevValidation
#ifndef PhysicalPointerDiagnosticDevValidation
var
  AppWindow: HWND;
  WaitCount: Integer;
#endif
#endif
#endif
begin
#if !defined PhysicalPointerDiagnosticDevValidation2 && !defined ClickRoutingFixDevValidation
#ifndef InputRoutingHotfixDevValidation
#ifndef PhysicalPointerDiagnosticDevValidation
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
#endif
#endif
#endif
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
#if defined PhysicalPointerDiagnosticDevValidation2 || defined ClickRoutingFixDevValidation
  DeleteUserDataCheckBox := nil;
#else
#ifdef InputRoutingHotfixDevValidation
  DeleteUserDataCheckBox := nil;
#else
#ifdef PhysicalPointerDiagnosticDevValidation
  DeleteUserDataCheckBox := nil;
#else
  DeleteUserDataCheckBox := TNewCheckBox.Create(UninstallProgressForm);
  DeleteUserDataCheckBox.Parent := UninstallProgressForm;
  DeleteUserDataCheckBox.Left := UninstallProgressForm.StatusLabel.Left;
  DeleteUserDataCheckBox.Top := UninstallProgressForm.StatusLabel.Top + UninstallProgressForm.StatusLabel.Height + ScaleY(28);
  DeleteUserDataCheckBox.Width := UninstallProgressForm.ClientWidth - (DeleteUserDataCheckBox.Left * 2);
  DeleteUserDataCheckBox.Caption := '同时删除用户设置、项目数据库、索引和历史日志';
  DeleteUserDataCheckBox.Checked := False;
#endif
#endif
#endif
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
#if defined PhysicalPointerDiagnosticDevValidation2 || defined ClickRoutingFixDevValidation
  if CurUninstallStep = usPostUninstall then
    RestorePhysicalPointerDiagnosticEnvironment();
#else
#ifndef PhysicalPointerDiagnosticDevValidation
#ifndef InputRoutingHotfixDevValidation
  if (CurUninstallStep = usPostUninstall) and DeleteUserDataCheckBox.Checked then
    DelTree(ExpandConstant('{localappdata}\KitaoPhotoSelector'), True, True, True);
#endif
#endif
#endif
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
#if defined PhysicalPointerDiagnosticDevValidation2 || defined ClickRoutingFixDevValidation
  if CurStep = ssPostInstall then
    ConfigurePhysicalPointerDiagnosticEnvironment();
#endif
end;
