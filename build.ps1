# Builds RevisionTracker on Windows and produces dist\RevisionTracker-Setup-<version>.exe
# Needs: Visual Studio 2022 (or the .NET SDK) and NSIS (https://nsis.sourceforge.io, or: winget install NSIS.NSIS)
param([string]$Version = "0.1.0")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet build RevisionTracker.sln -c Release
$out = Join-Path $PSScriptRoot "src\RevisionTracker.App\bin\Release\net48"

$makensis = (Get-Command makensis -ErrorAction SilentlyContinue).Source
if (-not $makensis) { $makensis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe" }
New-Item -ItemType Directory -Force dist | Out-Null
& $makensis "/DVERSION=$Version" "/DSRCDIR=$out" installer\RevisionTracker.nsi
Write-Host "Installer: dist\RevisionTracker-Setup-$Version.exe"
