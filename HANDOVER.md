# WangZip (왕집) 인수인계서

**마지막 갱신**: 2026-11-20 (KST)
**작업 공간**: `D:\llm\zip`
**저장소**: https://github.com/karanglass24-lang/WangZip (GPL-3.0)

---

## 0. 30초 요약

- Windows 11 우선, .NET 10 / WPF 기반 오픈소스 압축 프로그램입니다. ZIP은 직접 구현, 7z·RAR·TAR은 SharpCompress를 사용합니다.
- **이번 릴리스는 v0.9.0**입니다. `origin/main`(v0.8.3)을 병합한 뒤 분할 ZIP, 7z/TAR 만들기, 일시정지·대기열, 메뉴/환경설정 개편을 포함했습니다.
- 병합 전 로컬은 v0.8.1 베이스에서 5개 커밋 앞서 있었고, 원격 릴리스 2개(v0.8.2, v0.8.3)와 갈라져 있었습니다. **병합은 `integrate/0.9.0` 브랜치에서 완료**했고, 백업은 `backup/local-2026-11-20`에 있습니다.
- 현재 빌드는 **경고 0 / 오류 0**, 테스트는 **Core 92개 + App 12개 전부 통과**합니다.
- **남은 일은 실제 GUI 검증**입니다. 자동 테스트는 WPF 마우스·탐색기·다크 메뉴 상태를 보장하지 않습니다. 아래 6번 체크리스트를 참고하세요.

---

## 1. 지금 저장소 상태 (가장 먼저 확인)

```
브랜치            integrate/0.9.0  (릴리스 후 main으로 반영)
병합              origin/main(v0.8.3) 병합 완료
백업              backup/local-2026-11-20
origin/main       70d7ef3  Release v0.8.3
버전              0.9.0
```

### 병합 기록 (v0.9.0)

| 구분 | 내용 |
|---|---|
| 백업 | `backup/local-2026-11-20` |
| 통합 브랜치 | `integrate/0.9.0` |
| 병합 | `origin/main` (v0.8.3, `70d7ef3`) |
| 충돌 파일 | `installer/wangzip.iss`, `App.xaml.cs`, `PreviewDialog.xaml(.cs)`, `MainWindow.xaml(.cs)`, `Theme.xaml` |
| 해결 방식 | 로컬 구현을 채택. 원격 v0.8.3의 드래그 안정화·테마 메뉴·탐색기 메뉴·미리보기 변경이 로컬에 모두 포함된 것을 확인한 뒤 진행 |

병합 후 확인한 원격 변경:

- `MainWindow.xaml.cs`: `EntryGrid_PreviewMouseLeftButtonUp`, `LostMouseCapture`, `_dragPreparation`, `_pendingSingleSelection`, `_sweepAnchor`, `UpdateThemeMenuChecks`, `PreviewTextFileAsync` 포함.
- `MainWindow.xaml`: 최근 파일 메뉴, 테마 메뉴, 드래그 이벤트, 드롭 강조 오버레이 포함.
- `installer/wangzip.iss`: `MUIVerb`, `ExtendedSubCommandsKey`, `CleanShellMenus`, 업그레이드/제거 정리 포함.
- `Theme.xaml`: 다크 메뉴 글자색 보정과 `AccentHoverBrush` 포함.

> 로컬 `afd8a0d`의 미리보기·최근 항목·테마·드래그 캐시·탐색기 메뉴 변경은 v0.8.2/v0.8.3과 겹칩니다. 이후에도 이 영역을 수정할 때는 원격 릴리스 동작을 기준선으로 비교하세요.

---

## 2. 빌드·테스트 현황 (2026-11-20 실행)

```powershell
# 이 환경에서는 실행 정책 때문에 -ExecutionPolicy Bypass가 필요했습니다.
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
.\.dotnet\dotnet.exe test tests\WangZip.App.Tests\WangZip.App.Tests.csproj -c Debug --no-build
```

| 항목 | 결과 |
|---|---|
| Debug 빌드 | 경고 0, 오류 0 |
| Core 테스트 | **92 / 92 통과** (약 3초) |
| App UI 스모크 테스트 | **12 / 12 통과** (약 5초) |
| CLI `--version` | `WangZip CLI 0.8.1` (버전 미상향) |
| CI | `.github/workflows/build.yml`이 Core + App 테스트와 게시를 수행 |

> `build.ps1 -Test`는 **Core 테스트만** 실행합니다. App 테스트는 별도로 실행해야 합니다. CI(`build.yml`)는 둘 다 실행합니다.

---

## 3. 이번 로컬 작업 내용

### 3-1. 분할 ZIP 읽기/쓰기

- 읽기: `.z01 … .zip`(PKZIP/Info-ZIP)과 `.zip.001`, `.001 …`(단순 바이트 분할)을 지원합니다.
- 첫 볼륨 또는 마지막 `.zip`을 열어도 나머지 볼륨을 자동으로 찾습니다.
- 핵심 구현:
  - `src/WangZip.Core/Zip/ZipVolumeSet.cs` — 볼륨 탐색, 볼륨별 시작 오프셋, **seek 가능한** `MultiVolumeStream`.
  - `src/WangZip.Core/Zip/ZipArchiveSource.cs` — 중앙 디렉터리의 디스크 번호/오프셋을 절대 위치로 변환.
  - `src/WangZip.Core/Zip/ZipSplitter.cs` — 만들기용 바이트 분할.
- 만들기: 압축 만들기 창의 "분할 크기"(CLI `create … --split 100M`)로 `.zip.001`, `.zip.002` …를 생성합니다. **단순 바이트 분할**이라 첫 볼륨으로 바로 열 수 있습니다.
- 분할 ZIP은 **읽기 전용**입니다(항목 추가/삭제 비활성).

### 3-2. ZIP 읽기 확장

- Deflate64(압축 방식 9)와 BZip2(12) 항목을 목록·풀기·검사에서 처리합니다.
- 테스트: `tests/WangZip.Core.Tests/ZipMethodTests.cs`.

### 3-3. 압축 풀기 옵션

- "압축 파일 이름 폴더 만들기": 만들지 않음 / 자동 / 항상.
- "전체를 푼 뒤 원본을 휴지통으로 보내기".
- 계획 로직: `src/WangZip.Core/ExtractionPlanner.cs`, 테스트: `ExtractionPlannerTests.cs`.

### 3-4. 7z / TAR 만들기

- 압축하기 창에서 형식 선택: ZIP, 7z(LZMA2), TAR, TAR.GZ, TAR.BZ2.
- 형식을 바꾸면 저장 파일 확장자도 바뀝니다.
- 암호·분할·파일명 인코딩은 **ZIP 전용**이라 다른 형식에서는 비활성화됩니다.
- CLI는 출력 파일 확장자로 형식을 정합니다(`CreateFormats.FromPath`).
- 구현: `src/WangZip.Core/SharpCompressSupport/SharpCompressWriter.cs` — 임시 파일에 쓴 뒤 교체, 취소 시 정리.
- 진입점: `ZipWriter.Create`가 `options.Format`에 따라 SharpCompressWriter로 분기합니다.

### 3-5. 일시정지와 작업 대기열

- `src/WangZip.Core/PauseGate.cs` — `AsyncLocal` 기반. 진행률 보고 지점(`ProgressThrottle.Report`)마다 확인하므로 압축·풀기·검사·삭제가 모두 일시정지됩니다.
- 진행 창에 일시정지/계속 단추, 경과/남은 시간, 대기 순번 표시.
- 여러 작업을 동시에 시작하면 `ProgressDialog`의 정적 `QueueLock`으로 하나씩 실행합니다.
- 일시정지 중에도 취소가 동작합니다.

### 3-6. 메뉴 / 도구 모음 / 환경설정

- 메뉴: 파일 / 편집 / 도구 / 보기 / 도움말.
- 도구 모음: 열기 | 압축하기 ‖ 압축 풀기 | 선택 풀기 ‖ 파일 추가 | 항목 삭제 ‖ 검사 ‖ (오른쪽) 환경설정.
- 단축키: `Del`, `Enter`, `Ctrl+W`, `Ctrl+Shift+E`, `Ctrl+,`, `Ctrl+F`, `F5` 등.
- 상태에 맞는 활성화: 파일 추가·삭제·검사는 ZIP에서만(분할 ZIP 제외), 삭제·선택 풀기는 선택이 있을 때만.
- 환경설정 탭: 일반 / 압축 풀기 / 압축하기 / 목록 / 탐색기 연동.
- "기본값으로 되돌리기", "최근 목록 지우기" 추가.
- 레지스트리(탐색기 연동)는 값이 바뀔 때만 쓰고, 실패해도 나머지 설정은 저장합니다.
- 최근 항목 경로에 `_`가 있으면 사라지던 문제 수정.
- 테스트: `tests/WangZip.App.Tests/MainWindowMenuTests.cs`, `EnglishStringsTests.cs`.

### 3-7. 테마 / 접근성

- 전역 `TextBlock` 스타일 제거 후 창·컨트롤에 테마 글자색을 지정해, 다크 테마에서 대화상자·메뉴·체크 상자 글자가 보이도록 보정했습니다.
- 진행 창·미리보기 창의 흰 배경을 테마 배경으로 교체.
- 대화상자에 `Esc`(IsCancel), 처음 포커스, `AutomationProperties.Name` 추가.
- 테스트: `tests/WangZip.App.Tests/ThemeSmokeTests.cs`.

### 3-8. 릴리스 자동화

- `.github/workflows/release.yml` 신규:
  - `v*` 태그 푸시 → Core 테스트 → 앱/CLI 게시 → 포터블 ZIP + Inno Setup 설치 프로그램 → GitHub Release 첨부.
  - `workflow_dispatch` + 버전 입력 → 산출물만 만들고 릴리스에는 첨부하지 않음.
- `.github/workflows/build.yml`에 App UI 스모크 테스트 단계 추가.

---

## 4. 자동 테스트가 보장하는 것 / 보장하지 않는 것

**보장함**

- ZIP/분할 ZIP/7z/TAR 생성·읽기 왕복, CRC/무결성.
- Deflate64/BZip2 읽기.
- 압축 풀기 계획(폴더 만들기/휴지통), 일시정지 게이트.
- 영어 문자열 키 중복/누락, 메뉴 구성·단축키 중복.
- WPF 창 생성과 기본 테마 색상(스모크 수준).

**보장하지 않음**

- 실제 WPF 마우스 드래그 선택·탐색기 드롭.
- 실제 탐색기 우클릭 메뉴, 업그레이드/제거.
- 일시정지·대기열의 실제 장시간 작업 동작.
- 다크/라이트 런타임 전환과 메뉴 상태별 색상.
- 4GB 초과 ZIP64 분할, 암호 걸린 분할 ZIP, 유니코드 파일명 분할.
- 7z/TAR 생성의 취소·덮어쓰기·매우 긴 경로.

---

## 5. 다음 담당자 우선순위

1. **v0.9.0 릴리스 산출물 확인** — GitHub Release에 ZIP·설치 파일이 첨부됐는지, CI 로그에 경고/오류가 없는지 확인합니다.
2. **아래 6번 수동 검증 체크리스트 수행.** 자동 테스트가 다루지 않는 영역입니다.
3. 발견된 문제는 `main`에서 수정하고 필요하면 `0.9.1`로 릴리스합니다.
4. 다음 기능은 Win11 새 콘텍스트 메뉴, 셸 네임스페이스 드래그 아웃, 분할 7z/RAR입니다.
5. 버전을 올릴 때는 아래 5곳을 함께 수정합니다.
   - `src/WangZip.App/WangZip.App.csproj` (`<Version>`)
   - `src/WangZip.Cli/WangZip.Cli.csproj` (`<Version>`)
   - `src/WangZip.Cli/Program.cs` (도움말의 `vX.Y.Z`)
   - `installer/wangzip.iss` (`MyAppVersion`)
   - `publish.ps1`, `installer/build-installer.ps1` (`$version`)
6. `README.md`와 `CHANGELOG.md`의 버전/기능 설명을 함께 갱신합니다.

---

## 6. 수동 검증 체크리스트

### A. 분할 ZIP 읽기

- [ ] 7-Zip/Info-ZIP으로 만든 `.z01 … .zip` 세트를 첫 볼륨과 마지막 `.zip` 양쪽에서 열기
- [ ] `.zip.001` / `.001 …` 바이트 분할 세트 열기
- [ ] 목록·검색·폴더 트리·미리보기 정상
- [ ] 전체 풀기 후 원본과 해시/CRC 비교
- [ ] 한글 파일명, 빈 폴더, 중첩 폴더
- [ ] 볼륨이 빠졌을 때 이해 가능한 오류
- [ ] 분할 ZIP에서 항목 추가/삭제가 비활성화되는지
- [ ] 암호 걸린 분할 ZIP의 처리(현재 미지원 안내)

### B. 분할 ZIP 만들기

- [ ] 100MB 분할 생성 후 첫 볼륨으로 열기/풀기
- [ ] 기존 파일 덮어쓰기 확인
- [ ] 취소 시 임시/분할 파일 정리
- [ ] CLI `create … --split 100M`

### C. 7z / TAR 만들기

- [ ] ZIP, 7z, TAR, TAR.GZ, TAR.BZ2 각각 생성 후 다시 열기/풀기
- [ ] 형식 전환 시 확장자 변경, ZIP 전용 옵션 비활성화
- [ ] 취소·덮어쓰기·빈 폴더·긴 경로·큰 파일
- [ ] CLI 확장자 추론(`.7z`, `.tar`, `.tar.gz`, `.tar.bz2`)
- [ ] 7z 결과를 7-Zip 등 외부 도구로 열기

### D. 일시정지 / 대기열

- [ ] 압축·풀기·검사·삭제 각각 일시정지/계속
- [ ] 일시정지 중 경과 시간 정지, 취소 즉시 동작
- [ ] 작업 2개 이상 시작 시 순차 실행과 "대기 중" 표시
- [ ] 대기 중인 작업 취소
- [ ] 진행 창을 닫았을 때 동작

### E. 메뉴 / 단축키 / 환경설정

- [ ] 모든 단축키(`Del`, `Enter`, `Ctrl+W`, `Ctrl+Shift+E`, `Ctrl+,`, `Ctrl+F`, `F5`)
- [ ] 비활성 단추의 툴팁 이유
- [ ] 환경설정 저장→재시작→복원, 기본값 되돌리기, 최근 목록 지우기
- [ ] 레지스트리 값이 바뀔 때만 쓰이는지
- [ ] 레지스트리 쓰기 실패 시 나머지 설정 저장
- [ ] 최근 항목 경로의 `_` 표시

### F. 테마 / 접근성

- [ ] Windows 라이트/다크 × 앱 시스템/라이트/다크 조합
- [ ] 파일·편집·도구·보기·도움말, 최근 항목 중첩 메뉴, 목록/폴더 트리 우클릭
- [ ] 단축키 표시, 마우스 오버, 열림, 비활성 항목
- [ ] 런타임 테마 전환
- [ ] 대화상자 `Esc`, Tab 순서, 화면 읽기 이름

### G. 드래그 앤 드롭

- [ ] `Ctrl+A` 후 파일/폴더를 탐색기로 드롭
- [ ] 첫 복사 중 두 번째 드래그
- [ ] 준비 중 버튼을 놓거나 다시 누르기
- [ ] 2GB 초과 거부 안내
- [ ] 임시 캐시 8GiB 한도, 최근 2일 보호 정책
- [ ] 앱 종료 시 임시 폴더 정리

### H. 탐색기 연동

- [ ] 압축 파일/일반 파일/폴더/폴더 배경 메뉴 구조와 중복 여부
- [ ] "이 폴더 압축" 하위 메뉴
- [ ] 메뉴 끄기, 업그레이드, 제거
- [ ] Windows 11 "더 많은 옵션 표시" 경로

---

## 7. 릴리스 절차 (현재 기준)

### 권장: 태그 푸시로 CI 릴리스

```powershell
# 1) 병합·버전 상향·문서 갱신 후
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
.\.dotnet\dotnet.exe test tests\WangZip.App.Tests\WangZip.App.Tests.csproj -c Release

# 2) 커밋 & 태그
git add -A
git commit -m "Release v0.9.0"
git tag v0.9.0
git push origin main
git push origin v0.9.0
```

`release.yml`이 자동으로:

1. Core 테스트 실행
2. `artifacts/WangZip-0.9.0-win-x64` 게시 + ZIP 생성
3. Inno Setup으로 `WangZip-0.9.0-setup.exe` 생성
4. GitHub Release 생성/첨부

> `release.yml`의 테스트 단계는 Core만 실행합니다. App 테스트도 릴리스 전에 로컬에서 확인하거나 워크플로에 추가하는 것을 권장합니다.

### 수동 릴리스(참고)

```powershell
.\publish.ps1 -Zip
# → artifacts\WangZip-<version>-win-x64\ 및 .zip

# Inno Setup 6 설치 후
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "/DMyAppVersion=0.9.0" installer\wangzip.iss
# → artifacts\WangZip-0.9.0-setup.exe
```

- `publish.ps1`은 `-p:Version`을 넘기지 않으므로 **csproj 버전을 먼저 올려야** 합니다.
- `installer/wangzip.iss`는 `SourceDir = ..\artifacts\WangZip-<버전>-win-x64`를 사용합니다. 게시 폴더가 먼저 있어야 합니다.
- `installer/wangzip.iss`는 기본(=`/DTestBuild` 없음)일 때 `WangZip-<버전>-setup.exe`를 만듭니다.

### 현재 `artifacts/` 주의

- 들어 있는 것은 **0.8.x 산출물과 0.8.1 TEST 설치 파일**입니다.
- 새 릴리스에는 사용하지 말고, v0.9.0 CI 산출물이나 새로 게시한 파일을 사용하세요.

---

## 8. 코드 지도

```
src/
├── WangZip.Core/
│   ├── ArchiveService.cs        # 형식 감지, 열기 진입점
│   ├── ArchiveModels.cs         # ArchiveFormat, CreateFormat, 옵션 모델
│   ├── CreateFormats.cs         # 확장자 ↔ 형식
│   ├── ExtractionPlanner.cs     # 폴더 만들기/휴지통 계획
│   ├── ExtractionHelpers.cs     # 진행 스로틀 + PauseGate 확인
│   ├── PauseGate.cs             # 일시정지(AsyncLocal)
│   ├── Zip/
│   │   ├── ZipArchiveSource.cs  # ZIP 읽기/검사
│   │   ├── ZipVolumeSet.cs      # 분할 볼륨 탐색 + MultiVolumeStream
│   │   ├── ZipSplitter.cs       # 만들기용 바이트 분할
│   │   ├── ZipWriter.cs         # ZIP 생성 + 다른 형식 분기
│   │   ├── ZipRewriter.cs       # ZIP 항목 추가/삭제
│   │   ├── EncryptedZipWriter.cs
│   │   └── ZipStructures.cs
│   ├── SharpCompressSupport/
│   │   ├── SharpCompressSource.cs
│   │   └── SharpCompressWriter.cs  # 7z/TAR/TAR.GZ/TAR.BZ2 생성
│   ├── Safety/                  # PathGuard, CRC32, MOTW
│   └── EnglishStrings.cs        # 영어 번역(한국어 원문이 키)
├── WangZip.App/
│   ├── MainWindow.xaml(.cs)     # 메인 창, 드래그, 메뉴
│   ├── ViewModels/MainViewModel.cs
│   ├── Dialogs/                 # Create/Extract/Progress/Preferences/Preview/Password/About
│   ├── Theme/Theme.xaml
│   ├── ShellIntegration.cs      # 탐색기 우클릭 등록
│   ├── ShellCommands.cs
│   ├── DragTempCache.cs         # 드래그 임시 추출 캐시
│   └── Settings.cs
├── WangZip.Cli/Program.cs
tests/
├── WangZip.Core.Tests/          # 92개
└── WangZip.App.Tests/           # 12개 (WPF 스모크/메뉴/대화상자)
```

### 빠른 명령

```powershell
.\run-app.ps1
.\run-cli.ps1 --help
.\publish.ps1 -Zip
git log --oneline -10
```

`.dotnet/dotnet.exe`가 있으면 스크립트가 자동으로 사용합니다.

---

## 9. 알려진 한계 / 주의사항

| 항목 | 상태 |
|---|---|
| PKZIP 분할 ZIP 수정 | 읽기 전용(추가/삭제 비활성) |
| 분할 ZIP 만들기 | 단순 바이트 분할(`.zip.001`). `.z01` 레이아웃 생성은 아님 |
| 암호 걸린 분할 ZIP | 미지원 |
| ZIP64 4GB+ 분할 | 미검증 |
| 분할 ZIP 유니코드 파일명 | 미검증 |
| 7z/TAR 만들기 암호화 | 미지원(ZIP 전용 옵션) |
| 7z/TAR 만들기 | 취소·덮어쓰기·긴 경로 실제 GUI 미검증 |
| 드래그 앤 드롭 | 2GB 제한, 캐시 8GiB, 최근 2일 보호 |
| 드래그 캐시 크기 메타데이터 | 잘못된 크기 정보가 있으면 일시적으로 예약 한도 초과 가능 |
| 탐색기 메뉴 | 실제 Explorer/WPF 검증 필요 |
| 다크 테마 | 런타임 전환·상태별 색상 실제 화면 검증 필요 |
| 버전 번호 | 0.9.0 (csproj·iss·publish·CLI 도움말 동기화) |
| README | v0.9.0 기준으로 갱신 |
| `docs/HANDOFF.md` | 2026-11-05 문서, 최신 상태 아님 |
| `artifacts/` | 0.8.x 산출물은 릴리스에 사용 금지. v0.9.0 CI 산출물 사용 |

---

## 10. 과거 기록 요약

- **v0.6.x**: 환경설정, 탐색기 메뉴, RAR/7z 비밀번호, 안전성 수정.
- **v0.7.x**: 한/영 지원, 아이콘, 압축 풀기 대화상자 정리.
- **v0.8.0**: 탐색기 "모두 풀기".
- **v0.8.1**: 드래그 앤 드롭 개선(2GB/진행 표시/자동 정리), "왕집" 서브메뉴, 배치 메시지 억제.
- **v0.8.2**: 텍스트 미리보기, 최근 파일/폴더, 테마.
- **v0.8.3**: 드래그 앤 드롭 안정화, 탐색기 메뉴, 다크 메뉴 글자색.
- **v0.9.0**: 분할 ZIP 읽기/만들기, 7z/TAR 만들기, 일시정지·대기열, 메뉴/환경설정 개편.

> 이전 625줄 인수인계서(분할 ZIP 설계 초안 포함)는 git 커밋 `afd8a0d`에 남아 있습니다. 당시 문서의 분할 ZIP 설계는 **이미 구현되어 대체되었으므로** 그대로 따르지 마세요. 특히 "bit 3 = 분할 표시"라는 설명은 틀렸습니다(bit 3은 데이터 디스크립터 사용 여부).

---

## 11. 참고

- GitHub Issues: https://github.com/karanglass24-lang/WangZip/issues
- Releases: https://github.com/karanglass24-lang/WangZip/releases
- ZIP 명세: https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
- SharpCompress: https://github.com/adamhathcock/sharpcompress
- 커밋 기록: `git log --oneline -20`
- 릴리스 확인: `gh release view v0.8.3 --repo karanglass24-lang/WangZip`
