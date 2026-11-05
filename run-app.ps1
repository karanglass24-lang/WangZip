# WangZip GUI 실행
param(
    [string]$Archive
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }

$candidates = @(
    (Join-Path $root 'src\WangZip.App\bin\Debug\net10.0-windows\WangZip.dll'),
    (Join-Path $root 'src\WangZip.App\bin\Release\net10.0-windows\WangZip.dll')
)

$dll = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $dll) {
    throw "빌드 결과가 없습니다. 먼저 .\build.ps1 을 실행하세요."
}

if ($Archive) {
    & $dotnet $dll $Archive
}
else {
    & $dotnet $dll
}
