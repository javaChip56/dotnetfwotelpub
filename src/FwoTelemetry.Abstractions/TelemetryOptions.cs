using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryOptions
    {
        public TelemetryOptions()
        {
            this.ResourceAttributes = new Dictionary<string, object>();
            this.Redaction = new TelemetryRedactionOptions();
            this.MetricDefinitions = new List<TelemetryMetricDefinition>();
            this.EnableHttpClientInstrumentation = true;
            this.EnableAspNetInstrumentation = true;
            this.OtlpProtocol = TelemetryExportProtocol.HttpProtobuf;
            this.ExportTimeoutMilliseconds = 10000;
            this.BatchExportProcessorOptions = new TelemetryBatchExportProcessorOptions();
            this.SamplingRatio = 1.0;
        }

        public string ServiceName { get; set; }

        public string ServiceVersion { get; set; }

        public string EnvironmentName { get; set; }

        public string TracerName { get; set; }

        public string MeterName { get; set; }

        public string OtlpEndpoint { get; set; }

        public string OtlpHeaders { get; set; }

        public TelemetryExportProtocol OtlpProtocol { get; set; }

        public bool EnableConsoleExporter { get; set; }

        public bool EnableHttpClientInstrumentation { get; set; }

        public bool EnableAspNetInstrumentation { get; set; }

        public double SamplingRatio { get; set; }

        public int ExportTimeoutMilliseconds { get; set; }

        public bool StrictMetricSchema { get; set; }

        public TelemetryBatchExportProcessorOptions BatchExportProcessorOptions { get; private set; }

        public TelemetryRedactionOptions Redaction { get; private set; }

        public IList<TelemetryMetricDefinition> MetricDefinitions { get; private set; }

        public IDictionary<string, object> ResourceAttributes { get; private set; }
    }
}
