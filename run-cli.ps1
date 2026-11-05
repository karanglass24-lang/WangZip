# WangZip CLI 실행
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }

$candidates = @(
    (Join-Path $root 'src\WangZip.Cli\bin\Debug\net10.0\wangzip.dll'),
    (Join-Path $root 'src\WangZip.Cli\bin\Release\net10.0\wangzip.dll')
)

$dll = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $dll) {
    throw "빌드 결과가 없습니다. 먼저 .\build.ps1 을 실행하세요."
}

& $dotnet $dll @Arguments
exit $LASTEXITCODE
