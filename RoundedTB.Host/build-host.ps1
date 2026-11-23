<#
.SYNOPSIS
  Reproducible build for the lightweight RoundedTB taskbar host.

.DESCRIPTION
  Compiles every .cs file in this directory with the Windows .NET Framework
  C# compiler (csc.exe) into RoundedTBHost.exe, then copies tray.ico beside it.
  The same script runs locally and in CI, so the artifact is identical.

.PARAMETER OutputDir
  Directory for RoundedTBHost.exe. Defaults to ./out.

.PARAMETER RunSelfTests
  After building, run the host's headless self-tests (pure logic + self-owned
  HWND region checks). Any failure throws.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot 'out'),
    [switch]$RunSelfTests
)

$ErrorActionPreference = 'Stop'

function Find-FrameworkDir {
    $candidates = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319')
    )
    foreach ($dir in $candidates) {
        if (Test-Path (Join-Path $dir 'csc.exe')) { return $dir }
    }
    throw 'Could not find the .NET Framework C# compiler (csc.exe).'
}

$framework = Find-FrameworkDir
$csc = Join-Path $framework 'csc.exe'
$wpf = Join-Path $framework 'WPF'

$sources = @(Get-ChildItem -Path $PSScriptRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw 'No .cs sources found next to build-host.ps1.' }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$exe = Join-Path $OutputDir 'RoundedTBHost.exe'
$icon = Join-Path $PSScriptRoot 'tray.ico'

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    "/out:$exe",
    "/win32icon:$icon",
    '/reference:System.Web.Extensions.dll',
    "/reference:$wpf\UIAutomationClient.dll",
    "/reference:$wpf\UIAutomationTypes.dll",
    "/reference:$wpf\WindowsBase.dll"
) + $sources

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

Copy-Item -LiteralPath $icon -Destination $OutputDir -Force

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
        '/autohide-policy-self-test'
    )
    foreach ($test in $tests) {
        & $exe $test
        if ($LASTEXITCODE -ne 0) { throw "self-test $test failed with exit code $LASTEXITCODE" }
    }
}

Write-Host "Built $exe"
