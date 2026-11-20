# WangZip (왕집) 인수인계서

## 최신 인수인계 (2026-11-20)

### 현재 릴리스와 최우선 확인 사항
- 최신 공개 릴리스는 [v0.8.3](https://github.com/karanglass24-lang/WangZip/releases/tag/v0.8.3)입니다. 원격 `main`의 릴리스 커밋은 `70d7ef3`이며, Windows x64 자체 포함 설치형 EXE와 포터블 ZIP을 첨부했습니다. 별도 작업 공간 `C:\Users\i9\AppData\Local\Temp\opencode\wangzip-release-0.8.3`에서 새 산출물을 만들었고, Release 빌드 경고·오류 0개, 테스트 65개 통과, CLI 버전 0.8.3을 확인했습니다.
- v0.8.3에 목록 범위 선택/다중 드래그, 작업별 임시 추출 캐시, 탐색기 메뉴 등록 및 설치 프로그램 정리, 다크 메뉴의 일부 글자색 변경이 포함됐습니다. 실제 탐색기·WPF 동작은 검증되지 않았습니다.
- **v0.8.3 배포 후 사용자가 다크 테마에서 밝은 하위 메뉴의 글자가 거의 보이지 않는다고 스크린샷과 함께 보고했습니다.** 로컬 `D:\llm\zip\src\WangZip.App\Theme\Theme.xaml`에서 전역 `TextBlock` 글자색 스타일을 제거해 메뉴 템플릿의 제목과 단축키가 메뉴 항목 글자색을 상속하게 했습니다. 기본 팝업은 시스템 메뉴 글자색을 사용하고, 상단 메뉴는 마우스 오버·열림에도 앱 테마 글자색을 유지합니다. 최신 로컬 `.\build.ps1 -Test`는 경고·오류 0개, 테스트 65개 통과입니다. **수정은 미커밋·미출시이며 실제 화면 재검증이 필요합니다.**
- 먼저 Windows 다크/라이트 및 앱 시스템/다크/라이트 조합에서 파일·도구·도움말, 최근 파일의 중첩 메뉴, 목록 우클릭 메뉴, 단축키 표시, 마우스/키보드 강조, 비활성 항목, 런타임 전환을 확인하세요. 전역 스타일 제거로 툴바·목록·대화상자 텍스트에 영향이 없는지도 확인합니다.
- 그다음 Ctrl+A 후 파일/폴더 드롭, 첫 복사 중 두 번째 드래그, 준비 중 버튼 놓기·재누르기, 셸 메뉴 옵션을 끈 채 업그레이드와 제거를 확인하세요. 드래그 캐시는 기본 8 GiB 예약 한도와 최근 2일 보호 정책을 사용합니다. 잘못된 크기 메타데이터가 있으면 추출 중 일시적으로 예약량을 초과할 수 있고 2일이 넘는 Explorer 복사는 보호를 보장하지 않습니다.
- 원래 `D:\llm\zip` 작업 공간의 로컬 `main`은 릴리스 작업과 병합하지 않았습니다. 기존 미커밋·미추적 파일에는 이미 배포된 변경도 섞여 있으므로, `git status`와 `origin/main`을 대조하고 최신 테마 수정만 분리하세요. 일괄 리셋·덮어쓰기·재커밋을 피하고 새 릴리스는 요청받을 때만 진행합니다.

## 이전 인수인계 (v0.8.3 공개 전, 2026-11-19)

> 아래의 기존 문서는 과거 기록입니다. 버전, 작업 우선순위, 분할 ZIP 설계 및 릴리스 명령은 오래되었거나 잘못되었으므로 그대로 실행하지 마세요.

### 릴리스와 작업 공간
- [v0.8.2](https://github.com/karanglass24-lang/WangZip/releases/tag/v0.8.2)가 게시되었습니다. 텍스트 미리보기, 최근 파일·폴더, 테마 및 관련 수정이 포함되며 설치형 EXE와 포터블 ZIP이 첨부되어 있습니다.
- 릴리스 커밋은 `a03b1ff`이며 원격 `main`에 반영되었습니다. 별도 작업 공간 `C:\Users\i9\.local\share\opencode\worktree\401780\wangzip-release-0.8.2`에서 제작했습니다.
- 원래 작업 공간 `D:\llm\zip`의 로컬 `main`은 릴리스 커밋보다 한 커밋 뒤입니다. 상당한 미커밋 변경과 추적되지 않는 미리보기 파일 및 이 문서가 있으므로 덮어쓰거나 일괄 되돌리지 마세요. 작업 전 `git status`와 원격 상태를 다시 확인하고 릴리스 내용과 신중하게 대조하세요.
- 분할 ZIP 읽기와 별도의 폴더 드래그 변경은 v0.8.2에 포함되지 않았습니다.

### 진행 중: 목록 선택과 탐색기 드래그
- 사용자가 요청한 문제는 압축 목록에서 마우스로 여러 행을 드래그 선택할 수 없는 것과 전체 선택 후 탐색기로 끌 때 한 항목만 선택되는 것입니다.
- 로컬 `MainWindow.xaml` 및 `MainWindow.xaml.cs`에서 이미 선택된 여러 행 중 하나를 누를 때 선택 축소를 마우스 업까지 미루고, 선택 변경 시 드래그 후보를 취소하던 처리를 제거했습니다. `FileDrop`에 파일뿐 아니라 폴더도 허용합니다.
- 로컬 `ViewModels/MainViewModel.cs`의 `ExtractToTemp`는 선택한 상위 폴더를 전달하고 중복되는 자손 경로는 제외하도록 변경되었습니다. 이 수정은 미커밋이며 미출시입니다.
- **완료 판정 전** 실제 WPF에서 마우스 드래그 행 선택, Ctrl/Shift 선택, 선택된 행의 클릭과 드래그, Ctrl+A 후 탐색기 드롭을 확인해야 합니다. 파일·명시적/암묵적 폴더·빈 폴더·혼합 선택도 검증하세요.
- 임시 추출을 비동기로 마친 뒤 `DragDrop.DoDragDrop`을 호출하므로, 추출 중 마우스 버튼을 놓거나 큰 파일을 끌 때 드래그가 유지되는지 확인하고 필요하면 수정하세요. 기존 변경만으로 마우스 드래그 범위 선택이 구현되었다고 가정하지 마세요.

### 진행 중: 탐색기 우클릭 메뉴
- 요청: 압축 파일 우클릭 메뉴의 왕집 명령을 `왕집` 하위에 모으고, 열리지 않는 `이 폴더 압축` 하위 메뉴를 수정합니다. Windows 11에서는 클래식 메뉴인 **더 많은 옵션 표시**를 사용합니다.
- 로컬 `src/WangZip.App/ShellIntegration.cs`와 `installer/wangzip.iss`에서 상위 메뉴 이름과 `ExtendedSubCommandsKey` 참조를 수정하고 앱과 설치 프로그램의 등록 키를 맞췄습니다. 기존 독립 메뉴 키 정리 및 일반 파일 메뉴의 압축 파일 확장자 제외도 추가했습니다.
- 변경 사항은 **미커밋·미출시·미설치** 상태입니다. 업데이트된 앱에서 셸 메뉴 환경설정을 저장하거나 업데이트된 설치 프로그램을 실행해야 등록이 반영됩니다.
- 실제 탐색기 검증은 아직 없습니다. 압축 파일, 일반 파일, 폴더, 폴더 배경 각각에서 메뉴 중복·하위 메뉴 열기·명령 실행을 확인하고, 이전 등록이 있는 상태에서 업그레이드 및 메뉴 끄기도 검증하세요. 일반 파일 필터가 실제로 압축 파일 중복을 막는지도 확인해야 합니다.

### 진행 중: 다크 테마 메뉴
- 로컬 `src/WangZip.App/Theme/Theme.xaml`에 최상위 메뉴와 밝은 배경의 하위 메뉴 글자색 수정이 있습니다. 실제 WPF에서 다크/라이트 전환과 기본·마우스 오버·열림·비활성화·컨텍스트 메뉴 상태는 아직 검증되지 않았습니다.

### 검증 및 다음 작업
- 마지막 로컬 `.\build.ps1 -Test`: 빌드 경고 0개, 오류 0개, 기존 코어 테스트 61개 통과. 이 테스트는 WPF 마우스 동작이나 실제 탐색기 드롭을 검증하지 않습니다.
- Inno Setup 테스트 스크립트 컴파일도 성공했지만 기존 `artifacts\WangZip-0.8.1-win-x64` 바이너리를 사용했습니다. 생성된 테스트 설치 파일은 새 배포본이 아니므로 릴리스에 사용하지 마세요.
- 위 GUI 시나리오를 검증하고 필요한 수정과 회귀 테스트를 추가하세요. 새 릴리스는 사용자가 요청할 때만 진행하세요.
- 분할 ZIP은 향후 별도 작업입니다. 아래 과거 설계의 **bit 3을 분할 판별에 사용하는 주장**, 비탐색 순차 연결 스트림만으로 충분하다는 주장, 예시 API와 생성 명령은 신뢰하지 마세요. bit 3은 데이터 디스크립터 사용을 뜻합니다. ZIP의 디스크 번호와 볼륨별 오프셋, ZIP64 및 실제 도구가 생성한 샘플을 명세에 따라 별도로 검토해야 합니다.

---

## 과거 문서 (참고용, 최신 상태 아님)

> **작성일**: 2026-11-18  
> **현재 버전**: v0.8.1  
> **GitHub**: https://github.com/karanglass24-lang/WangZip (main 브랜치 최신)

---

## 📋 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **목표** | Windows 11 우선, .NET 10 / WPF 기반 오픈소스 압축 유틸리티 |
| **라이선스** | GPL-3.0 |
| **배포 방식** | Self-contained (런타임 불필요) |
| **언어** | 한국어/영어 완전 로컬라이즈 |
| **Explorer 연동** | 클래식 우클릭 메뉴 + Win11 "자세히 보기" 지원 |

---

## ✅ 완료된 작업 (Phase 1: 5개 기능)

### 1. 드래그 앤 드롭 폴더 구조 유지
- **파일**: `src/WangZip.App/ViewModels/MainViewModel.cs` (`ExtractToTemp`)
- **내용**: 압축 내부 폴더 계층을 임시 폴더에 그대로 재현, Explorer 드롭 시 폴더 트리 정상 생성
- **핵심**: 디렉터리 → 파일 순서, 깊이 순(부모→자식) 정렬로 반환

### 2. 텍스트 파일 미리보기 (60+ 확장자 지원)
- **신규**: `src/WangZip.App/Dialogs/PreviewDialog.xaml/.cs`
- **기능**: 
  - 인코딩 자동 감지 (UTF-8/16 BOM, UTF-8 유효성, CP949 휴리스틱)
  - 수동 인코딩 선택 (UTF-8, UTF-16, CP949, EUC-KR, ISO-8859-1)
  - 자동 줄바꿈, 줄/열 번호 표시, 외부 프로그램으로 열기
- **연결**: `MainWindow.xaml.cs` 더블클릭 시 텍스트 확장자면 즉시 PreviewDialog 표시

### 3. 검색 결과에서 추출/삭제
- **상태**: **별도 구현 불필요** (기존 `EntryGrid.SelectedItems` 기반 컨텍스트 메뉴가 검색 결과에서도 그대로 동작 확인)

### 4. 최근 파일/폴더 메뉴 (최대 10개)
- **파일**: `Settings.cs` (`RecentPaths`, `AddRecentPath`), `MainWindow.xaml/.cs` (메뉴 동적 생성)
- **동작**: 파일 열기/압축 생성 시 자동 기록, "파일 → 최근 파일" 서브메뉴로 진입

### 5. 다크 모드 대응 (시스템/라이트/다크 3모드)
- **파일**: `Theme.xaml` (라이트/다크 브러시 쌍), `App.xaml.cs` (`ApplyTheme`, 시스템 테마 감지), `MainWindow.xaml/.cs` (메뉴 연동)
- **적용**: 런타임 리소스 교체로 즉시 반영, 재시작 불필요

---

## 🏗️ 아키텍처 핵심 포인트

### 프로젝트 구조
```
src/
├── WangZip.Core/           # 핵심 라이브러리 (테스트 61개 통과)
│   ├── ArchiveService.cs   # 포맷 감지, 팩토리
│   ├── Zip/                # 커스텀 ZIP 구현 (SharpCompress 미사용)
│   │   ├── ZipArchiveSource.cs    # 핵심 읽기 로직
│   │   ├── ZipStructures.cs       # 헤더 구조체
│   │   └── ZipRewriter.cs         # 쓰기/수정/삭제
│   ├── SevenZip/           # SharpCompress 래핑
│   ├── Rar/                # SharpCompress 래핑
│   ├── Tar/                # SharpCompress 래핑
│   └── IArchiveSource.cs   # 공통 인터페이스
├── WangZip.App/            # WPF UI (.NET 10 windows)
│   ├── ViewModels/MainViewModel.cs   # UI 로직 집중
│   ├── MainWindow.xaml/.cs           # 메인 창
│   ├── Dialogs/                    # 각종 다이얼로그
│   └── Theme/Theme.xaml            # 테마 리소스
├── WangZip.Cli/            # CLI (System.CommandLine)
└── WangZip.Core.Tests/     # xUnit 테스트
```

### ZIP 읽기 흐름 (현재 단일 볼륨만 지원)
```
ArchiveService.DetectFormat() → ArchiveFormat.Zip
    → ZipArchiveSource.Open(Stream)
        → ReadLocalFileHeaders() / ReadCentralDirectory()
        → Entry[] 반환
```

---

## 🎯 다음 작업: Step 6 — 분할 압축 읽기 지원 (진행 예정)

### 목표
`.z01/.z02/...` (PKZIP) 또는 `.001/.002/...` (7-Zip/WinRAR) 형태의 **분할 ZIP 읽기** 지원

### 구현 계획

#### 1. 분할 ZIP 감지 (`ZipArchiveSource.Open`)
- 첫 볼륨(`.zip` 또는 `.z01`/`.001`) 열 때 **분할 시그니처** 확인
  - Local file header의 `generalPurposeBitFlag` bit 3 (0x08) = 데이터 디스크립터 사용
  - Central directory가 **첫 볼륨에만** 있거나 **마지막 볼륨에만** 있을 수 있음
- 이후 볼륨 파일 존재 여부 스캔 (`.z01`~`.z99`, `.001`~`.999`)

#### 2. `SplitZipReader` 클래스 신규 작성
```csharp
// 개념적 인터페이스
public sealed class SplitZipReader : IArchiveSource
{
    private readonly string[] _volumes;  // 정렬된 볼륨 경로들
    private int _currentVolumeIndex;
    private Stream _currentStream;
    
    // 순차 읽기: 현재 볼륨 끝 → 다음 볼륨 자동 전환
    public override Entry[] ReadEntries();
    public override Stream OpenEntry(Entry entry);
    public override void Extract(int[] indices, ExtractionOptions opts, ...);
}
```

#### 3. `ArchiveService.DetectFormat()` 확장
- 확장자 `.z01`, `.001` 등도 `ArchiveFormat.Zip`으로 인식
- 첫 볼륨(`.zip`)이 없어도 `.z01`/`.001`로 열기 시도

#### 4. CLI 지원 (`Program.cs`)
- `wangzip list archive.z01` → 자동으로 분할 세트 인식
- `wangzip extract archive.z01 -o out/` → 동일

#### 5. WPF UI 연동
- `MainViewModel.OpenAsync`에서 분할 세트 자동 감지 → `SplitZipReader` 사용

### 분할 ZIP 포맷 핵심 사항
| 구분 | PKZIP 방식 | 7-Zip/WinRAR 방식 |
|------|------------|-------------------|
| **확장자** | `.zip`, `.z01`, `.z02`... | `.zip`, `.001`, `.002`... |
| **중앙 디렉터리** | 첫 볼륨 또는 마지막 볼륨 | 마지막 볼륨 (`.zip`) |
| **로컬 헤더** | 각 볼륨마다 있음 | 각 볼륨마다 있음 |
| **데이터 디스크립터** | 필수 (bit 3 = 1) | 보통 사용 |

### 참고: SharpCompress 한계
- **분할 ZIP 미지원** → 커스텀 구현 필수 (기존 `ZipArchiveSource` 확장 권장)
- 비분할 ZIP, 7z, RAR, TAR은 SharpCompress 정상 사용 중

---

## 🔧 개발 환경 & 빌드

```powershell
# 빌드 + 테스트 (필수 확인)
.\build.ps1 -Test

# 앱 실행
.\run-app.ps1

# CLI 실행
.\run-cli.ps1 --help

# 게시 (self-contained, 단일 파일)
.\publish.ps1
```

### 현재 빌드 상태
- ✅ **경고 0개, 오류 0개**
- ✅ **테스트 61개 전부 통과**
- ✅ GitHub Actions CI 통과 (main 브랜치)

### 버전 관리
- `Directory.Build.props` / 각 `.csproj` / `.iss` (Inno Setup) / `publish.ps1` 모두 **0.8.1**로 동기화됨
- 다음 릴리스: **0.8.2** (분할 ZIP 읽기 추가 시)

---

## 🐛 알려진 이슈 / 주의사항

| 이슈 | 상태 | 비고 |
|------|------|------|
| 분할 ZIP 쓰기(생성) | 미구현 | 읽기만 우선, 쓰기는 Phase 2 이후 |
| 암호 분할 ZIP | 미지원 | 읽기 구현 후 별도 고려 |
| 4GB+ 분할 ZIP (ZIP64) | 미검증 | `ZipStructures.cs`에 ZIP64 필드 있음, 테스트 필요 |
| 유니코드 파일명 (분할) | 미검증 | 인코딩 모드(`NameEncodingMode`) 동일 적용 예정 |

---

## 📝 인수인계 체크리스트 (다음 담당자)

- [ ] `ZipArchiveSource.cs` 열어서 현재 단일 볼륨 읽기 로직 파악
- [ ] `ZipStructures.cs`의 헤더 구조체(특히 `GeneralPurposeBitFlag`) 이해
- [ ] 테스트용 분할 ZIP 샘플 준비 (`.z01`, `.001` 양 방식)
- [ ] `SplitZipReader` 구현 시작 (핵심: 스트림 체이닝, 엔트리 오프셋 계산)
- [ ] 단위 테스트 추가 (`WangZip.Core.Tests`)

---

## 🔗 참고 자료

- [PKWARE APPNOTE (ZIP 포맷 스펙)](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT)
- [SharpCompress 소스](https://github.com/adamhathcock/sharpcompress) — 분할 ZIP 미지원 확인용
- 기존 커밋 히스토리: `git log --oneline -20`

---

## 🚀 Step 6 상세 구현 가이드 (분할 ZIP 읽기)

### 6-1. 핵심 파일별 변경 사항

#### A. `src/WangZip.Core/Zip/ZipStructures.cs` — 시그니처/플래그 상수 추가
```csharp
// 분할 ZIP 시그니처
public const uint SplitArchiveMarker = 0x08074B50; // 데이터 디스크립터
public const uint SpannedArchiveMarker = 0x30304B50; // spanned marker (PKZIP 4.5+)

// General Purpose Bit Flag 비트 정의
[Flags]
public enum GeneralPurposeBitFlag : ushort
{
    None = 0,
    Encrypted = 0x0001,
    CompressionOption1 = 0x0002,
    CompressionOption2 = 0x0004,
    DataDescriptor = 0x0008,     // ← 분할 ZIP에서 필수
    EnhancedDeflating = 0x0010,
    PatchedData = 0x0020,
    StrongEncryption = 0x0040,
    Utf8Names = 0x0800,
    // ...
}
```

#### B. `src/WangZip.Core/Zip/SplitZipReader.cs` — 신규 작성
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WangZip.Core.Zip;

namespace WangZip.Core.Zip;

/// <summary>
/// 분할 ZIP(.z01/.z02... 또는 .001/.002...)을 순차 읽기하는 래퍼.
/// 첫 볼륨만 열면 나머지 볼륨은 자동으로 탐색되어 체인됩니다.
/// </summary>
public sealed class SplitZipReader : IArchiveSource
{
    private readonly string[] _volumePaths;  // 정렬된 볼륨 경로 배열
    private readonly List<Stream> _volumeStreams = new();
    private ZipArchiveSource? _innerSource;   // 기존 단일 볼륨 로직 재사용

    public SplitZipReader(string firstVolumePath)
    {
        _volumePaths = DiscoverVolumes(firstVolumePath);
        if (_volumePaths.Length == 0)
            throw new FileNotFoundException("분할 볼륨을 찾을 수 없습니다.", firstVolumePath);
    }

    /// <summary>분할 볼륨 세트를 자동 탐색합니다.</summary>
    private static string[] DiscoverVolumes(string firstPath)
    {
        var dir = Path.GetDirectoryName(firstPath)!;
        var name = Path.GetFileNameWithoutExtension(firstPath);
        var ext = Path.GetExtension(firstPath).ToLowerInvariant();

        var result = new List<string>();

        // Case 1: 첫 파일이 .z01 → .z02, .z03... 탐색
        if (ext.StartsWith(".z") && ext.Length == 4 && char.IsDigit(ext[2]))
        {
            result.Add(firstPath);
            for (int i = 2; i < 100; i++)
            {
                var next = Path.Combine(dir, $"{name}.z{i:D2}");
                if (File.Exists(next)) result.Add(next);
                else break;
            }
            // 마지막 .zip도 포함
            var zip = Path.Combine(dir, $"{name}.zip");
            if (File.Exists(zip)) result.Add(zip);
        }
        // Case 2: 첫 파일이 .001 → .002, .003... 탐색
        else if (ext == ".001")
        {
            result.Add(firstPath);
            for (int i = 2; i < 1000; i++)
            {
                var next = Path.Combine(dir, $"{name}.{i:D3}");
                if (File.Exists(next)) result.Add(next);
                else break;
            }
            var zip = Path.Combine(dir, $"{name}.zip");
            if (File.Exists(zip)) result.Add(zip);
        }
        // Case 3: 첫 파일이 .zip → .z01/.001이 있으면 분할 세트로 간주
        else if (ext == ".zip")
        {
            result.Add(firstPath);
        }
        // 단일 볼륨
        else
        {
            result.Add(firstPath);
        }

        return result.ToArray();
    }

    public void Open(string path)
    {
        // 각 볼륨을 순차적으로 열어서 스트림 체인 구성
        foreach (var volPath in _volumePaths)
        {
            if (!File.Exists(volPath)) continue;
            _volumeStreams.Add(new FileStream(volPath, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        // 첫 볼륨으로 기존 ZipArchiveSource 초기화
        // (중앙 디렉터리가 첫 볼륨에 있는 경우 처리)
        _innerSource = new ZipArchiveSource();
        _innerSource.Open(CombineStreams(_volumeStreams));
    }

    private static Stream CombineStreams(List<Stream> streams)
    {
        // 간단한 순차 읽기 체인 (실제로는 더 정교한 구현 필요)
        // ZIP 리더는 각 엔트리의 compressedSize만큼만 읽으므로
        // Stream 체이닝으로 충분
        return new ConcatStream(streams);
    }

    public ArchiveEntry[] ReadEntries() => _innerSource?.ReadEntries() ?? Array.Empty<ArchiveEntry>();

    public Stream OpenEntry(ArchiveEntry entry) => _innerSource!.OpenEntry(entry);

    public void Extract(int[] indices, ExtractionOptions options, IProgress<ProgressReport>? progress = null, CancellationToken token = default)
        => _innerSource?.Extract(indices, options, progress, token)
           ?? throw new InvalidOperationException("먼저 Open()을 호출하세요.");

    public void Dispose()
    {
        _innerSource?.Dispose();
        foreach (var s in _volumeStreams)
        {
            try { s.Dispose(); } catch { }
        }
    }
}

/// <summary>여러 Stream을 순차적으로 읽는 체인 스트림</summary>
internal sealed class ConcatStream : Stream
{
    private readonly List<Stream> _streams;
    private int _currentIndex;
    private long _totalRead;

    public ConcatStream(List<Stream> streams)
    {
        _streams = streams;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        while (_currentIndex < _streams.Count)
        {
            var s = _streams[_currentIndex];
            int read = s.Read(buffer, offset, count);
            if (read > 0)
            {
                _totalRead += read;
                return read;
            }
            _currentIndex++; // 현재 볼륨 끝 → 다음 볼륨
        }
        return 0; // 모든 볼륨 읽음
    }

    public override long Length => _streams.Sum(s => s.Length);
    public override long Position
    {
        get => _totalRead;
        set => throw new NotSupportedException("ConcatStream은 순차 읽기만 지원합니다.");
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
```

#### C. `src/WangZip.Core/ArchiveService.cs` — 분할 감지 추가
```csharp
public static ArchiveFormat DetectFormat(string path)
{
    var ext = Path.GetExtension(path).ToLowerInvariant();

    // 분할 ZIP 확장자 인식
    if (ext == ".z01" || ext == ".z02" /* ... */ || 
        (ext.Length == 4 && ext.StartsWith(".z") && char.IsDigit(ext[2])) ||
        ext == ".001" || ext == ".002" /* ... */)
    {
        return ArchiveFormat.Zip; // 분할 ZIP
    }

    // 기존 로직...
}

public static IArchiveSource CreateSource(string path, ...)
{
    var format = DetectFormat(path);
    if (format == ArchiveFormat.Zip && IsSplitArchive(path))
    {
        return new SplitZipReader(path);
    }
    // 기존 단일 볼륨 처리...
}

private static bool IsSplitArchive(string path)
{
    var dir = Path.GetDirectoryName(path)!;
    var name = Path.GetFileNameWithoutExtension(path);
    var ext = Path.GetExtension(path).ToLowerInvariant();

    // .z01 패턴
    if (ext.StartsWith(".z") && ext.Length == 4 && char.IsDigit(ext[2]))
    {
        return File.Exists(Path.Combine(dir, $"{name}.zip")) ||
               File.Exists(Path.Combine(dir, $"{name}.z02"));
    }
    // .001 패턴
    if (ext == ".001")
    {
        return File.Exists(Path.Combine(dir, $"{name}.002")) ||
               File.Exists(Path.Combine(dir, $"{name}.zip"));
    }
    return false;
}
```

---

### 6-2. CLI 변경 (`src/WangZip.Cli/Program.cs`)
```csharp
// wangzip list archive.z01 → 자동으로 SplitZipReader 사용
// wangzip extract archive.z01 -o out/ → 동일
// 기존 코드에서 ArchiveService.CreateSource()만 호출하면 되므로
// 별도 변경 불필요 (서비스 계층에서 자동 처리)
```

---

### 6-3. WPF UI 변경 (`MainViewModel.cs`)
```csharp
// OpenAsync() 내부에서 ArchiveService.CreateSource() 호출 시
// 자동으로 SplitZipReader가 선택되므로 별도 변경 불필요
```

---

## 📋 단계별 작업 체크리스트

| 단계 | 작업 | 파일 | 완료 |
|------|------|------|------|
| 1 | 시그니처 상수 정의 | `ZipStructures.cs` | ☐ |
| 2 | `SplitZipReader` + `ConcatStream` 구현 | `Zip/SplitZipReader.cs` (신규) | ☐ |
| 3 | `ArchiveService` 분할 감지 로직 | `ArchiveService.cs` | ☐ |
| 4 | 테스트용 분할 ZIP 샘플 생성 | (수동: 7-Zip으로 분할) | ☐ |
| 5 | 단위 테스트 작성 | `WangZip.Core.Tests/SplitZipTests.cs` | ☐ |
| 6 | 빌드 & 테스트 통과 확인 | `.\build.ps1 -Test` | ☐ |
| 7 | 실제 분할 ZIP으로 수동 테스트 | `.\run-cli.ps1 list test.z01` | ☐ |

---

## 🧪 테스트 시나리오 (Step 6 검증)

### 시나리오 A: PKZIP 방식 (.z01/.z02.../.zip)
```powershell
# 7-Zip으로 분할 생성
7z a -v10m test.zip largefile.dat
# 결과: test.zip, test.z01, test.z02...

# 테스트
.\run-cli.ps1 list test.z01
.\run-cli.ps1 extract test.z01 -o output/
```

### 시나리오 B: 7-Zip 방식 (.zip/.001/.002...)
```powershell
7z a -v10m test.7z largefile.dat
# 또는 WinRAR: .zip → .001, .002...
# 테스트 동일
.\run-cli.ps1 list test.001
```

### 시나리오 C: 단일 볼륨 ZIP (회귀 테스트)
```powershell
.\run-cli.ps1 list normal.zip  # 기존 동작 유지 확인
```

### 시나리오 D: 중앙 디렉터리가 마지막 볼륨에만 있는 경우
- 일부 ZIP 도구는 `.z01`~`.z99`에 데이터만 넣고 `.zip`(마지막)에만 중앙 디렉터리를 저장
- `SplitZipReader`가 모든 볼륨을 순차 읽으므로 자동 처리됨 (검증 필요)

---

## ⚠️ 주의사항 및 알려진 한계

| 항목 | 내용 |
|------|------|
| **스트림 체이닝 성능** | `ConcatStream.Read()`에서 매번 다음 볼륨 확인 → 대용량 시 약간의 오버헤드. 가능하면 `BufferedStream` 래핑 권장 |
| **ZIP64** | 4GB+ 분할 ZIP은 ZIP64 Extra Field 필요. `ZipStructures.cs`에 이미 필드 정의되어 있음 |
| **암호화** | 암호 분할 ZIP은 별도 처리 필요 (Phase 2) |
| **쓰기 지원** | 분할 ZIP 생성/수정은 미구현 (읽기만) |

---

## 🔗 참고 자료 (Step 6 전용)

- [PKWARE APPNOTE 4.4.3 - Split Archives](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT) (분할 ZIP 스펙)
- [7-Zip 분할 압축 포맷](https://www.7-zip.org/reliability.html)
-SharpCompress 이슈 #456 (분할 ZIP 미지원)

---

## 🚀 GitHub 릴리즈 워크플로우

### Step 6 완료 후 릴리즈 절차

```powershell
# 1. 버전 번호 변경 (0.8.1 → 0.8.2)
$files = @(
    "Directory.Build.props",
    "publish.ps1",
    "build/WangZip.iss"
)
# 각 파일에서 "0.8.1" → "0.8.2" 일괄 치환

# 2. 빌드 & 테스트
.\build.ps1 -Test

# 3. 아티팩트 생성
.\publish.ps1 -Zip   # → artifacts/WangZip-0.8.2-win-x64.zip

# 4. Inno Setup으로 설치형 생성
# build\WangZip-0.8.2-setup.exe 생성 (수동 또는 CI)

# 5. 태그 & 릴리즈
git add .
git commit -m "feat: 분할 ZIP 읽기 지원 (Step 6)"
git tag v0.8.2
git push origin main
git push origin v0.8.2

gh release create v0.8.2 `
  artifacts/WangZip-0.8.2-win-x64.zip `
  artifacts/WangZip-0.8.2-setup.exe `
  --title "왕집 v0.8.2" `
  --notes "## v0.8.2 (2026-11-XX)

### 🆕 새로운 기능
- **분할 ZIP 읽기 지원**: .z01/.z02... 및 .001/.002... 분할 압축 파일 열기
- CLI/UI에서 첫 볼륨만 지정하면 자동으로 전체 세트 인식

### 🐛 버그 수정
- (해당 시)

### 📥 다운로드
| 파일 | 설명 |
|------|------|
| WangZip-0.8.2-setup.exe | 설치형 |
| WangZip-0.8.2-win-x64.zip | 포터블 (.NET 불필요) |" `
  --latest
```

---

## 📞 다음 담당자 연락처 / 참고

- **GitHub Issues**: https://github.com/karanglass24-lang/WangZip/issues
- **커밋 히스토리**: `git log --oneline -20`
- **이전 릴리즈 노트**: `gh release view v0.8.1 --repo karanglass24-lang/WangZip`

---

**인수인계서 최종 수정일**: 2026-11-18  
**다음 릴리즈 예정**: v0.8.2 (분할 ZIP 읽기 지원)
