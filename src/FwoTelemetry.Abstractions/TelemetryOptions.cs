using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryOptions
    {
        public TelemetryOptions()
        {
            this.ResourceAttributes = new Dictionary<string, object>();
        }

        public string ServiceName { get; set; }

        public string ServiceVersion { get; set; }

        public string TracerName { get; set; }

        public string MeterName { get; set; }

        public string OtlpEndpoint { get; set; }

        public string OtlpHeaders { get; set; }

        public bool EnableConsoleExporter { get; set; }

        public IDictionary<string, object> ResourceAttributes { get; private set; }
    }
}
