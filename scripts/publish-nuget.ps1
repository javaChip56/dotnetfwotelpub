param(
    [string]$Version,
    [string]$PackageDir = ".\artifacts\packages",
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = $env:NUGET_API_KEY,
    [switch]$SkipSymbols
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$packagePath = Join-Path $repoRoot $PackageDir

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "NuGet API key is required. Pass -ApiKey or set NUGET_API_KEY."
}

if (-not (Test-Path $packagePath)) {
    throw "Package directory not found: $packagePath"
}

$packages = Get-ChildItem $packagePath -File -Filter *.nupkg |
    Where-Object { $_.Name -notlike "*.snupkg" }

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $packages = $packages | Where-Object { $_.BaseName -like "*.$Version" }
}

if (-not $packages) {
    throw "No NuGet packages found to publish."
}

foreach ($package in $packages) {
    Write-Host "==> Publishing $($package.Name)"
    dotnet nuget push $package.FullName --api-key $ApiKey --source $Source --skip-duplicate
}

if (-not $SkipSymbols) {
    $symbolPackages = Get-ChildItem $packagePath -File -Filter *.snupkg

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $symbolPackages = $symbolPackages | Where-Object { $_.BaseName -like "*.$Version" }
    }

    foreach ($symbolPackage in $symbolPackages) {
        Write-Host "==> Publishing $($symbolPackage.Name)"
        dotnet nuget push $symbolPackage.FullName --api-key $ApiKey --source $Source --skip-duplicate
    }
}
