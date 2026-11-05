# WangZip 설치 프로그램 빌드
#   .\installer\build-installer.ps1
# 필요: .tools\inno\ISCC.exe (Inno Setup 7, README 참고) + publish.ps1 결과물
[CmdletBinding()]
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = '0.5.0'

$publishDir = Join-Path $root "artifacts\WangZip-$version-win-x64"
if (-not $SkipPublish -and -not (Test-Path (Join-Path $publishDir 'WangZip.exe'))) {
    & (Join-Path $root 'publish.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'publish.ps1 실행 실패' }
}
if (-not (Test-Path (Join-Path $publishDir 'WangZip.exe'))) {
    throw "배포본이 없습니다: $publishDir (publish.ps1을 먼저 실행하세요)"
}

$iscc = Join-Path $root '.tools\inno\ISCC.exe'
if (-not (Test-Path $iscc)) {
    throw "ISCC.exe를 찾을 수 없습니다: $iscc`nInno Setup 7을 .tools\inno 폴더에 설치하세요."
}

$iss = Join-Path $PSScriptRoot 'wangzip.iss'
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw 'ISCC 컴파일 실패' }

$out = Join-Path $root "artifacts\WangZip-$version-setup.exe"
if (-not (Test-Path $out)) { throw "설치 프로그램이 만들어지지 않았습니다: $out" }

"설치 프로그램: $out ({0:N1} MB)" -f ((Get-Item $out).Length / 1MB)
