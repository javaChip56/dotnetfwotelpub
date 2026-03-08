using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public interface ITelemetryPropagator
    {
        void Inject(IDictionary<string, string> carrier);

        TelemetryPropagationContext Extract(IDictionary<string, string> carrier);
    }
}
