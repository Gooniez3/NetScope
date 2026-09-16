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

$appExe = Join-Path $Output "NetScope.App.exe"
$named = Join-Path $Output "NetScope.exe"
if (Test-Path $appExe) {
    Move-Item $appExe $named -Force
}

Write-Host "Published: $named"
