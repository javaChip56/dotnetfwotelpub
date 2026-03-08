using System;
using FwoTelemetry.Abstractions;
using Microsoft.Extensions.Logging;

namespace FwoTelemetry.OpenTelemetry
{
    public sealed class LoggerTelemetryLogSink : ITelemetryLogSink
    {
        private readonly ILogger logger;

        public LoggerTelemetryLogSink(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.logger = logger;
        }

        public void Write(TelemetryLogEntry entry)
        {
            this.logger.Log(
                MapLevel(entry.Level),
                default(EventId),
                entry,
                entry.Exception,
                (state, exception) => Format(state));
        }

        private static LogLevel MapLevel(TelemetryLogLevel level)
        {
            switch (level)
            {
                case TelemetryLogLevel.Trace:
                    return LogLevel.Trace;
                case TelemetryLogLevel.Debug:
                    return LogLevel.Debug;
                case TelemetryLogLevel.Warning:
                    return LogLevel.Warning;
                case TelemetryLogLevel.Error:
                    return LogLevel.Error;
                case TelemetryLogLevel.Critical:
                    return LogLevel.Critical;
                default:
                    return LogLevel.Information;
            }
        }

        private static string Format(TelemetryLogEntry entry)
        {
            return string.Format(
                "{0} trace={1} span={2}",
                entry.Message,
                string.IsNullOrWhiteSpace(entry.TraceId) ? "-" : entry.TraceId,
                string.IsNullOrWhiteSpace(entry.SpanId) ? "-" : entry.SpanId);
        }
    }
}
