using System;
using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryPropagationContext
    {
        public TelemetryPropagationContext()
        {
            this.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string TraceId { get; set; }

        public string SpanId { get; set; }

        public string TraceState { get; set; }

        public bool IsRemote { get; set; }

        public IDictionary<string, string> Headers { get; private set; }

        public object NativeContext { get; set; }
    }
}
