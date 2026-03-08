param(
    [string]$ComposeFile = ".\tests\collector\docker-compose.collector.yml"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required to run collector validation."
}

$artifactDir = Join-Path $PSScriptRoot "..\artifacts\collector"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

try {
    docker compose -f $ComposeFile up -d
    Start-Sleep -Seconds 5

    $env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4318"
    dotnet run --project .\samples\FwoTelemetry.SampleApp\FwoTelemetry.SampleApp.csproj | Out-Null
    Start-Sleep -Seconds 5

    $logPath = Join-Path $artifactDir "collector-output.log"
    docker compose -f $ComposeFile logs otel-collector | Set-Content $logPath

    $content = Get-Content $logPath -Raw

    if ($content -notmatch "sample.outbound") {
        throw "Collector output did not contain the outbound sample span."
    }

    if ($content -notmatch "sample.requests") {
        throw "Collector output did not contain the sample metric."
    }
}
finally {
    docker compose -f $ComposeFile down -v
}
