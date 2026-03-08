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

## Sample

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4318"
dotnet run --project .\samples\FwoTelemetry.SampleApp\FwoTelemetry.SampleApp.csproj
```

If no OTLP endpoint is configured, the sample still exports to the console.
