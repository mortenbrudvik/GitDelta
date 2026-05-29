#Requires -Version 7.0
<#
.SYNOPSIS
  Publishes GitDelta.UI as a framework-dependent single-file gitdelta.exe.
.DESCRIPTION
  Output: <repo>\publish\gitdelta.exe  (PDB is embedded, so only one file).
  Framework-dependent: the target machine needs the .NET 10 Desktop Runtime (the installer
  in installer/GitDelta.iss detects and installs it automatically).

  Canonical publish command (also what this script runs):
    dotnet publish src/GitDelta.UI/GitDelta.UI.csproj -c Release -r win-x64 -p:PublishProfile=win-x64 -o publish

  Note: EnableCompressionInSingleFile is NOT used — the .NET SDK only supports bundle
  compression for self-contained apps; framework-dependent single-file rejects it.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Runtime       = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'src/GitDelta.UI/GitDelta.UI.csproj'
$outDir   = Join-Path $repoRoot 'publish'

if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
}

Write-Host "Publishing GitDelta.UI ($Configuration / $Runtime) -> $outDir" -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    -p:PublishProfile=win-x64 `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $outDir 'gitdelta.exe'
if (-not (Test-Path $exe)) {
    throw "Expected output not found: $exe"
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "OK: $exe ($sizeMb MB)" -ForegroundColor Green
