param(
    [string]$Output = (Join-Path $PSScriptRoot "..\publish\win-x64")
)

$ErrorActionPreference = "Stop"
$app = Join-Path $PSScriptRoot "..\NetScope.App\NetScope.App.csproj"

dotnet publish $app `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    -p:CopyDebugSymbolFilesFromPackages=false `
    -o $Output `
    --nologo

Get-ChildItem $Output -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force

$legacy = Join-Path $Output "NetScope.App.exe"
$named = Join-Path $Output "NetScope.exe"
if ((Test-Path $legacy) -and -not (Test-Path $named)) {
    Move-Item $legacy $named -Force
}
elseif (Test-Path $legacy) {
    Remove-Item $legacy -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $named)) {
    throw "Publish did not produce NetScope.exe"
}

Write-Host "Published: $named"
