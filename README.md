# WangZip (왕집)

광고·번들 소프트웨어·사용 추적이 없는 **오픈소스 압축 프로그램**입니다.
한국 사용자가 알집/반디집에서 기대하는 사용감(한글 파일명, `여기에 풀기`, 탐색기형 목록)을
오픈소스만으로 구현하는 것을 목표로 합니다.

- 라이선스: **GPL-3.0** (광고를 넣은 폐쇄소스 포크를 막기 위해)
- 플랫폼: Windows 11 우선 (Windows 10은 최선 노력 지원)
- 런타임: .NET 10 / WPF

> **현재 상태: v0.6.1 개발판.** ZIP 읽기/쓰기와 한글 파일명 복원, 암호 걸린 압축 해제,
> **AES-256 암호 걸어 압축 만들기**, CLI, WPF UI가 동작합니다.
> 설치형·포터블 배포본은 [Releases](https://github.com/karanglass24-lang/WangZip/releases)에 있습니다.
> 분할 압축·Win11 새 우클릭 메뉴는 아직 없습니다. 아래 [제한 사항](#제한-사항)을 참고하세요.

---

## 왜 만들었나

알집과 반디집은 기능은 충분하지만, 무료 버전의 광고와 업그레이드 유도가 사용 흐름을 방해합니다.
WangZip은 다음 원칙으로 그 문제를 없앱니다.

| 원칙 | 구현 |
|---|---|
| 광고 없음 | UI에 광고·프로모션 영역이 없음 |
| 기능 잠금 없음 | 모든 구현 기능을 처음부터 사용 |
| 번들 설치 없음 | 설치 프로그램·포터블 모두 광고/추가 프로그램 끼워넣기 없음 |
| 추적 없음 | 네트워크 통신은 "업데이트 확인"시 단 한 번이며, 아무 정보도 전송/저장하지 않음 |
| 오픈소스 | GPL-3.0, 의존성은 MIT/BSD/zlib 계열만 사용 |

---

## 빠른 시작

### 0. 일반 사용자 — 설치형 또는 포터블

소스 빌드 없이 쓰려면 [Releases](https://github.com/karanglass24-lang/WangZip/releases)에서 받습니다.
둘 다 자체 포함 빌드라 별도 .NET 설치가 필요 없습니다.

- **설치형(권장)**: `WangZip-0.6.1-setup.exe` 실행. 폴더 선택, 시작 메뉴 바로 가기,
  바탕 화면 바로 가기(선택 사항), 프로그램 제거 등록까지 해 줍니다.
  관리자 권한 없이 사용자 단위(`LocalAppData\Programs\WangZip`)에 설치됩니다.
- **포터블**: `WangZip-0.6.1-win-x64.zip`을 풀고 `WangZip.exe`를 실행.

> **"Windows의 PC 보호" (SmartScreen) 경고가 뜨면:** 왕집은 아직 코드 서명이 없는 개인 배포본이라
> 처음 실행할 때 파란 경고창이 나올 수 있습니다. **추가 정보 → 실행**을 누르면 됩니다.
> 내려받은 파일의 무결성은 Releases 페이지의 파일 크기와 GitHub 빌드(CI) 결과로 확인할 수 있습니다.
> 코드 서명 적용은 로드맵에 있습니다.

### 1. .NET 10 SDK 준비 (소스 빌드할 때만)

이미 설치되어 있다면 건너뜁니다.

```powershell
winget install Microsoft.DotNet.SDK.10
```

관리자 권한 없이 저장소 안에만 설치하려면:

```powershell
curl.exe -sSL https://dot.net/v1/dotnet-install.ps1 -o dotnet-install.ps1
powershell -ExecutionPolicy Bypass -File .\dotnet-install.ps1 -Channel 10.0 -InstallDir .\.dotnet
```

`.dotnet/`은 `.gitignore`에 포함되어 있습니다.

### 2. 빌드

```powershell
.\build.ps1            # Debug 빌드
.\build.ps1 -Test      # 빌드 + 테스트
.\build.ps1 -Release   # Release 빌드
```

저장소에 `.dotnet/dotnet.exe`가 있으면 자동으로 그것을 사용하고, 없으면 PATH의 `dotnet`을 사용합니다.

### 3. 실행

```powershell
.\run-app.ps1                       # WPF UI
.\run-cli.ps1 list "사진.zip"       # CLI
```

### 4. 배포용 포터블 빌드

```powershell
.\publish.ps1            # 자체 포함 (권장, .NET 설치 불필요)
.\publish.ps1 -Zip       # ZIP까지 생성
```

결과는 `artifacts/WangZip-0.6.1-win-x64/`에 생성됩니다.

- `WangZip.exe` — GUI
- `cli/wangzip.exe` — CLI (Windows는 대소문자를 구분하지 않아 `WangZip.exe`와 같은 폴더에 둘 수 없습니다)

.NET 10 데스크톱 런타임이 이미 설치된 PC용으로 작게 만들려면 `-FrameworkDependent`를 붙입니다.

### 5. 설치 프로그램 빌드 (Inno Setup)

```powershell
.\installer\build-installer.ps1
```

`artifacts/WangZip-0.6.1-setup.exe`가 만들어집니다 (약 70MB, 한국어 설치 마법사).
배포본 폴더가 있으면 그대로 쓰고(`-SkipPublish`로 재사용), 없으면 자동으로 `publish.ps1`을 먼저 실행합니다.

사전 준비 — Inno Setup 7을 `.tools\inno`에 사용자 단위로 설치:

```powershell
curl.exe -sSL -o innosetup.exe "https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe"
powershell -c "& .\innosetup.exe /VERYSILENT /CURRENTUSER /DIR=\"$PWD\.tools\inno\""
```

---

## CLI 사용법

```text
wangzip list    <압축파일> [--encoding auto|utf8|cp949|raw] [--json] [--password 비밀번호]
wangzip extract <압축파일> [대상폴더] [--encoding ...] [--overwrite overwrite|skip|rename] [--select 이름1,이름2] [--password 비밀번호]
wangzip create  <출력.zip> <파일|폴더>... [--level none|fastest|optimal|smallest] [--encoding utf8|cp949] [--comment "설명"]
wangzip verify  <압축파일> [--password 비밀번호]
```

예시:

```powershell
# 목록 보기 (파일명 인코딩 자동 감지)
.\run-cli.ps1 list "C:\Download\자료.zip"

# CP949로 만들어진 구형 ZIP을 한국어 이름으로 풀기
.\run-cli.ps1 extract "자료.zip" "C:\Temp\자료" --encoding cp949

# 폴더를 ZIP으로 만들기
.\run-cli.ps1 create "백업.zip" "C:\작업" --level optimal

# 무결성(CRC) 검사
.\run-cli.ps1 verify "백업.zip"
```

`--json`을 붙이면 다른 도구에서 처리하기 쉬운 형태로 출력합니다.

---

## GUI

- **열기**: `Ctrl+O`, 또는 파일을 창으로 드래그
- **압축 풀기**: `Ctrl+E`, 선택 항목만 풀거나 전체 풀기
- **압축하기**: `Ctrl+N`, 파일/폴더 추가 후 저장 위치·압축 수준·파일명 인코딩 선택
- **미리보기**: 목록에서 파일을 더블클릭하면 임시 폴더에 풀어 기본 앱으로 엽니다
- **파일명 인코딩**: 상단 콤보박스에서 `자동 / UTF-8 / CP949 / 원본 바이트` 전환
- **새로 고침**: `F5`

---

## 한글 파일명 처리 (핵심)

ZIP은 파일명 인코딩이 통일되어 있지 않아 한국에서 특히 문제가 됩니다. WangZip은 ZIP 중앙 디렉터리를
직접 파싱해 **파일명 원본 바이트**를 확보한 뒤 다음 순서로 판별합니다.

1. 유니코드 경로 추가 필드(`0x7075`, CRC 검증) — 가장 신뢰도 높음
2. 일반 목적 플래그 bit 11 (UTF-8) — ZIP 표준
3. ASCII
4. 엄격한 UTF-8 해석 결과에 한글이 포함되면 UTF-8
5. 엄격한 CP949(EUC-KR 상위 집합) 해석 결과에 한글이 포함되면 CP949
6. 위가 모두 실패하면 UTF-8 → CP949 → CP437 순으로 대체

추가로 **NFC 정규화**를 수행해 macOS에서 만든 ZIP의 자모 분리(NFD) 한글도 정상 표시합니다.
자동 판별이 틀리면 UI에서 인코딩을 직접 바꿀 수 있습니다.

압축할 때는 기본이 **UTF-8 + 플래그**이며, 구형 Windows 프로그램과의 호환을 위해 **CP949**를 선택할 수 있습니다.

---

## 안전 장치

압축 파일은 신뢰할 수 없는 입력입니다. 추출 전후로 다음을 검사합니다.

- **zip-slip 차단**: `..`, 절대 경로, 드라이브 문자를 정규화하고 최종 경로가 대상 폴더 안인지 확인
- **Windows 예약 이름**: `CON`, `NUL`, `COM1`… → `_` 접두사
- **ADS/특수문자**: `:`, `<`, `>`, `|`, `?`, `*` 등 치환
- **후행 점/공백** 제거, **대소문자 충돌** 자동 리네임
- **긴 경로**: 248자 이상이면 `\\?\` 확장 경로 사용
- **CRC 검증**: 추출한 모든 ZIP 항목의 CRC를 계산해 불일치 시 실패 처리
- **원자적 쓰기**: 임시 파일에 쓴 뒤 성공 시에만 최종 이름으로 이동 (취소/실패 시 잔여 파일 없음)
- **여유 공간 사전 확인**
- **압축 폭탄 완화**: 항목별 CRC/크기 검증, 실패 시 즉시 중단
- **심볼릭 링크 항목**은 기본적으로 건너뜀

---

## 지원 형식

| 형식 | 읽기 | 쓰기 | 비고 |
|---|---|---|---|
| ZIP | ✅ | ✅ | ZIP64, UTF-8/CP949 파일명, CRC 검증 |
| 7z | ✅ | ❌ | SharpCompress로 읽기 |
| TAR | ✅ | ❌ | |
| GZ / TGZ | ✅ | ❌ | 압축 스트림 안의 TAR도 읽기 (`.tar.gz`/`.tar.bz2`/`.tar.xz`) |
| BZ2 | ✅ | ❌ | |
| XZ | ✅ | ❌ | |
| RAR | ✅ | ❌ | SharpCompress 읽기 (분할 RAR은 미지원) |
| ISO / CAB / LHA | ✅ | ❌ | SharpCompress 지원 범위 |

> RAR 쓰기는 공개된 오픈소스 구현이 없어 지원 계획이 없습니다.

---

## 제한 사항

현재 버전에서 **의도적으로 구현하지 않은** 것들입니다. README에 먼저 적어 두는 편이
사용자 기대를 맞추는 데 낫다고 판단했습니다.

| 암호가 걸린 압축 파일 풀기 — **지원** (ZIP ZipCrypto/WinZip AES, 7z/다른 형식은 라이브러리 경로로)
  - 압축 파일 **생성 시 암호 걸기 — 지원** (WinZip AES-256 / AE-2, `wangzip create --password`)
- 분할 ZIP(`.z01`, `.001`), 분할 7z, 분할 RAR 읽기
- 7z/TAR 쓰기
- 탐색기 우클릭 메뉴 — **고전 메뉴 지원** ("여기에 풀기", "이 폴더 압축" / Win11은 "여러 항목 표시" 아래) (Win11 새 메뉴는 다음 과제)
- 압축 파일에서 파일을 직접 드래그해 꺼내기 (셸 네임스페이스 확장)
- ZIP 항목 추가/삭제의 데이터 디스크립터·ZIP64 형식 지원
- ALZ/EGG 등 알집 고유 포맷 (연구 과제)

알려진 제약:

- 4GB 이상 ZIP64는 코드상 지원하지만 대용량 실측 테스트가 아직 부족합니다.
- 암호 ZIP 생성은 4GB 이상 항목·압축 파일과 65,535개 초과 항목을 아직 지원하지 않습니다.
- Windows 10은 Microsoft 지원 종료(2025-10) 이후이므로 Windows 11을 기준으로 검증합니다.

---

## 프로젝트 구조

```text
src/
  WangZip.Core/          압축 엔진 (ZIP 직접 파서, 인코딩 판별, 경로 안전, 7z/TAR/RAR 읽기)
  WangZip.Cli/           CLI (wangzip)
  WangZip.App/           WPF UI
installer/
  wangzip.iss            Inno Setup 설치 프로그램 정의
tests/
  WangZip.Core.Tests/    xUnit 테스트 52개
docs/
  DESIGN.md             설계 결정과 로드맵
```

핵심 파일:

- `src/WangZip.Core/Zip/ZipStructures.cs` — EOCD/ZIP64/중앙 디렉터리 파서
- `src/WangZip.Core/Zip/ZipNameDecoder.cs` — 한글 파일명 판별
- `src/WangZip.Core/Zip/ZipArchiveSource.cs` — 추출/CRC 검증
- `src/WangZip.Core/Safety/PathGuard.cs` — 경로 안전 계층
- `src/WangZip.Core/Zip/ZipWriter.cs` — ZIP 생성
- `src/WangZip.Core/Zip/EncryptedZipWriter.cs` — WinZip AES-256 암호 ZIP 생성
- `src/WangZip.Core/SharpCompressSupport/ReaderArchiveSource.cs` — `.tar.gz` 등 중첩 압축 읽기
- `src/WangZip.App/MainWindow.xaml` — 메인 UI

---

## 테스트

```powershell
.\.dotnet\dotnet.exe test .\tests\WangZip.Core.Tests\WangZip.Core.Tests.csproj
```

포함된 테스트:

- CP949 / UTF-8 / 유니코드 추가 필드 / macOS NFD 한글 판별
- zip-slip 거부, 예약 장치명, ADS, 대소문자 충돌, 긴 경로
- 생성→목록→추출 라운드트립, 빈 폴더 보존, 덮어쓰기 정책, CRC 손상 감지
- `.tar.gz` 중첩 압축 목록/추출
- 암호화 ZIP: PKWARE ZipCrypto(STORE/DEFLATE), WinZip AES-128/256 라운드트립,
  잘못된 비밀번호 거부, 암호 항목 무결성 검사
- AES-256 생성: 한글 이름·한글 비밀번호·빈 파일·STORE/DEFLATE 라운드트립, AE-2 구조 검증
- 외부 호환: python zipfile이 왕집이 만든 ZipCrypto 파일을 읽고,
  pyzipper(7-Zip 호환 AE-2)가 만든 AES ZIP을 왕집이 풀 수 있음.
  반대로 왕집이 만든 AES ZIP을 pyzipper가 풀 수 있음(수동 검증).

---

## 로드맵

| 버전 | 내용 |
|---|---|
| v0.1 | ZIP 읽기/쓰기, 한글 파일명, CLI, WPF UI, 안전 계층, 설치형 설치 프로그램 |
| v0.2 | 암호 걸린 압축 풀기(ZIP ZipCrypto/WinZip AES, 7z), 크래시 수정, 잠금/정리 보강 |
| v0.3 | 리본형 도구 모음, 폴더 트리, 압축 풀기 흐름, ZIP 항목 추가/삭제, 탐색기 우클릭 메뉴(고전) |
| v0.4 | 압축 풀기 대상 콤보(대화상자 이동), 레이아웃/콤보 정렬 수정, 자동 업데이트 |
| v0.5 | 암호 걸어 ZIP 만들기(AES-256), CLI `--password` |
| v0.6 | 폴더 더블클릭 탐색·트리 수정, 폴더 항목 기록, 도구 메뉴 정리, 새 아이콘 |
| v0.6.1 (현재) | 정렬·검색·끌어서 내보내기·우클릭 메뉴·형식별 아이콘, 폴더 선택 풀기 버그 수정 |
| 다음 | Win11 새 콘텍스트 메뉴, 7z/TAR 쓰기, 분할 ZIP, 드래그 아웃 |

자세한 설계 배경과 검토 결과는 [docs/DESIGN.md](docs/DESIGN.md)를 참고하세요.
버전별 변경 내용은 [CHANGELOG.md](CHANGELOG.md)에 정리되어 있습니다.

---

## 기여

이슈와 PR을 환영합니다. 특히 다음 분야의 도움이 필요합니다.

- 실제 국내 ZIP/RAR 샘플로 인코딩·분할 압축 테스트
- 대용량(4GB+) 및 손상 파일 테스트
- Windows 11 셸 확장 구현 경험

버그 신고 시 압축 파일의 **생성 도구와 인코딩**을 함께 알려주시면 큰 도움이 됩니다.
(민감한 파일은 공유하지 마세요.)

---

## 라이선스

GPL-3.0. 전문은 [LICENSE](LICENSE)를 참고하세요.
제3자 구성요소 고지는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)에 있습니다.
