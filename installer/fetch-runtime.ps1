#Requires -Version 7.0
<#
.SYNOPSIS
  Downloads the .NET 10 Desktop Runtime (x64) offline installer into installer/runtime/.
.DESCRIPTION
  The file is bundled by GitDelta.iss and run silently on the target machine only when the
  runtime is missing. The exe is gitignored (fetched at build time, never committed).

  Run from the repo root or from the installer/ directory:
    pwsh -File installer/fetch-runtime.ps1
#>
[CmdletBinding()]
param(
    # Pinned to a known .NET 10 Desktop Runtime build. Update when bumping the runtime.
    [string] $Version  = '10.0.0',
    [string] $FileName = 'windowsdesktop-runtime-10.0.0-win-x64.exe'
)

$ErrorActionPreference = 'Stop'
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
Write-Host "OK: $target ($([math]::Round((Get-Item $target).Length/1MB,0)) MB)" -ForegroundColor Green
