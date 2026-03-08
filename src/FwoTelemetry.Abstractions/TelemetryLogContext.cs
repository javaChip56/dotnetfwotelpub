namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryLogContext
    {
        public string TraceId { get; set; }

        public string SpanId { get; set; }
    }
}
