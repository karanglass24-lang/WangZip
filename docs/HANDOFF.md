# 왕집(WangZip) 인수인계 문서

작성일: 2026-11-05 · 기준: v0.5.0 릴리스 이후 작업 중인 상태
작업 환경: Windows 11 (RTX 3060, 세션 1) · 저장소 `D:\llm\zip` · 대화 언어 한국어

---

## 1. 프로젝트 개요

- **왕집(WangZip)**: 광고·번들·텔레메트리 없는 한국어 무료 오픈소스 압축 프로그램.
  GPL-3.0, C#/.NET 10 WPF, Windows 11 우선.
- GitHub: `https://github.com/karanglass24-lang/WangZip` (브랜치 main)
- 구성:
  - `src/WangZip.Core` — 압축 엔진 (ZIP 직접 구현, 암호화 생성/해제, 인코딩 판별, 경로 안전)
  - `src/WangZip.Cli` — CLI (`wangzip list/extract/create/verify --password`)
  - `src/WangZip.App` — WPF GUI
  - `tests/WangZip.Core.Tests` — xUnit 52개
  - `installer/` — Inno Setup 7 (`.tools\inno`)
- 배포형태: 설치형(setup.exe, 사용자 폴더 설치) + 포터블(zip), 둘 다 self-contained.
- 자동 업데이트 내장 (GitHub Releases 확인 → 무음 설치 → 재실행).

### 현재 릴리스

| 항목 | 값 |
|---|---|
| 릴리스 | v0.5.0 — https://github.com/karanglass24-lang/WangZip/releases/tag/v0.5.0 |
| 커밋 | `33a5cd6` (CI success) |
| 자산 | `WangZip-0.5.0-setup.exe` (73,043,714B), `WangZip-0.5.0-win-x64.zip` (98,166,772B) |
| v0.5.0 기능 | **암호 걸어 ZIP 만들기(WinZip AES-256/AE-2)** — 대화상자 체크박스 + CLI `--password` |
| 사용자 PC | v0.5.0 설치 완료 (`%LOCALAPPDATA%\Programs\WangZip`), 시작 메뉴/제거 항목 정상 |

---

## 2. 핵심 구현 위치 (v0.5.0)

- `src/WangZip.Core/Zip/EncryptedZipWriter.cs` — AES-256(AE-2) ZIP 생성기.
  salt 16B + PBKDF2-SHA1 1000회 + AES-CTR(카운터 1, LE) + HMAC-SHA1 앞 10B.
  로컬 헤더 seek-back 패치, AE-2는 CRC 필드 0(미사용).
- `src/WangZip.Core/Zip/ZipWriter.cs` — `CreationOptions.Password` 있으면 EncryptedZipWriter로 분기.
- `src/WangZip.Core/ArchiveModels.cs` — `CreationOptions.Password` 추가.
- `src/WangZip.App/Dialogs/CreateDialog.xaml(.cs)` — "암호 걸기 (AES-256)" 체크박스, 비밀번호/확인, 검증.
- `src/WangZip.Cli/Program.cs` — `create --password`, `--version`은 어셈블리 버전 자동.
- 저작 제한: 4GB 이상 항목·압축파일, 65,535개 초과 항목은 명시적 예외 + 부분 파일 삭제.

### 검증 결과 (모두 통과)

- 코어 테스트 52개 (`build.ps1 -Test`).
- pyzipper(외부 도구)로 생성 ZIP 목록/해제/암호 오류 거부/한글 파일명 확인.
- UI E2E(파일 대화상자 → 암호 입력 → 생성 → 구조 확인 `method=99, AE-2, strength 3`).

---

## 3. 진행 중 작업 (⚠ 커밋 안 됨 — git status에 변경 있음)

사용자 피드백 3가지 구현 중:

1. **폴더 더블클릭 → 폴더 안으로 진입 + 왼쪽 트리 자동 펼침**
   - `src/WangZip.App/MainWindow.xaml.cs`: `EntryGrid_MouseDoubleClick`에서 폴더 행이면
     `NavigateIntoFolder(row.Info.Name.TrimEnd('/'))` → `_viewModel.SelectFolder(path)` +
     `SelectTreeNode(path)`(트리 체인 생성 후 차례로 펼치고 선택/BringIntoView).
   - `BuildNodeChain`은 FolderNode 트리에서 경로 체인을 만들고,
     `ItemsControl.ItemContainerGenerator.ContainerFromItem`로 컨테이너를 하나씩 얻어
     `IsExpanded=true; UpdateLayout()`을 반복.
2. **메인 화면 "압축 풀기 위치" 콤보 삭제** (압축 풀기 대화상자에만 존재하도록)
   - MainWindow.xaml에서 콤보/레이블 제거, 코드에서 `QuickDestCombo`/`QuickDest_Changed` 제거.
   - MainViewModel: `QuickDestinationIndex`, `SelectedQuickHint`, `ResolveQuickDestination` 제거
     (`QuickDestinations`만 유지 — ExtractDialog가 사용).
3. **"파일명 인코딩"을 도구 메뉴 하위로 이동**
   - 메뉴: 도구 > 파일명 인코딩 (자동 감지/UTF-8/CP949/원본 바이트, `IsCheckable`).
   - 핸들러 `EncodingMenu_Click`(Tag 문자열 → NameEncodingMode, 재열기), `UpdateEncodingMenuChecks`.
   - MainViewModel에서 `EncodingOptions`/`EncodingOption` 레코드 삭제(사용처 없음).
4. **아이콘 교체 (사용자 제공 이미지)**
   - 앱 아이콘: `Assets/app-icon.ico`(다중 크기) + `app-icon.png`(1024 마스터) —
     마스코트 배지. 배경 제거는 %TEMP%\make-icons.py(PIL, 가장자리 플러드필)로 생성.
   - 상단 6개 버튼 아이콘: `Assets/tool-open/extract/extract-selected/create/add/delete.png`
     (사용자 시트에서 각 아이콘 크롭 + 배경 제거, 128px).
   - 매핑: 열기=금고, 압축 풀기=감옥(전체 해제), 선택 풀기=사슬묶인 폴더+손, 압축하기=프레스, 파일 추가=보관함+플러스, 항목 삭제=불타는 드럼.
     **검사/새로 고침 버튼은 예비 아이콘이 없어 기존 폰트 글리프 유지** (이미지 생성 시도는 계정 잔액 부족으로 실패).
   - csproj: `<Resource Include="Assets\tool-*.png" />` 추가.

### ⚠ 미해결 문제 (확인 중)

- **왼쪽 폴더 트리 루트에 하위 폴더가 절대 안 나옴(기존 버그 의심)**.
  `nav2.zip`(명시적 `DB/`, `EX/` 디렉터리 항목 포함)을 열어도 트리에 `nav2.zip`만 표시,
  펼침 화살표/ExpandCollapse 패턴 없음(`Expand` 호출 실패, UIA 상태 빈 값).
  `MainViewModel.RebuildTree`는 로직상 DB/EX/DB/sub 노드를 root와 연결해야 하는데 실제 렌더에 반영 안 됨.
  → 원인 후보: (a) RebuildTree에서 조용한 예외/로직 문제, (b) TreeItem 컨트롤 템플릿/헤더 이슈,
     (c) ExpandTreeRoot 타이밍. ** 우선 순위 1**.
- **그리드 더블클릭 네비게이션은 코드는 있으나 시각 확인 미완료**.
  (자동화 클릭이 헤더 부근을 눌렀을 가능성 — 행 rect 계산은 대략 `grid.Y + 55`.)
- 위 두 문제 확인 후에 버전 0.6.0으로 상향·문서 갱신·릴리스 예정.

---

## 4. 환경·도구 관련 중요 노트

- **dotnet이 PATH에 없음**: `D:\llm\zip\.dotnet\dotnet.exe` 사용.
  스크립트: `build.ps1 [-Test]`, `publish.ps1 [-Zip]`, `installer\build-installer.ps1 [-SkipPublish]`.
- **gh CLI**: `.tools\bin\gh.exe` (인증됨). Python이 있어 이미지/SQLite 작업 가능(numpy 없음, PIL 있음).
- **빌드 아티팩트**: `artifacts/WangZip-<ver>-win-x64/`, `...-setup.exe`.
- **한글 파일**: `.iss`, 한글 포함 .ps1은 반드시 **UTF-8 BOM**.
- **PowerShell 5.1 함정**:
  - BOM 없는 스크립트의 한글 리터럴이 깨짐 → 실행 전 BOM 추가 함수 사용(아래).
  - 장문 인라인 스크립트에서 괄호 개수 어긋 나면 **파서 오류가 EOF 위치만 알려 줌** →
    여러 줄 New-Object 중첩보다 `New-Object` 결과를 변수에 담아 `FindAll($scope, $cond)` 형태 권장.
- **첨부 이미지 추출 가능 위치**: `~/.local/share/opencode/opencode.db` →
  `session_message.data`의 JSON `files[].data`(base64). 현재 추출본:
  `%TEMP%\user-files\user-1044-0.jpg`(앱 아이콘), `user-1045-0.jpg`(버튼 시트).
- **이미지 생성 도구 사용 불가**: 계정 잔액 부족("Insufficient account funds") —
  3D 스타일 추가 아이콘(검사/새로 고침) 자동 생성 실패. 수동 제작 또는 상징 재활용 필요.

### WPF가 하얗게 나오는 문제(이 PC만의 현상)

- 최소 WPF 앱도 렌더가 실패하는 상태였음 → **WPF 하드웨어 가속 고장**.
- 완화책 적용 중: `HKCU\Software\Microsoft\Avalon.Graphics` `DisableHWAcceleration=1`
  (소프트웨어 렌더링으로 강제, 모든 WPF 앱 정상 표시).
- NVIDIA 드라이버 2026-10-27자(32.0.16.2112). **재부팅 후 정상이면 이 레지스트리 값 제거** 권장(작업 항목).
- 캡처 요령: CopyFromScreen은 다른 창에 가려지는 영역을 못 찍고, 화면 밖 좌표는 하얗게 나옴.
  **PrintWindow(PW_RENDERFULLCONTENT, flag=2)**가 확실 — 소프트웨어 렌더링 상태에서 정상 동작.

### 설치본 사고 이력(재발 주의)

- **같은 AppId 충돌**: 테스트 설치가 실제 설치의 시작 메뉴/제거 항목을 지운 적 있음
  (AppId `{B4F3D9E2-6C1A-4E8B-9D5F-2A7C0E1B8F30}` — setup을 다시 무음 설치해 복구됨).
  테스트 설치/포터블 검증은 별도 임시 경로 + AppId 조치 필요. **AppId 격리 미구현**.
- 설치 검증은 `artifacts\WangZip-<ver>-setup.exe /VERYSILENT` 후
  `%LOCALAPPDATA%\Programs\WangZip\WangZip.exe` 버전 +
  시작 메뉴 lnk + HKCU Uninstall DisplayName 확인.

---

## 5. 배포 절차 (반복용)

1. 기능 구현 완료 후 버전 상향: `src/WangZip.App/WangZip.App.csproj`, `src/WangZip.Cli/WangZip.Cli.csproj`
   (0.5.0 → 다음), `publish.ps1`, `installer/build-installer.ps1`, `installer/wangzip.iss`, CLI 도움말 텍스트(v0.X 표기).
2. `powershell .\build.ps1 -Test` (테스트 포함 빌드) → `.dotnet\dotnet.exe test ...`도 가능.
3. `powershell .\publish.ps1 -Zip` (self-contained + zip)
4. `powershell .\installer\build-installer.ps1 [-SkipPublish]`
5. 검증: 빌드된 포터블 실행/UI 확인, setup 무음 설치 후 시작 메뉴(lnk 2개: 왕집, 왕집 제거)와
   HKCU Uninstall `왕집 (WangZip)` / 버전 확인.
6. git commit/push → CI 통과(`.tools\bin\gh.exe run list`) 확인.
7. `.tools\bin\gh.exe release create vX.Y.Z "artifacts\..." "artifacts\..." --title "왕집 vX.Y.Z" --notes-file <notes.md>`
8. 사용자 PC 무음 설치로 업그레이드.
9. `CHANGELOG.md`, `README.md`(버전/기능/제한·로드맵 표) 갱신은 릴리스 전에.

릴리스 노트 양식 예시는 v0.5.0 페이지 참고(작성 시점의 노트).

---

## 6. 다음 해야 할 일 (우선순위)

1. **왼쪽 트리 하위 폴더 표현 깨진 원인 해결** — 3장에 상세. DB/EX 케이스 재현 스크립트:
   `%TEMP%\wangzip-nav-test\nav2.zip`(명시적 디렉터리 항목) 생성은 `%TEMP%\make-nav2.py`.
2. **더블클릭 네비게이션 시각 검증** — 행을 정확히 누르도록 자동화 수정 필요
   (rows는 DataItem 패턴에 이름이 안 나옴 → 열 헤더 아래 첫 번째 데이터 행 좌표 계산 이용).
   정상이면 `PathText`가 `/ → DB/`로 바뀌고 트리에서 DB가 선택/펼쳐져야 함.
3. 확정되면 **v0.6.0 릴리스** (번호 상향·문서·설치본·GitHub 릴리스).
4. (보류) Windows 11 새 콘텍스트 메뉴: MSIX sparse package 서명 필요 → 미서명 배포와 상충.
   필요하면 “고전 메뉴 유지 + 서명 연구” 순으로.
5. (개선) 앱이 스스로 만든 ZIP에 디렉터리 항목을 안 넣는 문제 →
   ZipWriter가 폴더 항목을 기록하지 않아 그리드에 하위 폴더 행이 안 보임(트리는 폴더 경로 유추).
   "폴더 항목 기록" 추가 검토. nav.zip은 이 때문의 예시로 만들었음.
6. (환경) 재부팅 후 `DisableHWAcceleration=1` 제거하고 WPF 하드웨어 가속 복구 확인.
7. AppId 격리(테스트 설치가 실제 설치 안 건드리게) 구현.
8. 검사/새로 고침용 아이콘 확보(사용자와 상의: 3D 새 제작 vs 기존 글리프 유지).

---

## 7. 자주 쓰는 명령·스니펫

```powershell
# 빌드+테스트
powershell .\build.ps1 -Test

# 배포
powershell .\publish.ps1 -Zip
powershell .\installer\build-installer.ps1 -SkipPublish

# CI 상태
.tools\bin\gh.exe run list --limit 3

# 무음 설치
Start-Process .\artifacts\WangZip-0.6.0-setup.exe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES' -Wait

# ps1에 BOM 붙이기
$f='파일.ps1'; $t=[IO.File]::ReadAllText($f,[Text.Encoding]::UTF8); [IO.File]::WriteAllText($f,$t,(New-Object System.Text.UTF8Encoding($true)))
```

테스트 검증용 암호 ZIP 생성기(주요): `tests/WangZip.Core.Tests/EncryptedZipWriter.cs` (ZipCrypto/AES-1 기록 헬퍼).
