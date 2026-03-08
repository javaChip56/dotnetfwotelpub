# DotNet Framework OpenTelemetry Adapter

This repository demonstrates a practical compatibility model for OpenTelemetry in legacy .NET Framework applications:

- `FwoTelemetry.Abstractions` targets `net45` and defines the adapter contract.
- `FwoTelemetry.NoOp` targets `net45` and provides a safe fallback implementation.
- `FwoTelemetry.OpenTelemetry` targets `net48` and bridges the same contract to OpenTelemetry.
- `FwoTelemetry.SampleApp` targets `net48` and shows the adapter in use.

## Compatibility model

OpenTelemetry's current .NET packages do not target `.NET Framework 4.5`. Because of that, the backward-compatible approach is not a single binary. The clean design is:

1. Keep your application-facing telemetry contract on `net45`.
2. Use the no-op adapter for `net45` applications or older deployment slices.
3. Use the OpenTelemetry-backed adapter in `net48` applications.

That lets one application code path remain source-compatible across `net45` through `net48`, while only the runtime-specific implementation changes.

## Included capabilities

- Trace spans and metrics through one shared adapter interface.
- HTTP/text-map propagation with `Inject` and `Extract` methods over plain dictionaries.
- Legacy logging hooks that enrich log properties with trace and span identifiers.
- Config-driven runtime initialization for .NET Framework 4.8 applications.
- Redaction policies for span attributes, metric tags, log properties, and propagated headers.
- Metric schema enforcement with approved metric names and tag keys.
- Automatic ASP.NET and `HttpClient` instrumentation through OpenTelemetry instrumentation packages.
- Unit tests plus collector-backed smoke validation.
- NuGet packaging metadata for the reusable library projects.

## Usage

### `net45` application

Reference:

```xml
<ItemGroup>
  <PackageReference Include="FwoTelemetry.Abstractions" Version="1.0.0" />
  <PackageReference Include="FwoTelemetry.NoOp" Version="1.0.0" />
</ItemGroup>
```

Wire the adapter:

```csharp
using System.Collections.Generic;
using FwoTelemetry.Abstractions;
using FwoTelemetry.NoOp;

ITelemetryAdapter telemetry = NoOpTelemetryAdapter.Instance;

using (var span = telemetry.StartSpan(
    "legacy.operation",
    TelemetrySpanKind.Internal,
    new Dictionary<string, object>
    {
        { "component", "legacy-app" },
    }))
{
    telemetry.IncrementCounter("legacy.requests");
    span.SetStatus(TelemetryStatusCode.Ok);
}
```

### `net48` application with OpenTelemetry

Reference:

```xml
<ItemGroup>
  <PackageReference Include="FwoTelemetry.Abstractions" Version="1.0.0" />
  <PackageReference Include="FwoTelemetry.OpenTelemetry" Version="1.0.0" />
</ItemGroup>
```

Create the runtime from `appSettings`:

```xml
<appSettings>
  <add key="FwoTelemetry:ServiceName" value="My.Legacy.Service" />
  <add key="FwoTelemetry:ServiceVersion" value="1.0.0" />
  <add key="FwoTelemetry:EnvironmentName" value="prod" />
  <add key="FwoTelemetry:TracerName" value="My.Legacy.Service.Trace" />
  <add key="FwoTelemetry:MeterName" value="My.Legacy.Service.Metrics" />
  <add key="FwoTelemetry:EnableConsoleExporter" value="false" />
  <add key="FwoTelemetry:EnableHttpClientInstrumentation" value="true" />
  <add key="FwoTelemetry:EnableAspNetInstrumentation" value="true" />
  <add key="FwoTelemetry:StrictMetricSchema" value="true" />
  <add key="FwoTelemetry:SamplingRatio" value="1.0" />
  <add key="FwoTelemetry:ExportTimeoutMilliseconds" value="10000" />
</appSettings>
```

Initialize once at process startup:

```csharp
using FwoTelemetry.Abstractions;
using FwoTelemetry.OpenTelemetry;

var options = TelemetryOptionsLoader.LoadFromAppSettings();
options.OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
options.OtlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
options.Redaction.LogProperties.SensitiveKeys.Add("customer.email");
options.Redaction.SpanAttributes.SensitiveKeys.Add("customer.email");
options.Redaction.Headers.AllowedKeys.Add("traceparent");
options.Redaction.Headers.AllowedKeys.Add("tracestate");
options.Redaction.Headers.AllowedKeys.Add("baggage");
options.Redaction.Headers.DropUnknownKeys = true;

options.MetricDefinitions.Add(new TelemetryMetricDefinition
{
    Name = "orders.requests",
    MetricType = TelemetryMetricType.Counter,
    Unit = "request",
    Description = "Number of order requests",
});
options.MetricDefinitions[0].AllowedTagKeys.Add("operation");

options.MetricDefinitions.Add(new TelemetryMetricDefinition
{
    Name = "orders.duration.ms",
    MetricType = TelemetryMetricType.Histogram,
    Unit = "ms",
    Description = "Order processing duration",
});
options.MetricDefinitions[1].AllowedTagKeys.Add("operation");

var telemetry = TelemetryRuntime.Initialize(options);
```

Use the singleton runtime during request or job handling:

```csharp
var telemetry = TelemetryRuntime.Current;

using (var span = telemetry.StartSpan("orders.submit"))
{
    telemetry.IncrementCounter(
        "orders.requests",
        attributes: new Dictionary<string, object>
        {
            { "operation", "orders.submit" },
        });

    telemetry.RecordHistogram(
        "orders.duration.ms",
        42.5,
        new Dictionary<string, object>
        {
            { "operation", "orders.submit" },
        },
        unit: "ms");

    span.SetStatus(TelemetryStatusCode.Ok);
}
```

Flush and shut down on process exit:

```csharp
TelemetryRuntime.ForceFlush(5000);
TelemetryRuntime.Shutdown();
```

### HTTP propagation

Inject outbound headers:

```csharp
var headers = new Dictionary<string, string>();

using (var span = telemetry.StartSpan("publish", TelemetrySpanKind.Producer))
{
    telemetry.Propagator.Inject(headers);
}
```

Extract inbound headers and continue the trace:

```csharp
var parentContext = telemetry.Propagator.Extract(headers);

using (var span = telemetry.StartSpan(
    "consume",
    TelemetrySpanKind.Consumer,
    parentContext: parentContext))
{
    span.SetStatus(TelemetryStatusCode.Ok);
}
```

### Legacy logging hook

Register a sink:

```csharp
using FwoTelemetry.Abstractions;

internal sealed class ConsoleLogSink : ITelemetryLogSink
{
    public void Write(TelemetryLogEntry entry)
    {
        Console.WriteLine(
            "[{0}] {1} trace={2} span={3}",
            entry.Level,
            entry.Message,
            entry.TraceId,
            entry.SpanId);
    }
}
```

Use it:

```csharp
telemetry.Logging.RegisterSink(new ConsoleLogSink());

var properties = new Dictionary<string, object>
{
    { "operation", "orders.submit" },
};

telemetry.Logging.Enrich(properties);
telemetry.Logging.Log(
    TelemetryLogLevel.Information,
    "Submitting order",
    properties: properties);
```

Built-in production sinks:

```csharp
telemetry.Logging.RegisterSink(new TraceSourceTelemetryLogSink(new TraceSource("Orders")));
telemetry.Logging.RegisterSink(new LoggerTelemetryLogSink(logger));
```

### ASP.NET and `HttpClient` integration

If `FwoTelemetry:EnableAspNetInstrumentation=true`, inbound ASP.NET requests are automatically traced once the runtime has been initialized.

Example `Global.asax`:

```csharp
protected void Application_Start()
{
    var options = TelemetryOptionsLoader.LoadFromAppSettings();
    options.OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    TelemetryRuntime.Initialize(options);
}

protected void Application_End()
{
    TelemetryRuntime.ForceFlush(5000);
    TelemetryRuntime.Shutdown();
}
```

If `FwoTelemetry:EnableHttpClientInstrumentation=true`, outbound `HttpClient` calls are automatically traced and linked to the current request span as long as they execute inside an active span or inbound ASP.NET request.

### Production controls

Recommended baseline:

- initialize telemetry once per process through `TelemetryRuntime`
- keep `StrictMetricSchema=true` and pre-register metric names and allowed tag keys
- define redaction for sensitive fields such as emails, account IDs, tokens, and auth headers
- keep OTLP export over HTTP/protobuf on .NET Framework
- use log sinks that feed your existing logger or `TraceSource`
- flush on controlled shutdown paths

### Where to use the adapter

Use the adapter at code boundaries where work starts, where work is handed off, and where expensive work happens.

Typical placement in legacy .NET Framework applications:

- `Global.asax`, MVC filters, Web API handlers, or HTTP modules for inbound web requests
- Windows Service startup and worker loops
- WCF service methods and client wrappers
- message queue publishers and consumers
- scheduled jobs, batch entrypoints, and console commands
- service-layer methods for major business operations
- outbound HTTP, database, queue, and third-party API calls
- exception handling and legacy logging pipelines

Practical rule:

- Start a span when an operation begins
- add child spans for important internal steps
- inject context before outbound calls
- extract context when receiving inbound work
- record counters and histograms for throughput and duration
- enrich logs with the current trace and span identifiers

### How to track telemetry across the whole codebase

The adapter does not automatically trace every line of code. The reliable way to track the application is to use one shared `ITelemetryAdapter` instance and apply it consistently at entrypoints and cross-process boundaries.

Recommended pattern:

1. Create the adapter once at application startup.
2. Pass `ITelemetryAdapter` into controllers, services, handlers, and background workers.
3. Start one parent span per incoming request, message, or job.
4. Start child spans for major operations such as validation, database work, external HTTP calls, and publishing messages.
5. Use `telemetry.Propagator.Inject(...)` on outbound messages or HTTP headers.
6. Use `telemetry.Propagator.Extract(...)` on inbound messages or HTTP headers to continue the same trace.
7. Use `telemetry.Logging.Enrich(...)` or `telemetry.Logging.Log(...)` so logs carry `trace.id` and `span.id`.
8. Export traces and metrics to the same OTLP backend so requests, dependencies, durations, and failures can be correlated.

Example layout:

```csharp
public sealed class OrderService
{
    private readonly ITelemetryAdapter telemetry;

    public OrderService(ITelemetryAdapter telemetry)
    {
        this.telemetry = telemetry;
    }

    public void Submit(Order order)
    {
        using (var span = this.telemetry.StartSpan("orders.submit"))
        {
            Validate(order);
            Save(order);
            Publish(order);
            span.SetStatus(TelemetryStatusCode.Ok);
        }
    }

    private void Validate(Order order)
    {
        using (var span = this.telemetry.StartSpan("orders.validate"))
        {
            span.SetAttribute("order.id", order.Id);
        }
    }

    private void Save(Order order)
    {
        using (var span = this.telemetry.StartSpan("orders.save"))
        {
            this.telemetry.RecordHistogram("orders.db.duration.ms", 12.0, unit: "ms");
        }
    }

    private void Publish(Order order)
    {
        using (var span = this.telemetry.StartSpan("orders.publish", TelemetrySpanKind.Producer))
        {
            var headers = new Dictionary<string, string>();
            this.telemetry.Propagator.Inject(headers);
        }
    }
}
```

If you follow that pattern, the backend can show:

- the full request or job trace
- child operations inside that trace
- the link between outbound and inbound components
- correlated logs for the same trace
- metrics such as request count and duration for the same operations

## Validation

Run unit tests:

```powershell
dotnet test .\DotNetFWOtelPub.sln
```

Run collector-backed validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-collector-validation.ps1
```

The collector validation script starts `otel/opentelemetry-collector-contrib:0.143.0`, sends sample telemetry to `http://localhost:4318`, and verifies that the collector output contains both traces and metrics.

## Build

```powershell
dotnet build .\DotNetFWOtelPub.sln
```

## Pack

```powershell
dotnet pack .\DotNetFWOtelPub.sln -c Release -o .\artifacts\packages
```

## Release Automation

### Local scripts

Build, test, run collector validation, and pack a specific version:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\pack-release.ps1 -Version 1.0.1
```

Publish already-built packages to NuGet:

```powershell
$env:NUGET_API_KEY = "your-nuget-api-key"
powershell -ExecutionPolicy Bypass -File .\scripts\publish-nuget.ps1 -Version 1.0.1
```

Run the full release flow and publish in one command:

```powershell
$env:NUGET_API_KEY = "your-nuget-api-key"
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1 -Version 1.0.1 -Publish
```

Available scripts:

- `scripts\pack-release.ps1`: restore, build, test, collector validation, and pack versioned packages
- `scripts\publish-nuget.ps1`: push `.nupkg` and `.snupkg` files to NuGet with `--skip-duplicate`
- `scripts\release.ps1`: wrapper that runs pack and optionally publish

Useful flags:

- `-SkipCollectorValidation` to skip Docker-based collector validation during a release pack
- `-SkipTests` or `-SkipBuild` for controlled reruns
- `-SkipSymbols` to publish only primary packages
- `-Source` to publish to a non-default feed

The scripts stamp package versions using `-p:Version=<version>`, so you do not need to edit project files for each release.

### GitHub Actions

This repository now includes two workflows:

- `.github/workflows/ci.yml`: runs restore, build, and test on `push` to `main`, pull requests, and manual dispatch
- `.github/workflows/release.yml`: builds, tests, packs, creates or updates a GitHub release, and can publish to NuGet

Required GitHub secret:

- `NUGET_API_KEY`: NuGet.org API key used by the release workflow when publishing packages

Recommended GitHub environment:

- `nuget`: optional protected environment used by the publish job in `.github/workflows/release.yml`

Release workflow triggers:

- push a tag such as `v1.0.1`
- run the `release` workflow manually with `workflow_dispatch`

Tag-driven release example:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

What the release workflow does:

1. Resolves the package version from the tag or manual input
2. Runs `scripts\pack-release.ps1`
3. Uploads the generated `.nupkg` and `.snupkg` files as workflow artifacts
4. Creates or updates the matching GitHub release
5. Publishes the packages to NuGet.org if `NUGET_API_KEY` is available and publishing is enabled

Notes:

- the GitHub-hosted Windows runner is used because the solution builds `net48`
- collector validation is skipped inside the release workflow because the release job is designed for standard hosted Windows runners; keep `tests\run-collector-validation.ps1` as an explicit pre-release or local validation step
- the release workflow reuses the local PowerShell scripts so local and CI packaging stay aligned

## Sample

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4318"
dotnet run --project .\samples\FwoTelemetry.SampleApp\FwoTelemetry.SampleApp.csproj
```

If no OTLP endpoint is configured, the sample still exports to the console.
