; WangZip (왕집) 설치 프로그램
; Inno Setup 7로 컴파일합니다: .\installer\build-installer.ps1
;
; - 자체 포함 배포본(artifacts\WangZip-<버전>-win-x64)을 통째로 담아 .NET 설치가 필요 없게 합니다.
; - 사용자 단위 설치(권한 상승 없음, {autopf} → LocalAppData\Programs)를 씁니다.

#define MyAppVersion "0.3.0"
#define MyAppName "왕집 (WangZip)"
#define MyAppShortName "왕집"
#define MyAppExe "WangZip.exe"
#define MyAppURL "https://github.com/karanglass24-lang/WangZip"
#define SourceDir "..\artifacts\WangZip-" + MyAppVersion + "-win-x64"

[Setup]
AppId={{B4F3D9E2-6C1A-4E8B-9D5F-2A7C0E1B8F30}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher=WangZip Project
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\WangZip
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
LicenseFile=..\LICENSE
SetupIconFile=..\src\WangZip.App\Assets\app-icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=WangZip-{#MyAppVersion}-setup
CloseApplications=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "{#MyAppShortName} 바탕 화면 바로 가기 만들기(&D)"; GroupDescription: "추가 작업:"; Flags: unchecked
Name: "shellmenu"; Description: "탐색기 마우스 오른쪽 메뉴에 '여기에 풀기 / 이 폴더 압축' 추가(&M) (Win11은 '여러 항목 표시' 아래)"; GroupDescription: "추가 작업:"

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; ZIP 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\zipfile\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "왕집: 여기에 풀기"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\zipfile\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\zipfile\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; 7z 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "왕집: 여기에 풀기"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; 폴더: 압축하기
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZipCreate"; ValueType: string; ValueName: ""; ValueData: "왕집: 이 폴더 압축"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZipCreate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZipCreate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu
; 폴더 배경: 바탕 화면/폴더 안에서 이 위치 압축
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZipCreateBackground"; ValueType: string; ValueName: ""; ValueData: "왕집: 이 폴더 압축"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZipCreateBackground"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZipCreateBackground\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%V"""; Tasks: shellmenu

[Icons]
Name: "{group}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExe}"; Comment: "광고 없는 압축 프로그램"
Name: "{group}\{#MyAppShortName} 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon; Comment: "광고 없는 압축 프로그램"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "지금 왕집 실행(&R)"; Flags: nowait postinstall skipifsilent
