param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$repoRootPath = (Resolve-Path $repoRoot).Path
$versionLine = Select-String -Path (Join-Path $repoRoot "AppVersion.cs") -Pattern 'Version\s*=\s*"([^"]+)"' | Select-Object -First 1
if (-not $versionLine) { throw "Could not read AppVersion.Version." }

$version = $versionLine.Matches[0].Groups[1].Value
$publishDir = Join-Path $repoRoot "publish\CowPilot-$version-win-x64"
$releaseDir = Join-Path $repoRoot "release"
$zipPath = Join-Path $releaseDir "CowPilot-$version-win-x64.zip"
$manifestPath = Join-Path $releaseDir "latest.json"

function Assert-UnderRepo([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $repoRootPath.TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing path outside repo: $fullPath"
    }
    return $fullPath
}

$publishDir = Assert-UnderRepo $publishDir
$releaseDir = Assert-UnderRepo $releaseDir
$zipPath = Assert-UnderRepo $zipPath
$manifestPath = Assert-UnderRepo $manifestPath

if (Test-Path $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

dotnet publish (Join-Path $repoRoot "CowPilot.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

@"
Cow Pilot $version

Run CowPilot.exe.

This package is self-contained for Windows x64 and includes the .NET runtime.
No separate .NET installer is required.

On launch, Cow Pilot checks the Cow Pilot GitHub Releases page for a newer
version and only shows a message if one exists. It does not download or install
updates.
"@ | Set-Content -LiteralPath (Join-Path $publishDir "README-FIRST.txt")

Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $publishDir "README.md") -Force

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

@{
    version = $version
    package = "CowPilot-$version-win-x64.zip"
    url = "https://github.com/AlecBurr/Cow-Pilot/releases/download/v$version/CowPilot-$version-win-x64.zip"
    selfContained = $true
    platform = "win-x64"
} | ConvertTo-Json | Set-Content -LiteralPath $manifestPath

Write-Output $zipPath
