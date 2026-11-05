# WangZip 배포 스크립트 (포터블)
#
#   .\publish.ps1                    자체 포함 (권장, .NET 설치 불필요, 용량 큼)
#   .\publish.ps1 -FrameworkDependent .NET 10 데스크톱 런타임이 설치된 PC용 (작음)
#   .\publish.ps1 -Zip               결과를 ZIP으로 묶음
[CmdletBinding()]
param(
    [switch]$FrameworkDependent,
    [switch]$Zip,
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }

$version = '0.7.0'
$outRoot = Join-Path $root 'artifacts'
$appOut = Join-Path $outRoot "WangZip-$version-$Runtime"
$cliOut = Join-Path $appOut 'cli'

if (Test-Path $appOut) {
    Remove-Item -Recurse -Force $appOut
}

New-Item -ItemType Directory -Force -Path $appOut, $cliOut | Out-Null

$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

$common = @(
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', $selfContained,
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

Write-Host "GUI 게시 중... (self-contained=$selfContained)"
& $dotnet publish (Join-Path $root 'src\WangZip.App\WangZip.App.csproj') @common -o $appOut
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "CLI 게시 중..."
# Windows는 대소문자를 구분하지 않으므로 WangZip.exe와 wangzip.exe가 충돌합니다.
# CLI는 cli 하위 폴더에 둡니다.
& $dotnet publish (Join-Path $root 'src\WangZip.Cli\WangZip.Cli.csproj') @common -o $cliOut
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item (Join-Path $root 'LICENSE') $appOut -Force
Copy-Item (Join-Path $root 'THIRD_PARTY_NOTICES.md') $appOut -Force
Copy-Item (Join-Path $root 'README.md') $appOut -Force
Copy-Item (Join-Path $root 'LICENSE') $cliOut -Force
Copy-Item (Join-Path $root 'THIRD_PARTY_NOTICES.md') $cliOut -Force

if ($Zip) {
    $zipPath = Join-Path $outRoot "WangZip-$version-$Runtime.zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path (Join-Path $appOut '*') -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "ZIP: $zipPath"
}

Write-Host ""
Write-Host "완료: $appOut"
Write-Host "실행: $(Join-Path $appOut 'WangZip.exe')"
Write-Host "CLI:  $(Join-Path $cliOut 'wangzip.exe')"
