using System;
using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.NoOp
{
    public sealed class NoOpTelemetryLogHook : ITelemetryLogHook
    {
        public static readonly NoOpTelemetryLogHook Instance = new NoOpTelemetryLogHook();

        private NoOpTelemetryLogHook()
        {
        }

        public void RegisterSink(ITelemetryLogSink sink)
        {
        }

        public TelemetryLogContext GetCurrentContext()
        {
            return new TelemetryLogContext();
        }

        public void Enrich(IDictionary<string, object> properties)
        {
        }

        public void Log(
            TelemetryLogLevel level,
            string message,
            Exception exception = null,
            IDictionary<string, object> properties = null)
        {
        }
    }
}
