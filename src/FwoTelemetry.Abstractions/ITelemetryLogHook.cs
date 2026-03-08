using System;
using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public interface ITelemetryLogHook
    {
        void RegisterSink(ITelemetryLogSink sink);

        TelemetryLogContext GetCurrentContext();

        void Enrich(IDictionary<string, object> properties);

        void Log(
            TelemetryLogLevel level,
            string message,
            Exception exception = null,
            IDictionary<string, object> properties = null);
    }
}
