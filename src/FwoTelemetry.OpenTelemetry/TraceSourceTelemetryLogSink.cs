using System;
using System.Diagnostics;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    public sealed class TraceSourceTelemetryLogSink : ITelemetryLogSink
    {
        private readonly TraceSource traceSource;

        public TraceSourceTelemetryLogSink(TraceSource traceSource)
        {
            if (traceSource == null)
            {
                throw new ArgumentNullException("traceSource");
            }

            this.traceSource = traceSource;
        }

        public void Write(TelemetryLogEntry entry)
        {
            this.traceSource.TraceEvent(
                MapLevel(entry.Level),
                0,
                "{0} trace={1} span={2}",
                entry.Message,
                string.IsNullOrWhiteSpace(entry.TraceId) ? "-" : entry.TraceId,
                string.IsNullOrWhiteSpace(entry.SpanId) ? "-" : entry.SpanId);

            if (entry.Exception != null)
            {
                this.traceSource.TraceData(MapLevel(entry.Level), 0, entry.Exception);
            }
        }

        private static TraceEventType MapLevel(TelemetryLogLevel level)
        {
            switch (level)
            {
                case TelemetryLogLevel.Trace:
                case TelemetryLogLevel.Debug:
                    return TraceEventType.Verbose;
                case TelemetryLogLevel.Warning:
                    return TraceEventType.Warning;
                case TelemetryLogLevel.Error:
                    return TraceEventType.Error;
                case TelemetryLogLevel.Critical:
                    return TraceEventType.Critical;
                default:
                    return TraceEventType.Information;
            }
        }
    }
}
