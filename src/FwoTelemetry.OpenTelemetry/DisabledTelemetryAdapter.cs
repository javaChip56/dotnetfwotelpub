using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class DisabledTelemetryAdapter : ITelemetryAdapter
    {
        public static readonly DisabledTelemetryAdapter Instance = new DisabledTelemetryAdapter();

        private DisabledTelemetryAdapter()
        {
        }

        public ITelemetryPropagator Propagator
        {
            get { return DisabledTelemetryPropagator.Instance; }
        }

        public ITelemetryLogHook Logging
        {
            get { return DisabledTelemetryLogHook.Instance; }
        }

        public bool ForceFlush(int timeoutMilliseconds)
        {
            return true;
        }

        public ITelemetrySpan StartSpan(
            string name,
            TelemetrySpanKind kind = TelemetrySpanKind.Internal,
            IDictionary<string, object> attributes = null,
            TelemetryPropagationContext parentContext = null)
        {
            return DisabledTelemetrySpan.Instance;
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
