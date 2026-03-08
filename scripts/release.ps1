param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\artifacts\packages",
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [switch]$Publish,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipCollectorValidation,
    [switch]$SkipSymbols
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "==> Starting release for version $Version"

powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts\pack-release.ps1") `
    -Version $Version `
    -Configuration $Configuration `
    -OutputDir $OutputDir `
    -SkipBuild:$SkipBuild `
    -SkipTests:$SkipTests `
    -SkipCollectorValidation:$SkipCollectorValidation

if ($Publish) {
    powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts\publish-nuget.ps1") `
        -Version $Version `
        -PackageDir $OutputDir `
        -ApiKey $ApiKey `
        -Source $Source `
        -SkipSymbols:$SkipSymbols
}

Write-Host "==> Release completed for version $Version"
