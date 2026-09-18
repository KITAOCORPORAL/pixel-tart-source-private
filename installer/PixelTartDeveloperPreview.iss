#define AppName "像素蛋挞 Pixel Tart 开发预览版"
#ifndef AppVersion
#define AppVersion "2.3.0-dev"
#endif
#ifndef OutputBaseFilename
#define OutputBaseFilename "PixelTart-DeveloperPreview-2.3.0-dev-x64-Setup"
#endif
#define AppPublisher "像素蛋挞"
#define AppExe "PixelTart.exe"
#ifndef PublishDir
#define PublishDir "..\artifacts\stage-v2-installable-acceptance\publish"
#endif

[Setup]
AppId={{3A5A5B1B-8A11-4E85-9C54-1B18D0E5D2F4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\PixelTart Developer Preview
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\artifacts\stage-v2-installable-acceptance\installer
OutputBaseFilename={#OutputBaseFilename}
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
DisableProgramGroupPage=yes
Uninstallable=yes
AllowNoIcons=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml,*.Tests.dll,*TestHost*,*Acceptance.dll,*.trx,*.cs,*.xaml"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "启动像素蛋挞 Pixel Tart 开发预览版"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\DemoWorkspace"
