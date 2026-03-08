using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryMetricDefinition
    {
        public TelemetryMetricDefinition()
        {
            this.AllowedTagKeys = new HashSet<string>();
        }

        public string Name { get; set; }

        public TelemetryMetricType MetricType { get; set; }

        public string Unit { get; set; }

        public string Description { get; set; }

        public ISet<string> AllowedTagKeys { get; private set; }
    }
}
