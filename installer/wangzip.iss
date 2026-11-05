; WangZip (왕집) 설치 프로그램
; Inno Setup 7로 컴파일합니다: .\installer\build-installer.ps1
;
; - 자체 포함 배포본(artifacts\WangZip-<버전>-win-x64)을 통째로 담아 .NET 설치가 필요 없게 합니다.
; - 사용자 단위 설치(권한 상승 없음, {autopf} → LocalAppData\Programs)를 씁니다.

#define MyAppVersion "0.7.0"
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
korean.TaskMenu=탐색기 마우스 오른쪽 메뉴에 '여기에 풀기 / ZIP으로 압축' 추가(&M) (Win11은 '더 많은 옵션 표시' 아래)
korean.MenuExtract=왕집: 여기에 풀기
korean.MenuCompress=왕집: ZIP으로 압축
korean.MenuFolder=왕집: 이 폴더 압축
korean.ArchiveFile=왕집 압축 파일
korean.UninstallName=왕집 제거
korean.RunNow=지금 왕집 실행(&R)
english.AppShort=WZIP
english.AppDesc=Ad-free open-source archive utility
english.TaskGroup=Additional tasks:
english.TaskDesktop=Create a WZIP &desktop shortcut
english.TaskAssoc=Register WangZip in the 'Open with' list for archive types (zip, 7z, rar, ...) (&A)
english.TaskMenu=Add 'Extract here / Compress to ZIP' to the Explorer right-click menu (&M) (Windows 11: under 'Show more options')
english.MenuExtract=WangZip: Extract here
english.MenuCompress=WangZip: Compress to ZIP
english.MenuFolder=WangZip: Compress this folder
english.ArchiveFile=WangZip archive
english.UninstallName=Uninstall WangZip
english.RunNow=&Run WangZip now

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; ZIP 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; 7z 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; 폴더: 압축하기
; 모든 파일: 왕집으로 압축 (여러 개를 선택하면 하나의 ZIP으로 묶음)
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZipCompress"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuCompress}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZipCompress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZipCompress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZipCreate"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuFolder}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZipCreate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZipCreate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%1"""; Tasks: shellmenu
; 폴더 배경: 바탕 화면/폴더 안에서 이 위치 압축
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZipCreateBackground"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuFolder}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZipCreateBackground"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZipCreateBackground\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-create ""%V"""; Tasks: shellmenu

; rar 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; tar 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; gz 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; tgz 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; bz2 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
; xz 형식: 여기에 풀기
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtract"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuExtract}"; Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtract"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExe}"; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" here-extract ""%1"""; Tasks: shellmenu
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
Root: HKCU; Subkey: "Software\Classes\*\shell\WangZipCompress"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\WangZipCreate"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\WangZipCreateBackground"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\zipfile\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.zip\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.7z\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.rar\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.tar\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.gz\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.tgz\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.bz2\OpenWithProgids"; ValueType: none; ValueName: "WangZip.Archive"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xz\shell\WangZipExtract"; ValueType: none; Flags: dontcreatekey uninsdeletekey
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
