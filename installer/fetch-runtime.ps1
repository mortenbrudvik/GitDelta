#Requires -Version 7.0
<#
.SYNOPSIS
  Downloads the .NET 10 Desktop Runtime (x64) offline installer into installer/runtime/.
.DESCRIPTION
  The file is bundled by GitDelta.iss and run silently on the target machine only when the
  runtime is missing. The exe is gitignored (fetched at build time, never committed).

  Obtain the official SHA-256 from https://dotnet.microsoft.com/download/dotnet/10.0 and pass
  it via -ExpectedSha256 so the download is integrity-checked before it is bundled.

  Run from the repo root or from the installer/ directory:
    pwsh -File installer/fetch-runtime.ps1 -ExpectedSha256 <hash>
#>
[CmdletBinding()]
param(
    # Pinned to a known .NET 10 Desktop Runtime build. Update when bumping the runtime.
    [string] $Version       = '10.0.0',
    # If empty, derived from $Version below so a custom -Version never saves under a stale name.
    [string] $FileName      = '',
    # Official SHA-256 of the runtime installer (from the Microsoft download page). When set,
    # the download is verified and the script throws on mismatch.
    [string] $ExpectedSha256 = ''
)

$ErrorActionPreference = 'Stop'

# Derive the filename from the version when not explicitly passed.
if ([string]::IsNullOrWhiteSpace($FileName)) {
    $FileName = "windowsdesktop-runtime-$Version-win-x64.exe"
}

$runtimeDir = Join-Path $PSScriptRoot 'runtime'
$target     = Join-Path $runtimeDir $FileName

if (Test-Path $target) {
    Write-Host "Already present: $target" -ForegroundColor Green
    return
}

# Microsoft's stable aka.ms redirector for the .NET Desktop Runtime offline installer.
$url = "https://aka.ms/dotnet/$Version/windowsdesktop-runtime-win-x64.exe"
Write-Host "Downloading .NET Desktop Runtime $Version -> $target" -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing

if (-not (Test-Path $target) -or (Get-Item $target).Length -lt 10MB) {
    throw "Runtime download failed or file too small: $target"
}

# Integrity check against the official SHA-256 (when provided).
if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
    $actual = (Get-FileHash $target -Algorithm SHA256).Hash
    if ($actual -ne $ExpectedSha256.Trim()) {
        Remove-Item -Force $target
        throw "SHA-256 mismatch for $FileName`n  expected: $($ExpectedSha256.Trim())`n  actual:   $actual`nDeleted the downloaded file."
    }
    Write-Host "SHA-256 verified: $actual" -ForegroundColor Green
}
else {
    Write-Host "WARNING: no -ExpectedSha256 supplied; download was NOT integrity-checked." -ForegroundColor Yellow
}

Write-Host "OK: $target ($([math]::Round((Get-Item $target).Length/1MB,0)) MB)" -ForegroundColor Green
