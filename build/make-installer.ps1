#Requires -Version 7.0
<#
.SYNOPSIS
  End-to-end installer build: publish gitdelta.exe, fetch the bundled runtime, compile GitDelta.iss.
.DESCRIPTION
  Output: installer\Output\GitDelta-<version>-setup.exe

  Prerequisites:
    - .NET 10 SDK (dotnet publish)
    - Inno Setup 6.x (ISCC.exe) — winget install JRSoftware.InnoSetup
#>
[CmdletBinding()]
param(
    [switch] $SkipRuntimeBundle,
    [string] $IsccPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$issFile  = Join-Path $repoRoot 'installer/GitDelta.iss'

# 1) Publish the single-file exe.
& (Join-Path $PSScriptRoot 'publish.ps1')

# 2) Fetch the bundled .NET Desktop Runtime (unless skipped, e.g. CI that relies on download page).
if (-not $SkipRuntimeBundle) {
    & (Join-Path $repoRoot 'installer/fetch-runtime.ps1')
}
else {
    Write-Host "Skipping runtime bundle (installer will open the download page if runtime is missing)." -ForegroundColor Yellow
}

# 3) Compile the installer.
if (-not (Test-Path $IsccPath)) {
    throw "Inno Setup compiler not found at '$IsccPath'. Install Inno Setup 6 or pass -IsccPath."
}
Write-Host "Compiling installer: $issFile" -ForegroundColor Cyan
& $IsccPath $issFile
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}

$out = Get-ChildItem (Join-Path $repoRoot 'installer/Output') -Filter '*-setup.exe' |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "OK: $($out.FullName) ($([math]::Round($out.Length/1MB,1)) MB)" -ForegroundColor Green
