# WangZip build script
[CmdletBinding()]
param(
    [switch]$Release,
    [switch]$Test,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
$configuration = if ($Release) { 'Release' } else { 'Debug' }
$solution = Join-Path $root 'WangZip.slnx'

Write-Host "dotnet: $dotnet"
Write-Host "configuration: $configuration"

if ($Clean) {
    & $dotnet clean $solution -c $configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& $dotnet build $solution -c $configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Test) {
    & $dotnet test (Join-Path $root 'tests\WangZip.Core.Tests\WangZip.Core.Tests.csproj') -c $configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ""
Write-Host "완료. 실행: .\run-app.ps1 또는 .\run-cli.ps1"
