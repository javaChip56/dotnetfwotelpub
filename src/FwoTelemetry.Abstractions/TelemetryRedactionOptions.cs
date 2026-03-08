namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryRedactionOptions
    {
        public TelemetryRedactionOptions()
        {
            this.RedactedValue = "[REDACTED]";
            this.SpanAttributes = new TelemetryTagPolicy();
            this.MetricTags = new TelemetryTagPolicy();
            this.LogProperties = new TelemetryTagPolicy();
            this.Headers = new TelemetryTagPolicy();
        }

        public string RedactedValue { get; set; }

        public TelemetryTagPolicy SpanAttributes { get; private set; }

        public TelemetryTagPolicy MetricTags { get; private set; }

        public TelemetryTagPolicy LogProperties { get; private set; }

        public TelemetryTagPolicy Headers { get; private set; }
    }
}
