# FwoTelemetry.Abstractions

This package provides the `net45`-compatible contract used by the other FwoTelemetry packages.

It defines:

- span and metric adapter interfaces
- HTTP/text-map propagation interfaces
- legacy logging hooks for correlation enrichment

Use this package when application code must stay source-compatible across legacy .NET Framework versions.
