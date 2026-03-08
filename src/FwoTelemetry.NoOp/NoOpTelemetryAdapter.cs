using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.NoOp
{
    public sealed class NoOpTelemetryAdapter : ITelemetryAdapter
    {
        public static readonly NoOpTelemetryAdapter Instance = new NoOpTelemetryAdapter();

        private NoOpTelemetryAdapter()
        {
        }

        public ITelemetryPropagator Propagator
        {
            get { return NoOpTelemetryPropagator.Instance; }
        }

        public ITelemetryLogHook Logging
        {
            get { return NoOpTelemetryLogHook.Instance; }
        }

        public ITelemetrySpan StartSpan(
            string name,
            TelemetrySpanKind kind = TelemetrySpanKind.Internal,
            IDictionary<string, object> attributes = null,
            TelemetryPropagationContext parentContext = null)
        {
            return NoOpTelemetrySpan.Instance;
        }

        public void IncrementCounter(
            string name,
            long value = 1,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null)
        {
        }

        public void RecordHistogram(
            string name,
            double value,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null)
        {
        }

        public void Dispose()
        {
        }
    }
}
