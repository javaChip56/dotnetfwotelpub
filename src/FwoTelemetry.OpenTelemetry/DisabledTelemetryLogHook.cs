using System;
using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class DisabledTelemetryLogHook : ITelemetryLogHook
    {
        public static readonly DisabledTelemetryLogHook Instance = new DisabledTelemetryLogHook();

        private DisabledTelemetryLogHook()
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
