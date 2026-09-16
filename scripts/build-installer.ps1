param(
    [string]$PublishOutput = (Join-Path $PSScriptRoot "..\publish\win-x64")
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$iss = Join-Path $root "installer\netscope.iss"

& (Join-Path $PSScriptRoot "publish-win.ps1") -Output $PublishOutput

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Inno Setup 6 is not installed. Install it from https://jrsoftware.org/isinfo.php then re-run this script."
    Write-Host "Published portable exe: $(Join-Path $PublishOutput 'NetScope.exe')"
    exit 1
}

& $iscc $iss
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installer: $(Join-Path $root 'installer\output\NetScope-Setup-1.0.0.exe')"
