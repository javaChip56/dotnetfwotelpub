using System;
using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryLogEntry
    {
        public TelemetryLogEntry()
        {
            this.TimestampUtc = DateTime.UtcNow;
            this.Properties = new Dictionary<string, object>();
        }

        public DateTime TimestampUtc { get; set; }

        public TelemetryLogLevel Level { get; set; }

        public string Message { get; set; }

        public Exception Exception { get; set; }

        public string TraceId { get; set; }

        public string SpanId { get; set; }

        public IDictionary<string, object> Properties { get; private set; }
    }
}
