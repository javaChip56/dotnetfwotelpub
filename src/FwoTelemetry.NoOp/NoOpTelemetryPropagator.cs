using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.NoOp
{
    public sealed class NoOpTelemetryPropagator : ITelemetryPropagator
    {
        public static readonly NoOpTelemetryPropagator Instance = new NoOpTelemetryPropagator();

        private NoOpTelemetryPropagator()
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
