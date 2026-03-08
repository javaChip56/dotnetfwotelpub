param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\artifacts\packages",
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipCollectorValidation
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "DotNetFWOtelPub.sln"
$outputPath = Join-Path $repoRoot $OutputDir

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "==> $Name"
    & $Action
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version is required."
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

Invoke-Step "Restore" {
    dotnet restore $solutionPath
}

if (-not $SkipBuild) {
    Invoke-Step "Build" {
        dotnet build $solutionPath -c $Configuration -p:Version=$Version --no-restore
    }
}

if (-not $SkipTests) {
    Invoke-Step "Test" {
        dotnet test $solutionPath -c $Configuration --no-build
    }
}

if (-not $SkipCollectorValidation) {
    Invoke-Step "Collector validation" {
        powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "tests\run-collector-validation.ps1")
    }
}

Invoke-Step "Pack" {
    dotnet pack $solutionPath -c $Configuration -o $outputPath -p:Version=$Version --no-build
}

Write-Host "Packages created in $outputPath"
