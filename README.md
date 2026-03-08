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

Create the adapter:

```csharp
using FwoTelemetry.Abstractions;
using FwoTelemetry.OpenTelemetry;

var options = new TelemetryOptions
{
    ServiceName = "My.Legacy.Service",
    ServiceVersion = "1.0.0",
    TracerName = "My.Legacy.Service.Trace",
    MeterName = "My.Legacy.Service.Metrics",
    OtlpEndpoint = "http://localhost:4318",
    EnableConsoleExporter = true,
};

using (var telemetry = new OpenTelemetryAdapter(options))
{
    using (var span = telemetry.StartSpan("orders.submit"))
    {
        telemetry.IncrementCounter("orders.requests");
        telemetry.RecordHistogram("orders.duration.ms", 42.5, unit: "ms");
        span.SetStatus(TelemetryStatusCode.Ok);
    }
}
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

## Build

```powershell
dotnet build .\DotNetFWOtelPub.sln
```

## Pack

```powershell
dotnet pack .\DotNetFWOtelPub.sln -c Release -o .\artifacts\packages
```

## Sample

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4318"
dotnet run --project .\samples\FwoTelemetry.SampleApp\FwoTelemetry.SampleApp.csproj
```

If no OTLP endpoint is configured, the sample still exports to the console.
