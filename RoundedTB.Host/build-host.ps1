<#
.SYNOPSIS
  Publish the unified RoundedTB Light app as one framework-dependent EXE.

.PARAMETER OutputDir
  Publish staging directory (default: ./publish).

.PARAMETER RunSelfTests
  Invoke the published single-file EXE with all nine safe self-test switches.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot 'publish'),
    [switch]$RunSelfTests
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\RoundedTB\RoundedTB.Light.csproj'
$msbuildCommand = Get-Command msbuild.exe -ErrorAction Stop

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$publishDir = (Resolve-Path $OutputDir).Path + '\'

& $msbuildCommand.Source $project -restore `
    '-property:Configuration=Release' `
    '-property:RuntimeIdentifier=win-x64' `
    '-property:SelfContained=false' `
    '-property:PublishSingleFile=true' `
    "-property:PublishDir=$publishDir" `
    '-property:DebugType=None' `
    '-property:DebugSymbols=false' `
    '-property:IncludeNativeLibrariesForSelfExtract=true' `
    '-target:Publish'
if ($LASTEXITCODE -ne 0) { throw "MSBuild publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $OutputDir 'RoundedTBHost.exe'
if (-not (Test-Path $exe)) { throw 'Published RoundedTBHost.exe was not produced.' }

$otherExecutables = @(Get-ChildItem -Path $OutputDir -Filter '*.exe' -File | Where-Object { $_.FullName -ne $exe })
if ($otherExecutables.Count -gt 0) { throw "Unexpected additional executable(s): $($otherExecutables.Name -join ', ')" }

if ($RunSelfTests) {
    $tests = @(
        '/settings-self-test',
        '/dynamic-self-test',
        '/hover-self-test',
        '/rebind-self-test',
        '/region-self-test',
        '/dynamic-region-self-test',
        '/autohide-self-test',
        '/taskbar-model-self-test',
        '/autohide-policy-self-test',
        '/entrypoint-self-test'
    )
    foreach ($test in $tests) {
        & $exe $test
        if ($LASTEXITCODE -ne 0) { throw "self-test $test failed with exit code $LASTEXITCODE" }
    }
}

Write-Host "Published one EXE: $exe"
