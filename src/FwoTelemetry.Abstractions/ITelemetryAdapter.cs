using System;
using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public interface ITelemetryAdapter : IDisposable
    {
        ITelemetryPropagator Propagator { get; }

        ITelemetryLogHook Logging { get; }

        bool ForceFlush(int timeoutMilliseconds);

        ITelemetrySpan StartSpan(
            string name,
            TelemetrySpanKind kind = TelemetrySpanKind.Internal,
            IDictionary<string, object> attributes = null,
            TelemetryPropagationContext parentContext = null);

        void IncrementCounter(
            string name,
            long value = 1,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null);

        void RecordHistogram(
            string name,
            double value,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null);
    }
}
