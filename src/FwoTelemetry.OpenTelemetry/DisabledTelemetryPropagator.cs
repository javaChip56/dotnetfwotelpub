using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class DisabledTelemetryPropagator : ITelemetryPropagator
    {
        public static readonly DisabledTelemetryPropagator Instance = new DisabledTelemetryPropagator();

        private DisabledTelemetryPropagator()
        {
        }

        public void Inject(IDictionary<string, string> carrier)
        {
        }

        public TelemetryPropagationContext Extract(IDictionary<string, string> carrier)
        {
            return new TelemetryPropagationContext();
        }
    }
}
