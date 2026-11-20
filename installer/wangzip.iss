; WangZip (왕집) 설치 프로그램
; Inno Setup 7로 컴파일합니다: .\installer\build-installer.ps1
;
; - 자체 포함 배포본(artifacts\WangZip-<버전>-win-x64)을 통째로 담아 .NET 설치가 필요 없게 합니다.
; - 사용자 단위 설치(권한 상승 없음, {autopf} → LocalAppData\Programs)를 씁니다.

#ifndef MyAppVersion
  ; 릴리스 자동화는 ISCC /DMyAppVersion=x.y.z 로 덮어씁니다.
  #define MyAppVersion "0.8.1"
#endif
#define MyAppName "왕집 (WangZip)"
#define MyAppShortName "왕집"
#define MyAppExe "WangZip.exe"
#define MyAppURL "https://github.com/karanglass24-lang/WangZip"
#define SourceDir "..\artifacts\WangZip-" + MyAppVersion + "-win-x64"

[Setup]
ChangesAssociations=yes
; 테스트 빌드(/DTestBuild)는 AppId·설치 폴더·그룹·출력 이름이 달라 실제 설치를 건드리지 않습니다.
#ifdef TestBuild
AppId={{B4F3D9E2-6C1A-4E8B-9D5F-2A7C0E1B8F31}
DefaultDirName={autopf}\WangZip-Test
DefaultGroupName={#MyAppName} 테스트
OutputBaseFilename=WangZip-{#MyAppVersion}-TEST-setup
#else
AppId={{B4F3D9E2-6C1A-4E8B-9D5F-2A7C0E1B8F30}
DefaultDirName={autopf}\WangZip
DefaultGroupName={#MyAppName}
OutputBaseFilename=WangZip-{#MyAppVersion}-setup
#endif
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher=WangZip Project
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
LicenseFile=..\LICENSE
SetupIconFile=..\src\WangZip.App\Assets\app-icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=..\artifacts
CloseApplications=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; GroupDescription: "{cm:TaskGroup}"; Flags: unchecked
Name: "fileassoc"; Description: "{cm:TaskAssoc}"; GroupDescription: "{cm:TaskGroup}"
Name: "shellmenu"; Description: "{cm:TaskMenu}"; GroupDescription: "{cm:TaskGroup}"

[CustomMessages]
korean.AppShort=왕집
korean.AppDesc=광고 없는 오픈소스 압축 프로그램
korean.TaskGroup=추가 작업:
korean.TaskDesktop=왕집 바탕 화면 바로 가기 만들기(&D)
korean.TaskAssoc=압축 파일 형식(zip, 7z, rar 등)의 '연결 프로그램' 목록에 왕집 등록(&A)
korean.TaskMenu=탐색기 마우스 오른쪽 메뉴에 '왕집' 서브메뉴 추가(&M) (Win11은 '더 많은 옵션 표시' 아래)
korean.MenuExtract=여기에 풀기
korean.MenuExtractAll=모두 풀기
korean.MenuCompress=ZIP으로 압축
korean.MenuFolder=이 폴더 압축
korean.ArchiveFile=왕집 압축 파일
korean.UninstallName=왕집 제거
korean.RunNow=지금 왕집 실행(&R)
english.AppShort=WZIP
english.AppDesc=Ad-free open-source archive utility
english.TaskGroup=Additional tasks:
english.TaskDesktop=Create a WZIP &desktop shortcut
english.TaskAssoc=Register WangZip in the 'Open with' list for archive types (zip, 7z, rar, ...) (&A)
english.TaskMenu=Add 'WangZip' submenu to Explorer right-click menu (&M) (Windows 11: under 'Show more options')
english.MenuExtract=Extract here
english.MenuExtractAll=Extract all
english.MenuCompress=Compress to ZIP
english.MenuFolder=Compress this folder
english.ArchiveFile=WangZip archive
english.UninstallName=Uninstall WangZip
english.RunNow=&Run WangZip now

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; ──────────────────────────────────────────────────────────────
; 탐색기 우클릭: "왕집" 서브메뉴 → 여기에 풀기 / 모두 풀기 / ZIP으로 압축
; ──────────────────────────────────────────────────────────────
; 각 확장자마다 SystemFileAssociations\{ext}\shell\WangZip 아래에 서브메뉴 생성
; .zip .7z .rar .tar .gz .tgz .bz2 .xz (총 8개)

; --- .zip ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.zip\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- .7z ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.7z\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- .rar ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.rar\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- .tar ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.tar\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- .gz ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.gz\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- .tgz ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.tgz\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- .bz2 ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.bz2\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- .xz ---
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "SystemFileAssociations\.xz\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtractAll}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipExtractAll"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipExtractAll\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract-all ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- 폴더: "왕집" 서브메뉴 → "이 폴더 압축" ---
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "Directory\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZip\shell\WangZipCreate"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuFolder}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZip\shell\WangZipCreate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZip\shell\WangZipCreate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu

; --- 폴더 배경: "왕집" 서브메뉴 → "이 폴더 압축" ---
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "Directory\Background\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZip\shell\WangZipCreateBackground"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuFolder}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZip\shell\WangZipCreateBackground"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZip\shell\WangZipCreateBackground\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%V"""; Tasks: shellmenu

; --- 모든 파일: "왕집" 서브메뉴 → "ZIP으로 압축" ---
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZip"; ValueType: string; ValueName: "ExtendedSubCommandsKey"; ValueData: "*\shell\WangZip"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZip\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZip\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu
; 연결 프로그램 등록 ("다른 앱 선택" / 기본 앱 목록에 왕집 표시)
Root: HKCU; Subkey: "Software\Classes\WangZip.Archive"; ValueType: string; ValueName: ""; ValueData: "{cm:ArchiveFile}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\WangZip.Archive\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExe},0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\WangZip.Archive\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" ""%1"""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExe},0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" ""%1"""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{cm:AppShort}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{cm:AppDesc}"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "WangZip"; ValueData: "Software\WangZip\Capabilities"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.zip\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".zip"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".zip"; ValueData: "WangZip.Archive"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.7z\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".7z"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".7z"; ValueData: "WangZip.Archive"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.rar\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".rar"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rar"; ValueData: "WangZip.Archive"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.tar\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".tar"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tar"; ValueData: "WangZip.Archive"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.gz\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".gz"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gz"; ValueData: "WangZip.Archive"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.tgz\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".tgz"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tgz"; ValueData: "WangZip.Archive"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.bz2\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".bz2"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bz2"; ValueData: "WangZip.Archive"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\.xz\OpenWithProgids"; ValueType: string; ValueName: "WangZip.Archive"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe\SupportedTypes"; ValueType: string; ValueName: ".xz"; ValueData: ""; Tasks: fileassoc
Root: HKCU; Subkey: "Software\WangZip\Capabilities\FileAssociations"; ValueType: string; ValueName: ".xz"; ValueData: "WangZip.Archive"; Tasks: fileassoc

; 제거할 때는 설치 때 선택한 작업과 무관하게 왕집이 만든 키를 모두 지웁니다.
; (설치 후 환경설정 창에서 켠 항목도 정리되도록 dontcreatekey로 항상 등록해 둡니다)
Root: HKCU; Subkey: "Software\Classes\WangZip.Archive"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\WangZip.exe"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: none; ValueName: "WangZip"; Flags: dontcreatekey uninsdeletevalue

; 새 서브메뉴 구조 (WangZip 키 전체 삭제)
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\zipfile\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey

; 8개 확장자 각각의 WangZip 서브메뉴 키 삭제
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZip"; ValueType: none; Flags: dontcreatekey uninsdeletekey

; 기존 평면 메뉴 키들도 정리
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.zip\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.7z\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.rar\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.tar\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.gz\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.tgz\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.bz2\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtractAll"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.xz\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.cab\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.cab\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.iso\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.iso\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue

[Icons]
Name: "{group}\{cm:AppShort}"; Filename: "{app}\{#MyAppExe}"; IconFilename: "{app}\{#MyAppExe}"; Comment: "{cm:AppDesc}"
Name: "{group}\{cm:UninstallName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{cm:AppShort}"; Filename: "{app}\{#MyAppExe}"; IconFilename: "{app}\{#MyAppExe}"; Tasks: desktopicon; Comment: "{cm:AppDesc}"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:RunNow}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CleanShellMenus(RemoveCurrent: Boolean);
var
  Extensions: TArrayOfString;
  I: Integer;
  Base: String;
begin
  Extensions := ['.zip', '.7z', '.rar', '.tar', '.gz', '.tgz', '.bz2', '.xz', '.cab', '.iso'];
  for I := 0 to GetArrayLength(Extensions) - 1 do
  begin
    Base := 'Software\Classes\SystemFileAssociations\' + Extensions[I] + '\shell\';
    RegDeleteKeyIncludingSubkeys(HKCU, Base + 'WangZipExtract');
    RegDeleteKeyIncludingSubkeys(HKCU, Base + 'WangZipExtractAll');
    if RemoveCurrent then RegDeleteKeyIncludingSubkeys(HKCU, Base + 'WangZip');
  end;
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\*\shell\WangZipCompress');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\WangZipCreate');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\Background\shell\WangZipCreateBackground');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\zipfile\shell\WangZipExtract');
  if RemoveCurrent then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\*\shell\WangZip');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\WangZip');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\Background\shell\WangZip');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then CleanShellMenus(True);
  if CurStep = ssPostInstall then CleanShellMenus(not WizardIsTaskSelected('shellmenu'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then CleanShellMenus(True);
end;
