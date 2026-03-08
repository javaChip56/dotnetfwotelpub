using System;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.SampleApp
{
    internal sealed class ConsoleLegacyLogSink : ITelemetryLogSink
    {
        public void Write(TelemetryLogEntry entry)
        {
            Console.WriteLine(
                "[legacy:{0}] {1} trace={2} span={3}",
                entry.Level,
                entry.Message,
                string.IsNullOrWhiteSpace(entry.TraceId) ? "-" : entry.TraceId,
                string.IsNullOrWhiteSpace(entry.SpanId) ? "-" : entry.SpanId);

            if (entry.Exception != null)
            {
                Console.WriteLine(entry.Exception);
            }
        }
    }
}
