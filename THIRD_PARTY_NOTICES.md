# 제3자 구성요소 고지

WangZip은 아래 오픈소스 소프트웨어를 사용합니다. 각 구성요소의 라이선스는 해당 프로젝트에 귀속됩니다.

## 런타임 의존성

### SharpCompress 0.50.8

- 라이선스: MIT
- 저장소: https://github.com/adamhathcock/sharpcompress
- 용도: 7z, TAR, GZ, BZ2, XZ, RAR, ISO, CAB, LHA 읽기
- 저작권: Copyright (c) 2000-2011 The Legion Of The Bouncy Castle 외 SharpCompress 기여자

SharpCompress는 다음 프로젝트의 코드를 포함/참조합니다.

- Bouncy Castle (MIT)
- XZ.NET (MIT)
- managed-lzma (MIT)
- ZstdSharp (MIT)

### .NET 10 / WPF

- 라이선스: MIT
- 저장소: https://github.com/dotnet/runtime, https://github.com/dotnet/wpf
- 저작권: .NET Foundation and Contributors

## 개발/테스트 의존성

| 구성요소 | 라이선스 |
|---|---|
| xUnit | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| coverlet.collector | MIT |

## 포함하지 않은 것

- 7-Zip 응용프로그램 및 7z.exe 바이너리 (LGPL) — 포함하지 않습니다.
- unRAR 소스/바이너리 (제한적 freeware 라이선스) — 포함하지 않습니다.
  RAR 읽기는 SharpCompress의 독립 구현을 사용합니다.

## 라이선스 전문

각 구성요소의 전체 라이선스 전문은 NuGet 패키지 및 위 저장소에서 확인할 수 있습니다.
배포 시 이 파일과 LICENSE를 함께 포함하세요.
