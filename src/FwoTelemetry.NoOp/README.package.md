# FwoTelemetry.NoOp

This package provides the no-op implementation of the FwoTelemetry adapter surface for `net45`.

Use it when:

- OpenTelemetry is not available on the target runtime
- you need the same application code path without exporting telemetry
- older deployment slices must remain binary-compatible
