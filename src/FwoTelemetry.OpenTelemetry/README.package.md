# FwoTelemetry.OpenTelemetry

This package bridges `FwoTelemetry.Abstractions` to OpenTelemetry on `.NET Framework 4.8`.

It includes:

- span creation over `ActivitySource`
- metrics over `Meter`
- text-map propagation for HTTP headers
- legacy logging hooks with trace/span correlation

Use `FwoTelemetry.NoOp` instead on `net45` application slices.
